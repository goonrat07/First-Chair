using System.Collections.Generic;

namespace FirstChair.Sim
{
    /// <summary>
    /// The deterministic riding sim. It advances only in fixed steps of 1/120 s, and the state after a step
    /// depends only on the previous state, that step's <see cref="SimInput"/>, the <see cref="RunSetup"/>,
    /// the <see cref="Heightmap"/>, the <see cref="SnowMap"/> and the <see cref="Tuning"/>. It reads no clock.
    /// </summary>
    /// <remarks>
    /// <para><b>Input.</b> Steering is <see cref="SimInput.CombinedSteering"/>: the analog value plus full lock
    /// for the left and right buttons. Back brakes. Forward tucks on slopes; on flats (gradient below
    /// <see cref="Tuning.FlatGradientMax"/>) it poles while the rider is slower than
    /// <see cref="Tuning.PoleSpeedMax"/> and tucks at or above it. Back wins over forward. Jump and grab are
    /// recorded but not read until Story 004.</para>
    /// <para><b>One step, in order:</b></para>
    /// <list type="number">
    /// <item><description>Sample the surface normal and the snow type under the rider, and choose the
    /// <see cref="Stance"/>.</description></item>
    /// <item><description>Move the edge angle toward steering / 127 * <see cref="Tuning.MaxEdgeAngle"/> at
    /// <see cref="Tuning.EdgeRate"/>, then turn the heading at <see cref="Tuning.TurnRate"/> scaled by
    /// edge / MaxEdgeAngle (and by <see cref="Tuning.TuckTurnFactor"/> in a tuck).</description></item>
    /// <item><description>Add gravity projected onto the surface.</description></item>
    /// <item><description>Split the velocity along and across the skis (the heading projected onto the surface).
    /// The edges cancel up to the snow's grip * step of sideways velocity. If that is all of it, the skis carve:
    /// the velocity follows them, losing only the second-order sliver of each step's turn. Otherwise they skid:
    /// the rest of the sideways velocity stays, and the slip angle is the angle left between the velocity and the
    /// skis.</description></item>
    /// <item><description>Pole: add <see cref="Tuning.PoleAcceleration"/> along the skis, up to
    /// <see cref="Tuning.PoleSpeedMax"/>.</description></item>
    /// <item><description>Slow the rider by snow drag (the snow's drag * gravity * cos slope), air drag
    /// (<see cref="Tuning.AirDrag"/> * speed^2, times <see cref="Tuning.TuckAirDragFactor"/> in a tuck), skid drag
    /// (<see cref="Tuning.SkidDrag"/> * slip angle * speed) and the brake
    /// (<see cref="Tuning.BrakeDeceleration"/>), together and never past a stop.</description></item>
    /// <item><description>Move X and Z by the velocity (semi-implicit Euler), set Y to the terrain height, store
    /// the state, advance the tick, and raise <see cref="SimEventKind.LeftHeightmap"/> if the rider just left the
    /// map.</description></item>
    /// </list>
    /// <para>A new run faces down the fall line at the start point (+Z on level ground) and starts at rest.</para>
    /// <para>Bump <see cref="Version"/> whenever riding behaviour changes; old recordings then report a
    /// version mismatch instead of replaying into a different ride.</para>
    /// <para>Not thread-safe: one instance per ride, stepped from one thread. Instances share no state,
    /// and a <see cref="Heightmap"/> or <see cref="SnowMap"/> may be shared between them. <see cref="Step"/>
    /// allocates nothing.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var simulation = new Simulation(setup, heightmap, snowMap, Tuning.Default);
    /// var recording = new Recording(setup, heightmap, snowMap, Tuning.Default);
    /// // Each frame, run as many steps as the elapsed time needs:
    /// SimInput input = SimInput.Pack(buttons, steering);
    /// recording.Append(input);
    /// simulation.Step(input);
    /// RiderState state = simulation.State;
    /// </code>
    /// </example>
    public sealed class Simulation
    {
        /// <summary>
        /// The sim version. Bump it whenever riding behaviour changes. Version 2: carving, skidding, tuck,
        /// brake, poling and snow types (Story 002).
        /// </summary>
        public const ushort Version = 2;

