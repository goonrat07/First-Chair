using System;
using System.Reflection;
using FirstChair.Sim;
using NUnit.Framework;

namespace FirstChair.Sim.Tests
{
    /// <summary>
    /// Story 002, 008 and 009 scripted-input tests: steering and edging, carves, quick turns, drifts, hockey stops and
    /// skating (sim version 8, after GMA), tuck, brake, poling on flats only, and the five snow types. They pin behaviour, not final numbers: expectations come from
    /// <see cref="Tuning"/>, and several tests also run tuning variants, so a riding number written into the
    /// logic instead of read from Tuning fails them.
    /// </summary>
    public class SnowRidingTests
    {
        // The run-out terrain: the standard 0.4 incline down to row 32 (z = 64 m), level beyond. Its test snow
        // starts at row 34 (z >= 67 m), and tests act once the rider is past z = 72 m on the level.
        private const int FlatRow = 32;
        private const int ZoneRow = 34;
        private const double OnFlatZ = 72.0;

        private static readonly SimInput NoInput = SimInput.Pack(SimButtons.None, 0);
        private static readonly SimInput Forward = SimInput.Pack(SimButtons.Forward, 0);
        private static readonly SimInput Back = SimInput.Pack(SimButtons.Back, 0);

        [TestCase(SimButtons.Left, 0, -127)]
        [TestCase(SimButtons.Right, 0, 127)]
        [TestCase(SimButtons.None, -45, -45)]
        [TestCase(SimButtons.Left | SimButtons.Right, 50, 50)]
        [TestCase(SimButtons.Left, 127, 0)]
        [TestCase(SimButtons.Right, -27, 100)]
        [TestCase(SimButtons.Right, 60, 127)]
        [TestCase(SimButtons.Left, -60, -127)]
        public void CombinedSteering_ButtonsAndAnalog_AddAndClamp(SimButtons buttons, int steering, int expected)
        {
            // Arrange
            SimInput input = SimInput.Pack(buttons | SimButtons.Forward, steering);

            // Act
            int combined = input.CombinedSteering;

            // Assert
            Assert.That(combined, Is.EqualTo(expected));
        }

        [Test]
        public void FullRightSteering_FromRest_RampsTheEdgeOverEdgeRampTime_AndPivotsTowardThatSide()
        {
            // Arrange
            Tuning tuning = Tuning.Default;
            Simulation simulation = StartOn(TestTerrain.Flat(), SnowType.Groomed, tuning, 64, 64);
            SimInput right = SimInput.Pack(SimButtons.Right, 0);

            // Act
            simulation.Step(right);
            Fix64 afterOneStep = simulation.State.EdgeAngle;
            Fix64 headingAfterOneStep = simulation.State.Heading;
            Ride(simulation, right, Simulation.StepsPerSecond - 1);

            // Assert
            Assert.That(D(afterOneStep), Is.EqualTo(D(tuning.MaxEdgeAngle) * D(Simulation.StepSeconds) / D(tuning.EdgeRampTime)).Within(1e-6));
            Assert.That(simulation.State.EdgeAngle, Is.EqualTo(tuning.MaxEdgeAngle));
            Assert.That(headingAfterOneStep.Raw, Is.GreaterThan(0L), "a resting rider pivots toward the steering");
        }

        [TestCase(SimButtons.Right, 1)]
        [TestCase(SimButtons.Left, -1)]
        public void SteeringOnIncline_FromRest_TurnsAndDriftsTowardThatSide(SimButtons button, int side)
        {
            // Arrange: the rider faces +Z, so right is +X.
            Simulation simulation = StartOn(TestTerrain.InclineDownhillPlusZ(), SnowType.Groomed, Tuning.Default, 64, 4);

            // Act: half a second; slow skis pivot fast (Story 008), and in a whole second they come round past uphill.
            Ride(simulation, SimInput.Pack(button, 0), Simulation.StepsPerSecond / 2);

            // Assert
            RiderState state = simulation.State;
            Assert.That(Math.Sign(state.Heading.Raw), Is.EqualTo(side));
            Assert.That(Math.Sign((state.Position.X - Fix64.FromInt(64)).Raw), Is.EqualTo(side));
        }

        [Test]
        public void NewRun_FacesDownTheFallLine()
        {
            // Arrange
            Heightmap downhillPlusX = TestTerrain.Build((row, column) => TestTerrain.MaxHeightMm - TestTerrain.DropMmPerCell * column);

            // Act
            Fix64 plusZ = StartOn(TestTerrain.InclineDownhillPlusZ(), SnowType.Groomed, Tuning.Default, 64, 4).State.Heading;
            Fix64 minusZ = StartOn(TestTerrain.InclineDownhillMinusZ(), SnowType.Groomed, Tuning.Default, 64, 124).State.Heading;
            Fix64 plusX = StartOn(downhillPlusX, SnowType.Groomed, Tuning.Default, 4, 64).State.Heading;
            Fix64 level = StartOn(TestTerrain.Flat(), SnowType.Groomed, Tuning.Default, 64, 64).State.Heading;

            // Assert
            Assert.That(plusZ, Is.EqualTo(Fix64.Zero));
            Assert.That(minusZ, Is.EqualTo(Fix64.Pi));
            Assert.That(D(plusX), Is.EqualTo(Math.PI / 2).Within(1e-8));
            Assert.That(level, Is.EqualTo(Fix64.Zero));
        }







