using System.Collections.Generic;
using FirstChair.Sim;
using NUnit.Framework;

namespace FirstChair.Sim.Tests
{
    /// <summary>
    /// Story 015: every mountain the mountain tool built into Assets/Terrain/ (the test mountain aside: it has its own
    /// tests) loads through the sim's loaders, starts its rider on its summit, and a scripted rider can ride each of its
    /// runs from the top to the base area without crashing or leaving the run. A mountain made from a file is checked
    /// here with no test written for it.
    /// </summary>
    public class EveryMountainTests
    {
        private const string TestMountain = "TestMountain";

        /// <summary>Each mountain but the test mountain.</summary>
        public static IEnumerable<string> Mountains()
        {
            foreach (string mountain in MountainRider.Mountains())
            {
                if (mountain != TestMountain)
                {
                    yield return mountain;
                }
            }
        }

        /// <summary>Each run of each mountain but the test mountain, as (mountain, run).</summary>
        public static IEnumerable<TestCaseData> Runs()
        {
            foreach (string mountain in Mountains())
            {
                (SlopeLayout layout, _, _) = MountainRider.Load(mountain);
                foreach (SlopeRun run in layout.Runs)
                {
                    yield return new TestCaseData(mountain, run.Name).SetName($"{mountain} {run.Name}");
                }
            }
        }

        [TestCaseSource(nameof(Mountains))]
        public void Mountain_Loads_WithItsStartOnTheSummitAndABaseArea(string mountain)
        {
            // Act
            (SlopeLayout layout, Heightmap heightmap, SnowMap snowMap) = MountainRider.Load(mountain);

            // Assert
            Assert.That(snowMap, Is.Not.Null);
            Assert.That(layout.Runs.Count, Is.GreaterThan(0), mountain + " has no runs");
            Assert.That(layout.TryGetZone("base", out _), Is.True);
            Assert.That(layout.TryGetZone("summit", out SlopeZone summit), Is.True);
            Assert.That(summit.Contains(layout.StartX, layout.StartZ), Is.True, mountain + "'s start is off its summit");
            Assert.That(new Forest(layout.Trees).Count, Is.EqualTo(layout.Trees.Count));
            Assert.That(heightmap.Resolution, Is.GreaterThan(32));
        }

        [TestCaseSource(nameof(Runs))]
        public void Run_RiddenDownItsCentreLine_ReachesTheBaseWithoutCrashing(string mountain, string runName)
        {
            // Arrange
            (SlopeLayout layout, Heightmap heightmap, SnowMap snowMap) = MountainRider.Load(mountain);
            SlopeRun run = MountainRider.FindRun(layout, runName);
            Assert.That(layout.TryGetZone("base", out SlopeZone baseArea), Is.True);

            // Act
            MountainRider.RideResult ride = MountainRider.RideCentreLine(layout, heightmap, snowMap, run, baseArea);

            // Assert
            TestContext.Out.WriteLine(
                $"{mountain} {runName}: {ride.Seconds:F0} s, mean {ride.MeanSpeed * MountainRider.MetresPerSecondToKmh:F0} km/h, " +
                $"top {ride.TopSpeed * MountainRider.MetresPerSecondToKmh:F0} km/h, furthest off the line {ride.FurthestOff:F1} m");
            Assert.That(ride.Crashed, Is.False, runName + " crashed at " + ride.Where);
            Assert.That(ride.ReachedBase, Is.True, runName + " never reached the base area; it stopped near " + ride.Where);
            Assert.That(ride.FurthestOff, Is.LessThanOrEqualTo(TestTerrain.ToDouble(run.Width) / 2.0 + MountainRider.OffRunAllowance), runName + " left its run");
        }
    }
}
