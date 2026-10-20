using System.Collections.Generic;

namespace FirstChair.Sim
{
    /// <summary>
    /// The deterministic riding sim. It advances only in fixed steps of 1/120 s, and the state after a step
    /// depends only on the previous state, that step's <see cref="SimInput"/>, the <see cref="RunSetup"/>,
    /// the <see cref="Heightmap"/>, the <see cref="SnowMap"/> and the <see cref="Tuning"/>. It reads no clock.
    /// </summary>
    /// <remarks>
    /// <para><b>Input on snow.</b> Steering is <see cref="SimInput.CombinedSteering"/>: the analog value plus full lock
    /// for the left and right buttons. Back brakes. Forward tucks on slopes; on flats (gradient below
    /// <see cref="Tuning.FlatGradientMax"/>) it poles while the rider is slower than
    /// <see cref="Tuning.PoleSpeedMax"/> and tucks at or above it. Back wins over forward. Holding jump loads a pop;
    /// releasing it pops. Grab does nothing on snow.</para>
    /// <para><b>Input in the air.</b> Steering spins: <see cref="Tuning.SpinRate"/> * steering / 127, turning
    /// <see cref="RiderState.Heading"/> the way the same steering turns it on snow. Forward flips forward and back flips
    /// backward at <see cref="Tuning.FlipRate"/>; both together cancel. Rotation lasts only while held. Holding grab turns
    /// the direction into a grab and stops all rotation: forward = tip, back = tail, steering at or past
    /// <see cref="Tuning.GrabSteeringThreshold"/> of full lock left = Japan or right = safety, none = mute (forward wins
    /// over back, and both over left and right).</para>
    /// <para><b>Reset.</b> On the step <see cref="SimButtons.Reset"/> goes down (held now, not in the previous input) the
    /// rider is put at rest at the last safe spot (<see cref="RiderState.SafePosition"/>, facing
    /// <see cref="RiderState.SafeHeading"/>), from any state, and <see cref="SimEventKind.Reset"/> is raised. That step
    /// does nothing else. The safe spot is sampled after every step that ends on snow, uncrashed and on the
    /// heightmap, so it is part of the state and a reset replays exactly.</para>
    /// <para><b>One step on snow, in order:</b></para>
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
    /// <item><description>Pop: while jump is held, count the load up to <see cref="Tuning.PopLoadTime"/>. On the first
    /// step without it, add PopSpeedMin + (PopSpeedMax - PopSpeedMin) * load / PopLoadTime along the surface normal
    /// (<see cref="Tuning.PopSpeedMin"/>, <see cref="Tuning.PopSpeedMax"/>) and leave the snow.</description></item>
    /// <item><description>Move X and Z by the velocity (semi-implicit Euler). Where the velocity alone would carry the
    /// rider (old Y + velocity.Y * step) is compared with the terrain at the new X and Z: if the ground has fallen away
    /// faster than <see cref="Tuning.TakeoffDropSpeed"/>, or the rider popped, the rider takes off at that height and
    /// <see cref="SimEventKind.Takeoff"/> is raised. Otherwise Y is set to the terrain height. Nothing here knows where
    /// kickers are: any shape that falls away fast enough launches.</description></item>
    /// </list>
    /// <para><b>One step in the air:</b> apply the rotation or grab input, add gravity (no drag), move, and count the
    /// height above the terrain below toward <see cref="RiderState.MaxHeight"/>. The rider touches down when they are no
    /// longer above the terrain.</para>
    /// <para><b>Touchdown.</b> The neutral skis follow the snow below (the rider spots the landing), so the angle between
    /// the skis and the slope is the unfinished flip: the flip past its nearest whole turn. The unfinished spin is the
    /// spin past its nearest half turn (a switch landing is fine). The landing angle is the larger of the two. At or
    /// below <see cref="Tuning.CleanLandingAngle"/> it is clean, then <see cref="Tuning.SolidLandingAngle"/> solid, then
    /// <see cref="Tuning.SketchyLandingAngle"/> sketchy: the velocity into the slope is absorbed, the rest rides on, and
    /// <see cref="SimEventKind.Land"/> is raised. Past the sketchy limit it is a crash, raising
    /// <see cref="SimEventKind.Crash"/>: over-rotation when the larger residual points the way that axis was rotating,
    /// under-rotation otherwise.</para>
    /// <para><b>Crashed:</b> the rider lies still and riding input is ignored; after
    /// <see cref="Tuning.CrashResetDelay"/> the rider is reset as above.</para>
    /// <para>Every step then stores the input, advances the tick, raises <see cref="SimEventKind.LeftHeightmap"/> if the
    /// rider just left the map, and samples the safe spot.</para>
    /// <para>A new run faces down the fall line at the start point (+Z on level ground) and starts at rest.</para>
    /// <para>Bump <see cref="Version"/> whenever riding behaviour changes; old recordings then report a
    /// version mismatch instead of replaying into a different ride.</para>
    /// <para>Not thread-safe: one instance per ride, stepped from one thread. Instances share no state,
    /// and a <see cref="Heightmap"/> or <see cref="SnowMap"/> may be shared between them. <see cref="Step"/>
    /// allocates nothing: event payloads are fields of the <see cref="SimEvent"/> value.</para>
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
        /// brake, poling and snow types (Story 002). Version 3: pops, takeoff, spins, flips, grabs, graded landings,
        /// crashes and reset (Story 004).
        /// </summary>
        public const ushort Version = 3;

