using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace FirstChair.Riding.Tests
{
    /// <summary>
    /// The old ski tracks drawn on the test slope (story 003 decisions, 2026-10-24): deterministic, inside their
    /// rectangle, one line per ski at the ski spacing, curving both ways about the fall line, and stopping at anything
    /// blocked.
    /// </summary>
    public class OldTracksTests
    {
        private const double XMin = 40, ZMin = 0, XMax = 140, ZMax = 460;
        private const ulong Seed = 7;

        private static readonly OldTrackSettings Settings = OldTrackSettings.Default;

        [Test]
        public void SameSeed_GivesTheSameTracks_AndAnotherSeedDoesNot()
        {
            // Act
            List<OldTrackLine> first = OldTracks.Generate(Seed, XMin, ZMin, XMax, ZMax, Settings, null);
            List<OldTrackLine> again = OldTracks.Generate(Seed, XMin, ZMin, XMax, ZMax, Settings, null);
            List<OldTrackLine> other = OldTracks.Generate(Seed + 1, XMin, ZMin, XMax, ZMax, Settings, null);

            // Assert
            Assert.That(first.Count, Is.EqualTo(again.Count));
            for (int i = 0; i < first.Count; i++)
            {
                Assert.That(again[i].X, Is.EqualTo(first[i].X), "line " + i);
                Assert.That(again[i].Z, Is.EqualTo(first[i].Z), "line " + i);
            }

            Assert.That(other[0].X[0], Is.Not.EqualTo(first[0].X[0]));
        }

        [Test]
        public void EveryTrack_StaysInsideTheRectangle_AndIsLongEnough()
        {
            // Act
            List<OldTrackLine> lines = OldTracks.Generate(Seed, XMin, ZMin, XMax, ZMax, Settings, null);

            // Assert: most tracks are kept, every point is inside, and every line is at least the shortest length.
            Assert.That(lines.Count, Is.GreaterThan(Settings.Count));
            foreach (OldTrackLine line in lines)
            {
                for (int i = 0; i < line.Count; i++)
                {
                    Assert.That(line.X[i], Is.InRange(XMin, XMax));
                    Assert.That(line.Z[i], Is.InRange(ZMin, ZMax));
                }

                Assert.That((line.Count - 1) * Settings.StepMetres, Is.GreaterThanOrEqualTo(Settings.MinLengthMetres));
            }
        }

        [Test]
        public void EachTrack_IsTwoLinesTheSkiSpacingApart_StepMetresBetweenPoints()
        {
            // Act
            List<OldTrackLine> lines = OldTracks.Generate(Seed, XMin, ZMin, XMax, ZMax, Settings, null);

            // Assert
            Assert.That(lines.Count % 2, Is.Zero);
            for (int track = 0; track < lines.Count; track += 2)
            {
                OldTrackLine left = lines[track], right = lines[track + 1];
                Assert.That(right.Count, Is.EqualTo(left.Count));
                for (int i = 0; i < left.Count; i++)
                {
                    double apart = Math.Sqrt(Square(right.X[i] - left.X[i]) + Square(right.Z[i] - left.Z[i]));
                    Assert.That(apart, Is.EqualTo(Settings.SkiSpacingMetres).Within(1e-9), "track " + track / 2 + ", point " + i);
                }

                double centreStep = Math.Sqrt(
                    Square((left.X[1] + right.X[1] - left.X[0] - right.X[0]) / 2) + Square((left.Z[1] + right.Z[1] - left.Z[0] - right.Z[0]) / 2));
                Assert.That(centreStep, Is.EqualTo(Settings.StepMetres).Within(1e-9));
            }
        }

        [Test]
        public void Tracks_RunDownhill_AndTurnBothWays()
        {
            // Act
            List<OldTrackLine> lines = OldTracks.Generate(Seed, XMin, ZMin, XMax, ZMax, Settings, null);
            int toPlusX = 0, toMinusX = 0;
            foreach (OldTrackLine line in lines)
            {
                for (int i = 1; i < line.Count; i++)
                {
                    // Assert: always down the fall line (+Z), never back up.
                    Assert.That(line.Z[i], Is.GreaterThan(line.Z[i - 1]));
                    toPlusX += line.X[i] > line.X[i - 1] ? 1 : 0;
                    toMinusX += line.X[i] < line.X[i - 1] ? 1 : 0;
                }
            }

            // Assert: S-turns, so plenty of steps each way across the slope.
            Assert.That(toPlusX, Is.GreaterThan(toMinusX / 3));
            Assert.That(toMinusX, Is.GreaterThan(toPlusX / 3));
        }

        [Test]
        public void BlockedBand_NoTrackPointEntersIt()
        {
            // Arrange: a band across the whole slope, as a row of trees would be.
            Func<double, double, bool> band = (x, z) => z > 200 && z < 210;

            // Act
            List<OldTrackLine> lines = OldTracks.Generate(Seed, XMin, ZMin, XMax, ZMax, Settings, band);

            // Assert
            Assert.That(lines, Is.Not.Empty);
            foreach (OldTrackLine line in lines)
            {
                for (int i = 0; i < line.Count; i++)
                {
                    Assert.That(band(line.X[i], line.Z[i]), Is.False);
                }
            }
        }

        [Test]
        public void EmptyRectangle_Throws()
        {
            // Act and Assert
            Assert.That(() => OldTracks.Generate(Seed, 10, 0, 10, 100, Settings, null), Throws.ArgumentException);
        }

        private static double Square(double value) => value * value;
    }
}