        [Test]
        public void QuickTurn_AlwaysSlipsALittle_AndSlipsAndSlowsMoreTheHarderItTurns()
        {
            // Arrange: at a moderate speed on the level run-out: straight, half and full steering, and full steering with
            // no plowing drag and with twice the skid stiffness.
            Tuning tuning = Tuning.Default;
            Tuning noSkidDrag = tuning;
            noSkidDrag.GroomedSkidDrag = Fix64.Zero;
            Tuning stiffer = tuning;
            stiffer.SkidStiffness = tuning.SkidStiffness * Fix64.FromInt(2);
            const double moderate = 10.0;
            Simulation straight = OnRunOutAt(SnowType.Groomed, tuning, moderate);
            Simulation half = OnRunOutAt(SnowType.Groomed, tuning, moderate);
            Simulation full = OnRunOutAt(SnowType.Groomed, tuning, moderate);
            Simulation fullNoSkidDrag = OnRunOutAt(SnowType.Groomed, noSkidDrag, moderate);
            Simulation fullStiffer = OnRunOutAt(SnowType.Groomed, stiffer, moderate);
            SimInput halfRight = SimInput.Pack(SimButtons.Quick, SimInput.SteeringMax / 2);
            double halfSlip = 0, fullSlip = 0, stifferSlip = 0;

            // Act
            for (int step = 0; step < Simulation.StepsPerSecond / 2; step++)
            {
                straight.Step(NoInput);
                half.Step(halfRight);
                full.Step(QuickTurnRight);
                fullNoSkidDrag.Step(QuickTurnRight);
                fullStiffer.Step(QuickTurnRight);
                halfSlip += D(half.State.SlipAngle);
                fullSlip += D(full.State.SlipAngle);
                stifferSlip += D(fullStiffer.State.SlipAngle);
            }

            // Assert
            Assert.That(halfSlip, Is.GreaterThan(0.0), "a quick turn always slips a little");
            Assert.That(fullSlip, Is.GreaterThan(halfSlip), "full steering must slip more");
            Assert.That(stifferSlip, Is.LessThan(fullSlip), "the slip must come from SkidStiffness");
            Assert.That(Speed(half.State), Is.LessThan(Speed(straight.State)));
            Assert.That(Speed(full.State), Is.LessThan(Speed(half.State)), "more slip must cost more speed");
            Assert.That(Speed(fullNoSkidDrag.State), Is.GreaterThan(Speed(full.State)), "the plowing drag must cost speed");
        }



        [Test]
        public void RampingEdge_OnIce_SkidsPartWay_WhileAGroomedCarveHoldsToFullEdge()
        {
            // Arrange: the same groomed run-in, then the edge ramps up on the test snow, with equal drags so grip is the only
            // difference.
            Tuning equalDrag = Tuning.Default;
            equalDrag.IceDrag = equalDrag.GroomedDrag;

            // Act
            Fix64? groomedOnset = SkidOnsetEdge(SnowType.Groomed, equalDrag);
            Fix64? iceOnset = SkidOnsetEdge(SnowType.Ice, equalDrag);

            // Assert: groomed snow's grip holds a full carve (TurnAcceleration is under it); ice's lets go part way.
            Assert.That(groomedOnset, Is.Null, "a full groomed carve must hold");
            Assert.That(iceOnset.HasValue, Is.True, "ice must skid");
            Assert.That(iceOnset.Value.Raw, Is.GreaterThan(0L));
            Assert.That(iceOnset.Value.Raw, Is.LessThan(equalDrag.MaxEdgeAngle.Raw));
        }

        [Test]
        public void StraightRun_OnPowderVersusGroomed_PowderEndsSlowerAndShorter()
        {
            // Arrange
            Heightmap incline = TestTerrain.InclineDownhillPlusZ();
            Simulation groomed = StartOn(incline, SnowType.Groomed, Tuning.Default, 64, 4);
            Simulation powder = StartOn(incline, SnowType.Powder, Tuning.Default, 64, 4);

            // Act
            Ride(groomed, NoInput, 4 * Simulation.StepsPerSecond);
            Ride(powder, NoInput, 4 * Simulation.StepsPerSecond);

            // Assert
            Assert.That(Speed(powder.State), Is.LessThan(Speed(groomed.State)));
            Assert.That(powder.State.Position.Z.Raw, Is.LessThan(groomed.State.Position.Z.Raw));
        }

        [Test]
        public void RaisingOneTypesDrag_ChangesOnlyRidesOnThatType([Values] SnowType changed)
        {
            // Arrange
            Tuning baseline = Tuning.Default;
            Tuning variant = WithField(baseline, changed + "Drag", ReadField(baseline, changed + "Drag") * Fix64.FromInt(2));

            foreach (SnowType ridden in (SnowType[])Enum.GetValues(typeof(SnowType)))
            {
                // Act
                RiderState before = RideIncline(ridden, baseline, NoInput, 2 * Simulation.StepsPerSecond);
                RiderState after = RideIncline(ridden, variant, NoInput, 2 * Simulation.StepsPerSecond);

                // Assert
                if (ridden == changed)
                {
                    Assert.That(after.Velocity.Z.Raw, Is.LessThan(before.Velocity.Z.Raw), ridden + " must use " + changed + "Drag");
                }
                else
                {
                    Assert.That(after, Is.EqualTo(before), ridden + " must not use " + changed + "Drag");
                }
            }
        }

