using FirstChair.Riding;
using NUnit.Framework;

namespace FirstChair.Riding.Tests
{
    /// <summary>
    /// The follow camera's math: view direction, look-ahead, yaw, slope and speed effects (Stories 003, 008, 009), and the
    /// Steep-style chase camera's tilt toward the snow ahead, pull-in with speed and tuck, clearance and blur (Story 011).
    /// </summary>
    public sealed class FollowCameraMathTests
    {
        // A frame at 60 Hz, seconds: the blur's reference frame.
        private const double SixtyHertzFrame = 1.0 / 60;

        [Test]
        public void ViewDirection_SlowerThanMinTravelSpeed_LooksDownTheFallLine()
        {
            // Act
            bool ok = FollowCameraMath.ViewDirection(0.0, 0.4, 1.0, 0.0, 0.6, 2.0, out double x, out double z);

            // Assert
            Assert.That(ok, Is.True);
            Assert.That(x, Is.EqualTo(0.0).Within(1e-9));
            Assert.That(z, Is.EqualTo(1.0).Within(1e-9));
        }

        [Test]
        public void ViewDirection_Traversing_BlendsTravelWithTheFallLine()
        {
            // Arrange: fall line +Z, travelling +X at 10 m/s, travel weight 0.5
            // Act
            bool ok = FollowCameraMath.ViewDirection(0.0, 0.3, 10.0, 0.0, 0.5, 2.0, out double x, out double z);

            // Assert: halfway between, 45 degrees
            Assert.That(ok, Is.True);
            Assert.That(x, Is.EqualTo(System.Math.Sqrt(0.5)).Within(1e-9));
            Assert.That(z, Is.EqualTo(System.Math.Sqrt(0.5)).Within(1e-9));
        }

        [Test]
        public void ViewDirection_FlatSnowAndStopped_ReturnsFalse()
        {
            // Act
            bool ok = FollowCameraMath.ViewDirection(0.0, 0.0, 0.5, 0.0, 0.6, 2.0, out double x, out double z);

            // Assert
            Assert.That(ok, Is.False);
            Assert.That(x, Is.EqualTo(0.0));
            Assert.That(z, Is.EqualTo(0.0));
        }

        [Test]
        public void ViewDirection_FlatSnowWhileMoving_FollowsTravel()
        {
            // Act
            bool ok = FollowCameraMath.ViewDirection(0.0, 0.0, -6.0, 0.0, 0.2, 2.0, out double x, out double z);

            // Assert
            Assert.That(ok, Is.True);
            Assert.That(x, Is.EqualTo(-1.0).Within(1e-9));
            Assert.That(z, Is.EqualTo(0.0).Within(1e-9));
        }

        [Test]
        public void ViewDirection_StraightUphillAtEqualWeight_KeepsTheFallLine()
        {
            // Act
            bool ok = FollowCameraMath.ViewDirection(0.0, 1.0, 0.0, -5.0, 0.5, 2.0, out double x, out double z);

            // Assert
            Assert.That(ok, Is.True);
            Assert.That(z, Is.EqualTo(1.0).Within(1e-9));
        }

        [Test]
        public void LookAhead_GrowsWithSpeed_UpToTheCap()
        {
            // Assert
            Assert.That(FollowCameraMath.LookAhead(3.0, 0.2, 10.0, 0.0), Is.EqualTo(3.0).Within(1e-9));
            Assert.That(FollowCameraMath.LookAhead(3.0, 0.2, 10.0, 15.0), Is.EqualTo(6.0).Within(1e-9));
            Assert.That(FollowCameraMath.LookAhead(3.0, 0.2, 10.0, 100.0), Is.EqualTo(10.0).Within(1e-9));
            Assert.That(FollowCameraMath.LookAhead(3.0, 0.2, 10.0, -5.0), Is.EqualTo(3.0).Within(1e-9));
        }

        [Test]
        public void YawDegrees_PlusXIs90_PlusZIs0()
        {
            // Assert
            Assert.That(FollowCameraMath.YawDegrees(1.0, 0.0), Is.EqualTo(90.0).Within(1e-9));
            Assert.That(FollowCameraMath.YawDegrees(0.0, 1.0), Is.EqualTo(0.0).Within(1e-9));
        }

