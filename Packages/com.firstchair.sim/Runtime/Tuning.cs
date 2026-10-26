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
        /// The edges' grip on groomed snow, m/s^2, on a level slope with the skis fully edged: the most sideways
        /// acceleration they hold (Story 009). It scales with the cosine of the slope and with
        /// <see cref="GripBase"/> + (1 - GripBase) * edge fraction; a turn asking more skids.
        /// </summary>
        public Fix64 GroomedCarveGrip;
        /// <summary>The edges' grip on powder, m/s^2 (see <see cref="GroomedCarveGrip"/>).</summary>
        public Fix64 PowderCarveGrip;
        /// <summary>The edges' grip on crud, m/s^2 (see <see cref="GroomedCarveGrip"/>).</summary>
        public Fix64 CrudCarveGrip;
        /// <summary>The edges' grip on ice, m/s^2 (see <see cref="GroomedCarveGrip"/>).</summary>
        public Fix64 IceCarveGrip;
        /// <summary>The edges' grip on slush, m/s^2 (see <see cref="GroomedCarveGrip"/>).</summary>
        public Fix64 SlushCarveGrip;
        /// <summary>
        /// Skid grip on groomed snow, m/s^2: the most sideways acceleration the snow puts on skis pivoted across the
        /// travel in a quick turn. It drags the velocity round after the skis; past it the skis slide.
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
        /// Quick-turn speed loss on groomed snow, per radian per second: pivoted skis sliding at a slip angle to the travel
        /// plow snow and lose SkidDrag * slip angle of their speed, as a fraction, each second.
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
        /// <summary>Air drag, per metre: the air slows a standing rider by AirDrag * speed^2 m/s^2.</summary>
        public Fix64 AirDrag;

        /// <summary>Fraction of <see cref="AirDrag"/> left while tucked (W held). Below 1 it raises terminal speed.</summary>
        public Fix64 TuckAirDragFactor;


        /// <summary>Edge angle at full steering, radians: <see cref="RiderState.EdgeAngle"/> is the edge fraction times this.</summary>
        public Fix64 MaxEdgeAngle;








        /// <summary>
        /// How hard the snow pushes back on pivoted skis per radian of slip, m/s^2 per radian, up to the snow's skid grip:
        /// a tyre's linear region before its limit. A quick turn always slips a little, slip angle = sideways
        /// acceleration / SkidStiffness, and the slip is what makes a hard quick turn cost speed. A carve's edges don't
        /// slip. Must be positive.
        /// </summary>
        public Fix64 SkidStiffness;


        /// <summary>How sharply skis turn on groomed snow: a factor on <see cref="TurnAcceleration"/> and <see cref="QuickTurnRate"/> (Story 009).</summary>
        public Fix64 GroomedTurnFactor;
        /// <summary>How sharply skis turn on powder (see <see cref="GroomedTurnFactor"/>): softer, surfier turns.</summary>
        public Fix64 PowderTurnFactor;
        /// <summary>How sharply skis turn on crud (see <see cref="GroomedTurnFactor"/>).</summary>
        public Fix64 CrudTurnFactor;
        /// <summary>How sharply skis turn on ice (see <see cref="GroomedTurnFactor"/>); ice turns hard but its grip lets go.</summary>
        public Fix64 IceTurnFactor;
        /// <summary>How sharply skis turn on slush (see <see cref="GroomedTurnFactor"/>).</summary>
        public Fix64 SlushTurnFactor;

        /// <summary>Seconds for a held key to take the edge from flat to full (Story 009, after the Bluebird prototype). Must be positive.</summary>
        public Fix64 EdgeRampTime;

        /// <summary>Seconds for a released edge to flatten from full. Must be positive.</summary>
        public Fix64 EdgeReleaseTime;

        /// <summary>
        /// Seconds to go edge to edge, full left to full right, when the other way is pressed: a flip snaps straight
        /// across instead of releasing and ramping. Must be positive.
        /// </summary>
        public Fix64 EdgeFlipTime;

        /// <summary>
        /// A carve's turn rate at full edge when slow, radians per second. Faster, the turn is held to
        /// <see cref="TurnAcceleration"/> sideways: turn rate = min(TurnRateLow, TurnAcceleration * turn factor / speed),
        /// so there is no dead zone between a slow turn and a fast one.
        /// </summary>
        public Fix64 TurnRateLow;

        /// <summary>
        /// A carve's sideways acceleration at full edge when fast, m/s^2: below the edges' grip, so a full carve holds
        /// instead of skidding.
        /// </summary>
        public Fix64 TurnAcceleration;

        /// <summary>
        /// Share of the edges' grip a flat ski still has, 0 to 1: the grip is CarveGrip * cos slope * (GripBase +
        /// (1 - GripBase) * edge fraction), so an edged ski holds harder than a flat one.
        /// </summary>
        public Fix64 GripBase;

        /// <summary>
        /// Share of the sideways speed the edges cancel that a carve turns into forward speed, 0 to 1: near 1, so carving
        /// keeps speed.
        /// </summary>
        public Fix64 CarveEfficiency;

        /// <summary>Deceleration per m/s of sideways speed the grip leaves (a skid), per second.</summary>
        public Fix64 ScrubDrag;

        /// <summary>Sideways speed left after the grip, m/s, above which the skis count as skidding.</summary>
        public Fix64 SkidSlipSpeed;

        /// <summary>Share of the carve's turn rate a tucked rider turns with, 0 to 1.</summary>
        public Fix64 TuckTurnFactor;

        /// <summary>How fast the skis pivot in a quick turn at full steering, radians per second (times the snow's turn factor).</summary>
        public Fix64 QuickTurnRate;

        /// <summary>
        /// The most a quick turn's skis lead the path, radians, from <see cref="QuickMinSpeed"/> up: past it they stop
        /// pivoting further across, so a held quick turn is a hard, steady skid. In (0, pi/2).
        /// </summary>
        public Fix64 QuickSlipMax;

        /// <summary>Speed below which a quick turn may pivot past <see cref="QuickSlipMax"/>, m/s: a slow rider turns on the spot.</summary>
        public Fix64 QuickMinSpeed;

        /// <summary>Share of the sideways speed a quick turn's snow cancels that turns into forward speed, 0 to 1.</summary>
        public Fix64 QuickKeep;

        /// <summary>How far ahead of the path a drift turn swings the skis at full steering, radians. In (0, pi/2).</summary>
        public Fix64 DriftAngle;

        /// <summary>How fast the skis swing out into a drift and follow it, radians per second. Must be positive.</summary>
        public Fix64 DriftSwingRate;

        /// <summary>Factor on the edges' grip while drifting: above 1 the sliding skis bite harder than a carve, so a drift turns tighter.</summary>
        public Fix64 DriftGripFactor;

        /// <summary>Share of the sideways speed a drift's grip cancels that turns into forward speed, 0 to 1.</summary>
        public Fix64 DriftKeep;

        /// <summary>Seconds a drift must be held for its release to earn <see cref="DriftBoost"/>.</summary>
        public Fix64 DriftBoostTime;

        /// <summary>Speed added along the skis when a drift held for <see cref="DriftBoostTime"/> is released, m/s.</summary>
        public Fix64 DriftBoost;

        /// <summary>How far across the path back plus steering throws the skis (a hockey stop), radians. In (0, pi).</summary>
        public Fix64 HockeyAngle;

        /// <summary>How fast the skis swing across into a hockey stop, radians per second. Must be positive.</summary>
        public Fix64 HockeySwingRate;

        /// <summary>Speed below which back plus steering doesn't throw the skis across, m/s: a stopped rider just turns.</summary>
        public Fix64 HockeyMinSpeed;

        /// <summary>Factor on the edges' grip while braking, 0 to 1.</summary>
        public Fix64 BrakeGripFactor;

        /// <summary>Share of the sideways speed a brake's grip cancels that turns into forward speed, 0 to 1: low, so it scrubs.</summary>
        public Fix64 BrakeKeep;

        /// <summary>Speed one skate push adds along the skis, m/s (an edge flip from at least <see cref="SkateMinEdge"/>).</summary>
        public Fix64 SkatePush;

        /// <summary>Speed above which edge flips no longer skate, m/s; a push never takes the rider past it.</summary>
        public Fix64 SkateMaxSpeed;

        /// <summary>Shortest time between skate pushes, seconds.</summary>
        public Fix64 SkateInterval;

        /// <summary>Edge fraction, 0 to 1, a flip must start from to skate: small wiggles don't push.</summary>
        public Fix64 SkateMinEdge;

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
            // Story 008 (the user's playtest, 2026-10-24: "the slope alone should accelerate me like in GMA2"): groomed
            // friction 0.035 (the research's 0.04) and air drag for C_d A 0.4 m^2 at 80 kg, so the slope alone accelerates
            // the rider hard. Story 009: every snow type's edges grip, about 2 g on groomed snow; powder and crud less
            // (softer, surfier turns), ice least (the Bluebird prototype's 0.55 g), so fast turns there skid.
            GroomedCarveGrip = Fix64.FromInt(20),
            PowderCarveGrip = Fix64.FromInt(13),
            CrudCarveGrip = Fix64.FromInt(16),
            IceCarveGrip = Fix64.FromInt(6),
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
            AirDrag = Fix64.FromRatio(28, 10000),
            TuckAirDragFactor = Fix64.FromRatio(55, 100),
            MaxEdgeAngle = Fix64.FromRatio(105, 100),
            SkidStiffness = Fix64.FromInt(400),
            // Story 009 (the user's playtest, 2026-10-26: copy GMA2's riding, adapted to the keyboard). Plain steering carves
            // as in the Bluebird prototype the user found close (edge in 0.2 s, out in 0.11 s, across in 0.09 s; 2.3 rad/s
            // slow, 1.6 g fast; 99% of the cancelled sideways speed kept). The quick turn, drift, hockey stop and skating
            // were tuned with a prototype of this step: a 60 degree turn at 54 km/h carves in 1.07 s for 7% of the speed,
            // quick-turns in 0.70 s for 15%, drifts in 0.66 s for 12%; a drift held 0.8 s gives its loss back on release.
            GroomedTurnFactor = Fix64.One,
            PowderTurnFactor = Fix64.FromRatio(8, 10),
            CrudTurnFactor = Fix64.FromRatio(9, 10),
            IceTurnFactor = Fix64.One,
            SlushTurnFactor = Fix64.FromRatio(85, 100),
            EdgeRampTime = Fix64.FromRatio(2, 10),
            EdgeReleaseTime = Fix64.FromRatio(11, 100),
            EdgeFlipTime = Fix64.FromRatio(9, 100),
            TurnRateLow = Fix64.FromRatio(23, 10),
            TurnAcceleration = Fix64.FromRatio(157, 10),
            GripBase = Fix64.FromRatio(35, 100),
            CarveEfficiency = Fix64.FromRatio(99, 100),
            ScrubDrag = Fix64.FromRatio(3, 10),
            SkidSlipSpeed = Fix64.One,
            TuckTurnFactor = Fix64.FromRatio(6, 10),
            QuickTurnRate = Fix64.FromRatio(46, 10),
            QuickSlipMax = Fix64.FromRatio(3, 10),
            QuickMinSpeed = Fix64.FromInt(5),
            QuickKeep = Fix64.FromRatio(85, 100),
            DriftAngle = Fix64.FromRatio(35, 100),
            DriftSwingRate = Fix64.FromInt(3),
            DriftGripFactor = Fix64.FromRatio(12, 10),
            DriftKeep = Fix64.FromRatio(92, 100),
            DriftBoostTime = Fix64.FromRatio(1, 2),
            DriftBoost = Fix64.FromInt(2),
            HockeyAngle = Fix64.FromRatio(135, 100),
            HockeySwingRate = Fix64.FromRatio(46, 10),
            HockeyMinSpeed = Fix64.One,
            BrakeGripFactor = Fix64.FromRatio(1, 2),
            BrakeKeep = Fix64.FromRatio(15, 100),
            SkatePush = Fix64.FromRatio(8, 10),
            SkateMaxSpeed = Fix64.FromInt(8),
            SkateInterval = Fix64.FromRatio(3, 10),
            SkateMinEdge = Fix64.FromRatio(1, 2),
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


        /// <summary>The turn factor of a snow type (Story 009).</summary>
        /// <param name="snow">The snow type.</param>
        public readonly Fix64 TurnFactor(SnowType snow) => snow switch
        {
            SnowType.Groomed => GroomedTurnFactor,
            SnowType.Powder => PowderTurnFactor,
            SnowType.Crud => CrudTurnFactor,
            SnowType.Ice => IceTurnFactor,
            SnowType.Slush => SlushTurnFactor,
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
            hash = Fnv1a64.AddInt64(hash, AirDrag.Raw);
            hash = Fnv1a64.AddInt64(hash, TuckAirDragFactor.Raw);
            hash = Fnv1a64.AddInt64(hash, MaxEdgeAngle.Raw);
            hash = Fnv1a64.AddInt64(hash, SkidStiffness.Raw);
            hash = Fnv1a64.AddInt64(hash, GroomedTurnFactor.Raw);
            hash = Fnv1a64.AddInt64(hash, PowderTurnFactor.Raw);
            hash = Fnv1a64.AddInt64(hash, CrudTurnFactor.Raw);
            hash = Fnv1a64.AddInt64(hash, IceTurnFactor.Raw);
            hash = Fnv1a64.AddInt64(hash, SlushTurnFactor.Raw);
            hash = Fnv1a64.AddInt64(hash, EdgeRampTime.Raw);
            hash = Fnv1a64.AddInt64(hash, EdgeReleaseTime.Raw);
            hash = Fnv1a64.AddInt64(hash, EdgeFlipTime.Raw);
            hash = Fnv1a64.AddInt64(hash, TurnRateLow.Raw);
            hash = Fnv1a64.AddInt64(hash, TurnAcceleration.Raw);
            hash = Fnv1a64.AddInt64(hash, GripBase.Raw);
            hash = Fnv1a64.AddInt64(hash, CarveEfficiency.Raw);
            hash = Fnv1a64.AddInt64(hash, ScrubDrag.Raw);
            hash = Fnv1a64.AddInt64(hash, SkidSlipSpeed.Raw);
            hash = Fnv1a64.AddInt64(hash, TuckTurnFactor.Raw);
            hash = Fnv1a64.AddInt64(hash, QuickTurnRate.Raw);
            hash = Fnv1a64.AddInt64(hash, QuickSlipMax.Raw);
            hash = Fnv1a64.AddInt64(hash, QuickMinSpeed.Raw);
            hash = Fnv1a64.AddInt64(hash, QuickKeep.Raw);
            hash = Fnv1a64.AddInt64(hash, DriftAngle.Raw);
            hash = Fnv1a64.AddInt64(hash, DriftSwingRate.Raw);
            hash = Fnv1a64.AddInt64(hash, DriftGripFactor.Raw);
            hash = Fnv1a64.AddInt64(hash, DriftKeep.Raw);
            hash = Fnv1a64.AddInt64(hash, DriftBoostTime.Raw);
            hash = Fnv1a64.AddInt64(hash, DriftBoost.Raw);
            hash = Fnv1a64.AddInt64(hash, HockeyAngle.Raw);
            hash = Fnv1a64.AddInt64(hash, HockeySwingRate.Raw);
            hash = Fnv1a64.AddInt64(hash, HockeyMinSpeed.Raw);
            hash = Fnv1a64.AddInt64(hash, BrakeGripFactor.Raw);
            hash = Fnv1a64.AddInt64(hash, BrakeKeep.Raw);
            hash = Fnv1a64.AddInt64(hash, SkatePush.Raw);
            hash = Fnv1a64.AddInt64(hash, SkateMaxSpeed.Raw);
            hash = Fnv1a64.AddInt64(hash, SkateInterval.Raw);
            hash = Fnv1a64.AddInt64(hash, SkateMinEdge.Raw);
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