        [Test]
        public void ChangingOneTypesTurnTuning_ChangesOnlyRidesOnThatType(
            [Values("CarveGrip", "SkidGrip", "SkidDrag", "TurnFactor")] string field, [Values] SnowType changed)
        {
            // Arrange: the field halved on one type. Carve grip and the turn factor act in a carve (steering alone), skid grip
            // and skid drag in a quick turn (quick and steering).
            Tuning baseline = Tuning.Default;
            Tuning variant = WithField(baseline, changed + field, ReadField(baseline, changed + field) / Fix64.FromInt(2));
            SimInput turn = field == "CarveGrip" || field == "TurnFactor" ? PlainRight : QuickTurnRight;

            foreach (SnowType ridden in (SnowType[])Enum.GetValues(typeof(SnowType)))
            {
                // Act: straight down the incline to carving speed, then the turn.
                RiderState before = RideInclineThenTurn(ridden, baseline, turn);
                RiderState after = RideInclineThenTurn(ridden, variant, turn);

                // Assert
                if (ridden == changed)
                {
                    Assert.That(after, Is.Not.EqualTo(before), ridden + " must use " + changed + field);
                }
                else
                {
                    Assert.That(after, Is.EqualTo(before), ridden + " must not use " + changed + field);
                }
            }
        }

        [Test]
        public void RiderCrossingSnowBoundary_RidesOnTheTypeOfTheNearestSample()
        {
            // Arrange: groomed for rows 0 to 19, powder from row 20 (2 m rows, so powder from z = 39 m).
            Heightmap incline = TestTerrain.InclineDownhillPlusZ();
            SnowMap snowMap = TestTerrain.Snow(incline, (row, column) => row < 20 ? SnowType.Groomed : SnowType.Powder);
            Simulation simulation = Start(incline, snowMap, Tuning.Default, 64, 4);
            bool sawGroomed = false, sawPowder = false;

            // Act and Assert, step by step: each step rides on the type of the sample nearest where it began.
            for (int step = 0; step < 6 * Simulation.StepsPerSecond; step++)
            {
                double zBefore = D(simulation.State.Position.Z);
                simulation.Step(NoInput);
                SnowType expected = Math.Floor(zBefore / 2 + 0.5) < 20 ? SnowType.Groomed : SnowType.Powder;
                Assert.That(simulation.State.Snow, Is.EqualTo(expected), "at tick " + simulation.State.Tick);
                sawGroomed |= expected == SnowType.Groomed;
                sawPowder |= expected == SnowType.Powder;
            }

            Assert.That(sawGroomed && sawPowder, Is.True, "the ride must cross the boundary");
        }

        [Test]
        public void Tuck_StraightRunDownLongGroomer_ReachesHigherTerminalSpeedThanStanding()
        {
            // Arrange: a 4 km slope, long enough for both runs to level off; plus a tuning where tucking keeps
            // all the air drag.
            Heightmap slope = LongIncline();
            SnowMap groomed = TestTerrain.Uniform(slope, SnowType.Groomed);
            Tuning noTuckBenefit = Tuning.Default;
            noTuckBenefit.TuckAirDragFactor = Fix64.One;

            // Act
            (double standing, double standingDrift) = TerminalSpeed(slope, groomed, Tuning.Default, NoInput);
            (double tucked, double tuckedDrift) = TerminalSpeed(slope, groomed, Tuning.Default, Forward);
            (double tuckedNoBenefit, _) = TerminalSpeed(slope, groomed, noTuckBenefit, Forward);

            // Assert
            Assert.That(standingDrift, Is.LessThan(1e-3), "standing must have reached terminal speed");
            Assert.That(tuckedDrift, Is.LessThan(1e-3), "tucking must have reached terminal speed");
            Assert.That(tucked, Is.GreaterThan(1.1 * standing));
            Assert.That(tuckedNoBenefit, Is.EqualTo(standing), "the tuck's speed must come from TuckAirDragFactor");
        }

        [TestCase(100)]
        [TestCase(150)]
        public void Brake_OnGroomerAtSpeed_StopsWithinTheStoppingDistanceTuningSets(int brakePercent)
        {
            // Arrange: 3 s straight down the 0.4 groomer, then brake. Along the slope the brake, snow drag and
            // gravity give a constant deceleration b and the air k v^2, so the stop takes ln(1 + k v0^2 / b) / (2 k).
            Tuning tuning = Tuning.Default;
            tuning.BrakeDeceleration = tuning.BrakeDeceleration * Fix64.FromRatio(brakePercent, 100);
            Simulation simulation = StartOn(TestTerrain.InclineDownhillPlusZ(), SnowType.Groomed, tuning, 64, 4);
            Ride(simulation, NoInput, 3 * Simulation.StepsPerSecond);
            double v0 = Speed(simulation.State);
            Vec3 brakePoint = simulation.State.Position;
            double g = D(tuning.Gravity), mu = D(tuning.GroomedDrag), k = D(tuning.AirDrag);
            double theta = Math.Atan(TestTerrain.Gradient);
            double b = D(tuning.BrakeDeceleration) + mu * g * Math.Cos(theta) - g * Math.Sin(theta);
            double expected = Math.Log(1 + k * v0 * v0 / b) / (2 * k);

            // Act
            int steps = 0;
            while (simulation.State.Velocity != Vec3.Zero && steps < 10 * Simulation.StepsPerSecond)
            {
                simulation.Step(Back);
                steps++;
            }

            Vec3 stopPoint = simulation.State.Position;
            Ride(simulation, Back, Simulation.StepsPerSecond);

            // Assert
            Assert.That(v0, Is.GreaterThan(5.0), "the rider must be moving when the brake goes on");
            Assert.That(Distance(brakePoint, stopPoint), Is.EqualTo(expected).Within(3).Percent);
            Assert.That(simulation.State.Velocity, Is.EqualTo(Vec3.Zero), "holding brake keeps the rider stopped");
            Assert.That(simulation.State.Position, Is.EqualTo(stopPoint));
        }

