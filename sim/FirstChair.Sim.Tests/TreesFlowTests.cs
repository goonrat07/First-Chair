using System;
using System.Collections.Generic;
using System.IO;
using FirstChair.Sim;
using NUnit.Framework;

namespace FirstChair.Sim.Tests
{
    /// <summary>
    /// Story 005 scripted-input tests: trees as sim data, near misses that fill Flow, the Flow drain, branch clips, trunk
    /// crashes, the drag cut, and replays refusing other trees. In-memory trees stand on the TestTerrain incline, where a
    /// rider with no input runs straight down x = 64 m (the plane has no slope across it); one test rides straight lines
    /// through the committed test slope's glade. Expectations come from <see cref="Tuning"/>.
    /// </summary>
    public class TreesFlowTests
    {
        private const int LineXMm = 64_000;
        private const int StartZMm = 10_000;
        private const int TrunkMm = 200;
        private const int RideSteps = 12 * Simulation.StepsPerSecond;
        private const int GladeLineSteps = 40 * Simulation.StepsPerSecond;
        private const int GladeLineSpacingMm = 2_000;
        private const int GladeRunUpM = 40;
        private const double Tolerance = 1e-6;
        private const double GapTolerance = 0.01;

        private static readonly SimInput NoInput = SimInput.Pack(SimButtons.None, 0);
        private static readonly SimInput Brake = SimInput.Pack(SimButtons.Back, 0);
        private static readonly SimInput Forward = SimInput.Pack(SimButtons.Forward, 0);

        [Test]
        public void ThreadingCloseGaps_StraightLine_RaisesOneNearMissPerTreeAndFillsFlow()
        {
            // Arrange: four gates of two trunks 1.2 m either side of the line (a 0.7 m gap), no branches past the trunks.
            var trees = new List<SlopeTree>();
            foreach (int zMm in new[] { 25_000, 35_000, 45_000, 55_000 })
            {
                trees.Add(Tree(LineXMm - 1_200, zMm));
                trees.Add(Tree(LineXMm + 1_200, zMm));
            }

            Simulation simulation = OnIncline(new Forest(trees), Tuning.Default);

            // Act
            List<SimEvent> events = RideUntilPast(simulation, 60_000);

            // Assert
            Assert.That(Count(events, SimEventKind.Crash), Is.Zero);
            Assert.That(Count(events, SimEventKind.BranchHit), Is.Zero);
            List<SimEvent> nearMisses = Of(events, SimEventKind.NearMiss);
            Assert.That(nearMisses.Count, Is.EqualTo(trees.Count));
            for (int tree = 0; tree < trees.Count; tree++)
            {
                Assert.That(nearMisses.FindAll(e => e.Tree == tree).Count, Is.EqualTo(1), "tree " + tree);
            }

            foreach (SimEvent nearMiss in nearMisses)
            {
                Assert.That(D(nearMiss.Gap), Is.EqualTo(0.7).Within(GapTolerance));
                Assert.That(D(nearMiss.Speed), Is.GreaterThanOrEqualTo(D(Tuning.Default.NearMissMinSpeed)));
            }

            Assert.That(simulation.State.Flow, Is.EqualTo(Fix64.One), "eight close passes fill Flow");
        }

