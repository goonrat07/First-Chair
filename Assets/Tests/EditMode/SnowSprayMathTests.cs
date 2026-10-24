using FirstChair.Riding;
using FirstChair.Sim;
using NUnit.Framework;

namespace FirstChair.Riding.Tests
{
    /// <summary>
    /// Snow spray from the skis (Story 008, presentation): quick turns and skids throw snow, a clean carve throws none,
    /// so the two kinds of turn read apart.
    /// </summary>
    public class SnowSprayMathTests
    {
        private const float PerSlideMetre = 60f, PerBrakeMetre = 12f, MaxRate = 300f;

        [Test]
        public void Rate_CleanCarve_IsZero()
        {
            // Act
            float rate = SnowSprayMath.Rate(0.0, 15.0, false, true, 1f, PerSlideMetre, PerBrakeMetre, MaxRate);

            // Assert
            Assert.That(rate, Is.EqualTo(0f));
        }

        [Test]
        public void Rate_Skid_IsFlakesPerMetreOfSidewaysSlide_GrowingWithSlipAndSpeed()
        {
            // Act
            float small = SnowSprayMath.Rate(0.1, 10.0, false, true, 1f, PerSlideMetre, PerBrakeMetre, MaxRate);
            float wider = SnowSprayMath.Rate(0.2, 10.0, false, true, 1f, PerSlideMetre, PerBrakeMetre, MaxRate);
            float faster = SnowSprayMath.Rate(0.2, 20.0, false, true, 1f, PerSlideMetre, PerBrakeMetre, MaxRate);

            // Assert: the sideways slide is speed * sin(slip).
            Assert.That(small, Is.EqualTo(PerSlideMetre * 10f * (float)System.Math.Sin(0.1)).Within(1e-3f));
            Assert.That(wider, Is.GreaterThan(small));
            Assert.That(faster, Is.GreaterThan(wider));
        }

        [Test]
        public void Rate_Braking_AddsFlakesPerMetreTravelled()
        {
            // Act
            float braking = SnowSprayMath.Rate(0.0, 10.0, true, true, 1f, PerSlideMetre, PerBrakeMetre, MaxRate);

            // Assert
            Assert.That(braking, Is.EqualTo(PerBrakeMetre * 10f).Within(1e-4f));
        }

        [Test]
        public void Rate_OffTheSnow_IsZero()
        {
            // Act: airborne or crashed.
            float rate = SnowSprayMath.Rate(0.3, 15.0, true, false, 1f, PerSlideMetre, PerBrakeMetre, MaxRate);

            // Assert
            Assert.That(rate, Is.EqualTo(0f));
        }

        [Test]
        public void Rate_IsCappedAtTheMaxRate()
        {
            // Act: a full sideways slide at speed.
            float rate = SnowSprayMath.Rate(1.5, 30.0, true, true, 2f, PerSlideMetre, PerBrakeMetre, MaxRate);

            // Assert
            Assert.That(rate, Is.EqualTo(MaxRate));
        }

        [Test]
        public void SnowFactor_PowderSpraysMoreAndIceLess_TheRestOne()
        {
            // Act and Assert
            Assert.That(SnowSprayMath.SnowFactor(SnowType.Powder, 1.6f, 0.3f), Is.EqualTo(1.6f));
            Assert.That(SnowSprayMath.SnowFactor(SnowType.Ice, 1.6f, 0.3f), Is.EqualTo(0.3f));
            foreach (SnowType snow in new[] { SnowType.Groomed, SnowType.Crud, SnowType.Slush })
            {
                Assert.That(SnowSprayMath.SnowFactor(snow, 1.6f, 0.3f), Is.EqualTo(1f), snow.ToString());
            }
        }
    }
}