        [TestCase(100)]
        [TestCase(150)]
        public void Pole_OnFlatFromRest_GainsSpeedUpToThePoleSpeedCap(int capPercent)
        {
            // Arrange
            Tuning tuning = Tuning.Default;
            tuning.PoleSpeedMax = tuning.PoleSpeedMax * Fix64.FromRatio(capPercent, 100);
            Simulation simulation = StartOn(TestTerrain.Flat(), SnowType.Groomed, tuning, 64, 32);
            double cap = D(tuning.PoleSpeedMax);
            double fastest = 0;

            // Act
            for (int step = 0; step < 10 * Simulation.StepsPerSecond; step++)
            {
                simulation.Step(Forward);
                fastest = Math.Max(fastest, Speed(simulation.State));
            }

            // Assert
            Assert.That(simulation.State.Stance, Is.EqualTo(Stance.Pole));
            Assert.That(fastest, Is.LessThanOrEqualTo(cap + 1e-9));
            Assert.That(Speed(simulation.State), Is.GreaterThan(0.95 * cap));
        }

        [TestCase(1, 2, true)]
        [TestCase(2, 1, false)]
        public void Forward_OnSlopeBelowOrAboveFlatThreshold_PolesOnlyBelow(int numerator, int denominator, bool expectPoling)
        {
            // Arrange: a slope at half or twice the flat threshold, ridden with normal and four-times-stronger poles.
            Tuning tuning = Tuning.Default;
            Tuning strongPoles = tuning;
            strongPoles.PoleAcceleration = tuning.PoleAcceleration * Fix64.FromInt(4);
            strongPoles.PoleSpeedMax = tuning.PoleSpeedMax * Fix64.FromInt(4);
            double gradient = D(tuning.FlatGradientMax) * numerator / denominator;
            var dropMmPerCell = (int)Math.Round(gradient * TestTerrain.SizeMm / (TestTerrain.Resolution - 1));
            Heightmap slope = TestTerrain.Build((row, column) => TestTerrain.MaxHeightMm - dropMmPerCell * row);

            // Act
            Simulation normal = StartOn(slope, SnowType.Groomed, tuning, 64, 4);
            Simulation strong = StartOn(slope, SnowType.Groomed, strongPoles, 64, 4);
            Ride(normal, Forward, 3 * Simulation.StepsPerSecond);
            Ride(strong, Forward, 3 * Simulation.StepsPerSecond);

            // Assert
            if (expectPoling)
            {
                Assert.That(normal.State.Stance, Is.EqualTo(Stance.Pole));
                Assert.That(Speed(normal.State), Is.GreaterThan(0.5));
                Assert.That(Speed(strong.State), Is.GreaterThan(Speed(normal.State)));
            }
            else
            {
                Assert.That(normal.State.Stance, Is.EqualTo(Stance.Tuck));
                Assert.That(strong.State.Velocity, Is.EqualTo(normal.State.Velocity), "poling must do nothing on the slope");
                Assert.That(strong.State.Position, Is.EqualTo(normal.State.Position));
            }
        }

