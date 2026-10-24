using System.Collections.Generic;

namespace FirstChair.Sim
{
    /// <summary>
    /// The deterministic riding sim. It advances only in fixed steps of 1/120 s, and the state after a step
    /// depends only on the previous state, that step's <see cref="SimInput"/>, the <see cref="RunSetup"/>,
    /// the <see cref="Heightmap"/>, the <see cref="SnowMap"/>, the <see cref="Forest"/>, the <see cref="RailSet"/> and the
    /// <see cref="Tuning"/>. It reads no clock.
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
    /// (<see cref="Tuning.BrakeDeceleration"/>), together and never past a stop. Flow cuts the snow and air drag (see
    /// Trees and Flow).</description></item>
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
    /// <para><b>Switch.</b> At takeoff the rider is riding switch when the velocity runs against the skis (its dot with the
    /// heading's direction on the surface is below zero). <see cref="RiderState.SwitchTakeoff"/> keeps it for the flight
    /// and <see cref="AirSummary.Switch"/> reports it with the Flow at touchdown (<see cref="AirSummary.Flow"/>); trick
    /// names and points are built from the event outside the step (<see cref="TrickScoring"/>, <see cref="RunScore"/>).</para>
    /// <para><b>Crashed:</b> the rider lies still and riding input is ignored; after
    /// <see cref="Tuning.CrashResetDelay"/> the rider is reset as above.</para>
    /// <para><b>Trees.</b> After every step that moves the rider, on snow or in the air, and does not end in a landing
    /// crash, the path the rider covered that step, seen from above, is checked against each tree of the
    /// <see cref="Forest"/> in index order. Distances run from the trunk's centre to the closest point of the path; the
    /// rider is a circle of <see cref="Tuning.RiderRadius"/>, so the gap is distance - trunk radius - rider radius.</para>
    /// <list type="bullet">
    /// <item><description>Trunk: a gap at or below zero is a crash (<see cref="CrashCause.Tree"/>). The rider stops on
    /// the snow where the step began, Flow empties, and the crash runs as any other: lie still, then reset to the safe
    /// spot. No later tree is checked that step.</description></item>
    /// <item><description>Branches: entering the circle of branch radius + rider radius clips them once per pass: the
    /// velocity loses <see cref="Tuning.BranchSpeedLoss"/> of itself, Flow loses <see cref="Tuning.BranchFlowCost"/>,
    /// and <see cref="SimEventKind.BranchHit"/> is raised. Leaving the circle ends the pass.</description></item>
    /// <item><description>Near miss: a pass starts when the gap falls to <see cref="Tuning.NearMissDistance"/> or less
    /// and ends when it rises past it. It is judged once: at its closest approach (the first step whose gap is larger
    /// than the smallest so far) or when it ends, whichever is first. If the rider is then at least
    /// <see cref="Tuning.NearMissMinSpeed"/> fast, <see cref="SimEventKind.NearMiss"/> is raised with the smallest gap
    /// and Flow fills by NearMissFlowGainFar + (NearMissFlowGainClose - NearMissFlowGainFar) * (1 - gap /
    /// NearMissDistance), up to 1. A trunk hit before the judgement raises no near miss.</description></item>
    /// </list>
    /// <para><b>Flow</b> (<see cref="RiderState.Flow"/>, 0 to 1) starts empty. Once <see cref="Tuning.FlowDrainDelay"/>
    /// has passed since the last near miss it drains at <see cref="Tuning.FlowDrainRate"/> per second. On snow it
    /// multiplies snow and air drag by 1 - <see cref="Tuning.FlowDragCut"/> * Flow, and any crash empties it. Trick
    /// points scale by <see cref="Tuning.TrickMultiplier"/> of the Flow at touchdown (<see cref="TrickScoring"/>).</para>
    /// <para><b>Rails and boxes</b> (Story 006, <see cref="RailSet"/>). A rail is a straight line seen from above whose top
    /// follows the terrain at its height; the rider is on it within half its width plus
    /// <see cref="Tuning.GrindCaptureDistance"/> of the line. A grind starts (<see cref="SimEventKind.GrindStart"/>) when a
    /// rider in the air comes down through the top over the rail (the touchdown is graded as any landing and raises
    /// <see cref="SimEventKind.Land"/> first; past sketchy it is a crash), or when a rider on snow enters the footprint of a
    /// rail no higher than <see cref="Tuning.GrindStepUpHeight"/> moving along it at <see cref="Tuning.GrindMinSpeed"/> or
    /// more. Higher rails are not obstacles from the side. Entering at an angle sets the lean
    /// (<see cref="RiderState.Balance"/>) to <see cref="Tuning.GrindEntryLean"/> times the share of the level speed that
    /// crosses the rail.</para>
    /// <para><b>One step on a rail:</b> the lean changes by (<see cref="Tuning.GrindTipRate"/> * lean +
    /// <see cref="Tuning.GrindBalanceRate"/> * steering / 127 + <see cref="Tuning.GrindWobble"/> * r) * step, r from the run's
    /// random generator in [-1, 1); at 1 either way the rider falls off: <see cref="SimEventKind.GrindEnd"/>, then a
    /// <see cref="CrashCause.RailFall"/> crash on the snow below. Otherwise the rider slides along the rail's direction on the
    /// surface: gravity along it, slowed by <see cref="Tuning.GrindDrag"/> * gravity * cos slope and air drag, never past a
    /// stop. Past either end the grind ends (<see cref="SimEventKind.GrindEnd"/>): from a rail no higher than the step-up
    /// height the rider steps down onto the snow, from a higher one they fly (<see cref="SimEventKind.Takeoff"/>). Steering
    /// does not turn the skis on a rail, and no safe spot is sampled there.</para>
    /// <para>The per-tree pass flags are kept here, one entry per tree, sized when the simulation is built. The start and
    /// every reset re-read them at the rider's new position, so a rider put down inside a ring starts a pass already
    /// judged: no clip and no near miss until they leave it.</para>
    /// <para>Every step then updates the Flow drain, stores the input, advances the tick, raises <see cref="SimEventKind.LeftHeightmap"/> if the
    /// rider just left the map, and samples the safe spot.</para>
    /// <para>A new run faces down the fall line at the start point (+Z on level ground) and starts at rest.</para>
    /// <para>Bump <see cref="Version"/> whenever riding behaviour changes; old recordings then report a
    /// version mismatch instead of replaying into a different ride.</para>
    /// <para>Not thread-safe: one instance per ride, stepped from one thread. Instances share no state,
    /// and a <see cref="Heightmap"/>, <see cref="SnowMap"/> or <see cref="Forest"/> may be shared between them. <see cref="Step"/>
    /// allocates nothing: event payloads are fields of the <see cref="SimEvent"/> value.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var simulation = new Simulation(setup, heightmap, snowMap, forest, Tuning.Default);
    /// var recording = new Recording(setup, heightmap, snowMap, forest, Tuning.Default);
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
        /// crashes and reset (Story 004). Version 4: trees (trunk crashes, branch clips, near misses) and Flow
        /// (Story 005). Version 5: rails, boxes and grinds, and the switch flag and touchdown Flow in the flight summary
        /// (Story 006). Version 6: more grip and a slower turn, so key turns carve to 48 km/h (playtest retune, 2026-10-24).
        /// </summary>
        public const ushort Version = 6;

