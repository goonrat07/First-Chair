using System;

namespace FirstChair.Sim
{
    /// <summary>What a <see cref="SimEvent"/> reports.</summary>
    public enum SimEventKind : byte
    {
        /// <summary>The rider crossed from on the heightmap to off it.</summary>
        LeftHeightmap = 1,

        /// <summary>The rider left the snow, off a lip or from a pop. <see cref="SimEvent.Speed"/> is the speed.</summary>
        Takeoff = 2,

        /// <summary>
        /// The rider touched down and rode away. <see cref="SimEvent.Grade"/> grades it and <see cref="SimEvent.Air"/>
        /// describes the flight.
        /// </summary>
        Land = 3,

        /// <summary>
        /// The rider crashed: touched down past <see cref="Tuning.SketchyLandingAngle"/>, or hit a trunk.
        /// <see cref="SimEvent.Cause"/> says why. A landing crash carries the flight in <see cref="SimEvent.Air"/>; a tree
        /// crash carries the tree in <see cref="SimEvent.Tree"/> and the impact speed in <see cref="SimEvent.Speed"/>.
        /// </summary>
        Crash = 4,

        /// <summary>
        /// The rider was put back, at rest, at the last safe spot: after a crash and <see cref="Tuning.CrashResetDelay"/>,
        /// or at once on a <see cref="SimButtons.Reset"/> tap. A view snaps to the new position instead of interpolating.
        /// </summary>
        Reset = 5,

        /// <summary>
        /// The rider passed a trunk inside <see cref="Tuning.NearMissDistance"/> without touching it; at most one per tree
        /// per pass. <see cref="SimEvent.Tree"/> is the tree, <see cref="SimEvent.Gap"/> the smallest gap of the pass,
        /// metres, and <see cref="SimEvent.Speed"/> the rider's speed when the pass was judged. Flow is already filled.
        /// </summary>
        NearMiss = 6,

        /// <summary>
        /// The rider clipped a tree's branches; at most one per tree per pass. <see cref="SimEvent.Tree"/> is the tree and
        /// <see cref="SimEvent.Speed"/> the speed before the clip. The speed and Flow costs are already taken.
        /// </summary>
        BranchHit = 7,

        /// <summary>
        /// The rider got onto a rail or box (Story 006): by landing on it from the air (a <see cref="Land"/> of the same
        /// step comes first) or by riding onto a low one. <see cref="SimEvent.Rail"/> is its index in the
        /// <see cref="RailSet"/>.
        /// </summary>
        GrindStart = 8,

        /// <summary>
        /// The grind ended: the rider rode off an end, or fell off (a <see cref="CrashCause.RailFall"/> crash of the same
        /// step follows). <see cref="SimEvent.Rail"/> is the rail and <see cref="SimEvent.Grind"/> what scoring reads.
        /// </summary>
        GrindEnd = 9,
    }

    /// <summary>How well a landing went, from the landing angle (see <see cref="Simulation"/>).</summary>
    public enum LandingGrade : byte
    {
        /// <summary>Not a landing event.</summary>
        None = 0,

        /// <summary>Landing angle at or below <see cref="Tuning.CleanLandingAngle"/>.</summary>
        Clean = 1,

        /// <summary>Landing angle at or below <see cref="Tuning.SolidLandingAngle"/>.</summary>
        Solid = 2,

        /// <summary>Landing angle at or below <see cref="Tuning.SketchyLandingAngle"/>.</summary>
        Sketchy = 3,
    }

    /// <summary>Why the rider crashed.</summary>
    public enum CrashCause : byte
    {
        /// <summary>Not a crash event.</summary>
        None = 0,

        /// <summary>The rider turned past the nearest landable orientation, in the direction they were rotating.</summary>
        OverRotation = 1,

        /// <summary>
        /// The rider stopped short of the nearest landable orientation. A landing angle with no rotation at all also
        /// counts here: the rider did not rotate enough to straighten out.
        /// </summary>
        UnderRotation = 2,

