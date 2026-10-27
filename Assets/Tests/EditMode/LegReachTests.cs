using FirstChair.Riding;
using NUnit.Framework;

namespace FirstChair.Riding.Tests
{
    /// <summary>How far the hips come down so both feet reach the snow (Story 013).</summary>
    public sealed class LegReachTests
    {
        private static readonly PoseVector Up = new PoseVector(0.0, 1.0, 0.0);
        private const double Reach = 0.80;
        private const double MaxDrop = 0.15;

        [Test]
        public void HipDrop_BothFeetWithinReach_IsZero()
        {
            // Arrange: each hip 0.7 m above its foot target
            var left = new PoseVector(0.0, 0.70, 0.0);
            var right = new PoseVector(0.0, 0.72, 0.05);

            // Act
            double drop = LegReach.HipDrop(left, right, Up, Reach, Reach, MaxDrop);

            // Assert
            Assert.That(drop, Is.EqualTo(0.0));
        }

        [Test]
        public void HipDrop_OneFootBelowReach_DropsByItsShortfall()
        {
            // Arrange: the right foot's snow is 0.86 m below its hip, 6 cm out of reach
            var left = new PoseVector(0.0, 0.70, 0.0);
            var right = new PoseVector(0.0, 0.86, 0.0);

            // Act
            double drop = LegReach.HipDrop(left, right, Up, Reach, Reach, MaxDrop);

            // Assert
            Assert.That(drop, Is.EqualTo(0.06).Within(1e-9));
        }

        [Test]
        public void HipDrop_FootOutToTheSide_CountsTheWholeDistance()
        {
            // Arrange: 0.6 down and 0.6 across is 0.849 m away: dropping 0.069 m brings it to 0.8
            var left = new PoseVector(0.6, 0.6, 0.0);
            var right = new PoseVector(0.0, 0.5, 0.0);

            // Act
            double drop = LegReach.HipDrop(left, right, Up, Reach, Reach, MaxDrop);

            // Assert: (0.6 - drop)^2 + 0.6^2 = 0.8^2
            Assert.That(drop, Is.EqualTo(0.6 - System.Math.Sqrt(0.8 * 0.8 - 0.36)).Within(1e-9));
        }

        [Test]
        public void HipDrop_FarOutOfReach_StopsAtTheLimit()
        {
            // Arrange
            var left = new PoseVector(0.0, 1.5, 0.0);
            var right = new PoseVector(0.0, 0.7, 0.0);

            // Act
            double drop = LegReach.HipDrop(left, right, Up, Reach, Reach, MaxDrop);

            // Assert
            Assert.That(drop, Is.EqualTo(MaxDrop));
        }

        [Test]
        public void HipDrop_FootTooFarSidewaysToEverReach_StopsAtTheLimit()
        {
            // Arrange: 0.9 m across, beyond the leg however low the hips go
            var left = new PoseVector(0.9, 0.3, 0.0);
            var right = new PoseVector(0.0, 0.7, 0.0);

            // Act
            double drop = LegReach.HipDrop(left, right, Up, Reach, Reach, MaxDrop);

            // Assert
            Assert.That(drop, Is.EqualTo(MaxDrop));
        }

        [Test]
        public void HipDrop_NegativeLimit_IsZero()
        {
            // Act
            double drop = LegReach.HipDrop(new PoseVector(0.0, 1.5, 0.0), new PoseVector(0.0, 0.7, 0.0), Up, Reach, Reach, -1.0);

            // Assert
            Assert.That(drop, Is.EqualTo(0.0));
        }
    }
}
