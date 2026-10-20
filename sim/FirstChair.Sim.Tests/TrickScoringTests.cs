using System;
using System.Collections.Generic;
using FirstChair.Sim;
using NUnit.Framework;

namespace FirstChair.Sim.Tests
{
    /// <summary>
    /// Story 006, trick naming and scoring: the name, ID and points of fixed flights (<see cref="TrickScoring"/>), the run's
    /// repeat decay and total (<see cref="RunScore"/>), and live jumps whose events fold to the expected tricks, switch
    /// included. Expected points are worked by hand from the story's formula with the default tuning,
    /// (100 h + 450 f + g + 60 t) * q * (1 + 0.5 Flow); a tuning variant checks the coefficients are read from
    /// <see cref="Tuning"/>.
    /// </summary>
    public class TrickScoringTests
    {
        private const int FlightSteps = 10 * Simulation.StepsPerSecond;
        private static readonly SimInput NoInput = SimInput.Pack(SimButtons.None, 0);
        private static readonly SimInput JumpHeld = SimInput.Pack(SimButtons.Jump, 0);

        [Test]
        public void Left720Japan_CleanLanding_IsNamedLeft720JapanWithAKebabIdAndScoredByTheFormula()
        {
            // Arrange: h = 4, g = 150 + 300 * 0.5 s = 300, t = 1.2 s, clean: (400 + 300 + 72) * 1.5 = 1158.
            AirSummary air = Air(144, halfTurns: -4, grab: GrabKind.Japan, grabTicks: 60);

            // Act
            string name = TrickScoring.Name(air, Tuning.Default);

            // Assert
            Assert.That(name, Is.EqualTo("Left 720 Japan"));
            Assert.That(TrickScoring.Id(name), Is.EqualTo("left-720-japan"));
            Assert.That(Score(LandingGrade.Clean, air, Tuning.Default), Is.EqualTo(1158));
        }

        [TestCase(-1, 0, "Backflip", "backflip")]
        [TestCase(1, 0, "Frontflip", "frontflip")]
        [TestCase(-2, 0, "Double Backflip", "double-backflip")]
        [TestCase(3, 0, "Triple Frontflip", "triple-frontflip")]
        [TestCase(-1, 2, "Backflip Right 360", "backflip-right-360")]
        [TestCase(0, -1, "Left 180", "left-180")]
        [TestCase(0, 5, "Right 900", "right-900")]
        [TestCase(0, 0, "Straight Air", "straight-air")]
        public void FlipsAndSpin_AreNamedFlipsFirstThenSpin(int flips, int halfTurns, string expectedName, string expectedId)
        {
            // Arrange
            AirSummary air = Air(60, halfTurns: halfTurns, flips: flips);

            // Act
            string name = TrickScoring.Name(air, Tuning.Default);

            // Assert
            Assert.That(name, Is.EqualTo(expectedName));
            Assert.That(TrickScoring.Id(name), Is.EqualTo(expectedId));
        }

        [Test]
        public void SwitchTakeoff_NameStartsWithSwitch_AndScoresTheSame()
        {
            // Arrange
            AirSummary riding = Air(120, halfTurns: 2, flips: -1, grab: GrabKind.Mute, grabTicks: 30);
            AirSummary switched = Air(120, halfTurns: 2, flips: -1, grab: GrabKind.Mute, grabTicks: 30, switchTakeoff: true);

            // Act
            string name = TrickScoring.Name(switched, Tuning.Default);

            // Assert
            Assert.That(name, Is.EqualTo("Switch Backflip Right 360 Mute"));
            Assert.That(TrickScoring.Id(name), Is.EqualTo("switch-backflip-right-360-mute"));
            Assert.That(Score(LandingGrade.Solid, switched, Tuning.Default), Is.EqualTo(Score(LandingGrade.Solid, riding, Tuning.Default)));
        }

