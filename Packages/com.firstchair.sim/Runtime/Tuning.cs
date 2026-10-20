using System;

namespace FirstChair.Sim
{
    /// <summary>
    /// The riding numbers. No riding number is written inline in sim logic; it lives here.
    /// </summary>
    /// <remarks>
    /// <para>A plain data struct: <see cref="Simulation"/> and <see cref="Recording"/> copy it when they are
    /// created, so changing a local copy later cannot change a run in progress.</para>
    /// <para><see cref="ComputeHash"/> identifies the values. A recording stores it, and the
    /// <see cref="Replayer"/> refuses a recording made with different tuning. Every field added here must
    /// be added to <see cref="ComputeHash"/>; a test fails if one is missed.</para>
    /// <para>Units are metres, seconds and radians. <see cref="Simulation"/> says where each number acts.</para>
    /// <para>The defaults aim between realism and fun: grip that rewards clean edging, skids that forgive.
    /// They are starting points; riding is tuned by playtest once Story 003 makes the rider visible.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// Tuning tuning = Tuning.Default;
    /// tuning.IceGrip = Fix64.FromInt(4); // a test variant: ice lets go even earlier
    /// var simulation = new Simulation(setup, heightmap, snowMap, tuning);
    /// </code>
    /// </example>
    public struct Tuning
    {
        /// <summary>Gravitational acceleration, metres per second squared.</summary>
        public Fix64 Gravity;

        /// <summary>Grip on groomed snow: the most sideways acceleration (m/s^2) the edges hold before the skis skid.</summary>
        public Fix64 GroomedGrip;

        /// <summary>Grip on powder, m/s^2 (see <see cref="GroomedGrip"/>).</summary>
        public Fix64 PowderGrip;

        /// <summary>Grip on crud, m/s^2 (see <see cref="GroomedGrip"/>).</summary>
        public Fix64 CrudGrip;

        /// <summary>Grip on ice, m/s^2 (see <see cref="GroomedGrip"/>).</summary>
        public Fix64 IceGrip;

        /// <summary>Grip on slush, m/s^2 (see <see cref="GroomedGrip"/>).</summary>
        public Fix64 SlushGrip;

        /// <summary>
        /// Drag of groomed snow: a sliding-friction coefficient. The snow slows the rider by
        /// drag * <see cref="Gravity"/> * (cosine of the slope) m/s^2, and holds a resting rider on slopes
        /// gentler than its friction angle.
        /// </summary>
        public Fix64 GroomedDrag;

        /// <summary>Drag of powder (see <see cref="GroomedDrag"/>).</summary>
        public Fix64 PowderDrag;

        /// <summary>Drag of crud (see <see cref="GroomedDrag"/>).</summary>
        public Fix64 CrudDrag;

        /// <summary>Drag of ice (see <see cref="GroomedDrag"/>).</summary>
        public Fix64 IceDrag;

        /// <summary>Drag of slush (see <see cref="GroomedDrag"/>).</summary>
        public Fix64 SlushDrag;

        /// <summary>Air drag, per metre: the air slows a standing rider by AirDrag * speed^2 m/s^2.</summary>
        public Fix64 AirDrag;

        /// <summary>Fraction of <see cref="AirDrag"/> left while tucked. Below 1 it raises terminal speed.</summary>
        public Fix64 TuckAirDragFactor;

        /// <summary>Fraction of the turn rate left while tucked.</summary>
        public Fix64 TuckTurnFactor;

        /// <summary>Edge angle at full steering, radians.</summary>
        public Fix64 MaxEdgeAngle;

        /// <summary>How fast the edge angle moves toward the steering target, radians per second.</summary>
        public Fix64 EdgeRate;

        /// <summary>
        /// Heading change at full edge (<see cref="MaxEdgeAngle"/>), radians per second. It scales linearly with
        /// the edge angle and does not depend on speed, so a slow rider can pivot.
        /// </summary>
        public Fix64 TurnRate;

        /// <summary>
        /// Skid speed loss, per radian per second: a skidding rider loses SkidDrag * slip angle of their speed,
        /// as a fraction, each second.
        /// </summary>
        public Fix64 SkidDrag;

        /// <summary>Deceleration while braking, m/s^2, on top of snow and air drag.</summary>
        public Fix64 BrakeDeceleration;

        /// <summary>Acceleration along the skis while poling, m/s^2.</summary>
        public Fix64 PoleAcceleration;

        /// <summary>
        /// Pole-speed cap, m/s: poling adds no speed along the skis past it. On a flat at or above it, forward
        /// input tucks instead.
        /// </summary>
        public Fix64 PoleSpeedMax;

