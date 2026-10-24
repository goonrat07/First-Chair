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
    /// types; since Story 004 it also jumps, grabs, spins, lands, crashes and resets; since Story 005 it rides among trees,
    /// with a near miss, a branch clip and Flow; since Story 006 it grinds a box and scores tricks. So these tests prove that every
    /// riding feature replays exactly, not only the recording path.
    /// </remarks>
    public class DeterminismTests
    {
        // The SHA-256 of the scripted ride's trace (ScriptedRide.TraceBytes), and the sim version it was
        // taken at. If riding behaviour changes on purpose: bump Simulation.Version, then re-pin both.
        // Re-pinned for version 2 (Story 002): carving, skidding, tuck, brake, poling and snow types change
        // the ride, the trace gained heading, edge, skid, snow and stance, and the ride runs on banded snow with
        // a new input script (symmetric taps and a triangle wave) and a start at z = 20.
        // Re-pinned for version 3 (Story 004): pops, takeoff, spins, flips, grabs, graded landings, crashes and reset
        // change the ride; the trace gained the air, crash and safe-spot fields and the event payloads; and the script
        // (18 s now) pops three times with scripted air input and taps reset once.
        // Re-pinned for version 4 (Story 005): trees and Flow change the ride. It now runs among ScriptedRide.Trees,
        // passing tree 0 close (a near miss that fills Flow) and clipping tree 1's branches before the first pop (a branch
        // hit that costs 30% of the speed and Flow), so everything after it moves; and the trace gained Flow,
        // FlowIdleTicks and the events' tree and gap. Pinned only after record/replay, interleaving and zero allocation
        // passed with the trees in the ride.
        // Re-pinned for version 5 (Story 006): rails and grinds change the ride. After the reset it now rides onto
        // ScriptedRide.Rails' box, grinds it (the run's random generator wobbles the lean, so the generator's state moves)
        // and leaves it; the trace gained the switch flag, the grind state and the events' touchdown Flow, rail and grind
        // summary. Pinned only after record/replay (with the run score folded from the replayed events), interleaving and
        // zero allocation passed with the box in the ride.
        // Re-pinned for version 6 (playtest retune, 2026-10-24): a quarter more grip on every snow type and a slower turn
        // change every carve and skid, so the whole ride moves; ScriptedRide.Rails' box moved 2 m to stay on the line after
        // the reset. Pinned only after record/replay, interleaving and zero allocation passed with the new numbers.
        // Re-pinned for version 7 (Story 008): carves and quick turns, turning by speed and snow, and faster slopes change
        // the whole ride. It runs faster, so the third jump crashes sooner: the reset tap moved into that jump's air window
        // (step 1200), while the rider still lies crashed, and ScriptedRide.Rails' box moved onto the new line after the
        // reset, past the crash, on the level ground beyond the map's edge. The ride now also carves and poles, so it takes
        // every stance. Pinned only after record/replay, interleaving and zero allocation passed with the new numbers.
        private const ushort GoldenSimVersion = 7;
        private const string GoldenTraceSha256 = "9181C0B3D642BF25642681EC31647E2BD0C6CAD450FCE0E220DCEDCBD997ABC2";

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
            ReplayResult result = Replayer.Replay(
                loaded, reloaded, TestTerrain.Bands(reloaded), ScriptedRide.Trees, ScriptedRide.Rails, Tuning.Default, simulation => replayed.Add(StepSnapshot.Of(simulation)));

            // Assert
            Assert.That(read, Is.True);
            Assert.That(result.Status, Is.EqualTo(ReplayStatus.Ok));
            Assert.That(live.Count, Is.GreaterThanOrEqualTo(10 * Simulation.StepsPerSecond), "the ride must last at least 10 s");
            Assert.That(replayed.Count, Is.EqualTo(live.Count));
            AssertRaisesEveryEvent(live);
            AssertExercisesEveryFeature(live);
            for (int i = 0; i < live.Count; i++)
            {
                if (!live[i].Equals(replayed[i]))
                {
                    Assert.Fail("First divergence at step " + (i + 1) + ":\n live   " + live[i] + "\n replay " + replayed[i]);
                }
            }

            Assert.That(result.FinalState, Is.EqualTo(live[live.Count - 1].State));

            // Story 006: the run total is a fold over the events, and the replay rebuilds it.
            var liveScore = new RunScore(Tuning.Default);
            foreach (StepSnapshot snapshot in live)
            {
                liveScore.AddAll(snapshot.Events);
            }

            Assert.That(liveScore.Tricks, Has.Some.Matches<ScoredTrick>(t => t.Kind == ScoredTrickKind.Jump && t.Points > 0), "the ride must score a trick");
            Assert.That(liveScore.Tricks, Has.Some.Matches<ScoredTrick>(t => t.Kind == ScoredTrickKind.Grind), "the ride must score a grind");
            Assert.That(liveScore.Total, Is.GreaterThan(0L));
            Assert.That(result.TotalScore, Is.EqualTo(liveScore.Total));
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
            var soloRide = new Simulation(ScriptedRide.Setup, heightmap, snowMap, ScriptedRide.Trees, ScriptedRide.Rails, Tuning.Default);
            for (int step = 0; step < ScriptedRide.Steps; step++)
            {
                soloRide.Step(ScriptedRide.InputAt(step));
                solo.Add(soloRide.State);
            }

            var rideA = new Simulation(ScriptedRide.Setup, heightmap, snowMap, ScriptedRide.Trees, ScriptedRide.Rails, Tuning.Default);
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
            var simulation = new Simulation(ScriptedRide.Setup, heightmap, TestTerrain.Bands(heightmap), ScriptedRide.Trees, ScriptedRide.Rails, Tuning.Default);
            for (int step = 0; step < warmupSteps; step++)
            {
                simulation.Step(ScriptedRide.InputAt(step));
            }

            // Act: the rest of the ride, including skids, pops, landings, the crash, both resets and the step that
            // raises LeftHeightmap. Event kinds are counted by index, which allocates nothing.
            var seen = new int[16];
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int step = warmupSteps; step < ScriptedRide.Steps; step++)
            {
                simulation.Step(ScriptedRide.InputAt(step));
                for (int i = 0; i < simulation.Events.Count; i++)
                {
                    seen[(int)simulation.Events[i].Kind]++;
                }
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            // Assert
            Assert.That(simulation.State.OutsideHeightmap, Is.True, "the measured steps must include the edge crossing");
            foreach (SimEventKind kind in new[]
            {
                SimEventKind.Takeoff, SimEventKind.Land, SimEventKind.Crash, SimEventKind.Reset, SimEventKind.NearMiss, SimEventKind.BranchHit,
                SimEventKind.GrindStart, SimEventKind.GrindEnd,
            })
            {
                Assert.That(seen[(int)kind], Is.GreaterThan(0), "the measured steps must raise " + kind);
            }

            Assert.That(allocated, Is.EqualTo(0L));
        }

        /// <summary>
        /// The ride must raise every event kind, land a grab and a spin, and crash, so event payloads are really
        /// compared.
        /// </summary>
        private static void AssertRaisesEveryEvent(List<StepSnapshot> trace)
        {
            var kinds = new HashSet<SimEventKind>();
            bool landedGrab = false, landedSpin = false;
            foreach (StepSnapshot snapshot in trace)
            {
                foreach (SimEvent simEvent in snapshot.Events)
                {
                    kinds.Add(simEvent.Kind);
                    landedGrab |= simEvent.Kind == SimEventKind.Land && simEvent.Air.Grab != GrabKind.None;
                    landedSpin |= simEvent.Kind == SimEventKind.Land && Fix64.Abs(simEvent.Air.Spin) > Fix64.Pi;
                }
            }

            Assert.That(kinds, Is.EquivalentTo((SimEventKind[])Enum.GetValues(typeof(SimEventKind))), "the ride must raise every event kind");
            Assert.That(landedGrab, Is.True, "the ride must land a grab");
            Assert.That(landedSpin, Is.True, "the ride must land a spin");
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
                carved |= state.Stance == Stance.Carve && !state.Skidding;
            }

            Assert.That(carved, Is.True, "the ride must carve");
            Assert.That(skidded, Is.True, "the ride must skid");
            Assert.That(stances, Is.EquivalentTo((Stance[])Enum.GetValues(typeof(Stance))), "the ride must take every stance");
            Assert.That(snows, Is.EquivalentTo((SnowType[])Enum.GetValues(typeof(SnowType))), "the ride must cross every snow type");
            Assert.That(trace.Exists(snapshot => snapshot.State.Airborne), Is.True, "the ride must fly");
            Assert.That(trace.Exists(snapshot => snapshot.State.Crashed), Is.True, "the ride must crash");
            Assert.That(trace.Exists(snapshot => snapshot.State.Flow > Fix64.Zero), Is.True, "the ride must build Flow");
        }
    }
}
