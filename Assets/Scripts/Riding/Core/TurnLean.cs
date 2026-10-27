using System;

namespace FirstChair.Riding
{
    /// <summary>
    /// How far a skier leans into a turn (Story 013). A rider in a turn leans until gravity balances the turn's pull:
    /// tan(lean) = speed x turn rate / g. The carve clips hold one lean whatever the speed, and the skier's frame is
    /// tilted to the snow, so the view adds the difference between that physical lean and the lean the blended clips and
    /// the frame already show. Positive is to the right (a turn toward positive steering), as the sim's edge angle.
    /// </summary>
    /// <example>
    /// <code>
    /// double physical = TurnLean.Physical(speed, turnRate, 9.81, maxLean);
    /// double shown = TurnLean.Shown(w[CarveLeft], w[CarveRight], w[RaceCarveLeft], w[RaceCarveRight], clipLean, raceClipLean);
    /// double frame = TurnLean.FrameRoll(upInFrame.x, upInFrame.y);          // world up as the skier's frame sees it
    /// double extra = TurnLean.ExtraOverFrame(physical, shown, frame, maxExtra);   // roll the body by this about the skis
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
        /// How far a frame leans right of vertical, radians, from world up as the frame sees it. The skier's frame is
        /// tilted to the snow (<c>RiderPlacement</c>), so on a traverse it already leans downhill by up to the slope's angle.
        /// </summary>
        /// <param name="upRight">World up's component along the frame's right.</param>
        /// <param name="upUp">World up's component along the frame's up.</param>
        public static double FrameRoll(double upRight, double upUp) => Math.Atan2(-upRight, upUp);

        /// <summary>
        /// The roll still to add over all the lean the skier already shows, radians, clamped to plus or minus
        /// <paramref name="maxExtra"/>: the clips' own (<see cref="Shown"/>) and its frame's tilt from vertical
        /// (<see cref="FrameRoll"/>). Physics sets the lean from vertical, so both count; counting the clips alone put a
        /// slow skier on a 17 degree slope almost flat on the snow (story 013's first ride).
        /// </summary>
        /// <param name="physical">The lean the turn needs (<see cref="Physical"/>).</param>
        /// <param name="clipsShown">The lean the clips show (<see cref="Shown"/>).</param>
        /// <param name="frameRoll">The frame's lean from vertical (<see cref="FrameRoll"/>).</param>
        /// <param name="maxExtra">The most the layer adds or takes away, radians, positive.</param>
        public static double ExtraOverFrame(double physical, double clipsShown, double frameRoll, double maxExtra) =>
            Extra(physical, clipsShown + frameRoll, maxExtra);

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
