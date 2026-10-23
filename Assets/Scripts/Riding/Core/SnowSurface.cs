using System;
using FirstChair.Sim;

namespace FirstChair.Riding
{
    /// <summary>
    /// The look of each <see cref="SnowType"/> on the test slope's terrain, and the terrain's layer weights painted from
    /// the sim's snow map (Story 003). Presentation only: the sim reads the snow map, this only draws it. Everything is
    /// generated, tileable and deterministic, so a build makes the same textures every time.
    /// </summary>
    /// <remarks>
    /// <para>Groomed snow has corduroy ridges down the fall line, which show speed and direction at the follow camera.
    /// Powder is soft and bumpy, crud is chunky, ice is grey-blue and streaky, slush is a warmer grey.</para>
    /// <para>Textures are RGBA32, row 0 at the bottom (Unity's <c>Texture2D.SetPixelData</c> order). Normals are tangent
    /// space with +Y up the texture (Unity's convention), encoded as 0.5 + 0.5 n.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var albedo = new byte[size * size * 4];
    /// var normal = new byte[size * size * 4];
    /// SnowSurface.Texture(SnowType.Groomed, size, albedo, normal);
    /// </code>
    /// </example>
    public static class SnowSurface
    {
        /// <summary>How many terrain layers: one per <see cref="SnowType"/>, in its numeric order.</summary>
        public const int LayerCount = 5;

        /// <summary>Tile size of a layer on the terrain, metres.</summary>
        /// <param name="kind">The snow type.</param>
        public static float TileMetres(SnowType kind)
        {
            switch (kind)
            {
                case SnowType.Groomed: return 6f;
                case SnowType.Powder: return 10f;
                case SnowType.Crud: return 8f;
                case SnowType.Ice: return 12f;
                default: return 8f;
            }
        }

        /// <summary>Smoothness of a layer, 0 to 1.</summary>
        /// <param name="kind">The snow type.</param>
        public static float Smoothness(SnowType kind)
        {
            switch (kind)
            {
                case SnowType.Ice: return 0.6f;
                case SnowType.Slush: return 0.35f;
                case SnowType.Groomed: return 0.15f;
                default: return 0.1f;
            }
        }

        /// <summary>
        /// Fills a square albedo and normal texture for a snow type. Both arrays must hold <c>size * size * 4</c> bytes.
        /// </summary>
        /// <param name="kind">The snow type.</param>
        /// <param name="size">Texture size, pixels; at least 8.</param>
        /// <param name="albedoRgba">Albedo, RGBA32.</param>
        /// <param name="normalRgba">Tangent-space normal, RGBA32.</param>
        /// <exception cref="ArgumentException">A size below 8, or an array of the wrong length.</exception>
        public static void Texture(SnowType kind, int size, byte[] albedoRgba, byte[] normalRgba)
        {
            if (size < 8)
            {
                throw new ArgumentException("Size must be at least 8.", nameof(size));
            }

            int bytes = size * size * 4;
            if (albedoRgba == null || albedoRgba.Length != bytes || normalRgba == null || normalRgba.Length != bytes)
            {
                throw new ArgumentException("Each array must hold size * size * 4 bytes.");
            }

            var height = new float[size * size];
            for (int j = 0; j < size; j++)
            {
                for (int i = 0; i < size; i++)
                {
                    height[j * size + i] = Height(kind, (double)i / size, (double)j / size);
                }
            }

            Colour(kind, out float baseR, out float baseG, out float baseB, out float shade, out float bump);
            for (int j = 0; j < size; j++)
            {
                for (int i = 0; i < size; i++)
                {
                    float h = height[j * size + i];
                    float dhdu = (height[j * size + (i + 1) % size] - height[j * size + (i + size - 1) % size]) * 0.5f;
                    float dhdv = (height[((j + 1) % size) * size + i] - height[((j + size - 1) % size) * size + i]) * 0.5f;
                    float lit = 1f - shade * (1f - h);
                    int o = (j * size + i) * 4;
                    albedoRgba[o] = ToByte(baseR * lit);
                    albedoRgba[o + 1] = ToByte(baseG * lit);
                    albedoRgba[o + 2] = ToByte(baseB * lit);
                    albedoRgba[o + 3] = 255;

                    // Slope per tile (height change across one tile) times the bump strength, so the look doesn't
                    // depend on the texture size.
                    float nx = -dhdu * size * bump * 0.01f;
                    float ny = -dhdv * size * bump * 0.01f;
                    float inverse = 1f / (float)Math.Sqrt(nx * nx + ny * ny + 1f);
                    normalRgba[o] = ToByte(0.5f + 0.5f * nx * inverse);
                    normalRgba[o + 1] = ToByte(0.5f + 0.5f * ny * inverse);
                    normalRgba[o + 2] = ToByte(0.5f + 0.5f * inverse);
                    normalRgba[o + 3] = 255;
                }
            }
        }