        /// <summary>Fixed steps per simulated second.</summary>
        public const int StepsPerSecond = 120;

        /// <summary>Length of one step, seconds: 1 / <see cref="StepsPerSecond"/>.</summary>
        public static readonly Fix64 StepSeconds = Fix64.FromRatio(1, StepsPerSecond);

        /// <summary>The longest a tuning duration may be, seconds: its step count must fit an int.</summary>
        private static readonly Fix64 MaxTuningSeconds = Fix64.FromInt(int.MaxValue / StepsPerSecond);

        /// <summary>The largest Flow trick bonus (see <see cref="TrickScoring"/> for the bound on a score).</summary>
        private static readonly Fix64 MaxFlowTrickBonus = Fix64.FromInt(10);

        /// <summary>
        /// The largest points coefficient (per half turn, flip, second, grab, grind second). With the factor and bonus
        /// limits and <see cref="TrickScoring"/>'s count and time caps, a score stays under about 8.8e8, inside Q32.32.
        /// </summary>
        private static readonly Fix64 MaxPointsCoefficient = Fix64.FromInt(10_000);

        /// <summary>The largest landing-quality factor (see <see cref="MaxPointsCoefficient"/>).</summary>
        private static readonly Fix64 MaxTrickFactor = Fix64.FromInt(10);

        /// <summary>Events one step can raise without trees; each tree adds at most a near miss and a branch hit.</summary>
        private const int BaseEventCapacity = 8;

        /// <summary>
        /// The largest <see cref="Tuning.RiderRadius"/> or <see cref="Tuning.NearMissDistance"/>, metres, so squared
        /// distances to trees fit Q32.32.
        /// </summary>
        private const int MaxTreeReachMetres = 1000;

        /// <summary>The largest grind rate, drag or lean factor: far above any sane value, far below where a step wraps.</summary>
        private const int MaxGrindRate = 1000;

        // Per-tree pass flags (see the class remarks).
        private const int PassNone = 0;
        private const int PassNear = 1;
        private const int PassJudged = 2;
        private const int PassBranches = 4;

        private static readonly Fix64 Half = Fix64.FromRatio(1, 2);

        private readonly Heightmap _heightmap;
        private readonly SnowMap _snowMap;
        private readonly Tuning _tuning;
        private readonly Fix64 _popLoadSteps;
        private readonly Fix64 _crashResetSteps;
        private readonly Fix64 _flowDrainSteps;
        private readonly Fix64 _flowDrainPerStep;
        private readonly List<SimEvent> _events;

        // One entry per tree, by index: centre, trunk reach (trunk + rider radius), branch reach (branch + rider radius),
        // the reach of every ring (a box test skips trees beyond it), the pass flags and the pass's smallest gap.
        private readonly Fix64[] _treeX;
        private readonly Fix64[] _treeZ;
        private readonly Fix64[] _trunkReach;
        private readonly Fix64[] _branchReach;
        private readonly Fix64[] _ringReach;
        private readonly int[] _treePass;
        private readonly Fix64[] _treeMinGap;

        // One entry per rail, by index: start, unit direction seen from above, length, height, reach (half width plus
        // the capture distance) and whether it is a box.
        private readonly Fix64[] _railX0;
        private readonly Fix64[] _railZ0;
        private readonly Fix64[] _railDirX;
        private readonly Fix64[] _railDirZ;
        private readonly Fix64[] _railLength;
        private readonly Fix64[] _railHeight;
        private readonly Fix64[] _railReach;
        private readonly bool[] _railBox;
        private RiderState _state;

        /// <summary>
        /// Starts a run: copies <paramref name="setup"/> and <paramref name="tuning"/>, and places the rider
        /// at rest on the surface at (<see cref="RunSetup.StartX"/>, <see cref="RunSetup.StartZ"/>), facing down
        /// the fall line. The start is the first safe spot. No trees: the overload that takes a <see cref="Forest"/> adds
        /// them.
        /// </summary>
        /// <param name="setup">The run setup, copied once.</param>
        /// <param name="heightmap">The terrain.</param>
        /// <param name="snowMap">The snow types, loaded for <paramref name="heightmap"/>.</param>
        /// <param name="tuning">The riding numbers, copied once.</param>
        /// <exception cref="System.ArgumentException"><paramref name="tuning"/> holds a value a ride cannot run on; the
        /// message names it. See <see cref="Validate"/>.</exception>
        public Simulation(RunSetup setup, Heightmap heightmap, SnowMap snowMap, Tuning tuning)
            : this(setup, heightmap, snowMap, Forest.Empty, tuning)
        {
        }

        /// <summary>
        /// Starts a run among trees: as the constructor without a forest, with <paramref name="forest"/>'s trees on the
        /// slope (see the class remarks).
        /// </summary>
        /// <param name="setup">The run setup, copied once.</param>
        /// <param name="heightmap">The terrain.</param>
        /// <param name="snowMap">The snow types, loaded for <paramref name="heightmap"/>.</param>
        /// <param name="forest">The trees; <see cref="Forest.Empty"/> for none.</param>
        /// <param name="tuning">The riding numbers, copied once.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="forest"/> is null.</exception>
        /// <exception cref="System.ArgumentException"><paramref name="tuning"/> holds a value a ride cannot run on; the
        /// message names it. See <see cref="Validate"/>.</exception>
        public Simulation(RunSetup setup, Heightmap heightmap, SnowMap snowMap, Forest forest, Tuning tuning)
            : this(setup, heightmap, snowMap, forest, RailSet.Empty, tuning)
        {
        }