        /// <summary>Slopes whose gradient (rise over run) is below this are flats, where forward input poles.</summary>
        public Fix64 FlatGradientMax;

        /// <summary>The default tuning.</summary>
        public static Tuning Default => new Tuning
        {
            Gravity = Fix64.FromRatio(981, 100),
            GroomedGrip = Fix64.FromInt(16),
            PowderGrip = Fix64.FromInt(13),
            CrudGrip = Fix64.FromInt(10),
            IceGrip = Fix64.FromInt(6),
            SlushGrip = Fix64.FromInt(12),
            GroomedDrag = Fix64.FromRatio(5, 100),
            PowderDrag = Fix64.FromRatio(18, 100),
            CrudDrag = Fix64.FromRatio(10, 100),
            IceDrag = Fix64.FromRatio(3, 100),
            SlushDrag = Fix64.FromRatio(14, 100),
            AirDrag = Fix64.FromRatio(5, 1000),
            TuckAirDragFactor = Fix64.FromRatio(1, 2),
            TuckTurnFactor = Fix64.FromRatio(1, 2),
            MaxEdgeAngle = Fix64.FromRatio(105, 100),
            EdgeRate = Fix64.FromInt(5),
            TurnRate = Fix64.FromRatio(18, 10),
            SkidDrag = Fix64.FromRatio(4, 10),
            BrakeDeceleration = Fix64.FromInt(8),
            PoleAcceleration = Fix64.FromRatio(15, 10),
            PoleSpeedMax = Fix64.FromInt(4),
            FlatGradientMax = Fix64.FromRatio(8, 100),
        };

        /// <summary>The grip of a snow type, m/s^2.</summary>
        /// <param name="snow">The snow type.</param>
        public readonly Fix64 Grip(SnowType snow) => snow switch
        {
            SnowType.Groomed => GroomedGrip,
            SnowType.Powder => PowderGrip,
            SnowType.Crud => CrudGrip,
            SnowType.Ice => IceGrip,
            SnowType.Slush => SlushGrip,
            _ => throw new ArgumentOutOfRangeException(nameof(snow)),
        };

        /// <summary>The drag (sliding-friction coefficient) of a snow type.</summary>
        /// <param name="snow">The snow type.</param>
        public readonly Fix64 Drag(SnowType snow) => snow switch
        {
            SnowType.Groomed => GroomedDrag,
            SnowType.Powder => PowderDrag,
            SnowType.Crud => CrudDrag,
            SnowType.Ice => IceDrag,
            SnowType.Slush => SlushDrag,
            _ => throw new ArgumentOutOfRangeException(nameof(snow)),
        };

        /// <summary>A 64-bit FNV-1a hash of every field's raw bits, in declaration order.</summary>
        public ulong ComputeHash()
        {
            ulong hash = Fnv1a64.OffsetBasis;
            hash = Fnv1a64.AddInt64(hash, Gravity.Raw);
            hash = Fnv1a64.AddInt64(hash, GroomedGrip.Raw);
            hash = Fnv1a64.AddInt64(hash, PowderGrip.Raw);
            hash = Fnv1a64.AddInt64(hash, CrudGrip.Raw);
            hash = Fnv1a64.AddInt64(hash, IceGrip.Raw);
            hash = Fnv1a64.AddInt64(hash, SlushGrip.Raw);
            hash = Fnv1a64.AddInt64(hash, GroomedDrag.Raw);
            hash = Fnv1a64.AddInt64(hash, PowderDrag.Raw);
            hash = Fnv1a64.AddInt64(hash, CrudDrag.Raw);
            hash = Fnv1a64.AddInt64(hash, IceDrag.Raw);
            hash = Fnv1a64.AddInt64(hash, SlushDrag.Raw);
            hash = Fnv1a64.AddInt64(hash, AirDrag.Raw);
            hash = Fnv1a64.AddInt64(hash, TuckAirDragFactor.Raw);
            hash = Fnv1a64.AddInt64(hash, TuckTurnFactor.Raw);
            hash = Fnv1a64.AddInt64(hash, MaxEdgeAngle.Raw);
            hash = Fnv1a64.AddInt64(hash, EdgeRate.Raw);
            hash = Fnv1a64.AddInt64(hash, TurnRate.Raw);
            hash = Fnv1a64.AddInt64(hash, SkidDrag.Raw);
            hash = Fnv1a64.AddInt64(hash, BrakeDeceleration.Raw);
            hash = Fnv1a64.AddInt64(hash, PoleAcceleration.Raw);
            hash = Fnv1a64.AddInt64(hash, PoleSpeedMax.Raw);
            hash = Fnv1a64.AddInt64(hash, FlatGradientMax.Raw);
            return hash;
        }
    }
}