        /// <summary>Fixed steps per simulated second.</summary>
        public const int StepsPerSecond = 120;

        /// <summary>Length of one step, seconds: 1 / <see cref="StepsPerSecond"/>.</summary>
        public static readonly Fix64 StepSeconds = Fix64.FromRatio(1, StepsPerSecond);

        private const int EventCapacity = 8;

        private static readonly Fix64 Half = Fix64.FromRatio(1, 2);

        private readonly Heightmap _heightmap;
        private readonly SnowMap _snowMap;
        private readonly Tuning _tuning;
        private readonly Fix64 _popLoadSteps;
        private readonly Fix64 _crashResetSteps;
        private readonly List<SimEvent> _events = new List<SimEvent>(EventCapacity);
        private RiderState _state;

        /// <summary>
        /// Starts a run: copies <paramref name="setup"/> and <paramref name="tuning"/>, and places the rider
        /// at rest on the surface at (<see cref="RunSetup.StartX"/>, <see cref="RunSetup.StartZ"/>), facing down
        /// the fall line. The start is the first safe spot.
        /// </summary>
        /// <param name="setup">The run setup, copied once.</param>
        /// <param name="heightmap">The terrain.</param>
        /// <param name="snowMap">The snow types, loaded for <paramref name="heightmap"/>.</param>
        /// <param name="tuning">The riding numbers, copied once.</param>
        /// <exception cref="System.ArgumentException"><see cref="Tuning.MaxEdgeAngle"/> or <see cref="Tuning.PopLoadTime"/>
        /// is not positive; steps divide by them.</exception>
        public Simulation(RunSetup setup, Heightmap heightmap, SnowMap snowMap, Tuning tuning)
        {
            if (tuning.MaxEdgeAngle <= Fix64.Zero)
            {
                throw new System.ArgumentException("Tuning.MaxEdgeAngle must be positive.", nameof(tuning));
            }

            Fix64 popLoadSteps = tuning.PopLoadTime * Fix64.FromInt(StepsPerSecond);
            if (tuning.PopLoadTime <= Fix64.Zero || popLoadSteps <= Fix64.Zero)
            {
                throw new System.ArgumentException("Tuning.PopLoadTime must be positive.", nameof(tuning));
            }

            Setup = setup;
            _heightmap = heightmap;
            _snowMap = snowMap;
            _tuning = tuning;
            _popLoadSteps = popLoadSteps;
            _crashResetSteps = tuning.CrashResetDelay * Fix64.FromInt(StepsPerSecond);

            heightmap.Sample(setup.StartX, setup.StartZ, out Fix64 height, out Vec3 normal);
            _state.Position = new Vec3(setup.StartX, height, setup.StartZ);
            _state.Velocity = Vec3.Zero;
            _state.Heading = Fix64.Atan2(normal.X, normal.Z);
            _state.Snow = snowMap.TypeAt(setup.StartX, setup.StartZ);
            _state.OutsideHeightmap = !heightmap.Contains(setup.StartX, setup.StartZ);
            _state.Rng = new SimRandom(setup.Seed);
            _state.SafePosition = _state.Position;
            _state.SafeHeading = _state.Heading;
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
            int tick = _state.Tick + 1;
            bool resetTapped = (input.Buttons & SimButtons.Reset) != 0 && (_state.LastInput.Buttons & SimButtons.Reset) == 0;

            if (resetTapped)
            {
                ResetToSafeSpot(tick);
            }
            else if (_state.Crashed)
            {
                StepCrashed(tick);
            }
            else if (_state.Airborne)
            {
                StepAir(input, tick);
            }
            else
            {
                StepSnow(input, tick);
            }

            _state.LastInput = input;
            _state.Tick = tick;

            bool outside = !_heightmap.Contains(_state.Position.X, _state.Position.Z);
            if (outside && !_state.OutsideHeightmap)
            {
                _events.Add(new SimEvent(tick, SimEventKind.LeftHeightmap));
            }

            _state.OutsideHeightmap = outside;
            if (!_state.Airborne && !_state.Crashed && !outside)
            {
                _state.SafePosition = _state.Position;
                _state.SafeHeading = _state.Heading;
            }
        }

