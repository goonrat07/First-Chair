using System;
using FirstChair.Riding;
using NUnit.Framework;

namespace FirstChair.Riding.Tests
{
    /// <summary>The snow's height change at a ski track (Story 018): down in the trench, up where the snow was pushed.</summary>
    public sealed class SnowTrenchTests
    {
        // Powder's first-pass depth (metres) and how much of the trench's spread rises beside it.
        private const float Depth = 0.15f;
        private const float Raise = 0.6f;
        private const float Tolerance = 1e-6f;

        // The snow terrain's layers, one per snow type in SnowType's order, and the lighting rig's default depths (metres).
        private const int Layers = 5;
        private const int Groomed = 0;
        private const int Powder = 1;
        private const int Slush = 4;
        private const float GroomedDepth = 0.02f;
        private const float PowderDepth = 0.15f;
        private const byte FullWeight = 255;
        private static readonly float[] LayerDepths = { GroomedDepth, PowderDepth, 0.08f, 0f, 0.06f };

        [Test]
        public void Offset_OnTheCentreLine_IsTheFullDepthDown()
        {
            // Act
            float offset = SnowTrench.Offset(1f, 0.8f, Depth, Raise);

            // Assert
            Assert.That(offset, Is.EqualTo(-Depth).Within(Tolerance));
        }

        [Test]
        public void Offset_BesideTheTrench_RaisesTheSnowItPushedAside()
        {
            // Act: past the trench's edge, where only its blurred spread reaches
            float offset = SnowTrench.Offset(0f, 0.3f, Depth, Raise);

            // Assert
            Assert.That(offset, Is.EqualTo(Depth * Raise * 0.3f).Within(Tolerance));
            Assert.That(offset, Is.GreaterThan(0f));
        }

        [Test]
        public void Offset_ANewTrenchThroughAnOldRim_CutsTheRimAway()
        {
            // Act: a crossing track's trench where another's spread was raised
            float offset = SnowTrench.Offset(1f, 1f, Depth, Raise);

            // Assert
            Assert.That(offset, Is.EqualTo(-Depth).Within(Tolerance));
        }

        [Test]
        public void Offset_AwayFromTracksOrWithNoDepth_IsZero()
        {
            // Act and assert
            Assert.That(SnowTrench.Offset(0f, 0f, Depth, Raise), Is.EqualTo(0f));
            Assert.That(SnowTrench.Offset(1f, 0.5f, 0f, Raise), Is.EqualTo(0f));
        }

