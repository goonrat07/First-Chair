using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using FirstChair.Sim;
using NUnit.Framework;

namespace FirstChair.Sim.Tests
{
    /// <summary>
    /// The mandatory determinism tests (.claude/rules/sim-code.md): record a ride, replay it from its header
    /// and inputs, and compare every step bit for bit; refuse recordings from another version, tuning or map.
    /// </summary>
    /// <remarks>
    /// In Story 001 the inputs do not move the rider yet (that is Story 002), so these tests prove the
    /// recording and replay path and the absence of hidden state, not that inputs change the ride. The
    /// last input is part of <see cref="RiderState"/>, so a replay fed different inputs still cannot match.
    /// </remarks>
    public class DeterminismTests
    {
        // The SHA-256 of the scripted ride's trace (ScriptedRide.TraceBytes), and the sim version it was
        // taken at. If riding behaviour changes on purpose: bump Simulation.Version, then re-pin both.
        private const ushort GoldenSimVersion = 1;
        private const string GoldenTraceSha256 = "ADDC775BE3DCFF9FE6AD754BBB10E357C17C2F664DB4C11955617C9A9BB73F2A";

        [Test]
        public void ScriptedRide_RecordedThenReplayedFromBytes_MatchesBitForBitEveryStep()
        {
            // Arrange: ride 12 s live while recording, then serialize the recording to bytes.
            var live = new List<StepSnapshot>();
            Recording recording = ScriptedRide.Record(TestTerrain.InclineDownhillPlusZ(), Tuning.Default, ScriptedRide.Steps, live);
            byte[] bytes = recording.ToBytes();

            // Act: read the bytes back and replay them on a freshly loaded copy of the same map.
            bool read = Recording.TryRead(bytes, out Recording loaded);
            var replayed = new List<StepSnapshot>();
            ReplayResult result = Replayer.Replay(loaded, TestTerrain.InclineDownhillPlusZ(), Tuning.Default, simulation => replayed.Add(StepSnapshot.Of(simulation)));

            // Assert
            Assert.That(read, Is.True);
            Assert.That(result.Status, Is.EqualTo(ReplayStatus.Ok));
            Assert.That(live.Count, Is.GreaterThanOrEqualTo(10 * Simulation.StepsPerSecond), "the ride must last at least 10 s");
            Assert.That(replayed.Count, Is.EqualTo(live.Count));
            Assert.That(CountEvents(live), Is.EqualTo(1), "the ride must leave the map once, so the event lists are really compared");
            for (int i = 0; i < live.Count; i++)
            {
                if (!live[i].Equals(replayed[i]))
                {
                    Assert.Fail("First divergence at step " + (i + 1) + ":\n live   " + live[i] + "\n replay " + replayed[i]);
                }
            }

            Assert.That(result.FinalState, Is.EqualTo(live[live.Count - 1].State));
        }

        [Test]
        public void ScriptedRide_TraceHash_MatchesGolden()
        {
            // Arrange
            var trace = new List<StepSnapshot>();
            ScriptedRide.Record(TestTerrain.InclineDownhillPlusZ(), Tuning.Default, ScriptedRide.Steps, trace);

            // Act
            string hash = Convert.ToHexString(SHA256.HashData(ScriptedRide.TraceBytes(trace)));

            // Assert
            Assert.That(Simulation.Version, Is.EqualTo(GoldenSimVersion),
                "Simulation.Version changed: re-pin GoldenTraceSha256 and GoldenSimVersion for the new version.");
            Assert.That(hash, Is.EqualTo(GoldenTraceSha256),
                "The ride changed without a version bump. If riding behaviour changed on purpose, bump Simulation.Version and re-pin the golden.");
        }

        [Test]
        public void Replay_HeaderWithDifferentSimVersion_ReportsVersionMismatchAndRunsNoSteps()
        {
            // Arrange: patch the sim version field (offset 6, little-endian) of a real recording.
            Heightmap heightmap = TestTerrain.InclineDownhillPlusZ();
            byte[] bytes = ScriptedRide.Record(heightmap, Tuning.Default, 120, null).ToBytes();
            var otherVersion = (ushort)(Simulation.Version + 1);
            bytes[6] = (byte)otherVersion;
            bytes[7] = (byte)(otherVersion >> 8);
            Recording.TryRead(bytes, out Recording recording);
            int stepsSeen = 0;

            // Act
            ReplayResult result = Replayer.Replay(recording, heightmap, Tuning.Default, _ => stepsSeen++);

            // Assert
            Assert.That(recording.Setup.SimVersion, Is.EqualTo(otherVersion));
            Assert.That(result.Status, Is.EqualTo(ReplayStatus.SimVersionMismatch));
            Assert.That(result.StepsRun, Is.EqualTo(0));
            Assert.That(stepsSeen, Is.EqualTo(0));
        }