        /// <summary>Fixed steps per simulated second.</summary>
        public const int StepsPerSecond = 120;

        /// <summary>Length of one step, seconds: 1 / <see cref="StepsPerSecond"/>.</summary>
        public static readonly Fix64 StepSeconds = Fix64.FromRatio(1, StepsPerSecond);

        private const int EventCapacity = 8;

        private readonly Heightmap _heightmap;
        private readonly SnowMap _snowMap;
        private readonly Tuning _tuning;
        private readonly List<SimEvent> _events = new List<SimEvent>(EventCapacity);
        private RiderState _state;

        /// <summary>
        /// Starts a run: copies <paramref name="setup"/> and <paramref name="tuning"/>, and places the rider
        /// at rest on the surface at (<see cref="RunSetup.StartX"/>, <see cref="RunSetup.StartZ"/>), facing down
        /// the fall line.
        /// </summary>
        /// <param name="setup">The run setup, copied once.</param>
        /// <param name="heightmap">The terrain.</param>
        /// <param name="snowMap">The snow types, loaded for <paramref name="heightmap"/>.</param>
        /// <param name="tuning">The riding numbers, copied once.</param>
        public Simulation(RunSetup setup, Heightmap heightmap, SnowMap snowMap, Tuning tuning)
        {
            Setup = setup;
            _heightmap = heightmap;
            _snowMap = snowMap;
            _tuning = tuning;

            heightmap.Sample(setup.StartX, setup.StartZ, out Fix64 height, out Vec3 normal);
            _state.Position = new Vec3(setup.StartX, height, setup.StartZ);
            _state.Velocity = Vec3.Zero;
            _state.Heading = Fix64.Atan2(normal.X, normal.Z);
            _state.Snow = snowMap.TypeAt(setup.StartX, setup.StartZ);
            _state.OutsideHeightmap = !heightmap.Contains(setup.StartX, setup.StartZ);
            _state.Rng = new SimRandom(setup.Seed);
        }

        /// <summary>The run setup this simulation was started with.</summary>
        public RunSetup Setup { get; }

        /// <summary>A copy of the current state.</summary>
        public RiderState State => _state;

        /// <summary>The events raised by the last <see cref="Step"/>; cleared at the start of each step.</summary>
        public IReadOnlyList<SimEvent> Events => _events;