        [Test]
        public void LineIntoTrunk_CrashesWithCauseTreeEmptiesFlowAndResetsToTheLastSafeSpot()
        {
            // Arrange: a gate builds Flow, then a trunk stands on the line.
            var forest = new Forest(new[] { Tree(LineXMm - 1_000, 22_000), Tree(LineXMm + 1_000, 22_000), Tree(LineXMm, 32_000) });
            Simulation simulation = OnIncline(forest, Tuning.Default);
            var events = new List<SimEvent>();
            RiderState before = simulation.State;

            // Act: ride until the crash, then lie through CrashResetDelay.
            for (int step = 0; step < RideSteps && !simulation.State.Crashed; step++)
            {
                before = simulation.State;
                StepInto(simulation, NoInput, events);
            }

            RiderState crashed = simulation.State;
            List<SimEvent> crashes = Of(events, SimEventKind.Crash);
            int resetSteps = (int)Math.Ceiling(D(Tuning.Default.CrashResetDelay) * Simulation.StepsPerSecond - 1e-9);
            for (int step = 0; step < resetSteps; step++)
            {
                StepInto(simulation, NoInput, events);
            }

            // Assert
            Assert.That(crashes.Count, Is.EqualTo(1));
            Assert.That(crashes[0].Cause, Is.EqualTo(CrashCause.Tree));
            Assert.That(crashes[0].Tree, Is.EqualTo(2));
            Assert.That(D(crashes[0].Speed), Is.GreaterThan(D(Tuning.Default.NearMissMinSpeed)));
            Assert.That(crashed.Crashed, Is.True);
            Assert.That(crashed.Velocity, Is.EqualTo(Vec3.Zero));
            Assert.That(crashed.Position.X == before.Position.X && crashed.Position.Z == before.Position.Z, Is.True, "stops where the step began");
            Assert.That(D(before.Flow), Is.GreaterThan(0.0), "the gate built Flow");
            Assert.That(crashed.Flow, Is.EqualTo(Fix64.Zero), "a crash empties Flow");
            Assert.That(events.Exists(e => e.Kind == SimEventKind.NearMiss && e.Tree == 2), Is.False, "a trunk hit is no near miss");
            Single(events, SimEventKind.Reset);
            Assert.That(simulation.State.Crashed, Is.False);
            Assert.That(simulation.State.Position, Is.EqualTo(crashed.SafePosition));
            Assert.That(simulation.State.Velocity, Is.EqualTo(Vec3.Zero));
        }

        [Test]
        public void CloserPass_FillsMoreFlow()
        {
            // Arrange: one trunk beside the line, passed at a 0.4 m gap in one ride and at 1.5 m in the other.
            Simulation close = OnIncline(new Forest(new[] { Tree(LineXMm + 900, 30_000) }), Tuning.Default);
            Simulation far = OnIncline(new Forest(new[] { Tree(LineXMm + 2_000, 30_000) }), Tuning.Default);

            // Act
            SimEvent closeMiss = Single(RideUntilPast(close, 35_000), SimEventKind.NearMiss);
            SimEvent farMiss = Single(RideUntilPast(far, 35_000), SimEventKind.NearMiss);

            // Assert
            Assert.That(D(closeMiss.Gap), Is.EqualTo(0.4).Within(GapTolerance));
            Assert.That(D(farMiss.Gap), Is.EqualTo(1.5).Within(GapTolerance));
            Assert.That(D(close.State.Flow), Is.GreaterThan(D(far.State.Flow)));
            Assert.That(D(close.State.Flow), Is.EqualTo(ExpectedGain(closeMiss.Gap)).Within(Tolerance));
            Assert.That(D(far.State.Flow), Is.EqualTo(ExpectedGain(farMiss.Gap)).Within(Tolerance));
        }

        [Test]
        public void TreePassHash_WhilePassingATree_TracksThePassState()
        {
            // Arrange: per-tree pass state lives outside RiderState; replay tests compare it through this hash, so the
            // hash must move with the pass (closest gap so far, then judged).
            Simulation simulation = OnIncline(new Forest(new[] { Tree(LineXMm + 900, 30_000) }), Tuning.Default);
            var hashes = new HashSet<ulong> { simulation.TreePassHash };

            // Act
            for (int step = 0; step < RideSteps && simulation.State.Position.Z < Metres(35_000); step++)
            {
                simulation.Step(NoInput);
                hashes.Add(simulation.TreePassHash);
            }

            // Assert
            Assert.That(hashes.Count, Is.GreaterThan(2));
        }

        [Test]
        public void LongPassInsideTheRing_RaisesExactlyOneNearMissPerTree()
        {
            // Arrange: two trunks 1 m off the line; the rider spends many steps inside each one's near-miss ring.
            var forest = new Forest(new[] { Tree(LineXMm + 1_000, 30_000), Tree(LineXMm - 1_000, 45_000) });
            Simulation simulation = OnIncline(forest, Tuning.Default);
            var events = new List<SimEvent>();
            double reach = D(Metres(TrunkMm) + Tuning.Default.RiderRadius);
            int stepsInRing = 0;

            // Act
            for (int step = 0; step < RideSteps && D(simulation.State.Position.Z) < 50.0; step++)
            {
                StepInto(simulation, NoInput, events);
                double along = D(simulation.State.Position.Z) - 30.0;
                if (Math.Sqrt(1.0 + along * along) - reach <= D(Tuning.Default.NearMissDistance))
                {
                    stepsInRing++;
                }
            }

            // Assert
            Assert.That(stepsInRing, Is.GreaterThan(10), "a long pass, not a single step");
            Assert.That(events.FindAll(e => e.Kind == SimEventKind.NearMiss && e.Tree == 0).Count, Is.EqualTo(1));
            Assert.That(events.FindAll(e => e.Kind == SimEventKind.NearMiss && e.Tree == 1).Count, Is.EqualTo(1));
        }