        [Test]
        public void Forward_OnFlatAbovePoleSpeedCap_TucksInsteadOfPoling()
        {
            // Arrange
            Simulation gliding = StartOnRunOut(SnowType.Groomed, Tuning.Default);
            Simulation forward = StartOnRunOut(SnowType.Groomed, Tuning.Default);
            RideOntoFlat(gliding);
            RideOntoFlat(forward);

            // Act and Assert
            for (int step = 0; step < Simulation.StepsPerSecond / 2; step++)
            {
                gliding.Step(NoInput);
                forward.Step(Forward);
                Assert.That(forward.State.Stance, Is.EqualTo(Stance.Tuck));
            }

            Assert.That(Speed(forward.State), Is.GreaterThan(Speed(gliding.State)));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void NewRun_MaxEdgeAngleNotPositive_ThrowsArgumentException(int maxEdgeAngle)
        {
            // Arrange
            Tuning tuning = Tuning.Default;
            tuning.MaxEdgeAngle = Fix64.FromInt(maxEdgeAngle);

            // Act and Assert
            Assert.That(() => StartOn(TestTerrain.Flat(), SnowType.Groomed, tuning, 64, 64), Throws.ArgumentException);
        }

        [Test]
        public void Step_AtRestWithNegativeDrag_StaysAtRestWithoutThrowing()
        {
            // Arrange: a negative drag is bad tuning, but it must not crash a ride that is standing still.
            Tuning tuning = Tuning.Default;
            tuning.GroomedDrag = -Tuning.Default.GroomedDrag;
            Simulation simulation = StartOn(TestTerrain.Flat(), SnowType.Groomed, tuning, 64, 64);

            // Act
            simulation.Step(NoInput);

            // Assert
            Assert.That(simulation.State.Velocity, Is.EqualTo(Vec3.Zero));
        }

        // ---- Story 009: GMA-style riding (sim version 8) ----

        private static readonly SimInput PlainRight = SimInput.Pack(SimButtons.Right, 0);
        private static readonly SimInput PlainLeft = SimInput.Pack(SimButtons.Left, 0);
        private static readonly SimInput QuickTurnRight = SimInput.Pack(SimButtons.Quick | SimButtons.Right, 0);
        private static readonly SimInput DriftRight = SimInput.Pack(SimButtons.Grab | SimButtons.Right, 0);
        private static readonly SimInput HockeyRight = SimInput.Pack(SimButtons.Back | SimButtons.Right, 0);
        private static readonly SimInput TuckRight = SimInput.Pack(SimButtons.Forward | SimButtons.Right, 0);

        [Test]
        public void Edge_HeldKeyRampsInEdgeRampTime_ReleaseFlattensInEdgeReleaseTime_OtherKeyFlipsInEdgeFlipTime()
        {
            // Arrange
            Tuning tuning = Tuning.Default;
            Simulation simulation = OnRunOutAt(SnowType.Groomed, tuning, 10.0);

            // Act
            int ramp = StepsUntil(simulation, PlainRight, state => state.EdgeAngle >= tuning.MaxEdgeAngle);
            int flip = StepsUntil(simulation, PlainLeft, state => state.EdgeAngle <= -tuning.MaxEdgeAngle);
            int release = StepsUntil(simulation, NoInput, state => state.EdgeAngle == Fix64.Zero);

            // Assert: each within a step of its time.
            Assert.That(ramp, Is.InRange(StepsOf(tuning.EdgeRampTime), StepsOf(tuning.EdgeRampTime) + 1), "ramp");
            Assert.That(flip, Is.InRange(StepsOf(tuning.EdgeFlipTime), StepsOf(tuning.EdgeFlipTime) + 1), "flip");
            Assert.That(release, Is.InRange(StepsOf(tuning.EdgeReleaseTime), StepsOf(tuning.EdgeReleaseTime) + 1), "release");
        }

        [Test]
        public void PlainSteering_OnGroomedFlatAtSpeed_CarvesWithoutSkidding_AndKeepsSpeed()
        {
            // Arrange: a carve and a straight run from the same 15 m/s.
            Simulation carve = OnRunOutAt(SnowType.Groomed, Tuning.Default, 15.0);
            Simulation straight = OnRunOutAt(SnowType.Groomed, Tuning.Default, 15.0);
            double start = Speed(carve.State);
            bool skidded = false;

            // Act: one second of plain steering, the main turn since sim version 8.
            for (int step = 0; step < Simulation.StepsPerSecond; step++)
            {
                carve.Step(PlainRight);
                straight.Step(NoInput);
                skidded |= carve.State.Skidding;
            }

            // Assert: no skid, the path follows the skis, and the carve loses at most 2% of the speed more than a straight run.
            Assert.That(carve.State.Stance, Is.EqualTo(Stance.Carve));
            Assert.That(skidded, Is.False);
            Assert.That(Math.Abs(Lead(carve.State)), Is.LessThan(0.02));
            Assert.That(PathAngle(carve.State), Is.GreaterThan(0.8), "it must really turn");
            Assert.That(Speed(straight.State) - Speed(carve.State), Is.LessThan(0.02 * start));
        }

        [TestCase(3.0)]
        [TestCase(12.0)]
        [TestCase(20.0)]
        public void CarveAtFullEdge_TurnsAtTurnRateLowWhenSlow_AndTurnAccelerationOverSpeedWhenFast(double speed)
        {
            // Arrange: a variant too, so the rate must come from tuning.
            Tuning variant = WithField(WithField(Tuning.Default, nameof(Tuning.TurnRateLow), Fix64.FromRatio(18, 10)),
                nameof(Tuning.TurnAcceleration), Fix64.FromInt(11));
            foreach (Tuning tuning in new[] { Tuning.Default, variant })
            {
                // Act
                (double before, double rate) = FullEdgeTurnRate(tuning, SnowType.Groomed, speed, PlainRight);

                // Assert
                double expected = Math.Min(D(tuning.TurnRateLow), D(tuning.TurnAcceleration) * D(tuning.GroomedTurnFactor) / before);
                Assert.That(rate, Is.EqualTo(expected).Within(1e-6 + 1e-4 * expected), "at " + before + " m/s");
            }
        }

        [Test]
        public void TuckedCarve_TurnsTuckTurnFactorAsSharply()
        {
            // Arrange and Act
            (double plainSpeed, double plain) = FullEdgeTurnRate(Tuning.Default, SnowType.Groomed, 12.0, PlainRight);
            (double tuckSpeed, double tucked) = FullEdgeTurnRate(Tuning.Default, SnowType.Groomed, 12.0, TuckRight);

            // Assert
            Assert.That(tuckSpeed, Is.EqualTo(plainSpeed).Within(0.05));
            Assert.That(tucked / plain, Is.EqualTo(D(Tuning.Default.TuckTurnFactor)).Within(0.01));
        }

        [Test]
        public void CarveOnPowder_TurnsByPowdersTurnFactor()
        {
            // Arrange and Act: at 10 m/s both turns are held to TurnAcceleration, so the rates differ by the factor alone.
            (_, double groomed) = FullEdgeTurnRate(Tuning.Default, SnowType.Groomed, 10.0, PlainRight);
            (_, double powder) = FullEdgeTurnRate(Tuning.Default, SnowType.Powder, 10.0, PlainRight);

            // Assert
            Assert.That(powder / groomed, Is.EqualTo(D(Tuning.Default.PowderTurnFactor) / D(Tuning.Default.GroomedTurnFactor)).Within(0.02));
        }

        [Test]
        public void QuickTurn_TurnsThePathSoonerThanACarve_ButLosesMoreSpeed()
        {
            // Arrange and Act: 60 degrees from 15 m/s.
            (int carveSteps, double carveLoss) = TurnPath(Tuning.Default, SnowType.Groomed, 15.0, PlainRight, Math.PI / 3);
            (int quickSteps, double quickLoss) = TurnPath(Tuning.Default, SnowType.Groomed, 15.0, QuickTurnRight, Math.PI / 3);

            // Assert
            Assert.That(quickSteps, Is.LessThan(carveSteps));
            Assert.That(quickLoss, Is.GreaterThan(carveLoss));
        }

        [Test]
        public void QuickTurnHeldAtSpeed_LeadsThePathByAtMostQuickSlipMax()
        {
            // Arrange
            Tuning tuning = Tuning.Default;
            Simulation simulation = OnRunOutAt(SnowType.Groomed, tuning, 15.0);
            double most = 0;

            // Act
            for (int step = 0; step < Simulation.StepsPerSecond; step++)
            {
                simulation.Step(QuickTurnRight);
                most = Math.Max(most, Lead(simulation.State));
            }

            // Assert: up to the cap (one step's pivot of slack), and well past a carve's lead.
            Assert.That(simulation.State.Stance, Is.EqualTo(Stance.Quick));
            Assert.That(most, Is.LessThanOrEqualTo(D(tuning.QuickSlipMax) + D(tuning.QuickTurnRate) * D(Simulation.StepSeconds)));
            Assert.That(most, Is.GreaterThan(D(tuning.QuickSlipMax) / 2));
        }

        [Test]
        public void Drift_HoldsTheSkisDriftAngleAheadOfThePath_TurnsTighterThanACarve_AndCostsSomeSpeed()
        {
            // Arrange
            Tuning tuning = Tuning.Default;
            Simulation drift = OnRunOutAt(SnowType.Groomed, tuning, 15.0);
            Simulation carve = OnRunOutAt(SnowType.Groomed, tuning, 15.0);
            double start = Speed(drift.State);

            // Act: 0.6 s each.
            Ride(drift, DriftRight, 72);
            Ride(carve, PlainRight, 72);

            // Assert
            Assert.That(drift.State.Stance, Is.EqualTo(Stance.Drift));
            Assert.That(Lead(drift.State), Is.EqualTo(D(tuning.DriftAngle)).Within(0.03));
            Assert.That(PathAngle(drift.State), Is.GreaterThan(PathAngle(carve.State)));
            Assert.That(Speed(drift.State), Is.LessThan(Speed(carve.State)));
            Assert.That(Speed(drift.State), Is.GreaterThan(0.75 * start), "a drift costs some speed, not most of it");
        }

        [TestCase(96, true)]
        [TestCase(36, false)]
        public void DriftReleased_AfterDriftBoostTime_AddsDriftBoost_AndNotBefore(int driftSteps, bool boosted)
        {
            // Arrange: the same ride with and without the boost.
            Tuning tuning = Tuning.Default;
            Tuning noBoost = WithField(tuning, nameof(Tuning.DriftBoost), Fix64.Zero);
            Simulation with = OnRunOutAt(SnowType.Groomed, tuning, 15.0);
            Simulation without = OnRunOutAt(SnowType.Groomed, noBoost, 15.0);

            // Act: drift, then let go of grab (still steering).
            Ride(with, DriftRight, driftSteps);
            Ride(without, DriftRight, driftSteps);
            with.Step(PlainRight);
            without.Step(PlainRight);

            // Assert: the boost acts along the skis, which a drift holds at an angle to the path; both rides point the same way.
            Assert.That(with.State.Heading, Is.EqualTo(without.State.Heading));
            double gain = AlongSkis(with.State) - AlongSkis(without.State);
            Assert.That(gain, Is.EqualTo(boosted ? D(tuning.DriftBoost) : 0.0).Within(0.01));
        }

        [Test]
        public void HockeyStop_BackWithSteering_ThrowsTheSkisHockeyAngleAcross_AndStopsSoonerThanAStraightBrake()
        {
            // Arrange
            Tuning tuning = Tuning.Default;
            Simulation hockey = OnRunOutAt(SnowType.Groomed, tuning, 15.0);
            Simulation straight = OnRunOutAt(SnowType.Groomed, tuning, 15.0);
            Vec3 hockeyStart = hockey.State.Position, straightStart = straight.State.Position;

            // Act
            Ride(hockey, HockeyRight, 48);
            double lead = Lead(hockey.State);
            int hockeySteps = 48 + StepsUntil(hockey, HockeyRight, state => Speed(state) < 0.1);
            int straightSteps = StepsUntil(straight, Back, state => Speed(state) < 0.1);

            // Assert
            Assert.That(lead, Is.EqualTo(D(tuning.HockeyAngle)).Within(0.05));
            Assert.That(hockeySteps, Is.LessThan(straightSteps));
            Assert.That(Distance(hockey.State.Position, hockeyStart), Is.LessThan(Distance(straight.State.Position, straightStart)));
        }

        [Test]
        public void Skate_AlternatingSteeringOnTheFlat_GainsSpeed_UpToSkateMaxSpeed()
        {
            // Arrange: from 2 m/s on the level run-out, a push at every edge flip; and the same with no push.
            Tuning tuning = Tuning.Default;
            Simulation skate = OnRunOutAt(SnowType.Groomed, tuning, 2.0);
            Simulation glide = OnRunOutAt(SnowType.Groomed, WithField(tuning, nameof(Tuning.SkatePush), Fix64.Zero), 2.0);
            double start = Speed(skate.State), most = 0;

            // Act: left and right in turn, 0.35 s each, for 8 s.
            for (int step = 0; step < 8 * Simulation.StepsPerSecond; step++)
            {
                SimInput input = step / 42 % 2 == 0 ? PlainLeft : PlainRight;
                skate.Step(input);
                glide.Step(input);
                most = Math.Max(most, Speed(skate.State));
            }

            // Assert
            Assert.That(Speed(skate.State), Is.GreaterThan(start + 2.0));
            Assert.That(most, Is.LessThanOrEqualTo(D(tuning.SkateMaxSpeed) + 0.01));
            Assert.That(Speed(glide.State), Is.LessThan(start), "without the push the same steering only slows");
        }

        /// <summary>Steps <paramref name="input"/> until <paramref name="done"/> holds (at most 20 s); returns how many.</summary>
        private static int StepsUntil(Simulation simulation, SimInput input, Func<RiderState, bool> done)
        {
            for (int step = 1; step <= 20 * Simulation.StepsPerSecond; step++)
            {
                simulation.Step(input);
                if (done(simulation.State))
                {
                    return step;
                }
            }

            Assert.Fail("never happened");
            return -1;
        }

        /// <summary>The velocity's level component along the skis' heading, m/s.</summary>
        private static double AlongSkis(RiderState state) =>
            D(state.Velocity.X) * Math.Sin(D(state.Heading)) + D(state.Velocity.Z) * Math.Cos(D(state.Heading));

        /// <summary>A duration in whole steps, rounded up.</summary>
        private static int StepsOf(Fix64 seconds) => (int)Math.Ceiling(D(seconds) * Simulation.StepsPerSecond - 1e-9);

        /// <summary>
        /// On the <paramref name="snow"/> run-out at <paramref name="speed"/> or just below: holds <paramref name="turn"/> until
        /// the edge is full, then returns the speed and the heading's turn rate over one more step, radians per second.
        /// </summary>
        private static (double Speed, double Rate) FullEdgeTurnRate(Tuning tuning, SnowType snow, double speed, SimInput turn)
        {
            Simulation simulation = OnRunOutAt(snow, tuning, speed);
            while (simulation.State.EdgeAngle < tuning.MaxEdgeAngle)
            {
                simulation.Step(turn);
            }

            double before = Speed(simulation.State);
            Fix64 heading = simulation.State.Heading;
            simulation.Step(turn);
            return (before, D(Fix64.WrapAngle(simulation.State.Heading - heading)) / D(Simulation.StepSeconds));
        }

        private static Simulation Start(Heightmap heightmap, SnowMap snowMap, Tuning tuning, int x, int z) =>
            new Simulation(new RunSetup(Sport.Skis, 0, 1UL, Fix64.FromInt(x), Fix64.FromInt(z)), heightmap, snowMap, tuning);

        private static Simulation StartOn(Heightmap heightmap, SnowType type, Tuning tuning, int x, int z) =>
            Start(heightmap, TestTerrain.Uniform(heightmap, type), tuning, x, z);

        private static void Ride(Simulation simulation, SimInput input, int steps)
        {
            for (int step = 0; step < steps; step++)
            {
                simulation.Step(input);
            }
        }

        /// <summary>From rest at (64, 4) on the 0.4 incline, all one snow type.</summary>
        private static RiderState RideIncline(SnowType type, Tuning tuning, SimInput input, int steps)
        {
            Simulation simulation = StartOn(TestTerrain.InclineDownhillPlusZ(), type, tuning, 64, 4);
            Ride(simulation, input, steps);
            return simulation.State;
        }

        /// <summary>The run-out terrain from (64, 4): groomed run-in, <paramref name="zone"/> snow from row 34.</summary>
        private static Simulation StartOnRunOut(SnowType zone, Tuning tuning)
        {
            Heightmap runOut = TestTerrain.Build((row, column) => TestTerrain.MaxHeightMm - TestTerrain.DropMmPerCell * Math.Min(row, FlatRow));
            SnowMap snowMap = TestTerrain.Snow(runOut, (row, column) => row < ZoneRow ? SnowType.Groomed : zone);
            return Start(runOut, snowMap, tuning, 64, 4);
        }

        private static void RideOntoFlat(Simulation simulation)
        {
            for (int step = 0; step < 20 * Simulation.StepsPerSecond && D(simulation.State.Position.Z) < OnFlatZ; step++)
            {
                simulation.Step(NoInput);
            }

            Assert.That(D(simulation.State.Position.Z), Is.GreaterThanOrEqualTo(OnFlatZ), "the rider must reach the level run-out");
        }

        /// <summary>
        /// Ramps steering by one per step on the run-out's test snow, then holds it full for a second; returns the edge angle
        /// at the first skid, or null if it never skids.
        /// </summary>
        private static Fix64? SkidOnsetEdge(SnowType zone, Tuning tuning)
        {
            Simulation simulation = StartOnRunOut(zone, tuning);
            RideOntoFlat(simulation);
            for (int step = 1; step <= SimInput.SteeringMax + Simulation.StepsPerSecond; step++)
            {
                simulation.Step(SimInput.Pack(SimButtons.None, Math.Min(step, SimInput.SteeringMax)));
                if (simulation.State.Skidding)
                {
                    return simulation.State.EdgeAngle;
                }
            }

            return null;
        }

        /// <summary>On the level run-out's <paramref name="zone"/> snow, braked to <paramref name="speed"/> or just below.</summary>
        private static Simulation OnRunOutAt(SnowType zone, Tuning tuning, double speed)
        {
            Simulation simulation = StartOnRunOut(zone, tuning);
            RideOntoFlat(simulation);
            while (Speed(simulation.State) > speed)
            {
                simulation.Step(Back);
            }

            return simulation;
        }


        /// <summary>
        /// On the <paramref name="zone"/> run-out at <paramref name="speed"/> or just below: holds <paramref name="turn"/>
        /// until the path has turned <paramref name="angle"/>; returns the steps taken and the speed lost.
        /// </summary>
        private static (int Steps, double SpeedLoss) TurnPath(Tuning tuning, SnowType zone, double speed, SimInput turn, double angle)
        {
            Simulation simulation = OnRunOutAt(zone, tuning, speed);
            double startPath = PathAngle(simulation.State), startSpeed = Speed(simulation.State);
            int steps = 0;
            while (PathAngle(simulation.State) - startPath < angle)
            {
                Assert.That(steps, Is.LessThan(5 * Simulation.StepsPerSecond), "the path never turned " + angle);
                simulation.Step(turn);
                steps++;
            }

            return (steps, startSpeed - Speed(simulation.State));
        }


        /// <summary>From rest at (64, 4) on the 0.4 incline, all one snow type: 3 s straight, then 1 s of <paramref name="turn"/>.</summary>
        private static RiderState RideInclineThenTurn(SnowType type, Tuning tuning, SimInput turn)
        {
            Simulation simulation = StartOn(TestTerrain.InclineDownhillPlusZ(), type, tuning, 64, 4);
            Ride(simulation, NoInput, 3 * Simulation.StepsPerSecond);
            Ride(simulation, turn, Simulation.StepsPerSecond);
            return simulation.State;
        }


        /// <summary>The direction of travel seen from above, radians clockwise from +Z, like the heading.</summary>
        private static double PathAngle(RiderState state) => Math.Atan2(D(state.Velocity.X), D(state.Velocity.Z));

        /// <summary>How far the skis lead the path, radians, wrapped to [-pi, pi].</summary>
        private static double Lead(RiderState state)
        {
            double lead = D(state.Heading) - PathAngle(state);
            return Math.Atan2(Math.Sin(lead), Math.Cos(lead));
        }

        /// <summary>
        /// A 4096 m square plane falling 0.5 toward +Z. Heights are whole multiples of the 32 mm sample step,
        /// so every cell has exactly the same gradient.
        /// </summary>
        private static Heightmap LongIncline()
        {
            const int sizeMm = 4_096_000, sampleMm = 32, dropMmPerCell = 32_000;
            const int maxHeightMm = 65_535 * sampleMm;
            byte[] bytes = TestTerrain.R16Bytes(TestTerrain.Resolution, 0, maxHeightMm, (row, column) => 64 * dropMmPerCell - dropMmPerCell * row);
            return Heightmap.FromR16(bytes, new HeightmapSpec(sizeMm, sizeMm, 0, maxHeightMm));
        }

        /// <summary>Rides 45 s straight down from (2048, 64); returns the final speed and its relative change over the last second.</summary>
        private static (double Speed, double Drift) TerminalSpeed(Heightmap slope, SnowMap snowMap, Tuning tuning, SimInput input)
        {
            Simulation simulation = Start(slope, snowMap, tuning, 2048, 64);
            Ride(simulation, input, 44 * Simulation.StepsPerSecond);
            double before = Speed(simulation.State);
            Ride(simulation, input, Simulation.StepsPerSecond);
            double after = Speed(simulation.State);
            return (after, Math.Abs(after - before) / after);
        }

        private static Tuning WithField(Tuning tuning, string name, Fix64 value)
        {
            FieldInfo field = typeof(Tuning).GetField(name);
            Assert.That(field, Is.Not.Null, "Tuning has no field " + name);
            object boxed = tuning;
            field.SetValue(boxed, value);
            return (Tuning)boxed;
        }

        private static Fix64 ReadField(Tuning tuning, string name) => (Fix64)typeof(Tuning).GetField(name).GetValue(tuning);

        private static double Speed(RiderState state)
        {
            double x = D(state.Velocity.X), y = D(state.Velocity.Y), z = D(state.Velocity.Z);
            return Math.Sqrt(x * x + y * y + z * z);
        }

        private static double Distance(Vec3 a, Vec3 b)
        {
            double x = D(a.X - b.X), y = D(a.Y - b.Y), z = D(a.Z - b.Z);
            return Math.Sqrt(x * x + y * y + z * z);
        }

        private static double D(Fix64 value) => TestTerrain.ToDouble(value);
    }
}
