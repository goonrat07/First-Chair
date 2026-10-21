using FirstChair.Sim;
using NUnit.Framework;

namespace FirstChair.Riding.Tests
{
    /// <summary>Gamepad steering is quantized into the packed sim input before the sim sees it.</summary>
    public class RiderInputQuantizerTests
    {
        private const float Deadzone = 0.15f;

        [TestCase(0f)]
        [TestCase(0.1f)]
        [TestCase(-0.15f)]
        public void StickInsideDeadzone_QuantizesToZero(float axis)
        {
            Assert.That(RiderInputQuantizer.QuantizeSteering(axis, Deadzone), Is.EqualTo(0));
        }

        [TestCase(1f, SimInput.SteeringMax)]
        [TestCase(-1f, -SimInput.SteeringMax)]
        [TestCase(1.5f, SimInput.SteeringMax)]
        [TestCase(float.NaN, 0)]
        public void StickAtOrBeyondTheEnds_QuantizesToTheLimits(float axis, int expected)
        {
            Assert.That(RiderInputQuantizer.QuantizeSteering(axis, Deadzone), Is.EqualTo(expected));
        }

        [Test]
        public void JustOutsideDeadzone_QuantizesToOneStep()
        {
            Assert.That(RiderInputQuantizer.QuantizeSteering(Deadzone + 1e-4f, Deadzone), Is.EqualTo(1));
        }

        [Test]
        public void QuantizedSteering_SurvivesPacking()
        {
            // Arrange
            var quantizer = new RiderInputQuantizer(RiderInputQuantizerSettings.Default);
            var raw = new RawRiderInput { SteerAxis = -0.6f };

            // Act
            SimInput packed = quantizer.Pack(raw);

            // Assert
            Assert.That(packed.Steering, Is.EqualTo(RiderInputQuantizer.QuantizeSteering(-0.6f, RiderInputQuantizerSettings.Default.SteeringDeadzone)));
            Assert.That(packed.Steering, Is.LessThan(0));
        }

        [Test]
        public void KeyboardSteerLeft_IsFullLockThroughTheLeftButton()
        {
            // Arrange
            var quantizer = new RiderInputQuantizer(RiderInputQuantizerSettings.Default);

            // Act
            SimInput packed = quantizer.Pack(new RawRiderInput { SteerLeft = true });

            // Assert
            Assert.That(packed.Buttons, Is.EqualTo(SimButtons.Left));
            Assert.That(packed.CombinedSteering, Is.EqualTo(-SimInput.SteeringMax));
        }

        [Test]
        public void TriggerPastThreshold_PressesForwardAndBelowDoesNot()
        {
            // Arrange
            var quantizer = new RiderInputQuantizer(RiderInputQuantizerSettings.Default);

            // Act
            SimInput pressed = quantizer.Pack(new RawRiderInput { TuckAxis = 0.6f });
            SimInput released = quantizer.Pack(new RawRiderInput { TuckAxis = 0.3f });

            // Assert
            Assert.That(pressed.Buttons, Is.EqualTo(SimButtons.Forward));
            Assert.That(released.Buttons, Is.EqualTo(SimButtons.None));
        }
    }
}
