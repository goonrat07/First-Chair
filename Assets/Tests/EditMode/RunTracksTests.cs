using System;
using System.Collections.Generic;
using FirstChair.Riding;
using FirstChair.Sim;
using NUnit.Framework;

namespace FirstChair.Riding.Tests
{
    /// <summary>Old ski tracks bent onto a marked run, so they follow it round its curves (Story 010).</summary>
    public sealed class RunTracksTests
    {
        private const ulong Seed = 7;
        private const double AreaPerTrack = 350.0;
        private const double EdgeMargin = 1.0;

        private static SlopeRun Run(int widthMm, params SlopePoint[] points)
        {
            var builder = new System.Text.StringBuilder("format 1\nspec 400000 400000 0 100000\nbackground crud\nstart 1000 1000\nrun r blue ");
            builder.Append(widthMm);
            foreach (SlopePoint point in points)
            {
                builder.Append(' ').Append(point.XMm).Append(' ').Append(point.ZMm);
            }

            return SlopeLayout.Parse(builder.Append('\n').ToString()).Runs[0];
        }

        [Test]
        public void Generate_StraightRun_KeepsEveryTrackWithinTheRunsWidth_AndAlongItsLength()
        {
            // Arrange: 30 m wide, straight down +Z from z = 20 to z = 320 at x = 100.
            SlopeRun run = Run(30_000, new SlopePoint(100_000, 20_000), new SlopePoint(100_000, 320_000));

            // Act
            List<OldTrackLine> lines = RunTracks.Generate(run, Seed, OldTrackSettings.Default, AreaPerTrack, EdgeMargin, null);

            // Assert
            Assert.That(lines.Count, Is.GreaterThan(0));
            foreach (OldTrackLine line in lines)
            {
                for (int i = 0; i < line.Count; i++)
                {
                    Assert.That(Math.Abs(line.X[i] - 100.0), Is.LessThanOrEqualTo(15.0 - EdgeMargin + 1e-6));
                    Assert.That(line.Z[i], Is.InRange(20.0 - 1e-6, 320.0 + 1e-6));
                }
            }
        }

        [Test]
        public void Generate_BentRun_KeepsEveryTrackWithinTheRun_RoundTheBend()
        {
            // Arrange: a 24 m wide run swinging east then back west.
            SlopeRun run = Run(24_000, new SlopePoint(100_000, 0), new SlopePoint(180_000, 120_000), new SlopePoint(120_000, 260_000));
            var xs = new List<double>();
            var zs = new List<double>();
            RunLine.Sample(run.Points, 0.5, xs, zs);

            // Act
            List<OldTrackLine> lines = RunTracks.Generate(run, Seed, OldTrackSettings.Default, AreaPerTrack, EdgeMargin, null);

            // Assert: every point is within half the width (less the margin) of the centre line, give or take the
            // sampling's chord error.
            Assert.That(lines.Count, Is.GreaterThan(0));
            foreach (OldTrackLine line in lines)
            {
                for (int i = 0; i < line.Count; i += 5)
                {
                    double nearest = double.MaxValue;
                    for (int k = 0; k < xs.Count; k++)
                    {
                        nearest = Math.Min(nearest, Math.Sqrt((xs[k] - line.X[i]) * (xs[k] - line.X[i]) + (zs[k] - line.Z[i]) * (zs[k] - line.Z[i])));
                    }

                    Assert.That(nearest, Is.LessThanOrEqualTo(12.0 - EdgeMargin + 0.5));
                }
            }
        }

        [Test]
        public void Generate_TwiceTheWidth_LaysAboutTwiceTheTracks_AndTheSameSeedTheSameTracks()
        {
            // Arrange
            SlopeRun narrow = Run(20_000, new SlopePoint(100_000, 0), new SlopePoint(100_000, 300_000));
            SlopeRun wide = Run(38_000, new SlopePoint(100_000, 0), new SlopePoint(100_000, 300_000));

            // Act
            int few = RunTracks.Generate(narrow, Seed, OldTrackSettings.Default, AreaPerTrack, EdgeMargin, null).Count;
            int many = RunTracks.Generate(wide, Seed, OldTrackSettings.Default, AreaPerTrack, EdgeMargin, null).Count;
            List<OldTrackLine> once = RunTracks.Generate(wide, Seed, OldTrackSettings.Default, AreaPerTrack, EdgeMargin, null);
            List<OldTrackLine> again = RunTracks.Generate(wide, Seed, OldTrackSettings.Default, AreaPerTrack, EdgeMargin, null);

            // Assert: lines come in pairs, one per ski, and some tracks are dropped short, so only roughly double.
            Assert.That(many, Is.GreaterThan(few * 3 / 2));
            Assert.That(again.Count, Is.EqualTo(once.Count));
            Assert.That(again[0].X, Is.EqualTo(once[0].X));
            Assert.That(again[0].Z, Is.EqualTo(once[0].Z));
        }

        [Test]
        public void Generate_RunNarrowerThanTheMargins_LaysNone()
        {
            // Arrange: 2 m wide, with a 1 m margin each side.
            SlopeRun run = Run(2_000, new SlopePoint(100_000, 0), new SlopePoint(100_000, 300_000));

            // Act and Assert
            Assert.That(RunTracks.Generate(run, Seed, OldTrackSettings.Default, AreaPerTrack, EdgeMargin, null), Is.Empty);
        }

