using FirstChair.Riding;
using FirstChair.Sim;
using NUnit.Framework;

namespace FirstChair.Riding.Tests
{
    /// <summary>When the skis touch snow, for the tracks, the trenches and the spray (Stories 008 and 018).</summary>
    public sealed class RiderContactTests
    {
        [Test]
        public void OnSnow_RidingTheSnow_IsTrue()
        {
            // Arrange
            var state = new RiderState();

            // Act and assert
            Assert.That(RiderContact.OnSnow(state, false), Is.True);
        }

        [Test]
        public void OnSnow_InTheAirCrashedOrOnTheLift_IsFalse()
        {
            // Arrange
            var air = new RiderState { Airborne = true };
            var crashed = new RiderState { Crashed = true };

            // Act and assert
            Assert.That(RiderContact.OnSnow(air, false), Is.False);
            Assert.That(RiderContact.OnSnow(crashed, false), Is.False);
            Assert.That(RiderContact.OnSnow(new RiderState(), true), Is.False);
        }

        [Test]
        public void OnSnow_GrindingARailOrBox_IsFalse()
        {
            // Arrange: on a feature the skis touch the rail, not the snow under it (Airborne is false meanwhile)
            var grinding = new RiderState { Grinding = true };

            // Act and assert: no track, trench or spray under a rail
            Assert.That(RiderContact.OnSnow(grinding, false), Is.False);
        }
    }
}
