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
    }
}
