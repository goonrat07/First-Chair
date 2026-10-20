using System;

namespace FirstChair.Sim
{
    /// <summary>What the rider does with their body on the snow, chosen each step from the input and the slope.</summary>
    public enum Stance : byte
    {
        /// <summary>Standing: neither forward nor back held.</summary>
        Stand = 0,

        /// <summary>
        /// Tucked: forward held on a slope, or on a flat at or above <see cref="Tuning.PoleSpeedMax"/>. Less air
        /// drag (<see cref="Tuning.TuckAirDragFactor"/>) and slower turning (<see cref="Tuning.TuckTurnFactor"/>).
        /// </summary>
        Tuck = 1,

        /// <summary>Braking: back held. Back wins over forward.</summary>
        Brake = 2,

        /// <summary>Poling: forward held on a flat below <see cref="Tuning.PoleSpeedMax"/>.</summary>
        Pole = 3,
    }

    /// <summary>The grab held in the air (see <see cref="SimButtons.Grab"/>).</summary>
    public enum GrabKind : byte
    {
        /// <summary>No grab.</summary>
        None = 0,

        /// <summary>Grab with no direction.</summary>
        Mute = 1,

        /// <summary>Grab plus forward.</summary>
        Tip = 2,

        /// <summary>Grab plus back.</summary>
        Tail = 3,

        /// <summary>Grab plus left.</summary>
        Japan = 4,

        /// <summary>Grab plus right.</summary>
        Safety = 5,
    }

    /// <summary>
    /// The whole mutable state of a ride, as one value. Two runs match bit for bit exactly when their
    /// states are equal after every step.
    /// </summary>
    /// <remarks>
    /// <para><see cref="Simulation.State"/> returns a copy: the game layer reads it and draws it, and
    /// writing to the copy changes nothing.</para>
    /// <para>On snow, <see cref="Position"/>.Y is the terrain height under the rider. In the air
    /// (<see cref="Airborne"/>) it is the rider's own height, above the terrain. <see cref="Flow"/> is the Flow meter
    /// (Story 005); the per-tree pass flags that gate tree events live in the <see cref="Simulation"/>, not here.</para>
    /// <para>The air fields (<see cref="AirTicks"/> to <see cref="SwitchTakeoff"/>) describe the current flight and are
    /// zero on snow; the flight's totals leave in the <see cref="SimEventKind.Land"/> or <see cref="SimEventKind.Crash"/>
    /// event.</para>
    /// <para><b>Angles</b> are radians. <see cref="Heading"/> 0 faces +Z and pi/2 faces +X, the yaw a Y-up,
    /// left-handed view (Unity) turns the rider by; positive steering increases it.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// RiderState previous = simulation.State;
    /// simulation.Step(input);
    /// RiderState current = simulation.State;
    /// // A view interpolates between previous and current position and heading,
    /// // leans the skier by current.EdgeAngle and sprays snow while current.Skidding.
    /// </code>
    /// </example>
    public struct RiderState : IEquatable<RiderState>
    {
        /// <summary>Position, metres, in heightmap space (Y up).</summary>
        public Vec3 Position;

        /// <summary>Velocity, metres per second, kept tangent to the surface.</summary>
        public Vec3 Velocity;

        /// <summary>Ski heading seen from above, radians in (-pi, pi]; see the class remarks for the convention.</summary>
        public Fix64 Heading;

        /// <summary>
        /// Edge angle, radians: how far the skis are tipped onto their edges, up to
        /// <see cref="Tuning.MaxEdgeAngle"/> either way. Negative is the left edges (turning left).
        /// </summary>
        public Fix64 EdgeAngle;

        /// <summary>True when the edges could not hold in the last step and the skis skidded.</summary>
        public bool Skidding;

        /// <summary>
        /// After a skidding step, the angle left between the velocity and the skis, radians, 0 to pi/2; zero
        /// while carving.
        /// </summary>
        public Fix64 SlipAngle;

        /// <summary>
        /// The snow the last step rode on: the type at the rider's position when the step began. At the start
        /// of a run, the type at the start position.
        /// </summary>
        public SnowType Snow;

        /// <summary>The stance of the last step.</summary>
        public Stance Stance;

        /// <summary>Steps completed since the run started.</summary>
        public int Tick;

        /// <summary>True while the rider is off the heightmap.</summary>
        public bool OutsideHeightmap;

