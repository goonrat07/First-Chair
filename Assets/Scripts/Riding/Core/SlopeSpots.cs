using System;
using FirstChair.Sim;

namespace FirstChair.Riding
{
    /// <summary>
    /// Places the Feel Lab can start a run at, to try a feature without riding all the way to it (Story 010). Engine-free,
    /// so it is testable without Unity.
    /// </summary>
    /// <example>
    /// <code>
    /// if (SlopeSpots.AboveTheKickers(layout, 40.0, out double x, out double z))
    /// {
    ///     driver.RestartRunAt(driver.RunTuning, SimCoordinates.FromMetres(x), SimCoordinates.FromMetres(z));
    /// }
    /// </code>
    /// </example>
    public static class SlopeSpots
    {
        /// <summary>
        /// A start <paramref name="approach"/> metres back up the first kicker's run-in from its foot, so a rider who lets
        /// go rolls straight into it. The first kicker is the one furthest up the fall line (least Z).
        /// </summary>
        /// <param name="layout">The slope's layout.</param>
        /// <param name="approach">How far above the kicker's foot to start, metres.</param>
        /// <param name="x">The start's X, metres.</param>
        /// <param name="z">The start's Z, metres.</param>
        /// <returns>False, with (0, 0), when the slope has no kickers.</returns>
        public static bool AboveTheKickers(SlopeLayout layout, double approach, out double x, out double z)
        {
            x = 0.0;
            z = 0.0;
            if (layout.Kickers.Count == 0)
            {
                return false;
            }

            SlopeKicker first = layout.Kickers[0];
            foreach (SlopeKicker kicker in layout.Kickers)
            {
                if (kicker.ZMm < first.ZMm)
                {
                    first = kicker;
                }
            }

            // The kicker launches along its facing, (sin, cos) in (X, Z); its foot is its length back from the lip.
            double facing = first.FacingMillidegrees / 1000.0 * Math.PI / 180.0;
            double back = first.LengthMm / 1000.0 + approach;
            x = first.XMm / 1000.0 - Math.Sin(facing) * back;
            z = first.ZMm / 1000.0 - Math.Cos(facing) * back;
            return true;
        }
    }
}
