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
    /// tuning.IceCarveGrip = Fix64.FromInt(4); // a test variant: carves on ice let go even earlier
    /// var simulation = new Simulation(setup, heightmap, snowMap, tuning);
    /// </code>
    /// </example>
    public struct Tuning
    {
        /// <summary>Gravitational acceleration, metres per second squared.</summary>
        public Fix64 Gravity;

        /// <summary>
        /// Carve grip on groomed snow, m/s^2: the most sideways acceleration a carve's edges hold, which caps how tight a
        /// carve can be at speed (radius at least speed^2 / CarveGrip). 0 means the snow can't be carved: W plus steering
        /// pivots there, as in powder and crud (Story 008).
        /// </summary>
        public Fix64 GroomedCarveGrip;
        /// <summary>Carve grip on powder, m/s^2 (see <see cref="GroomedCarveGrip"/>); 0 means it can't be carved.</summary>
        public Fix64 PowderCarveGrip;
        /// <summary>Carve grip on crud, m/s^2 (see <see cref="GroomedCarveGrip"/>); 0 means it can't be carved.</summary>
        public Fix64 CrudCarveGrip;
        /// <summary>Carve grip on ice, m/s^2 (see <see cref="GroomedCarveGrip"/>); 0 means it can't be carved.</summary>
        public Fix64 IceCarveGrip;
        /// <summary>Carve grip on slush, m/s^2 (see <see cref="GroomedCarveGrip"/>); 0 means it can't be carved.</summary>
        public Fix64 SlushCarveGrip;
        /// <summary>
        /// Skid grip on groomed snow, m/s^2: the most sideways acceleration the snow puts on skis that are turned across
        /// the travel (a quick turn or a skid). It drags the velocity round after the skis; past it the skis slide.
        /// </summary>
        public Fix64 GroomedSkidGrip;
        /// <summary>Skid grip on powder, m/s^2 (see <see cref="GroomedSkidGrip"/>).</summary>
        public Fix64 PowderSkidGrip;
        /// <summary>Skid grip on crud, m/s^2 (see <see cref="GroomedSkidGrip"/>).</summary>
        public Fix64 CrudSkidGrip;
        /// <summary>Skid grip on ice, m/s^2 (see <see cref="GroomedSkidGrip"/>).</summary>
        public Fix64 IceSkidGrip;
        /// <summary>Skid grip on slush, m/s^2 (see <see cref="GroomedSkidGrip"/>).</summary>
        public Fix64 SlushSkidGrip;
        /// <summary>
        /// Drag of groomed snow: a sliding-friction coefficient. The snow slows the rider by drag times the load: gravity's
        /// part normal to the snow, plus the sideways acceleration of a carve (Story 008). It holds a resting rider on
        /// slopes gentler than its friction angle.
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
        /// <summary>
        /// Skid speed loss on groomed snow, per radian per second: skis sliding at a slip angle to the travel plow snow
        /// and lose SkidDrag * slip angle of their speed, as a fraction, each second, on top of the skid grip's own
        /// braking.
        /// </summary>
        public Fix64 GroomedSkidDrag;
        /// <summary>Skid speed loss on powder (see <see cref="GroomedSkidDrag"/>).</summary>
        public Fix64 PowderSkidDrag;
        /// <summary>Skid speed loss on crud (see <see cref="GroomedSkidDrag"/>).</summary>
        public Fix64 CrudSkidDrag;
        /// <summary>Skid speed loss on ice (see <see cref="GroomedSkidDrag"/>).</summary>
        public Fix64 IceSkidDrag;
        /// <summary>Skid speed loss on slush (see <see cref="GroomedSkidDrag"/>).</summary>
        public Fix64 SlushSkidDrag;
        /// <summary>How readily the skis pivot on groomed snow: a factor on <see cref="PivotRate"/>.</summary>
        public Fix64 GroomedPivotFactor;
        /// <summary>How readily the skis pivot on powder (see <see cref="GroomedPivotFactor"/>).</summary>
        public Fix64 PowderPivotFactor;
        /// <summary>How readily the skis pivot on crud (see <see cref="GroomedPivotFactor"/>).</summary>
        public Fix64 CrudPivotFactor;
        /// <summary>How readily the skis pivot on ice (see <see cref="GroomedPivotFactor"/>).</summary>
        public Fix64 IcePivotFactor;
        /// <summary>How readily the skis pivot on slush (see <see cref="GroomedPivotFactor"/>).</summary>
        public Fix64 SlushPivotFactor;
        /// <summary>Air drag, per metre: the air slows a standing rider by AirDrag * speed^2 m/s^2.</summary>
        public Fix64 AirDrag;

        /// <summary>Fraction of <see cref="AirDrag"/> left while tucked (W held, no steering). Below 1 it raises terminal speed.</summary>
        public Fix64 TuckAirDragFactor;

        /// <summary>Fraction of <see cref="AirDrag"/> left while carving (W held and steering): low, but not a full tuck.</summary>
        public Fix64 CarveAirDragFactor;

        /// <summary>Edge angle at full steering, radians.</summary>
        public Fix64 MaxEdgeAngle;

        /// <summary>How fast the edge angle moves toward the steering target, radians per second.</summary>
        public Fix64 EdgeRate;

        /// <summary>
        /// The tightest carve, metres: the radius at full edge when slow enough for the grip to hold it (a carving ski's
        /// sidecut radius times the cosine of its edge angle).
        /// </summary>
        public Fix64 CarveRadiusMin;

        /// <summary>
        /// Speed below which a carve can't hold, m/s: too slow to lean the skis over, so W plus steering pivots instead.
        /// Must be positive.
        /// </summary>
        public Fix64 CarveMinSpeed;

        /// <summary>
        /// Speed band over which a carve takes over from the pivot, m/s: at CarveMinSpeed a W turn still pivots, and from
        /// CarveMinSpeed + CarveBlendSpeed it is a pure carve. Must be positive.
        /// </summary>
        public Fix64 CarveBlendSpeed;

        /// <summary>
        /// Share of the carve grip left after gravity's pull across the slope that a carve turns with, 0 to 1. The rest
        /// is headroom, so a carve at its tightest stays clean on a tilted slope instead of sliding a fraction of a degree.
        /// </summary>
        public Fix64 CarveTurnShare;

        /// <summary>
        /// How fast the skis pivot in a quick turn at full steering when stopped, radians per second. It falls with speed,
        /// divided by 1 + speed / <see cref="PivotFadeSpeed"/>, and is scaled by the snow's pivot factor.
        /// </summary>
        public Fix64 PivotRate;

        /// <summary>Speed at which the pivot rate has fallen to half, m/s. Must be positive.</summary>
        public Fix64 PivotFadeSpeed;

        /// <summary>
        /// How hard the snow pushes back on pivoted skis per radian of slip, m/s^2 per radian, up to the snow's skid grip:
        /// a tyre's linear region before its limit. A quick turn always slips a little, slip angle = sideways
        /// acceleration / SkidStiffness, and the slip is what makes a hard quick turn cost speed. A carve's edges don't
        /// slip. Must be positive.
        /// </summary>
        public Fix64 SkidStiffness;

        /// <summary>
        /// The most a quick turn's skis lead the path, radians. Past it the skis stop pivoting further across the travel,
        /// so a quick turn held at speed is a steady skid that checks speed, not a spin to a stop.
        /// </summary>
        public Fix64 PivotSlipMax;

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

        /// <summary>
        /// Shortest air time, seconds, that scores: a landed jump with less air scores nothing and does not count as a
        /// repeat. It keeps short hops off crests and small rail exits from earning points. A placeholder until a
        /// playtest picks it.
        /// </summary>
        public Fix64 MinScoredAirTime;

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
            // Story 008 (the user's playtest, 2026-10-24: "the slope alone should accelerate me like in GMA2"; carve on
            // groomers, quicker turns in the trees, no carving in the trees). Research values, then tuned by the probes in
            // the story: carving grip near 2 g and an 8 m tightest carve (a 14 m sidecut at about 55 degrees of edge);
            // groomed friction 0.035 (the research's 0.04); air drag for C_d A 0.4 m^2 at 80 kg. Powder and crud don't
            // carve. Powder pivots more slowly but holds the skis sideways well, and plowing it costs speed. Ice slides.
            GroomedCarveGrip = Fix64.FromInt(20),
            PowderCarveGrip = Fix64.Zero,
            CrudCarveGrip = Fix64.Zero,
            IceCarveGrip = Fix64.FromInt(8),
            SlushCarveGrip = Fix64.FromInt(12),
            GroomedSkidGrip = Fix64.FromInt(22),
            PowderSkidGrip = Fix64.FromInt(16),
            CrudSkidGrip = Fix64.FromInt(20),
            IceSkidGrip = Fix64.FromInt(4),
            SlushSkidGrip = Fix64.FromInt(16),
            GroomedDrag = Fix64.FromRatio(35, 1000),
            PowderDrag = Fix64.FromRatio(75, 1000),
            CrudDrag = Fix64.FromRatio(45, 1000),
            IceDrag = Fix64.FromRatio(20, 1000),
            SlushDrag = Fix64.FromRatio(90, 1000),
            GroomedSkidDrag = Fix64.FromRatio(15, 100),
            PowderSkidDrag = Fix64.FromRatio(45, 100),
            CrudSkidDrag = Fix64.FromRatio(25, 100),
            IceSkidDrag = Fix64.FromRatio(5, 100),
            SlushSkidDrag = Fix64.FromRatio(35, 100),
            GroomedPivotFactor = Fix64.One,
            PowderPivotFactor = Fix64.FromRatio(7, 10),
            CrudPivotFactor = Fix64.One,
            IcePivotFactor = Fix64.FromRatio(11, 10),
            SlushPivotFactor = Fix64.FromRatio(85, 100),
            AirDrag = Fix64.FromRatio(28, 10000),
            TuckAirDragFactor = Fix64.FromRatio(55, 100),
            CarveAirDragFactor = Fix64.FromRatio(8, 10),
            MaxEdgeAngle = Fix64.FromRatio(105, 100),
            EdgeRate = Fix64.FromInt(7),
            CarveRadiusMin = Fix64.FromInt(9),
            CarveMinSpeed = Fix64.FromInt(5),
            CarveBlendSpeed = Fix64.FromInt(4),
            CarveTurnShare = Fix64.FromRatio(9, 10),
            PivotRate = Fix64.FromRatio(9, 2),
            PivotFadeSpeed = Fix64.FromInt(8),
            SkidStiffness = Fix64.FromInt(400),
            PivotSlipMax = Fix64.FromRatio(15, 100),
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
            MinScoredAirTime = Fix64.FromRatio(1, 4),
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

        /// <summary>The carve grip of a snow type, m/s^2; 0 if it can't be carved.</summary>
        /// <param name="snow">The snow type.</param>
        public readonly Fix64 CarveGrip(SnowType snow) => snow switch
        {
            SnowType.Groomed => GroomedCarveGrip,
            SnowType.Powder => PowderCarveGrip,
            SnowType.Crud => CrudCarveGrip,
            SnowType.Ice => IceCarveGrip,
            SnowType.Slush => SlushCarveGrip,
            _ => throw new ArgumentOutOfRangeException(nameof(snow)),
        };

        /// <summary>The skid grip of a snow type, m/s^2.</summary>
        /// <param name="snow">The snow type.</param>
        public readonly Fix64 SkidGrip(SnowType snow) => snow switch
        {
            SnowType.Groomed => GroomedSkidGrip,
            SnowType.Powder => PowderSkidGrip,
            SnowType.Crud => CrudSkidGrip,
            SnowType.Ice => IceSkidGrip,
            SnowType.Slush => SlushSkidGrip,
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

        /// <summary>The skid speed loss of a snow type, per radian per second.</summary>
        /// <param name="snow">The snow type.</param>
        public readonly Fix64 SkidDrag(SnowType snow) => snow switch
        {
            SnowType.Groomed => GroomedSkidDrag,
            SnowType.Powder => PowderSkidDrag,
            SnowType.Crud => CrudSkidDrag,
            SnowType.Ice => IceSkidDrag,
            SnowType.Slush => SlushSkidDrag,
            _ => throw new ArgumentOutOfRangeException(nameof(snow)),
        };

        /// <summary>The pivot factor of a snow type.</summary>
        /// <param name="snow">The snow type.</param>
        public readonly Fix64 PivotFactor(SnowType snow) => snow switch
        {
            SnowType.Groomed => GroomedPivotFactor,
            SnowType.Powder => PowderPivotFactor,
            SnowType.Crud => CrudPivotFactor,
            SnowType.Ice => IcePivotFactor,
            SnowType.Slush => SlushPivotFactor,
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
            hash = Fnv1a64.AddInt64(hash, GroomedCarveGrip.Raw);
            hash = Fnv1a64.AddInt64(hash, PowderCarveGrip.Raw);
            hash = Fnv1a64.AddInt64(hash, CrudCarveGrip.Raw);
            hash = Fnv1a64.AddInt64(hash, IceCarveGrip.Raw);
            hash = Fnv1a64.AddInt64(hash, SlushCarveGrip.Raw);
            hash = Fnv1a64.AddInt64(hash, GroomedSkidGrip.Raw);
            hash = Fnv1a64.AddInt64(hash, PowderSkidGrip.Raw);
            hash = Fnv1a64.AddInt64(hash, CrudSkidGrip.Raw);
            hash = Fnv1a64.AddInt64(hash, IceSkidGrip.Raw);
            hash = Fnv1a64.AddInt64(hash, SlushSkidGrip.Raw);
            hash = Fnv1a64.AddInt64(hash, GroomedDrag.Raw);
            hash = Fnv1a64.AddInt64(hash, PowderDrag.Raw);
            hash = Fnv1a64.AddInt64(hash, CrudDrag.Raw);
            hash = Fnv1a64.AddInt64(hash, IceDrag.Raw);
            hash = Fnv1a64.AddInt64(hash, SlushDrag.Raw);
            hash = Fnv1a64.AddInt64(hash, GroomedSkidDrag.Raw);
            hash = Fnv1a64.AddInt64(hash, PowderSkidDrag.Raw);
            hash = Fnv1a64.AddInt64(hash, CrudSkidDrag.Raw);
            hash = Fnv1a64.AddInt64(hash, IceSkidDrag.Raw);
            hash = Fnv1a64.AddInt64(hash, SlushSkidDrag.Raw);
            hash = Fnv1a64.AddInt64(hash, GroomedPivotFactor.Raw);
            hash = Fnv1a64.AddInt64(hash, PowderPivotFactor.Raw);
            hash = Fnv1a64.AddInt64(hash, CrudPivotFactor.Raw);
            hash = Fnv1a64.AddInt64(hash, IcePivotFactor.Raw);
            hash = Fnv1a64.AddInt64(hash, SlushPivotFactor.Raw);
            hash = Fnv1a64.AddInt64(hash, AirDrag.Raw);
            hash = Fnv1a64.AddInt64(hash, TuckAirDragFactor.Raw);
            hash = Fnv1a64.AddInt64(hash, CarveAirDragFactor.Raw);
            hash = Fnv1a64.AddInt64(hash, MaxEdgeAngle.Raw);
            hash = Fnv1a64.AddInt64(hash, EdgeRate.Raw);
            hash = Fnv1a64.AddInt64(hash, CarveRadiusMin.Raw);
            hash = Fnv1a64.AddInt64(hash, CarveMinSpeed.Raw);
            hash = Fnv1a64.AddInt64(hash, CarveBlendSpeed.Raw);
            hash = Fnv1a64.AddInt64(hash, CarveTurnShare.Raw);
            hash = Fnv1a64.AddInt64(hash, PivotRate.Raw);
            hash = Fnv1a64.AddInt64(hash, PivotFadeSpeed.Raw);
            hash = Fnv1a64.AddInt64(hash, SkidStiffness.Raw);
            hash = Fnv1a64.AddInt64(hash, PivotSlipMax.Raw);
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
            hash = Fnv1a64.AddInt64(hash, MinScoredAirTime.Raw);
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