        /// <summary>One step on the snow: steer, slide, pop, and take off where the ground falls away.</summary>
        private void StepSnow(SimInput input, int tick)
        {
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
            // At rest the velocity is already zero; testing speed first keeps a negative loss (bad tuning) from
            // dividing by zero.
            velocity = speed == Fix64.Zero || loss >= speed ? Vec3.Zero : velocity - velocity * (loss / speed);

            // Pop: holding jump loads it up to PopLoadTime; the first step without it pops along the surface normal.
            bool popped = false;
            if ((buttons & SimButtons.Jump) != 0)
            {
                if (Fix64.FromInt(_state.PopLoadTicks) < _popLoadSteps)
                {
                    _state.PopLoadTicks++;
                }
            }
            else if (_state.PopLoadTicks > 0)
            {
                Fix64 load = Fix64.Min(Fix64.FromInt(_state.PopLoadTicks) / _popLoadSteps, Fix64.One);
                velocity = velocity + normal * (_tuning.PopSpeedMin + (_tuning.PopSpeedMax - _tuning.PopSpeedMin) * load);
                _state.PopLoadTicks = 0;
                popped = true;
            }

            Fix64 x = _state.Position.X + velocity.X * dt;
            Fix64 z = _state.Position.Z + velocity.Z * dt;
            _heightmap.Sample(x, z, out Fix64 height, out _);

            // Where the velocity alone carries the rider. If the ground under it has fallen away faster than
            // TakeoffDropSpeed (a lip or a sharp crest), the snow no longer holds the rider: they fly.
            Fix64 flightY = _state.Position.Y + velocity.Y * dt;
            bool takeoff = popped || flightY - height > _tuning.TakeoffDropSpeed * dt;

            _state.Position = new Vec3(x, takeoff ? Fix64.Max(flightY, height) : height, z);
            _state.Velocity = velocity;
            _state.Heading = heading;
            _state.EdgeAngle = edge;
            _state.Skidding = skidding;
            _state.SlipAngle = slip;
            _state.Snow = snow;
            _state.Stance = stance;

            if (takeoff)
            {
                ClearAir();
                _state.Airborne = true;
                _state.PopLoadTicks = 0;
                _events.Add(SimEvent.Takeoff(tick, Fix64.Sqrt(Vec3.Dot(velocity, velocity))));
            }
        }

        /// <summary>One step in the air: rotate or grab, fall, and touch down when the terrain is reached.</summary>
        private void StepAir(SimInput input, int tick)
        {
            Fix64 dt = StepSeconds;
            SimButtons buttons = input.Buttons;
            bool forward = (buttons & SimButtons.Forward) != 0;
            bool back = (buttons & SimButtons.Back) != 0;
            Fix64 steering = Fix64.FromRatio(input.CombinedSteering, SimInput.SteeringMax);
            Fix64 heading = _state.Heading;
            GrabKind grab = GrabKind.None;

            if ((buttons & SimButtons.Grab) != 0)
            {
                grab = forward ? GrabKind.Tip
                    : back ? GrabKind.Tail
                    : steering <= -_tuning.GrabSteeringThreshold ? GrabKind.Japan
                    : steering >= _tuning.GrabSteeringThreshold ? GrabKind.Safety
                    : GrabKind.Mute;
            }
            else
            {
                Fix64 spin = _tuning.SpinRate * steering * dt;
                heading = Fix64.WrapAngle(heading + spin);
                _state.SpinAngle = _state.SpinAngle + spin;
                if (forward != back)
                {
                    Fix64 flip = _tuning.FlipRate * dt;
                    _state.FlipAngle = forward ? _state.FlipAngle + flip : _state.FlipAngle - flip;
                }
            }

            if (grab != GrabKind.None && grab == _state.Grab)
            {
                _state.GrabTicks++;
            }
            else
            {
                EndGrab();
                _state.Grab = grab;
                _state.GrabTicks = grab == GrabKind.None ? 0 : 1;
            }

            Vec3 velocity = _state.Velocity - new Vec3(Fix64.Zero, _tuning.Gravity * dt, Fix64.Zero);
            Vec3 position = _state.Position + velocity * dt;
            _heightmap.Sample(position.X, position.Z, out Fix64 height, out Vec3 normal);
            _state.AirTicks++;
            _state.Heading = heading;
            _state.Skidding = false;
            _state.SlipAngle = Fix64.Zero;

            if (position.Y > height)
            {
                _state.Position = position;
                _state.Velocity = velocity;
                _state.MaxHeight = Fix64.Max(_state.MaxHeight, position.Y - height);
                return;
            }

            Touchdown(new Vec3(position.X, height, position.Z), velocity, normal, tick);
        }

