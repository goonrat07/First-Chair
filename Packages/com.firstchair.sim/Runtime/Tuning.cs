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

        /// <summary>Pop speed at the shortest load (one step), m/s, added along the surface normal on release.</summary>
        public Fix64 PopSpeedMin;

        /// <summary>Pop speed at a full load, m/s: the cap. Between, it grows linearly with hold time.</summary>
        public Fix64 PopSpeedMax;

        /// <summary>Seconds of holding jump to reach <see cref="PopSpeedMax"/>. Must be positive.</summary>
        public Fix64 PopLoadTime;

        /// <summary>
        /// Takeoff detection, m/s: the rider leaves the snow when the ground under the next position falls away from
        /// where the rider's own velocity carries them faster than this (a lip or a sharp crest). Slower, the rider
        /// rolls over and stays on the snow. Per step the gap must exceed this times the step length.
        /// </summary>
        public Fix64 TakeoffDropSpeed;

        /// <summary>Spin rate in the air at full steering, radians per second.</summary>
        public Fix64 SpinRate;

        /// <summary>Flip rate in the air while forward or back is held, radians per second.</summary>
        public Fix64 FlipRate;

        /// <summary>
        /// Fraction of full steering (0 to 1) that makes grab plus left a Japan or grab plus right a safety; less is a
        /// mute.
        /// </summary>
        public Fix64 GrabSteeringThreshold;

        /// <summary>Largest landing angle, radians, graded clean.</summary>
        public Fix64 CleanLandingAngle;

        /// <summary>Largest landing angle, radians, graded solid.</summary>
        public Fix64 SolidLandingAngle;

        /// <summary>Largest landing angle, radians, graded sketchy; past it the landing is a crash.</summary>
        public Fix64 SketchyLandingAngle;

        /// <summary>Seconds from a crash to the reset to the last safe spot.</summary>
        public Fix64 CrashResetDelay;

        /// <summary>
        /// The rider's radius seen from above, metres: trunks, branches and near-miss gaps are measured to this circle
        /// around the rider's position.
        /// </summary>
        public Fix64 RiderRadius;

        /// <summary>
        /// Near-miss distance, metres: a pass whose smallest gap between the rider and a trunk is above zero and at most
        /// this is a near miss. Must be positive (the Flow fill divides by it).
        /// </summary>
        public Fix64 NearMissDistance;

        /// <summary>Slowest speed, m/s, at which a near miss counts; slower passes raise nothing.</summary>
        public Fix64 NearMissMinSpeed;

        /// <summary>Flow a near miss adds at a gap of <see cref="NearMissDistance"/> (the farthest that counts).</summary>
        public Fix64 NearMissFlowGainFar;

        /// <summary>
        /// Flow a near miss would add at a gap of zero. Between, the gain runs linearly with closeness,
        /// 1 - gap / <see cref="NearMissDistance"/>: a closer pass fills more.
        /// </summary>
        public Fix64 NearMissFlowGainClose;

        /// <summary>Seconds without a near miss before Flow starts to drain.</summary>
        public Fix64 FlowDrainDelay;

        /// <summary>Flow lost per second while draining.</summary>
        public Fix64 FlowDrainRate;

        /// <summary>Fraction of the rider's velocity a branch clip takes, 0 to 1.</summary>
        public Fix64 BranchSpeedLoss;

        /// <summary>Flow a branch clip takes (an amount of the 0 to 1 meter, not a fraction of what is left).</summary>
        public Fix64 BranchFlowCost;

        /// <summary>
        /// Fraction of snow and air drag that full Flow cuts, 0 to 1. It scales with Flow: drag is multiplied by
        /// 1 - FlowDragCut * Flow.
        /// </summary>
        public Fix64 FlowDragCut;

        /// <summary>
        /// Extra trick points at full Flow, as a fraction: <see cref="TrickMultiplier"/> is 1 + FlowTrickBonus * Flow.
        /// <see cref="TrickScoring"/> applies it with the Flow at touchdown.
        /// </summary>
        public Fix64 FlowTrickBonus;

        /// <summary>Points per half turn of spin in a landed jump: h in the trick formula (<see cref="TrickScoring"/>).</summary>
        public Fix64 TrickPointsPerHalfTurn;

        /// <summary>Points per full flip in a landed jump: f in the trick formula.</summary>
        public Fix64 TrickPointsPerFlip;

        /// <summary>Points per second of air time in a landed jump: t in the trick formula.</summary>
        public Fix64 TrickPointsPerAirSecond;

        /// <summary>The grab bonus's fixed part, earned by a grab held at least <see cref="GrabMinTime"/>.</summary>
        public Fix64 GrabBasePoints;

        /// <summary>The grab bonus per second the grab was held in one go.</summary>
        public Fix64 GrabPointsPerSecond;

        /// <summary>The most grab bonus one jump can earn.</summary>
        public Fix64 GrabPointsCap;

        /// <summary>Shortest grab, seconds, that counts: shorter earns no bonus and does not name the trick.</summary>
        public Fix64 GrabMinTime;

        /// <summary>Landing-quality factor q of a clean landing.</summary>
        public Fix64 CleanTrickFactor;

        /// <summary>Landing-quality factor q of a solid landing.</summary>
        public Fix64 SolidTrickFactor;

        /// <summary>Landing-quality factor q of a sketchy landing.</summary>
        public Fix64 SketchyTrickFactor;

        /// <summary>
        /// Repeat decay, 0 to 1: a trick name already landed n times in the run scores its points times this to the
        /// power n.
        /// </summary>
        public Fix64 TrickRepeatFactor;

        /// <summary>How far past half a rail's width, metres, the rider still lands on or rides onto it.</summary>
        public Fix64 GrindCaptureDistance;

        /// <summary>The highest rail or box, metres, a rider on snow rides onto, and steps down from at its end.</summary>
        public Fix64 GrindStepUpHeight;

        /// <summary>Slowest speed along a low rail, m/s, at which riding onto it starts a grind.</summary>
        public Fix64 GrindMinSpeed;

        /// <summary>Sliding-friction coefficient on a rail or box: it slows the rider by GrindDrag * gravity * cos slope.</summary>
        public Fix64 GrindDrag;

        /// <summary>How fast the lean grows by itself, per second: the lean changes by GrindTipRate * lean per second.</summary>
        public Fix64 GrindTipRate;

        /// <summary>Lean change per second at full steering: steering pushes the lean its way.</summary>
        public Fix64 GrindBalanceRate;

        /// <summary>The largest random lean change per second (the run's random generator picks it each step).</summary>
        public Fix64 GrindWobble;

        /// <summary>Entry lean per share of the level speed that crosses the rail: entering at an angle starts off balance.</summary>
        public Fix64 GrindEntryLean;

        /// <summary>Grind points per second on the rail at perfect balance.</summary>
        public Fix64 GrindPointsPerSecond;

        /// <summary>Fraction of its points a grind that ends in a fall keeps, 0 to 1.</summary>
        public Fix64 GrindFallFactor;

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
            PopSpeedMin = Fix64.FromInt(2),
            PopSpeedMax = Fix64.FromInt(5),
            PopLoadTime = Fix64.FromRatio(1, 2),
            TakeoffDropSpeed = Fix64.FromRatio(1, 2),
            SpinRate = Fix64.Pi * Fix64.FromRatio(5, 2),
            FlipRate = Fix64.TwoPi,
            GrabSteeringThreshold = Fix64.FromRatio(1, 2),
            CleanLandingAngle = Fix64.Pi / Fix64.FromInt(12),
            SolidLandingAngle = Fix64.Pi / Fix64.FromInt(6),
            SketchyLandingAngle = Fix64.Pi / Fix64.FromInt(4),
            CrashResetDelay = Fix64.One,

            // Story 005 starting values (design/reference/bluebird-spec.md, Flow and the trees): near misses inside
            // 2.4 m, a branch clip costs 25% of Flow, drain 0.22 per second after 1.4 s, full Flow cuts drag 30% and adds
            // 50% to trick points. A branch clip keeps 70% of speed, as the spec's other stumble (tree wells) does.
            RiderRadius = Fix64.FromRatio(3, 10),
            NearMissDistance = Fix64.FromRatio(24, 10),
            NearMissMinSpeed = Fix64.FromInt(3),
            NearMissFlowGainFar = Fix64.FromRatio(5, 100),
            NearMissFlowGainClose = Fix64.FromRatio(25, 100),
            FlowDrainDelay = Fix64.FromRatio(14, 10),
            FlowDrainRate = Fix64.FromRatio(22, 100),
            BranchSpeedLoss = Fix64.FromRatio(3, 10),
            BranchFlowCost = Fix64.FromRatio(25, 100),
            FlowDragCut = Fix64.FromRatio(3, 10),
            FlowTrickBonus = Fix64.FromRatio(1, 2),

            // Story 006 starting values (design/reference/bluebird-spec.md, Single tricks): air =
            // (100 h + 450 f + g + 60 t) * q * (1 + FlowTrickBonus * Flow); g = 150 + 300 per second held, capped at 600,
            // only from 0.15 s; q = 1.5 clean, 1.0 solid, 0.6 sketchy; each repeat of a name scores half.
            TrickPointsPerHalfTurn = Fix64.FromInt(100),
            TrickPointsPerFlip = Fix64.FromInt(450),
            TrickPointsPerAirSecond = Fix64.FromInt(60),
            GrabBasePoints = Fix64.FromInt(150),
            GrabPointsPerSecond = Fix64.FromInt(300),
            GrabPointsCap = Fix64.FromInt(600),
            GrabMinTime = Fix64.FromRatio(15, 100),
            CleanTrickFactor = Fix64.FromRatio(3, 2),
            SolidTrickFactor = Fix64.One,
            SketchyTrickFactor = Fix64.FromRatio(6, 10),
            TrickRepeatFactor = Fix64.FromRatio(1, 2),

            // Story 006 starting values for rails, to be tuned by playtest: a quarter metre of catch, boxes up to 0.4 m
            // ride on, steel-on-base friction below groomed snow's, a lean that doubles in under a second unless steered,
            // and 200 points per balanced second; a fall keeps half.
            GrindCaptureDistance = Fix64.FromRatio(1, 4),
            GrindStepUpHeight = Fix64.FromRatio(4, 10),
            GrindMinSpeed = Fix64.FromInt(2),
            GrindDrag = Fix64.FromRatio(4, 100),
            GrindTipRate = Fix64.FromRatio(12, 10),
            GrindBalanceRate = Fix64.FromInt(3),
            GrindWobble = Fix64.FromRatio(8, 10),
            GrindEntryLean = Fix64.One,
            GrindPointsPerSecond = Fix64.FromInt(200),
            GrindFallFactor = Fix64.FromRatio(1, 2),
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

        /// <summary>The trick-point multiplier at a Flow level: 1 + <see cref="FlowTrickBonus"/> * flow.</summary>
        /// <param name="flow">The Flow meter, 0 to 1 (<see cref="RiderState.Flow"/>).</param>
        public readonly Fix64 TrickMultiplier(Fix64 flow) => Fix64.One + FlowTrickBonus * flow;

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
            hash = Fnv1a64.AddInt64(hash, PopSpeedMin.Raw);
            hash = Fnv1a64.AddInt64(hash, PopSpeedMax.Raw);
            hash = Fnv1a64.AddInt64(hash, PopLoadTime.Raw);
            hash = Fnv1a64.AddInt64(hash, TakeoffDropSpeed.Raw);
            hash = Fnv1a64.AddInt64(hash, SpinRate.Raw);
            hash = Fnv1a64.AddInt64(hash, FlipRate.Raw);
            hash = Fnv1a64.AddInt64(hash, GrabSteeringThreshold.Raw);
            hash = Fnv1a64.AddInt64(hash, CleanLandingAngle.Raw);
            hash = Fnv1a64.AddInt64(hash, SolidLandingAngle.Raw);
            hash = Fnv1a64.AddInt64(hash, SketchyLandingAngle.Raw);
            hash = Fnv1a64.AddInt64(hash, CrashResetDelay.Raw);
            hash = Fnv1a64.AddInt64(hash, RiderRadius.Raw);
            hash = Fnv1a64.AddInt64(hash, NearMissDistance.Raw);
            hash = Fnv1a64.AddInt64(hash, NearMissMinSpeed.Raw);
            hash = Fnv1a64.AddInt64(hash, NearMissFlowGainFar.Raw);
            hash = Fnv1a64.AddInt64(hash, NearMissFlowGainClose.Raw);
            hash = Fnv1a64.AddInt64(hash, FlowDrainDelay.Raw);
            hash = Fnv1a64.AddInt64(hash, FlowDrainRate.Raw);
            hash = Fnv1a64.AddInt64(hash, BranchSpeedLoss.Raw);
            hash = Fnv1a64.AddInt64(hash, BranchFlowCost.Raw);
            hash = Fnv1a64.AddInt64(hash, FlowDragCut.Raw);
            hash = Fnv1a64.AddInt64(hash, FlowTrickBonus.Raw);
            hash = Fnv1a64.AddInt64(hash, TrickPointsPerHalfTurn.Raw);
            hash = Fnv1a64.AddInt64(hash, TrickPointsPerFlip.Raw);
            hash = Fnv1a64.AddInt64(hash, TrickPointsPerAirSecond.Raw);
            hash = Fnv1a64.AddInt64(hash, GrabBasePoints.Raw);
            hash = Fnv1a64.AddInt64(hash, GrabPointsPerSecond.Raw);
            hash = Fnv1a64.AddInt64(hash, GrabPointsCap.Raw);
            hash = Fnv1a64.AddInt64(hash, GrabMinTime.Raw);
            hash = Fnv1a64.AddInt64(hash, CleanTrickFactor.Raw);
            hash = Fnv1a64.AddInt64(hash, SolidTrickFactor.Raw);
            hash = Fnv1a64.AddInt64(hash, SketchyTrickFactor.Raw);
            hash = Fnv1a64.AddInt64(hash, TrickRepeatFactor.Raw);
            hash = Fnv1a64.AddInt64(hash, GrindCaptureDistance.Raw);
            hash = Fnv1a64.AddInt64(hash, GrindStepUpHeight.Raw);
            hash = Fnv1a64.AddInt64(hash, GrindMinSpeed.Raw);
            hash = Fnv1a64.AddInt64(hash, GrindDrag.Raw);
            hash = Fnv1a64.AddInt64(hash, GrindTipRate.Raw);
            hash = Fnv1a64.AddInt64(hash, GrindBalanceRate.Raw);
            hash = Fnv1a64.AddInt64(hash, GrindWobble.Raw);
            hash = Fnv1a64.AddInt64(hash, GrindEntryLean.Raw);
            hash = Fnv1a64.AddInt64(hash, GrindPointsPerSecond.Raw);
            hash = Fnv1a64.AddInt64(hash, GrindFallFactor.Raw);
            return hash;
        }
    }
}
