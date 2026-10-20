using System.Collections.Generic;

namespace FirstChair.Sim
{
    /// <summary>
    /// The deterministic riding sim. It advances only in fixed steps of 1/120 s, and the state after a step
    /// depends only on the previous state, that step's <see cref="SimInput"/>, the <see cref="RunSetup"/>,
    /// the <see cref="Heightmap"/> and the <see cref="Tuning"/>. It reads no clock.
    /// </summary>
    /// <remarks>
    /// <para><b>One step, in order:</b> sample the surface under the rider; add gravity projected onto the
    /// surface; apply drag; remove any velocity into the surface; move X and Z by the velocity
    /// (semi-implicit Euler); set Y to the terrain height; store the input; advance the tick; raise
    /// <see cref="SimEventKind.LeftHeightmap"/> if the rider just left the map.</para>
    /// <para>In this story the input is recorded and stored in the state but does not move the rider:
    /// carving, tuck, brake and poling arrive in Story 002.</para>
    /// <para>Bump <see cref="Version"/> whenever riding behaviour changes; old recordings then report a
    /// version mismatch instead of replaying into a different ride.</para>
    /// <para>Not thread-safe: one instance per ride, stepped from one thread. Instances share no state,
    /// and a <see cref="Heightmap"/> may be shared between them. <see cref="Step"/> allocates nothing.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var simulation = new Simulation(setup, heightmap, Tuning.Default);
    /// var recording = new Recording(setup, heightmap, Tuning.Default);
    /// // Each frame, run as many steps as the elapsed time needs:
    /// SimInput input = SimInput.Pack(buttons, steering);
    /// recording.Append(input);
    /// simulation.Step(input);
    /// RiderState state = simulation.State;
    /// </code>
    /// </example>
    public sealed class Simulation
    {
        /// <summary>The sim version. Bump it whenever riding behaviour changes.</summary>
        public const ushort Version = 1;

        /// <summary>Fixed steps per simulated second.</summary>
        public const int StepsPerSecond = 120;

        /// <summary>Length of one step, seconds: 1 / <see cref="StepsPerSecond"/>.</summary>
        public static readonly Fix64 StepSeconds = Fix64.FromRatio(1, StepsPerSecond);

        private const int EventCapacity = 8;

        private readonly Heightmap _heightmap;
        private readonly Tuning _tuning;
        private readonly List<SimEvent> _events = new List<SimEvent>(EventCapacity);
        private RiderState _state;

        /// <summary>
        /// Starts a run: copies <paramref name="setup"/> and <paramref name="tuning"/>, and places the rider
        /// at rest on the surface at (<see cref="RunSetup.StartX"/>, <see cref="RunSetup.StartZ"/>).
        /// </summary>
        /// <param name="setup">The run setup, copied once.</param>
        /// <param name="heightmap">The terrain.</param>
        /// <param name="tuning">The riding numbers, copied once.</param>
        public Simulation(RunSetup setup, Heightmap heightmap, Tuning tuning)
        {
            Setup = setup;
            _heightmap = heightmap;
            _tuning = tuning;

            heightmap.Sample(setup.StartX, setup.StartZ, out Fix64 height, out _);
            _state.Position = new Vec3(setup.StartX, height, setup.StartZ);
            _state.Velocity = Vec3.Zero;
            _state.OutsideHeightmap = !heightmap.Contains(setup.StartX, setup.StartZ);
            _state.Rng = new SimRandom(setup.Seed);
        }

        /// <summary>The run setup this simulation was started with.</summary>
        public RunSetup Setup { get; }

        /// <summary>A copy of the current state.</summary>
        public RiderState State => _state;

        /// <summary>The events raised by the last <see cref="Step"/>; cleared at the start of each step.</summary>
        public IReadOnlyList<SimEvent> Events => _events;

        /// <summary>Advances the ride by one fixed step of 1/120 s.</summary>
        /// <param name="input">This step's input.</param>
        public void Step(SimInput input)
        {
            _events.Clear();
            Fix64 dt = StepSeconds;

            _heightmap.Sample(_state.Position.X, _state.Position.Z, out _, out Vec3 normal);

            // Gravity projected onto the surface: g * (n.y * n.x, n.y * n.y - 1, n.y * n.z).
            Fix64 normalGravity = _tuning.Gravity * normal.Y;
            var acceleration = new Vec3(
                normalGravity * normal.X,
                normalGravity * normal.Y - _tuning.Gravity,
                normalGravity * normal.Z);

            Vec3 velocity = _state.Velocity + acceleration * dt;
            velocity = velocity - velocity * (_tuning.SlideDrag * dt);
            velocity = velocity - normal * Vec3.Dot(velocity, normal);

            Fix64 x = _state.Position.X + velocity.X * dt;
            Fix64 z = _state.Position.Z + velocity.Z * dt;
            _heightmap.Sample(x, z, out Fix64 height, out _);

            _state.Position = new Vec3(x, height, z);
            _state.Velocity = velocity;
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
