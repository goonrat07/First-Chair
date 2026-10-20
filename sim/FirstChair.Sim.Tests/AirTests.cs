using System;
using System.Collections.Generic;
using System.IO;
using FirstChair.Sim;
using NUnit.Framework;

namespace FirstChair.Sim.Tests
{
    /// <summary>
    /// Story 004 scripted-input tests: pops, takeoff off shaped terrain, spins, flips, grabs, graded landings, crashes
    /// and reset, and the data their events carry. Expectations come from <see cref="Tuning"/>; tuning variants check
    /// that the logic reads its numbers from there.
    /// </summary>
    public class AirTests
    {
        private const int MaxAirSteps = 5 * Simulation.StepsPerSecond;
        private const int SlideSteps = 60;
        private const int InAirSteps = 20;

        // The in-memory kicker, shaped like the test slope's: the slope falls 0.3 toward +Z on 0.5 m cells, a ramp
        // h = H (s / L)^2 rises from its foot to the lip, and behind the lip it falls back to the slope over 2 H along a
        // smoothstep (tools/terrain/make_test_slope.py).
        private const int KickerResolution = 129;
        private const int KickerSizeMm = 64_000;
        private const int KickerTopMm = 40_000;
        private const double KickerCellM = 0.5;
        private const double KickerGradient = 0.3;
        private const double KickerFootZ = 25.0;
        private const double KickerLengthM = 5.0;
        private const double KickerHeightM = 1.2;
        private const double KickerLipZ = KickerFootZ + KickerLengthM;
        private const double LipToleranceM = 1.0;
        private const double RunUpM = 25.0;

        private static readonly SimInput NoInput = SimInput.Pack(SimButtons.None, 0);
        private static readonly SimInput Forward = SimInput.Pack(SimButtons.Forward, 0);
        private static readonly SimInput JumpHeld = SimInput.Pack(SimButtons.Jump, 0);
        private static readonly SimInput ResetHeld = SimInput.Pack(SimButtons.Reset, 0);

        [Test]
        public void StraightJumpOffKicker_NoInputInAir_TakesOffAtTheLipAndLandsClean()
        {
            // Arrange
            Heightmap terrain = KickerSlope();
            var simulation = new Simulation(
                new RunSetup(Sport.Skis, 0, 1UL, Fix64.FromInt(32), Fix64.FromInt(2)), terrain, TestTerrain.Uniform(terrain, SnowType.Groomed), Tuning.Default);
            var events = new List<SimEvent>();

            // Act: tuck straight down into the kicker, then fly with no input.
            RiderState takeoffState = RideUntilAirborne(simulation, Forward, events);
            FlyUntilDown(simulation, _ => NoInput, events);

            // Assert
            Assert.That(D(takeoffState.Position.Z), Is.EqualTo(KickerLipZ).Within(LipToleranceM), "the rider leaves the snow at the lip");
            SimEvent takeoff = Single(events, SimEventKind.Takeoff);
            SimEvent land = Single(events, SimEventKind.Land);
            Assert.That(takeoff.Tick, Is.EqualTo(takeoffState.Tick));
            Assert.That(takeoff.Speed, Is.EqualTo(Fix64.Sqrt(Vec3.Dot(takeoffState.Velocity, takeoffState.Velocity))));
            Assert.That(land.Grade, Is.EqualTo(LandingGrade.Clean));
            Assert.That(land.Air.AirTicks, Is.GreaterThan(Simulation.StepsPerSecond / 4), "a real flight, not a hop");
            Assert.That(D(land.Air.MaxHeight), Is.GreaterThan(KickerHeightM / 2));
            Assert.That(land.Air.Spin, Is.EqualTo(Fix64.Zero));
            Assert.That(land.Air.Flip, Is.EqualTo(Fix64.Zero));
            Assert.That(Count(events, SimEventKind.Crash), Is.Zero);
            Assert.That(simulation.State.Airborne, Is.False);
            Assert.That(Speed(simulation.State), Is.GreaterThan(1.0), "a clean landing rides on");
        }

