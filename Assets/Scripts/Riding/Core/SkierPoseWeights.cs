using System;
using FirstChair.Sim;

namespace FirstChair.Riding
{
    /// <summary>The skier's poses, in the order of the clips in <c>CH_Skier.fbx</c> (its <c>animation-contract.json</c>).</summary>
    public enum SkierPose
    {
        /// <summary>The athletic stance, with a slow breathing loop.</summary>
        Stance = 0,

        /// <summary>A full carve on the left edges: inclined and angulated into the turn.</summary>
        CarveLeft = 1,

        /// <summary>A full carve on the right edges.</summary>
        CarveRight = 2,

        /// <summary>A deep downhill tuck.</summary>
        Tuck = 3,

        /// <summary>Loaded low before a pop.</summary>
        Crouch = 4,

        /// <summary>Balanced in the air.</summary>
        Air = 5,

        /// <summary>A hard skid stop.</summary>
        Brake = 6,

        /// <summary>A double-pole push cycle, for flats.</summary>
        Pole = 7,

        /// <summary>Fallen on the snow.</summary>
        Crash = 8,
    }

    /// <summary>
    /// Turns the sim's rider state into a weight for each <see cref="SkierPose"/>, so the view can blend the skier's
    /// pose clips (Story 003). Presentation only: it reads sim state and writes nothing back.
    /// </summary>
    /// <remarks>
    /// Rules, in order: a crash shows <see cref="SkierPose.Crash"/>; in the air, <see cref="SkierPose.Air"/>; on snow, the
    /// stance (<see cref="Stance.Tuck"/>, <see cref="Stance.Brake"/> or <see cref="Stance.Pole"/>) takes over from the
    /// carve blend, the carve blend goes from <see cref="SkierPose.CarveLeft"/> through <see cref="SkierPose.Stance"/> to
    /// <see cref="SkierPose.CarveRight"/> with the edge angle, and loading a pop crouches the whole pose. Weights always
    /// sum to 1. <see cref="Smooth"/> eases the shown weights toward the target so changes don't snap.
    /// </remarks>
    /// <example>
    /// <code>
    /// SkierPoseWeights.Target(session.Current, session.Tuning, target);
    /// SkierPoseWeights.Smooth(shown, target, Time.deltaTime, 0.12f);
    /// for (int i = 0; i &lt; SkierPoseWeights.Count; i++) mixer.SetInputWeight(i, shown[i]);
    /// </code>
    /// </example>
    public static class SkierPoseWeights
    {
        /// <summary>How many poses.</summary>
        public const int Count = 9;

        /// <summary>The clip name of each pose, as exported from Blender.</summary>
        /// <param name="pose">The pose.</param>
        public static string ClipName(SkierPose pose) => pose.ToString();

        /// <summary>Fills <paramref name="weights"/> (length <see cref="Count"/>) with the target weights for a state.</summary>
        /// <param name="state">The rider state (normally the latest sim state).</param>
        /// <param name="tuning">The run's tuning, for the full edge angle and the pop load time.</param>
        /// <param name="weights">Output weights, summing to 1.</param>
        /// <exception cref="ArgumentException"><paramref name="weights"/> is not <see cref="Count"/> long.</exception>
        public static void Target(in RiderState state, in Tuning tuning, float[] weights)
        {
            double maxEdge = SimCoordinates.ToDouble(tuning.MaxEdgeAngle);
            double edge = maxEdge > 0.0 ? SimCoordinates.ToDouble(state.EdgeAngle) / maxEdge : 0.0;
            double popSteps = SimCoordinates.ToDouble(tuning.PopLoadTime) * Simulation.StepsPerSecond;
            double load = popSteps > 0.0 ? state.PopLoadTicks / popSteps : 0.0;
            Target(edge, load, state.Stance, state.Airborne, state.Crashed, weights);
        }

        /// <summary>The target weights from plain numbers (testable without a sim).</summary>
        /// <param name="edgeFraction">Edge angle over the full edge angle: -1 is full left, +1 full right.</param>
        /// <param name="popLoadFraction">How loaded a pop is, 0 to 1.</param>
        /// <param name="stance">The sim's stance.</param>
        /// <param name="airborne">In the air.</param>
        /// <param name="crashed">Crashed.</param>
        /// <param name="weights">Output weights, summing to 1.</param>
        /// <exception cref="ArgumentException"><paramref name="weights"/> is not <see cref="Count"/> long.</exception>
        public static void Target(double edgeFraction, double popLoadFraction, Stance stance, bool airborne, bool crashed, float[] weights)
        {
            if (weights == null || weights.Length != Count)
            {
                throw new ArgumentException("Weights must be " + Count + " long.", nameof(weights));
            }

            Array.Clear(weights, 0, Count);
            if (crashed)
            {
                weights[(int)SkierPose.Crash] = 1f;
                return;
            }

            if (airborne)
            {
                weights[(int)SkierPose.Air] = 1f;
                return;
            }

            SkierPose? held = stance == Stance.Tuck ? SkierPose.Tuck
                : stance == Stance.Brake ? SkierPose.Brake
                : stance == Stance.Pole ? SkierPose.Pole
                : (SkierPose?)null;
            if (held.HasValue)
            {
                weights[(int)held.Value] = 1f;
                return;
            }

            float edge = (float)Math.Max(-1.0, Math.Min(1.0, edgeFraction));
            float load = (float)Math.Max(0.0, Math.Min(1.0, popLoadFraction));
            float carve = 1f - load;
            weights[(int)SkierPose.CarveLeft] = carve * Math.Max(0f, -edge);
            weights[(int)SkierPose.CarveRight] = carve * Math.Max(0f, edge);
            weights[(int)SkierPose.Stance] = carve * (1f - Math.Abs(edge));
            weights[(int)SkierPose.Crouch] = load;
        }

        /// <summary>
        /// Eases <paramref name="shown"/> toward <paramref name="target"/> over <paramref name="smoothTime"/> seconds,
        /// frame-rate independent, and keeps the sum at 1. A smoothing time of 0 or less snaps.
        /// </summary>
        /// <param name="shown">The weights shown last frame; updated in place.</param>
        /// <param name="target">The target weights.</param>
        /// <param name="deltaSeconds">Frame time, seconds.</param>
        /// <param name="smoothTime">Smoothing time, seconds.</param>
        /// <exception cref="ArgumentException">Either array is not <see cref="Count"/> long.</exception>
        public static void Smooth(float[] shown, float[] target, float deltaSeconds, float smoothTime)
        {
            if (shown == null || target == null || shown.Length != Count || target.Length != Count)
            {
                throw new ArgumentException("Weights must be " + Count + " long.");
            }

            float blend = FollowCameraMath.SmoothingFactor(smoothTime, deltaSeconds);
            float sum = 0f;
            for (int i = 0; i < Count; i++)
            {
                shown[i] += (target[i] - shown[i]) * blend;
                sum += shown[i];
            }

            if (sum <= 1e-6f)
            {
                Array.Copy(target, shown, Count);
                return;
            }

            for (int i = 0; i < Count; i++)
            {
                shown[i] /= sum;
            }
        }
    }
}
