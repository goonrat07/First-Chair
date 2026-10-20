using System;
using FirstChair.Sim;
using NUnit.Framework;

namespace FirstChair.Sim.Tests
{
    /// <summary>
    /// Fix64's integer-only trig against System.Math (test code only; the sim never uses floating point), and
    /// Vec3.Cross.
    /// </summary>
    public class Fix64TrigTests
    {
        private const double Tolerance = 1e-8;
        private const double RawPerUnit = 4294967296.0;

        [Test]
        public void PiConstants_AreCorrectlyRounded()
        {
            // Arrange and Act
            double pi = Fix64.Pi.Raw, halfPi = Fix64.HalfPi.Raw, twoPi = Fix64.TwoPi.Raw;

            // Assert
            Assert.That(Math.Abs(pi - Math.PI * RawPerUnit), Is.LessThanOrEqualTo(0.5));
            Assert.That(Math.Abs(halfPi - Math.PI / 2 * RawPerUnit), Is.LessThanOrEqualTo(0.5));
            Assert.That(Math.Abs(twoPi - Math.PI * 2 * RawPerUnit), Is.LessThanOrEqualTo(0.5));
        }

        [Test]
        public void SinAndCos_AnglesAcrossTwoTurns_MatchMathWithinTolerance()
        {
            // Arrange
            double worst = 0;

            // Act: about -6.29 to 6.29 rad in uneven steps.
            for (int i = -20000; i <= 20000; i++)
            {
                long raw = i * 1_350_000L + 12_345L;
                double angle = raw / RawPerUnit;
                Fix64 x = Fix64.FromRaw(raw);
                worst = Math.Max(worst, Math.Abs(TestTerrain.ToDouble(Fix64.Sin(x)) - Math.Sin(angle)));
                worst = Math.Max(worst, Math.Abs(TestTerrain.ToDouble(Fix64.Cos(x)) - Math.Cos(angle)));
            }

            // Assert
            Assert.That(worst, Is.LessThan(Tolerance));
        }

        [Test]
        public void Atan2_VectorsInEveryOctantAndScale_MatchMathWithinTolerance()
        {
            // Arrange: a grid about +-5 across, and one about +-1e-4 across.
            double worst = 0;

            // Act
            foreach (long step in new[] { 357_913_941L, 3_579L })
            {
                for (int i = -60; i <= 60; i++)
                {
                    for (int j = -60; j <= 60; j++)
                    {
                        long yRaw = i * step + 7, xRaw = j * step - 3;
                        double actual = TestTerrain.ToDouble(Fix64.Atan2(Fix64.FromRaw(yRaw), Fix64.FromRaw(xRaw)));
                        worst = Math.Max(worst, Math.Abs(actual - Math.Atan2(yRaw / RawPerUnit, xRaw / RawPerUnit)));
                    }
                }
            }

            // Assert
            Assert.That(worst, Is.LessThan(Tolerance));
        }

        [Test]
        public void Atan2_OnTheAxes_ReturnsExactAngles()
        {
            // Arrange
            Fix64 one = Fix64.One;

            // Act and Assert
            Assert.That(Fix64.Atan2(Fix64.Zero, Fix64.Zero), Is.EqualTo(Fix64.Zero));
            Assert.That(Fix64.Atan2(Fix64.Zero, one), Is.EqualTo(Fix64.Zero));
            Assert.That(Fix64.Atan2(Fix64.Zero, -one), Is.EqualTo(Fix64.Pi));
            Assert.That(Fix64.Atan2(one, Fix64.Zero), Is.EqualTo(Fix64.HalfPi));
            Assert.That(Fix64.Atan2(-one, Fix64.Zero), Is.EqualTo(-Fix64.HalfPi));
        }

        [TestCase(0.5, 0.5)]
        [TestCase(-3.0, -3.0)]
        [TestCase(7.0, 7.0 - 2 * Math.PI)]
        [TestCase(-7.0, -7.0 + 2 * Math.PI)]
        [TestCase(20.0, 20.0 - 6 * Math.PI)]
        public void WrapAngle_AnyAngle_LandsInHalfOpenPiRange(double angle, double expected)
        {
            // Arrange
            Fix64 value = Fix64.FromRaw((long)Math.Round(angle * RawPerUnit));

            // Act
            Fix64 wrapped = Fix64.WrapAngle(value);

            // Assert
            Assert.That(TestTerrain.ToDouble(wrapped), Is.EqualTo(expected).Within(Tolerance));
            Assert.That(wrapped.Raw, Is.GreaterThan((-Fix64.Pi).Raw).And.LessThanOrEqualTo(Fix64.Pi.Raw));
        }

        [Test]
        public void WrapAngle_Pi_StaysPi()
        {
            // Arrange and Act
            Fix64 wrapped = Fix64.WrapAngle(Fix64.Pi);

            // Assert
            Assert.That(wrapped, Is.EqualTo(Fix64.Pi));
        }

        [Test]
        public void Cross_UpAndForward_PointsRightAndAnticommutes()
        {
            // Arrange
            Fix64 zero = Fix64.Zero, one = Fix64.One;
            var up = new Vec3(zero, one, zero);
            var forward = new Vec3(zero, zero, one);
            var a = new Vec3(Fix64.FromInt(1), Fix64.FromInt(2), Fix64.FromInt(3));
            var b = new Vec3(Fix64.FromInt(4), Fix64.FromInt(5), Fix64.FromInt(6));

            // Act and Assert
            Assert.That(Vec3.Cross(up, forward), Is.EqualTo(new Vec3(one, zero, zero)));
            Assert.That(Vec3.Cross(forward, up), Is.EqualTo(new Vec3(-one, zero, zero)));
            Assert.That(Vec3.Cross(a, b), Is.EqualTo(new Vec3(Fix64.FromInt(-3), Fix64.FromInt(6), Fix64.FromInt(-3))));
        }
    }
}
