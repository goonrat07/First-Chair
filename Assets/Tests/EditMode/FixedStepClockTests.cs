using NUnit.Framework;

namespace FirstChair.Riding.Tests
{
    /// <summary>The fixed-step accumulator <c>SimDriver</c> uses to run 120 Hz steps from variable frames.</summary>
    public class FixedStepClockTests
    {
        private const int StepsPerSecond = 120;
        private const int Cap = 12;

        [Test]
        public void SixtyHertzFrames_RunTwoStepsEach()
        {
            // Arrange
            var clock = new FixedStepClock(StepsPerSecond, Cap);
            int total = 0;

            // Act
            for (int i = 0; i < 600; i++)
            {
                int steps = clock.Advance(1.0 / 60);
                Assert.That(steps, Is.EqualTo(2), "frame " + i);
                total += steps;
            }

            // Assert
            Assert.That(total, Is.EqualTo(1200));
            Assert.That(clock.Alpha, Is.LessThan(1e-6));
        }

        [Test]
        public void ShortFrames_AccumulateUntilAStepIsDue()
        {
            // Arrange
            var clock = new FixedStepClock(StepsPerSecond, Cap);

            // Act
            int first = clock.Advance(1.0 / 240);
            double alpha = clock.Alpha;
            int second = clock.Advance(1.0 / 240);

            // Assert
            Assert.That(first, Is.EqualTo(0));
            Assert.That(alpha, Is.EqualTo(0.5).Within(1e-9));
            Assert.That(second, Is.EqualTo(1));
        }

        [Test]
        public void LongFrame_IsCappedAndReportsDroppedSteps()
        {
            // Arrange
            var clock = new FixedStepClock(StepsPerSecond, Cap);

            // Act
            int steps = clock.Advance(0.5);

            // Assert
            Assert.That(steps, Is.EqualTo(Cap));
            Assert.That(clock.LastDroppedSteps, Is.EqualTo(60 - Cap));
            Assert.That(clock.TotalDroppedSteps, Is.EqualTo(60 - Cap));
            Assert.That(clock.Alpha, Is.LessThan(1.0));
        }

        [Test]
        public void FrameWithinTheCap_MovesTheViewThroughAllOfIt()
        {
            // Arrange
            var clock = new FixedStepClock(StepsPerSecond, Cap);

            // Act: a frame of two steps, then one too short for a step (the view moves half a step between states).
            clock.Advance(1.0 / 60);
            double sixtyHertz = clock.LastAdvancedSeconds;
            clock.Advance(1.0 / 240);

            // Assert
            Assert.That(sixtyHertz, Is.EqualTo(1.0 / 60).Within(1e-12));
            Assert.That(clock.LastAdvancedSeconds, Is.EqualTo(1.0 / 240).Within(1e-12));
        }

        [Test]
        public void LongFrame_MovesTheViewOnlyThroughTheStepsItRan()
        {
            // Arrange
            var clock = new FixedStepClock(StepsPerSecond, Cap);

            // Act
            clock.Advance(0.5);

            // Assert: 12 steps of 1/120 s; the 48 dropped never reach the view.
            Assert.That(clock.LastAdvancedSeconds, Is.EqualTo(0.1).Within(1e-12));
        }

        [TestCase(0.0)]
        [TestCase(-0.1)]
        [TestCase(double.NaN)]
        public void ZeroNegativeOrNonFiniteFrame_MovesTheViewThroughNothing(double seconds)
        {
            // Arrange: a normal frame first, so a stale value would show.
            var clock = new FixedStepClock(StepsPerSecond, Cap);
            clock.Advance(1.0 / 60);

            // Act
            clock.Advance(seconds);

            // Assert
            Assert.That(clock.LastAdvancedSeconds, Is.EqualTo(0.0));
        }

        [TestCase(0.0)]
        [TestCase(-0.1)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void ZeroNegativeOrNonFiniteFrame_RunsNoSteps(double seconds)
        {
            // Arrange
            var clock = new FixedStepClock(StepsPerSecond, Cap);

            // Act
            int steps = clock.Advance(seconds);

            // Assert
            Assert.That(steps, Is.EqualTo(0));
            Assert.That(clock.Alpha, Is.EqualTo(0.0));
        }
    }
}