        [Test]
        public void EachCommittedTestSlopeKicker_RiddenStraight_LaunchesAtItsLipAndLandsClean()
        {
            // Arrange
            (SlopeLayout layout, Heightmap heightmap, SnowMap snowMap) = LoadTestSlope();
            Assert.That(layout.Kickers, Is.Not.Empty);

            foreach (SlopeKicker kicker in layout.Kickers)
            {
                Fix64 forwardX = Fix64.Sin(kicker.Facing);
                Fix64 forwardZ = Fix64.Cos(kicker.Facing);
                Fix64 back = kicker.Length + Fix64.FromRatio((long)(RunUpM * 1000), 1000);
                var setup = new RunSetup(Sport.Skis, 0, 3UL, kicker.X - forwardX * back, kicker.Z - forwardZ * back);
                var simulation = new Simulation(setup, heightmap, snowMap, Tuning.Default);
                var events = new List<SimEvent>();

                // Act: tuck into the kicker from 25 m before its foot; fly with no input.
                RiderState takeoffState = RideUntilAirborne(simulation, Forward, events);
                FlyUntilDown(simulation, _ => NoInput, events);

                // Assert: the takeoff came from the terrain's shape at the lip, nothing keyed to the kicker list.
                string name = "kicker at (" + kicker.XMm + ", " + kicker.ZMm + ")";
                double along = D((takeoffState.Position.X - kicker.X) * forwardX + (takeoffState.Position.Z - kicker.Z) * forwardZ);
                Assert.That(along, Is.EqualTo(0.0).Within(LipToleranceM), name + ": leaves the snow at the lip");
                SimEvent land = Single(events, SimEventKind.Land);
                Assert.That(land.Grade, Is.EqualTo(LandingGrade.Clean), name);
                Assert.That(D(land.Air.MaxHeight), Is.GreaterThan(D(kicker.Height) / 2), name);
            }
        }

        [Test]
        public void StraightPop_OnIncline_NoInputInAir_LandsClean()
        {
            // Arrange
            Tuning tuning = Tuning.Default;
            Simulation simulation = StartIncline(tuning);
            var events = new List<SimEvent>();
            Ride(simulation, NoInput, SlideSteps, events);

            // Act
            LoadAndPop(simulation, FullLoadSteps(tuning), events);
            FlyUntilDown(simulation, _ => NoInput, events);

            // Assert
            SimEvent land = Single(events, SimEventKind.Land);
            Assert.That(land.Grade, Is.EqualTo(LandingGrade.Clean));
            Assert.That(land.Air.LandingAngle, Is.EqualTo(Fix64.Zero));
            Assert.That(Count(events, SimEventKind.Crash), Is.Zero);
        }

        [TestCase(SimButtons.Left, -1)]
        [TestCase(SimButtons.Right, 1)]
        public void Timed360_SpinHeldForOneTurn_LandsClean(SimButtons spin, int sign)
        {
            // Arrange
            Tuning tuning = Tuning.Default;
            Simulation simulation = StartIncline(tuning);
            var events = new List<SimEvent>();
            Ride(simulation, NoInput, SlideSteps, events);
            int spinSteps = RotationSteps(tuning, spin, 360);
            double oneStep = D(tuning.SpinRate * Simulation.StepSeconds);

            // Act
            LoadAndPop(simulation, FullLoadSteps(tuning), events);
            Fix64 takeoffHeading = simulation.State.Heading;
            FlyUntilDown(simulation, step => step < spinSteps ? SimInput.Pack(spin, 0) : NoInput, events);

            // Assert
            SimEvent land = Single(events, SimEventKind.Land);
            Assert.That(land.Air.AirTicks, Is.GreaterThan(spinSteps), "the spin must finish in the air");
            Assert.That(land.Grade, Is.EqualTo(LandingGrade.Clean));
            Assert.That(D(land.Air.Spin), Is.EqualTo(sign * 2 * Math.PI).Within(oneStep / 2 + 1e-9));
            Assert.That(D(land.Air.LandingAngle), Is.LessThanOrEqualTo(oneStep / 2 + 1e-9));
            Assert.That(D(Fix64.WrapAngle(simulation.State.Heading - takeoffHeading)), Is.EqualTo(0.0).Within(oneStep));
            Assert.That(Count(events, SimEventKind.Crash), Is.Zero);
        }

