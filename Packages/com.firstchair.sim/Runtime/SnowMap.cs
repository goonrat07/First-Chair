using System;

namespace FirstChair.Sim
{
    /// <summary>The kind of snow under the rider. Each has its own grip and drag in <see cref="Tuning"/>.</summary>
    public enum SnowType : byte
    {
        /// <summary>Machine-groomed corduroy: the reference snow.</summary>
        Groomed = 0,

        /// <summary>Deep, soft snow: holds an edge but slows the rider.</summary>
        Powder = 1,

        /// <summary>Tracked-out, choppy snow.</summary>
        Crud = 2,

        /// <summary>Hard, polished snow: fast, and the edges let go early.</summary>
        Ice = 3,

        /// <summary>Wet spring snow: grabby and slow.</summary>
        Slush = 4,
    }

    /// <summary>
    /// Which <see cref="SnowType"/> covers each heightmap sample: an 8-bit layer at the heightmap's own
    /// resolution and alignment.
    /// </summary>
    /// <remarks>
    /// <para><b>File format.</b> Headerless, one byte per sample, row-major, N x N where N is the heightmap's
    /// <see cref="Heightmap.Resolution"/>. Rows and columns are the heightmap's (row index along +Z, column
    /// index along +X), so byte i covers the same point as the heightmap's sample i. Each byte is a
    /// <see cref="SnowType"/> value, 0 to 4.</para>
    /// <para><b>Lookup.</b> The type at a point is the type of the nearest sample; halves round toward +X and
    /// +Z. Off the map the nearest edge sample is used, matching the heightmap's extruded edge.</para>
    /// <para><b>Identity.</b> <see cref="Hash"/> covers the bytes and the hash of the heightmap the layer was
    /// loaded for, since that heightmap's grid places it. Recordings store it, and the <see cref="Replayer"/>
    /// refuses a recording made on another snow map.</para>
    /// <para>The sim never reads files: the caller loads the bytes. The layer copies them, so it is immutable
    /// after loading and may be shared by any number of simulations and threads. Pass a
    /// <see cref="Simulation"/> the heightmap this layer was loaded for.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// Heightmap heightmap = Heightmap.FromR16(r16Bytes, spec);
    /// SnowMap snowMap = SnowMap.FromBytes(snowBytes, heightmap);   // bytes loaded by the game layer
    /// var simulation = new Simulation(setup, heightmap, snowMap, Tuning.Default);
    /// SnowType under = snowMap.TypeAt(x, z);
    /// </code>
    /// </example>
    public sealed class SnowMap
    {
        private readonly byte[] _types;
        private readonly Heightmap _heightmap;

        private SnowMap(byte[] types, Heightmap heightmap, ulong hash)
        {
            _types = types;
            _heightmap = heightmap;
            Hash = hash;
        }

        /// <summary>64-bit FNV-1a hash of the heightmap's hash and the raw bytes. Recordings store it.</summary>
        public ulong Hash { get; }

        /// <summary>
        /// Loads a snow-type layer for <paramref name="heightmap"/>. Throws <see cref="ArgumentException"/> when
        /// the byte count is not the heightmap's sample count, or when a byte is not a <see cref="SnowType"/>.
        /// </summary>
        /// <param name="data">The raw file contents, one byte per heightmap sample.</param>
        /// <param name="heightmap">The heightmap whose grid the layer follows.</param>
        public static SnowMap FromBytes(byte[] data, Heightmap heightmap)
        {
            int resolution = heightmap.Resolution;
            if (data.Length != resolution * resolution)
            {
                throw new ArgumentException(
                    "A snow map for a " + resolution + " x " + resolution + " heightmap must hold " + resolution * resolution
                    + " bytes; got " + data.Length + ".",
                    nameof(data));
            }

            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] > (byte)SnowType.Slush)
                {
                    throw new ArgumentException(
                        "Snow map byte " + i + " is " + data[i] + "; snow types run from 0 to " + (byte)SnowType.Slush + ".",
                        nameof(data));
                }
            }

            ulong hash = Fnv1a64.OffsetBasis;
            hash = Fnv1a64.AddInt64(hash, unchecked((long)heightmap.Hash));
            hash = Fnv1a64.AddBytes(hash, data);

            return new SnowMap((byte[])data.Clone(), heightmap, hash);
        }

        /// <summary>The snow type at (x, z): the type of the nearest sample (see the class remarks).</summary>
        /// <param name="x">X, metres.</param>
        /// <param name="z">Z, metres.</param>
        public SnowType TypeAt(Fix64 x, Fix64 z) => (SnowType)_types[_heightmap.NearestSampleIndex(x, z)];
    }
}
