using System;

namespace FirstChair.Riding
{
    /// <summary>
    /// Which of the forest's tree models stands at a tree (Story 016): picked from a hash of the tree's position in the
    /// layout, weighted by a mix, so a mountain always grows the same forest and neighbours differ. Integer math only, so
    /// every machine picks alike. The sim never sees the choice: its trees keep the layout's radii. Also how big the
    /// chosen model stands (<see cref="Scale"/>).
    /// </summary>
    /// <example>
    /// <code>
    /// int[] mix = { 45, 25, 20, 7, 3 };                        // spruce, slim fir, broad spruce, young, snag
    /// int variant = TreeVariantPicker.Pick(tree.XMm, tree.ZMm, mix);   // 0 to 4; -1 when the mix is all zero
    /// float scale = TreeVariantPicker.Scale(branchRadius, 0.5f * crownWidth, height, 16.5f);
    /// </code>
    /// </example>
    public static class TreeVariantPicker
    {
        /// <summary>The least a model is scaled to fit the layout's branch radius.</summary>
        public const float MinScale = 0.5f;

        /// <summary>The most a model is scaled to fit the layout's branch radius.</summary>
        public const float MaxScale = 2f;

        // Odd 64-bit constants (the golden ratio's and two of MurmurHash3's) that spread x and z before the finalizer.
        private const ulong MixX = 0x9E3779B97F4A7C15UL;
        private const ulong MixZ = 0xC2B2AE3D27D4EB4FUL;
        private const ulong Fmix1 = 0xFF51AFD7ED558CCDUL;
        private const ulong Fmix2 = 0xC4CEB9FE1A85EC53UL;

        /// <summary>
        /// How much to scale a tree model: its crown radius matched to the layout's branch radius (so what the sim's
        /// branches clip is what the rider sees), from <see cref="MinScale"/> to <see cref="MaxScale"/> times, then lowered
        /// if that would stand the model taller than <paramref name="maxHeight"/>.
        /// </summary>
        /// <param name="branchRadius">The layout's branch radius for the tree, metres.</param>
        /// <param name="crownRadius">The model's crown radius, metres; above 0.</param>
        /// <param name="height">The model's height, metres; 0 or less when unknown, which skips the height limit.</param>
        /// <param name="maxHeight">The tallest a tree may stand, metres.</param>
        public static float Scale(float branchRadius, float crownRadius, float height, float maxHeight)
        {
            float scale = Math.Max(MinScale, Math.Min(MaxScale, branchRadius / crownRadius));
            return height > 0f ? Math.Min(scale, maxHeight / height) : scale;
        }

        /// <summary>The variant at a position: an index into <paramref name="weights"/>, chosen in proportion to them.</summary>
        /// <param name="xMm">The tree's x, millimetres, as the layout gives it.</param>
        /// <param name="zMm">The tree's z, millimetres.</param>
        /// <param name="weights">Each variant's share, in any whole units (per cent, per mille); a negative one counts as 0.</param>
        /// <returns>The variant's index, or -1 when there are no weights or every one is 0.</returns>
        public static int Pick(long xMm, long zMm, int[] weights)
        {
            if (weights == null)
            {
                return -1;
            }

            long total = 0;
            foreach (int w in weights)
            {
                total += w > 0 ? w : 0;
            }

            if (total == 0)
            {
                return -1;
            }

            long roll = (long)(Hash(xMm, zMm) % (ulong)total);
            for (int i = 0; i < weights.Length; i++)
            {
                roll -= weights[i] > 0 ? weights[i] : 0;
                if (roll < 0)
                {
                    return i;
                }
            }

            return weights.Length - 1;   // not reached: the roll is below the total
        }

        /// <summary>A well-mixed 64-bit hash of a position (MurmurHash3's finalizer over the two coordinates).</summary>
        /// <param name="xMm">X, millimetres.</param>
        /// <param name="zMm">Z, millimetres.</param>
        public static ulong Hash(long xMm, long zMm)
        {
            ulong h = unchecked((ulong)xMm * MixX + (ulong)zMm * MixZ);
            h ^= h >> 33;
            h = unchecked(h * Fmix1);
            h ^= h >> 33;
            h = unchecked(h * Fmix2);
            h ^= h >> 33;
            return h;
        }
    }
}