        [TestCase(SimButtons.Right, 240, CrashCause.OverRotation)]
        [TestCase(SimButtons.Left, 240, CrashCause.OverRotation)]
        [TestCase(SimButtons.Right, 120, CrashCause.UnderRotation)]
        [TestCase(SimButtons.Forward, 60, CrashCause.OverRotation)]
        [TestCase(SimButtons.Forward, 270, CrashCause.UnderRotation)]
        [TestCase(SimButtons.Back, 60, CrashCause.OverRotation)]
        [TestCase(SimButtons.Back, 270, CrashCause.UnderRotation)]
        public void UnfinishedRotation_PastSketchyLimit_CrashesWithCause(SimButtons rotate, int degrees, CrashCause cause)
        {
            // Arrange
            Tuning tuning = Tuning.Default;
            Simulation simulation = StartFlat(tuning);
            var events = new List<SimEvent>();
            int steps = RotationSteps(tuning, rotate, degrees);

            // Act
            LoadAndPop(simulation, FullLoadSteps(tuning), events);
            FlyUntilDown(simulation, step => step < steps ? SimInput.Pack(rotate, 0) : NoInput, events);

            // Assert
            SimEvent crash = Single(events, SimEventKind.Crash);
            Assert.That(crash.Air.AirTicks, Is.GreaterThan(steps), "the rotation must finish in the air");
            Assert.That(crash.Cause, Is.EqualTo(cause));
            Assert.That(crash.Air.LandingAngle.Raw, Is.GreaterThan(tuning.SketchyLandingAngle.Raw));
            Assert.That(Count(events, SimEventKind.Land), Is.Zero);
            Assert.That(simulation.State.Crashed, Is.True);
            Assert.That(simulation.State.Velocity, Is.EqualTo(Vec3.Zero), "a crash stops riding");
        }

        [TestCase(2, LandingGrade.Clean)]
        [TestCase(6, LandingGrade.Solid)]
        [TestCase(10, LandingGrade.Sketchy)]
        public void SpinOffByAFewSteps_GradesByTheTuningThresholds(int spinSteps, LandingGrade expected)
        {
            // Arrange
            Tuning tuning = Tuning.Default;
            Simulation simulation = StartFlat(tuning);
            var events = new List<SimEvent>();
            Fix64 expectedAngle = Fix64.Zero;
            for (int i = 0; i < spinSteps; i++)
            {
                expectedAngle = expectedAngle + tuning.SpinRate * Simulation.StepSeconds;
            }

            // Act
            LoadAndPop(simulation, FullLoadSteps(tuning), events);
            FlyUntilDown(simulation, step => step < spinSteps ? SimInput.Pack(SimButtons.Right, 0) : NoInput, events);

            // Assert
            SimEvent land = Single(events, SimEventKind.Land);
            Assert.That(land.Air.LandingAngle, Is.EqualTo(expectedAngle));
            Assert.That(land.Grade, Is.EqualTo(expected));
            Assert.That(Count(events, SimEventKind.Crash), Is.Zero);
        }

        [Test]
        public void LandingThresholds_ReadFromTuning_WiderCleanGradesASketchyAngleClean()
        {
            // Arrange: every threshold at 40 degrees; a 37.5 degree spin is sketchy with the defaults.
            Tuning tuning = Tuning.Default;
            tuning.CleanLandingAngle = Fix64.Pi * Fix64.FromRatio(40, 180);
            tuning.SolidLandingAngle = tuning.CleanLandingAngle;
            tuning.SketchyLandingAngle = tuning.CleanLandingAngle;
            Simulation simulation = StartFlat(tuning);
            var events = new List<SimEvent>();

            // Act
            LoadAndPop(simulation, FullLoadSteps(tuning), events);
            FlyUntilDown(simulation, step => step < 10 ? SimInput.Pack(SimButtons.Right, 0) : NoInput, events);

            // Assert
            Assert.That(Single(events, SimEventKind.Land).Grade, Is.EqualTo(LandingGrade.Clean));
        }

