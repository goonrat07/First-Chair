using System;
using System.Globalization;
using System.Text;

namespace FirstChair.Sim
{
    /// <summary>
    /// Names and scores single tricks (Story 006) from the data the sim's events carry. These are pure functions of an
    /// event's payload and the <see cref="Tuning"/>: <see cref="Simulation.Step"/> raises <see cref="SimEventKind.Land"/>
    /// without building any string, and this class, called outside the step, turns the flight into a name and points.
    /// </summary>
    /// <remarks>
    /// <para><b>Jump points</b> (design/reference/bluebird-spec.md, Single tricks):
    /// (<see cref="Tuning.TrickPointsPerHalfTurn"/> * h + <see cref="Tuning.TrickPointsPerFlip"/> * f + g
    /// + <see cref="Tuning.TrickPointsPerAirSecond"/> * t) * q * <see cref="Tuning.TrickMultiplier"/>(Flow). h is the spin's
    /// half turns and f the full flips, each rounded to the nearest whole count (a landing is always within
    /// <see cref="Tuning.SketchyLandingAngle"/> of one); t is the air time in seconds; Flow is the meter at touchdown
    /// (<see cref="AirSummary.Flow"/>); q is the landing grade's factor (<see cref="Tuning.CleanTrickFactor"/>,
    /// <see cref="Tuning.SolidTrickFactor"/>, <see cref="Tuning.SketchyTrickFactor"/>); and g is the grab bonus,
    /// <see cref="Tuning.GrabBasePoints"/> + <see cref="Tuning.GrabPointsPerSecond"/> * seconds held, at most
    /// <see cref="Tuning.GrabPointsCap"/>, and zero for a grab held less than <see cref="Tuning.GrabMinTime"/>.</para>
    /// <para><b>Names:</b> optional "Switch", then the flips ("Backflip", "Frontflip", "Double Backflip", ...), then the spin
    /// ("Left 720", "Right 180"), then the grab ("Japan"), named only when it earns the bonus. A jump with no flip, spin or
    /// counted grab is "Straight Air". The ID is the name in lower-case kebab-case: "left-720-japan".</para>
    /// <para><b>Grind points:</b> <see cref="Tuning.GrindPointsPerSecond"/> * seconds on the rail * balance
    /// (<see cref="GrindSummary.Balance"/>), times <see cref="Tuning.GrindFallFactor"/> when the grind ended in a fall.</para>
    /// <para>Points round to the nearest whole point after the repeat decay. Stateless and thread-safe; the run's history
    /// lives in <see cref="RunScore"/>.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// if (e.Kind == SimEventKind.Land)
    /// {
    ///     string name = TrickScoring.Name(e.Air, tuning);                      // "Left 720 Japan"
    ///     int points = TrickScoring.Points(TrickScoring.JumpValue(e.Grade, e.Air, tuning), repeats: 0, tuning);
    /// }
    /// </code>
    /// </example>
    public static class TrickScoring
    {
        /// <summary>The name of a jump with no flip, no spin and no counted grab.</summary>
        public const string StraightAir = "Straight Air";

        /// <summary>Degrees per half turn of spin, for spin names ("Left 540").</summary>
        private const int DegreesPerHalfTurn = 180;

        /// <summary>
        /// The most half turns or flips a jump counts. With the scored-time cap and the limits in Simulation.Validate,
        /// it keeps every score under about 8.8e8, inside the Q32.32 range.
        /// </summary>
        public const int MaxCount = 100;

        /// <summary>The most air time or grind time that scores, seconds (see <see cref="MaxCount"/>).</summary>
        public const int MaxScoredSeconds = 600;

        private static readonly Fix64 Half = Fix64.FromRatio(1, 2);
        private static readonly Fix64 ScoredSecondsCap = Fix64.FromInt(MaxScoredSeconds);
        private static readonly Fix64 PointsCap = Fix64.FromInt(int.MaxValue - 1);

