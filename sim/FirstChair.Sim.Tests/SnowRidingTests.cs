using System;
using System.Reflection;
using FirstChair.Sim;
using NUnit.Framework;

namespace FirstChair.Sim.Tests
{
    /// <summary>
    /// Story 002 scripted-input tests: steering and edging, carve and skid, tuck, brake, poling on flats only,
    /// and the five snow types. They pin behaviour, not final numbers: expectations come from
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
        public void FullRightSteering_FromRest_EdgesAtEdgeRateUpToMaxEdgeAngle()
        {
            // Arrange
            Tuning tuning = Tuning.Default;
            Simulation simulation = StartOn(TestTerrain.Flat(), SnowType.Groomed, tuning, 64, 64);
            SimInput right = SimInput.Pack(SimButtons.Right, 0);

            // Act
            simulation.Step(right);
            Fix64 afterOneStep = simulation.State.EdgeAngle;
            Ride(simulation, right, Simulation.StepsPerSecond - 1);

            // Assert
            Assert.That(afterOneStep, Is.EqualTo(tuning.EdgeRate * Simulation.StepSeconds));
            Assert.That(simulation.State.EdgeAngle, Is.EqualTo(tuning.MaxEdgeAngle));
            Assert.That(simulation.State.Heading.Raw, Is.GreaterThan(0L), "a resting rider pivots toward the steering");
        }