        [Test]
        public void PassSlowerThanNearMissMinSpeed_RaisesNothingAndFillsNoFlow()
        {
            // Arrange: a tuning variant no rider here reaches.
            Tuning tuning = Tuning.Default;
            tuning.NearMissMinSpeed = Fix64.FromInt(100);
            Simulation simulation = OnIncline(new Forest(new[] { Tree(LineXMm + 1_000, 30_000) }), tuning);

            // Act
            List<SimEvent> events = RideUntilPast(simulation, 35_000);

            // Assert
            Assert.That(Count(events, SimEventKind.NearMiss), Is.Zero);
            Assert.That(simulation.State.Flow, Is.EqualTo(Fix64.Zero));
        }

        [Test]
        public void Flow_AfterDrainDelayWithoutNearMiss_DrainsAtTheTunedRateToZero()
        {
            // Arrange: two gates build Flow; then the rider brakes to a stop in open snow.
            var forest = new Forest(new[]
            {
                Tree(LineXMm - 1_000, 22_000), Tree(LineXMm + 1_000, 22_000), Tree(LineXMm - 1_000, 28_000), Tree(LineXMm + 1_000, 28_000),
            });
            Simulation simulation = OnIncline(forest, Tuning.Default);
            var events = new List<SimEvent>();
            for (int step = 0; step < RideSteps && Count(events, SimEventKind.NearMiss) < 4; step++)
            {
                StepInto(simulation, NoInput, events);
            }

            Fix64 full = simulation.State.Flow;
            Tuning tuning = Tuning.Default;
            int delaySteps = (int)Math.Round(D(tuning.FlowDrainDelay) * Simulation.StepsPerSecond);
            double perStep = D(tuning.FlowDrainRate) / Simulation.StepsPerSecond;
            var flow = new List<Fix64>();

            // Act: flow[k - 1] is the Flow k steps after the last near miss.
            for (int k = 1; k <= delaySteps + 600; k++)
            {
                StepInto(simulation, Brake, events);
                flow.Add(simulation.State.Flow);
            }

            // Assert
            Assert.That(Count(events, SimEventKind.NearMiss), Is.EqualTo(4));
            Assert.That(D(full), Is.GreaterThan(60 * perStep), "the gates built Flow to drain");
            Assert.That(flow[delaySteps - 2], Is.EqualTo(full), "Flow holds through the delay");
            Assert.That(D(flow[delaySteps - 1]), Is.EqualTo(D(full) - perStep).Within(Tolerance), "and drains once it has passed");
            Assert.That(D(flow[delaySteps + 58]), Is.EqualTo(D(full) - 60 * perStep).Within(Tolerance), "at FlowDrainRate per second");
            Assert.That(flow[flow.Count - 1], Is.EqualTo(Fix64.Zero), "down to zero");
            Assert.That(flow.TrueForAll(value => value >= Fix64.Zero), Is.True, "never below it");
        }