        /// <summary>
        /// Paints terrain layer weights from the snow map: each alphamap texel takes the snow type of the nearest map
        /// sample, then a box blur of <paramref name="blurRadius"/> texels softens the edges. Weights sum to 1.
        /// </summary>
        /// <param name="snowTypes">The snow map: one <see cref="SnowType"/> byte per heightmap sample, row-major, rows on Z.</param>
        /// <param name="resolution">Samples a side of the snow map.</param>
        /// <param name="weights">
        /// Output, <c>[alphamapResolution, alphamapResolution, LayerCount]</c>, indexed [z, x, layer] like
        /// <c>TerrainData.SetAlphamaps</c>.
        /// </param>
        /// <param name="blurRadius">Blur radius, texels; 0 for hard edges.</param>
        /// <exception cref="ArgumentException">Sizes that don't match.</exception>
        public static void LayerWeights(byte[] snowTypes, int resolution, float[,,] weights, int blurRadius)
        {
            if (snowTypes == null || resolution < 2 || snowTypes.Length != resolution * resolution)
            {
                throw new ArgumentException("The snow map must hold resolution * resolution bytes.", nameof(snowTypes));
            }

            int size = weights.GetLength(0);
            if (weights.GetLength(1) != size || weights.GetLength(2) != LayerCount || size < 1)
            {
                throw new ArgumentException("Weights must be [n, n, LayerCount].", nameof(weights));
            }

            var hard = new float[size, size, LayerCount];
            for (int z = 0; z < size; z++)
            {
                int row = size == 1 ? 0 : (int)Math.Round((double)z * (resolution - 1) / (size - 1));
                for (int x = 0; x < size; x++)
                {
                    int column = size == 1 ? 0 : (int)Math.Round((double)x * (resolution - 1) / (size - 1));
                    int type = snowTypes[row * resolution + column];
                    hard[z, x, type < LayerCount ? type : 0] = 1f;
                }
            }

            for (int z = 0; z < size; z++)
            {
                for (int x = 0; x < size; x++)
                {
                    float total = 0f;
                    for (int layer = 0; layer < LayerCount; layer++)
                    {
                        float sum = 0f;
                        int count = 0;
                        for (int dz = -blurRadius; dz <= blurRadius; dz++)
                        {
                            int zz = z + dz;
                            if (zz < 0 || zz >= size)
                            {
                                continue;
                            }

                            for (int dx = -blurRadius; dx <= blurRadius; dx++)
                            {
                                int xx = x + dx;
                                if (xx < 0 || xx >= size)
                                {
                                    continue;
                                }

                                sum += hard[zz, xx, layer];
                                count++;
                            }
                        }

                        float weight = sum / count;
                        weights[z, x, layer] = weight;
                        total += weight;
                    }

                    for (int layer = 0; layer < LayerCount; layer++)
                    {
                        weights[z, x, layer] /= total;
                    }
                }
            }
        }

        // Height in 0..1 at texture coordinates (u, v) in [0, 1), tileable.
        private static float Height(SnowType kind, double u, double v)
        {
            switch (kind)
            {
                case SnowType.Groomed:
                    // Corduroy: 18 straight ridges a tile (33 cm on a 6 m tile) down the fall line (+V), over soft
                    // large-scale variation so the groomer doesn't read as a printed pattern from the follow camera.
                    double ridge = 0.5 + 0.5 * Math.Sin(2.0 * Math.PI * 18.0 * u);
                    return (float)(0.45 * ridge + 0.55 * Fractal(u, v, 3, 3, 11));
                case SnowType.Powder:
                    return (float)Fractal(u, v, 4, 4, 23);
                case SnowType.Crud:
                    double clumps = Fractal(u, v, 12, 3, 37);
                    return (float)(clumps * clumps);
                case SnowType.Ice:
                    double streak = Fractal(u * 8.0 % 1.0, v, 2, 2, 41);
                    return (float)(0.6 * Fractal(u, v, 3, 3, 43) + 0.4 * streak);
                default:
                    return (float)Fractal(u, v, 16, 3, 53);
            }
        }

        private static void Colour(SnowType kind, out float r, out float g, out float b, out float shade, out float bump)
        {
            switch (kind)
            {
                case SnowType.Groomed: r = 0.95f; g = 0.96f; b = 0.98f; shade = 0.05f; bump = 0.35f; return;
                case SnowType.Powder: r = 0.98f; g = 0.985f; b = 1.0f; shade = 0.05f; bump = 3.0f; return;
                case SnowType.Crud: r = 0.92f; g = 0.93f; b = 0.95f; shade = 0.12f; bump = 0.8f; return;
                case SnowType.Ice: r = 0.78f; g = 0.85f; b = 0.92f; shade = 0.10f; bump = 0.6f; return;
                default: r = 0.88f; g = 0.88f; b = 0.86f; shade = 0.10f; bump = 0.6f; return;
            }
        }

        // Tileable fractal value noise in 0..1: octaves of lattice noise at frequency, 2x frequency, ...
        private static double Fractal(double u, double v, int frequency, int octaves, int seed)
        {
            double sum = 0.0;
            double amplitude = 0.5;
            double norm = 0.0;
            for (int octave = 0; octave < octaves; octave++)
            {
                sum += amplitude * ValueNoise(u, v, frequency << octave, seed + octave * 101);
                norm += amplitude;
                amplitude *= 0.5;
            }

            return sum / norm;
        }

        private static double ValueNoise(double u, double v, int frequency, int seed)
        {
            double x = u * frequency;
            double y = v * frequency;
            int x0 = (int)Math.Floor(x);
            int y0 = (int)Math.Floor(y);
            double fx = x - x0;
            double fy = y - y0;
            double sx = fx * fx * (3.0 - 2.0 * fx);
            double sy = fy * fy * (3.0 - 2.0 * fy);
            double a = Lattice(x0, y0, frequency, seed);
            double b = Lattice(x0 + 1, y0, frequency, seed);
            double c = Lattice(x0, y0 + 1, frequency, seed);
            double d = Lattice(x0 + 1, y0 + 1, frequency, seed);
            return (a + (b - a) * sx) + ((c + (d - c) * sx) - (a + (b - a) * sx)) * sy;
        }

        private static double Lattice(int x, int y, int period, int seed)
        {
            x = ((x % period) + period) % period;
            y = ((y % period) + period) % period;
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)seed * 2246822519u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (double)0xFFFFFF;
            }
        }

        private static byte ToByte(float value)
        {
            float clamped = value < 0f ? 0f : (value > 1f ? 1f : value);
            return (byte)(clamped * 255f + 0.5f);
        }
    }
}
