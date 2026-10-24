using System;
using System.Reflection;
using FirstChair.Sim;
using NUnit.Framework;

namespace FirstChair.Sim.Tests
{
    /// <summary>
    /// Story 002 and Story 008 scripted-input tests: steering and edging, carves and quick turns, tuck, brake, poling on
    /// flats only, and the five snow types. They pin behaviour, not final numbers: expectations come from
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
        private static readonly SimInput QuickRight = SimInput.Pack(SimButtons.Right, 0);
        private static readonly SimInput CarveRight = SimInput.Pack(SimButtons.Forward | SimButtons.Right, 0);

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
        public void FullRightSteering_FromRest_EdgesAtEdgeRateUpToMaxEdgeAngle()
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
            Assert.That(afterOneStep, Is.EqualTo(tuning.EdgeRate * Simulation.StepSeconds));
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
        public void Carve_ForwardAndSteeringOnGroomedFlat_FollowsTheSkisWithoutSkiddingAndKeepsSpeed()
        {
            // Arrange: onto the level run-out, fast enough for a full carve.
            Tuning tuning = Tuning.Default;
            Simulation simulation = StartOnRunOut(SnowType.Groomed, tuning);
            RideOntoFlat(simulation);
            double entrySpeed = Speed(simulation.State);
            Fix64 startHeading = simulation.State.Heading;
            int carvingSteps = 0, skiddingSteps = 0;
            double worstMisalignment = 0;

            // Act
            for (int step = 0; step < Simulation.StepsPerSecond; step++)
            {
                simulation.Step(CarveRight);
                RiderState state = simulation.State;
                carvingSteps += state.Stance == Stance.Carve ? 1 : 0;
                skiddingSteps += state.Skidding ? 1 : 0;

                // On level ground the skis point along (sin heading, cos heading).
                worstMisalignment = Math.Max(worstMisalignment, Math.Abs(PathAngle(state) - D(state.Heading)));
            }

            // Assert: the speed falls by no more than the snow's friction on the carve's heaviest load, plus air drag.
            double g = D(tuning.Gravity);
            double turnGrip = D(tuning.CarveTurnShare) * D(tuning.GroomedCarveGrip);
            double mostLoss = (D(tuning.GroomedDrag) * Math.Sqrt(g * g + turnGrip * turnGrip)
                + D(tuning.AirDrag) * D(tuning.CarveAirDragFactor) * entrySpeed * entrySpeed) * D(Simulation.StepSeconds) * Simulation.StepsPerSecond;
            Assert.That(entrySpeed, Is.GreaterThan(D(tuning.CarveMinSpeed + tuning.CarveBlendSpeed)), "the entry speed must allow a full carve");
            Assert.That(carvingSteps, Is.EqualTo(Simulation.StepsPerSecond));
            Assert.That(skiddingSteps, Is.EqualTo(0));
            Assert.That(worstMisalignment, Is.LessThan(1e-6), "velocity must follow the ski heading");
            Assert.That(D(simulation.State.Heading - startHeading), Is.GreaterThan(0.5), "the carve must turn the rider");
            Assert.That(entrySpeed - Speed(simulation.State), Is.InRange(0.0, mostLoss));
        }

        [Test]
        public void Carve_SnowFrictionActsOnTheTurnsLoad_SoATightCarveCostsALittleSpeed()
        {
            // Arrange: on the run-out's slush, which carves and has the most friction, a carve against a straight run with
            // W, with the tuck's air drag set to the carve's so only the snow tells them apart; and both again without
            // slush friction. The run-in is groomed, so every ride reaches the slush at the same speed.
            Tuning tuning = Tuning.Default;
            tuning.TuckAirDragFactor = tuning.CarveAirDragFactor;
            Tuning frictionless = tuning;
            frictionless.SlushDrag = Fix64.Zero;
            double g = D(tuning.Gravity), mu = D(tuning.SlushDrag), dt = D(Simulation.StepSeconds);

            // Act
            (double extraLoss, double loadIntegral) = CarveExtraLoss(tuning, g, mu, dt);
            (double extraLossFrictionless, _) = CarveExtraLoss(frictionless, g, mu, dt);

            // Assert: friction on the turn's load sqrt(g^2 + (v w)^2) instead of g alone costs mu times the difference.
            Assert.That(loadIntegral, Is.GreaterThan(0.1), "the carve must load the skis");
            Assert.That(extraLoss - extraLossFrictionless, Is.EqualTo(loadIntegral).Within(10).Percent);
        }

