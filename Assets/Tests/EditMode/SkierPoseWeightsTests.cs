using FirstChair.Riding;
using FirstChair.Sim;
using NUnit.Framework;

namespace FirstChair.Riding.Tests
{
    /// <summary>Which skier pose shows for which sim state (Story 003).</summary>
    public sealed class SkierPoseWeightsTests
    {
        private readonly float[] _weights = new float[SkierPoseWeights.Count];

        [Test]
        public void Target_RidingFlatOnStandStance_IsAllStance()
        {
            // Act
            SkierPoseWeights.Target(0.0, 0.0, Stance.Stand, false, false, _weights);

            // Assert
            Assert.That(_weights[(int)SkierPose.Stance], Is.EqualTo(1f));
            Assert.That(Sum(), Is.EqualTo(1f).Within(1e-6f));
        }

        [Test]
        public void Target_HalfLeftEdge_BlendsStanceAndCarveLeft()
        {
            // Act
            SkierPoseWeights.Target(-0.5, 0.0, Stance.Stand, false, false, _weights);

            // Assert
            Assert.That(_weights[(int)SkierPose.CarveLeft], Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(_weights[(int)SkierPose.Stance], Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(_weights[(int)SkierPose.CarveRight], Is.EqualTo(0f));
        }

        [Test]
        public void Target_PastFullRightEdge_ClampsToCarveRight()
        {
            // Act
            SkierPoseWeights.Target(1.7, 0.0, Stance.Stand, false, false, _weights);

            // Assert
            Assert.That(_weights[(int)SkierPose.CarveRight], Is.EqualTo(1f));
            Assert.That(Sum(), Is.EqualTo(1f).Within(1e-6f));
        }

        [Test]
        public void Target_LoadingAPop_CrouchesThePose()
        {
            // Act
            SkierPoseWeights.Target(0.4, 0.75, Stance.Stand, false, false, _weights);

            // Assert
            Assert.That(_weights[(int)SkierPose.Crouch], Is.EqualTo(0.75f).Within(1e-6f));
            Assert.That(_weights[(int)SkierPose.CarveRight], Is.EqualTo(0.25f * 0.4f).Within(1e-6f));
            Assert.That(Sum(), Is.EqualTo(1f).Within(1e-6f));
        }

        [TestCase(Stance.Tuck, SkierPose.Tuck)]
        [TestCase(Stance.Brake, SkierPose.Brake)]
        [TestCase(Stance.Pole, SkierPose.Pole)]
        public void Target_HeldStance_TakesOver(Stance stance, SkierPose expected)
        {
            // Act
            SkierPoseWeights.Target(-1.0, 0.5, stance, false, false, _weights);

            // Assert
            Assert.That(_weights[(int)expected], Is.EqualTo(1f));
            Assert.That(Sum(), Is.EqualTo(1f).Within(1e-6f));
        }

        [Test]
        public void Target_Airborne_IsAir_AndCrashBeatsAir()
        {
            // Act
            SkierPoseWeights.Target(0.3, 0.0, Stance.Tuck, true, false, _weights);
            float air = _weights[(int)SkierPose.Air];
            SkierPoseWeights.Target(0.3, 0.0, Stance.Tuck, true, true, _weights);

            // Assert
            Assert.That(air, Is.EqualTo(1f));
            Assert.That(_weights[(int)SkierPose.Crash], Is.EqualTo(1f));
            Assert.That(_weights[(int)SkierPose.Air], Is.EqualTo(0f));
        }

        [Test]
        public void Smooth_EasesTowardTheTarget_AndKeepsTheSumAtOne()
        {
            // Arrange
            var shown = new float[SkierPoseWeights.Count];
            shown[(int)SkierPose.Stance] = 1f;
            SkierPoseWeights.Target(0.0, 0.0, Stance.Tuck, false, false, _weights);

            // Act: one 60 Hz frame with a 0.12 s smoothing time
            SkierPoseWeights.Smooth(shown, _weights, 1f / 60f, 0.12f);

            // Assert
            Assert.That(shown[(int)SkierPose.Tuck], Is.GreaterThan(0.1f).And.LessThan(0.2f));
            float sum = 0f;
            foreach (float w in shown)
            {
                sum += w;
            }

            Assert.That(sum, Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void Smooth_ZeroSmoothTime_Snaps()
        {
            // Arrange
            var shown = new float[SkierPoseWeights.Count];
            shown[(int)SkierPose.Stance] = 1f;
            SkierPoseWeights.Target(0.0, 0.0, Stance.Brake, false, false, _weights);

            // Act
            SkierPoseWeights.Smooth(shown, _weights, 1f / 60f, 0f);

            // Assert
            Assert.That(shown[(int)SkierPose.Brake], Is.EqualTo(1f));
        }

        private float Sum()
        {
            float sum = 0f;
            foreach (float w in _weights)
            {
                sum += w;
            }

            return sum;
        }
    }
}