        [TestCase(SimButtons.None, GrabKind.Mute)]
        [TestCase(SimButtons.Forward, GrabKind.Tip)]
        [TestCase(SimButtons.Back, GrabKind.Tail)]
        [TestCase(SimButtons.Left, GrabKind.Japan)]
        [TestCase(SimButtons.Right, GrabKind.Safety)]
        public void GrabPlusDirection_InTheAir_HoldsItsGrabWithoutRotating(SimButtons direction, GrabKind expected)
        {
            // Arrange
            const int grabSteps = 30;
            Tuning tuning = Tuning.Default;
            Simulation simulation = StartFlat(tuning);
            var events = new List<SimEvent>();
            var held = new List<GrabKind>();

            // Act
            LoadAndPop(simulation, FullLoadSteps(tuning), events);
            FlyUntilDown(simulation, step => step < grabSteps ? SimInput.Pack(SimButtons.Grab | direction, 0) : NoInput, events, held);

            // Assert
            Assert.That(held.GetRange(0, grabSteps), Is.All.EqualTo(expected));
            Assert.That(held[grabSteps], Is.EqualTo(GrabKind.None), "letting go ends the grab");
            SimEvent land = Single(events, SimEventKind.Land);
            Assert.That(land.Air.Grab, Is.EqualTo(expected));
            Assert.That(land.Air.GrabTicks, Is.EqualTo(grabSteps));
            Assert.That(land.Air.GrabSeconds, Is.EqualTo(Fix64.FromRatio(grabSteps, Simulation.StepsPerSecond)));
            Assert.That(land.Air.Spin, Is.EqualTo(Fix64.Zero), "a grab does not spin");
            Assert.That(land.Air.Flip, Is.EqualTo(Fix64.Zero), "a grab does not flip");
            Assert.That(land.Grade, Is.EqualTo(LandingGrade.Clean));
        }

        [Test]
        public void PopStrength_GrowsWithHoldTime_UpToTheTuningCap()
        {
            // Arrange: at rest on the flat the pop is all vertical, so velocity.Y after the release step is the pop.
            int full = FullLoadSteps(Tuning.Default);
            int[] loads = { 1, full / 4, full / 2, full - 1, full, 2 * full };
            var pops = new double[loads.Length];
            Tuning bigger = Tuning.Default;
            bigger.PopSpeedMax = Fix64.FromInt(8);

            // Act
            for (int i = 0; i < loads.Length; i++)
            {
                pops[i] = PopSpeed(Tuning.Default, loads[i]);
            }

            double biggerCap = PopSpeed(bigger, 2 * full);

            // Assert
            double min = D(Tuning.Default.PopSpeedMin), max = D(Tuning.Default.PopSpeedMax);
            for (int i = 0; i < loads.Length; i++)
            {
                double expected = min + (max - min) * Math.Min(1.0, (double)loads[i] / full);
                Assert.That(pops[i], Is.EqualTo(expected).Within(1e-6), "load " + loads[i]);
            }

            for (int i = 1; i < 5; i++)
            {
                Assert.That(pops[i], Is.GreaterThan(pops[i - 1]), "a longer hold pops harder, load " + loads[i]);
            }

            Assert.That(pops[5], Is.EqualTo(pops[4]), "holding past PopLoadTime adds nothing");
            Assert.That(pops[4], Is.EqualTo(max).Within(1e-9));
            Assert.That(biggerCap, Is.EqualTo(8.0).Within(1e-9), "the cap is PopSpeedMax");
        }