        [Test]
        public void SlopeDownAlongDegrees_LookingDownA30DegreeSlope_Is30()
        {
            // Arrange: a 30 degree slope falling toward +Z has the normal (0, cos 30, sin 30).
            double c = System.Math.Cos(System.Math.PI / 6.0);
            double s = System.Math.Sin(System.Math.PI / 6.0);

            // Act
            double down = FollowCameraMath.SlopeDownAlongDegrees(0.0, c, s, 0.0, 1.0);
            double across = FollowCameraMath.SlopeDownAlongDegrees(0.0, c, s, 1.0, 0.0);
            double up = FollowCameraMath.SlopeDownAlongDegrees(0.0, c, s, 0.0, -1.0);

            // Assert
            Assert.That(down, Is.EqualTo(30.0).Within(1e-9));
            Assert.That(across, Is.EqualTo(0.0).Within(1e-9));
            Assert.That(up, Is.EqualTo(-30.0).Within(1e-9));
        }

        [Test]
        public void FovAtSpeed_WidensWithSpeed_UpToTheCap()
        {
            // Assert: 27 degrees, 0.12 more per m/s, at most 5 more (Story 009: speed reads faster through a wider view).
            Assert.That(FollowCameraMath.FovAtSpeed(27.0, 0.12, 5.0, 0.0), Is.EqualTo(27.0).Within(1e-12));
            Assert.That(FollowCameraMath.FovAtSpeed(27.0, 0.12, 5.0, 25.0), Is.EqualTo(30.0).Within(1e-12));
            Assert.That(FollowCameraMath.FovAtSpeed(27.0, 0.12, 5.0, 100.0), Is.EqualTo(32.0).Within(1e-12));
            Assert.That(FollowCameraMath.FovAtSpeed(27.0, 0.12, 5.0, -3.0), Is.EqualTo(27.0).Within(1e-12));
        }

        [Test]
        public void RollDegrees_BanksIntoTheTurn_ByItsSidewaysG_UpToTheCap()
        {
            // Assert: Unity rolls a positive z angle to the left, so a right turn (positive) banks the other way.
            Assert.That(FollowCameraMath.RollDegrees(9.81, 9.81, 2.5, 3.0), Is.EqualTo(-2.5).Within(1e-12));
            Assert.That(FollowCameraMath.RollDegrees(-4.905, 9.81, 2.5, 3.0), Is.EqualTo(1.25).Within(1e-12));
            Assert.That(FollowCameraMath.RollDegrees(30.0, 9.81, 2.5, 3.0), Is.EqualTo(-3.0).Within(1e-12));
            Assert.That(FollowCameraMath.RollDegrees(5.0, 9.81, 2.5, 0.0), Is.EqualTo(0.0));
        }

        [Test]
        public void ShakeAmount_NothingBelowItsSpeed_RisingToOneAtFullSpeed_Squared()
        {
            // Assert
            Assert.That(FollowCameraMath.ShakeAmount(15.0, 18.0, 35.0), Is.EqualTo(0.0));
            Assert.That(FollowCameraMath.ShakeAmount(26.5, 18.0, 35.0), Is.EqualTo(0.25).Within(1e-12));
            Assert.That(FollowCameraMath.ShakeAmount(50.0, 18.0, 35.0), Is.EqualTo(1.0));
            Assert.That(FollowCameraMath.ShakeAmount(50.0, 18.0, 18.0), Is.EqualTo(1.0), "a zero-width band switches on at its speed");
        }

        [Test]
        public void DropAheadDegrees_BenchThenRollover_TakesTheSteeperFarDrop()
        {
            // Arrange: the snow at the rider at 100 m; 5 m ahead it has dropped 0.5 m (5.7 degrees), 15 m ahead 6 m.
            // Act
            double drop = FollowCameraMath.DropAheadDegrees(100.0, 5.0, 99.5, 15.0, 94.0);

            // Assert: the rollover the rider is about to go over, atan(6 / 15).
            Assert.That(drop, Is.EqualTo(System.Math.Atan(6.0 / 15.0) * 180.0 / System.Math.PI).Within(1e-9));
        }

