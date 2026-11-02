using System;
using FirstChair.Riding;
using NUnit.Framework;

namespace FirstChair.Riding.Tests
{
    /// <summary>Which blocks of a splat map hold any slush (Story 018): the snow mesh skips its add pass where none does.</summary>
    public sealed class SplatLayerBlocksTests
    {
        // A 64-texel map of the snow terrain's five layers, in blocks of 8; slush is the add pass's first layer.
        private const int Texels = 64;
        private const int Layers = 5;
        private const int Groomed = 0;
        private const int Slush = 4;
        private const int BlockTexels = 8;
        private const byte FullWeight = 255;

        [Test]
        public void AnyIn_NoSlushAnywhere_IsFalseOverTheWholeTerrain()
        {
            // Arrange
            var blocks = new SplatLayerBlocks(Uniform(Groomed), Texels, Layers, Slush, BlockTexels);

            // Act and assert
            Assert.That(blocks.AnyIn(0f, 0f, 1f, 1f), Is.False);
        }

        [Test]
        public void AnyIn_ABoxOverASlushTexel_IsTrue()
        {
            // Arrange: one slush texel at column 40, row 20
            byte[] weights = Uniform(Groomed);
            Paint(weights, 40, 20, Slush);
            var blocks = new SplatLayerBlocks(weights, Texels, Layers, Slush, BlockTexels);

            // Act
            bool over = blocks.AnyIn(U(36f), U(16f), U(44f), U(24f));

            // Assert
            Assert.That(over, Is.True);
        }

        [Test]
        public void AnyIn_ABoxBlocksAway_IsFalse()
        {
            // Arrange
            byte[] weights = Uniform(Groomed);
            Paint(weights, 40, 20, Slush);
            var blocks = new SplatLayerBlocks(weights, Texels, Layers, Slush, BlockTexels);

            // Act
            bool away = blocks.AnyIn(U(0f), U(0f), U(10f), U(10f));

            // Assert
            Assert.That(away, Is.False);
        }

        [Test]
        public void AnyIn_ABoxEndingBetweenTexels_CountsTheTexelTheReadReaches()
        {
            // Arrange: slush at column 40, the first texel of its block; the block before it is clear
            byte[] weights = Uniform(Groomed);
            Paint(weights, 40, 20, Slush);
            var blocks = new SplatLayerBlocks(weights, Texels, Layers, Slush, BlockTexels);

            // Act: a read at column 39.5 blends columns 39 and 40; one at column 39 reads column 39 alone
            bool halfway = blocks.AnyIn(U(30f), U(16f), U(39.5f), U(24f));
            bool short_ = blocks.AnyIn(U(30f), U(16f), U(39f), U(24f));

            // Assert
            Assert.That(halfway, Is.True);
            Assert.That(short_, Is.False);
        }

        [Test]
        public void AnyIn_ABoxPastTheTerrain_IsHeldToIt()
        {
            // Arrange: slush in the far corner
            byte[] weights = Uniform(Groomed);
            Paint(weights, Texels - 1, Texels - 1, Slush);
            var blocks = new SplatLayerBlocks(weights, Texels, Layers, Slush, BlockTexels);

            // Act
            bool beyond = blocks.AnyIn(0.9f, 0.9f, 3f, 3f);

            // Assert
            Assert.That(beyond, Is.True);
        }

        [Test]
        public void Constructor_WeightsOfTheWrongSizeOrALayerOrBlockOutOfRange_Throws()
        {
            // Act and assert
            Assert.Throws<ArgumentException>(() => new SplatLayerBlocks(new byte[7], Texels, Layers, Slush, BlockTexels));
            Assert.Throws<ArgumentException>(() => new SplatLayerBlocks(Uniform(Groomed), Texels, Layers, Layers, BlockTexels));
            Assert.Throws<ArgumentException>(() => new SplatLayerBlocks(Uniform(Groomed), Texels, Layers, Slush, 0));
        }

        // The terrain U (or V) of a texel column (or row): the texel centres fall on the terrain's corners and edges.
        private static float U(float texel) => texel / (Texels - 1);

        // Splat weights with every texel all one layer, a byte a layer a texel, rows along Z.
        private static byte[] Uniform(int layer)
        {
            var weights = new byte[Texels * Texels * Layers];
            for (int texel = 0; texel < Texels * Texels; texel++)
            {
                weights[texel * Layers + layer] = FullWeight;
            }

            return weights;
        }

        // Makes one texel all one layer.
        private static void Paint(byte[] weights, int column, int row, int layer)
        {
            int at = (row * Texels + column) * Layers;
            Array.Clear(weights, at, Layers);
            weights[at + layer] = FullWeight;
        }
    }
}
