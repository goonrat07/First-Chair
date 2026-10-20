using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using FirstChair.Sim;
using NUnit.Framework;

namespace FirstChair.Sim.Tests
{
    /// <summary>
    /// The mandatory determinism tests (.claude/rules/sim-code.md): record a ride, replay it from its header
    /// and inputs, and compare every step bit for bit; refuse recordings from another version, tuning, heightmap
    /// or snow map.
    /// </summary>
    /// <remarks>
    /// Since Story 002 the inputs steer, tuck, brake and pole, and the scripted ride crosses all five snow
    /// types, so these tests prove that every riding feature replays exactly, not only the recording path.
    /// </remarks>
    public class DeterminismTests
    {
        // The SHA-256 of the scripted ride's trace (ScriptedRide.TraceBytes), and the sim version it was
        // taken at. If riding behaviour changes on purpose: bump Simulation.Version, then re-pin both.
        // Re-pinned for version 2 (Story 002): carving, skidding, tuck, brake, poling and snow types change
        // the ride, the trace gained heading, edge, skid, snow and stance, and the ride runs on banded snow with
        // a new input script (symmetric taps and a triangle wave) and a start at z = 20.
        private const ushort GoldenSimVersion = 2;
        private const string GoldenTraceSha256 = "52A0F8DB9F976BC8827568518894401A4BEDFE4BF7F4ACBEF8B00B4CD6A36FEC";

