using System;
using System.Collections.Generic;
using System.IO;
using FirstChair.Sim;
using NUnit.Framework;

namespace FirstChair.Sim.Tests
{
    /// <summary>
    /// Story 006, rails and boxes: riding onto a low box and popping onto a high rail start a grind (GrindStart), riding
    /// off an end or falling off ends it (GrindEnd); steering moves the lean and a lean of 1 falls off; grinds score by
    /// time and balance; replay refuses other rails; and the layout format and the committed test slope carry them.
    /// Every ride runs straight down the +Z incline from (64, 20) m, over rails laid along x = 64 m.
    /// </summary>
    public class RailTests
    {
        private const int MaxSteps = 20 * Simulation.StepsPerSecond;
        private static readonly Heightmap Terrain = TestTerrain.InclineDownhillPlusZ();
        private static readonly SnowMap Snow = TestTerrain.Uniform(Terrain, SnowType.Groomed);
        private static readonly RunSetup Setup = new RunSetup(Sport.Skis, 0, 11UL, Fix64.FromInt(64), Fix64.FromInt(20));
        private static readonly SimInput NoInput = SimInput.Pack(SimButtons.None, 0);
        private static readonly SimInput JumpHeld = SimInput.Pack(SimButtons.Jump, 0);
        private static readonly SimInput RightHeld = SimInput.Pack(SimButtons.Right, 0);
        private static readonly SimInput LeftHeld = SimInput.Pack(SimButtons.Left, 0);

        // A 0.3 m box low enough to ride onto, from z = 30 m to 60 m; and a 0.6 m rail on the same line that needs a pop.
        private static readonly SlopeRail Box = new SlopeRail(64_000, 30_000, 64_000, 60_000, 300, 600, true);
        private static readonly SlopeRail HighRail = new SlopeRail(64_000, 21_000, 64_000, 60_000, 600, 80, false);

        [Test]
        public void RidingOntoALowBox_StartsAGrindOnItsTop_AndRidingOffTheEndEndsIt()
        {
            // Arrange
            Simulation simulation = Start(Steady(), Box);
            var events = new List<SimEvent>();

            // Act
            RideUntil(simulation, _ => NoInput, s => s.State.Grinding, events);
            RiderState onBox = simulation.State;
            RideUntil(simulation, _ => NoInput, s => !s.State.Grinding, events);

            // Assert
            SimEvent start = Single(events, SimEventKind.GrindStart);
            SimEvent end = Single(events, SimEventKind.GrindEnd);
            Terrain.Sample(onBox.Position.X, onBox.Position.Z, out Fix64 ground, out _);
            Assert.That(start.Rail, Is.EqualTo(0));
            Assert.That(start.Tick, Is.EqualTo(onBox.Tick));
            Assert.That(D(onBox.Position.Y - ground), Is.EqualTo(0.3).Within(0.01), "the rider rides on the box's top");
            Assert.That(Count(events, SimEventKind.Land), Is.Zero, "riding on is not a landing");
            Assert.That(end.Rail, Is.EqualTo(0));
            Assert.That(end.Grind.Fell, Is.False);
            Assert.That(end.Grind.Box, Is.True);
            Assert.That(end.Grind.Ticks, Is.EqualTo(end.Tick - start.Tick));
            Assert.That(end.Grind.Balance, Is.EqualTo(Fix64.One), "a straight entry with no wobble never leans");
            Assert.That(Count(events, SimEventKind.Crash), Is.Zero);
            Assert.That(simulation.State.Airborne, Is.False, "the rider steps down from a low box");
            Assert.That(D(simulation.State.Position.Z), Is.GreaterThan(60.0));
        }