        [Test]
        public void BranchClip_CostsSpeedAndFlowAndRaisesBranchHit()
        {
            // Arrange: two gates build Flow; then a tree 1.3 m off the line whose 1.5 m branches reach over it (trunk gap 0.8 m).
            var forest = new Forest(new[]
            {
                Tree(LineXMm - 1_000, 22_000), Tree(LineXMm + 1_000, 22_000), Tree(LineXMm - 1_000, 28_000), Tree(LineXMm + 1_000, 28_000),
                new SlopeTree(LineXMm + 1_300, 40_000, TrunkMm, 1_500),
            });
            Simulation simulation = OnIncline(forest, Tuning.Default);
            var events = new List<SimEvent>();
            RiderState before = default, after = default;
            SimEvent clip = default;

            // Act
            for (int step = 0; step < RideSteps && D(simulation.State.Position.Z) < 50.0; step++)
            {
                RiderState previous = simulation.State;
                int firstNew = events.Count;
                StepInto(simulation, NoInput, events);
                for (int i = firstNew; i < events.Count; i++)
                {
                    if (events[i].Kind == SimEventKind.BranchHit && clip.Kind != SimEventKind.BranchHit)
                    {
                        clip = events[i];
                        before = previous;
                        after = simulation.State;
                    }
                }
            }

            // Assert
            Tuning tuning = Tuning.Default;
            Assert.That(Count(events, SimEventKind.BranchHit), Is.EqualTo(1));
            Assert.That(clip.Tree, Is.EqualTo(4));
            Assert.That(Count(events, SimEventKind.Crash), Is.Zero);
            Assert.That(D(before.Flow), Is.GreaterThan(D(tuning.BranchFlowCost)), "the gates built more Flow than a clip costs");
            Assert.That(after.Flow, Is.EqualTo(before.Flow - tuning.BranchFlowCost), "the clip costs BranchFlowCost");
            Assert.That(Speed(after), Is.EqualTo(D(clip.Speed) * (1.0 - D(tuning.BranchSpeedLoss))).Within(1e-4), "and BranchSpeedLoss of the speed");
            Assert.That(Speed(after), Is.LessThan(Speed(before) * 0.75));
            Assert.That(events.Exists(e => e.Kind == SimEventKind.NearMiss && e.Tree == 4), Is.True, "the trunk itself was still missed");
        }

        [Test]
        public void FullFlow_CutsSnowAndAirDragByFlowDragCut()
        {
            // Arrange: pole across a flat through four gates until Flow is full and every pass is judged.
            Heightmap flat = TestTerrain.Flat();
            SnowMap snowMap = TestTerrain.Uniform(flat, SnowType.Groomed);
            var setup = new RunSetup(Sport.Skis, 0, 9UL, Fix64.FromInt(64), Fix64.FromInt(10));
            Fix64 heading = new Simulation(setup, flat, snowMap, Tuning.Default).State.Heading;
            double forwardX = D(Fix64.Sin(heading)), forwardZ = D(Fix64.Cos(heading));
            var trees = new List<SlopeTree>();
            foreach (double along in new[] { 10.0, 14.0, 18.0, 22.0 })
            {
                foreach (double side in new[] { -1.0, 1.0 })
                {
                    trees.Add(Tree(Mm(64.0 + forwardX * along + forwardZ * side), Mm(10.0 + forwardZ * along - forwardX * side)));
                }
            }

            var simulation = new Simulation(setup, flat, snowMap, new Forest(trees), Tuning.Default);
            var events = new List<SimEvent>();
            for (int step = 0; step < RideSteps; step++)
            {
                double travelled = (D(simulation.State.Position.X) - 64.0) * forwardX + (D(simulation.State.Position.Z) - 10.0) * forwardZ;
                if (Count(events, SimEventKind.NearMiss) == trees.Count && travelled > 25.5)
                {
                    break;
                }

                StepInto(simulation, Forward, events);
            }

            RiderState before = simulation.State;

            // Act: one coasting step.
            simulation.Step(NoInput);
            RiderState after = simulation.State;

            // Assert
            Tuning tuning = Tuning.Default;
            double speed = Speed(before);
            double uncut = (D(tuning.GroomedDrag) * D(tuning.Gravity) + D(tuning.AirDrag) * speed * speed) / Simulation.StepsPerSecond;
            Assert.That(Count(events, SimEventKind.NearMiss), Is.EqualTo(trees.Count));
            Assert.That(before.Flow, Is.EqualTo(Fix64.One));
            Assert.That(simulation.Events.Count, Is.Zero, "no tree event on the measured step");
            Assert.That(speed - Speed(after), Is.EqualTo(uncut * (1.0 - D(tuning.FlowDragCut))).Within(uncut * 0.01));
        }

