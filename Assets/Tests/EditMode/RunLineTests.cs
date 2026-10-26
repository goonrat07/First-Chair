using System;
using System.Collections.Generic;
using FirstChair.Riding;
using FirstChair.Sim;
using NUnit.Framework;

namespace FirstChair.Riding.Tests
{
    /// <summary>A run's centre line: the spline the test mountain's generator draws through its points (Story 010).</summary>
    public sealed class RunLineTests
    {
        private const double Step = 2.0;
        private const double Tolerance = 1e-9;

        private static readonly SlopePoint[] Bend =
        {
            new SlopePoint(0, 0), new SlopePoint(20_000, 30_000), new SlopePoint(10_000, 60_000), new SlopePoint(40_000, 90_000),
        };

        [Test]
        public void Sample_PassesThroughEveryPoint_InOrder_StartingAtTheFirst()
        {
            // Arrange
            var xs = new List<double>();
            var zs = new List<double>();

            // Act
            RunLine.Sample(Bend, Step, xs, zs);

            // Assert
            Assert.That(xs[0], Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(zs[zs.Count - 1], Is.EqualTo(90.0).Within(Tolerance));
            int from = 0;
            foreach (SlopePoint point in Bend)
            {
                int found = -1;
                for (int i = from; i < xs.Count && found < 0; i++)
                {
                    if (Math.Abs(xs[i] - point.XMm / 1000.0) < Tolerance && Math.Abs(zs[i] - point.ZMm / 1000.0) < Tolerance)
                    {
                        found = i;
                    }
                }

                Assert.That(found, Is.GreaterThanOrEqualTo(0), "point (" + point.XMm + ", " + point.ZMm + ")");
                from = found;
            }
        }

        [TestCase(7.416, 9.425)]
        [TestCase(18.319, 38.283)]
        [TestCase(15.838, 69.5)]
        public void Sample_FollowsTheGeneratorsCentripetalSpline(double x, double z)
        {
            // Arrange: points on make_test_mountain.py's catmull_rom through the same bend, at 12, 45 and 80 m along it.
            var xs = new List<double>();
            var zs = new List<double>();

            // Act
            RunLine.Sample(Bend, 0.25, xs, zs);

            // Assert: the sampled line passes within 1 cm of each (distance to its segments, not its samples).
            double nearest = double.MaxValue;
            for (int i = 1; i < xs.Count; i++)
            {
                double dx = xs[i] - xs[i - 1], dz = zs[i] - zs[i - 1];
                double t = Math.Max(0.0, Math.Min(1.0, ((x - xs[i - 1]) * dx + (z - zs[i - 1]) * dz) / (dx * dx + dz * dz)));
                double ex = xs[i - 1] + t * dx - x, ez = zs[i - 1] + t * dz - z;
                nearest = Math.Min(nearest, Math.Sqrt(ex * ex + ez * ez));
            }

            Assert.That(nearest, Is.LessThan(0.01));
        }

        [Test]
        public void Sample_SpacesSamplesAboutAStepApart()
        {
            // Arrange
            var xs = new List<double>();
            var zs = new List<double>();

            // Act
            RunLine.Sample(Bend, Step, xs, zs);

            // Assert
            for (int i = 1; i < xs.Count; i++)
            {
                double gap = Math.Sqrt((xs[i] - xs[i - 1]) * (xs[i] - xs[i - 1]) + (zs[i] - zs[i - 1]) * (zs[i] - zs[i - 1]));
                Assert.That(gap, Is.GreaterThan(0.5 * Step).And.LessThan(2.0 * Step), "gap " + i);
            }
        }

        [Test]
        public void Sample_StraightRun_StaysOnTheLine()
        {
            // Arrange
            var xs = new List<double>();
            var zs = new List<double>();
            SlopePoint[] straight = { new SlopePoint(10_000, 0), new SlopePoint(10_000, 40_000), new SlopePoint(10_000, 100_000) };

            // Act
            RunLine.Sample(straight, Step, xs, zs);

            // Assert
            Assert.That(xs, Has.All.EqualTo(10.0).Within(1e-6));
            Assert.That(zs.Count, Is.GreaterThan(40));
        }

        [Test]
        public void Sample_OnePoint_Throws()
        {
            // Act and Assert
            Assert.That(() => RunLine.Sample(new[] { new SlopePoint(0, 0) }, Step, new List<double>(), new List<double>()), Throws.ArgumentException);
        }
    }
}