        /// <summary>Grades the landing on the terrain at <paramref name="position"/>; rides on or crashes.</summary>
        private void Touchdown(Vec3 position, Vec3 velocity, Vec3 normal, int tick)
        {
            EndGrab();

            Fix64 flipOff = PastNearest(_state.FlipAngle, Fix64.TwoPi);
            Fix64 spinOff = PastNearest(_state.SpinAngle, Fix64.Pi);
            bool flipWorse = Fix64.Abs(flipOff) >= Fix64.Abs(spinOff);
            Fix64 residual = flipWorse ? flipOff : spinOff;
            Fix64 rotation = flipWorse ? _state.FlipAngle : _state.SpinAngle;
            Fix64 angle = Fix64.Abs(residual);
            var air = new AirSummary(
                _state.AirTicks, _state.MaxHeight, _state.SpinAngle, _state.FlipAngle, _state.BestGrab, _state.BestGrabTicks, angle);

            _state.Position = position;
            _state.Airborne = false;
            _state.Snow = _snowMap.TypeAt(position.X, position.Z);
            ClearAir();

            if (angle > _tuning.SketchyLandingAngle)
            {
                // A residual is non-zero only when its rotation is, so the signs always compare.
                CrashCause cause = (residual > Fix64.Zero) == (rotation > Fix64.Zero) ? CrashCause.OverRotation : CrashCause.UnderRotation;
                _state.Velocity = Vec3.Zero;
                _state.Crashed = true;
                _state.CrashTicks = 0;
                _events.Add(SimEvent.Crash(tick, cause, air));
                return;
            }

            // The snow absorbs the velocity into the slope; what runs along it rides on.
            Fix64 into = Vec3.Dot(velocity, normal);
            _state.Velocity = into < Fix64.Zero ? velocity - normal * into : velocity;
            LandingGrade grade = angle <= _tuning.CleanLandingAngle ? LandingGrade.Clean
                : angle <= _tuning.SolidLandingAngle ? LandingGrade.Solid
                : LandingGrade.Sketchy;
            _events.Add(SimEvent.Land(tick, grade, air));
        }

        /// <summary>One crashed step: lie still, and reset once <see cref="Tuning.CrashResetDelay"/> has passed.</summary>
        private void StepCrashed(int tick)
        {
            _state.CrashTicks++;
            if (Fix64.FromInt(_state.CrashTicks) >= _crashResetSteps)
            {
                ResetToSafeSpot(tick);
            }
        }

        /// <summary>Puts the rider at rest at the last safe spot and raises <see cref="SimEventKind.Reset"/>.</summary>
        private void ResetToSafeSpot(int tick)
        {
            _state.Position = _state.SafePosition;
            _state.Velocity = Vec3.Zero;
            _state.Heading = _state.SafeHeading;
            _state.EdgeAngle = Fix64.Zero;
            _state.Skidding = false;
            _state.SlipAngle = Fix64.Zero;
            _state.Stance = Stance.Stand;
            _state.Snow = _snowMap.TypeAt(_state.SafePosition.X, _state.SafePosition.Z);
            _state.Airborne = false;
            _state.Crashed = false;
            _state.CrashTicks = 0;
            _state.PopLoadTicks = 0;
            ClearAir();
            _events.Add(new SimEvent(tick, SimEventKind.Reset));
        }

        /// <summary>Keeps the grab that just ended if it was held longer than the best so far.</summary>
        private void EndGrab()
        {
            if (_state.GrabTicks > _state.BestGrabTicks)
            {
                _state.BestGrab = _state.Grab;
                _state.BestGrabTicks = _state.GrabTicks;
            }
        }

        /// <summary>Zeroes the flight fields.</summary>
        private void ClearAir()
        {
            _state.AirTicks = 0;
            _state.MaxHeight = Fix64.Zero;
            _state.SpinAngle = Fix64.Zero;
            _state.FlipAngle = Fix64.Zero;
            _state.Grab = GrabKind.None;
            _state.GrabTicks = 0;
            _state.BestGrab = GrabKind.None;
            _state.BestGrabTicks = 0;
        }

        /// <summary>
        /// The signed rotation past the nearest whole multiple of <paramref name="period"/>, in [-period/2, period/2).
        /// </summary>
        private static Fix64 PastNearest(Fix64 angle, Fix64 period)
        {
            int turns = ((angle + period * Half) / period).FloorToInt();
            return angle - period * Fix64.FromInt(turns);
        }
    }
}
