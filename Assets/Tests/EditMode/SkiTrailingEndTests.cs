using FirstChair.Riding;
using NUnit.Framework;

namespace FirstChair.Riding.Tests
{
    /// <summary>Which end of a ski its track starts from: the tail riding forward, the tip riding switch.</summary>
    public sealed class SkiTrailingEndTests
    {
        private const double Threshold = 0.5;   // cosine: within 60 degrees of the ski's line
        private const double Speed = 5.0;       // m/s

        private static readonly PoseVector TipDirection = new PoseVector(0.0, 0.0, 1.0);

        [Test]
        public void Update_RidingForward_TailTrails()
        {
            // Arrange
            var end = new SkiTrailingEnd();

            // Act
            bool changed = end.Update(TipDirection * Speed, TipDirection, Threshold);

            // Assert
            Assert.That(end.TipTrails, Is.False);
            Assert.That(changed, Is.False);
        }

        [Test]
        public void Update_RidingSwitch_TipTrailsAndReportsTheChangeOnce()
        {
            // Arrange
            var end = new SkiTrailingEnd();

            // Act
            bool first = end.Update(TipDirection * -Speed, TipDirection, Threshold);
            bool second = end.Update(TipDirection * -Speed, TipDirection, Threshold);

            // Assert
            Assert.That(end.TipTrails, Is.True);
            Assert.That(first, Is.True);
            Assert.That(second, Is.False);
        }

        [Test]
        public void Update_SlidingSideways_KeepsTheTrailingEnd()
        {
            // Arrange: riding switch, then a skid square to the ski and one 70 degrees off its line, backwards
            var end = new SkiTrailingEnd();
            end.Update(TipDirection * -Speed, TipDirection, Threshold);
            var sideways = new PoseVector(Speed, 0.0, 0.0);
            var steep = new PoseVector(Speed * 0.94, 0.0, Speed * 0.34);

            // Act
            bool skid = end.Update(sideways, TipDirection, Threshold);
            bool steepSkid = end.Update(steep, TipDirection, Threshold);

            // Assert: neither is past the threshold toward the tip, so the tip still trails
            Assert.That(end.TipTrails, Is.True);
            Assert.That(skid || steepSkid, Is.False);
        }

        [Test]
        public void Update_Standing_KeepsTheTrailingEnd()
        {
            // Arrange
            var end = new SkiTrailingEnd();
            end.Update(TipDirection * -Speed, TipDirection, Threshold);

            // Act: creeping forward at a few centimetres a second
            bool changed = end.Update(TipDirection * 0.05, TipDirection, Threshold);

            // Assert
            Assert.That(end.TipTrails, Is.True);
            Assert.That(changed, Is.False);
        }

        [Test]
        public void Update_SwitchThenForward_TailTrailsAgain()
        {
            // Arrange
            var end = new SkiTrailingEnd();
            end.Update(TipDirection * -Speed, TipDirection, Threshold);

            // Act
            bool changed = end.Update(TipDirection * Speed, TipDirection, Threshold);

            // Assert
            Assert.That(end.TipTrails, Is.False);
            Assert.That(changed, Is.True);
        }

        [Test]
        public void Update_TipDirectionNotUnitLength_JudgesByAngleOnly()
        {
            // Arrange: a tip direction twice as long, and a step 50 degrees off the line toward the tail
            var end = new SkiTrailingEnd();
            var longTip = TipDirection * 2.0;
            var backwards = new PoseVector(Speed * 0.766, 0.0, -Speed * 0.643);

            // Act
            bool changed = end.Update(backwards, longTip, Threshold);

            // Assert: cos 50 degrees is 0.64, past the 0.5 threshold
            Assert.That(end.TipTrails, Is.True);
            Assert.That(changed, Is.True);
        }

        [Test]
        public void Offset_EachEnd_IsTheDistanceAlongTheSkiToThatEnd()
        {
            // Arrange
            var forward = new SkiTrailingEnd();
            var backward = new SkiTrailingEnd();
            backward.Update(TipDirection * -Speed, TipDirection, Threshold);

            // Act
            double tail = forward.Offset(tailBehind: 0.804, tipAhead: 0.964);
            double tip = backward.Offset(tailBehind: 0.804, tipAhead: 0.964);

            // Assert: along the tip direction from the boot, back to the tail or out to the tip
            Assert.That(tail, Is.EqualTo(-0.804).Within(1e-12));
            Assert.That(tip, Is.EqualTo(0.964).Within(1e-12));
        }
    }
}
