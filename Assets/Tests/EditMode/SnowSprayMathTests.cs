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
        public void CarveRate_SoftOrSlowCarvesThrowNothing_HardFastOnesThrowByEdgeAndSpeed()
        {
            // Act: from 60% edge and 10 m/s, rising to 4 flakes a metre at full edge.
            float soft = SnowSprayMath.CarveRate(0.5, 20.0, true, 1f, 4f, 0.6f, 10f);
            float slow = SnowSprayMath.CarveRate(1.0, 8.0, true, 1f, 4f, 0.6f, 10f);
            float air = SnowSprayMath.CarveRate(1.0, 20.0, false, 1f, 4f, 0.6f, 10f);
            float half = SnowSprayMath.CarveRate(-0.8, 20.0, true, 1f, 4f, 0.6f, 10f);
            float full = SnowSprayMath.CarveRate(1.0, 20.0, true, 1f, 4f, 0.6f, 10f);
            float powder = SnowSprayMath.CarveRate(1.0, 20.0, true, 1.6f, 4f, 0.6f, 10f);

            // Assert
            Assert.That(soft, Is.EqualTo(0f));
            Assert.That(slow, Is.EqualTo(0f));
            Assert.That(air, Is.EqualTo(0f));
            Assert.That(half, Is.EqualTo(40f).Within(1e-3f), "half way from 0.6 to full edge, either side");
            Assert.That(full, Is.EqualTo(80f).Within(1e-3f));
            Assert.That(powder, Is.EqualTo(128f).Within(1e-3f));
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