        [Test]
        public void Generate_StraightTracksOnABentRun_StayParallelToTheCentreLine_EvenlySpaced()
        {
            // Arrange: tracks with no swing, so each ski's line is a constant distance across the run; and a wide run
            // that bends hard, so a line near its edge swings far round the centre line's turns.
            SlopeRun run = Run(40_000, new SlopePoint(100_000, 0), new SlopePoint(200_000, 90_000), new SlopePoint(110_000, 220_000));
            OldTrackSettings settings = OldTrackSettings.Default;
            settings.MinSwingDegrees = 0.0;
            settings.MaxSwingDegrees = 0.0;
            var xs = new List<double>();
            var zs = new List<double>();
            RunLine.Sample(run.Points, 0.1, xs, zs);

            // Act
            List<OldTrackLine> lines = RunTracks.Generate(run, Seed, settings, AreaPerTrack, EdgeMargin, null);

            // Assert: each line keeps its distance from the centre line, and its points stay evenly spaced: the spacing
            // changes only as the bend tightens, never in a jump where the side direction steps between the centre
            // line's samples.
            Assert.That(lines.Count, Is.GreaterThan(0));
            foreach (OldTrackLine line in lines)
            {
                double offset = Nearest(xs, zs, line.X[0], line.Z[0]);
                double previous = double.NaN;
                for (int i = 1; i < line.Count; i++)
                {
                    Assert.That(Nearest(xs, zs, line.X[i], line.Z[i]), Is.EqualTo(offset).Within(0.05), "distance across at point " + i);
                    double gap = Math.Sqrt((line.X[i] - line.X[i - 1]) * (line.X[i] - line.X[i - 1]) + (line.Z[i] - line.Z[i - 1]) * (line.Z[i] - line.Z[i - 1]));
                    if (!double.IsNaN(previous))
                    {
                        Assert.That(Math.Abs(gap - previous), Is.LessThan(0.05), "spacing at point " + i);
                    }

                    previous = gap;
                }
            }
        }

        [Test]
        public void Generate_Blocked_NoTrackEntersTheBlockedPlace()
        {
            // Arrange: a straight run with a 6 m disc blocked in its middle.
            SlopeRun run = Run(30_000, new SlopePoint(100_000, 0), new SlopePoint(100_000, 300_000));
            Func<double, double, bool> blocked = (x, z) => (x - 100.0) * (x - 100.0) + (z - 150.0) * (z - 150.0) < 36.0;

            // Act
            List<OldTrackLine> free = RunTracks.Generate(run, Seed, OldTrackSettings.Default, AreaPerTrack, EdgeMargin, null);
            List<OldTrackLine> kept = RunTracks.Generate(run, Seed, OldTrackSettings.Default, AreaPerTrack, EdgeMargin, blocked);

            // Assert: unblocked, some track crosses the disc, so the test can fail.
            Assert.That(CountInside(free, blocked), Is.GreaterThan(0));
            Assert.That(CountInside(kept, blocked), Is.EqualTo(0));
        }

        [TestCase(762.0, 470.0, true, TestName = "NearAFeature_OnAKickersRamp_True")]
        [TestCase(762.0, 467.0, true, TestName = "NearAFeature_JustAboveAKickersFoot_True")]
        [TestCase(762.0, 465.0, false, TestName = "NearAFeature_ClearAboveAKickersFoot_False")]
        [TestCase(762.0, 474.0, false, TestName = "NearAFeature_ClearBelowAKickersLip_False")]
        [TestCase(765.5, 470.0, true, TestName = "NearAFeature_BesideAKickerWithinTheMargin_True")]
        [TestCase(767.0, 470.0, false, TestName = "NearAFeature_BesideAKickerClearOfTheMargin_False")]
        [TestCase(784.0, 480.0, true, TestName = "NearAFeature_OnABox_True")]
        [TestCase(785.5, 480.0, true, TestName = "NearAFeature_BesideABoxWithinTheMargin_True")]
        [TestCase(786.0, 480.0, false, TestName = "NearAFeature_BesideABoxClearOfTheMargin_False")]
        [TestCase(784.0, 474.0, false, TestName = "NearAFeature_ClearAboveABoxsStart_False")]
        public void NearAFeature_PointsAroundAKickerAndABox(double x, double z, bool near)
        {
            // Arrange: a kicker with its lip at (762, 472) facing +Z, 4 m long and 5 m wide; a box 0.6 m wide from z 476 to 491.
            SlopeLayout layout = SlopeLayout.Parse(
                "format 1\nspec 1024000 1024000 0 400000\nbackground crud\nstart 1000 1000\n"
                + "kicker 762000 472000 0 4000 5000 800\nbox 784000 476000 784000 491000 300 600\n");

            // Act and Assert: a 1.2 m margin.
            Assert.That(RunTracks.NearAFeature(layout, x, z, 1.2), Is.EqualTo(near));
        }

        private static double Nearest(List<double> xs, List<double> zs, double x, double z)
        {
            double nearest = double.MaxValue;
            for (int k = 0; k < xs.Count; k++)
            {
                nearest = Math.Min(nearest, Math.Sqrt((xs[k] - x) * (xs[k] - x) + (zs[k] - z) * (zs[k] - z)));
            }

            return nearest;
        }

        private static int CountInside(List<OldTrackLine> lines, Func<double, double, bool> blocked)
        {
            int inside = 0;
            foreach (OldTrackLine line in lines)
            {
                for (int i = 0; i < line.Count; i++)
                {
                    inside += blocked(line.X[i], line.Z[i]) ? 1 : 0;
                }
            }

            return inside;
        }
    }
}