        [TestCase(SimButtons.Right, 1)]
        [TestCase(SimButtons.Left, -1)]
        public void SteeringOnIncline_FromRest_TurnsAndDriftsTowardThatSide(SimButtons button, int side)
        {
            // Arrange: the rider faces +Z, so right is +X.
            Simulation simulation = StartOn(TestTerrain.InclineDownhillPlusZ(), SnowType.Groomed, Tuning.Default, 64, 4);

            // Act
            Ride(simulation, SimInput.Pack(button, 0), Simulation.StepsPerSecond);

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
        public void SteeringBelowGripLimit_OnGroomedFlat_CarvesWithVelocityAlongSkisAndLittleSpeedLoss()
        {
            // Arrange: two identical rides onto the level run-out; one steers at half the grip limit for its speed.
            Tuning tuning = Tuning.Default;
            Simulation straight = StartOnRunOut(SnowType.Groomed, tuning);
            Simulation carving = StartOnRunOut(SnowType.Groomed, tuning);
            RideOntoFlat(straight);
            RideOntoFlat(carving);
            var steering = (int)(LimitSteering(tuning, SnowType.Groomed, Speed(carving.State)) / 2);
            Fix64 startHeading = carving.State.Heading;
            int skiddingSteps = 0;
            double worstMisalignment = 0;

            // Act
            for (int step = 0; step < 2 * Simulation.StepsPerSecond; step++)
            {
                straight.Step(NoInput);
                carving.Step(SimInput.Pack(SimButtons.None, steering));
                RiderState state = carving.State;
                skiddingSteps += state.Skidding ? 1 : 0;

                // On level ground the skis point along (sin heading, cos heading).
                double travel = Math.Atan2(D(state.Velocity.X), D(state.Velocity.Z));
                worstMisalignment = Math.Max(worstMisalignment, Math.Abs(travel - D(state.Heading)));
            }

            // Assert
            Assert.That(steering, Is.GreaterThan(10), "the entry speed must leave room for a real carve");
            Assert.That(skiddingSteps, Is.EqualTo(0));
            Assert.That(worstMisalignment, Is.LessThan(1e-6), "velocity must follow the ski heading");
            Assert.That(D(carving.State.Heading - startHeading), Is.GreaterThan(0.4), "the carve must turn the rider");
            Assert.That(Speed(carving.State), Is.GreaterThanOrEqualTo(0.98 * Speed(straight.State)));
        }

        [Test]
        public void SteeringPastGripLimit_OnGroomedFlat_SkidsLosingMoreSpeedTheBiggerTheSlipAngle()
        {
            // Arrange: rides onto the level run-out, then straight, 1.3 times the grip limit, and full lock,
            // plus full lock again with no skid drag.
            Tuning tuning = Tuning.Default;
            Tuning noSkidDrag = tuning;
            noSkidDrag.SkidDrag = Fix64.Zero;
            Simulation straight = StartOnRunOut(SnowType.Groomed, tuning);
            Simulation over = StartOnRunOut(SnowType.Groomed, tuning);
            Simulation full = StartOnRunOut(SnowType.Groomed, tuning);
            Simulation fullNoSkidDrag = StartOnRunOut(SnowType.Groomed, noSkidDrag);
            foreach (Simulation simulation in new[] { straight, over, full, fullNoSkidDrag })
            {
                RideOntoFlat(simulation);
            }

            var overSteering = (int)Math.Round(1.3 * LimitSteering(tuning, SnowType.Groomed, Speed(over.State)));
            SimInput fullLock = SimInput.Pack(SimButtons.Right, 0);
            double overSlip = 0, fullSlip = 0;
            int overSkids = 0, fullSkids = 0;

            // Act
            for (int step = 0; step < Simulation.StepsPerSecond; step++)
            {
                straight.Step(NoInput);
                over.Step(SimInput.Pack(SimButtons.None, overSteering));
                full.Step(fullLock);
                fullNoSkidDrag.Step(fullLock);
                overSlip += D(over.State.SlipAngle);
                fullSlip += D(full.State.SlipAngle);
                overSkids += over.State.Skidding ? 1 : 0;
                fullSkids += full.State.Skidding ? 1 : 0;
            }

            // Assert
            Assert.That(overSteering, Is.LessThan(SimInput.SteeringMax), "the moderate skid must steer less than full lock");
            Assert.That(overSkids, Is.GreaterThan(0));
            Assert.That(fullSkids, Is.GreaterThan(0));
            Assert.That(overSlip, Is.GreaterThan(0.0));
            Assert.That(fullSlip, Is.GreaterThan(overSlip), "full lock must slip more");
            Assert.That(Speed(over.State), Is.LessThan(Speed(straight.State)));
            Assert.That(Speed(full.State), Is.LessThan(Speed(over.State)), "more slip must cost more speed");
            Assert.That(Speed(fullNoSkidDrag.State), Is.GreaterThan(Speed(full.State)), "the slip-angle drag must cost speed");
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
        public void RemovingOneTypesGrip_ChangesOnlyRidesOnThatType([Values] SnowType changed)
        {
            // Arrange: with no grip on one type, any turn on it skids.
            Tuning baseline = Tuning.Default;
            Tuning variant = WithField(baseline, changed + "Grip", Fix64.Zero);
            SimInput right = SimInput.Pack(SimButtons.Right, 0);

            foreach (SnowType ridden in (SnowType[])Enum.GetValues(typeof(SnowType)))
            {
                // Act
                RiderState before = RideIncline(ridden, baseline, right, Simulation.StepsPerSecond);
                RiderState after = RideIncline(ridden, variant, right, Simulation.StepsPerSecond);

                // Assert
                if (ridden == changed)
                {
                    Assert.That(after.Skidding, Is.True, ridden + " must use " + changed + "Grip");
                }
                else
                {
                    Assert.That(after, Is.EqualTo(before), ridden + " must not use " + changed + "Grip");
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

        [Test]
        public void Tuck_SameSteering_TurnsLessThanStanding()
        {
            // Arrange
            Tuning noTurnPenalty = Tuning.Default;
            noTurnPenalty.TuckTurnFactor = Fix64.One;
            SimInput right = SimInput.Pack(SimButtons.Right, 0);
            SimInput rightTucked = SimInput.Pack(SimButtons.Right | SimButtons.Forward, 0);

            // Act
            Fix64 standing = RideIncline(SnowType.Groomed, Tuning.Default, right, Simulation.StepsPerSecond / 2).Heading;
            Fix64 tucked = RideIncline(SnowType.Groomed, Tuning.Default, rightTucked, Simulation.StepsPerSecond / 2).Heading;
            Fix64 tuckedNoPenalty = RideIncline(SnowType.Groomed, noTurnPenalty, rightTucked, Simulation.StepsPerSecond / 2).Heading;

            // Assert
            Assert.That(tucked.Raw, Is.GreaterThan(0L));
            Assert.That(tucked.Raw, Is.LessThan(standing.Raw));
            Assert.That(tuckedNoPenalty, Is.EqualTo(standing), "the slower turn must come from TuckTurnFactor");
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

        /// <summary>The steering at which speed * turn rate equals the snow's grip.</summary>
        private static double LimitSteering(Tuning tuning, SnowType type, double speed) =>
            SimInput.SteeringMax * D(tuning.Grip(type)) / (speed * D(tuning.TurnRate));

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