        /// <summary>
        /// Starts a run among trees, rails and boxes: as the constructor with a forest, with <paramref name="rails"/> on the
        /// slope (see the class remarks).
        /// </summary>
        /// <param name="setup">The run setup, copied once.</param>
        /// <param name="heightmap">The terrain.</param>
        /// <param name="snowMap">The snow types, loaded for <paramref name="heightmap"/>.</param>
        /// <param name="forest">The trees; <see cref="Forest.Empty"/> for none.</param>
        /// <param name="rails">The rails and boxes; <see cref="RailSet.Empty"/> for none.</param>
        /// <param name="tuning">The riding numbers, copied once.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="forest"/> or <paramref name="rails"/> is null.</exception>
        /// <exception cref="System.ArgumentException"><paramref name="tuning"/> holds a value a ride cannot run on; the
        /// message names it. See <see cref="Validate"/>.</exception>
        public Simulation(RunSetup setup, Heightmap heightmap, SnowMap snowMap, Forest forest, RailSet rails, Tuning tuning)
        {
            Validate(tuning);
            if (forest == null)
            {
                throw new System.ArgumentNullException(nameof(forest));
            }

            if (rails == null)
            {
                throw new System.ArgumentNullException(nameof(rails));
            }

            Setup = setup;
            Forest = forest;
            Rails = rails;
            _heightmap = heightmap;
            _snowMap = snowMap;
            _tuning = tuning;
            _popLoadSteps = tuning.PopLoadTime * Fix64.FromInt(StepsPerSecond);
            _crashResetSteps = tuning.CrashResetDelay * Fix64.FromInt(StepsPerSecond);
            _flowDrainSteps = tuning.FlowDrainDelay * Fix64.FromInt(StepsPerSecond);
            _flowDrainPerStep = tuning.FlowDrainRate * StepSeconds;
            _events = new List<SimEvent>(BaseEventCapacity + 2 * forest.Count);

            int treeCount = forest.Count;
            _treeX = new Fix64[treeCount];
            _treeZ = new Fix64[treeCount];
            _trunkReach = new Fix64[treeCount];
            _branchReach = new Fix64[treeCount];
            _ringReach = new Fix64[treeCount];
            _treePass = new int[treeCount];
            _treeMinGap = new Fix64[treeCount];
            for (int i = 0; i < treeCount; i++)
            {
                SlopeTree tree = forest[i];
                _treeX[i] = tree.X;
                _treeZ[i] = tree.Z;
                _trunkReach[i] = tree.TrunkRadius + tuning.RiderRadius;
                _branchReach[i] = tree.BranchRadius + tuning.RiderRadius;
                _ringReach[i] = Fix64.Max(_branchReach[i], _trunkReach[i] + tuning.NearMissDistance);

                // A run that starts inside a trunk would crash and reset there forever: the reset spot is the start.
                // Box test first, so the squares stay small however far apart the start and the tree are.
                Fix64 dx = Fix64.Abs(setup.StartX - _treeX[i]);
                Fix64 dz = Fix64.Abs(setup.StartZ - _treeZ[i]);
                if (dx < _trunkReach[i] && dz < _trunkReach[i] && dx * dx + dz * dz < _trunkReach[i] * _trunkReach[i])
                {
                    throw new System.ArgumentException("The run starts inside the trunk of tree " + i + ".", nameof(setup));
                }
            }

            int railCount = rails.Count;
            _railX0 = new Fix64[railCount];
            _railZ0 = new Fix64[railCount];
            _railDirX = new Fix64[railCount];
            _railDirZ = new Fix64[railCount];
            _railLength = new Fix64[railCount];
            _railHeight = new Fix64[railCount];
            _railReach = new Fix64[railCount];
            _railBox = new bool[railCount];
            for (int i = 0; i < railCount; i++)
            {
                SlopeRail rail = rails[i];
                Fix64 dx = rail.X1 - rail.X0;
                Fix64 dz = rail.Z1 - rail.Z0;

                // RailSet keeps every rail at least RailSet.MinLengthMm long, so the length is never zero.
                Fix64 length = Fix64.Sqrt(dx * dx + dz * dz);
                _railX0[i] = rail.X0;
                _railZ0[i] = rail.Z0;
                _railDirX[i] = dx / length;
                _railDirZ[i] = dz / length;
                _railLength[i] = length;
                _railHeight[i] = rail.Height;
                _railReach[i] = rail.Width * Half + tuning.GrindCaptureDistance;
                _railBox[i] = rail.Box;
            }

            heightmap.Sample(setup.StartX, setup.StartZ, out Fix64 height, out Vec3 normal);
            _state.Position = new Vec3(setup.StartX, height, setup.StartZ);
            _state.Velocity = Vec3.Zero;
            _state.Heading = Fix64.Atan2(normal.X, normal.Z);
            _state.Snow = snowMap.TypeAt(setup.StartX, setup.StartZ);
            _state.OutsideHeightmap = !heightmap.Contains(setup.StartX, setup.StartZ);
            _state.Rng = new SimRandom(setup.Seed);
            _state.SafePosition = _state.Position;
            _state.SafeHeading = _state.Heading;
            PrimeTrees(_state.Position);
        }

        /// <summary>The run setup this simulation was started with.</summary>
        public RunSetup Setup { get; }

        /// <summary>The trees this simulation rides among.</summary>
        public Forest Forest { get; }

        /// <summary>The rails and boxes on this simulation's slope.</summary>
        public RailSet Rails { get; }

        /// <summary>
        /// A 64-bit FNV-1a hash of the per-tree pass state: which trees the rider is passing and the closest gap so
        /// far. That state lives here, not in <see cref="State"/>, so two equal states can still ride differently
        /// among trees, and <see cref="State"/> alone cannot restore a ride. Replay tests compare this every step.
        /// Allocates nothing.
        /// </summary>
        public ulong TreePassHash
        {
            get
            {
                ulong hash = Fnv1a64.OffsetBasis;
                for (int i = 0; i < _treePass.Length; i++)
                {
                    hash = Fnv1a64.AddInt32(hash, _treePass[i]);
                    hash = Fnv1a64.AddInt64(hash, _treeMinGap[i].Raw);
                }

                return hash;
            }
        }

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

            bool nearMiss = false;
            if (resetTapped)
            {
                ResetToSafeSpot(tick);
            }
            else if (_state.Crashed)
            {
                StepCrashed(tick);
            }
            else
            {
                Vec3 from = _state.Position;
                if (_state.Grinding)
                {
                    StepGrind(input, tick);
                }
                else if (_state.Airborne)
                {
                    StepAir(input, tick);
                }
                else
                {
                    StepSnow(input, tick);
                }

                if (!_state.Crashed)
                {
                    nearMiss = CheckTrees(from, tick);
                }
            }