        /// <summary>The input of the last step.</summary>
        public SimInput LastInput;

        /// <summary>The run's one random generator, seeded from <see cref="RunSetup.Seed"/>.</summary>
        public SimRandom Rng;

        /// <summary>True while the rider is in the air.</summary>
        public bool Airborne;

        /// <summary>
        /// Steps <see cref="SimButtons.Jump"/> has been held on snow, up to <see cref="Tuning.PopLoadTime"/>'s worth;
        /// zero when not loading.
        /// </summary>
        public int PopLoadTicks;

        /// <summary>Steps in the current flight.</summary>
        public int AirTicks;

        /// <summary>The greatest height above the terrain in the current flight, metres.</summary>
        public Fix64 MaxHeight;

        /// <summary>
        /// Spin in the current flight, radians, unwrapped; positive toward positive steering. <see cref="Heading"/>
        /// turns with it.
        /// </summary>
        public Fix64 SpinAngle;

        /// <summary>
        /// Flip in the current flight, radians, unwrapped; positive is forward. The skis' pitch off the snow below
        /// (a view pitches the rider by it).
        /// </summary>
        public Fix64 FlipAngle;

        /// <summary>The grab held this step, or <see cref="GrabKind.None"/>.</summary>
        public GrabKind Grab;

        /// <summary>Steps <see cref="Grab"/> has been held in one go.</summary>
        public int GrabTicks;

        /// <summary>The grab held longest in one go in this flight so far.</summary>
        public GrabKind BestGrab;

        /// <summary>Steps <see cref="BestGrab"/> was held.</summary>
        public int BestGrabTicks;

        /// <summary>
        /// True when the current flight took off riding switch: the velocity ran against the skis' heading (its dot with
        /// the heading's direction on the surface was below zero). Story 006 names such tricks "Switch ...".
        /// </summary>
        public bool SwitchTakeoff;

        /// <summary>True from a crash landing until the reset; riding input is ignored meanwhile.</summary>
        public bool Crashed;

        /// <summary>Steps since the crash.</summary>
        public int CrashTicks;

        /// <summary>
        /// The last safe spot: the position at the end of the most recent step that ended on snow, uncrashed and on
        /// the heightmap (the start, before the first step). A reset puts the rider here.
        /// </summary>
        public Vec3 SafePosition;

        /// <summary>The heading at <see cref="SafePosition"/>.</summary>
        public Fix64 SafeHeading;

        /// <summary>
        /// The Flow meter, 0 to 1 (Story 005): near misses fill it, branch clips cost it, it drains after
        /// <see cref="Tuning.FlowDrainDelay"/> without a near miss, and any crash empties it. It cuts drag
        /// (<see cref="Tuning.FlowDragCut"/>) and scales trick points (<see cref="Tuning.TrickMultiplier"/>).
        /// </summary>
        public Fix64 Flow;

        /// <summary>
        /// Steps since the last near miss, counted up to <see cref="Tuning.FlowDrainDelay"/>'s worth and held there;
        /// Flow drains while it is there.
        /// </summary>
        public int FlowIdleTicks;

        /// <summary>True while the rider grinds a rail or box (Story 006); <see cref="Airborne"/> is false meanwhile.</summary>
        public bool Grinding;

        /// <summary>The rail being ground, an index in the <see cref="RailSet"/>; read it only while <see cref="Grinding"/>.</summary>
        public int GrindRail;

        /// <summary>Steps on the current rail.</summary>
        public int GrindTicks;

        /// <summary>
        /// The lean on the rail, -1 to 1: negative is to the rider's left. It grows by itself, steering pushes it, and
        /// at 1 either way the rider falls off. Zero off rails. A view tilts the rider by it.
        /// </summary>
        public Fix64 Balance;

        /// <summary>The sum of |<see cref="Balance"/>| over the current grind's steps: its mean grades the grind.</summary>
        public Fix64 GrindLeanSum;