        [Test]
        public void PopOntoAHighRail_LandsOnItThenGrinds_AndFliesOffTheEnd()
        {
            // Arrange
            Simulation simulation = Start(Steady(), HighRail);
            var events = new List<SimEvent>();
            for (int step = 0; step < 60; step++)
            {
                simulation.Step(JumpHeld);
                events.AddRange(simulation.Events);
            }

            Assert.That(Count(events, SimEventKind.GrindStart), Is.Zero, "a 0.6 m rail is too high to ride onto");

            // Act
            RideUntil(simulation, _ => NoInput, s => s.State.Grinding, events);
            RideUntil(simulation, _ => NoInput, s => !s.State.Grinding, events);

            // Assert: Land, then GrindStart in the same step; then GrindEnd and a takeoff from the rail's end.
            int land = events.FindIndex(e => e.Kind == SimEventKind.Land);
            int start = events.FindIndex(e => e.Kind == SimEventKind.GrindStart);
            int end = events.FindIndex(e => e.Kind == SimEventKind.GrindEnd);
            Assert.That(land, Is.GreaterThanOrEqualTo(0));
            Assert.That(start, Is.EqualTo(land + 1));
            Assert.That(events[start].Tick, Is.EqualTo(events[land].Tick));
            Assert.That(events[land].Grade, Is.EqualTo(LandingGrade.Clean));
            Assert.That(events[end].Grind.Fell, Is.False);
            Assert.That(events[end].Grind.Box, Is.False);
            Assert.That(events[end + 1].Kind, Is.EqualTo(SimEventKind.Takeoff));
            Assert.That(simulation.State.Airborne, Is.True, "the rider flies off the end of a high rail");
        }

        [Test]
        public void SteeringPushesTheLeanItsWay_AndHeldOneWayFallsOffIntoACrash()
        {
            // Arrange: no wobble and no tipping, so only steering moves the lean.
            Tuning tuning = Steady();
            tuning.GrindTipRate = Fix64.Zero;
            Simulation simulation = Start(tuning, Box);
            var events = new List<SimEvent>();
            RideUntil(simulation, _ => NoInput, s => s.State.Grinding, events);
            Fix64 perStep = tuning.GrindBalanceRate * Simulation.StepSeconds;

            // Act and Assert: right leans right by GrindBalanceRate per second, left brings it back.
            simulation.Step(RightHeld);
            Assert.That(simulation.State.Balance, Is.EqualTo(perStep));
            simulation.Step(LeftHeld);
            Assert.That(simulation.State.Balance, Is.EqualTo(Fix64.Zero));

            // Act: hold right until the lean reaches 1 (1 / (3 per second) = 40 steps).
            int steps = RideUntil(simulation, _ => RightHeld, s => !s.State.Grinding, events);

            // Assert
            Assert.That(steps, Is.InRange(40, 41));
            SimEvent end = Single(events, SimEventKind.GrindEnd);
            SimEvent crash = Single(events, SimEventKind.Crash);
            Assert.That(end.Grind.Fell, Is.True);
            Assert.That(crash.Cause, Is.EqualTo(CrashCause.RailFall));
            Assert.That(crash.Rail, Is.EqualTo(0));
            Assert.That(crash.Tick, Is.EqualTo(end.Tick));
            Assert.That(events.IndexOf(crash), Is.EqualTo(events.IndexOf(end) + 1));
            Assert.That(simulation.State.Crashed, Is.True);
            Terrain.Sample(simulation.State.Position.X, simulation.State.Position.Z, out Fix64 ground, out _);
            Assert.That(simulation.State.Position.Y, Is.EqualTo(ground), "the rider falls onto the snow");
        }

        [Test]
        public void CounterSteeringTheLean_HoldsTheDefaultWobbleToTheEnd_WhileNoInputUnderHeavyWobbleFallsOff()
        {
            // Arrange
            Simulation balanced = Start(Tuning.Default, Box);
            Tuning wobbly = Tuning.Default;
            wobbly.GrindWobble = Fix64.FromInt(20);
            Simulation unbalanced = Start(wobbly, Box);
            var balancedEvents = new List<SimEvent>();
            var unbalancedEvents = new List<SimEvent>();

            // Act: steer against the lean (left when leaning right) on the rail.
            Func<Simulation, SimInput> counterSteer = s => !s.State.Grinding ? NoInput
                : s.State.Balance > Fix64.Zero ? LeftHeld : s.State.Balance < Fix64.Zero ? RightHeld : NoInput;
            RideUntil(balanced, counterSteer, s => s.State.Grinding, balancedEvents);
            RideUntil(balanced, counterSteer, s => !s.State.Grinding, balancedEvents);
            RideUntil(unbalanced, _ => NoInput, s => s.State.Grinding, unbalancedEvents);
            RideUntil(unbalanced, _ => NoInput, s => !s.State.Grinding, unbalancedEvents);

            // Assert
            SimEvent held = Single(balancedEvents, SimEventKind.GrindEnd);
            Assert.That(held.Grind.Fell, Is.False);
            Assert.That(D(held.Grind.Balance), Is.GreaterThan(0.9));
            Assert.That(Single(unbalancedEvents, SimEventKind.GrindEnd).Grind.Fell, Is.True);
            Assert.That(Single(unbalancedEvents, SimEventKind.Crash).Cause, Is.EqualTo(CrashCause.RailFall));
        }

