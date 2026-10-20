using System;
using FirstChair.Sim;

namespace FirstChair.Sim.Tests
{
    /// <summary>
    /// Builds small R16 heightmaps and their snow maps in memory, so no test touches the filesystem for terrain.
    /// The standard map is 65 x 65 samples over 128 m (2 m cells) with heights from 0 m to 60 m.
    /// </summary>
    internal static class TestTerrain
    {
        public const int Resolution = 65;
        public const int SizeMm = 128_000;
        public const int MinHeightMm = 0;
        public const int MaxHeightMm = 60_000;

        /// <summary>Drop per 2 m cell on the incline: a gradient of 0.4 (about 21.8 degrees).</summary>
        public const int DropMmPerCell = 800;
        public const double Gradient = 0.4;

        public static readonly HeightmapSpec Spec = new HeightmapSpec(SizeMm, SizeMm, MinHeightMm, MaxHeightMm);

        /// <summary>A plane that falls toward +Z: 60 m at z = 0, 8.8 m at z = 128.</summary>
        public static Heightmap InclineDownhillPlusZ() => Build((row, column) => MaxHeightMm - DropMmPerCell * row);

        /// <summary>The same plane mirrored: it falls toward -Z.</summary>
        public static Heightmap InclineDownhillMinusZ() => Build((row, column) => MaxHeightMm - DropMmPerCell * (Resolution - 1 - row));

        /// <summary>A level plane at 30 m.</summary>
        public static Heightmap Flat() => Build((row, column) => 30_000);

        public static Heightmap Build(Func<int, int, int> heightMmAt) =>
            Heightmap.FromR16(R16Bytes(Resolution, MinHeightMm, MaxHeightMm, heightMmAt), Spec);

        /// <summary>
        /// Encodes heights (whole millimetres, by row and column) as little-endian R16 samples, rounded
        /// to the nearest sample. Integer arithmetic only.
        /// </summary>
        public static byte[] R16Bytes(int resolution, int minHeightMm, int maxHeightMm, Func<int, int, int> heightMmAt)
        {
            var bytes = new byte[resolution * resolution * 2];
            long range = maxHeightMm - minHeightMm;
            for (int row = 0; row < resolution; row++)
            {
                for (int column = 0; column < resolution; column++)
                {
                    long height = heightMmAt(row, column) - minHeightMm;
                    var sample = (ushort)((height * 65535 + range / 2) / range);
                    int index = 2 * (row * resolution + column);
                    bytes[index] = (byte)sample;
                    bytes[index + 1] = (byte)(sample >> 8);
                }
            }

            return bytes;
        }

        /// <summary>A snow map for <paramref name="heightmap"/> with one type everywhere.</summary>
        public static SnowMap Uniform(Heightmap heightmap, SnowType type) => Snow(heightmap, (row, column) => type);

        /// <summary>A snow map whose type changes every 6 rows (12 m) through all five types, in enum order.</summary>
        public static SnowMap Bands(Heightmap heightmap) => Snow(heightmap, (row, column) => (SnowType)(row / 6 % 5));

        /// <summary>A snow map for <paramref name="heightmap"/> with the type at each row and column.</summary>
        public static SnowMap Snow(Heightmap heightmap, Func<int, int, SnowType> typeAt) =>
            SnowMap.FromBytes(SnowBytes(heightmap.Resolution, typeAt), heightmap);

        /// <summary>Encodes snow types (by row and column) as one byte per sample, row-major.</summary>
        public static byte[] SnowBytes(int resolution, Func<int, int, SnowType> typeAt)
        {
            var bytes = new byte[resolution * resolution];
            for (int row = 0; row < resolution; row++)
            {
                for (int column = 0; column < resolution; column++)
                {
                    bytes[row * resolution + column] = (byte)typeAt(row, column);
                }
            }

            return bytes;
        }

        /// <summary>Converts to double for comparisons against closed-form expectations. Test code only.</summary>
        public static double ToDouble(Fix64 value) => value.Raw / 4294967296.0;
    }
}