        // 1 s of air (60) plus the grab: 17 steps is under 0.15 s and earns nothing; 18 steps is 0.15 s: 150 + 45.
        [TestCase(17, "Straight Air", 60)]
        [TestCase(18, "Japan", 255)]
        public void Grab_UnderTheMinimumEarnsNothingAndIsNotNamed_AtItCounts(int grabTicks, string expectedName, int expectedPoints)
        {
            // Arrange
            AirSummary air = Air(120, grab: GrabKind.Japan, grabTicks: grabTicks);

            // Act and Assert
            Assert.That(TrickScoring.Name(air, Tuning.Default), Is.EqualTo(expectedName));
            Assert.That(Score(LandingGrade.Solid, air, Tuning.Default), Is.EqualTo(expectedPoints));
        }

        // 1 s of air (60) plus the grab: 1.25 s earns 150 + 375 = 525; 1.5 s reaches the 600 cap; 2 s stays at it.
        [TestCase(150, 585)]
        [TestCase(180, 660)]
        [TestCase(240, 660)]
        public void GrabBonus_GrowsPerSecondHeld_AndCapsAt600(int grabTicks, int expectedPoints)
        {
            // Arrange
            AirSummary air = Air(120, grab: GrabKind.Safety, grabTicks: grabTicks);

            // Act and Assert
            Assert.That(Score(LandingGrade.Solid, air, Tuning.Default), Is.EqualTo(expectedPoints));
            Assert.That(TrickScoring.GrabBonus(air, Tuning.Default) <= Tuning.Default.GrabPointsCap, Is.True);
        }

        // A backflip with 0.5 s of air is 450 + 30 = 480, times q.
        [TestCase(LandingGrade.Clean, 720)]
        [TestCase(LandingGrade.Solid, 480)]
        [TestCase(LandingGrade.Sketchy, 288)]
        public void EachLandingGrade_MultipliesByItsQualityFactor(LandingGrade grade, int expectedPoints)
        {
            Assert.That(Score(grade, Air(60, flips: -1), Tuning.Default), Is.EqualTo(expectedPoints));
        }

        // 480 times 1 + 0.5 * Flow.
        [TestCase(0, 1, 480)]
        [TestCase(1, 2, 600)]
        [TestCase(1, 1, 720)]
        public void FlowAtTouchdown_MultipliesThePoints(int flowNumerator, int flowDenominator, int expectedPoints)
        {
            // Arrange
            AirSummary air = Air(60, flips: -1, flow: Fix64.FromRatio(flowNumerator, flowDenominator));

            // Act and Assert
            Assert.That(Score(LandingGrade.Solid, air, Tuning.Default), Is.EqualTo(expectedPoints));
        }

        [Test]
        public void Coefficients_AreReadFromTuning()
        {
            // Arrange
            Tuning tuning = Tuning.Default;
            tuning.TrickPointsPerFlip = Fix64.FromInt(1000);
            tuning.TrickPointsPerHalfTurn = Fix64.FromInt(50);
            tuning.CleanTrickFactor = Fix64.FromInt(2);
            tuning.FlowTrickBonus = Fix64.One;

            // Act and Assert: (1000 + 30) * 1; (100 + 30) * 1; (1000 + 30) * 2; (1000 + 30) * (1 + 1 * 1).
            Assert.That(Score(LandingGrade.Solid, Air(60, flips: -1), tuning), Is.EqualTo(1030));
            Assert.That(Score(LandingGrade.Solid, Air(60, halfTurns: -2), tuning), Is.EqualTo(130));
            Assert.That(Score(LandingGrade.Clean, Air(60, flips: -1), tuning), Is.EqualTo(2060));
            Assert.That(Score(LandingGrade.Solid, Air(60, flips: -1, flow: Fix64.One), tuning), Is.EqualTo(2060));
        }