        [Test]
        public void DropAheadDegrees_UphillAhead_IsNegative_TheGentlerRise()
        {
            // Act: 1 m higher 5 m ahead (11.3 degrees up), 1.5 m higher 15 m ahead (5.7 degrees up).
            double drop = FollowCameraMath.DropAheadDegrees(100.0, 5.0, 101.0, 15.0, 101.5);

            // Assert
            Assert.That(drop, Is.EqualTo(-System.Math.Atan(1.5 / 15.0) * 180.0 / System.Math.PI).Within(1e-9));
        }

        [Test]
        public void DropAheadDegrees_SamplesAtNoDistance_CountAsFlat()
        {
            // Assert
            Assert.That(FollowCameraMath.DropAheadDegrees(100.0, 0.0, 50.0, 0.0, 50.0), Is.EqualTo(0.0));
        }

        [TestCase(0.0, 0.0, 5.5, TestName = "ChaseValue_AtRest_TheRestValue")]
        [TestCase(0.0, 1.0, 5.5, TestName = "ChaseValue_TuckedAtRest_StillTheRestValue")]
        [TestCase(25.0, 0.0, 4.5, TestName = "ChaseValue_AtFullSpeed_TheSpeedValue")]
        [TestCase(40.0, 0.0, 4.5, TestName = "ChaseValue_PastFullSpeed_StaysAtTheSpeedValue")]
        [TestCase(25.0, 1.0, 3.5, TestName = "ChaseValue_TuckedAtFullSpeed_TheTuckValue")]
        public void ChaseValue_RestSpeedAndTuck(double speed, double tuck, double expected)
        {
            // Act: 5.5 m at rest, 4.5 m at speed, 3.5 m tucked at speed; full speed 25 m/s.
            double distance = FollowCameraMath.ChaseValue(5.5, 4.5, 3.5, speed, 25.0, tuck);

            // Assert
            Assert.That(distance, Is.EqualTo(expected).Within(1e-9));
        }

        [Test]
        public void ChaseValue_GainingSpeed_ClosesInSteadily_AndATuckClosesInFurther()
        {
            // Arrange
            double previous = double.MaxValue;

            // Act and Assert
            for (double speed = 0.0; speed <= 25.0; speed += 2.5)
            {
                double open = FollowCameraMath.ChaseValue(5.5, 4.5, 3.5, speed, 25.0, 0.0);
                double tucked = FollowCameraMath.ChaseValue(5.5, 4.5, 3.5, speed, 25.0, 1.0);
                Assert.That(open, Is.LessThanOrEqualTo(previous), "speed " + speed);
                Assert.That(tucked, Is.LessThanOrEqualTo(open), "speed " + speed);
                previous = open;
            }

            Assert.That(FollowCameraMath.ChaseValue(5.5, 4.5, 3.5, 12.5, 25.0, 0.0), Is.InRange(4.6, 5.4));
        }

        [Test]
        public void RiseToClear_LineWellAboveTheSnow_NoRise()
        {
            // Arrange: aim at 101.2 m, camera at 103 m; the snow halfway at 100 m, 0.5 m margin.
            // Act and Assert
            Assert.That(FollowCameraMath.RiseToClear(101.2, 103.0, 0.5, 100.0, 0.5), Is.EqualTo(0.0));
        }

        [Test]
        public void RiseToClear_SnowRightUnderTheCamera_RaisesItToTheClearance()
        {
            // Arrange: the camera at 103 m over snow at 102.5 m; it must clear the snow by 1 m.
            // Act and Assert: up 0.5 m.
            Assert.That(FollowCameraMath.RiseToClear(101.2, 103.0, 1.0, 102.5, 1.0), Is.EqualTo(0.5).Within(1e-9));
        }

        [Test]
        public void RiseToClear_SnowBetween_RaisesTheCameraSoTheLineClearsIt()
        {
            // Arrange: aim at 100 m, camera at 102 m; halfway along the line the snow stands at 101.2 m, 0.5 m margin.
            // The line there is at 101 m and must be at 101.7 m: the camera end goes up twice that, 1.4 m.
            // Act
            double rise = FollowCameraMath.RiseToClear(100.0, 102.0, 0.5, 101.2, 0.5);

            // Assert
            Assert.That(rise, Is.EqualTo(1.4).Within(1e-9));
            Assert.That(100.0 + 0.5 * (102.0 + rise - 100.0), Is.EqualTo(101.7).Within(1e-9));
        }