        [Test]
        public void GrindScore_IsPointsPerSecondTimesSecondsTimesBalance_HalvedByAFall()
        {
            // Arrange: 2 s at a balance of 0.75: 200 * 2 * 0.75 = 300.
            var grind = new GrindSummary(240, Fix64.FromRatio(3, 4), false, false);
            var fell = new GrindSummary(240, Fix64.FromRatio(3, 4), true, true);
            Tuning slower = Tuning.Default;
            slower.GrindPointsPerSecond = Fix64.FromInt(100);
            var score = new RunScore(Tuning.Default);

            // Act
            score.Add(SimEvent.GrindEnd(50, 0, grind));
            score.Add(SimEvent.GrindEnd(90, 1, fell));
            score.Add(SimEvent.GrindStart(95, 0));

            // Assert
            Assert.That(TrickScoring.Points(TrickScoring.GrindValue(grind, Tuning.Default), 0, Tuning.Default), Is.EqualTo(300));
            Assert.That(TrickScoring.Points(TrickScoring.GrindValue(grind, slower), 0, slower), Is.EqualTo(150));
            Assert.That(score.Tricks.Count, Is.EqualTo(2));
            Assert.That(score.Tricks[0].Name, Is.EqualTo("Rail Grind"));
            Assert.That(score.Tricks[0].Points, Is.EqualTo(300));
            Assert.That(score.Tricks[1].Name, Is.EqualTo("Box Slide"));
            Assert.That(score.Tricks[1].Points, Is.EqualTo(150));
            Assert.That(score.Total, Is.EqualTo(450L));
        }

        [Test]
        public void LiveGrind_FoldsToItsTimeTimesBalance()
        {
            // Arrange
            Simulation simulation = Start(Steady(), Box);
            var events = new List<SimEvent>();

            // Act
            RideUntil(simulation, _ => NoInput, s => s.State.Grinding, events);
            RideUntil(simulation, _ => NoInput, s => !s.State.Grinding, events);
            var score = new RunScore(Steady());
            score.AddAll(events);

            // Assert: balance 1, so 200 points per second on the box.
            SimEvent end = Single(events, SimEventKind.GrindEnd);
            Assert.That(score.Tricks.Count, Is.EqualTo(1));
            Assert.That(score.Total, Is.EqualTo((long)Math.Round(200.0 * end.Grind.Ticks / Simulation.StepsPerSecond)));
        }

        [Test]
        public void GrindRide_RecordedThenReplayed_MatchesEveryStepAndTheScore()
        {
            // Arrange: heavy wobble, so the run's random generator decides the lean.
            Tuning wobbly = Tuning.Default;
            wobbly.GrindWobble = Fix64.FromInt(20);
            var rails = new RailSet(new[] { Box });
            var simulation = new Simulation(Setup, Terrain, Snow, Forest.Empty, rails, wobbly);
            var recording = new Recording(Setup, Terrain, Snow, Forest.Empty, rails, wobbly);
            var live = new List<StepSnapshot>();
            var liveScore = new RunScore(wobbly);
            for (int step = 0; step < 6 * Simulation.StepsPerSecond; step++)
            {
                SimInput input = NoInput;
                recording.Append(input);
                simulation.Step(input);
                liveScore.AddAll(simulation.Events);
                live.Add(StepSnapshot.Of(simulation));
            }

            // Act
            Recording.TryRead(recording.ToBytes(), out Recording loaded);
            var replayed = new List<StepSnapshot>();
            ReplayResult result = Replayer.Replay(loaded, Terrain, Snow, Forest.Empty, new RailSet(new[] { Box }), wobbly, s => replayed.Add(StepSnapshot.Of(s)));

            // Assert
            Assert.That(result.Status, Is.EqualTo(ReplayStatus.Ok));
            Assert.That(live.Exists(s => Array.Exists(s.Events, e => e.Kind == SimEventKind.GrindEnd)), Is.True, "the ride must grind");
            Assert.That(replayed, Is.EqualTo(live));
            Assert.That(result.TotalScore, Is.EqualTo(liveScore.Total));
        }