        [Test]
        public void RepeatedTrick_ScoresHalfAsMuchEachTime_AndTheTotalSumsEveryTrick()
        {
            // Arrange: backflip (480), left 360 (200 + 30 = 230), backflip again, and again.
            var score = new RunScore(Tuning.Default);
            AirSummary backflip = Air(60, flips: -1);

            // Act
            score.Add(SimEvent.Land(10, LandingGrade.Solid, backflip));
            score.Add(SimEvent.Land(20, LandingGrade.Solid, Air(60, halfTurns: -2)));
            score.Add(SimEvent.Land(30, LandingGrade.Solid, backflip));
            score.Add(SimEvent.Land(40, LandingGrade.Solid, backflip));

            // Assert
            Assert.That(score.Tricks.Count, Is.EqualTo(4));
            Assert.That(new[] { score.Tricks[0].Points, score.Tricks[1].Points, score.Tricks[2].Points, score.Tricks[3].Points },
                Is.EqualTo(new[] { 480, 230, 240, 120 }));
            Assert.That(new[] { score.Tricks[0].Repeat, score.Tricks[1].Repeat, score.Tricks[2].Repeat, score.Tricks[3].Repeat },
                Is.EqualTo(new[] { 0, 0, 1, 2 }));
            Assert.That(score.Tricks[1].Name, Is.EqualTo("Left 360"));
            Assert.That(score.TimesLanded("backflip"), Is.EqualTo(3));
            Assert.That(score.Total, Is.EqualTo(480L + 230 + 240 + 120));
        }

        [Test]
        public void Crash_KeepsEverythingAlreadyScored_AndScoresNothing()
        {
            // Arrange
            var score = new RunScore(Tuning.Default);
            score.Add(SimEvent.Land(10, LandingGrade.Clean, Air(60, flips: -1)));

            // Act: a landing crash with a big flight, a tree crash and a rail fall.
            score.Add(SimEvent.Crash(20, CrashCause.OverRotation, Air(240, halfTurns: 8, flips: -3)));
            score.Add(SimEvent.TreeCrash(30, 0, Fix64.FromInt(10)));
            score.Add(SimEvent.RailCrash(40, 0, Fix64.FromInt(5)));

            // Assert
            Assert.That(score.Tricks.Count, Is.EqualTo(1));
            Assert.That(score.Total, Is.EqualTo(720L));
        }

        [TestCase("TrickPointsPerHalfTurn negative")]
        [TestCase("TrickPointsPerAirSecond too large")]
        [TestCase("GrabPointsCap negative")]
        [TestCase("GrabMinTime negative")]
        [TestCase("SketchyTrickFactor negative")]
        [TestCase("TrickRepeatFactor above 1")]
        public void ScoringTuningARunCannotUse_ThrowsArgumentException(string badValue)
        {
            // Arrange
            Tuning tuning = Tuning.Default;
            switch (badValue)
            {
                case "TrickPointsPerHalfTurn negative": tuning.TrickPointsPerHalfTurn = -Fix64.One; break;
                case "TrickPointsPerAirSecond too large": tuning.TrickPointsPerAirSecond = Fix64.FromInt(2_000_000); break;
                case "GrabPointsCap negative": tuning.GrabPointsCap = -Fix64.One; break;
                case "GrabMinTime negative": tuning.GrabMinTime = -Fix64.One; break;
                case "SketchyTrickFactor negative": tuning.SketchyTrickFactor = -Fix64.One; break;
                case "TrickRepeatFactor above 1": tuning.TrickRepeatFactor = Fix64.FromInt(2); break;
            }

            Heightmap terrain = TestTerrain.Flat();

            // Act and Assert
            Assert.That(() => new RunScore(tuning), Throws.ArgumentException);
            Assert.That(
                () => new Simulation(new RunSetup(Sport.Skis, 0, 1UL, Fix64.FromInt(64), Fix64.FromInt(64)), terrain, TestTerrain.Uniform(terrain, SnowType.Groomed), tuning),
                Throws.ArgumentException);
        }