        [Test]
        public void Replay_AmongDifferentTrees_ReportsForestMismatchAndRunsNoSteps()
        {
            // Arrange: record 5 s past one tree; a second forest moves that tree by 1 mm.
            Heightmap terrain = TestTerrain.InclineDownhillPlusZ();
            SnowMap snowMap = TestTerrain.Uniform(terrain, SnowType.Groomed);
            var forest = new Forest(new[] { Tree(LineXMm + 1_000, 30_000) });
            var moved = new Forest(new[] { Tree(LineXMm + 1_001, 30_000) });
            var simulation = new Simulation(InclineSetup(), terrain, snowMap, forest, Tuning.Default);
            var recording = new Recording(InclineSetup(), terrain, snowMap, forest, Tuning.Default);
            var events = new List<SimEvent>();
            for (int step = 0; step < 5 * Simulation.StepsPerSecond; step++)
            {
                recording.Append(NoInput);
                StepInto(simulation, NoInput, events);
            }

            Recording.TryRead(recording.ToBytes(), out Recording loaded);
            int stepsSeen = 0;

            // Act
            ReplayResult same = Replayer.Replay(loaded, terrain, snowMap, forest, Tuning.Default);
            ReplayResult other = Replayer.Replay(loaded, terrain, snowMap, moved, Tuning.Default, _ => stepsSeen++);
            ReplayResult none = Replayer.Replay(loaded, terrain, snowMap, Tuning.Default, _ => stepsSeen++);

            // Assert
            Assert.That(Count(events, SimEventKind.NearMiss), Is.EqualTo(1), "the recorded ride passes the tree");
            Assert.That(loaded.ForestHash, Is.EqualTo(forest.Hash));
            Assert.That(moved.Hash, Is.Not.EqualTo(forest.Hash));
            Assert.That(same.Status, Is.EqualTo(ReplayStatus.Ok));
            Assert.That(same.FinalState, Is.EqualTo(simulation.State));
            Assert.That(other.Status, Is.EqualTo(ReplayStatus.ForestMismatch));
            Assert.That(other.StepsRun, Is.EqualTo(0));
            Assert.That(none.Status, Is.EqualTo(ReplayStatus.ForestMismatch));
            Assert.That(stepsSeen, Is.EqualTo(0));
        }

        [Test]
        public void ForestFromCommittedLayout_HoldsEveryTreeInFileOrder()
        {
            // Arrange
            (SlopeLayout layout, _, _) = LoadTestSlope();

            // Act
            Forest forest = Forest.FromLayout(layout);

            // Assert
            Assert.That(forest.Count, Is.EqualTo(layout.Trees.Count));
            Assert.That(forest.Count, Is.GreaterThan(0));
            for (int i = 0; i < forest.Count; i++)
            {
                Assert.That(forest[i], Is.EqualTo(layout.Trees[i]), "tree " + i);
            }

            Assert.That(Forest.FromLayout(layout).Hash, Is.EqualTo(forest.Hash));
            Assert.That(forest.Hash, Is.Not.EqualTo(Forest.Empty.Hash));
        }

        [TestCase(0, 0)]
        [TestCase(300, 299)]
        [TestCase(300, Forest.MaxRadiusMm + 1)]
        public void NewForest_TreeWithBadRadii_ThrowsArgumentException(int trunkMm, int branchMm)
        {
            // Arrange
            var trees = new[] { Tree(LineXMm, 30_000), new SlopeTree(LineXMm, 40_000, trunkMm, branchMm) };

            // Act and Assert
            Assert.That(() => new Forest(trees), Throws.ArgumentException.With.Message.Contains("Tree 1"));
        }

