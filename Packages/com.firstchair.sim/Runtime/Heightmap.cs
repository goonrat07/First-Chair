using System;

namespace FirstChair.Sim
{
    /// <summary>
    /// The terrain the sim rides on, read from a raw 16-bit heightmap: the same file Unity Terrain renders.
    /// </summary>
    /// <remarks>
    /// <para><b>File format (R16).</b> Headerless, unsigned 16-bit little-endian samples, row-major, square:
    /// N x N samples where N is 2^n + 1 (3, 5, 9, ... 129, 257, 513, 1025, 2049), which is the resolution
    /// rule Unity Terrain uses. Row index runs along +Z (row 0 at z = 0); column index runs along +X
    /// (column 0 at x = 0). Sample 0 is <see cref="HeightmapSpec.MinHeightMm"/> and sample 65535 is
    /// <see cref="HeightmapSpec.MaxHeightMm"/>, linearly. World size and height range come from the
    /// <see cref="HeightmapSpec"/>, not the file.</para>
    /// <para><b>Sampling.</b> Height is bilinear inside a cell. The normal comes from the analytic gradient
    /// of that bilinear patch, normalized with <see cref="Fix64.Sqrt"/>.</para>
    /// <para><b>At and beyond the edge.</b> Points outside [0, size] are clamped to the edge for the
    /// lookup, and the slope across a crossed edge is zero: outside the map the surface is the edge
    /// profile extruded outward, so height and normal stay consistent. <see cref="Contains"/> reports
    /// whether a point is on the map.</para>
    /// <para>The sim never reads files: the caller loads the bytes. Immutable after loading, so one
    /// instance may be shared by any number of simulations and threads.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// byte[] bytes = File.ReadAllBytes("TestSlope.r16");     // done by the game layer, not the sim
    /// var spec = new HeightmapSpec(512000, 512000, 0, 300000);
    /// Heightmap heightmap = Heightmap.FromR16(bytes, spec);
    /// heightmap.Sample(x, z, out Fix64 height, out Vec3 normal);
    /// </code>
    /// </example>
    public sealed class Heightmap
    {
        private const int MaxSample = 65535;
        private const int MillimetresPerMetre = 1000;

        private static readonly Fix64 Half = Fix64.FromRatio(1, 2);

        private readonly ushort[] _samples;
        private readonly int _lastCell;
        private readonly Fix64 _cellsPerMetreX;
        private readonly Fix64 _cellsPerMetreZ;
        private readonly Fix64 _minHeight;
        private readonly Fix64 _heightPerSample;

        private Heightmap(ushort[] samples, int resolution, HeightmapSpec spec, ulong hash)
        {
            _samples = samples;
            Resolution = resolution;
            Spec = spec;
            Hash = hash;

            int cells = resolution - 1;
            _lastCell = cells - 1;
            SizeX = Fix64.FromRatio(spec.SizeXMm, MillimetresPerMetre);
            SizeZ = Fix64.FromRatio(spec.SizeZMm, MillimetresPerMetre);
            _cellsPerMetreX = Fix64.FromRatio((long)cells * MillimetresPerMetre, spec.SizeXMm);
            _cellsPerMetreZ = Fix64.FromRatio((long)cells * MillimetresPerMetre, spec.SizeZMm);
            _minHeight = Fix64.FromRatio(spec.MinHeightMm, MillimetresPerMetre);
            _heightPerSample = Fix64.FromRatio((long)spec.MaxHeightMm - spec.MinHeightMm, (long)MaxSample * MillimetresPerMetre);
        }

        /// <summary>Samples per side (2^n + 1).</summary>
        public int Resolution { get; }

        /// <summary>The world size and height range this map was loaded with.</summary>
        public HeightmapSpec Spec { get; }

        /// <summary>World size along X, metres.</summary>
        public Fix64 SizeX { get; }

        /// <summary>World size along Z, metres.</summary>
        public Fix64 SizeZ { get; }

        /// <summary>64-bit FNV-1a hash of the spec and the raw bytes. Recordings store it.</summary>
        public ulong Hash { get; }

        /// <summary>
        /// Loads a heightmap from raw R16 bytes. Throws <see cref="ArgumentException"/> when the byte count is
        /// not 2 * N * N with N = 2^n + 1, or when the spec has a non-positive size or a height range below zero.
        /// </summary>
        /// <param name="data">The raw file contents.</param>
        /// <param name="spec">World size and height range.</param>
        public static Heightmap FromR16(byte[] data, HeightmapSpec spec)
        {
            int resolution = 0;
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
                    "An R16 heightmap must hold N x N 16-bit samples with N = 2^n + 1; got " + data.Length + " bytes.",
                    nameof(data));
            }