        [Test]
        public void PoppedLeft360OnIncline_FoldsToOneLeft360ScoredFromItsLanding()
        {
            // Arrange
            Simulation simulation = StartIncline();
            var events = new List<SimEvent>();
            Run(simulation, NoInput, 60, events);
            Run(simulation, JumpHeld, 60, events);

            // Act: the release step pops; then 96 air steps of left spin (one turn at 2.5 pi rad/s).
            Fly(simulation, step => step >= 1 && step <= 96 ? SimInput.Pack(SimButtons.Left, 0) : NoInput, events);
            var score = new RunScore(Tuning.Default);
            score.AddAll(events);

            // Assert
            SimEvent land = events.Find(e => e.Kind == SimEventKind.Land);
            Assert.That(land.Kind, Is.EqualTo(SimEventKind.Land));
            Assert.That(land.Air.Switch, Is.False);
            Assert.That(score.Tricks.Count, Is.EqualTo(1));
            Assert.That(score.Tricks[0].Name, Is.EqualTo("Left 360"));
            Assert.That(score.Tricks[0].Points, Is.EqualTo(TrickScoring.Points(TrickScoring.JumpValue(land.Grade, land.Air, Tuning.Default), 0, Tuning.Default)));
            Assert.That(score.Total, Is.EqualTo((long)score.Tricks[0].Points));
        }

        [Test]
        public void LandedRight180ThenPoppedAgain_TheSecondJumpIsSwitch()
        {
            // Arrange
            Simulation simulation = StartIncline();
            var events = new List<SimEvent>();
            Run(simulation, NoInput, 60, events);
            Run(simulation, JumpHeld, 60, events);

            // Act: a half turn (48 air steps), land backward, ride, pop again with no input.
            Fly(simulation, step => step >= 1 && step <= 48 ? SimInput.Pack(SimButtons.Right, 0) : NoInput, events);
            Run(simulation, NoInput, 30, events);
            Run(simulation, JumpHeld, 60, events);
            Fly(simulation, _ => NoInput, events);
            var score = new RunScore(Tuning.Default);
            score.AddAll(events);

            // Assert
            List<SimEvent> lands = events.FindAll(e => e.Kind == SimEventKind.Land);
            Assert.That(lands.Count, Is.EqualTo(2));
            Assert.That(lands[0].Air.Switch, Is.False);
            Assert.That(lands[1].Air.Switch, Is.True, "riding backward at the second takeoff is switch");
            Assert.That(score.Tricks[0].Name, Is.EqualTo("Right 180"));
            Assert.That(score.Tricks[1].Name, Is.EqualTo("Switch Straight Air"));
        }

        private static AirSummary Air(
            int airTicks, int halfTurns = 0, int flips = 0, GrabKind grab = GrabKind.None, int grabTicks = 0, bool switchTakeoff = false, Fix64 flow = default) =>
            new AirSummary(
                airTicks, Fix64.One, Fix64.Pi * Fix64.FromInt(halfTurns), Fix64.TwoPi * Fix64.FromInt(flips), grab, grabTicks, Fix64.Zero, switchTakeoff, flow);

        private static int Score(LandingGrade grade, AirSummary air, Tuning tuning) =>
            TrickScoring.Points(TrickScoring.JumpValue(grade, air, tuning), 0, tuning);

        private static Simulation StartIncline()
        {
            Heightmap terrain = TestTerrain.InclineDownhillPlusZ();
            return new Simulation(
                new RunSetup(Sport.Skis, 0, 7UL, Fix64.FromInt(64), Fix64.FromInt(20)), terrain, TestTerrain.Uniform(terrain, SnowType.Groomed), Tuning.Default);
        }

        private static void Run(Simulation simulation, SimInput input, int steps, List<SimEvent> events)
        {
            for (int step = 0; step < steps; step++)
            {
                simulation.Step(input);
                events.AddRange(simulation.Events);
            }
        }

        private static void Fly(Simulation simulation, Func<int, SimInput> input, List<SimEvent> events)
        {
            int step = 0;
            do
            {
                simulation.Step(input(step++));
                events.AddRange(simulation.Events);
            }
            while ((simulation.State.Airborne || step == 1) && step < FlightSteps);

            Assert.That(simulation.State.Airborne, Is.False, "the flight must end");
        }
    }
}