        /// <summary>Half turns of spin, rounded to the nearest whole count, ignoring direction; at most <see cref="MaxCount"/>.</summary>
        /// <param name="spin">Total spin, radians (<see cref="AirSummary.Spin"/>).</param>
        public static int HalfTurns(Fix64 spin) => Capped(((Fix64.Abs(spin) + Fix64.HalfPi) / Fix64.Pi).FloorToInt());

        /// <summary>Full flips, rounded to the nearest whole count, ignoring direction; at most <see cref="MaxCount"/>.</summary>
        /// <param name="flip">Total flip, radians (<see cref="AirSummary.Flip"/>).</param>
        public static int Flips(Fix64 flip) => Capped(((Fix64.Abs(flip) + Fix64.Pi) / Fix64.TwoPi).FloorToInt());

        /// <summary>True when the flight lasted at least <see cref="Tuning.MinScoredAirTime"/>, so the jump scores.</summary>
        /// <param name="air">The flight.</param>
        /// <param name="tuning">The tuning.</param>
        public static bool AirCounts(AirSummary air, Tuning tuning) => air.AirSeconds >= tuning.MinScoredAirTime;

        /// <summary>True when the flight's grab was held at least <see cref="Tuning.GrabMinTime"/>.</summary>
        /// <param name="air">The flight.</param>
        /// <param name="tuning">The tuning.</param>
        public static bool GrabCounts(AirSummary air, Tuning tuning) => air.Grab != GrabKind.None && air.GrabSeconds >= tuning.GrabMinTime;

        /// <summary>The grab bonus g: base plus per-second, capped; zero for a grab that does not count.</summary>
        /// <param name="air">The flight.</param>
        /// <param name="tuning">The tuning.</param>
        public static Fix64 GrabBonus(AirSummary air, Tuning tuning) =>
            GrabCounts(air, tuning)
                ? Fix64.Min(tuning.GrabBasePoints + tuning.GrabPointsPerSecond * air.GrabSeconds, tuning.GrabPointsCap)
                : Fix64.Zero;

        /// <summary>The landing-quality factor q of a grade.</summary>
        /// <param name="grade">The landing grade; not <see cref="LandingGrade.None"/>.</param>
        /// <param name="tuning">The tuning.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="grade"/> is not a landing grade.</exception>
        public static Fix64 GradeFactor(LandingGrade grade, Tuning tuning) => grade switch
        {
            LandingGrade.Clean => tuning.CleanTrickFactor,
            LandingGrade.Solid => tuning.SolidTrickFactor,
            LandingGrade.Sketchy => tuning.SketchyTrickFactor,
            _ => throw new ArgumentOutOfRangeException(nameof(grade)),
        };

        /// <summary>A landed jump's points before repeat decay and rounding (the formula in the class remarks).</summary>
        /// <param name="grade">The landing grade (<see cref="SimEvent.Grade"/>).</param>
        /// <param name="air">The flight (<see cref="SimEvent.Air"/>).</param>
        /// <param name="tuning">The tuning.</param>
        public static Fix64 JumpValue(LandingGrade grade, AirSummary air, Tuning tuning)
        {
            Fix64 raw = tuning.TrickPointsPerHalfTurn * Fix64.FromInt(HalfTurns(air.Spin))
                + tuning.TrickPointsPerFlip * Fix64.FromInt(Flips(air.Flip))
                + GrabBonus(air, tuning)
                + tuning.TrickPointsPerAirSecond * Fix64.Min(air.AirSeconds, ScoredSecondsCap);
            return raw * GradeFactor(grade, tuning) * tuning.TrickMultiplier(Fix64.Clamp(air.Flow, Fix64.Zero, Fix64.One));
        }

