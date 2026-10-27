using System;

namespace FirstChair.Riding
{
    /// <summary>
    /// Which end of a ski its track starts from: the end that trails, the tail riding forward and the tip riding switch.
    /// The track ribbon is translucent and sits a little off the snow, above most of the ski's thickness, so a ribbon laid
    /// from under the boot showed through the back half of the ski (seen in the story 012 rides). Laid from the trailing
    /// end, it only ever lies where the ski has already passed.
    /// </summary>
    /// <remarks>
    /// The end changes only when the ski moves within the threshold angle of its own line, so a skid across the ski keeps
    /// the end it had, and so does a ski all but standing still. When it changes, the view breaks the ribbon, so no strip
    /// joins one end's track to the other's.
    /// </remarks>
    /// <example>
    /// <code>
    /// if (_end.Update(velocity, tipDirection, 0.5))
    /// {
    ///     ribbon.Break();
    /// }
    ///
    /// PoseVector start = skiBase + tipDirection * _end.Offset(tailBehind, tipAhead);
    /// </code>
    /// </example>
    public struct SkiTrailingEnd
    {
        // Slower than this, m/s, the ski is as good as standing still, and keeps its end.
        private const double StillSpeed = 0.5;

        /// <summary>True while the tip trails (riding switch); false while the tail does, as when riding forward.</summary>
        public bool TipTrails;

        /// <summary>Updates the trailing end from how the ski is moving.</summary>
        /// <param name="velocity">The ski's velocity, m/s, in any frame of reference.</param>
        /// <param name="tipDirection">The direction from the ski's tail to its tip, in the same frame; any length.</param>
        /// <param name="threshold">
        /// The cosine of the widest angle between the move and the ski's line at which the end may change: 0.5 changes it
        /// only for moves within 60 degrees of straight forward or straight back.
        /// </param>
        /// <returns>Whether the trailing end changed.</returns>
        public bool Update(PoseVector velocity, PoseVector tipDirection, double threshold)
        {
            double speed = velocity.Length;
            double tipLength = tipDirection.Length;
            if (speed < StillSpeed || tipLength < 1e-9)
            {
                return false;
            }

            double along = PoseVector.Dot(velocity, tipDirection) / (speed * tipLength);
            bool tipTrails = TipTrails;
            if (along > threshold)
            {
                tipTrails = false;
            }
            else if (along < -threshold)
            {
                tipTrails = true;
            }

            bool changed = tipTrails != TipTrails;
            TipTrails = tipTrails;
            return changed;
        }

        /// <summary>
        /// How far along the tip direction the trailing end lies from the ski's origin: back to the tail (negative) or out to
        /// the tip.
        /// </summary>
        /// <param name="tailBehind">The tail's distance behind the origin, metres.</param>
        /// <param name="tipAhead">The tip's distance ahead of the origin, metres.</param>
        public double Offset(double tailBehind, double tipAhead) => TipTrails ? Math.Max(0.0, tipAhead) : -Math.Max(0.0, tailBehind);
    }
}
