using System;
using System.Collections.Generic;

namespace FirstChair.Sim
{
    /// <summary>
    /// The trees a ride runs among (Story 005): trunks the rider crashes into, branches that clip, and gaps between
    /// trunks that fill Flow. Sim data like the <see cref="Heightmap"/> and the <see cref="SnowMap"/>: a
    /// <see cref="Recording"/> stores its <see cref="Hash"/>, and the <see cref="Replayer"/> refuses a recording made
    /// among other trees.
    /// </summary>
    /// <remarks>
    /// <para>A tree's index is its identity: events name trees by it (<see cref="SimEvent.Tree"/>), and the hash
    /// covers the order as well as every tree's position and radii.</para>
    /// <para>Immutable after construction, so one instance may be shared by any number of simulations and threads.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// Forest forest = Forest.FromLayout(layout);                 // the slope's trees
    /// var simulation = new Simulation(setup, heightmap, snowMap, forest, Tuning.Default);
    /// var recording = new Recording(setup, heightmap, snowMap, forest, Tuning.Default);
    /// </code>
    /// </example>
    public sealed class Forest
    {
        /// <summary>The largest trunk or branch radius accepted, millimetres (1 km), so squared distances fit Q32.32.</summary>
        public const int MaxRadiusMm = 1_000_000;

        private readonly SlopeTree[] _trees;

        /// <summary>Copies a list of trees. Their order becomes their indices.</summary>
        /// <param name="trees">The trees, for example <see cref="SlopeLayout.Trees"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="trees"/> is null.</exception>
        /// <exception cref="ArgumentException">A tree has a trunk radius that is not positive, a branch radius below its
        /// trunk radius, or a radius above <see cref="MaxRadiusMm"/>; the message names its index.</exception>
        public Forest(IReadOnlyList<SlopeTree> trees)
        {
            if (trees == null)
            {
                throw new ArgumentNullException(nameof(trees));
            }

            _trees = new SlopeTree[trees.Count];
            ulong hash = Fnv1a64.AddInt32(Fnv1a64.OffsetBasis, trees.Count);
            for (int i = 0; i < _trees.Length; i++)
            {
                SlopeTree tree = trees[i];
                if (tree.TrunkRadiusMm <= 0 || tree.BranchRadiusMm < tree.TrunkRadiusMm || tree.BranchRadiusMm > MaxRadiusMm)
                {
                    throw new ArgumentException(
                        "Tree " + i + ": the trunk radius must be positive and the branch radius from it up to " + MaxRadiusMm + " mm.",
                        nameof(trees));
                }

                _trees[i] = tree;
                hash = Fnv1a64.AddInt32(hash, tree.XMm);
                hash = Fnv1a64.AddInt32(hash, tree.ZMm);
                hash = Fnv1a64.AddInt32(hash, tree.TrunkRadiusMm);
                hash = Fnv1a64.AddInt32(hash, tree.BranchRadiusMm);
            }

            Hash = hash;
        }

        /// <summary>No trees: a slope with open snow only.</summary>
        public static Forest Empty { get; } = new Forest(Array.Empty<SlopeTree>());

        /// <summary>The number of trees.</summary>
        public int Count => _trees.Length;

        /// <summary>A 64-bit FNV-1a hash of the tree count and every tree's millimetre fields, in index order.</summary>
        public ulong Hash { get; }

        /// <summary>The tree at an index.</summary>
        /// <param name="index">0 to <see cref="Count"/> - 1.</param>
        public SlopeTree this[int index] => _trees[index];

        /// <summary>The trees of a slope layout, in file order.</summary>
        /// <param name="layout">The parsed layout.</param>
        /// <exception cref="ArgumentNullException"><paramref name="layout"/> is null.</exception>
        public static Forest FromLayout(SlopeLayout layout) =>
            new Forest((layout ?? throw new ArgumentNullException(nameof(layout))).Trees);
    }
}