        /// <summary>A finished grind's points before rounding (see the class remarks).</summary>
        /// <param name="grind">The grind (<see cref="SimEvent.Grind"/> of a <see cref="SimEventKind.GrindEnd"/>).</param>
        /// <param name="tuning">The tuning.</param>
        public static Fix64 GrindValue(GrindSummary grind, Tuning tuning)
        {
            Fix64 value = tuning.GrindPointsPerSecond * Fix64.Min(grind.Seconds, ScoredSecondsCap) * grind.Balance;
            return grind.Fell ? value * tuning.GrindFallFactor : value;
        }

        /// <summary>
        /// Whole points: <paramref name="value"/> times <see cref="Tuning.TrickRepeatFactor"/> once per earlier landing of
        /// the same name, rounded to the nearest point.
        /// </summary>
        /// <param name="value">Points before decay, from <see cref="JumpValue"/>.</param>
        /// <param name="repeats">Earlier landings of the same name in the run; zero for the first.</param>
        /// <param name="tuning">The tuning.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="repeats"/> is negative.</exception>
        public static int Points(Fix64 value, int repeats, Tuning tuning)
        {
            if (repeats < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(repeats));
            }

            for (int i = 0; i < repeats && value > Fix64.Zero; i++)
            {
                value = value * tuning.TrickRepeatFactor;
            }

            // Clamped so rounding can never wrap: no score is negative or past int range.
            return (Fix64.Clamp(value, Fix64.Zero, PointsCap) + Half).FloorToInt();
        }

        /// <summary>A count clamped to [0, <see cref="MaxCount"/>]; a wrapped negative count reads as the maximum.</summary>
        private static int Capped(int count) => count < 0 || count > MaxCount ? MaxCount : count;

        /// <summary>The trick's name (see the class remarks), for example "Switch Backflip Right 360 Mute".</summary>
        /// <param name="air">The flight.</param>
        /// <param name="tuning">The tuning (it decides whether the grab counts).</param>
        public static string Name(AirSummary air, Tuning tuning)
        {
            var name = new StringBuilder();
            if (air.Switch)
            {
                Append(name, "Switch");
            }

            int flips = Flips(air.Flip);
            if (flips > 0)
            {
                Append(name, FlipCount(flips) + (air.Flip > Fix64.Zero ? "Frontflip" : "Backflip"));
            }

            int halfTurns = HalfTurns(air.Spin);
            if (halfTurns > 0)
            {
                Append(name, (air.Spin > Fix64.Zero ? "Right " : "Left ") + (halfTurns * DegreesPerHalfTurn).ToString(CultureInfo.InvariantCulture));
            }

            bool grab = GrabCounts(air, tuning);
            if (grab)
            {
                Append(name, GrabName(air.Grab));
            }

            if (flips == 0 && halfTurns == 0 && !grab)
            {
                Append(name, StraightAir);
            }

            return name.ToString();
        }

        /// <summary>A trick's ID: its name in lower-case kebab-case ("Left 720 Japan" is "left-720-japan").</summary>
        /// <param name="name">The trick's name.</param>
        /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
        public static string Id(string name) =>
            (name ?? throw new ArgumentNullException(nameof(name))).ToLowerInvariant().Replace(' ', '-');

        /// <summary>The display name of a grab.</summary>
        /// <param name="grab">The grab; not <see cref="GrabKind.None"/>.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="grab"/> is not a grab.</exception>
        public static string GrabName(GrabKind grab) => grab switch
        {
            GrabKind.Mute => "Mute",
            GrabKind.Tip => "Tip Grab",
            GrabKind.Tail => "Tail Grab",
            GrabKind.Japan => "Japan",
            GrabKind.Safety => "Safety",
            _ => throw new ArgumentOutOfRangeException(nameof(grab)),
        };

        private static string FlipCount(int flips) => flips switch
        {
            1 => string.Empty,
            2 => "Double ",
            3 => "Triple ",
            4 => "Quad ",
            _ => flips.ToString(CultureInfo.InvariantCulture) + "x ",
        };

        private static void Append(StringBuilder name, string part)
        {
            if (name.Length > 0)
            {
                name.Append(' ');
            }

            name.Append(part);
        }
    }
}