        [Test]
        public void Events_TakeoffAndLand_CarrySpeedGradeAirTimeAndMaxHeight()
        {
            // Arrange
            Tuning tuning = Tuning.Default;
            Simulation simulation = StartFlat(tuning);
            var events = new List<SimEvent>();
            double pop = D(tuning.PopSpeedMax), g = D(tuning.Gravity);
            int landTick = 0;

            // Act
            LoadAndPop(simulation, FullLoadSteps(tuning), events);
            int takeoffTick = simulation.State.Tick;
            FlyUntilDown(simulation, _ => NoInput, events);
            landTick = simulation.State.Tick;

            // Assert
            SimEvent takeoff = Single(events, SimEventKind.Takeoff);
            SimEvent land = Single(events, SimEventKind.Land);
            Assert.That(takeoff.Tick, Is.EqualTo(takeoffTick));
            Assert.That(takeoff.Speed, Is.EqualTo(tuning.PopSpeedMax), "at rest on the flat the takeoff speed is the pop");
            Assert.That(land.Tick, Is.EqualTo(landTick));
            Assert.That(land.Grade, Is.EqualTo(LandingGrade.Clean));
            Assert.That(land.Air.AirTicks, Is.EqualTo(landTick - takeoffTick));
            Assert.That(land.Air.AirTicks, Is.EqualTo(2 * pop / g * Simulation.StepsPerSecond).Within(1.5));
            Assert.That(land.Air.AirSeconds, Is.EqualTo(Fix64.FromRatio(land.Air.AirTicks, Simulation.StepsPerSecond)));
            Assert.That(D(land.Air.MaxHeight), Is.EqualTo(pop * pop / (2 * g)).Within(0.05));
            Assert.That(land.Air.Grab, Is.EqualTo(GrabKind.None));
            Assert.That(land.Cause, Is.EqualTo(CrashCause.None));
            Assert.That(takeoff.Grade, Is.EqualTo(LandingGrade.None));
        }

        [Test]
        public void TapReset_InTheAir_ReturnsToTheLastSafeSpotAtOnce()
        {
            // Arrange: slide, pop, fly a little.
            Tuning tuning = Tuning.Default;
            Simulation simulation = StartIncline(tuning);
            var events = new List<SimEvent>();
            Ride(simulation, NoInput, SlideSteps, events);
            RiderState safe = LoadAndPop(simulation, FullLoadSteps(tuning), events);
            Ride(simulation, SimInput.Pack(SimButtons.Right, 0), InAirSteps, events);
            Assert.That(simulation.State.Airborne, Is.True);

            // Act
            simulation.Step(ResetHeld);
            RiderState reset = simulation.State;
            bool resetRaised = Has(simulation, SimEventKind.Reset);
            simulation.Step(ResetHeld);
            bool raisedWhileHeld = Has(simulation, SimEventKind.Reset);

            // Assert
            Assert.That(resetRaised, Is.True);
            Assert.That(reset.Position, Is.EqualTo(safe.Position), "the last grounded, uncrashed spot before the pop");
            Assert.That(reset.Heading, Is.EqualTo(safe.Heading));
            Assert.That(reset.Velocity, Is.EqualTo(Vec3.Zero));
            Assert.That(reset.Airborne, Is.False);
            Assert.That(reset.SpinAngle, Is.EqualTo(Fix64.Zero));
            Assert.That(raisedWhileHeld, Is.False, "holding reset is one tap");
        }

        [TestCase(1, 1)]
        [TestCase(1, 2)]
        public void Crash_ResetsToTheLastSafeSpotAfterTheTuningDelay(int numerator, int denominator)
        {
            // Arrange
            Tuning tuning = Tuning.Default;
            tuning.CrashResetDelay = Fix64.FromRatio(numerator, denominator);
            int delaySteps = Simulation.StepsPerSecond * numerator / denominator;
            Simulation simulation = StartIncline(tuning);
            var events = new List<SimEvent>();
            Ride(simulation, NoInput, SlideSteps, events);
            RiderState safe = LoadAndPop(simulation, FullLoadSteps(tuning), events);
            int flipSteps = RotationSteps(tuning, SimButtons.Forward, 60);
            FlyUntilDown(simulation, step => step < flipSteps ? Forward : NoInput, events);
            RiderState crashed = simulation.State;
            Assert.That(crashed.Crashed, Is.True);

            // Act: hold forward and steer; a crashed rider ignores it.
            int steps = 0;
            bool stayedPut = true;
            while (!Has(simulation, SimEventKind.Reset) && steps < 10 * Simulation.StepsPerSecond)
            {
                simulation.Step(SimInput.Pack(SimButtons.Forward | SimButtons.Left, 0));
                steps++;
                stayedPut &= simulation.State.Crashed ? simulation.State.Position == crashed.Position && simulation.State.Velocity == Vec3.Zero : true;
            }

            // Assert
            Assert.That(steps, Is.EqualTo(delaySteps));
            Assert.That(stayedPut, Is.True, "a crashed rider lies still");
            RiderState reset = simulation.State;
            Assert.That(reset.Crashed, Is.False);
            Assert.That(reset.Position, Is.EqualTo(safe.Position));
            Assert.That(reset.Heading, Is.EqualTo(safe.Heading));
            Assert.That(reset.Velocity, Is.EqualTo(Vec3.Zero));
        }

