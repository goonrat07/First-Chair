using System.Collections.Generic;
using FirstChair.Sim;
using NUnit.Framework;

namespace FirstChair.Sim.Tests
{
    public class SimInputTests
    {
        [Test]
        public void Pack_EveryButtonComboAndSteeringValue_RoundTrips()
        {
            // Arrange
            var failures = new List<string>();

            // Act
            for (uint combo = 0; combo < 64; combo++)
            {
                for (int steering = -SimInput.SteeringMax; steering <= SimInput.SteeringMax; steering++)
                {
                    SimInput input = SimInput.Pack((SimButtons)combo, steering);
                    if (input.Buttons != (SimButtons)combo || input.Steering != steering)
                    {
                        failures.Add(combo + "/" + steering + " -> " + input.Buttons + "/" + input.Steering);
                    }
                }
            }

            // Assert
            Assert.That(failures, Is.Empty);
        }

        [TestCase(128, 127)]
        [TestCase(1000, 127)]
        [TestCase(-128, -127)]
        [TestCase(-1000, -127)]
        public void Pack_SteeringOutOfRange_ClampsToPlusMinus127(int steering, int expected)
        {
            // Arrange and Act
            SimInput input = SimInput.Pack(SimButtons.None, steering);

            // Assert
            Assert.That(input.Steering, Is.EqualTo(expected));
        }

        [Test]
        public void Pack_ButtonsAndSteering_UsesDocumentedBitLayout()
        {
            // Arrange: Left (bit 0) + Grab (bit 5) = 0x21; steering -1 is 0xFF in bits 8-15.
            SimButtons buttons = SimButtons.Left | SimButtons.Grab;

            // Act
            SimInput input = SimInput.Pack(buttons, -1);

            // Assert
            Assert.That(input.Packed, Is.EqualTo(0x0000FF21u));
        }

        [Test]
        public void FromPacked_ReservedBitsSet_IgnoresThem()
        {
            // Arrange: bits 6-7 and 16-31 are reserved.
            const uint reservedOnly = 0xFFFF00C0u;

            // Act
            SimInput input = SimInput.FromPacked(reservedOnly);

            // Assert
            Assert.That(input.Buttons, Is.EqualTo(SimButtons.None));
            Assert.That(input.Steering, Is.EqualTo(0));
        }
    }
}