        [Test]
        public void CarveAtFullEdge_IsCarveRadiusMinWhenSlow_AndSpeedSquaredOverItsGripWhenFast()
        {
            // Arrange: a speed where the sidecut limits the carve (speed^2 / grip below CarveRadiusMin), and the run-out's
            // full entry speed, where the grip does.
            Tuning tuning = Tuning.Default;
            double turnGrip = D(tuning.CarveTurnShare) * D(tuning.GroomedCarveGrip);
            double slowTarget = D(tuning.CarveMinSpeed + tuning.CarveBlendSpeed) + 1;

            // Act
            (double slowSpeed, double slowRadius) = FullEdgeRadius(tuning, slowTarget, CarveRight);
            (double fastSpeed, double fastRadius) = FullEdgeRadius(tuning, double.MaxValue, CarveRight);

            // Assert
            Assert.That(slowSpeed * slowSpeed / turnGrip, Is.LessThan(D(tuning.CarveRadiusMin)), "the slow carve must be sidecut-limited");
            Assert.That(fastSpeed * fastSpeed / turnGrip, Is.GreaterThan(D(tuning.CarveRadiusMin)), "the fast carve must be grip-limited");
            Assert.That(slowRadius, Is.EqualTo(D(tuning.CarveRadiusMin)).Within(0.1).Percent);
            Assert.That(fastRadius, Is.EqualTo(fastSpeed * fastSpeed / turnGrip).Within(0.1).Percent);
            Assert.That(fastRadius, Is.GreaterThan(slowRadius));
        }

        [Test]
        public void ForwardSteering_BelowCarveMinSpeed_PivotsExactlyLikeAQuickTurn()
        {
            // Arrange: from rest on the incline, so the rider stays below CarveMinSpeed; and a pair at full carving speed.
            Tuning tuning = Tuning.Default;
            Simulation slowForward = StartOn(TestTerrain.InclineDownhillPlusZ(), SnowType.Groomed, tuning, 64, 4);
            Simulation slowQuick = StartOn(TestTerrain.InclineDownhillPlusZ(), SnowType.Groomed, tuning, 64, 4);
            Simulation fastForward = StartOnRunOut(SnowType.Groomed, tuning);
            Simulation fastQuick = StartOnRunOut(SnowType.Groomed, tuning);
            RideOntoFlat(fastForward);
            RideOntoFlat(fastQuick);

            // Act
            for (int step = 0; step < Simulation.StepsPerSecond; step++)
            {
                slowForward.Step(CarveRight);
                slowQuick.Step(QuickRight);
                fastForward.Step(CarveRight);
                fastQuick.Step(QuickRight);
                Assert.That(slowForward.State.Stance, Is.EqualTo(Stance.Stand), "below CarveMinSpeed there is no carve");
            }

            // Assert
            Assert.That(Speed(slowForward.State), Is.LessThan(D(tuning.CarveMinSpeed)), "the rider must stay below CarveMinSpeed");
            Assert.That(slowForward.State.Heading, Is.EqualTo(slowQuick.State.Heading));
            Assert.That(slowForward.State.Velocity, Is.EqualTo(slowQuick.State.Velocity));
            Assert.That(slowForward.State.Position, Is.EqualTo(slowQuick.State.Position));
            Assert.That(fastForward.State.Heading, Is.Not.EqualTo(fastQuick.State.Heading), "at speed W must carve instead");
        }

        [Test]
        public void QuickTurn_AtModerateSpeed_TurnsThePathSoonerThanACarveButLosesMoreSpeed()
        {
            // Arrange: both brake to the same moderate speed on the level run-out.
            Tuning tuning = Tuning.Default;
            double moderate = D(tuning.CarveMinSpeed + tuning.CarveBlendSpeed) + 1;

            // Act: turn the path 45 degrees.
            (int carveSteps, double carveLoss) = TurnPath(tuning, SnowType.Groomed, moderate, CarveRight, Math.PI / 4);
            (int quickSteps, double quickLoss) = TurnPath(tuning, SnowType.Groomed, moderate, QuickRight, Math.PI / 4);

            // Assert
            Assert.That(quickSteps, Is.LessThan(carveSteps));
            Assert.That(quickLoss, Is.GreaterThan(carveLoss), "over the same change of direction a quick turn costs more");
        }

