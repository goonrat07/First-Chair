using System;
using FirstChair.Sim;
using NUnit.Framework;

namespace FirstChair.Sim.Tests
{
    /// <summary>
    /// Story 010: the generated test mountain in Assets/Terrain/TestMountain (built by tools/mountain/mountain.py from
    /// design/mountains/test-mountain.mountain.toml) loads through the sim's loaders, and a scripted rider can ride each
    /// run's centre line from its top to the base area without crashing. These tests read the committed files, so the
    /// heightmap and snow map must be pulled from Git LFS.
    /// </summary>
    public class TestMountainTests
    {
        private const string Mountain = "TestMountain";

        [Test]
        public void CommittedFiles_LoadThroughTheSimLoaders_WithRunsALiftAndForest()
        {
            // Act
            (SlopeLayout layout, Heightmap heightmap, SnowMap snowMap) = MountainRider.Load(Mountain);

            // Assert
            Assert.That(heightmap.Resolution, Is.EqualTo(2049));
            Assert.That(snowMap, Is.Not.Null);
            Assert.That(layout.Runs.Count, Is.EqualTo(5));
            Assert.That(layout.Lifts.Count, Is.EqualTo(1));
            Assert.That(layout.Lifts[0].Towers.Count, Is.GreaterThanOrEqualTo(5));
            Assert.That(layout.Trees.Count, Is.GreaterThan(2000));
            Assert.That(new Forest(layout.Trees).Count, Is.EqualTo(layout.Trees.Count));
            Assert.That(new RailSet(layout.Rails).Count, Is.EqualTo(layout.Rails.Count));
            Assert.That(layout.TryGetZone("base", out _), Is.True);
            Assert.That(layout.TryGetZone("summit", out SlopeZone summit), Is.True);
            Assert.That(summit.Contains(layout.StartX, layout.StartZ), Is.True);
        }

        [Test]
        public void TheRider_StartsBelowTheTopStation_FacingDownTheFallLine()
        {
            // Arrange
            (SlopeLayout layout, Heightmap heightmap, SnowMap snowMap) = MountainRider.Load(Mountain);
            var setup = new RunSetup(Sport.Skis, 0, 7UL, layout.StartX, layout.StartZ);

            // Act
            var simulation = new Simulation(setup, heightmap, snowMap, new Forest(layout.Trees), new RailSet(layout.Rails), Tuning.Default);

            // Assert: the sim faces the rider down the snow's slope at the start, and the mountain falls along +Z
            // (heading 0), so a rider who just lets go rides down the middle of the mountain.
            Assert.That(Math.Abs(TestTerrain.ToDouble(simulation.State.Heading)) * 180.0 / Math.PI, Is.LessThanOrEqualTo(10.0));
        }

        [TestCase("meadow-way")]
        [TestCase("kettle-run")]
        [TestCase("ridge-run")]
        [TestCase("the-chute")]
        [TestCase("kettle-park")]
        public void EachRun_RiddenDownItsCentreLine_ReachesTheBaseWithoutCrashing(string runName)
        {
            // Arrange
            (SlopeLayout layout, Heightmap heightmap, SnowMap snowMap) = MountainRider.Load(Mountain);
            SlopeRun run = MountainRider.FindRun(layout, runName);
            Assert.That(layout.TryGetZone("base", out SlopeZone baseArea), Is.True);

            // Act
            MountainRider.RideResult ride = MountainRider.RideCentreLine(layout, heightmap, snowMap, run, baseArea);

            // Assert
            TestContext.Out.WriteLine(
                $"{runName}: {ride.Seconds:F0} s, mean {ride.MeanSpeed * MountainRider.MetresPerSecondToKmh:F0} km/h, " +
                $"top {ride.TopSpeed * MountainRider.MetresPerSecondToKmh:F0} km/h, furthest off the line {ride.FurthestOff:F1} m");
            Assert.That(ride.Crashed, Is.False, runName + " crashed at " + ride.Where);
            Assert.That(ride.ReachedBase, Is.True, runName + " never reached the base area; it stopped near " + ride.Where);
            Assert.That(ride.FurthestOff, Is.LessThanOrEqualTo(TestTerrain.ToDouble(run.Width) / 2.0 + MountainRider.OffRunAllowance), runName + " left its run");
        }

        [Test]
        public void TheGreenRun_IsRiddenSlowerThanTheBlue()
        {
            // Arrange
            (SlopeLayout layout, Heightmap heightmap, SnowMap snowMap) = MountainRider.Load(Mountain);
            Assert.That(layout.TryGetZone("base", out SlopeZone baseArea), Is.True);

            // Act
            MountainRider.RideResult green = MountainRider.RideCentreLine(layout, heightmap, snowMap, MountainRider.FindRun(layout, "meadow-way"), baseArea);
            MountainRider.RideResult blue = MountainRider.RideCentreLine(layout, heightmap, snowMap, MountainRider.FindRun(layout, "kettle-run"), baseArea);

            // Assert
            Assert.That(green.MeanSpeed, Is.LessThan(blue.MeanSpeed));
        }
    }
}
