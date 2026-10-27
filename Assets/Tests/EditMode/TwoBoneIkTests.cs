using FirstChair.Riding;
using NUnit.Framework;

namespace FirstChair.Riding.Tests
{
    /// <summary>The analytic two-bone solve for the legs and arms (Story 013).</summary>
    public sealed class TwoBoneIkTests
    {
        // A leg: hip at the top, knee 0.42 m below, ankle 0.39 m below that, the knee bent forward (-Z is forward here).
        private static readonly PoseVector Hip = new PoseVector(0.1, 0.9, 0.0);
        private static readonly PoseVector Knee = new PoseVector(0.1, 0.48, -0.05);
        private static readonly PoseVector Ankle = new PoseVector(0.1, 0.1, 0.0);
        private static readonly PoseVector Forward = new PoseVector(0.1, 0.5, -1.0);

        private static double Upper => (Knee - Hip).Length;

        private static double Lower => (Ankle - Knee).Length;

        [Test]
        public void Solve_ReachableTarget_ReachesItAndKeepsTheBoneLengths()
        {
            // Arrange: the snow is 6 cm higher under this foot
            var target = new PoseVector(0.1, 0.16, 0.02);

            // Act
            TwoBoneIk.Solve(Hip, Knee, Ankle, target, Forward, out PoseVector knee, out PoseVector ankle);

            // Assert
            Assert.That((ankle - target).Length, Is.LessThan(1e-9));
            Assert.That((knee - Hip).Length, Is.EqualTo(Upper).Within(1e-9));
            Assert.That((ankle - knee).Length, Is.EqualTo(Lower).Within(1e-9));
        }

        [Test]
        public void Solve_BendsTheKneeTowardTheHint()
        {
            // Arrange
            var target = new PoseVector(0.1, 0.25, 0.0);

            // Act
            TwoBoneIk.Solve(Hip, Knee, Ankle, target, Forward, out PoseVector knee, out _);

            // Assert: the knee is in front of the hip-to-foot line (negative Z), not behind it
            Assert.That(knee.Z, Is.LessThan(-0.05));
        }

        [Test]
        public void Solve_HintBehind_BendsTheOtherWay()
        {
            // Arrange
            var target = new PoseVector(0.1, 0.25, 0.0);
            var behind = new PoseVector(0.1, 0.5, 1.0);

            // Act
            TwoBoneIk.Solve(Hip, Knee, Ankle, target, behind, out PoseVector knee, out _);

            // Assert
            Assert.That(knee.Z, Is.GreaterThan(0.05));
        }

        [Test]
        public void Solve_OutOfReach_StraightensTowardTheTarget()
        {
            // Arrange: snow 30 cm below the straight leg
            var target = new PoseVector(0.1, -0.3, 0.0);

            // Act
            TwoBoneIk.Solve(Hip, Knee, Ankle, target, Forward, out PoseVector knee, out PoseVector ankle);

            // Assert: the foot lies on the line to the target, at (nearly) full reach, lengths kept
            PoseVector toTarget = (target - Hip).Normalized();
            PoseVector toAnkle = (ankle - Hip).Normalized();
            Assert.That(PoseVector.Dot(toTarget, toAnkle), Is.EqualTo(1.0).Within(1e-9));
            Assert.That((ankle - Hip).Length, Is.GreaterThan((Upper + Lower) * 0.999));
            Assert.That((knee - Hip).Length, Is.EqualTo(Upper).Within(1e-9));
            Assert.That((ankle - knee).Length, Is.EqualTo(Lower).Within(1e-9));
        }

        [Test]
        public void Solve_TargetAtTheHip_FoldsWithoutBreakingTheBones()
        {
            // Act
            TwoBoneIk.Solve(Hip, Knee, Ankle, Hip, Forward, out PoseVector knee, out PoseVector ankle);

            // Assert
            Assert.That((knee - Hip).Length, Is.EqualTo(Upper).Within(1e-9));
            Assert.That((ankle - knee).Length, Is.EqualTo(Lower).Within(1e-9));
            Assert.That(double.IsNaN(ankle.X) || double.IsNaN(knee.Y), Is.False);
        }

        [Test]
        public void Solve_HintOnTheLine_KeepsTheClipsBendPlane()
        {
            // Arrange: a hint straight below the hip says nothing about which way to bend
            var target = new PoseVector(0.1, 0.25, 0.0);
            var below = new PoseVector(0.1, -1.0, 0.0);

            // Act
            TwoBoneIk.Solve(Hip, Knee, Ankle, target, below, out PoseVector knee, out _);

            // Assert: the knee still bends forward, as in the clip
            Assert.That(knee.Z, Is.LessThan(-0.05));
        }
    }
}
