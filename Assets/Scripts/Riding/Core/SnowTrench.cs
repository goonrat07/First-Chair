using System;

namespace FirstChair.Riding
{
    /// <summary>
    /// The snow's height change at a ski track (Story 018), as the snow mesh's vertex shader computes it
    /// (<c>Assets/Shaders/SnowTerrain/SH_Env_SnowSkin.hlsl</c>, which mirrors <see cref="Offset"/>): pushed down in the
    /// trench and raised beside it, where the ski pushed the snow aside. The trench is the stamped track profile
    /// (<c>TrackGrooves</c>, 1 on a track's centre line); its spread is the same profile blurred, which reaches
    /// past the trench's edge, so the rim follows every track, crossing and track end. Engine-free, so it is testable.
    /// </summary>
    /// <example>
    /// <code>
    /// float metres = SnowTrench.Offset(trench, spread, powderDepth, raise);   // below 0 in the trench, above 0 on its rim
    /// var weights = new float[5];
    /// SnowTrench.GaussianWeights(sigmaTexels, weights);                       // the blur's taps, centre first
    /// </code>
    /// </example>
    public static class SnowTrench
    {
        /// <summary>The snow's height change at a point, metres: down by the trench, up by the snow pushed beside it.</summary>
        /// <param name="trench">The stamped track profile there, 0 to 1 (1 on a track's centre line).</param>
        /// <param name="spread">The same profile blurred, 0 to 1: where it is above the trench, snow was pushed there.</param>
        /// <param name="depth">How deep a ski cuts into this snow, metres.</param>
        /// <param name="raise">How much of the spread past the trench rises, in the trench's depth; 0 is no rim.</param>
        /// <returns>The height change, metres: negative in the trench, positive on its rim.</returns>
        public static float Offset(float trench, float spread, float depth, float raise)
        {
            float rim = Math.Max(0f, spread - trench);
            return depth * (raise * rim - trench);
        }

        /// <summary>
        /// Fills <paramref name="weights"/> with a normalised Gaussian's taps, centre first: the centre and both sides of
        /// every other tap sum to 1, as a separable blur uses them.
        /// </summary>
        /// <param name="sigma">The Gaussian's width, in taps; above 0.</param>
        /// <param name="weights">Receives the centre tap's weight, then each tap's out to one side; at least one long.</param>
        /// <exception cref="ArgumentOutOfRangeException">A width of 0 or less.</exception>
        /// <exception cref="ArgumentException">No taps.</exception>
        public static void GaussianWeights(float sigma, float[] weights)
        {
            if (!(sigma > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(sigma), sigma, "The width must be above 0.");
            }

            if (weights == null || weights.Length == 0)
            {
                throw new ArgumentException("The blur needs at least its centre tap.", nameof(weights));
            }

            double sum = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                double w = Math.Exp(-0.5 * i * i / (sigma * sigma));
                weights[i] = (float)w;
                sum += i == 0 ? w : 2 * w;
            }

            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] = (float)(weights[i] / sum);
            }
        }
    }
}