        [Test]
        public void Replay_WithDifferentTuning_ReportsTuningMismatchAndRunsNoSteps()
        {
            // Arrange
            Heightmap heightmap = TestTerrain.InclineDownhillPlusZ();
            Recording recording = ScriptedRide.Record(heightmap, Tuning.Default, 120, null);
            Tuning other = Tuning.Default;
            other.SlideDrag = Fix64.FromRatio(1, 5);
            int stepsSeen = 0;

            // Act
            ReplayResult result = Replayer.Replay(recording, heightmap, other, _ => stepsSeen++);

            // Assert
            Assert.That(result.Status, Is.EqualTo(ReplayStatus.TuningMismatch));
            Assert.That(result.StepsRun, Is.EqualTo(0));
            Assert.That(stepsSeen, Is.EqualTo(0));
        }

        [Test]
        public void Replay_OnDifferentHeightmap_ReportsHeightmapMismatchAndRunsNoSteps()
        {
            // Arrange
            Recording recording = ScriptedRide.Record(TestTerrain.InclineDownhillPlusZ(), Tuning.Default, 120, null);
            int stepsSeen = 0;

            // Act
            ReplayResult result = Replayer.Replay(recording, TestTerrain.InclineDownhillMinusZ(), Tuning.Default, _ => stepsSeen++);

            // Assert
            Assert.That(result.Status, Is.EqualTo(ReplayStatus.HeightmapMismatch));
            Assert.That(result.StepsRun, Is.EqualTo(0));
            Assert.That(stepsSeen, Is.EqualTo(0));
        }

        [Test]
        public void Step_InterleavedWithAnotherSimulation_GivesSameStatesAsASoloRun()
        {
            // Arrange: a solo run of ride A, then A again stepped in lockstep with an unrelated ride B.
            Heightmap heightmap = TestTerrain.InclineDownhillPlusZ();
            var solo = new List<RiderState>();
            var soloRide = new Simulation(ScriptedRide.Setup, heightmap, Tuning.Default);
            for (int step = 0; step < ScriptedRide.Steps; step++)
            {
                soloRide.Step(ScriptedRide.InputAt(step));
                solo.Add(soloRide.State);
            }

            var rideA = new Simulation(ScriptedRide.Setup, heightmap, Tuning.Default);
            var setupB = new RunSetup(Sport.Skis, 2, 99UL, Fix64.FromInt(20), Fix64.FromInt(100));
            var rideB = new Simulation(setupB, TestTerrain.InclineDownhillMinusZ(), Tuning.Default);

            // Act and Assert
            for (int step = 0; step < ScriptedRide.Steps; step++)
            {
                rideB.Step(ScriptedRide.InputAt(step + 17));
                rideA.Step(ScriptedRide.InputAt(step));
                if (!rideA.State.Equals(solo[step]))
                {
                    Assert.Fail("Ride A diverged from its solo run at step " + (step + 1) + ":\n solo  " + solo[step] + "\n mixed " + rideA.State);
                }
            }
        }

        [Test]
        public void Step_AfterWarmup_AllocatesZeroBytes()
        {
            // Arrange
            const int warmupSteps = 10;
            var simulation = new Simulation(ScriptedRide.Setup, TestTerrain.InclineDownhillPlusZ(), Tuning.Default);
            for (int step = 0; step < warmupSteps; step++)
            {
                simulation.Step(ScriptedRide.InputAt(step));
            }

            // Act: the rest of the ride, including the step that raises LeftHeightmap.
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int step = warmupSteps; step < ScriptedRide.Steps; step++)
            {
                simulation.Step(ScriptedRide.InputAt(step));
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            // Assert
            Assert.That(simulation.State.OutsideHeightmap, Is.True, "the measured steps must include the edge crossing");
            Assert.That(allocated, Is.EqualTo(0L));
        }

        private static int CountEvents(List<StepSnapshot> trace)
        {
            int count = 0;
            foreach (StepSnapshot snapshot in trace)
            {
                count += snapshot.Events.Length;
            }

            return count;
        }
    }
}
