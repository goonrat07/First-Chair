using System;
using FirstChair.Riding;
using NUnit.Framework;

namespace FirstChair.Riding.Tests
{
    /// <summary>The lean into a turn from speed and turn rate, and the part the carve clips do not already show (Story 013).</summary>
    public sealed class TurnLeanTests
    {
        private const double Gravity = 9.81;
        private const double MaxLean = 1.0;     // radians, about 57 degrees

        [Test]
        public void Physical_RunningStraight_IsZero()
        {
            // Act
            double lean = TurnLean.Physical(15.0, 0.0, Gravity, MaxLean);

            // Assert
            Assert.That(lean, Is.EqualTo(0.0).Within(1e-12));
        }

        [Test]
        public void Physical_BalancesGravityAgainstTheTurnsPull()
        {
            // Arrange: 10 m/s turning at 0.4905 rad/s, so speed x turn rate = g / 2
            // Act
            double lean = TurnLean.Physical(10.0, Gravity / 20.0, Gravity, MaxLean);

            // Assert: tan(lean) = 0.5
            Assert.That(System.Math.Tan(lean), Is.EqualTo(0.5).Within(1e-12));
        }

        [Test]
        public void Physical_TurningLeft_LeansLeft()
        {
            // Act
            double right = TurnLean.Physical(12.0, 0.3, Gravity, MaxLean);
            double left = TurnLean.Physical(12.0, -0.3, Gravity, MaxLean);

            // Assert
            Assert.That(right, Is.GreaterThan(0.0));
            Assert.That(left, Is.EqualTo(-right).Within(1e-12));
        }

        [Test]
        public void Physical_FasterInTheSameTurn_LeansFurther()
        {
            // Act
            double slow = TurnLean.Physical(6.0, 0.5, Gravity, MaxLean);
            double fast = TurnLean.Physical(16.0, 0.5, Gravity, MaxLean);

            // Assert
            Assert.That(fast, Is.GreaterThan(slow));
        }

        [Test]
        public void Physical_HardTurnAtSpeed_ClampsToTheMaximum()
        {
            // Act
            double lean = TurnLean.Physical(30.0, 3.0, Gravity, MaxLean);
            double other = TurnLean.Physical(30.0, -3.0, Gravity, MaxLean);

            // Assert
            Assert.That(lean, Is.EqualTo(MaxLean).Within(1e-12));
            Assert.That(other, Is.EqualTo(-MaxLean).Within(1e-12));
        }

        [Test]
        public void Physical_NoGravityOrNoMaximum_IsZero()
        {
            // Act and assert: settings that cannot give a lean give none, rather than a division by zero
            Assert.That(TurnLean.Physical(10.0, 0.5, 0.0, MaxLean), Is.EqualTo(0.0));
            Assert.That(TurnLean.Physical(10.0, 0.5, Gravity, 0.0), Is.EqualTo(0.0));
            Assert.That(TurnLean.Physical(double.NaN, 0.5, Gravity, MaxLean), Is.EqualTo(0.0));
        }

        [Test]
        public void Extra_ClipsAlreadyShowTheLean_AddsNothing()
        {
            // Act
            double extra = TurnLean.Extra(0.4, 0.4, 0.5);

            // Assert
            Assert.That(extra, Is.EqualTo(0.0).Within(1e-12));
        }

        [Test]
        public void Extra_FastCarve_AddsWhatTheClipsLack()
        {
            // Act
            double extra = TurnLean.Extra(0.7, 0.4, 0.5);

            // Assert
            Assert.That(extra, Is.EqualTo(0.3).Within(1e-12));
        }

        [Test]
        public void Extra_SlowCarve_StandsTheSkierUpAgainstTheClip()
        {
            // Act: the clip leans 0.4 but the turn needs 0.1
            double extra = TurnLean.Extra(0.1, 0.4, 0.5);

            // Assert
            Assert.That(extra, Is.EqualTo(-0.3).Within(1e-12));
        }

