using System.IO;
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
            SkierPoseWeights.Target(0.0, 0.0, Stance.Stand, false, false, GrabKind.None, _weights);

            // Assert
            Assert.That(_weights[(int)SkierPose.Stance], Is.EqualTo(1f));
            Assert.That(Sum(), Is.EqualTo(1f).Within(1e-6f));
        }

        [Test]
        public void Target_HalfLeftEdge_BlendsStanceAndCarveLeft()
        {
            // Act
            SkierPoseWeights.Target(-0.5, 0.0, Stance.Stand, false, false, GrabKind.None, _weights);

            // Assert
            Assert.That(_weights[(int)SkierPose.CarveLeft], Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(_weights[(int)SkierPose.Stance], Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(_weights[(int)SkierPose.CarveRight], Is.EqualTo(0f));
        }

        [Test]
        public void Target_PastFullRightEdge_ClampsToCarveRight()
        {
            // Act
            SkierPoseWeights.Target(1.7, 0.0, Stance.Stand, false, false, GrabKind.None, _weights);

            // Assert
            Assert.That(_weights[(int)SkierPose.CarveRight], Is.EqualTo(1f));
            Assert.That(Sum(), Is.EqualTo(1f).Within(1e-6f));
        }

        [Test]
        public void Target_LoadingAPop_CrouchesThePose()
        {
            // Act
            SkierPoseWeights.Target(0.4, 0.75, Stance.Stand, false, false, GrabKind.None, _weights);

            // Assert
            Assert.That(_weights[(int)SkierPose.Crouch], Is.EqualTo(0.75f).Within(1e-6f));
            Assert.That(_weights[(int)SkierPose.CarveRight], Is.EqualTo(0.25f * 0.4f).Within(1e-6f));
            Assert.That(Sum(), Is.EqualTo(1f).Within(1e-6f));
        }

        [Test]
        public void Target_TuckStanceHalfRightEdge_BlendsTuckAndRaceCarveRight()
        {
            // Act: W and steering carve low (Story 008; tucked since sim version 8), coming out of the tuck as the edge
            // comes on.
            SkierPoseWeights.Target(0.5, 0.0, Stance.Tuck, false, false, GrabKind.None, _weights);

            // Assert
            Assert.That(_weights[(int)SkierPose.RaceCarveRight], Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(_weights[(int)SkierPose.Tuck], Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(_weights[(int)SkierPose.CarveRight], Is.EqualTo(0f), "the standing carve is for turns without W");
            Assert.That(Sum(), Is.EqualTo(1f).Within(1e-6f));
        }

        [Test]
        public void Target_TuckStanceFullLeftEdgeLoadingAPop_CrouchesTheRaceCarve()
        {
            // Act
            SkierPoseWeights.Target(-1.0, 0.5, Stance.Tuck, false, false, GrabKind.None, _weights);

            // Assert
            Assert.That(_weights[(int)SkierPose.RaceCarveLeft], Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(_weights[(int)SkierPose.Crouch], Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(Sum(), Is.EqualTo(1f).Within(1e-6f));
        }

        [Test]
        public void Target_TuckStanceNoEdge_IsTheTuck()
        {
            // Act
            SkierPoseWeights.Target(0.0, 0.0, Stance.Tuck, false, false, GrabKind.None, _weights);

            // Assert
            Assert.That(_weights[(int)SkierPose.Tuck], Is.EqualTo(1f));
            Assert.That(Sum(), Is.EqualTo(1f).Within(1e-6f));
        }

        [TestCase(Stance.Carve)]
        [TestCase(Stance.Drift)]
        [TestCase(Stance.Quick)]
        public void Target_PlainCarveDriftOrQuickTurnHalfRightEdge_BlendsStanceAndCarveRight(Stance stance)
        {
            // Act: steering without W, the main turn since sim version 8 (Story 009), stands up.
            SkierPoseWeights.Target(0.5, 0.0, stance, false, false, GrabKind.None, _weights);

            // Assert
            Assert.That(_weights[(int)SkierPose.CarveRight], Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(_weights[(int)SkierPose.Stance], Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(_weights[(int)SkierPose.RaceCarveRight], Is.EqualTo(0f));
            Assert.That(Sum(), Is.EqualTo(1f).Within(1e-6f));
        }

        [TestCase(Stance.Brake, SkierPose.Brake)]
        [TestCase(Stance.Pole, SkierPose.Pole)]
        public void Target_HeldStance_TakesOver(Stance stance, SkierPose expected)
        {
            // Act
            SkierPoseWeights.Target(-1.0, 0.5, stance, false, false, GrabKind.None, _weights);

            // Assert
            Assert.That(_weights[(int)expected], Is.EqualTo(1f));
            Assert.That(Sum(), Is.EqualTo(1f).Within(1e-6f));
        }

        [Test]
        public void Target_Airborne_IsAir_AndCrashBeatsAir()
        {
            // Act
            SkierPoseWeights.Target(0.3, 0.0, Stance.Tuck, true, false, GrabKind.None, _weights);
            float air = _weights[(int)SkierPose.Air];
            SkierPoseWeights.Target(0.3, 0.0, Stance.Tuck, true, true, GrabKind.None, _weights);

            // Assert
            Assert.That(air, Is.EqualTo(1f));
            Assert.That(_weights[(int)SkierPose.Crash], Is.EqualTo(1f));
            Assert.That(_weights[(int)SkierPose.Air], Is.EqualTo(0f));
        }

        [TestCase(GrabKind.Mute, SkierPose.GrabMute)]
        [TestCase(GrabKind.Tip, SkierPose.GrabTip)]
        [TestCase(GrabKind.Tail, SkierPose.GrabTail)]
        [TestCase(GrabKind.Japan, SkierPose.GrabJapan)]
        [TestCase(GrabKind.Safety, SkierPose.GrabSafety)]
        public void Target_AirborneHoldingAGrab_IsThatGrabsPose(GrabKind grab, SkierPose expected)
        {
            // Act
            SkierPoseWeights.Target(0.3, 0.0, Stance.Stand, true, false, grab, _weights);

            // Assert
            Assert.That(_weights[(int)expected], Is.EqualTo(1f));
            Assert.That(Sum(), Is.EqualTo(1f).Within(1e-6f));
        }

        [Test]
        public void Target_GrabOnTheSnowOrCrashed_IsNotAGrabPose()
        {
            // Act: the sim clears the grab on landing, but a stale one must never show outside the air.
            SkierPoseWeights.Target(0.0, 0.0, Stance.Stand, false, false, GrabKind.Japan, _weights);
            float onSnow = _weights[(int)SkierPose.GrabJapan];
            SkierPoseWeights.Target(0.0, 0.0, Stance.Stand, true, true, GrabKind.Japan, _weights);

            // Assert
            Assert.That(onSnow, Is.EqualTo(0f));
            Assert.That(_weights[(int)SkierPose.Crash], Is.EqualTo(1f));
        }

        [Test]
        public void EveryPose_HasAClipInTheSkiersAnimationContract()
        {
            // Arrange: CH_Skier's contract, written by art/source/CH_Skier/build/09_export.py from the FBX's takes.
            string contract = File.ReadAllText(Path.Combine(TestSlopeFiles.RepoRoot(), "Assets/Art/Characters/CH_Skier/animation-contract.json"));

            for (int i = 0; i < SkierPoseWeights.Count; i++)
            {
                // Act
                string name = SkierPoseWeights.ClipName((SkierPose)i);

                // Assert
                Assert.That(contract, Does.Contain("\"name\": \"" + name + "\""), "no " + name + " clip");
            }
        }

        [Test]
        public void Smooth_EasesTowardTheTarget_AndKeepsTheSumAtOne()
        {
            // Arrange
            var shown = new float[SkierPoseWeights.Count];
            shown[(int)SkierPose.Stance] = 1f;
            SkierPoseWeights.Target(0.0, 0.0, Stance.Tuck, false, false, GrabKind.None, _weights);

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
            SkierPoseWeights.Target(0.0, 0.0, Stance.Brake, false, false, GrabKind.None, _weights);

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
