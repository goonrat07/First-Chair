using System;
using FirstChair.Riding;
using NUnit.Framework;

namespace FirstChair.Riding.Tests
{
    /// <summary>A chairlift's haul rope as a closed loop, and when the next chair reaches a point (story 010).</summary>
    public sealed class CableLoopTests
    {
        private const double Side = 10.0;
        private const double Tolerance = 1e-9;

        // A level square, 10 m a side: (0, 0, 0) to (0, 0, 10) to (10, 0, 10) to (10, 0, 0) and back.
        private static CableLoop Square() =>
            new CableLoop(new[] { 0.0, 0.0, Side, Side }, new[] { 0.0, 0.0, 0.0, 0.0 }, new[] { 0.0, Side, Side, 0.0 });

        [Test]
        public void Length_IsThePerimeterIncludingTheClosingSide()
        {
            // Act and Assert
            Assert.That(Square().Length, Is.EqualTo(4 * Side).Within(Tolerance));
            Assert.That(Square().DistanceAt(2), Is.EqualTo(2 * Side).Within(Tolerance));
        }

        [TestCase(5.0, 0.0, 5.0, 0.0, 1.0)]
        [TestCase(15.0, 5.0, 10.0, 1.0, 0.0)]
        [TestCase(35.0, 5.0, 0.0, -1.0, 0.0)]
        [TestCase(45.0, 0.0, 5.0, 0.0, 1.0)]
        [TestCase(-5.0, 5.0, 0.0, -1.0, 0.0)]
        public void Evaluate_FindsThePointAndDirection_WrappingRoundTheLoop(double distance, double x, double z, double dx, double dz)
        {
            // Act
            Square().Evaluate(distance, out double px, out double py, out double pz, out double ex, out double ez);

            // Assert
            Assert.That(new[] { px, py, pz, ex, ez }, Is.EqualTo(new[] { x, 0.0, z, dx, dz }).Within(Tolerance));
        }

        [Test]
        public void Evaluate_ClimbingSide_InterpolatesHeight_AndKeepsTheDirectionLevel()
        {
            // Arrange: up 4 m over 3 m of ground, then back down.
            var rope = new CableLoop(new[] { 0.0, 0.0, 1.0 }, new[] { 0.0, 4.0, 0.0 }, new[] { 0.0, 3.0, 0.0 });

            // Act: halfway up the first side (it is 5 m long).
            rope.Evaluate(2.5, out _, out double y, out double z, out double dx, out double dz);

            // Assert
            Assert.That(y, Is.EqualTo(2.0).Within(Tolerance));
            Assert.That(z, Is.EqualTo(1.5).Within(Tolerance));
            Assert.That(dx * dx + dz * dz, Is.EqualTo(1.0).Within(Tolerance));
            Assert.That(dz, Is.EqualTo(1.0).Within(Tolerance));
        }

        [TestCase(0.0, 25.0, 5.0)]
        [TestCase(0.0, 30.0, 0.0)]
        [TestCase(7.0, 5.0, 8.0)]
        [TestCase(-3.0, 0.0, 3.0)]
        public void NextArrival_IsHowFarTheRopeMovesBeforeAChairReachesTheTarget(double offset, double target, double expected)
        {
            // Act and Assert: chairs every 10 m.
            Assert.That(CableLoop.NextArrival(offset, 10.0, target), Is.EqualTo(expected).Within(Tolerance));
        }

        [Test]
        public void Constructor_FewerThanThreePointsOrNoLength_Throws()
        {
            // Act and Assert
            Assert.That(() => new CableLoop(new[] { 0.0, 1.0 }, new[] { 0.0, 0.0 }, new[] { 0.0, 0.0 }), Throws.ArgumentException);
            Assert.That(() => new CableLoop(new[] { 1.0, 1.0, 1.0 }, new[] { 2.0, 2.0, 2.0 }, new[] { 3.0, 3.0, 3.0 }), Throws.ArgumentException);
            Assert.That(() => CableLoop.NextArrival(0.0, 0.0, 1.0), Throws.InstanceOf<ArgumentOutOfRangeException>());
        }
    }
}
