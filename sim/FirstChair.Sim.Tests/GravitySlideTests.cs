using System;
using System.Collections.Generic;
using FirstChair.Sim;
using NUnit.Framework;

namespace FirstChair.Sim.Tests
{
    /// <summary>The minimal rider model: a rider at rest with no input slides downhill under gravity.</summary>
    public class GravitySlideTests
    {
        private static readonly SimInput NoInput = SimInput.Pack(SimButtons.None, 0);

        [Test]
        public void RiderOnIncline_NoInput_SlidesDownhill()
        {
            // Arrange: the plane falls toward +Z.
            Heightmap heightmap = TestTerrain.InclineDownhillPlusZ();
            Simulation simulation = StartAt(heightmap, 64, 4);
            RiderState start = simulation.State;
            Fix64 previousSpeed = Fix64.Zero;

            // Act and Assert, step by step for 2 s.
            for (int step = 0; step < 2 * Simulation.StepsPerSecond; step++)
            {
                simulation.Step(NoInput);
                RiderState state = simulation.State;
                heightmap.Sample(state.Position.X, state.Position.Z, out Fix64 terrainHeight, out _);

                Assert.That(state.Velocity.Z.Raw, Is.GreaterThan(previousSpeed.Raw), "speed must grow at step " + state.Tick);
                Assert.That(state.Position.Y, Is.EqualTo(terrainHeight), "rider must stay on the snow at step " + state.Tick);
                previousSpeed = state.Velocity.Z;
            }

            RiderState end = simulation.State;
            Assert.That(end.Position.Z.Raw, Is.GreaterThan((start.Position.Z + Fix64.FromInt(5)).Raw));
            Assert.That(end.Position.X, Is.EqualTo(start.Position.X));
            Assert.That(end.Position.Y.Raw, Is.LessThan(start.Position.Y.Raw));
            Assert.That(end.Velocity.Y.Raw, Is.LessThan(0L));
        }

        [Test]
        public void RiderOnMirroredIncline_NoInput_SlidesTowardMinusZ()
        {
            // Arrange: the plane falls toward -Z.
            Heightmap heightmap = TestTerrain.InclineDownhillMinusZ();
            Simulation simulation = StartAt(heightmap, 64, 124);
            RiderState start = simulation.State;

            // Act
            for (int step = 0; step < 2 * Simulation.StepsPerSecond; step++)
            {
                simulation.Step(NoInput);
            }

            // Assert
            RiderState end = simulation.State;
            Assert.That(end.Position.Z.Raw, Is.LessThan((start.Position.Z - Fix64.FromInt(5)).Raw));
            Assert.That(end.Velocity.Z.Raw, Is.LessThan(0L));
            Assert.That(end.Position.X, Is.EqualTo(start.Position.X));
        }

        [Test]
        public void RiderOnFlat_NoInput_StaysExactlyPut()
        {
            // Arrange
            Simulation simulation = StartAt(TestTerrain.Flat(), 64, 64);
            Vec3 start = simulation.State.Position;

            // Act
            for (int step = 0; step < 10 * Simulation.StepsPerSecond; step++)
            {
                simulation.Step(NoInput);
            }

            // Assert
            Assert.That(simulation.State.Position, Is.EqualTo(start));
            Assert.That(simulation.State.Velocity, Is.EqualTo(Vec3.Zero));
        }

        [Test]
        public void SlideAfterOneSecond_MatchesClosedFormWithinTwoPercent()
        {
            // Arrange: along the slope, a = g sin(theta) and linear drag k give
            // s(t) = (a / k) * (t - (1 - e^(-k t)) / k); the horizontal run is s cos(theta).
            // 120 steps must be one second: a wrong step length fails this test.
            Tuning tuning = Tuning.Default;
            double g = TestTerrain.ToDouble(tuning.Gravity);
            double k = TestTerrain.ToDouble(tuning.SlideDrag);
            double theta = Math.Atan(TestTerrain.Gradient);
            double a = g * Math.Sin(theta);
            const double t = 1.0;
            double expectedRun = a / k * (t - (1 - Math.Exp(-k * t)) / k) * Math.Cos(theta);
            Simulation simulation = StartAt(TestTerrain.InclineDownhillPlusZ(), 64, 4);

            // Act
            for (int step = 0; step < Simulation.StepsPerSecond; step++)
            {
                simulation.Step(NoInput);
            }

            // Assert
            double run = TestTerrain.ToDouble(simulation.State.Position.Z) - 4.0;
            Assert.That(run, Is.EqualTo(expectedRun).Within(2).Percent);
        }

        [Test]
        public void RiderLeavingHeightmap_RaisesLeftHeightmapOnceAndKeepsStepping()
        {
            // Arrange: start 8 m above the downhill edge.
            Heightmap heightmap = TestTerrain.InclineDownhillPlusZ();
            Simulation simulation = StartAt(heightmap, 64, 120);
            heightmap.Sample(Fix64.FromInt(64), Fix64.FromInt(128), out Fix64 edgeHeight, out _);
            var events = new List<SimEvent>();
            int firstOutsideTick = -1;

            // Act
            for (int step = 0; step < 5 * Simulation.StepsPerSecond; step++)
            {
                simulation.Step(NoInput);
                events.AddRange(simulation.Events);
                if (firstOutsideTick < 0 && simulation.State.OutsideHeightmap)
                {
                    firstOutsideTick = simulation.State.Tick;
                }
            }

            // Assert
            RiderState end = simulation.State;
            Assert.That(firstOutsideTick, Is.GreaterThan(0));
            Assert.That(events, Is.EqualTo(new[] { new SimEvent(firstOutsideTick, SimEventKind.LeftHeightmap) }));
            Assert.That(simulation.Events, Is.Empty, "events belong only to the step that raised them");
            Assert.That(end.Tick, Is.EqualTo(5 * Simulation.StepsPerSecond));
            Assert.That(end.OutsideHeightmap, Is.True);
            Assert.That(end.Position.Z.Raw, Is.GreaterThan(Fix64.FromInt(130).Raw), "the rider keeps going on the extruded edge");
            Assert.That(end.Position.Y, Is.EqualTo(edgeHeight));
        }

        [Test]
        public void ScriptedRide_StaysFarInsideFix64Range()
        {
            // Arrange: Q32.32 holds magnitudes up to 2^31; demand 2^11 of headroom.
            var trace = new List<StepSnapshot>();
            const long limitRaw = 1L << (20 + 32);

            // Act
            ScriptedRide.Record(TestTerrain.InclineDownhillPlusZ(), Tuning.Default, ScriptedRide.Steps, trace);

            // Assert
            long largest = 0;
            foreach (StepSnapshot snapshot in trace)
            {
                RiderState state = snapshot.State;
                foreach (Fix64 value in new[] { state.Position.X, state.Position.Y, state.Position.Z, state.Velocity.X, state.Velocity.Y, state.Velocity.Z })
                {
                    largest = Math.Max(largest, Math.Abs(value.Raw));
                }
            }

            Assert.That(largest, Is.LessThan(limitRaw));
        }

        private static Simulation StartAt(Heightmap heightmap, int x, int z) =>
            new Simulation(new RunSetup(Sport.Skis, 0, 7UL, Fix64.FromInt(x), Fix64.FromInt(z)), heightmap, Tuning.Default);
    }
}