        [Test]
        public void StraightLinesThroughCommittedGlade_SomeThreadGapsAndFillFlowSomeHitATrunkAndReset()
        {
            // Arrange
            (SlopeLayout layout, Heightmap heightmap, SnowMap snowMap) = LoadTestSlope();
            Forest forest = Forest.FromLayout(layout);
            Assert.That(layout.TryGetZone("glade", out SlopeZone glade), Is.True);
            int threaded = 0, crashed = 0;

            // Act and Assert: one straight, input-free line every 2 m across the glade, from 40 m above it.
            for (int xMm = glade.XMinMm; xMm <= glade.XMaxMm; xMm += GladeLineSpacingMm)
            {
                var setup = new RunSetup(Sport.Skis, 0, 7UL, Metres(xMm), glade.ZMin - Fix64.FromInt(GladeRunUpM));
                var simulation = new Simulation(setup, heightmap, snowMap, forest, Tuning.Default);
                var events = new List<SimEvent>();
                RiderState crashState = default;
                Fix64 peakFlow = Fix64.Zero;
                for (int step = 0; step < GladeLineSteps; step++)
                {
                    StepInto(simulation, NoInput, events);
                    peakFlow = Fix64.Max(peakFlow, simulation.State.Flow);
                    if (!crashState.Crashed && simulation.State.Crashed)
                    {
                        crashState = simulation.State;
                    }

                    if (Count(events, SimEventKind.Reset) > 0 || (!crashState.Crashed && simulation.State.Position.Z > glade.ZMax))
                    {
                        break;
                    }
                }

                string line = "line x = " + xMm + " mm";
                foreach (SimEvent nearMiss in Of(events, SimEventKind.NearMiss))
                {
                    Assert.That(nearMiss.Tree, Is.InRange(0, forest.Count - 1), line);
                    Assert.That(D(nearMiss.Gap), Is.GreaterThan(0.0).And.LessThanOrEqualTo(D(Tuning.Default.NearMissDistance)), line);
                }

                if (crashState.Crashed)
                {
                    SimEvent crash = Single(events, SimEventKind.Crash);
                    Assert.That(crash.Cause, Is.EqualTo(CrashCause.Tree), line);
                    Assert.That(simulation.State.Position, Is.EqualTo(crashState.SafePosition), line + " resets to the last safe spot");
                    crashed++;
                }
                else if (Count(events, SimEventKind.NearMiss) > 0)
                {
                    Assert.That(D(peakFlow), Is.GreaterThan(0.0), line);
                    threaded++;
                }
            }

            Assert.That(threaded, Is.GreaterThan(0), "some line threads the glade's gaps and fills Flow");
            Assert.That(crashed, Is.GreaterThan(0), "some line hits a trunk");
        }

        [Test]
        public void TrickMultiplier_ScalesWithFlow_FullFlowAddsFlowTrickBonus()
        {
            // Arrange
            Tuning tuning = Tuning.Default;

            // Act and Assert
            Assert.That(tuning.TrickMultiplier(Fix64.Zero), Is.EqualTo(Fix64.One));
            Assert.That(D(tuning.TrickMultiplier(Fix64.One)), Is.EqualTo(1.0 + D(tuning.FlowTrickBonus)).Within(Tolerance));
            Assert.That(D(tuning.TrickMultiplier(Fix64.One)), Is.EqualTo(1.5).Within(Tolerance), "the story's +50%");
            Assert.That(D(tuning.TrickMultiplier(Fix64.FromRatio(1, 2))), Is.EqualTo(1.25).Within(Tolerance));
        }

        [TestCase("RiderRadius negative")]
        [TestCase("RiderRadius too large")]
        [TestCase("NearMissDistance zero")]
        [TestCase("NearMissDistance too large")]
        [TestCase("NearMissMinSpeed negative")]
        [TestCase("NearMissFlowGainFar negative")]
        [TestCase("Close below Far")]
        [TestCase("NearMissFlowGainClose above one")]
        [TestCase("FlowDrainDelay negative")]
        [TestCase("FlowDrainDelay too long")]
        [TestCase("FlowDrainRate negative")]
        [TestCase("BranchSpeedLoss above one")]
        [TestCase("BranchFlowCost negative")]
        [TestCase("FlowDragCut above one")]
        [TestCase("FlowTrickBonus negative")]
        public void NewRun_TreeOrFlowTuningARideCannotRunOn_ThrowsArgumentException(string badValue)
        {
            // Arrange
            Tuning tuning = Tuning.Default;
            Fix64 tiny = Fix64.FromRaw(1);
            switch (badValue)
            {
                case "RiderRadius negative": tuning.RiderRadius = -tiny; break;
                case "RiderRadius too large": tuning.RiderRadius = Fix64.FromInt(1000) + tiny; break;
                case "NearMissDistance zero": tuning.NearMissDistance = Fix64.Zero; break;
                case "NearMissDistance too large": tuning.NearMissDistance = Fix64.FromInt(1000) + tiny; break;
                case "NearMissMinSpeed negative": tuning.NearMissMinSpeed = -tiny; break;
                case "NearMissFlowGainFar negative": tuning.NearMissFlowGainFar = -tiny; break;
                case "Close below Far": tuning.NearMissFlowGainClose = tuning.NearMissFlowGainFar - tiny; break;
                case "NearMissFlowGainClose above one": tuning.NearMissFlowGainClose = Fix64.One + tiny; break;
                case "FlowDrainDelay negative": tuning.FlowDrainDelay = -tiny; break;
                case "FlowDrainDelay too long": tuning.FlowDrainDelay = Fix64.FromInt(int.MaxValue / Simulation.StepsPerSecond); break;
                case "FlowDrainRate negative": tuning.FlowDrainRate = -tiny; break;
                case "BranchSpeedLoss above one": tuning.BranchSpeedLoss = Fix64.One + tiny; break;
                case "BranchFlowCost negative": tuning.BranchFlowCost = -tiny; break;
                case "FlowDragCut above one": tuning.FlowDragCut = Fix64.One + tiny; break;
                case "FlowTrickBonus negative": tuning.FlowTrickBonus = -tiny; break;
                default: throw new ArgumentOutOfRangeException(nameof(badValue), badValue, "no such case");
            }

            Heightmap flat = TestTerrain.Flat();

            // Act and Assert
            Assert.That(
                () => new Simulation(InclineSetup(), flat, TestTerrain.Uniform(flat, SnowType.Groomed), Forest.Empty, tuning),
                Throws.ArgumentException);
        }

