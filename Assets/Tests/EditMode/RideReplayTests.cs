using FirstChair.Sim;
using NUnit.Framework;

namespace FirstChair.Riding.Tests
{
    /// <summary>
    /// Story 003, acceptance criterion 7, without the editor: a ride driven the way <c>SimDriver</c> drives it (uneven
    /// frames through <see cref="FixedStepClock"/>, one quantized input per frame for every step of that frame) replays
    /// step for step from its recording.
    /// </summary>
    public class RideReplayTests
    {
        private const int MaxStepsPerFrame = 12;
        private const int Frames = 500;
        private const ulong Seed = 7UL;
        private static readonly double[] FrameSeconds = { 1.0 / 60, 1.0 / 144, 1.0 / 30, 1.0 / 90, 0.004, 1.0 / 75, 0.021 };

        [Test]
        public void RecordedRide_ReplaysStepForStep()
        {
            // Arrange
            RideSession session = NewSession();

            // Act
            Drive(session, -1);
            ReplayCheckResult result = ReplayCheck.Verify(session);

            // Assert
            Assert.That(result.Passed, Is.True, result.ToString());
            Assert.That(result.StepsCompared, Is.EqualTo(session.StepsRun));
            Assert.That(session.StepsRun, Is.GreaterThan(Frames));
        }

        [Test]
        public void ReplayOfADifferentRide_FailsAtTheFirstDifferentStep()
        {
            // Arrange: the same ride, except one frame steers the other way.
            const int ChangedFrame = 200;
            RideSession live = NewSession();
            RideSession other = NewSession();
            Drive(live, -1);
            int stepsBefore = Drive(other, ChangedFrame);

            // Act
            ReplayCheckResult result = ReplayCheck.Verify(
                other.Recording, other.Slope, other.Tuning, live.StateHistory, live.EventHistory, live.Score.Total);

            // Assert
            Assert.That(result.Passed, Is.False);
            Assert.That(result.FirstMismatchStep, Is.EqualTo(stepsBefore));
        }

        [Test]
        public void OneFrame_RecordsItsInputOncePerStep()
        {
            // Arrange
            RideSession session = NewSession();
            SimInput input = SimInput.Pack(SimButtons.Forward, 40);

            // Act
            session.RunSteps(input, 3);

            // Assert
            Assert.That(session.Recording.Inputs.Count, Is.EqualTo(3));
            Assert.That(session.Recording.Inputs, Has.All.EqualTo(input));
            Assert.That(session.Previous.Tick + 1, Is.EqualTo(session.Current.Tick));
        }

        [Test]
        public void RiderAtRest_SlidesDownTheFallLineTowardPlusZ()
        {
            // Arrange: two seconds with no input from the start of the groomer.
            RideSession session = NewSession();
            RiderState start = session.Current;

            // Act
            session.RunSteps(SimInput.Pack(SimButtons.None, 0), 2 * Simulation.StepsPerSecond);

            // Assert: the mapping in SimCoordinates puts the fall line on Unity +Z, downhill in Y.
            Assert.That(SimCoordinates.ToDouble(session.Current.Position.Z), Is.GreaterThan(SimCoordinates.ToDouble(start.Position.Z)));
            Assert.That(SimCoordinates.ToDouble(session.Current.Position.Y), Is.LessThan(SimCoordinates.ToDouble(start.Position.Y)));
        }

        private static RideSession NewSession()
        {
            TestSlopeData slope = TestSlopeFiles.Load();
            return new RideSession(slope, Tuning.Default, slope.CreateSetup(Seed, 0), true);
        }

        /// <summary>Drives a session through scripted frames; returns the steps run before <paramref name="changedFrame"/>.</summary>
        private static int Drive(RideSession session, int changedFrame)
        {
            var clock = new FixedStepClock(Simulation.StepsPerSecond, MaxStepsPerFrame);
            var quantizer = new RiderInputQuantizer(RiderInputQuantizerSettings.Default);
            int stepsBefore = -1;
            for (int frame = 0; frame < Frames; frame++)
            {
                var raw = new RawRiderInput
                {
                    SteerAxis = ((frame / 40) % 2 == 0 ? 0.7f : -0.55f) * (frame == changedFrame ? -1f : 1f),
                    TuckAxis = frame % 90 < 30 ? 0.8f : 0f,
                    Jump = frame % 150 > 130,
                };
                if (frame == changedFrame)
                {
                    stepsBefore = session.StepsRun;
                }

                session.RunSteps(quantizer.Pack(raw), clock.Advance(FrameSeconds[frame % FrameSeconds.Length]));
            }

            return stepsBefore;
        }
    }
}
