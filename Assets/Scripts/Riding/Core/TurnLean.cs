using System;

namespace FirstChair.Riding
{
    /// <summary>
    /// How far a skier leans into a turn (Story 013). A rider in a turn leans until gravity balances the turn's pull:
    /// tan(lean) = speed x turn rate / g. The carve clips hold one lean whatever the speed, so the view adds the
    /// difference between that physical lean and the lean the blended clips already show. Positive is to the right
    /// (a turn toward positive steering), as the sim's edge angle.
    /// </summary>
    /// <example>
    /// <code>
    /// double physical = TurnLean.Physical(speed, turnRate, 9.81, maxLean);
    /// double shown = TurnLean.Shown(w[CarveLeft], w[CarveRight], w[RaceCarveLeft], w[RaceCarveRight], clipLean, raceClipLean);
    /// double extra = TurnLean.Extra(physical, shown, maxExtra);   // roll the body by this about the skis
    /// </code>
    /// </example>
    public static class TurnLean
    {
        /// <summary>The lean that balances the turn, radians, clamped to plus or minus <paramref name="maxLean"/>.</summary>
        /// <param name="speed">Speed along the path, m/s.</param>
        /// <param name="turnRate">How fast the path turns, rad/s, positive to the right.</param>
        /// <param name="gravity">Gravity, m/s², positive.</param>
        /// <param name="maxLean">The most the body leans, radians, positive.</param>
        /// <returns>Zero for settings that cannot give a lean (no gravity, no maximum, a value that is not a number).</returns>
        public static double Physical(double speed, double turnRate, double gravity, double maxLean)
        {
            if (!(gravity > 0.0) || !(maxLean > 0.0) || double.IsNaN(speed) || double.IsNaN(turnRate))
            {
                return 0.0;
            }

            double lean = Math.Atan(speed * turnRate / gravity);
            return Math.Max(-maxLean, Math.Min(maxLean, lean));
        }

        /// <summary>The lean the blended carve clips already show, radians, positive to the right.</summary>
        /// <param name="carveLeft">The CarveLeft clip's weight.</param>
        /// <param name="carveRight">The CarveRight clip's weight.</param>
        /// <param name="raceCarveLeft">The RaceCarveLeft clip's weight.</param>
        /// <param name="raceCarveRight">The RaceCarveRight clip's weight.</param>
        /// <param name="clipLean">How far a full carve clip leans the body, radians.</param>
        /// <param name="raceClipLean">How far a full race-carve clip leans the body, radians.</param>
        public static double Shown(
            double carveLeft, double carveRight, double raceCarveLeft, double raceCarveRight, double clipLean, double raceClipLean)
        {
            return (carveRight - carveLeft) * clipLean + (raceCarveRight - raceCarveLeft) * raceClipLean;
        }

        /// <summary>
        /// The roll still to add over what the clips show, radians, clamped to plus or minus <paramref name="maxExtra"/>:
        /// positive leans further right, negative stands a slow turn up against its clip.
        /// </summary>
        public static double Extra(double physical, double shown, double maxExtra)
        {
            if (!(maxExtra > 0.0))
            {
                return 0.0;
            }

            return Math.Max(-maxExtra, Math.Min(maxExtra, physical - shown));
        }
    }
}