        [Test]
        public void TapReset_WhileCrashed_ResetsAtOnce()
        {
            // Arrange
            Tuning tuning = Tuning.Default;
            Simulation simulation = StartIncline(tuning);
            var events = new List<SimEvent>();
            Ride(simulation, NoInput, SlideSteps, events);
            RiderState safe = LoadAndPop(simulation, FullLoadSteps(tuning), events);
            int flipSteps = RotationSteps(tuning, SimButtons.Back, 90);
            FlyUntilDown(simulation, step => step < flipSteps ? SimInput.Pack(SimButtons.Back, 0) : NoInput, events);
            Ride(simulation, NoInput, 10, events);
            Assert.That(simulation.State.Crashed, Is.True);

            // Act
            simulation.Step(ResetHeld);

            // Assert
            Assert.That(Has(simulation, SimEventKind.Reset), Is.True);
            Assert.That(simulation.State.Crashed, Is.False);
            Assert.That(simulation.State.Position, Is.EqualTo(safe.Position));
        }

        private static double PopSpeed(Tuning tuning, int loadSteps)
        {
            Simulation simulation = StartFlat(tuning);
            var events = new List<SimEvent>();
            LoadAndPop(simulation, loadSteps, events);
            Assert.That(D(Single(events, SimEventKind.Takeoff).Speed), Is.EqualTo(D(simulation.State.Velocity.Y)).Within(1e-8));
            return D(simulation.State.Velocity.Y);
        }

        private static Simulation StartFlat(Tuning tuning)
        {
            Heightmap flat = TestTerrain.Flat();
            return new Simulation(new RunSetup(Sport.Skis, 0, 1UL, Fix64.FromInt(64), Fix64.FromInt(64)), flat, TestTerrain.Uniform(flat, SnowType.Groomed), tuning);
        }

        private static Simulation StartIncline(Tuning tuning)
        {
            Heightmap incline = TestTerrain.InclineDownhillPlusZ();
            return new Simulation(new RunSetup(Sport.Skis, 0, 1UL, Fix64.FromInt(64), Fix64.FromInt(20)), incline, TestTerrain.Uniform(incline, SnowType.Groomed), tuning);
        }

        private static int FullLoadSteps(Tuning tuning) => (int)Math.Ceiling(D(tuning.PopLoadTime) * Simulation.StepsPerSecond - 1e-9);

        private static int RotationSteps(Tuning tuning, SimButtons button, int degrees)
        {
            Fix64 rate = button == SimButtons.Left || button == SimButtons.Right ? tuning.SpinRate : tuning.FlipRate;
            return (int)Math.Round(degrees * Math.PI / 180 / D(rate * Simulation.StepSeconds));
        }

        /// <summary>Holds jump for <paramref name="loadSteps"/>, then releases it. Returns the last grounded state.</summary>
        private static RiderState LoadAndPop(Simulation simulation, int loadSteps, List<SimEvent> events)
        {
            Ride(simulation, JumpHeld, loadSteps, events);
            Assert.That(simulation.State.Airborne, Is.False, "loading keeps the rider on the snow");
            RiderState lastGrounded = simulation.State;
            Step(simulation, NoInput, events);
            Assert.That(simulation.State.Airborne, Is.True, "the release must pop the rider off the snow");
            return lastGrounded;
        }

