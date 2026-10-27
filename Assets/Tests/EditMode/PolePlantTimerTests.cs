using FirstChair.Riding;
using NUnit.Framework;

namespace FirstChair.Riding.Tests
{
    /// <summary>When the skier plants a pole: at each change from one edge to the other, on the snow at speed (Story 013).</summary>
    public sealed class PolePlantTimerTests
    {
        private const double Frame = 1.0 / 60;
        private const double Edge = 0.3;        // radians
        private const double Speed = 8.0;       // m/s

        private static readonly PolePlantSettings Settings = new PolePlantSettings(
            minSpeed: 3.0, edgeThreshold: 0.12, reachSeconds: 0.15, holdSeconds: 0.2, releaseSeconds: 0.25);

        private static void Hold(ref PolePlantTimer timer, double edge, double speed, bool canPlant, double seconds)
        {
            for (double t = 0.0; t < seconds - 1e-9; t += Frame)
            {
                timer.Update(edge, speed, canPlant, Frame, Settings);
            }
        }

        [Test]
        public void Update_LeftEdgeToRightEdge_PlantsTheRightPole()
        {
            // Arrange
            var timer = new PolePlantTimer();
            Hold(ref timer, -Edge, Speed, true, 0.5);

            // Act
            timer.Update(Edge, Speed, true, Frame, Settings);

            // Assert: right edges turn right, so the new turn's inside pole is the right one
            Assert.That(timer.Side, Is.EqualTo(1));
        }

        [Test]
        public void Update_RightEdgeToLeftEdge_PlantsTheLeftPole()
        {
            // Arrange
            var timer = new PolePlantTimer();
            Hold(ref timer, Edge, Speed, true, 0.5);

            // Act
            timer.Update(-Edge, Speed, true, Frame, Settings);

            // Assert
            Assert.That(timer.Side, Is.EqualTo(-1));
        }

        [Test]
        public void Update_FirstEdgeFromAStraightRun_DoesNotPlant()
        {
            // Arrange
            var timer = new PolePlantTimer();
            Hold(ref timer, 0.0, Speed, true, 0.5);

            // Act
            Hold(ref timer, Edge, Speed, true, 0.2);

            // Assert
            Assert.That(timer.Side, Is.EqualTo(0));
        }

        [Test]
        public void Update_TooSlow_DoesNotPlant()
        {
            // Arrange
            var timer = new PolePlantTimer();
            Hold(ref timer, -Edge, 2.0, true, 0.5);

            // Act
            timer.Update(Edge, 2.0, true, Frame, Settings);

            // Assert
            Assert.That(timer.Side, Is.EqualTo(0));
        }

        [Test]
        public void Update_NotAllowed_DoesNotPlant()
        {
            // Arrange: tucked, in the air, crashed or on the lift
            var timer = new PolePlantTimer();
            Hold(ref timer, -Edge, Speed, false, 0.5);

            // Act
            timer.Update(Edge, Speed, false, Frame, Settings);

            // Assert
            Assert.That(timer.Side, Is.EqualTo(0));
        }

        [Test]
        public void Update_WiggleInsideTheThreshold_DoesNotPlant()
        {
            // Arrange
            var timer = new PolePlantTimer();
            Hold(ref timer, -Edge, Speed, true, 0.5);

            // Act: the edges flatten and tip a little the other way, never past the threshold
            Hold(ref timer, 0.08, Speed, true, 0.3);

            // Assert
            Assert.That(timer.Side, Is.EqualTo(0));
        }

        [Test]
        public void Update_StayingOnTheSameEdge_PlantsOncePerTurn()
        {
            // Arrange
            var timer = new PolePlantTimer();
            Hold(ref timer, -Edge, Speed, true, 0.5);
            timer.Update(Edge, Speed, true, Frame, Settings);

            // Act: the whole plant plays out, then the turn goes on
            Hold(ref timer, Edge, Speed, true, 2.0);

            // Assert
            Assert.That(timer.Side, Is.EqualTo(0));
            Assert.That(timer.Weight(Settings), Is.EqualTo(0.0));
        }

        [Test]
        public void Weight_ReachesUpHoldsThenLetsGo()
        {
            // Arrange
            var timer = new PolePlantTimer();
            Hold(ref timer, -Edge, Speed, true, 0.5);
            timer.Update(Edge, Speed, true, 0.0, Settings);

            // Act and assert: 0 at the start, 1 by the end of the reach and through the hold, 0 after the release
            Assert.That(timer.Weight(Settings), Is.EqualTo(0.0).Within(1e-9));
            Assert.That(timer.Planted(Settings), Is.False);
            timer.Update(Edge, Speed, true, 0.15, Settings);
            Assert.That(timer.Weight(Settings), Is.EqualTo(1.0).Within(1e-9));
            Assert.That(timer.Planted(Settings), Is.True);
            timer.Update(Edge, Speed, true, 0.19, Settings);
            Assert.That(timer.Weight(Settings), Is.EqualTo(1.0).Within(1e-9));
            Assert.That(timer.Planted(Settings), Is.True);
            timer.Update(Edge, Speed, true, 0.13, Settings);
            Assert.That(timer.Weight(Settings), Is.GreaterThan(0.0).And.LessThan(1.0));
            Assert.That(timer.Planted(Settings), Is.False);
            timer.Update(Edge, Speed, true, 0.2, Settings);
            Assert.That(timer.Weight(Settings), Is.EqualTo(0.0));
            Assert.That(timer.Side, Is.EqualTo(0));
        }

        [Test]
        public void Update_LeavingTheSnowMidPlant_LetsGo()
        {
            // Arrange
            var timer = new PolePlantTimer();
            Hold(ref timer, -Edge, Speed, true, 0.5);
            timer.Update(Edge, Speed, true, Frame, Settings);

            // Act: a pop into the air
            timer.Update(Edge, Speed, false, Frame, Settings);

            // Assert
            Assert.That(timer.Side, Is.EqualTo(0));
        }

        [Test]
        public void Update_NextTurn_PlantsAgainOnTheOtherSide()
        {
            // Arrange: a left-to-right change, played out
            var timer = new PolePlantTimer();
            Hold(ref timer, -Edge, Speed, true, 0.5);
            Hold(ref timer, Edge, Speed, true, 1.0);

            // Act
            timer.Update(-Edge, Speed, true, Frame, Settings);

            // Assert
            Assert.That(timer.Side, Is.EqualTo(-1));
        }
    }
}