        /// <summary>The rider hit a trunk (Story 005). <see cref="SimEvent.Tree"/> is the tree.</summary>
        Tree = 3,

        /// <summary>The rider lost their balance on a rail or box (Story 006). <see cref="SimEvent.Rail"/> is the rail.</summary>
        RailFall = 4,
    }

    /// <summary>
    /// What happened in one flight, carried by <see cref="SimEventKind.Land"/> and <see cref="SimEventKind.Crash"/>
    /// events: the data trick naming and scoring (Story 006) reads.
    /// </summary>
    /// <remarks>Immutable and thread-safe. A value type, so raising it allocates nothing.</remarks>
    public readonly struct AirSummary : IEquatable<AirSummary>
    {
        /// <summary>Steps in the air, counting the touchdown step.</summary>
        public readonly int AirTicks;

        /// <summary>The greatest height above the terrain under the rider during the flight, metres.</summary>
        public readonly Fix64 MaxHeight;

        /// <summary>
        /// Total spin, radians, signed: positive is toward positive steering (right). A 360 is about 2 pi; it is not
        /// wrapped.
        /// </summary>
        public readonly Fix64 Spin;

        /// <summary>Total flip, radians, signed: positive is forward. One full flip is about 2 pi; it is not wrapped.</summary>
        public readonly Fix64 Flip;

        /// <summary>The grab held longest in one go, or <see cref="GrabKind.None"/>; the first wins a tie.</summary>
        public readonly GrabKind Grab;

        /// <summary>Steps <see cref="Grab"/> was held in one go.</summary>
        public readonly int GrabTicks;

        /// <summary>
        /// The landing angle, radians: the larger of the skis' pitch off the slope (the flip past its nearest whole turn)
        /// and their yaw off the line (the spin past its nearest half turn). See <see cref="Simulation"/>.
        /// </summary>
        public readonly Fix64 LandingAngle;

        /// <summary>
        /// True when the rider took off riding switch: the velocity ran against the skis' heading
        /// (<see cref="RiderState.SwitchTakeoff"/>). Trick names start with "Switch" then.
        /// </summary>
        public readonly bool Switch;

        /// <summary>
        /// The Flow meter at touchdown, 0 to 1, before a landing crash empties it: the Flow
        /// <see cref="TrickScoring"/> multiplies the trick's points by (<see cref="Tuning.TrickMultiplier"/>).
        /// </summary>
        public readonly Fix64 Flow;

        /// <summary>Creates a flight summary that took off riding forward, with no Flow.</summary>
        /// <param name="airTicks">Steps in the air.</param>
        /// <param name="maxHeight">Greatest height above the terrain, metres.</param>
        /// <param name="spin">Total spin, radians.</param>
        /// <param name="flip">Total flip, radians.</param>
        /// <param name="grab">Longest grab.</param>
        /// <param name="grabTicks">Steps the longest grab was held.</param>
        /// <param name="landingAngle">Landing angle, radians.</param>
        public AirSummary(int airTicks, Fix64 maxHeight, Fix64 spin, Fix64 flip, GrabKind grab, int grabTicks, Fix64 landingAngle)
            : this(airTicks, maxHeight, spin, flip, grab, grabTicks, landingAngle, false, Fix64.Zero)
        {
        }

        /// <summary>Creates a flight summary.</summary>
        /// <param name="airTicks">Steps in the air.</param>
        /// <param name="maxHeight">Greatest height above the terrain, metres.</param>
        /// <param name="spin">Total spin, radians.</param>
        /// <param name="flip">Total flip, radians.</param>
        /// <param name="grab">Longest grab.</param>
        /// <param name="grabTicks">Steps the longest grab was held.</param>
        /// <param name="landingAngle">Landing angle, radians.</param>
        /// <param name="switchTakeoff">True when the rider took off riding switch.</param>
        /// <param name="flow">The Flow meter at touchdown.</param>
        public AirSummary(
            int airTicks, Fix64 maxHeight, Fix64 spin, Fix64 flip, GrabKind grab, int grabTicks, Fix64 landingAngle, bool switchTakeoff, Fix64 flow)
        {
            Switch = switchTakeoff;
            Flow = flow;
            AirTicks = airTicks;
            MaxHeight = maxHeight;
            Spin = spin;
            Flip = flip;
            Grab = grab;
            GrabTicks = grabTicks;
            LandingAngle = landingAngle;
        }

        /// <summary>Air time, seconds: <see cref="AirTicks"/> / <see cref="Simulation.StepsPerSecond"/>.</summary>
        public Fix64 AirSeconds => Fix64.FromRatio(AirTicks, Simulation.StepsPerSecond);

        /// <summary>Grab time, seconds: <see cref="GrabTicks"/> / <see cref="Simulation.StepsPerSecond"/>.</summary>
        public Fix64 GrabSeconds => Fix64.FromRatio(GrabTicks, Simulation.StepsPerSecond);

        /// <inheritdoc/>
        public bool Equals(AirSummary other) =>
            AirTicks == other.AirTicks && MaxHeight == other.MaxHeight && Spin == other.Spin && Flip == other.Flip
            && Grab == other.Grab && GrabTicks == other.GrabTicks && LandingAngle == other.LandingAngle && Switch == other.Switch
            && Flow == other.Flow;

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is AirSummary other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = AirTicks;
                hash = hash * 397 ^ MaxHeight.GetHashCode();
                hash = hash * 397 ^ Spin.GetHashCode();
                hash = hash * 397 ^ Flip.GetHashCode();
                hash = hash * 397 ^ (int)Grab;
                hash = hash * 397 ^ GrabTicks;
                hash = hash * 397 ^ LandingAngle.GetHashCode();
                hash = hash * 397 ^ (Switch ? 1 : 0);
                return hash * 397 ^ Flow.GetHashCode();
            }
        }

        /// <inheritdoc/>
        public override string ToString() =>
            "air " + AirTicks + " max " + MaxHeight + " spin " + Spin + " flip " + Flip + " grab " + Grab + "/" + GrabTicks
            + " angle " + LandingAngle + (Switch ? " switch" : string.Empty) + " flow " + Flow;
    }

    /// <summary>
    /// What happened in one grind, carried by <see cref="SimEventKind.GrindEnd"/>: the data grind scoring reads
    /// (<see cref="TrickScoring.GrindValue"/>).
    /// </summary>
    /// <remarks>Immutable and thread-safe. A value type, so raising it allocates nothing.</remarks>
    public readonly struct GrindSummary : IEquatable<GrindSummary>
    {
        /// <summary>Steps on the rail, counting the step it ended.</summary>
        public readonly int Ticks;

        /// <summary>Balance quality, 0 to 1: one minus the mean lean (<see cref="RiderState.Balance"/>) over the grind.</summary>
        public readonly Fix64 Balance;

        /// <summary>True when the grind ended in a fall.</summary>
        public readonly bool Fell;

        /// <summary>True for a box, false for a rail.</summary>
        public readonly bool Box;

        /// <summary>Creates a grind summary.</summary>
        /// <param name="ticks">Steps on the rail.</param>
        /// <param name="balance">Balance quality, 0 to 1.</param>
        /// <param name="fell">True when it ended in a fall.</param>
        /// <param name="box">True for a box.</param>
        public GrindSummary(int ticks, Fix64 balance, bool fell, bool box)
        {
            Ticks = ticks;
            Balance = balance;
            Fell = fell;
            Box = box;
        }

        /// <summary>Time on the rail, seconds: <see cref="Ticks"/> / <see cref="Simulation.StepsPerSecond"/>.</summary>
        public Fix64 Seconds => Fix64.FromRatio(Ticks, Simulation.StepsPerSecond);

        /// <inheritdoc/>
        public bool Equals(GrindSummary other) => Ticks == other.Ticks && Balance == other.Balance && Fell == other.Fell && Box == other.Box;

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is GrindSummary other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Ticks * 397 ^ Balance.GetHashCode();
                return (hash * 397 ^ (Fell ? 1 : 0)) * 397 ^ (Box ? 1 : 0);
            }
        }

        /// <inheritdoc/>
        public override string ToString() => (Box ? "box " : "rail ") + Ticks + " balance " + Balance + (Fell ? " fell" : string.Empty);
    }

    /// <summary>
    /// Something that happened during one step. <see cref="Simulation.Events"/> holds the events of the
    /// last step; the game layer drains them after each step instead of polling rider state.
    /// </summary>
    /// <remarks>
    /// <para>One flat value type for every kind, so raising an event allocates nothing. Fields a kind does not use are
    /// zero: <see cref="Speed"/> belongs to <see cref="SimEventKind.Takeoff"/>, <see cref="SimEventKind.NearMiss"/>,
    /// <see cref="SimEventKind.BranchHit"/> and a tree crash; <see cref="Grade"/> to <see cref="SimEventKind.Land"/>;
    /// <see cref="Cause"/> to <see cref="SimEventKind.Crash"/>; <see cref="Air"/> to a landing and a landing crash;
    /// <see cref="Tree"/> to near misses, branch hits and tree crashes; and <see cref="Gap"/> to near misses. Tree 0
    /// is a real tree, so read <see cref="Tree"/> only for those kinds.</para>
    /// <para>Immutable and thread-safe.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// simulation.Step(input);
    /// // Loop by index: foreach over the IReadOnlyList boxes an enumerator every step.
    /// for (int i = 0; i &lt; simulation.Events.Count; i++)
    /// {
    ///     SimEvent e = simulation.Events[i];
    ///     if (e.Kind == SimEventKind.Land) { /* score e.Air, show e.Grade */ }
    ///     if (e.Kind == SimEventKind.NearMiss) { /* flash the tree e.Tree, show e.Gap */ }
    /// }
    /// </code>
    /// </example>
    public readonly struct SimEvent : IEquatable<SimEvent>
    {
        /// <summary><see cref="RiderState.Tick"/> after the step that raised the event.</summary>
        public readonly int Tick;

        /// <summary>What happened.</summary>
        public readonly SimEventKind Kind;

        /// <summary>
        /// The rider's speed, m/s: leaving the snow (<see cref="SimEventKind.Takeoff"/>), when a near miss was judged,
        /// before a branch clip, or at a trunk impact. Zero otherwise.
        /// </summary>
        public readonly Fix64 Speed;

        /// <summary><see cref="SimEventKind.Land"/>: the landing grade. <see cref="LandingGrade.None"/> otherwise.</summary>
        public readonly LandingGrade Grade;

        /// <summary><see cref="SimEventKind.Crash"/>: the cause. <see cref="CrashCause.None"/> otherwise.</summary>
        public readonly CrashCause Cause;

        /// <summary><see cref="SimEventKind.Land"/> and a landing crash: the flight. Default otherwise.</summary>
        public readonly AirSummary Air;

        /// <summary>
        /// <see cref="SimEventKind.NearMiss"/>, <see cref="SimEventKind.BranchHit"/> and a <see cref="CrashCause.Tree"/>
        /// crash: the tree's index in the <see cref="Forest"/>. Zero otherwise.
        /// </summary>
        public readonly int Tree;

        /// <summary>
        /// <see cref="SimEventKind.NearMiss"/>: the smallest gap of the pass between the rider and the trunk, metres
        /// (above zero, at most <see cref="Tuning.NearMissDistance"/>). Zero otherwise.
        /// </summary>
        public readonly Fix64 Gap;

        /// <summary>
        /// <see cref="SimEventKind.GrindStart"/>, <see cref="SimEventKind.GrindEnd"/> and a <see cref="CrashCause.RailFall"/>
        /// crash: the rail's index in the <see cref="RailSet"/>. Zero otherwise; rail 0 is a real rail.
        /// </summary>
        public readonly int Rail;

        /// <summary><see cref="SimEventKind.GrindEnd"/>: the grind. Default otherwise.</summary>
        public readonly GrindSummary Grind;

        /// <summary>Creates an event with no payload.</summary>
        /// <param name="tick">Tick after the step that raised it.</param>
        /// <param name="kind">What happened.</param>
        public SimEvent(int tick, SimEventKind kind)
            : this(tick, kind, Fix64.Zero, LandingGrade.None, CrashCause.None, default(AirSummary), 0, Fix64.Zero)
        {
        }

        private SimEvent(
            int tick, SimEventKind kind, Fix64 speed, LandingGrade grade, CrashCause cause, AirSummary air, int tree, Fix64 gap,
            int rail = 0, GrindSummary grind = default)
        {
            Rail = rail;
            Grind = grind;
            Tick = tick;
            Kind = kind;
            Speed = speed;
            Grade = grade;
            Cause = cause;
            Air = air;
            Tree = tree;
            Gap = gap;
        }

        /// <summary>A <see cref="SimEventKind.Takeoff"/> event.</summary>
        /// <param name="tick">Tick after the step that raised it.</param>
        /// <param name="speed">Speed leaving the snow, m/s.</param>
        public static SimEvent Takeoff(int tick, Fix64 speed) =>
            new SimEvent(tick, SimEventKind.Takeoff, speed, LandingGrade.None, CrashCause.None, default(AirSummary), 0, Fix64.Zero);

        /// <summary>A <see cref="SimEventKind.Land"/> event.</summary>
        /// <param name="tick">Tick after the step that raised it.</param>
        /// <param name="grade">The landing grade.</param>
        /// <param name="air">The flight.</param>
        public static SimEvent Land(int tick, LandingGrade grade, AirSummary air) =>
            new SimEvent(tick, SimEventKind.Land, Fix64.Zero, grade, CrashCause.None, air, 0, Fix64.Zero);

        /// <summary>A <see cref="SimEventKind.Crash"/> event for a landing.</summary>
        /// <param name="tick">Tick after the step that raised it.</param>
        /// <param name="cause">Why the landing failed.</param>
        /// <param name="air">The flight.</param>
        public static SimEvent Crash(int tick, CrashCause cause, AirSummary air) =>
            new SimEvent(tick, SimEventKind.Crash, Fix64.Zero, LandingGrade.None, cause, air, 0, Fix64.Zero);

        /// <summary>A <see cref="SimEventKind.Crash"/> event with cause <see cref="CrashCause.Tree"/>.</summary>
        /// <param name="tick">Tick after the step that raised it.</param>
        /// <param name="tree">The tree's index in the forest.</param>
        /// <param name="speed">Speed at impact, m/s.</param>
        public static SimEvent TreeCrash(int tick, int tree, Fix64 speed) =>
            new SimEvent(tick, SimEventKind.Crash, speed, LandingGrade.None, CrashCause.Tree, default(AirSummary), tree, Fix64.Zero);

        /// <summary>A <see cref="SimEventKind.NearMiss"/> event.</summary>
        /// <param name="tick">Tick after the step that raised it.</param>
        /// <param name="tree">The tree's index in the forest.</param>
        /// <param name="gap">The smallest gap of the pass, metres.</param>
        /// <param name="speed">The rider's speed, m/s.</param>
        public static SimEvent NearMiss(int tick, int tree, Fix64 gap, Fix64 speed) =>
            new SimEvent(tick, SimEventKind.NearMiss, speed, LandingGrade.None, CrashCause.None, default(AirSummary), tree, gap);

        /// <summary>A <see cref="SimEventKind.BranchHit"/> event.</summary>
        /// <param name="tick">Tick after the step that raised it.</param>
        /// <param name="tree">The tree's index in the forest.</param>
        /// <param name="speed">The rider's speed before the clip, m/s.</param>
        public static SimEvent BranchHit(int tick, int tree, Fix64 speed) =>
            new SimEvent(tick, SimEventKind.BranchHit, speed, LandingGrade.None, CrashCause.None, default(AirSummary), tree, Fix64.Zero);

        /// <summary>A <see cref="SimEventKind.GrindStart"/> event.</summary>
        /// <param name="tick">Tick after the step that raised it.</param>
        /// <param name="rail">The rail's index in the rail set.</param>
        public static SimEvent GrindStart(int tick, int rail) =>
            new SimEvent(tick, SimEventKind.GrindStart, Fix64.Zero, LandingGrade.None, CrashCause.None, default(AirSummary), 0, Fix64.Zero, rail);

        /// <summary>A <see cref="SimEventKind.GrindEnd"/> event.</summary>
        /// <param name="tick">Tick after the step that raised it.</param>
        /// <param name="rail">The rail's index in the rail set.</param>
        /// <param name="grind">The grind.</param>
        public static SimEvent GrindEnd(int tick, int rail, GrindSummary grind) =>
            new SimEvent(tick, SimEventKind.GrindEnd, Fix64.Zero, LandingGrade.None, CrashCause.None, default(AirSummary), 0, Fix64.Zero, rail, grind);

        /// <summary>A <see cref="SimEventKind.Crash"/> event with cause <see cref="CrashCause.RailFall"/>.</summary>
        /// <param name="tick">Tick after the step that raised it.</param>
        /// <param name="rail">The rail's index in the rail set.</param>
        /// <param name="speed">Speed when the rider fell, m/s.</param>
        public static SimEvent RailCrash(int tick, int rail, Fix64 speed) =>
            new SimEvent(tick, SimEventKind.Crash, speed, LandingGrade.None, CrashCause.RailFall, default(AirSummary), 0, Fix64.Zero, rail);

        /// <inheritdoc/>
        public bool Equals(SimEvent other) =>
            Tick == other.Tick && Kind == other.Kind && Speed == other.Speed && Grade == other.Grade && Cause == other.Cause
            && Air.Equals(other.Air) && Tree == other.Tree && Gap == other.Gap && Rail == other.Rail && Grind.Equals(other.Grind);

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is SimEvent other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Tick * 397 ^ (int)Kind;
                hash = hash * 397 ^ Speed.GetHashCode();
                hash = hash * 397 ^ (int)Grade;
                hash = hash * 397 ^ (int)Cause;
                hash = hash * 397 ^ Air.GetHashCode();
                hash = hash * 397 ^ Tree;
                hash = hash * 397 ^ Gap.GetHashCode();
                hash = hash * 397 ^ Rail;
                return hash * 397 ^ Grind.GetHashCode();
            }
        }

        /// <inheritdoc/>
        public override string ToString() => Kind switch
        {
            SimEventKind.Takeoff => Kind + "@" + Tick + " speed " + Speed,
            SimEventKind.Land => Kind + "@" + Tick + " " + Grade + " " + Air,
            SimEventKind.Crash when Cause == CrashCause.RailFall => Kind + "@" + Tick + " RailFall " + Rail + " speed " + Speed,
            SimEventKind.GrindStart => Kind + "@" + Tick + " rail " + Rail,
            SimEventKind.GrindEnd => Kind + "@" + Tick + " rail " + Rail + " " + Grind,
            SimEventKind.Crash when Cause == CrashCause.Tree => Kind + "@" + Tick + " Tree " + Tree + " speed " + Speed,
            SimEventKind.Crash => Kind + "@" + Tick + " " + Cause + " " + Air,
            SimEventKind.NearMiss => Kind + "@" + Tick + " tree " + Tree + " gap " + Gap + " speed " + Speed,
            SimEventKind.BranchHit => Kind + "@" + Tick + " tree " + Tree + " speed " + Speed,
            _ => Kind + "@" + Tick,
        };
    }
}
