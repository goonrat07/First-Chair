using System;

namespace FirstChair.Riding
{
    /// <summary>
    /// Decodes the sim's R16 heightmap into the normalised array Unity's <c>TerrainData.SetHeights</c> takes, without
    /// touching Unity, so the decode is testable on its own.
    /// </summary>
    public static class TerrainHeights
    {
        private const float MaxSample = 65535f;

        /// <summary>
        /// Decodes headerless little-endian unsigned 16-bit samples, N x N with N = 2^n + 1, row-major, into
        /// <c>heights[row, column]</c> = sample / 65535. Row is Z and column is X, the order Unity's
        /// <c>SetHeights(0, 0, heights)</c> expects (first index Z); see <see cref="SimCoordinates"/>. With
        /// <c>TerrainData.size.y</c> = MaxHeight - MinHeight and the terrain at y = MinHeight, a vertex's Unity height is
        /// MinHeight + sample * (MaxHeight - MinHeight) / 65535, the same formula as the sim's <c>Heightmap</c>.
        /// </summary>
        /// <param name="data">The file's bytes.</param>
        /// <param name="resolution">N, samples per side.</param>
        /// <exception cref="ArgumentException">The length is not 2 * N * N for N = 2^n + 1.</exception>
        public static float[,] DecodeR16(byte[] data, out int resolution)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            resolution = 0;
            for (int power = 1; power < 16; power++)
            {
                long side = (1L << power) + 1;
                if (side * side * 2 == data.Length)
                {
                    resolution = (int)side;
                    break;
                }
            }

            if (resolution == 0)
            {
                throw new ArgumentException(
                    "An R16 heightmap must hold N x N 16-bit samples with N = 2^n + 1; got " + data.Length + " bytes.", nameof(data));
            }

            var heights = new float[resolution, resolution];
            for (int row = 0; row < resolution; row++)
            {
                for (int column = 0; column < resolution; column++)
                {
                    int i = 2 * (row * resolution + column);
                    heights[row, column] = (data[i] | (data[i + 1] << 8)) / MaxSample;
                }
            }

            return heights;
        }
    }
}