        /// <inheritdoc/>
        public bool Equals(RiderState other) =>
            Position == other.Position
            && Velocity == other.Velocity
            && Heading == other.Heading
            && EdgeAngle == other.EdgeAngle
            && Skidding == other.Skidding
            && SlipAngle == other.SlipAngle
            && Snow == other.Snow
            && Stance == other.Stance
            && Tick == other.Tick
            && OutsideHeightmap == other.OutsideHeightmap
            && LastInput.Equals(other.LastInput)
            && Rng.Equals(other.Rng)
            && Airborne == other.Airborne
            && PopLoadTicks == other.PopLoadTicks
            && AirTicks == other.AirTicks
            && MaxHeight == other.MaxHeight
            && SpinAngle == other.SpinAngle
            && FlipAngle == other.FlipAngle
            && Grab == other.Grab
            && GrabTicks == other.GrabTicks
            && BestGrab == other.BestGrab
            && BestGrabTicks == other.BestGrabTicks
            && SwitchTakeoff == other.SwitchTakeoff
            && Crashed == other.Crashed
            && CrashTicks == other.CrashTicks
            && SafePosition == other.SafePosition
            && SafeHeading == other.SafeHeading
            && Flow == other.Flow
            && FlowIdleTicks == other.FlowIdleTicks
            && Grinding == other.Grinding
            && GrindRail == other.GrindRail
            && GrindTicks == other.GrindTicks
            && Balance == other.Balance
            && GrindLeanSum == other.GrindLeanSum;

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is RiderState other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Position.GetHashCode();
                hash = hash * 397 ^ Velocity.GetHashCode();
                hash = hash * 397 ^ Heading.GetHashCode();
                hash = hash * 397 ^ EdgeAngle.GetHashCode();
                hash = hash * 397 ^ (Skidding ? 1 : 0);
                hash = hash * 397 ^ SlipAngle.GetHashCode();
                hash = hash * 397 ^ (int)Snow;
                hash = hash * 397 ^ (int)Stance;
                hash = hash * 397 ^ Tick;
                hash = hash * 397 ^ (OutsideHeightmap ? 1 : 0);
                hash = hash * 397 ^ LastInput.GetHashCode();
                hash = hash * 397 ^ Rng.GetHashCode();
                hash = hash * 397 ^ (Airborne ? 1 : 0);
                hash = hash * 397 ^ PopLoadTicks;
                hash = hash * 397 ^ AirTicks;
                hash = hash * 397 ^ MaxHeight.GetHashCode();
                hash = hash * 397 ^ SpinAngle.GetHashCode();
                hash = hash * 397 ^ FlipAngle.GetHashCode();
                hash = hash * 397 ^ (int)Grab;
                hash = hash * 397 ^ GrabTicks;
                hash = hash * 397 ^ (int)BestGrab;
                hash = hash * 397 ^ BestGrabTicks;
                hash = hash * 397 ^ (SwitchTakeoff ? 1 : 0);
                hash = hash * 397 ^ (Crashed ? 1 : 0);
                hash = hash * 397 ^ CrashTicks;
                hash = hash * 397 ^ SafePosition.GetHashCode();
                hash = hash * 397 ^ SafeHeading.GetHashCode();
                hash = hash * 397 ^ Flow.GetHashCode();
                hash = hash * 397 ^ FlowIdleTicks;
                hash = hash * 397 ^ (Grinding ? 1 : 0);
                hash = hash * 397 ^ GrindRail;
                hash = hash * 397 ^ GrindTicks;
                hash = hash * 397 ^ Balance.GetHashCode();
                return hash * 397 ^ GrindLeanSum.GetHashCode();
            }
        }

        /// <inheritdoc/>
        public override string ToString() =>
            "Tick " + Tick + " pos " + Position + " vel " + Velocity + " heading " + Heading + " edge " + EdgeAngle
            + (Skidding ? " skid " + SlipAngle : string.Empty) + " " + Snow + " " + Stance
            + (OutsideHeightmap ? " outside" : string.Empty) + " input " + LastInput
            + (PopLoadTicks > 0 ? " load " + PopLoadTicks : string.Empty)
            + (Airborne ? " AIR " + AirTicks + " max " + MaxHeight + " spin " + SpinAngle + " flip " + FlipAngle + " grab " + Grab + "/" + GrabTicks + (SwitchTakeoff ? " switch" : string.Empty) : string.Empty)
            + (Crashed ? " CRASHED " + CrashTicks : string.Empty)
            + " safe " + SafePosition + " flow " + Flow + "/" + FlowIdleTicks
            + (Grinding ? " GRIND " + GrindRail + "/" + GrindTicks + " balance " + Balance + " lean " + GrindLeanSum : string.Empty);
    }
}
