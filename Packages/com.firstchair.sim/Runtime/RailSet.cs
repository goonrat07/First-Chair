using System;
using System.Collections.Generic;

namespace FirstChair.Sim
{
    /// <summary>
    /// The rails and boxes on a slope (Story 006). Sim data like the <see cref="Forest"/>: a <see cref="Recording"/>
    /// stores its <see cref="Hash"/>, and the <see cref="Replayer"/> refuses a recording made on other rails.
    /// </summary>
    /// <remarks>
    /// <para>A rail's index is its identity: grind events name rails by it (<see cref="SimEvent.Rail"/>), and the hash covers
    /// the order as well as every rail's ends, height, width and kind.</para>
    /// <para>Immutable after construction, so one instance may be shared by any number of simulations and threads.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// RailSet rails = RailSet.FromLayout(layout);                 // the slope's rails and boxes
    /// var simulation = new Simulation(setup, heightmap, snowMap, forest, rails, Tuning.Default);
    /// var recording = new Recording(setup, heightmap, snowMap, forest, rails, Tuning.Default);
    /// </code>
    /// </example>
    public sealed class RailSet
    {
        /// <summary>The shortest rail accepted, millimetres, seen from above.</summary>
        public const int MinLengthMm = 1_000;

        /// <summary>The largest span along either axis, height or width accepted, millimetres (1 km), so products fit Q32.32.</summary>
        public const int MaxSpanMm = 1_000_000;

        private readonly SlopeRail[] _rails;

        /// <summary>Copies a list of rails and boxes. Their order becomes their indices.</summary>
        /// <param name="rails">The rails, for example <see cref="SlopeLayout.Rails"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="rails"/> is null.</exception>
        /// <exception cref="ArgumentException">A rail is shorter than <see cref="MinLengthMm"/>, has a height or width that is
        /// not positive, or a span, height or width above <see cref="MaxSpanMm"/>; the message names its index.</exception>
        public RailSet(IReadOnlyList<SlopeRail> rails)
        {
            if (rails == null)
            {
                throw new ArgumentNullException(nameof(rails));
            }

            _rails = new SlopeRail[rails.Count];
            ulong hash = Fnv1a64.AddInt32(Fnv1a64.OffsetBasis, rails.Count);
            for (int i = 0; i < _rails.Length; i++)
            {
                SlopeRail rail = rails[i];
                long dx = (long)rail.X1Mm - rail.X0Mm;
                long dz = (long)rail.Z1Mm - rail.Z0Mm;
                bool spanOk = dx <= MaxSpanMm && dx >= -MaxSpanMm && dz <= MaxSpanMm && dz >= -MaxSpanMm;
                if (!spanOk || dx * dx + dz * dz < (long)MinLengthMm * MinLengthMm
                    || rail.HeightMm <= 0 || rail.HeightMm > MaxSpanMm || rail.WidthMm <= 0 || rail.WidthMm > MaxSpanMm)
                {
                    throw new ArgumentException(
                        "Rail " + i + ": it must be " + MinLengthMm + " to " + MaxSpanMm + " mm long along each axis, with a height and width from 1 to "
                        + MaxSpanMm + " mm.",
                        nameof(rails));
                }

                _rails[i] = rail;
                hash = Fnv1a64.AddInt32(hash, rail.X0Mm);
                hash = Fnv1a64.AddInt32(hash, rail.Z0Mm);
                hash = Fnv1a64.AddInt32(hash, rail.X1Mm);
                hash = Fnv1a64.AddInt32(hash, rail.Z1Mm);
                hash = Fnv1a64.AddInt32(hash, rail.HeightMm);
                hash = Fnv1a64.AddInt32(hash, rail.WidthMm);
                hash = Fnv1a64.AddInt32(hash, rail.Box ? 1 : 0);
            }

            Hash = hash;
        }

        /// <summary>No rails or boxes.</summary>
        public static RailSet Empty { get; } = new RailSet(Array.Empty<SlopeRail>());

        /// <summary>The number of rails and boxes.</summary>
        public int Count => _rails.Length;

        /// <summary>A 64-bit FNV-1a hash of the count and every rail's fields, in index order.</summary>
        public ulong Hash { get; }

        /// <summary>The rail at an index.</summary>
        /// <param name="index">0 to <see cref="Count"/> - 1.</param>
        public SlopeRail this[int index] => _rails[index];

        /// <summary>The rails and boxes of a slope layout, in file order.</summary>
        /// <param name="layout">The parsed layout.</param>
        /// <exception cref="ArgumentNullException"><paramref name="layout"/> is null.</exception>
        public static RailSet FromLayout(SlopeLayout layout) =>
            new RailSet((layout ?? throw new ArgumentNullException(nameof(layout))).Rails);
    }
}