        [Test]
        public void Extra_FarFromTheClip_ClampsToTheMaximum()
        {
            // Act
            double extra = TurnLean.Extra(1.0, -0.2, 0.5);

            // Assert
            Assert.That(extra, Is.EqualTo(0.5).Within(1e-12));
        }

        [Test]
        public void Shown_WeighsEachCarveClipsLean_RightPositive()
        {
            // Act: 0.75 of the right carve (0.4 rad) and 0.25 of the left race carve (0.5 rad)
            double shown = TurnLean.Shown(0.0, 0.75, 0.25, 0.0, 0.4, 0.5);

            // Assert
            Assert.That(shown, Is.EqualTo(0.75 * 0.4 - 0.25 * 0.5).Within(1e-12));
        }

        [Test]
        public void SpeedFade_BelowItsStart_IsZero()
        {
            // Act: a 13 km/h pivot, where the sim's GMA-style turn spins at about 3 rad/s (story 013's second ride)
            double fade = TurnLean.SpeedFade(3.6, 6.0, 14.0);

            // Assert
            Assert.That(fade, Is.EqualTo(0.0));
        }

        [Test]
        public void SpeedFade_FromItsFullSpeed_IsOne()
        {
            // Act
            double fade = TurnLean.SpeedFade(20.0, 6.0, 14.0);

            // Assert
            Assert.That(fade, Is.EqualTo(1.0));
        }

        [Test]
        public void SpeedFade_Between_RisesSmoothlyThroughAHalfAtTheMiddle()
        {
            // Act
            double low = TurnLean.SpeedFade(8.0, 6.0, 14.0);
            double middle = TurnLean.SpeedFade(10.0, 6.0, 14.0);
            double high = TurnLean.SpeedFade(12.0, 6.0, 14.0);

            // Assert: a smoothstep, symmetric about the middle
            Assert.That(middle, Is.EqualTo(0.5).Within(1e-12));
            Assert.That(low, Is.GreaterThan(0.0).And.LessThan(middle));
            Assert.That(high, Is.EqualTo(1.0 - low).Within(1e-12));
        }

        [Test]
        public void SpeedFade_FullSpeedNotAboveTheStart_StepsAtTheStart()
        {
            // Act
            double below = TurnLean.SpeedFade(5.9, 6.0, 6.0);
            double at = TurnLean.SpeedFade(6.0, 6.0, 6.0);

            // Assert
            Assert.That(below, Is.EqualTo(0.0));
            Assert.That(at, Is.EqualTo(1.0));
        }

        [Test]
        public void FrameRoll_FrameLeaningRight_IsPositive()
        {
            // Arrange: a frame rolled 17 degrees right sees world up toward its left
            double roll = 17.0 * Math.PI / 180.0;

            // Act
            double shown = TurnLean.FrameRoll(-Math.Sin(roll), Math.Cos(roll));

            // Assert
            Assert.That(shown, Is.EqualTo(roll).Within(1e-12));
        }

        [Test]
        public void ExtraOverFrame_StraightTraverseOnAFrameTiltedToTheSnow_StandsTheSkierUp()
        {
            // Arrange: no turn, no carve clip, the frame tilted 17 degrees right by the slope (story 013's first ride)
            double frame = 17.0 * Math.PI / 180.0;

            // Act
            double extra = TurnLean.ExtraOverFrame(0.0, 0.0, frame, 0.5);

            // Assert
            Assert.That(extra, Is.EqualTo(-frame).Within(1e-12));
        }

        [Test]
        public void ExtraOverFrame_ClipsAndFrameAlreadyLeanAsFarAsTheTurnNeeds_AddsNothing()
        {
            // Arrange: the turn needs 0.68; the carve clip shows 0.38 and the frame's tilt the other 0.30
            const double physical = 0.68, clips = 0.38, frame = 0.30;

            // Act
            double extra = TurnLean.ExtraOverFrame(physical, clips, frame, 0.5);

            // Assert: counting the clips alone would add 0.30 more
            Assert.That(extra, Is.EqualTo(0.0).Within(1e-12));
        }
    }
}
