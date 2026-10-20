namespace FirstChair.Sim
{
    /// <summary>
    /// 64-bit FNV-1a hash, used to tell whether a recording was made with the same tuning and heightmap.
    /// Not cryptographic. Every multi-byte value is fed little-endian, so the hash is the same on every
    /// machine.
    /// </summary>
    internal static class Fnv1a64
    {
        public const ulong OffsetBasis = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        public static ulong AddByte(ulong hash, byte value) => unchecked((hash ^ value) * Prime);

        public static ulong AddInt32(ulong hash, int value)
        {
            for (int shift = 0; shift < 32; shift += 8)
            {
                hash = AddByte(hash, unchecked((byte)(value >> shift)));
            }

            return hash;
        }

        public static ulong AddInt64(ulong hash, long value)
        {
            for (int shift = 0; shift < 64; shift += 8)
            {
                hash = AddByte(hash, unchecked((byte)(value >> shift)));
            }

            return hash;
        }

        public static ulong AddBytes(ulong hash, byte[] data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                hash = AddByte(hash, data[i]);
            }

            return hash;
        }
    }
}