        [Test]
        public void Replay_OnDifferentRails_ReportsRailsMismatchAndRunsNoSteps()
        {
            // Arrange
            var recording = new Recording(Setup, Terrain, Snow, Forest.Empty, new RailSet(new[] { Box }), Tuning.Default);
            for (int step = 0; step < 120; step++)
            {
                recording.Append(NoInput);
            }

            int stepsSeen = 0;

            // Act
            ReplayResult other = Replayer.Replay(recording, Terrain, Snow, Forest.Empty, new RailSet(new[] { HighRail }), Tuning.Default, _ => stepsSeen++);
            ReplayResult none = Replayer.Replay(recording, Terrain, Snow, Forest.Empty, Tuning.Default, _ => stepsSeen++);
            ReplayResult same = Replayer.Replay(recording, Terrain, Snow, Forest.Empty, new RailSet(new[] { Box }), Tuning.Default);

            // Assert
            Assert.That(other.Status, Is.EqualTo(ReplayStatus.RailsMismatch));
            Assert.That(none.Status, Is.EqualTo(ReplayStatus.RailsMismatch));
            Assert.That(other.StepsRun, Is.EqualTo(0));
            Assert.That(stepsSeen, Is.EqualTo(0));
            Assert.That(same.Status, Is.EqualTo(ReplayStatus.Ok));
        }

        [Test]
        public void Parse_RailAndBoxRecords_ReadInFileOrder()
        {
            // Arrange
            const string text = "format 1\nspec 100000 100000 0 50000\nbackground groomed\nstart 10000 10000\n"
                + "rail 1000 2000 1000 9000 500 80\nbox 3000 2000 4000 9000 300 600\n";

            // Act
            SlopeLayout layout = SlopeLayout.Parse(text);

            // Assert
            Assert.That(layout.Rails.Count, Is.EqualTo(2));
            Assert.That(layout.Rails[0].Box, Is.False);
            Assert.That(layout.Rails[1].Box, Is.True);
            Assert.That(
                new[] { layout.Rails[1].X0Mm, layout.Rails[1].Z0Mm, layout.Rails[1].X1Mm, layout.Rails[1].Z1Mm, layout.Rails[1].HeightMm, layout.Rails[1].WidthMm },
                Is.EqualTo(new[] { 3000, 2000, 4000, 9000, 300, 600 }));
            Assert.That(RailSet.FromLayout(layout).Count, Is.EqualTo(2));
        }

        [TestCase("rail 1000 2000 1000 2000 500 80")]
        [TestCase("box 1000 2000 1000 9000 0 600")]
        [TestCase("rail 1000 2000 1000 9000 500")]
        [TestCase("box 1000 2000 1000 900000 300 600")]
        public void Parse_BadRailRecord_Throws(string record)
        {
            string text = "format 1\nspec 100000 100000 0 50000\nbackground groomed\nstart 10000 10000\n" + record + "\n";
            Assert.That(() => SlopeLayout.Parse(text), Throws.ArgumentException);
        }

        [TestCase(0, 500, 300, 600)]
        [TestCase(0, 9000, 0, 600)]
        [TestCase(0, 9000, 300, 0)]
        public void NewRailSet_RailTooShortOrWithoutHeightOrWidth_Throws(int x1, int z1, int height, int width)
        {
            Assert.That(() => new RailSet(new[] { new SlopeRail(0, 0, x1, z1, height, width, false) }), Throws.ArgumentException);
        }