        [Test]
        public void RiseToClear_AtTheAim_NeverRises()
        {
            // Assert: a sample at the aim itself can't be cleared by moving the camera.
            Assert.That(FollowCameraMath.RiseToClear(100.0, 102.0, 0.0, 120.0, 0.5), Is.EqualTo(0.0));
        }

        [Test]
        public void RolloverTiltDegrees_SteeperAhead_TiltsDownByAShareOfTheDifference_UpToItsMost()
        {
            // Assert: 0.6 of the difference between the drop ahead and the slope here, at most 15 degrees.
            Assert.That(FollowCameraMath.RolloverTiltDegrees(30.0, 15.0, 0.6, 15.0), Is.EqualTo(9.0).Within(1e-12));
            Assert.That(FollowCameraMath.RolloverTiltDegrees(60.0, 10.0, 0.6, 15.0), Is.EqualTo(15.0).Within(1e-12));
        }

        [Test]
        public void RolloverTiltDegrees_SameOrGentlerAhead_NoTilt()
        {
            // Assert: the chase camera's placement already follows a steady slope; only a steeper drop ahead adds tilt.
            Assert.That(FollowCameraMath.RolloverTiltDegrees(15.0, 15.0, 0.6, 15.0), Is.EqualTo(0.0));
            Assert.That(FollowCameraMath.RolloverTiltDegrees(5.0, 20.0, 0.6, 15.0), Is.EqualTo(0.0));
        }

        [Test]
        public void MotionBlurIntensity_NoneBelowItsSpeed_ThenRisingToItsMost()
        {
            // Assert: from 15 m/s, full at 30 m/s, at most 0.6, on the shake's squared ramp, at the reference frame rate.
            Assert.That(FollowCameraMath.MotionBlurIntensity(10.0, 15.0, 30.0, 0.6, SixtyHertzFrame, SixtyHertzFrame), Is.EqualTo(0.0));
            Assert.That(FollowCameraMath.MotionBlurIntensity(22.5, 15.0, 30.0, 0.6, SixtyHertzFrame, SixtyHertzFrame), Is.EqualTo(0.15).Within(1e-12));
            Assert.That(FollowCameraMath.MotionBlurIntensity(30.0, 15.0, 30.0, 0.6, SixtyHertzFrame, SixtyHertzFrame), Is.EqualTo(0.6).Within(1e-12));
            Assert.That(FollowCameraMath.MotionBlurIntensity(45.0, 15.0, 30.0, 0.6, SixtyHertzFrame, SixtyHertzFrame), Is.EqualTo(0.6).Within(1e-12));
            Assert.That(FollowCameraMath.MotionBlurIntensity(45.0, 15.0, 30.0, -1.0, SixtyHertzFrame, SixtyHertzFrame), Is.EqualTo(0.0), "a negative most is off");
        }

        [Test]
        public void MotionBlurIntensity_ScalesWithTheFrame_SoTheStreaksAreAsLongAtAnyFrameRate()
        {
            // Assert: URP blurs across a share of each frame's motion, so a frame half as long needs twice the share (up to
            // URP's most, 1), a slow machine's long frame less, and a frame that moved the view nowhere none.
            Assert.That(FollowCameraMath.MotionBlurIntensity(22.5, 15.0, 30.0, 0.6, 1.0 / 120, SixtyHertzFrame), Is.EqualTo(0.3).Within(1e-12));
            Assert.That(FollowCameraMath.MotionBlurIntensity(30.0, 15.0, 30.0, 0.6, 1.0 / 120, SixtyHertzFrame), Is.EqualTo(1.0).Within(1e-12));
            Assert.That(FollowCameraMath.MotionBlurIntensity(30.0, 15.0, 30.0, 0.6, 0.1, SixtyHertzFrame), Is.EqualTo(0.1).Within(1e-12));
            Assert.That(FollowCameraMath.MotionBlurIntensity(30.0, 15.0, 30.0, 0.6, 0.0, SixtyHertzFrame), Is.EqualTo(0.0));
        }
    }
}
