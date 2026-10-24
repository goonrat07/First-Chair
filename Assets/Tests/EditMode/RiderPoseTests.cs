using FirstChair.Sim;
using NUnit.Framework;

namespace FirstChair.Riding.Tests
{
    /// <summary>
    /// The pose a view draws between two sim states. Story 004 put flips in the sim; the view pitches the rider by
    /// them only in the air, so a landing never shows the rider unwinding a flip on the snow.
    /// </summary>
    public class RiderPoseTests
    {
        private const double Tolerance = 1e-4;

        [Test]
        public void FlipInTheAir_HalfwayBetweenSteps_IsTheLerpInDegrees()
        {
            // Arrange: a forward flip moving from a quarter turn to a half turn.
            RiderState previous = InTheAir(Fix64.HalfPi);
            RiderState current = InTheAir(Fix64.Pi);

            // Act
            RiderPose pose = RiderPose.Interpolate(previous, current, 0.5);

            // Assert: positive is forward, as in the sim.
            Assert.That(pose.FlipDegrees, Is.EqualTo(135.0).Within(Tolerance));
        }

        [Test]
        public void FlipPastOneTurn_IsNotWrapped()
        {
            // Arrange: a backflip past a full turn; wrapping would spin the rider the short way round.
            RiderState previous = InTheAir(-Fix64.TwoPi + Fix64.FromRatio(1, 10));
            RiderState current = InTheAir(-Fix64.TwoPi - Fix64.FromRatio(1, 10));

            // Act
            RiderPose pose = RiderPose.Interpolate(previous, current, 0.5);

            // Assert
            Assert.That(pose.FlipDegrees, Is.EqualTo(-360.0).Within(Tolerance));
        }

        [Test]
        public void LandingStep_FromMidFlip_ShowsNoFlip()
        {
            // Arrange: the step before landing still carries the flight's flip; the landed state keeps it too.
            RiderState previous = InTheAir(Fix64.TwoPi);
            RiderState current = previous;
            current.Airborne = false;

            // Act
            RiderPose pose = RiderPose.Interpolate(previous, current, 0.5);

            // Assert
            Assert.That(pose.FlipDegrees, Is.EqualTo(0.0));
        }

        [Test]
        public void TakeoffStep_FromTheSnow_StartsTheFlipAtZero()
        {
            // Arrange: on the snow the state still holds the last flight's flip; the first step in the air has a new one.
            RiderState previous = InTheAir(Fix64.Pi);
            previous.Airborne = false;
            RiderState current = InTheAir(Fix64.FromRatio(1, 10));

            // Act
            RiderPose pose = RiderPose.Interpolate(previous, current, 0.5);

            // Assert: half of the new flight's first step, not halfway from the old flight's flip.
            Assert.That(pose.FlipDegrees, Is.EqualTo(0.05 * 180.0 / System.Math.PI).Within(Tolerance));
        }

        private static RiderState InTheAir(Fix64 flip) => new RiderState { Airborne = true, FlipAngle = flip };
    }
}