        /// <summary>Advances the ride by one fixed step of 1/120 s (see the class remarks for the order).</summary>
        /// <param name="input">This step's input.</param>
        public void Step(SimInput input)
        {
            _events.Clear();
            Fix64 dt = StepSeconds;

            _heightmap.Sample(_state.Position.X, _state.Position.Z, out _, out Vec3 normal);
            SnowType snow = _snowMap.TypeAt(_state.Position.X, _state.Position.Z);
            Vec3 velocity = _state.Velocity;
            Fix64 speed = Fix64.Sqrt(Vec3.Dot(velocity, velocity));

            // A flat has gradient sqrt(nx^2 + nz^2) / ny below FlatGradientMax; compared squared, without division.
            Fix64 flatLimit = _tuning.FlatGradientMax * normal.Y;
            bool onFlat = normal.X * normal.X + normal.Z * normal.Z < flatLimit * flatLimit;
            SimButtons buttons = input.Buttons;
            Stance stance = (buttons & SimButtons.Back) != 0 ? Stance.Brake
                : (buttons & SimButtons.Forward) == 0 ? Stance.Stand
                : onFlat && speed < _tuning.PoleSpeedMax ? Stance.Pole
                : Stance.Tuck;

            // Edge toward the steering target, then turn the heading by the edge.
            Fix64 edgeTarget = _tuning.MaxEdgeAngle * Fix64.FromRatio(input.CombinedSteering, SimInput.SteeringMax);
            Fix64 edgeStep = _tuning.EdgeRate * dt;
            Fix64 edge = _state.EdgeAngle < edgeTarget
                ? Fix64.Min(_state.EdgeAngle + edgeStep, edgeTarget)
                : Fix64.Max(_state.EdgeAngle - edgeStep, edgeTarget);
            Fix64 turnRate = _tuning.TurnRate * edge / _tuning.MaxEdgeAngle;
            if (stance == Stance.Tuck)
            {
                turnRate = turnRate * _tuning.TuckTurnFactor;
            }

            Fix64 heading = Fix64.WrapAngle(_state.Heading + turnRate * dt);

            // Gravity projected onto the surface: g * (n.y * n.x, n.y * n.y - 1, n.y * n.z).
            Fix64 normalGravity = _tuning.Gravity * normal.Y;
            velocity = velocity + new Vec3(
                normalGravity * normal.X,
                normalGravity * normal.Y - _tuning.Gravity,
                normalGravity * normal.Z) * dt;

            // The skis on the surface: forward is the heading's level direction projected onto it; side points right.
            var level = new Vec3(Fix64.Sin(heading), Fix64.Zero, Fix64.Cos(heading));
            Vec3 forward = level - normal * Vec3.Dot(level, normal);
            forward = forward * (Fix64.One / Fix64.Sqrt(Vec3.Dot(forward, forward)));
            Vec3 side = Vec3.Cross(normal, forward);
            Fix64 along = Vec3.Dot(velocity, forward);
            Fix64 across = Vec3.Dot(velocity, side);

            // Grip: the edges cancel up to grip * dt of sideways velocity. All of it is a carve; less is a skid.
            Fix64 grip = _tuning.Grip(snow) * dt;
            bool skidding = Fix64.Abs(across) > grip;
            across = !skidding ? Fix64.Zero : (across > Fix64.Zero ? across - grip : across + grip);
            Fix64 slip = skidding ? Fix64.Atan2(Fix64.Abs(across), Fix64.Abs(along)) : Fix64.Zero;

            if (stance == Stance.Pole && along < _tuning.PoleSpeedMax)
            {
                along = Fix64.Min(along + _tuning.PoleAcceleration * dt, _tuning.PoleSpeedMax);
            }

            velocity = forward * along + side * across;

            // Everything that slows the rider, together; it can stop the rider but never push them backward.
            speed = Fix64.Sqrt(Vec3.Dot(velocity, velocity));
            Fix64 airDrag = stance == Stance.Tuck ? _tuning.AirDrag * _tuning.TuckAirDragFactor : _tuning.AirDrag;
            Fix64 deceleration = _tuning.Drag(snow) * normalGravity + airDrag * speed * speed + _tuning.SkidDrag * slip * speed;
            if (stance == Stance.Brake)
            {
                deceleration = deceleration + _tuning.BrakeDeceleration;
            }

            Fix64 loss = deceleration * dt;
            velocity = loss >= speed ? Vec3.Zero : velocity - velocity * (loss / speed);

            Fix64 x = _state.Position.X + velocity.X * dt;
            Fix64 z = _state.Position.Z + velocity.Z * dt;
            _heightmap.Sample(x, z, out Fix64 height, out _);

            _state.Position = new Vec3(x, height, z);
            _state.Velocity = velocity;
            _state.Heading = heading;
            _state.EdgeAngle = edge;
            _state.Skidding = skidding;
            _state.SlipAngle = slip;
            _state.Snow = snow;
            _state.Stance = stance;
            _state.LastInput = input;
            _state.Tick++;

            bool outside = !_heightmap.Contains(x, z);
            if (outside && !_state.OutsideHeightmap)
            {
                _events.Add(new SimEvent(_state.Tick, SimEventKind.LeftHeightmap));
            }

            _state.OutsideHeightmap = outside;
        }
    }
}
