using System;

namespace FirstChair.Riding
{
    /// <summary>
    /// Which parts of a terrain's splat map hold any weight in a run of its layers (Story 018). The map is cut into square
    /// blocks of texels, each flagged once if any texel in it weighs anything in those layers. The snow mesh near the
    /// rider asks whether any lies under it, so it can leave out its add pass (the terrain's layers 4 to 7; on the test
    /// slope, slush) where that pass would add nothing. Engine-free; built once, then read only, so any thread may read
    /// it.
    /// </summary>
    /// <example>
    /// <code>
    /// var slush = new SplatLayerBlocks(weights, texels, layers, firstLayer: 4, blockTexels: 32);
    /// bool addPass = slush.AnyIn(uMin, vMin, uMax, vMax);   // the mesh's square, in terrain UV
    /// </code>
    /// </example>
    public sealed class SplatLayerBlocks
    {
        private readonly bool[] _flagged;
        private readonly int _texels;
        private readonly int _blockTexels;
        private readonly int _blocksPerSide;

        // A read within this many texels of a whole texel takes nothing from the next one: float rounding in the UV (Unity's
        // Mono keeps 39 / 63 * 63 as 39.0000014) must not reach into the next block.
        private const double TexelSlack = 1e-3;

        /// <summary>Flags the blocks of <paramref name="weights"/> where layers from <paramref name="firstLayer"/> on weigh anything.</summary>
        /// <param name="weights">The splat weights, a byte a layer a texel, as <see cref="SnowTrench.SplatDepth"/> reads them.</param>
        /// <param name="texels">Texels a side; at least 1.</param>
        /// <param name="layers">Layers a texel; at least 1.</param>
        /// <param name="firstLayer">The first layer that counts; every layer after it counts too. Below <paramref name="layers"/>.</param>
        /// <param name="blockTexels">Texels a side of a block; at least 1.</param>
        /// <exception cref="ArgumentException">Weights that don't hold every layer of every texel, or a layer or block size out of range.</exception>
        public SplatLayerBlocks(byte[] weights, int texels, int layers, int firstLayer, int blockTexels)
        {
            if (texels < 1 || layers < 1 || weights == null || weights.Length != texels * texels * layers)
            {
                throw new ArgumentException("The weights must hold every layer of every texel.", nameof(weights));
            }

            if (firstLayer < 0 || firstLayer >= layers)
            {
                throw new ArgumentException("The first layer must be one of the map's layers.", nameof(firstLayer));
            }

            if (blockTexels < 1)
            {
                throw new ArgumentException("A block must hold at least one texel.", nameof(blockTexels));
            }

            _texels = texels;
            _blockTexels = blockTexels;
            _blocksPerSide = (texels + blockTexels - 1) / blockTexels;
            _flagged = new bool[_blocksPerSide * _blocksPerSide];
            for (int row = 0; row < texels; row++)
            {
                for (int column = 0; column < texels; column++)
                {
                    int at = (row * texels + column) * layers;
                    for (int layer = firstLayer; layer < layers; layer++)
                    {
                        if (weights[at + layer] != 0)
                        {
                            _flagged[(row / blockTexels) * _blocksPerSide + column / blockTexels] = true;
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>Whether any texel a bilinear read inside the box would touch weighs anything in the layers.</summary>
        /// <param name="uMin">The box's least terrain U (0 at the terrain's least X, 1 at its greatest); held to the terrain.</param>
        /// <param name="vMin">Its least V (along Z).</param>
        /// <param name="uMax">Its greatest U.</param>
        /// <param name="vMax">Its greatest V.</param>
        /// <returns>True if any such texel weighs anything there.</returns>
        public bool AnyIn(float uMin, float vMin, float uMax, float vMax)
        {
            // A bilinear read at texel position t blends texels floor(t) and floor(t) + 1, so the box reaches from the
            // texel below its least edge to the texel above its greatest.
            int blockMinX = Block(Math.Floor(Position(uMin) + TexelSlack));
            int blockMaxX = Block(Math.Ceiling(Position(uMax) - TexelSlack));
            int blockMinZ = Block(Math.Floor(Position(vMin) + TexelSlack));
            int blockMaxZ = Block(Math.Ceiling(Position(vMax) - TexelSlack));
            for (int row = blockMinZ; row <= blockMaxZ; row++)
            {
                for (int column = blockMinX; column <= blockMaxX; column++)
                {
                    if (_flagged[row * _blocksPerSide + column])
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // A terrain UV's position in texels, in double so every runtime rounds it alike.
        private double Position(float uv) => (double)Clamp01(uv) * (_texels - 1);

        // The block holding a texel, the texel held to the map.
        private int Block(double texel) => (int)Math.Min(Math.Max(texel, 0.0), _texels - 1) / _blockTexels;

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    }
}
