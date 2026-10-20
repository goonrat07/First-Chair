namespace FirstChair.Sim
{
    /// <summary>
    /// The metadata a raw R16 heightmap file does not carry: its world size and height range, in whole
    /// millimetres so any text format stores them exactly.
    /// </summary>
    /// <remarks>Immutable and thread-safe.</remarks>
    /// <example>
    /// <code>
    /// // A 1 km square slope whose samples span 0 m to 400 m.
    /// var spec = new HeightmapSpec(sizeXMm: 1000000, sizeZMm: 1000000, minHeightMm: 0, maxHeightMm: 400000);
    /// Heightmap heightmap = Heightmap.FromR16(r16Bytes, spec);
    /// </code>
    /// </example>
    public readonly struct HeightmapSpec
    {
        /// <summary>World size along X (columns), millimetres. Must be positive.</summary>
        public readonly int SizeXMm;

        /// <summary>World size along Z (rows), millimetres. Must be positive.</summary>
        public readonly int SizeZMm;

        /// <summary>Height of sample value 0, millimetres.</summary>
        public readonly int MinHeightMm;

        /// <summary>Height of sample value 65535, millimetres. Must not be below <see cref="MinHeightMm"/>.</summary>
        public readonly int MaxHeightMm;

        /// <summary>Creates a spec.</summary>
        /// <param name="sizeXMm">World size along X, millimetres.</param>
        /// <param name="sizeZMm">World size along Z, millimetres.</param>
        /// <param name="minHeightMm">Height of sample 0, millimetres.</param>
        /// <param name="maxHeightMm">Height of sample 65535, millimetres.</param>
        public HeightmapSpec(int sizeXMm, int sizeZMm, int minHeightMm, int maxHeightMm)
        {
            SizeXMm = sizeXMm;
            SizeZMm = sizeZMm;
            MinHeightMm = minHeightMm;
            MaxHeightMm = maxHeightMm;
        }
    }
}
