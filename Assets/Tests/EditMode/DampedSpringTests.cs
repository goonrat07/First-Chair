using FirstChair.Riding;
using NUnit.Framework;

namespace FirstChair.Riding.Tests
{
    /// <summary>The damped spring behind the skier's secondary motion (Story 013): exact for any frame time.</summary>
    public sealed class DampedSpringTests
    {
        private const double Frequency = 2.5;   // Hz

        private static DampedSpring Run(double target, double frequency, double damping, int frames, double seconds)
        {
            var spring = new DampedSpring();
            double dt = seconds / frames;
            for (int i = 0; i < frames; i++)
            {
                spring.Step(target, frequency, damping, dt);
            }

            return spring;
        }

        [TestCase(0.3)]
        [TestCase(1.0)]
        [TestCase(2.0)]
        public void Step_HeldTarget_ConvergesOnIt(double damping)
        {
            // Act
            DampedSpring spring = Run(1.0, Frequency, damping, 600, 10.0);

            // Assert
            Assert.That(spring.Value, Is.EqualTo(1.0).Within(1e-4));
            Assert.That(spring.Velocity, Is.EqualTo(0.0).Within(1e-3));
        }

        [Test]
        public void Step_CriticallyDamped_NeverOvershoots()
        {
            // Arrange
            var spring = new DampedSpring();
            double highest = 0.0;

            // Act
            for (int i = 0; i < 600; i++)
            {
                spring.Step(1.0, Frequency, 1.0, 1.0 / 60);
                highest = System.Math.Max(highest, spring.Value);
            }

            // Assert
            Assert.That(highest, Is.LessThanOrEqualTo(1.0 + 1e-12));
        }

        [Test]
        public void Step_UnderDamped_Overshoots()
        {
            // Arrange
            var spring = new DampedSpring();
            double highest = 0.0;

            // Act
            for (int i = 0; i < 600; i++)
            {
                spring.Step(1.0, Frequency, 0.3, 1.0 / 60);
                highest = System.Math.Max(highest, spring.Value);
            }

            // Assert: a bouncy spring is what the low damping is for
            Assert.That(highest, Is.GreaterThan(1.1));
        }

        [TestCase(0.3)]
        [TestCase(1.0)]
        [TestCase(2.0)]
        public void Step_SameTimeAtAnyFrameRate_SameMotion(double damping)
        {
            // Act: the same half second at 30, 60 and 144 frames a second
            DampedSpring at30 = Run(1.0, Frequency, damping, 15, 0.5);
            DampedSpring at60 = Run(1.0, Frequency, damping, 30, 0.5);
            DampedSpring at144 = Run(1.0, Frequency, damping, 72, 0.5);

            // Assert
            Assert.That(at60.Value, Is.EqualTo(at30.Value).Within(1e-9));
            Assert.That(at144.Value, Is.EqualTo(at30.Value).Within(1e-9));
            Assert.That(at60.Velocity, Is.EqualTo(at30.Velocity).Within(1e-9));
            Assert.That(at144.Velocity, Is.EqualTo(at30.Velocity).Within(1e-9));
        }

        [Test]
        public void Step_ZeroOrBadFrameOrFrequency_ChangesNothing()
        {
            // Arrange
            var spring = new DampedSpring { Value = 0.2, Velocity = -1.0 };

            // Act
            spring.Step(1.0, Frequency, 1.0, 0.0);
            spring.Step(1.0, Frequency, 1.0, -0.1);
            spring.Step(1.0, Frequency, 1.0, double.NaN);
            spring.Step(1.0, 0.0, 1.0, 1.0 / 60);

            // Assert
            Assert.That(spring.Value, Is.EqualTo(0.2));
            Assert.That(spring.Velocity, Is.EqualTo(-1.0));
        }

        [Test]
        public void Step_KickedFromRest_SwingsOutAndBack()
        {
            // Arrange: a landing gives the hips a downward velocity; the target stays at rest
            var spring = new DampedSpring { Velocity = -1.0 };
            double lowest = 0.0;

            // Act
            for (int i = 0; i < 120; i++)
            {
                spring.Step(0.0, Frequency, 0.7, 1.0 / 60);
                lowest = System.Math.Min(lowest, spring.Value);
            }

            // Assert: it dipped (the exact deepest point is 2.9 cm: 1 m/s over w = 15.7 rad/s, damped by zeta 0.7),
            // then came back near rest
            Assert.That(lowest, Is.LessThan(-0.025));
            Assert.That(System.Math.Abs(spring.Value), Is.LessThan(0.01));
        }
    }
}