        private static RiderState RideUntilAirborne(Simulation simulation, SimInput input, List<SimEvent> events)
        {
            for (int step = 0; step < 30 * Simulation.StepsPerSecond; step++)
            {
                Step(simulation, input, events);
                if (simulation.State.Airborne)
                {
                    return simulation.State;
                }
            }

            Assert.Fail("the rider never left the snow: " + simulation.State);
            return default;
        }

        private static void FlyUntilDown(Simulation simulation, Func<int, SimInput> inputAt, List<SimEvent> events, List<GrabKind> grabs = null)
        {
            for (int step = 0; simulation.State.Airborne && step < MaxAirSteps; step++)
            {
                Step(simulation, inputAt(step), events);
                grabs?.Add(simulation.State.Grab);
            }

            Assert.That(simulation.State.Airborne, Is.False, "the rider must come down");
        }

        private static void Ride(Simulation simulation, SimInput input, int steps, List<SimEvent> events)
        {
            for (int step = 0; step < steps; step++)
            {
                Step(simulation, input, events);
            }
        }

        private static void Step(Simulation simulation, SimInput input, List<SimEvent> events)
        {
            simulation.Step(input);
            events.AddRange(simulation.Events);
        }

        private static bool Has(Simulation simulation, SimEventKind kind)
        {
            for (int i = 0; i < simulation.Events.Count; i++)
            {
                if (simulation.Events[i].Kind == kind)
                {
                    return true;
                }
            }

            return false;
        }

        private static int Count(List<SimEvent> events, SimEventKind kind) => events.FindAll(e => e.Kind == kind).Count;

        private static SimEvent Single(List<SimEvent> events, SimEventKind kind)
        {
            List<SimEvent> found = events.FindAll(e => e.Kind == kind);
            Assert.That(found.Count, Is.EqualTo(1), "one " + kind + " event expected in [" + string.Join(", ", events) + "]");
            return found[0];
        }

        private static Heightmap KickerSlope()
        {
            byte[] bytes = TestTerrain.R16Bytes(KickerResolution, 0, KickerTopMm, (row, column) =>
            {
                double z = row * KickerCellM;
                double rise = z < KickerFootZ ? 0.0
                    : z <= KickerLipZ ? KickerHeightM * Square((z - KickerFootZ) / KickerLengthM)
                    : KickerHeightM * (1.0 - Smoothstep((z - KickerLipZ) / (2.0 * KickerHeightM)));
                return (int)Math.Round((KickerTopMm / 1000.0 - KickerGradient * z + rise) * 1000.0);
            });
            return Heightmap.FromR16(bytes, new HeightmapSpec(KickerSizeMm, KickerSizeMm, 0, KickerTopMm));
        }

        private static double Square(double value) => value * value;

        private static double Smoothstep(double t)
        {
            t = Math.Clamp(t, 0.0, 1.0);
            return t * t * (3.0 - 2.0 * t);
        }

        private static (SlopeLayout Layout, Heightmap Heightmap, SnowMap SnowMap) LoadTestSlope()
        {
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Packages/com.firstchair.sim/package.json")))
            {
                directory = directory.Parent;
            }

            Assert.That(directory, Is.Not.Null, "repository root not found");
            string slope = Path.Combine(directory.FullName, "Assets/Terrain/TestSlope");
            SlopeLayout layout = SlopeLayout.Parse(File.ReadAllText(Path.Combine(slope, "TestSlope.txt")));
            Heightmap heightmap = Heightmap.FromR16(File.ReadAllBytes(Path.Combine(slope, "TestSlope.raw")), layout.Spec);
            return (layout, heightmap, SnowMap.FromBytes(File.ReadAllBytes(Path.Combine(slope, "TestSlopeSnow.raw")), heightmap));
        }

        private static double Speed(RiderState state)
        {
            double x = D(state.Velocity.X), y = D(state.Velocity.Y), z = D(state.Velocity.Z);
            return Math.Sqrt(x * x + y * y + z * z);
        }

        private static double D(Fix64 value) => TestTerrain.ToDouble(value);
    }
}
