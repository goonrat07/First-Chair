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
        /// The rider touched down past <see cref="Tuning.SketchyLandingAngle"/> and crashed.
        /// <see cref="SimEvent.Cause"/> says why and <see cref="SimEvent.Air"/> describes the flight.
        /// </summary>
        Crash = 4,

        /// <summary>
        /// The rider was put back, at rest, at the last safe spot: after a crash and <see cref="Tuning.CrashResetDelay"/>,
        /// or at once on a <see cref="SimButtons.Reset"/> tap. A view snaps to the new position instead of interpolating.
        /// </summary>
        Reset = 5,
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

    /// <summary>Why a landing became a crash.</summary>
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

        /// <summary>Creates a flight summary.</summary>
        /// <param name="airTicks">Steps in the air.</param>
        /// <param name="maxHeight">Greatest height above the terrain, metres.</param>
        /// <param name="spin">Total spin, radians.</param>
        /// <param name="flip">Total flip, radians.</param>
        /// <param name="grab">Longest grab.</param>
        /// <param name="grabTicks">Steps the longest grab was held.</param>
        /// <param name="landingAngle">Landing angle, radians.</param>
        public AirSummary(int airTicks, Fix64 maxHeight, Fix64 spin, Fix64 flip, GrabKind grab, int grabTicks, Fix64 landingAngle)
        {
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
            && Grab == other.Grab && GrabTicks == other.GrabTicks && LandingAngle == other.LandingAngle;

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
                return hash * 397 ^ LandingAngle.GetHashCode();
            }
        }

        /// <inheritdoc/>
        public override string ToString() =>
            "air " + AirTicks + " max " + MaxHeight + " spin " + Spin + " flip " + Flip + " grab " + Grab + "/" + GrabTicks
            + " angle " + LandingAngle;
    }

    /// <summary>
    /// Something that happened during one step. <see cref="Simulation.Events"/> holds the events of the
    /// last step; the game layer drains them after each step instead of polling rider state.
    /// </summary>
    /// <remarks>
    /// <para>One flat value type for every kind, so raising an event allocates nothing. Fields a kind does not use are
    /// zero: <see cref="Speed"/> belongs to <see cref="SimEventKind.Takeoff"/>, <see cref="Grade"/> to
    /// <see cref="SimEventKind.Land"/>, <see cref="Cause"/> to <see cref="SimEventKind.Crash"/>, and <see cref="Air"/>
    /// to both of the last two.</para>
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
    /// }
    /// </code>
    /// </example>
    public readonly struct SimEvent : IEquatable<SimEvent>
    {
        /// <summary><see cref="RiderState.Tick"/> after the step that raised the event.</summary>
        public readonly int Tick;

        /// <summary>What happened.</summary>
        public readonly SimEventKind Kind;

        /// <summary><see cref="SimEventKind.Takeoff"/>: the rider's speed leaving the snow, m/s. Zero otherwise.</summary>
        public readonly Fix64 Speed;

        /// <summary><see cref="SimEventKind.Land"/>: the landing grade. <see cref="LandingGrade.None"/> otherwise.</summary>
        public readonly LandingGrade Grade;

        /// <summary><see cref="SimEventKind.Crash"/>: the cause. <see cref="CrashCause.None"/> otherwise.</summary>
        public readonly CrashCause Cause;

        /// <summary><see cref="SimEventKind.Land"/> and <see cref="SimEventKind.Crash"/>: the flight. Default otherwise.</summary>
        public readonly AirSummary Air;

        /// <summary>Creates an event with no payload.</summary>
        /// <param name="tick">Tick after the step that raised it.</param>
        /// <param name="kind">What happened.</param>
        public SimEvent(int tick, SimEventKind kind)
            : this(tick, kind, Fix64.Zero, LandingGrade.None, CrashCause.None, default(AirSummary))
        {
        }

        private SimEvent(int tick, SimEventKind kind, Fix64 speed, LandingGrade grade, CrashCause cause, AirSummary air)
        {
            Tick = tick;
            Kind = kind;
            Speed = speed;
            Grade = grade;
            Cause = cause;
            Air = air;
        }

        /// <summary>A <see cref="SimEventKind.Takeoff"/> event.</summary>
        /// <param name="tick">Tick after the step that raised it.</param>
        /// <param name="speed">Speed leaving the snow, m/s.</param>
        public static SimEvent Takeoff(int tick, Fix64 speed) =>
            new SimEvent(tick, SimEventKind.Takeoff, speed, LandingGrade.None, CrashCause.None, default(AirSummary));

        /// <summary>A <see cref="SimEventKind.Land"/> event.</summary>
        /// <param name="tick">Tick after the step that raised it.</param>
        /// <param name="grade">The landing grade.</param>
        /// <param name="air">The flight.</param>
        public static SimEvent Land(int tick, LandingGrade grade, AirSummary air) =>
            new SimEvent(tick, SimEventKind.Land, Fix64.Zero, grade, CrashCause.None, air);

        /// <summary>A <see cref="SimEventKind.Crash"/> event.</summary>
        /// <param name="tick">Tick after the step that raised it.</param>
        /// <param name="cause">Why the landing failed.</param>
        /// <param name="air">The flight.</param>
        public static SimEvent Crash(int tick, CrashCause cause, AirSummary air) =>
            new SimEvent(tick, SimEventKind.Crash, Fix64.Zero, LandingGrade.None, cause, air);

        /// <inheritdoc/>
        public bool Equals(SimEvent other) =>
            Tick == other.Tick && Kind == other.Kind && Speed == other.Speed && Grade == other.Grade && Cause == other.Cause
            && Air.Equals(other.Air);

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
                return hash * 397 ^ Air.GetHashCode();
            }
        }

        /// <inheritdoc/>
        public override string ToString() => Kind switch
        {
            SimEventKind.Takeoff => Kind + "@" + Tick + " speed " + Speed,
            SimEventKind.Land => Kind + "@" + Tick + " " + Grade + " " + Air,
            SimEventKind.Crash => Kind + "@" + Tick + " " + Cause + " " + Air,
            _ => Kind + "@" + Tick,
        };
    }
}
