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
        /// How deep a ski cuts the snow at a point of the terrain, metres, as the snow mesh's shader finds it
        /// (<c>FCSnowSkinDepth</c>): each terrain layer's depth weighed by that layer's splat weight there, times the
        /// strength. The weights are read between the four nearest texels, bilinearly, with the texel centres on the
        /// terrain's corners and edges, as the terrain and the shader read their splat maps. For the skis' sink into the
        /// trench they cut (<c>SkierView</c>). Allocates nothing.
        /// </summary>
        /// <param name="weights">
        /// The splat weights, a byte (0 to 255) a layer a texel: layer <c>l</c> of the texel in column <c>c</c> and row
        /// <c>r</c> at <c>(r * texels + c) * layers + l</c>, columns along X and rows along Z, as
        /// <c>TerrainData.GetAlphamaps</c> orders them.
        /// </param>
        /// <param name="texels">Texels a side; at least 1.</param>
        /// <param name="layers">Layers a texel; at least 1.</param>
        /// <param name="u">The point across the terrain along X, 0 at its least X and 1 at its greatest; held to the terrain.</param>
        /// <param name="v">The same along Z.</param>
        /// <param name="layerDepths">Each layer's depth, metres; a layer past its end cuts nothing.</param>
        /// <param name="strength">Scales every depth; 0 cuts nothing.</param>
        /// <returns>The depth, metres.</returns>
        /// <exception cref="ArgumentException">Weights that don't hold <paramref name="layers"/> for every texel.</exception>
        /// <exception cref="ArgumentNullException">No depths.</exception>
        /// <example>
        /// <code>
        /// // GetAlphamaps' [z, x, layer] floats, kept as bytes once:
        /// float metres = SnowTrench.SplatDepth(weights, texels, layers, u, v, layerDepths, strength);
        /// </code>
        /// </example>
        public static float SplatDepth(byte[] weights, int texels, int layers, float u, float v, float[] layerDepths, float strength)
        {
            if (texels < 1 || layers < 1 || weights == null || weights.Length != texels * texels * layers)
            {
                throw new ArgumentException("The weights must hold every layer of every texel.", nameof(weights));
            }

            if (layerDepths == null)
            {
                throw new ArgumentNullException(nameof(layerDepths));
            }

            // Texel space: the first texel's centre at 0 and the last's at texels - 1, so the corners are texel centres.
            float last = texels - 1;
            float x = Clamp01(u) * last, z = Clamp01(v) * last;
            int column = Math.Min((int)x, Math.Max(texels - 2, 0)), row = Math.Min((int)z, Math.Max(texels - 2, 0));
            int nextColumn = Math.Min(column + 1, texels - 1), nextRow = Math.Min(row + 1, texels - 1);
            float fx = x - column, fz = z - row;
            int at00 = (row * texels + column) * layers, at10 = (row * texels + nextColumn) * layers;
            int at01 = (nextRow * texels + column) * layers, at11 = (nextRow * texels + nextColumn) * layers;

            float depth = 0f;
            int counted = Math.Min(layers, layerDepths.Length);
            for (int layer = 0; layer < counted; layer++)
            {
                float near = weights[at00 + layer] + (weights[at10 + layer] - weights[at00 + layer]) * fx;
                float far = weights[at01 + layer] + (weights[at11 + layer] - weights[at01 + layer]) * fx;
                depth += (near + (far - near) * fz) * layerDepths[layer];
            }

            return depth / FullWeight * strength;
        }

        /// <summary>
        /// How far the skis sink into snow that a ski cuts <paramref name="depth"/> deep, metres (Story 018). Firm snow
        /// holds them up: in a cut no deeper than <paramref name="ridesOver"/> they ride on top, their groove behind them;
        /// deeper, softer snow takes them down by the rest of the depth, times <paramref name="share"/>. A ski is under
        /// 2 cm thick and wider than the rounded trench it stamps, so sunk the groomer's whole 2 cm it was buried along
        /// its sides and looked thin (the Pine Bowl ride of build 49c1146); in powder the snow should cover it. No step
        /// as the depth crosses <paramref name="ridesOver"/>. Allocates nothing.
        /// </summary>
        /// <param name="depth">How deep a ski cuts the snow there, metres (<see cref="SplatDepth"/>).</param>
        /// <param name="ridesOver">The deepest cut the skis ride over, metres; below 0 counts as 0.</param>
        /// <param name="share">How much of the rest of the depth they sink, 1 for all of it; below 0 counts as 0.</param>
        /// <returns>The sink, metres; never below 0.</returns>
        /// <example>
        /// <code>
        /// float sink = SnowTrench.SkiSink(snow.DepthAt(position), settings.SnowSinkRidesOver, settings.SnowSinkWeight);
        /// </code>
        /// </example>
        public static float SkiSink(float depth, float ridesOver, float share) =>
            Math.Max(0f, depth - Math.Max(0f, ridesOver)) * Math.Max(0f, share);

        // A splat weight of 1, as a byte.
        private const float FullWeight = 255f;

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;

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
