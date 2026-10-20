namespace FirstChair.Sim
{
    /// <summary>
    /// The riding numbers. No riding number is written inline in sim logic; it lives here.
    /// </summary>
    /// <remarks>
    /// <para>A plain data struct: <see cref="Simulation"/> and <see cref="Recording"/> copy it when they are
    /// created, so changing a local copy later cannot change a run in progress.</para>
    /// <para><see cref="ComputeHash"/> identifies the values. A recording stores it, and the
    /// <see cref="Replayer"/> refuses a recording made with different tuning. Every field added here must
    /// be added to <see cref="ComputeHash"/>; a test fails if one is missed.</para>
    /// <para>The values are placeholders until Story 002 tunes riding by playtest.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// Tuning tuning = Tuning.Default;
    /// tuning.SlideDrag = Fix64.FromRatio(1, 5); // a test variant
    /// var simulation = new Simulation(setup, heightmap, tuning);
    /// </code>
    /// </example>
    public struct Tuning
    {
        /// <summary>Gravitational acceleration, metres per second squared.</summary>
        public Fix64 Gravity;

        /// <summary>Linear drag on the slide, per second: each second the rider loses this fraction of speed.</summary>
        public Fix64 SlideDrag;

        /// <summary>The default tuning.</summary>
        public static Tuning Default => new Tuning
        {
            Gravity = Fix64.FromRatio(981, 100),
            SlideDrag = Fix64.FromRatio(1, 10),
        };

        /// <summary>A 64-bit FNV-1a hash of every field's raw bits, in declaration order.</summary>
        public ulong ComputeHash()
        {
            ulong hash = Fnv1a64.OffsetBasis;
            hash = Fnv1a64.AddInt64(hash, Gravity.Raw);
            hash = Fnv1a64.AddInt64(hash, SlideDrag.Raw);
            return hash;
        }
    }
}