        [Test]
        public void QuickTurn_PivotsMoreSlowlyTheFasterTheRiderGoes()
        {
            // Arrange: the first step of a quick turn from a straight run, below and at carving speed.
            Tuning tuning = Tuning.Default;
            double edgeFraction = D(tuning.EdgeRate * Simulation.StepSeconds / tuning.MaxEdgeAngle);
            double Expected(double speed) =>
                edgeFraction * D(tuning.PivotRate) * D(tuning.GroomedPivotFactor) / (1 + speed / D(tuning.PivotFadeSpeed));

            // Act
            (double slowSpeed, double slowRate) = FirstStepPivotRate(tuning, D(tuning.CarveMinSpeed) - 1);
            (double fastSpeed, double fastRate) = FirstStepPivotRate(tuning, double.MaxValue);

            // Assert
            Assert.That(fastSpeed, Is.GreaterThan(2 * slowSpeed));
            Assert.That(slowRate, Is.EqualTo(Expected(slowSpeed)).Within(0.01).Percent);
            Assert.That(fastRate, Is.EqualTo(Expected(fastSpeed)).Within(0.01).Percent);
            Assert.That(fastRate, Is.LessThan(slowRate));
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
            double moderate = D(tuning.CarveMinSpeed + tuning.CarveBlendSpeed) + 1;
            Simulation straight = OnRunOutAt(SnowType.Groomed, tuning, moderate);
            Simulation half = OnRunOutAt(SnowType.Groomed, tuning, moderate);
            Simulation full = OnRunOutAt(SnowType.Groomed, tuning, moderate);
            Simulation fullNoSkidDrag = OnRunOutAt(SnowType.Groomed, noSkidDrag, moderate);
            Simulation fullStiffer = OnRunOutAt(SnowType.Groomed, stiffer, moderate);
            SimInput halfRight = SimInput.Pack(SimButtons.None, SimInput.SteeringMax / 2);
            double halfSlip = 0, fullSlip = 0, stifferSlip = 0;

            // Act
            for (int step = 0; step < Simulation.StepsPerSecond / 2; step++)
            {
                straight.Step(NoInput);
                half.Step(halfRight);
                full.Step(QuickRight);
                fullNoSkidDrag.Step(QuickRight);
                fullStiffer.Step(QuickRight);
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
        public void QuickTurnHeldAtSpeed_LeadsThePathByAtMostPivotSlipMax_AndChecksSpeed()
        {
            // Arrange: at the run-out's full entry speed, straight and a held quick turn; and the held turn again with
            // half the cap.
            Tuning tuning = Tuning.Default;
            Tuning halfCap = tuning;
            halfCap.PivotSlipMax = tuning.PivotSlipMax / Fix64.FromInt(2);
            Simulation straight = StartOnRunOut(SnowType.Groomed, tuning);
            Simulation quick = StartOnRunOut(SnowType.Groomed, tuning);
            Simulation tight = StartOnRunOut(SnowType.Groomed, halfCap);
            foreach (Simulation simulation in new[] { straight, quick, tight })
            {
                RideOntoFlat(simulation);
            }

            double oneStep = D(tuning.PivotRate * tuning.GroomedPivotFactor * Simulation.StepSeconds);
            double quickLead = 0, tightLead = 0;
            int skiddingSteps = 0;

            // Act
            for (int step = 0; step < Simulation.StepsPerSecond; step++)
            {
                straight.Step(NoInput);
                quick.Step(QuickRight);
                tight.Step(QuickRight);
                quickLead = Math.Max(quickLead, Math.Abs(Lead(quick.State)));
                tightLead = Math.Max(tightLead, Math.Abs(Lead(tight.State)));
                skiddingSteps += quick.State.Skidding ? 1 : 0;
            }

            // Assert
            double tightCap = D(halfCap.PivotSlipMax);
            Assert.That(quickLead, Is.LessThanOrEqualTo(D(tuning.PivotSlipMax) + oneStep));
            Assert.That(quickLead, Is.GreaterThan(tightCap + oneStep), "the turn must lead further than the half cap allows");
            Assert.That(tightLead, Is.InRange(tightCap - oneStep, tightCap + oneStep), "the lead must stop at PivotSlipMax");
            Assert.That(skiddingSteps, Is.GreaterThan(0), "at speed a held quick turn skids");
            Assert.That(Speed(quick.State), Is.LessThan(0.9 * Speed(straight.State)), "the skid must check speed");
        }

        [TestCase(SnowType.Groomed, true)]
        [TestCase(SnowType.Ice, true)]
        [TestCase(SnowType.Slush, true)]
        [TestCase(SnowType.Powder, false)]
        [TestCase(SnowType.Crud, false)]
        public void ForwardSteering_CarvesOnlyOnSnowWithACarveGrip(SnowType snow, bool carves)
        {
            // Arrange: onto the level run-out's test snow at full carving speed.
            Simulation forward = StartOnRunOut(snow, Tuning.Default);
            Simulation quick = StartOnRunOut(snow, Tuning.Default);
            RideOntoFlat(forward);
            RideOntoFlat(quick);
            int carvingSteps = 0;

            // Act
            for (int step = 0; step < Simulation.StepsPerSecond; step++)
            {
                forward.Step(CarveRight);
                quick.Step(QuickRight);
                carvingSteps += forward.State.Stance == Stance.Carve ? 1 : 0;
            }

            // Assert
            Assert.That(forward.State.Snow, Is.EqualTo(snow));
            if (carves)
            {
                Assert.That(carvingSteps, Is.EqualTo(Simulation.StepsPerSecond));
                Assert.That(forward.State.Velocity, Is.Not.EqualTo(quick.State.Velocity), "a carve must ride differently");
            }
            else
            {
                Assert.That(carvingSteps, Is.EqualTo(0));
                Assert.That(forward.State.Heading, Is.EqualTo(quick.State.Heading), "W plus steering must turn like a quick turn");
                Assert.That(forward.State.Velocity, Is.EqualTo(quick.State.Velocity));
                Assert.That(forward.State.Position, Is.EqualTo(quick.State.Position));
            }
        }

        [Test]
        public void RampingEdge_OnIceVersusGroomed_IceSkidsAtALowerEdgeAngle()
        {
            // Arrange: the same groomed run-in, then the edge ramps up on the test snow. Ice's lower drag carries
            // the rider in a little faster, which alone lowers its skid onset, so a variant with equal drags
            // leaves grip as the only difference.
            Tuning equalDrag = Tuning.Default;
            equalDrag.IceDrag = equalDrag.GroomedDrag;

            // Act
            Fix64 groomedOnset = SkidOnsetEdge(SnowType.Groomed, Tuning.Default);
            Fix64 iceOnset = SkidOnsetEdge(SnowType.Ice, Tuning.Default);
            Fix64 groomedOnsetEqualDrag = SkidOnsetEdge(SnowType.Groomed, equalDrag);
            Fix64 iceOnsetEqualDrag = SkidOnsetEdge(SnowType.Ice, equalDrag);

            // Assert
            Assert.That(iceOnset.Raw, Is.GreaterThan(0L));
            Assert.That(iceOnset.Raw, Is.LessThan(groomedOnset.Raw));
            Assert.That(iceOnsetEqualDrag.Raw, Is.LessThan(groomedOnsetEqualDrag.Raw), "ice's lower grip must make it skid earlier");
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
            [Values("CarveGrip", "SkidGrip", "SkidDrag", "PivotFactor")] string field, [Values] SnowType changed)
        {
            // Arrange: the field halved on one type, or for snow that can't carve, given groomed snow's carve grip. Carve
            // grip acts in a carve (W and steering), the rest in a quick turn (steering alone).
            Tuning baseline = Tuning.Default;
            Fix64 value = ReadField(baseline, changed + field);
            Tuning variant = WithField(baseline, changed + field, value == Fix64.Zero ? baseline.GroomedCarveGrip : value / Fix64.FromInt(2));
            SimInput turn = field == "CarveGrip" ? CarveRight : QuickRight;

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

        /// <summary>Ramps steering by one per step on the run-out's test snow; returns the edge angle at the first skid.</summary>
        private static Fix64 SkidOnsetEdge(SnowType zone, Tuning tuning)
        {
            Simulation simulation = StartOnRunOut(zone, tuning);
            RideOntoFlat(simulation);
            for (int step = 1; step <= 2 * SimInput.SteeringMax; step++)
            {
                simulation.Step(SimInput.Pack(SimButtons.None, Math.Min(step, SimInput.SteeringMax)));
                if (simulation.State.Skidding)
                {
                    return simulation.State.EdgeAngle;
                }
            }

            Assert.Fail(zone + " never skidded");
            return Fix64.Zero;
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
        /// On the groomed run-out at <paramref name="speed"/> or just below: holds <paramref name="turn"/> until the edge is
        /// full, then returns the speed and the radius, speed / turn rate, over one more step.
        /// </summary>
        private static (double Speed, double Radius) FullEdgeRadius(Tuning tuning, double speed, SimInput turn)
        {
            Simulation simulation = OnRunOutAt(SnowType.Groomed, tuning, speed);
            while (simulation.State.EdgeAngle < tuning.MaxEdgeAngle)
            {
                simulation.Step(turn);
            }

            double before = Speed(simulation.State);
            Fix64 heading = simulation.State.Heading;
            simulation.Step(turn);
            return (before, before / (D(simulation.State.Heading - heading) / D(Simulation.StepSeconds)));
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

        /// <summary>
        /// On the groomed run-out at <paramref name="speed"/> or just below: the speed, and the heading's turn rate over the
        /// first step of a quick turn, radians per second.
        /// </summary>
        private static (double Speed, double Rate) FirstStepPivotRate(Tuning tuning, double speed)
        {
            Simulation simulation = OnRunOutAt(SnowType.Groomed, tuning, speed);
            double before = Speed(simulation.State);
            Fix64 heading = simulation.State.Heading;
            simulation.Step(QuickRight);
            return (before, D(simulation.State.Heading - heading) / D(Simulation.StepSeconds));
        }

        /// <summary>From rest at (64, 4) on the 0.4 incline, all one snow type: 3 s straight, then 1 s of <paramref name="turn"/>.</summary>
        private static RiderState RideInclineThenTurn(SnowType type, Tuning tuning, SimInput turn)
        {
            Simulation simulation = StartOn(TestTerrain.InclineDownhillPlusZ(), type, tuning, 64, 4);
            Ride(simulation, NoInput, 3 * Simulation.StepsPerSecond);
            Ride(simulation, turn, Simulation.StepsPerSecond);
            return simulation.State;
        }

        /// <summary>
        /// One second on the run-out's slush: a W carve and a straight W run. Returns how much more speed the carve lost, and
        /// mu times the integral of sqrt(g^2 + (v w)^2) - g over the carve's steps, from its speed v and turn rate w.
        /// </summary>
        private static (double ExtraLoss, double LoadIntegral) CarveExtraLoss(Tuning tuning, double g, double mu, double dt)
        {
            Simulation carve = StartOnRunOut(SnowType.Slush, tuning);
            Simulation straight = StartOnRunOut(SnowType.Slush, tuning);
            RideOntoFlat(carve);
            RideOntoFlat(straight);
            double carveStart = Speed(carve.State), straightStart = Speed(straight.State), loadIntegral = 0;
            for (int step = 0; step < Simulation.StepsPerSecond; step++)
            {
                double speed = Speed(carve.State);
                Fix64 heading = carve.State.Heading;
                carve.Step(CarveRight);
                straight.Step(Forward);
                double sideways = speed * D(carve.State.Heading - heading) / dt;
                loadIntegral += mu * (Math.Sqrt(g * g + sideways * sideways) - g) * dt;
            }

            Assert.That(carve.State.Stance, Is.EqualTo(Stance.Carve));
            return (carveStart - Speed(carve.State) - (straightStart - Speed(straight.State)), loadIntegral);
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