            UpdateFlowDrain(nearMiss);
            _state.LastInput = input;
            _state.Tick = tick;

            bool outside = !_heightmap.Contains(_state.Position.X, _state.Position.Z);
            if (outside && !_state.OutsideHeightmap)
            {
                _events.Add(new SimEvent(tick, SimEventKind.LeftHeightmap));
            }

            _state.OutsideHeightmap = outside;
            if (!_state.Airborne && !_state.Crashed && !_state.Grinding && !outside)
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
            // Flow cuts snow and air drag in proportion: the whole FlowDragCut at full Flow.
            Fix64 flowDragFactor = Fix64.One - _tuning.FlowDragCut * _state.Flow;
            Fix64 deceleration = (_tuning.Drag(snow) * normalGravity + airDrag * speed * speed) * flowDragFactor
                + _tuning.SkidDrag * slip * speed;
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

            Vec3 previous = _state.Position;
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
                _state.SwitchTakeoff = Vec3.Dot(velocity, forward) < Fix64.Zero;
                _events.Add(SimEvent.Takeoff(tick, Fix64.Sqrt(Vec3.Dot(velocity, velocity))));
            }
            else
            {
                int rail = RideOntoRail(previous, _state.Position, velocity);
                if (rail >= 0)
                {
                    StartGrind(rail, tick);
                }
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

            // Coming down through the top of a rail or box lands on it.
            if (velocity.Y < Fix64.Zero)
            {
                int rail = CatchRail(_state.Position, position, out Vec3 railTop);
                if (rail >= 0)
                {
                    Touchdown(railTop, velocity, normal, tick, rail);
                    return;
                }
            }

            if (position.Y > height)
            {
                _state.Position = position;
                _state.Velocity = velocity;
                _state.MaxHeight = Fix64.Max(_state.MaxHeight, position.Y - height);
                return;
            }

            Touchdown(new Vec3(position.X, height, position.Z), velocity, normal, tick);
        }

        /// <summary>
        /// Grades the landing at <paramref name="position"/>: on the terrain, or on top of rail <paramref name="rail"/> when it
        /// is not negative. Rides on, starts the grind, or crashes (onto the snow below a rail).
        /// </summary>
        private void Touchdown(Vec3 position, Vec3 velocity, Vec3 normal, int tick, int rail = -1)
        {
            EndGrab();

            Fix64 flipOff = PastNearest(_state.FlipAngle, Fix64.TwoPi);
            Fix64 spinOff = PastNearest(_state.SpinAngle, Fix64.Pi);
            bool flipWorse = Fix64.Abs(flipOff) >= Fix64.Abs(spinOff);
            Fix64 residual = flipWorse ? flipOff : spinOff;
            Fix64 rotation = flipWorse ? _state.FlipAngle : _state.SpinAngle;
            Fix64 angle = Fix64.Abs(residual);
            var air = new AirSummary(
                _state.AirTicks, _state.MaxHeight, _state.SpinAngle, _state.FlipAngle, _state.BestGrab, _state.BestGrabTicks, angle,
                _state.SwitchTakeoff, _state.Flow);

            _state.Position = position;
            _state.Airborne = false;
            _state.Snow = _snowMap.TypeAt(position.X, position.Z);
            ClearAir();

            if (angle > _tuning.SketchyLandingAngle)
            {
                // A residual is non-zero only when its rotation is, so the signs always compare.
                CrashCause cause = (residual > Fix64.Zero) == (rotation > Fix64.Zero) ? CrashCause.OverRotation : CrashCause.UnderRotation;
                if (rail >= 0)
                {
                    _heightmap.Sample(position.X, position.Z, out Fix64 ground, out _);
                    _state.Position = new Vec3(position.X, ground, position.Z);
                }

                _state.Velocity = Vec3.Zero;
                _state.Crashed = true;
                _state.CrashTicks = 0;
                _state.Flow = Fix64.Zero;
                _events.Add(SimEvent.Crash(tick, cause, air));
                return;
            }

            LandingGrade grade = angle <= _tuning.CleanLandingAngle ? LandingGrade.Clean
                : angle <= _tuning.SolidLandingAngle ? LandingGrade.Solid
                : LandingGrade.Sketchy;
            if (rail >= 0)
            {
                // Onto a rail or box: graded as any landing, then the grind takes the velocity along the rail.
                _state.Velocity = velocity;
                _events.Add(SimEvent.Land(tick, grade, air));
                StartGrind(rail, tick);
                return;
            }

            // The snow absorbs the velocity into the slope; what runs along it rides on.
            Fix64 into = Vec3.Dot(velocity, normal);
            _state.Velocity = into < Fix64.Zero ? velocity - normal * into : velocity;
            _events.Add(SimEvent.Land(tick, grade, air));
        }

        /// <summary>One step on a rail or box: balance, slide along it, and leave past an end or fall off.</summary>
        private void StepGrind(SimInput input, int tick)
        {
            Fix64 dt = StepSeconds;
            int rail = _state.GrindRail;

            // The lean grows by itself, steering pushes it its way, and the run's random generator wobbles it.
            Fix64 steering = Fix64.FromRatio(input.CombinedSteering, SimInput.SteeringMax);
            Fix64 wobble = Fix64.FromRaw(unchecked((long)(_state.Rng.NextU64() >> 31))) - Fix64.One;
            Fix64 balance = _state.Balance
                + (_tuning.GrindTipRate * _state.Balance + _tuning.GrindBalanceRate * steering + _tuning.GrindWobble * wobble) * dt;
            _state.Balance = balance;
            _state.GrindTicks++;
            _state.GrindLeanSum = _state.GrindLeanSum + Fix64.Min(Fix64.Abs(balance), Fix64.One);
            _state.Skidding = false;
            _state.SlipAngle = Fix64.Zero;
            if (Fix64.Abs(balance) >= Fix64.One)
            {
                FallOffRail(tick);
                return;
            }

            // Slide along the rail's direction on the surface under it: gravity along it, rail and air drag against it.
            _heightmap.Sample(_state.Position.X, _state.Position.Z, out _, out Vec3 normal);
            Vec3 tangent = RailTangent(rail, normal);
            Fix64 along = Vec3.Dot(_state.Velocity, tangent) - _tuning.Gravity * tangent.Y * dt;
            Fix64 loss = (_tuning.GrindDrag * _tuning.Gravity * normal.Y + _tuning.AirDrag * along * along) * dt;
            along = Fix64.Abs(along) <= loss ? Fix64.Zero : (along > Fix64.Zero ? along - loss : along + loss);
            Vec3 velocity = tangent * along;
            Fix64 x = _state.Position.X + velocity.X * dt;
            Fix64 z = _state.Position.Z + velocity.Z * dt;
            Fix64 s = (x - _railX0[rail]) * _railDirX[rail] + (z - _railZ0[rail]) * _railDirZ[rail];
            _state.Velocity = velocity;
            if (s >= Fix64.Zero && s <= _railLength[rail])
            {
                _state.Position = RailTop(rail, s);
                return;
            }

            // Past an end: step down onto the snow from a low rail, fly from a high one.
            EndGrind(tick, false);
            _heightmap.Sample(x, z, out Fix64 ground, out _);
            if (_railHeight[rail] <= _tuning.GrindStepUpHeight)
            {
                _state.Position = new Vec3(x, ground, z);
                _state.Snow = _snowMap.TypeAt(x, z);
                return;
            }

            _state.Position = new Vec3(x, Fix64.Max(_state.Position.Y + velocity.Y * dt, ground), z);
            ClearAir();
            _state.Airborne = true;
            var facing = new Vec3(Fix64.Sin(_state.Heading), Fix64.Zero, Fix64.Cos(_state.Heading));
            _state.SwitchTakeoff = Vec3.Dot(velocity, facing) < Fix64.Zero;
            _events.Add(SimEvent.Takeoff(tick, Fix64.Abs(along)));
        }

        /// <summary>
        /// Puts the rider on rail <paramref name="rail"/> at the point nearest their position, keeps the velocity along it,
        /// sets the entry lean and raises <see cref="SimEventKind.GrindStart"/>.
        /// </summary>
        private void StartGrind(int rail, int tick)
        {
            Vec3 position = _state.Position;
            Fix64 s = Fix64.Clamp(
                (position.X - _railX0[rail]) * _railDirX[rail] + (position.Z - _railZ0[rail]) * _railDirZ[rail], Fix64.Zero, _railLength[rail]);
            Vec3 top = RailTop(rail, s);
            _heightmap.Sample(top.X, top.Z, out _, out Vec3 normal);
            Vec3 tangent = RailTangent(rail, normal);
            Vec3 velocity = _state.Velocity;
            Fix64 along = Vec3.Dot(velocity, tangent);

            // The entry lean: the share of the level speed that crosses the rail toward the rider's right, times GrindEntryLean.
            Fix64 across = velocity.X * _railDirZ[rail] - velocity.Z * _railDirX[rail];
            across = along < Fix64.Zero ? -across : across;
            Fix64 level = Fix64.Sqrt(velocity.X * velocity.X + velocity.Z * velocity.Z);
            Fix64 lean = level > Fix64.Zero ? Fix64.Clamp(_tuning.GrindEntryLean * (across / level), -Fix64.One, Fix64.One) : Fix64.Zero;

            _state.Position = top;
            _state.Velocity = tangent * along;
            _state.Airborne = false;
            _state.Skidding = false;
            _state.SlipAngle = Fix64.Zero;
            _state.PopLoadTicks = 0;
            _state.Grinding = true;
            _state.GrindRail = rail;
            _state.GrindTicks = 0;
            _state.GrindLeanSum = Fix64.Zero;
            _state.Balance = lean;
            _events.Add(SimEvent.GrindStart(tick, rail));
        }

        /// <summary>Raises <see cref="SimEventKind.GrindEnd"/> with the grind's summary and leaves the rail.</summary>
        private void EndGrind(int tick, bool fell)
        {
            int rail = _state.GrindRail;
            int ticks = _state.GrindTicks;

            // One minus the mean lean; a grind that ends before any step on the rail counts as balanced.
            Fix64 balance = ticks > 0 ? Fix64.Max(Fix64.Zero, Fix64.One - _state.GrindLeanSum / Fix64.FromInt(ticks)) : Fix64.One;
            _events.Add(SimEvent.GrindEnd(tick, rail, new GrindSummary(ticks, balance, fell, _railBox[rail])));
            ClearGrind();
        }

        /// <summary>The lean reached 1: the grind ends and the rider crashes onto the snow below the rail.</summary>
        private void FallOffRail(int tick)
        {
            int rail = _state.GrindRail;
            Fix64 speed = Fix64.Sqrt(Vec3.Dot(_state.Velocity, _state.Velocity));
            EndGrind(tick, true);
            Vec3 position = _state.Position;
            _heightmap.Sample(position.X, position.Z, out Fix64 ground, out _);
            _state.Position = new Vec3(position.X, ground, position.Z);
            _state.Snow = _snowMap.TypeAt(position.X, position.Z);
            _state.Velocity = Vec3.Zero;
            _state.Crashed = true;
            _state.CrashTicks = 0;
            _state.Flow = Fix64.Zero;
            _events.Add(SimEvent.RailCrash(tick, rail, speed));
        }

        /// <summary>Zeroes the grind fields.</summary>
        private void ClearGrind()
        {
            _state.Grinding = false;
            _state.GrindRail = 0;
            _state.GrindTicks = 0;
            _state.Balance = Fix64.Zero;
            _state.GrindLeanSum = Fix64.Zero;
        }

        /// <summary>
        /// The first rail, in index order, whose top the path from <paramref name="from"/> to <paramref name="to"/> comes down
        /// through over its footprint, or -1. <paramref name="top"/> is the point on its top under <paramref name="to"/>.
        /// </summary>
        private int CatchRail(Vec3 from, Vec3 to, out Vec3 top)
        {
            for (int i = 0; i < _railLength.Length; i++)
            {
                if (OnRail(i, to.X, to.Z, out Fix64 s))
                {
                    Vec3 point = RailTop(i, s);
                    if (from.Y >= point.Y && to.Y <= point.Y)
                    {
                        top = point;
                        return i;
                    }
                }
            }

            top = Vec3.Zero;
            return -1;
        }

        /// <summary>
        /// The first rail, in index order, no higher than <see cref="Tuning.GrindStepUpHeight"/> whose footprint the rider
        /// entered this step (from <paramref name="previous"/> to <paramref name="now"/>) moving along it at
        /// <see cref="Tuning.GrindMinSpeed"/> or more; -1 for none.
        /// </summary>
        private int RideOntoRail(Vec3 previous, Vec3 now, Vec3 velocity)
        {
            for (int i = 0; i < _railLength.Length; i++)
            {
                if (_railHeight[i] <= _tuning.GrindStepUpHeight && OnRail(i, now.X, now.Z, out _) && !OnRail(i, previous.X, previous.Z, out _)
                    && Fix64.Abs(velocity.X * _railDirX[i] + velocity.Z * _railDirZ[i]) >= _tuning.GrindMinSpeed)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// True when (<paramref name="x"/>, <paramref name="z"/>) lies over rail <paramref name="rail"/>'s footprint seen from
        /// above; <paramref name="s"/> is the distance along it from its start.
        /// </summary>
        private bool OnRail(int rail, Fix64 x, Fix64 z, out Fix64 s)
        {
            Fix64 offsetX = x - _railX0[rail];
            Fix64 offsetZ = z - _railZ0[rail];
            s = offsetX * _railDirX[rail] + offsetZ * _railDirZ[rail];
            Fix64 across = offsetX * _railDirZ[rail] - offsetZ * _railDirX[rail];
            return s >= Fix64.Zero && s <= _railLength[rail] && Fix64.Abs(across) <= _railReach[rail];
        }

        /// <summary>The point on rail <paramref name="rail"/>'s top at distance <paramref name="s"/> from its start.</summary>
        private Vec3 RailTop(int rail, Fix64 s)
        {
            Fix64 x = _railX0[rail] + _railDirX[rail] * s;
            Fix64 z = _railZ0[rail] + _railDirZ[rail] * s;
            _heightmap.Sample(x, z, out Fix64 ground, out _);
            return new Vec3(x, ground + _railHeight[rail], z);
        }

        /// <summary>Rail <paramref name="rail"/>'s direction on the surface whose normal is <paramref name="normal"/>, unit length.</summary>
        private Vec3 RailTangent(int rail, Vec3 normal)
        {
            var level = new Vec3(_railDirX[rail], Fix64.Zero, _railDirZ[rail]);

            // The heightmap's normal always points up, so a level direction never lies along it and the length is not zero.
            Vec3 tangent = level - normal * Vec3.Dot(level, normal);
            return tangent * (Fix64.One / Fix64.Sqrt(Vec3.Dot(tangent, tangent)));
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
            ClearGrind();
            PrimeTrees(_state.Position);
            _events.Add(new SimEvent(tick, SimEventKind.Reset));
        }

        /// <summary>
        /// Checks this step's path, from <paramref name="from"/> to the rider's position, against every tree in index
        /// order: trunk crash, branch clip, near-miss pass (see the class remarks). Returns true if a near miss was raised.
        /// </summary>
        private bool CheckTrees(Vec3 from, int tick)
        {
            bool nearMiss = false;
            Vec3 to = _state.Position;
            Fix64 pathX = to.X - from.X;
            Fix64 pathZ = to.Z - from.Z;
            Fix64 pathLengthSquared = pathX * pathX + pathZ * pathZ;
            Fix64 minX = Fix64.Min(from.X, to.X);
            Fix64 maxX = Fix64.Max(from.X, to.X);
            Fix64 minZ = Fix64.Min(from.Z, to.Z);
            Fix64 maxZ = Fix64.Max(from.Z, to.Z);
            for (int i = 0; i < _treeX.Length; i++)
            {
                int pass = _treePass[i];
                Fix64 reach = _ringReach[i];
                if (_treeX[i] < minX - reach || _treeX[i] > maxX + reach || _treeZ[i] < minZ - reach || _treeZ[i] > maxZ + reach)
                {
                    // Beyond every ring: a pass in progress ends, judged now if it was not yet.
                    if ((pass & (PassNear | PassJudged)) == PassNear)
                    {
                        nearMiss |= JudgeNearMiss(i, tick);
                    }

                    _treePass[i] = PassNone;
                    continue;
                }

                Fix64 distance = PathDistance(from.X, from.Z, pathX, pathZ, pathLengthSquared, _treeX[i], _treeZ[i]);
                Fix64 gap = distance - _trunkReach[i];
                if (gap <= Fix64.Zero)
                {
                    CrashIntoTree(i, from, tick);
                    return nearMiss;
                }

                if (distance < _branchReach[i])
                {
                    if ((pass & PassBranches) == 0)
                    {
                        pass |= PassBranches;
                        ClipBranches(i, tick);
                    }
                }
                else
                {
                    pass &= ~PassBranches;
                }

                if (gap <= _tuning.NearMissDistance)
                {
                    if ((pass & PassNear) == 0)
                    {
                        pass = (pass | PassNear) & ~PassJudged;
                        _treeMinGap[i] = gap;
                    }
                    else if ((pass & PassJudged) == 0)
                    {
                        if (gap > _treeMinGap[i])
                        {
                            pass |= PassJudged;
                            nearMiss |= JudgeNearMiss(i, tick);
                        }
                        else
                        {
                            _treeMinGap[i] = gap;
                        }
                    }
                }
                else
                {
                    if ((pass & (PassNear | PassJudged)) == PassNear)
                    {
                        nearMiss |= JudgeNearMiss(i, tick);
                    }

                    pass &= ~(PassNear | PassJudged);
                }

                _treePass[i] = pass;
            }

            return nearMiss;
        }

        /// <summary>
        /// Judges tree <paramref name="tree"/>'s pass at its smallest gap: fast enough, it fills Flow by closeness and
        /// raises <see cref="SimEventKind.NearMiss"/>. Returns true if it did.
        /// </summary>
        private bool JudgeNearMiss(int tree, int tick)
        {
            Fix64 speed = Fix64.Sqrt(Vec3.Dot(_state.Velocity, _state.Velocity));
            if (speed < _tuning.NearMissMinSpeed)
            {
                return false;
            }

            // The smallest gap is above zero and at most NearMissDistance, which Validate keeps positive.
            Fix64 gap = _treeMinGap[tree];
            Fix64 closeness = Fix64.One - gap / _tuning.NearMissDistance;
            Fix64 gain = _tuning.NearMissFlowGainFar + (_tuning.NearMissFlowGainClose - _tuning.NearMissFlowGainFar) * closeness;
            _state.Flow = Fix64.Min(Fix64.One, _state.Flow + gain);
            _events.Add(SimEvent.NearMiss(tick, tree, gap, speed));
            return true;
        }

        /// <summary>Clips tree <paramref name="tree"/>'s branches: costs speed and Flow, raises <see cref="SimEventKind.BranchHit"/>.</summary>
        private void ClipBranches(int tree, int tick)
        {
            Fix64 speed = Fix64.Sqrt(Vec3.Dot(_state.Velocity, _state.Velocity));
            _state.Velocity = _state.Velocity * (Fix64.One - _tuning.BranchSpeedLoss);
            _state.Flow = Fix64.Max(Fix64.Zero, _state.Flow - _tuning.BranchFlowCost);
            _events.Add(SimEvent.BranchHit(tick, tree, speed));
        }

        /// <summary>
        /// A trunk hit: the rider stops on the snow where the step began (<paramref name="from"/>, clear of every trunk),
        /// crashes with <see cref="CrashCause.Tree"/> and loses all Flow.
        /// </summary>
        private void CrashIntoTree(int tree, Vec3 from, int tick)
        {
            if (_state.Grinding)
            {
                EndGrind(tick, true);
            }

            Fix64 speed = Fix64.Sqrt(Vec3.Dot(_state.Velocity, _state.Velocity));
            _heightmap.Sample(from.X, from.Z, out Fix64 height, out _);
            _state.Position = new Vec3(from.X, height, from.Z);
            _state.Velocity = Vec3.Zero;
            _state.Snow = _snowMap.TypeAt(from.X, from.Z);
            _state.Skidding = false;
            _state.SlipAngle = Fix64.Zero;
            _state.Airborne = false;
            _state.PopLoadTicks = 0;
            ClearAir();
            _state.Crashed = true;
            _state.CrashTicks = 0;
            _state.Flow = Fix64.Zero;
            _events.Add(SimEvent.TreeCrash(tick, tree, speed));
        }

        /// <summary>
        /// Sets every tree's pass flags for a rider standing at <paramref name="at"/>: inside a ring counts as a pass
        /// already judged, so it raises nothing until the rider leaves it.
        /// </summary>
        private void PrimeTrees(Vec3 at)
        {
            for (int i = 0; i < _treeX.Length; i++)
            {
                int pass = PassNone;
                Fix64 reach = _ringReach[i];
                if (_treeX[i] >= at.X - reach && _treeX[i] <= at.X + reach && _treeZ[i] >= at.Z - reach && _treeZ[i] <= at.Z + reach)
                {
                    Fix64 distance = PathDistance(at.X, at.Z, Fix64.Zero, Fix64.Zero, Fix64.Zero, _treeX[i], _treeZ[i]);
                    if (distance < _branchReach[i])
                    {
                        pass |= PassBranches;
                    }

                    if (distance - _trunkReach[i] <= _tuning.NearMissDistance)
                    {
                        pass |= PassNear | PassJudged;
                    }
                }

                _treePass[i] = pass;
            }
        }

        /// <summary>
        /// Distance seen from above from (<paramref name="pointX"/>, <paramref name="pointZ"/>) to the closest point of
        /// the path that starts at (<paramref name="fromX"/>, <paramref name="fromZ"/>) and runs (<paramref name="pathX"/>,
        /// <paramref name="pathZ"/>), whose squared length is <paramref name="pathLengthSquared"/>.
        /// </summary>
        private static Fix64 PathDistance(Fix64 fromX, Fix64 fromZ, Fix64 pathX, Fix64 pathZ, Fix64 pathLengthSquared, Fix64 pointX, Fix64 pointZ)
        {
            Fix64 offsetX = pointX - fromX;
            Fix64 offsetZ = pointZ - fromZ;
            Fix64 along = Fix64.Zero;

            // A path too short to have a squared length in Q32.32 is a point; testing first keeps the division safe.
            if (pathLengthSquared > Fix64.Zero)
            {
                along = Fix64.Clamp((offsetX * pathX + offsetZ * pathZ) / pathLengthSquared, Fix64.Zero, Fix64.One);
            }

            Fix64 restX = offsetX - pathX * along;
            Fix64 restZ = offsetZ - pathZ * along;
            return Fix64.Sqrt(restX * restX + restZ * restZ);
        }

        /// <summary>
        /// Counts steps since the last near miss up to <see cref="Tuning.FlowDrainDelay"/>'s worth; once there, drains
        /// Flow by <see cref="Tuning.FlowDrainRate"/> per second, never below zero.
        /// </summary>
        private void UpdateFlowDrain(bool nearMiss)
        {
            if (nearMiss)
            {
                _state.FlowIdleTicks = 0;
                return;
            }

            if (Fix64.FromInt(_state.FlowIdleTicks) < _flowDrainSteps)
            {
                _state.FlowIdleTicks++;
            }

            if (Fix64.FromInt(_state.FlowIdleTicks) >= _flowDrainSteps)
            {
                _state.Flow = Fix64.Max(Fix64.Zero, _state.Flow - _flowDrainPerStep);
            }
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
            _state.SwitchTakeoff = false;
        }

        /// <summary>
        /// The signed rotation past the nearest whole multiple of <paramref name="period"/>, in [-period/2, period/2).
        /// </summary>
        private static Fix64 PastNearest(Fix64 angle, Fix64 period)
        {
            int turns = ((angle + period * Half) / period).FloorToInt();
            return angle - period * Fix64.FromInt(turns);
        }

        /// <summary>
        /// Rejects tuning a ride cannot run on: values a step divides by (<see cref="Tuning.MaxEdgeAngle"/>,
        /// <see cref="Tuning.PopLoadTime"/>), gravity that would keep a flight going forever, durations whose step
        /// count overflows an int, a negative takeoff threshold (a takeoff every step), landing thresholds out of
        /// order, a grab threshold outside (0, 1] (no neutral grab), and for trees and Flow (Story 005): a near-miss
        /// distance the Flow fill cannot divide by, tree reaches whose squares overflow, negative speeds or rates, Flow
        /// gains out of order or past 1, a drain delay that overflows an int of steps, and fractions outside [0, 1] that
        /// would reverse the velocity or make drag push; and for trick scoring (Story 006): points coefficients outside
        /// [0, 1000000], landing-quality factors outside [0, 1000], a grab minimum that is negative or overflows an int of
        /// steps, and a repeat factor outside [0, 1]; and for rails: a capture distance or step-up height outside [0, 1000] m,
        /// negative speeds, drag or balance rates past [0, 1000], grind points outside [0, 1000000] and a fall factor outside
        /// [0, 1]. <see cref="RunScore"/> checks its tuning here too.
        /// </summary>
        internal static void Validate(Tuning tuning)
        {
            Require(tuning.Gravity > Fix64.Zero, "Gravity must be positive");
            Require(tuning.MaxEdgeAngle > Fix64.Zero, "MaxEdgeAngle must be positive");
            Require(tuning.PopLoadTime > Fix64.Zero && tuning.PopLoadTime < MaxTuningSeconds, "PopLoadTime must be positive and fit an int of steps");
            Require(tuning.CrashResetDelay >= Fix64.Zero && tuning.CrashResetDelay < MaxTuningSeconds, "CrashResetDelay must be zero or more and fit an int of steps");
            Require(tuning.TakeoffDropSpeed >= Fix64.Zero, "TakeoffDropSpeed must be zero or more");
            Require(
                Fix64.Zero <= tuning.CleanLandingAngle && tuning.CleanLandingAngle <= tuning.SolidLandingAngle
                    && tuning.SolidLandingAngle <= tuning.SketchyLandingAngle,
                "landing angles must run 0 <= Clean <= Solid <= Sketchy");
            Require(tuning.GrabSteeringThreshold > Fix64.Zero && tuning.GrabSteeringThreshold <= Fix64.One, "GrabSteeringThreshold must be in (0, 1]");

            Fix64 maxReach = Fix64.FromInt(MaxTreeReachMetres);
            Require(tuning.RiderRadius >= Fix64.Zero && tuning.RiderRadius <= maxReach, "RiderRadius must be in [0, 1000] m");
            Require(tuning.NearMissDistance > Fix64.Zero && tuning.NearMissDistance <= maxReach, "NearMissDistance must be in (0, 1000] m");
            Require(tuning.NearMissMinSpeed >= Fix64.Zero, "NearMissMinSpeed must be zero or more");
            Require(
                Fix64.Zero <= tuning.NearMissFlowGainFar && tuning.NearMissFlowGainFar <= tuning.NearMissFlowGainClose
                    && tuning.NearMissFlowGainClose <= Fix64.One,
                "near-miss Flow gains must run 0 <= Far <= Close <= 1");
            Require(tuning.FlowDrainDelay >= Fix64.Zero && tuning.FlowDrainDelay < MaxTuningSeconds, "FlowDrainDelay must be zero or more and fit an int of steps");
            Require(tuning.FlowDrainRate >= Fix64.Zero, "FlowDrainRate must be zero or more");
            Require(IsFraction(tuning.BranchSpeedLoss), "BranchSpeedLoss must be in [0, 1]");
            Require(IsFraction(tuning.BranchFlowCost), "BranchFlowCost must be in [0, 1]");
            Require(IsFraction(tuning.FlowDragCut), "FlowDragCut must be in [0, 1]");
            Require(tuning.FlowTrickBonus >= Fix64.Zero && tuning.FlowTrickBonus <= MaxFlowTrickBonus, "FlowTrickBonus must be in [0, 10]");

            Require(IsWithin(tuning.TrickPointsPerHalfTurn, MaxPointsCoefficient), "TrickPointsPerHalfTurn must be in [0, 10000]");
            Require(IsWithin(tuning.TrickPointsPerFlip, MaxPointsCoefficient), "TrickPointsPerFlip must be in [0, 10000]");
            Require(IsWithin(tuning.TrickPointsPerAirSecond, MaxPointsCoefficient), "TrickPointsPerAirSecond must be in [0, 10000]");
            Require(IsWithin(tuning.GrabBasePoints, MaxPointsCoefficient), "GrabBasePoints must be in [0, 10000]");
            Require(IsWithin(tuning.GrabPointsPerSecond, MaxPointsCoefficient), "GrabPointsPerSecond must be in [0, 10000]");
            Require(IsWithin(tuning.GrabPointsCap, MaxPointsCoefficient), "GrabPointsCap must be in [0, 10000]");
            Require(tuning.GrabMinTime >= Fix64.Zero && tuning.GrabMinTime < MaxTuningSeconds, "GrabMinTime must be zero or more and fit an int of steps");
            Require(tuning.MinScoredAirTime >= Fix64.Zero && tuning.MinScoredAirTime < MaxTuningSeconds, "MinScoredAirTime must be zero or more and fit an int of steps");
            Require(IsWithin(tuning.CleanTrickFactor, MaxTrickFactor), "CleanTrickFactor must be in [0, 10]");
            Require(IsWithin(tuning.SolidTrickFactor, MaxTrickFactor), "SolidTrickFactor must be in [0, 10]");
            Require(IsWithin(tuning.SketchyTrickFactor, MaxTrickFactor), "SketchyTrickFactor must be in [0, 10]");
            Require(IsFraction(tuning.TrickRepeatFactor), "TrickRepeatFactor must be in [0, 1]");

            Fix64 maxRate = Fix64.FromInt(MaxGrindRate);
            Require(IsWithin(tuning.GrindCaptureDistance, maxReach), "GrindCaptureDistance must be in [0, 1000] m");
            Require(IsWithin(tuning.GrindStepUpHeight, maxReach), "GrindStepUpHeight must be in [0, 1000] m");
            Require(tuning.GrindMinSpeed >= Fix64.Zero, "GrindMinSpeed must be zero or more");
            Require(IsWithin(tuning.GrindDrag, maxRate), "GrindDrag must be in [0, 1000]");
            Require(IsWithin(tuning.GrindTipRate, maxRate), "GrindTipRate must be in [0, 1000]");
            Require(IsWithin(tuning.GrindBalanceRate, maxRate), "GrindBalanceRate must be in [0, 1000]");
            Require(IsWithin(tuning.GrindWobble, maxRate), "GrindWobble must be in [0, 1000]");
            Require(IsWithin(tuning.GrindEntryLean, maxRate), "GrindEntryLean must be in [0, 1000]");
            Require(IsWithin(tuning.GrindPointsPerSecond, MaxPointsCoefficient), "GrindPointsPerSecond must be in [0, 10000]");
            Require(IsFraction(tuning.GrindFallFactor), "GrindFallFactor must be in [0, 1]");
        }

        private static bool IsFraction(Fix64 value) => value >= Fix64.Zero && value <= Fix64.One;

        private static bool IsWithin(Fix64 value, Fix64 max) => value >= Fix64.Zero && value <= max;

        private static void Require(bool valid, string rule)
        {
            if (!valid)
            {
                throw new System.ArgumentException("Tuning: " + rule + ".", "tuning");
            }
        }
    }
}