            if (spec.SizeXMm <= 0 || spec.SizeZMm <= 0 || spec.MaxHeightMm < spec.MinHeightMm)
            {
                throw new ArgumentException("Heightmap sizes must be positive and MaxHeightMm must not be below MinHeightMm.", nameof(spec));
            }

            var samples = new ushort[resolution * resolution];
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = (ushort)(data[2 * i] | (data[2 * i + 1] << 8));
            }

            ulong hash = Fnv1a64.OffsetBasis;
            hash = Fnv1a64.AddInt32(hash, spec.SizeXMm);
            hash = Fnv1a64.AddInt32(hash, spec.SizeZMm);
            hash = Fnv1a64.AddInt32(hash, spec.MinHeightMm);
            hash = Fnv1a64.AddInt32(hash, spec.MaxHeightMm);
            hash = Fnv1a64.AddBytes(hash, data);

            return new Heightmap(samples, resolution, spec, hash);
        }

        /// <summary>True when (x, z) lies on the map, edges included.</summary>
        /// <param name="x">X, metres.</param>
        /// <param name="z">Z, metres.</param>
        public bool Contains(Fix64 x, Fix64 z) => x >= Fix64.Zero && x <= SizeX && z >= Fix64.Zero && z <= SizeZ;

        /// <summary>
        /// Samples the surface height and unit normal at (x, z). Points off the map use the extruded edge
        /// (see the class remarks).
        /// </summary>
        /// <param name="x">X, metres.</param>
        /// <param name="z">Z, metres.</param>
        /// <param name="height">Surface height, metres.</param>
        /// <param name="normal">Unit surface normal, pointing up.</param>
        public void Sample(Fix64 x, Fix64 z, out Fix64 height, out Vec3 normal)
        {
            bool outsideX = x < Fix64.Zero || x > SizeX;
            bool outsideZ = z < Fix64.Zero || z > SizeZ;

            Fix64 gridX = Fix64.Clamp(x, Fix64.Zero, SizeX) * _cellsPerMetreX;
            Fix64 gridZ = Fix64.Clamp(z, Fix64.Zero, SizeZ) * _cellsPerMetreZ;
            int column = gridX.FloorToInt();
            int row = gridZ.FloorToInt();
            if (column > _lastCell)
            {
                column = _lastCell;
            }

            if (row > _lastCell)
            {
                row = _lastCell;
            }

            Fix64 u = Fix64.Min(gridX - Fix64.FromInt(column), Fix64.One);
            Fix64 v = Fix64.Min(gridZ - Fix64.FromInt(row), Fix64.One);
            Fix64 oneMinusU = Fix64.One - u;
            Fix64 oneMinusV = Fix64.One - v;

            Fix64 h00 = HeightAt(row, column);
            Fix64 h10 = HeightAt(row, column + 1);
            Fix64 h01 = HeightAt(row + 1, column);
            Fix64 h11 = HeightAt(row + 1, column + 1);

            height = oneMinusV * (oneMinusU * h00 + u * h10) + v * (oneMinusU * h01 + u * h11);

            Fix64 slopeX = outsideX ? Fix64.Zero : (oneMinusV * (h10 - h00) + v * (h11 - h01)) * _cellsPerMetreX;
            Fix64 slopeZ = outsideZ ? Fix64.Zero : (oneMinusU * (h01 - h00) + u * (h11 - h10)) * _cellsPerMetreZ;
            Fix64 inverseLength = Fix64.One / Fix64.Sqrt(Fix64.One + slopeX * slopeX + slopeZ * slopeZ);
            normal = new Vec3(-slopeX * inverseLength, inverseLength, -slopeZ * inverseLength);
        }

        /// <summary>
        /// Index (row * <see cref="Resolution"/> + column) of the sample nearest (x, z); halves round up,
        /// toward +X and +Z. Points off the map use the nearest edge sample. <see cref="SnowMap"/> looks its
        /// layer up through this, so the two grids line up exactly.
        /// </summary>
        internal int NearestSampleIndex(Fix64 x, Fix64 z)
        {
            int column = (Fix64.Clamp(x, Fix64.Zero, SizeX) * _cellsPerMetreX + Half).FloorToInt();
            int row = (Fix64.Clamp(z, Fix64.Zero, SizeZ) * _cellsPerMetreZ + Half).FloorToInt();
            return row * Resolution + column;
        }

        private Fix64 HeightAt(int row, int column) =>
            _minHeight + Fix64.FromInt(_samples[row * Resolution + column]) * _heightPerSample;
    }
}