        [Test]
        public void CommittedTestSlope_HasRailsAndBoxesOnTheKickerLine()
        {
            // Arrange
            SlopeLayout layout = SlopeLayout.Parse(File.ReadAllText(SlopeFile("TestSlope.txt")));

            // Act
            RailSet rails = RailSet.FromLayout(layout);

            // Assert
            Assert.That(rails.Count, Is.GreaterThanOrEqualTo(3));
            Assert.That(layout.Rails, Has.Some.Matches<SlopeRail>(r => r.Box).And.Some.Matches<SlopeRail>(r => !r.Box));
            Assert.That(layout.TryGetZone("park", out SlopeZone park), Is.True);
            foreach (SlopeRail rail in layout.Rails)
            {
                Assert.That(park.Contains(rail.X0, rail.Z0) && park.Contains(rail.X1, rail.Z1), Is.True, "rails stand in the park");
                Assert.That(layout.Kickers, Has.Some.Matches<SlopeKicker>(k => k.XMm == rail.X0Mm && k.XMm == rail.X1Mm), "on the kicker line");
            }

            Assert.That(RailSet.FromLayout(layout).Hash, Is.EqualTo(rails.Hash));
        }

        [TestCase("GrindCaptureDistance negative")]
        [TestCase("GrindStepUpHeight negative")]
        [TestCase("GrindMinSpeed negative")]
        [TestCase("GrindDrag negative")]
        [TestCase("GrindWobble too large")]
        [TestCase("GrindPointsPerSecond negative")]
        [TestCase("GrindFallFactor above 1")]
        public void NewRun_GrindTuningARideCannotRunOn_ThrowsArgumentException(string badValue)
        {
            // Arrange
            Tuning tuning = Tuning.Default;
            switch (badValue)
            {
                case "GrindCaptureDistance negative": tuning.GrindCaptureDistance = -Fix64.One; break;
                case "GrindStepUpHeight negative": tuning.GrindStepUpHeight = -Fix64.One; break;
                case "GrindMinSpeed negative": tuning.GrindMinSpeed = -Fix64.One; break;
                case "GrindDrag negative": tuning.GrindDrag = -Fix64.One; break;
                case "GrindWobble too large": tuning.GrindWobble = Fix64.FromInt(2000); break;
                case "GrindPointsPerSecond negative": tuning.GrindPointsPerSecond = -Fix64.One; break;
                case "GrindFallFactor above 1": tuning.GrindFallFactor = Fix64.FromInt(2); break;
            }

            // Act and Assert
            Assert.That(() => Start(tuning, Box), Throws.ArgumentException);
        }

        private static Tuning Steady()
        {
            Tuning tuning = Tuning.Default;
            tuning.GrindWobble = Fix64.Zero;
            return tuning;
        }

        private static Simulation Start(Tuning tuning, params SlopeRail[] rails) =>
            new Simulation(Setup, Terrain, Snow, Forest.Empty, new RailSet(rails), tuning);

        private static int RideUntil(Simulation simulation, Func<Simulation, SimInput> input, Func<Simulation, bool> done, List<SimEvent> events)
        {
            int steps = 0;
            while (!done(simulation) && steps < MaxSteps)
            {
                simulation.Step(input(simulation));
                events.AddRange(simulation.Events);
                steps++;
            }

            Assert.That(done(simulation), Is.True, "the ride never got there");
            return steps;
        }

        private static SimEvent Single(List<SimEvent> events, SimEventKind kind)
        {
            List<SimEvent> found = events.FindAll(e => e.Kind == kind);
            Assert.That(found.Count, Is.EqualTo(1), "expected one " + kind + " in [" + string.Join(", ", events) + "]");
            return found[0];
        }

        private static int Count(List<SimEvent> events, SimEventKind kind) => events.FindAll(e => e.Kind == kind).Count;

        private static double D(Fix64 value) => TestTerrain.ToDouble(value);

        private static string SlopeFile(string name)
        {
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "Assets", "Terrain", "TestSlope")))
            {
                directory = directory.Parent;
            }

            Assert.That(directory, Is.Not.Null, "the repo's Assets/Terrain/TestSlope was not found above the test directory");
            return Path.Combine(directory.FullName, "Assets", "Terrain", "TestSlope", name);
        }
    }
}