        [Test]
        public void ScriptedRide_RecordedThenReplayedFromBytes_MatchesBitForBitEveryStep()
        {
            // Arrange: ride 12 s live while recording, then serialize the recording to bytes.
            var live = new List<StepSnapshot>();
            Heightmap heightmap = TestTerrain.InclineDownhillPlusZ();
            Recording recording = ScriptedRide.Record(heightmap, TestTerrain.Bands(heightmap), Tuning.Default, ScriptedRide.Steps, live);
            byte[] bytes = recording.ToBytes();

            // Act: read the bytes back and replay them on freshly loaded copies of the same maps.
            bool read = Recording.TryRead(bytes, out Recording loaded);
            var replayed = new List<StepSnapshot>();
            Heightmap reloaded = TestTerrain.InclineDownhillPlusZ();
            ReplayResult result = Replayer.Replay(loaded, reloaded, TestTerrain.Bands(reloaded), Tuning.Default, simulation => replayed.Add(StepSnapshot.Of(simulation)));

            // Assert
            Assert.That(read, Is.True);
            Assert.That(result.Status, Is.EqualTo(ReplayStatus.Ok));
            Assert.That(live.Count, Is.GreaterThanOrEqualTo(10 * Simulation.StepsPerSecond), "the ride must last at least 10 s");
            Assert.That(replayed.Count, Is.EqualTo(live.Count));
            Assert.That(CountEvents(live), Is.EqualTo(1), "the ride must leave the map once, so the event lists are really compared");
            AssertExercisesEveryFeature(live);
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
        public void PolingRideOnFlat_RecordedThenReplayed_MatchesBitForBitEveryStep()
        {
            // Arrange: the scripted ride never slows below the pole-speed cap on a flat, so poling gets its own ride.
            Heightmap flat = TestTerrain.Flat();
            SnowMap snowMap = TestTerrain.Bands(flat);
            var setup = new RunSetup(Sport.Skis, 0, 42UL, Fix64.FromInt(64), Fix64.FromInt(10));
            var simulation = new Simulation(setup, flat, snowMap, Tuning.Default);
            var recording = new Recording(setup, flat, snowMap, Tuning.Default);
            var live = new List<StepSnapshot>();
            for (int step = 0; step < 8 * Simulation.StepsPerSecond; step++)
            {
                SimInput input = SimInput.Pack(step % 200 < 150 ? SimButtons.Forward : SimButtons.Back, 127 - Math.Abs(step * 3 % 508 - 254));
                recording.Append(input);
                simulation.Step(input);
                live.Add(StepSnapshot.Of(simulation));
            }

            // Act
            Recording.TryRead(recording.ToBytes(), out Recording loaded);
            var replayed = new List<StepSnapshot>();
            Heightmap reloaded = TestTerrain.Flat();
            ReplayResult result = Replayer.Replay(loaded, reloaded, TestTerrain.Bands(reloaded), Tuning.Default, s => replayed.Add(StepSnapshot.Of(s)));

            // Assert
            Assert.That(result.Status, Is.EqualTo(ReplayStatus.Ok));
            Assert.That(live.Exists(snapshot => snapshot.State.Stance == Stance.Pole), Is.True, "the ride must pole");
            Assert.That(replayed, Is.EqualTo(live));
        }

        [Test]
        public void ScriptedRide_TraceHash_MatchesGolden()
        {
            // Arrange
            var trace = new List<StepSnapshot>();
            Heightmap heightmap = TestTerrain.InclineDownhillPlusZ();
            ScriptedRide.Record(heightmap, TestTerrain.Bands(heightmap), Tuning.Default, ScriptedRide.Steps, trace);

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
            SnowMap snowMap = TestTerrain.Bands(heightmap);
            byte[] bytes = ScriptedRide.Record(heightmap, snowMap, Tuning.Default, 120, null).ToBytes();
            var otherVersion = (ushort)(Simulation.Version + 1);
            bytes[6] = (byte)otherVersion;
            bytes[7] = (byte)(otherVersion >> 8);
            Recording.TryRead(bytes, out Recording recording);
            int stepsSeen = 0;

            // Act
            ReplayResult result = Replayer.Replay(recording, heightmap, snowMap, Tuning.Default, _ => stepsSeen++);

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
            SnowMap snowMap = TestTerrain.Bands(heightmap);
            Recording recording = ScriptedRide.Record(heightmap, snowMap, Tuning.Default, 120, null);
            Tuning other = Tuning.Default;
            other.GroomedDrag = Fix64.FromRatio(1, 5);
            int stepsSeen = 0;

            // Act
            ReplayResult result = Replayer.Replay(recording, heightmap, snowMap, other, _ => stepsSeen++);

            // Assert
            Assert.That(result.Status, Is.EqualTo(ReplayStatus.TuningMismatch));
            Assert.That(result.StepsRun, Is.EqualTo(0));
            Assert.That(stepsSeen, Is.EqualTo(0));
        }

        [Test]
        public void Replay_OnDifferentHeightmap_ReportsHeightmapMismatchAndRunsNoSteps()
        {
            // Arrange
            Heightmap heightmap = TestTerrain.InclineDownhillPlusZ();
            Recording recording = ScriptedRide.Record(heightmap, TestTerrain.Bands(heightmap), Tuning.Default, 120, null);
            Heightmap other = TestTerrain.InclineDownhillMinusZ();
            int stepsSeen = 0;

            // Act
            ReplayResult result = Replayer.Replay(recording, other, TestTerrain.Bands(other), Tuning.Default, _ => stepsSeen++);

            // Assert
            Assert.That(result.Status, Is.EqualTo(ReplayStatus.HeightmapMismatch));
            Assert.That(result.StepsRun, Is.EqualTo(0));
            Assert.That(stepsSeen, Is.EqualTo(0));
        }

        [Test]
        public void Replay_OnDifferentSnowMap_ReportsSnowMapMismatchAndRunsNoSteps()
        {
            // Arrange: the same heightmap, with one sample's snow type changed.
            Heightmap heightmap = TestTerrain.InclineDownhillPlusZ();
            Recording recording = ScriptedRide.Record(heightmap, TestTerrain.Bands(heightmap), Tuning.Default, 120, null);
            SnowMap other = TestTerrain.Snow(heightmap, (row, column) =>
                row == 40 && column == 7 ? SnowType.Slush : (SnowType)(row / 6 % 5));
            int stepsSeen = 0;

            // Act
            ReplayResult result = Replayer.Replay(recording, heightmap, other, Tuning.Default, _ => stepsSeen++);

            // Assert
            Assert.That(result.Status, Is.EqualTo(ReplayStatus.SnowMapMismatch));
            Assert.That(result.StepsRun, Is.EqualTo(0));
            Assert.That(stepsSeen, Is.EqualTo(0));
        }

        [Test]
        public void Step_InterleavedWithAnotherSimulation_GivesSameStatesAsASoloRun()
        {
            // Arrange: a solo run of ride A, then A again stepped in lockstep with an unrelated ride B.
            Heightmap heightmap = TestTerrain.InclineDownhillPlusZ();
            SnowMap snowMap = TestTerrain.Bands(heightmap);
            var solo = new List<RiderState>();
            var soloRide = new Simulation(ScriptedRide.Setup, heightmap, snowMap, Tuning.Default);
            for (int step = 0; step < ScriptedRide.Steps; step++)
            {
                soloRide.Step(ScriptedRide.InputAt(step));
                solo.Add(soloRide.State);
            }

            var rideA = new Simulation(ScriptedRide.Setup, heightmap, snowMap, Tuning.Default);
            var setupB = new RunSetup(Sport.Skis, 2, 99UL, Fix64.FromInt(20), Fix64.FromInt(100));
            Heightmap heightmapB = TestTerrain.InclineDownhillMinusZ();
            var rideB = new Simulation(setupB, heightmapB, TestTerrain.Uniform(heightmapB, SnowType.Ice), Tuning.Default);

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
            Heightmap heightmap = TestTerrain.InclineDownhillPlusZ();
            var simulation = new Simulation(ScriptedRide.Setup, heightmap, TestTerrain.Bands(heightmap), Tuning.Default);
            for (int step = 0; step < warmupSteps; step++)
            {
                simulation.Step(ScriptedRide.InputAt(step));
            }

            // Act: the rest of the ride, including skids and the step that raises LeftHeightmap.
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

        /// <summary>The ride must carve, skid, use every stance and cross every snow type, or it proves too little.</summary>
        private static void AssertExercisesEveryFeature(List<StepSnapshot> trace)
        {
            var stances = new HashSet<Stance>();
            var snows = new HashSet<SnowType>();
            bool carved = false, skidded = false;
            foreach (StepSnapshot snapshot in trace)
            {
                RiderState state = snapshot.State;
                stances.Add(state.Stance);
                snows.Add(state.Snow);
                skidded |= state.Skidding;
                carved |= !state.Skidding && state.EdgeAngle != Fix64.Zero && state.Velocity != Vec3.Zero;
            }

            Assert.That(carved, Is.True, "the ride must carve");
            Assert.That(skidded, Is.True, "the ride must skid");
            Assert.That(stances, Is.EquivalentTo(new[] { Stance.Stand, Stance.Tuck, Stance.Brake }), "the ride must stand, tuck and brake");
            Assert.That(snows, Is.EquivalentTo((SnowType[])Enum.GetValues(typeof(SnowType))), "the ride must cross every snow type");
        }
    }
}