        private static SlopeTree Tree(int xMm, int zMm) => new SlopeTree(xMm, zMm, TrunkMm, TrunkMm);

        private static RunSetup InclineSetup() => new RunSetup(Sport.Skis, 0, 5UL, Metres(LineXMm), Metres(StartZMm));

        private static Simulation OnIncline(Forest forest, Tuning tuning)
        {
            Heightmap terrain = TestTerrain.InclineDownhillPlusZ();
            return new Simulation(InclineSetup(), terrain, TestTerrain.Uniform(terrain, SnowType.Groomed), forest, tuning);
        }

        private static void StepInto(Simulation simulation, SimInput input, List<SimEvent> events)
        {
            simulation.Step(input);
            for (int i = 0; i < simulation.Events.Count; i++)
            {
                events.Add(simulation.Events[i]);
            }
        }

        private static List<SimEvent> RideUntilPast(Simulation simulation, int zMm)
        {
            var events = new List<SimEvent>();
            Fix64 z = Metres(zMm);
            for (int step = 0; step < RideSteps && simulation.State.Position.Z <= z; step++)
            {
                StepInto(simulation, NoInput, events);
            }

            Assert.That(simulation.State.Position.Z > z, Is.True, "the ride must get past z = " + zMm + " mm");
            return events;
        }

        private static double ExpectedGain(Fix64 gap)
        {
            Tuning tuning = Tuning.Default;
            double far = D(tuning.NearMissFlowGainFar);
            return far + (D(tuning.NearMissFlowGainClose) - far) * (1.0 - D(gap) / D(tuning.NearMissDistance));
        }

        private static List<SimEvent> Of(List<SimEvent> events, SimEventKind kind) => events.FindAll(e => e.Kind == kind);

        private static int Count(List<SimEvent> events, SimEventKind kind) => Of(events, kind).Count;

        private static SimEvent Single(List<SimEvent> events, SimEventKind kind)
        {
            List<SimEvent> found = Of(events, kind);
            Assert.That(found.Count, Is.EqualTo(1), kind + " events: " + string.Join(", ", found));
            return found[0];
        }

        private static Fix64 Metres(int mm) => Fix64.FromRatio(mm, 1000);

        private static int Mm(double metres) => (int)Math.Round(metres * 1000.0);

        private static double D(Fix64 value) => TestTerrain.ToDouble(value);

        private static double Speed(RiderState state) => D(Fix64.Sqrt(Vec3.Dot(state.Velocity, state.Velocity)));

        private static (SlopeLayout Layout, Heightmap Heightmap, SnowMap SnowMap) LoadTestSlope()
        {
            SlopeLayout layout = SlopeLayout.Parse(File.ReadAllText(SlopePath("TestSlope.txt")));
            Heightmap heightmap = Heightmap.FromR16(File.ReadAllBytes(SlopePath("TestSlope.raw")), layout.Spec);
            SnowMap snowMap = SnowMap.FromBytes(File.ReadAllBytes(SlopePath("TestSlopeSnow.raw")), heightmap);
            return (layout, heightmap, snowMap);
        }

        private static string SlopePath(string name)
        {
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Packages", "com.firstchair.sim", "package.json")))
            {
                directory = directory.Parent;
            }

            Assert.That(directory, Is.Not.Null, "no repository root above " + TestContext.CurrentContext.TestDirectory);
            return Path.Combine(directory.FullName, "Assets", "Terrain", "TestSlope", name);
        }
    }
}