        [Test]
        public void GaussianWeights_AnyWidth_SumToOneAndFallAway()
        {
            // Arrange
            var weights = new float[5];

            // Act
            SnowTrench.GaussianWeights(2f, weights);

            // Assert: the centre plus both sides of each other tap make one, and each tap weighs less than the last
            float sum = weights[0];
            for (int i = 1; i < weights.Length; i++)
            {
                sum += 2f * weights[i];
                Assert.That(weights[i], Is.LessThan(weights[i - 1]));
            }

            Assert.That(sum, Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void GaussianWeights_ABadWidthOrNoTaps_Throws()
        {
            // Act and assert
            Assert.Throws<ArgumentOutOfRangeException>(() => SnowTrench.GaussianWeights(0f, new float[3]));
            Assert.Throws<ArgumentException>(() => SnowTrench.GaussianWeights(1f, new float[0]));
        }

        [Test]
        public void SplatDepth_OneSnowTypeEverywhere_IsItsDepthTimesTheStrength()
        {
            // Arrange: powder over the whole terrain
            byte[] weights = Uniform(4, Powder);

            // Act
            float full = SnowTrench.SplatDepth(weights, 4, Layers, 0.3f, 0.7f, LayerDepths, 1f);
            float half = SnowTrench.SplatDepth(weights, 4, Layers, 0.3f, 0.7f, LayerDepths, 0.5f);

            // Assert
            Assert.That(full, Is.EqualTo(PowderDepth).Within(Tolerance));
            Assert.That(half, Is.EqualTo(0.5f * PowderDepth).Within(Tolerance));
        }

        [Test]
        public void SplatDepth_OnATexelCentre_IsThatTexelsDepth()
        {
            // Arrange: groomed snow with one powder texel in the middle of three by three; the texel centres fall on the
            // terrain's corners, edges and middle, so the middle one is at (0.5, 0.5)
            byte[] weights = Uniform(3, Groomed);
            Paint(weights, 3, 1, 1, Powder);

            // Act
            float middle = SnowTrench.SplatDepth(weights, 3, Layers, 0.5f, 0.5f, LayerDepths, 1f);
            float corner = SnowTrench.SplatDepth(weights, 3, Layers, 0f, 0f, LayerDepths, 1f);

            // Assert
            Assert.That(middle, Is.EqualTo(PowderDepth).Within(Tolerance));
            Assert.That(corner, Is.EqualTo(GroomedDepth).Within(Tolerance));
        }

        [Test]
        public void SplatDepth_BetweenTexelCentres_BlendsTheirDepths()
        {
            // Arrange: two by two, groomed on the left column (low X) and powder on the right
            byte[] weights = Uniform(2, Groomed);
            Paint(weights, 2, 1, 0, Powder);
            Paint(weights, 2, 1, 1, Powder);

            // Act: halfway across and a quarter of the way
            float halfway = SnowTrench.SplatDepth(weights, 2, Layers, 0.5f, 0.2f, LayerDepths, 1f);
            float quarter = SnowTrench.SplatDepth(weights, 2, Layers, 0.25f, 0.9f, LayerDepths, 1f);

            // Assert
            Assert.That(halfway, Is.EqualTo(0.5f * (GroomedDepth + PowderDepth)).Within(Tolerance));
            Assert.That(quarter, Is.EqualTo(0.75f * GroomedDepth + 0.25f * PowderDepth).Within(Tolerance));
        }

        [Test]
        public void SplatDepth_OffTheTerrain_HoldsTheEdgeTexels()
        {
            // Arrange: as above, groomed on the left and powder on the right
            byte[] weights = Uniform(2, Groomed);
            Paint(weights, 2, 1, 0, Powder);
            Paint(weights, 2, 1, 1, Powder);

            // Act
            float left = SnowTrench.SplatDepth(weights, 2, Layers, -3f, 0.5f, LayerDepths, 1f);
            float right = SnowTrench.SplatDepth(weights, 2, Layers, 4f, 0.5f, LayerDepths, 1f);

            // Assert
            Assert.That(left, Is.EqualTo(GroomedDepth).Within(Tolerance));
            Assert.That(right, Is.EqualTo(PowderDepth).Within(Tolerance));
        }

        [Test]
        public void SplatDepth_ALayerWithNoDepthGiven_CutsNothing()
        {
            // Arrange: slush everywhere, and depths for the first four layers only
            byte[] weights = Uniform(2, Slush);
            var firstFour = new float[Slush];
            Array.Copy(LayerDepths, firstFour, Slush);

            // Act
            float depth = SnowTrench.SplatDepth(weights, 2, Layers, 0.5f, 0.5f, firstFour, 1f);

            // Assert
            Assert.That(depth, Is.EqualTo(0f));
        }

        [Test]
        public void SplatDepth_WeightsOfTheWrongSizeOrNoDepths_Throws()
        {
            // Act and assert
            Assert.Throws<ArgumentException>(() => SnowTrench.SplatDepth(new byte[7], 2, Layers, 0f, 0f, LayerDepths, 1f));
            Assert.Throws<ArgumentException>(() => SnowTrench.SplatDepth(Uniform(2, Groomed), 0, Layers, 0f, 0f, LayerDepths, 1f));
            Assert.Throws<ArgumentNullException>(() => SnowTrench.SplatDepth(Uniform(2, Groomed), 2, Layers, 0f, 0f, null, 1f));
        }

        // Splat weights as the snow terrain has them: one byte a layer a texel, rows along Z, every texel all one layer.
        private static byte[] Uniform(int texels, int layer)
        {
            var weights = new byte[texels * texels * Layers];
            for (int texel = 0; texel < texels * texels; texel++)
            {
                weights[texel * Layers + layer] = FullWeight;
            }

            return weights;
        }

        // Makes one texel all one layer.
        private static void Paint(byte[] weights, int texels, int column, int row, int layer)
        {
            int at = (row * texels + column) * Layers;
            Array.Clear(weights, at, Layers);
            weights[at + layer] = FullWeight;
        }
    }
}
