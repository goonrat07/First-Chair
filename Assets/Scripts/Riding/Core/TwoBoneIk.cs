using System;

namespace FirstChair.Riding
{
    /// <summary>
    /// The analytic two-bone solve (Story 013): a thigh and shin reaching for the snow under the boot, or an upper arm
    /// and forearm placing the hand that holds a planted pole. Both bone lengths are kept; the middle joint bends in the
    /// plane of the hint, or in the plane the clip already bent it in when the hint gives none.
    /// </summary>
    /// <example>
    /// <code>
    /// TwoBoneIk.Solve(hip, knee, ankle, snowUnderBoot, kneeHint, out PoseVector newKnee, out PoseVector newAnkle);
    /// // then turn the thigh from (knee - hip) to (newKnee - hip) and the shin from its old to its new direction
    /// </code>
    /// </example>
    public static class TwoBoneIk
    {
        // How far short of straight, or of folded flat, the solve stops, as a fraction of the reach: a chain dead
        // straight has no bend plane left, and the knee pops when it unbends past it.
        private const double Slack = 1e-4;

        /// <summary>Solves the chain root, mid, end so the end reaches <paramref name="target"/>, or as near as it can.</summary>
        /// <param name="root">The fixed joint (hip, shoulder).</param>
        /// <param name="mid">The middle joint as the clip posed it (knee, elbow).</param>
        /// <param name="end">The end joint as the clip posed it (ankle, wrist).</param>
        /// <param name="target">Where the end should go.</param>
        /// <param name="hint">A point the middle joint should bend toward (in front of the knee, behind the elbow).</param>
        /// <param name="newMid">The middle joint's new position.</param>
        /// <param name="newEnd">The end's new position: the target when it is in reach, else on the line to it.</param>
        public static void Solve(
            PoseVector root, PoseVector mid, PoseVector end, PoseVector target, PoseVector hint, out PoseVector newMid, out PoseVector newEnd)
        {
            double upper = (mid - root).Length;
            double lower = (end - mid).Length;
            PoseVector toTarget = target - root;
            PoseVector dir = toTarget.Normalized();
            if (dir.Length < 0.5)
            {
                dir = (end - root).Normalized();
            }

            if (dir.Length < 0.5)
            {
                newMid = mid;
                newEnd = end;
                return;
            }

            double reach = upper + lower;
            double shortest = Math.Abs(upper - lower);
            double distance = Math.Max(shortest + Slack * reach, Math.Min(reach * (1.0 - Slack), toTarget.Length));

            PoseVector bend = Perpendicular(hint - root, dir);
            if (bend.Length < 0.5)
            {
                bend = Perpendicular(mid - root, dir);
            }

            if (bend.Length < 0.5)
            {
                bend = Perpendicular(new PoseVector(0.0, 0.0, 1.0), dir);
            }

            if (bend.Length < 0.5)
            {
                bend = Perpendicular(new PoseVector(1.0, 0.0, 0.0), dir);
            }

            // Law of cosines: how far along the line the middle joint sits, and how far out of it
            double along = (upper * upper - lower * lower + distance * distance) / (2.0 * distance);
            double across = Math.Sqrt(Math.Max(0.0, upper * upper - along * along));
            newMid = root + dir * along + bend * across;
            newEnd = root + dir * distance;
        }

        // The unit part of v square to the unit direction d, or zero when v lies along d.
        private static PoseVector Perpendicular(PoseVector v, PoseVector d)
        {
            PoseVector p = v - d * PoseVector.Dot(v, d);
            return p.Length > 1e-9 ? p.Normalized() : PoseVector.Zero;
        }
    }
}
