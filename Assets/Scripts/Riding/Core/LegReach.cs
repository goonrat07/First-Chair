using System;

namespace FirstChair.Riding
{
    /// <summary>
    /// How far the hips must come down so both boots can reach the snow under them (Story 013). A foot can always bend
    /// up to raised snow; reaching down is limited by the leg, so when one leg would have to be longer than it is, the
    /// hips drop by what it lacks and the other knee bends more.
    /// </summary>
    /// <example>
    /// <code>
    /// double drop = LegReach.HipDrop(hipL - footTargetL, hipR - footTargetR, up, reachL, reachR, maxDrop);
    /// hips.position -= up * drop;   // then solve each leg with TwoBoneIk
    /// </code>
    /// </example>
    public static class LegReach
    {
        /// <summary>The hips' drop, metres, from 0 to <paramref name="maxDrop"/>.</summary>
        /// <param name="left">From the left foot's target to the left hip joint.</param>
        /// <param name="right">From the right foot's target to the right hip joint.</param>
        /// <param name="up">The body's unit up: the hips move down along it.</param>
        /// <param name="reachLeft">The left leg's longest reach, hip to sole, metres.</param>
        /// <param name="reachRight">The right leg's longest reach, metres.</param>
        /// <param name="maxDrop">The most the hips may drop, metres; zero or less means they never move.</param>
        public static double HipDrop(PoseVector left, PoseVector right, PoseVector up, double reachLeft, double reachRight, double maxDrop)
        {
            if (!(maxDrop > 0.0))
            {
                return 0.0;
            }

            double need = Math.Max(Need(left, up, reachLeft), Need(right, up, reachRight));
            return Math.Max(0.0, Math.Min(maxDrop, need));
        }

        // The smallest drop d >= 0 with |q - up d| <= reach, or infinity when no drop brings the foot into reach.
        private static double Need(PoseVector q, PoseVector up, double reach)
        {
            double height = PoseVector.Dot(q, up);
            double across = Math.Max(0.0, PoseVector.Dot(q, q) - height * height);
            double room = reach * reach - across;
            if (room < 0.0)
            {
                return double.PositiveInfinity;
            }

            return Math.Max(0.0, height - Math.Sqrt(room));
        }
    }
}
