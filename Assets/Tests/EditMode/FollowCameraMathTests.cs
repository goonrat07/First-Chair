using FirstChair.Riding;
using NUnit.Framework;

namespace FirstChair.Riding.Tests
{
    /// <summary>The GMA2-style follow camera's math: view direction, look-ahead, yaw and how low the rider may sit (Stories 003, 008).</summary>
    public sealed class FollowCameraMathTests
    {
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
        public void Pitch_AddsTheMarginToTheSlope_WithinTheLimits()
        {
            // Assert
            Assert.That(FollowCameraMath.Pitch(17.0, 35.0, 30.0, 72.0), Is.EqualTo(52.0).Within(1e-9));
            Assert.That(FollowCameraMath.Pitch(-20.0, 35.0, 30.0, 72.0), Is.EqualTo(30.0).Within(1e-9));
            Assert.That(FollowCameraMath.Pitch(50.0, 35.0, 30.0, 72.0), Is.EqualTo(72.0).Within(1e-9));
        }

        [Test]
        public void LateralFollow_RiderInsideTheDriftZone_CameraOnlyEasesBack()
        {
            // Assert: 1.5 m off centre inside a 2.5 m zone: no catch-up, only the recentring share.
            Assert.That(FollowCameraMath.LateralFollow(1.5, 2.5, 0.0), Is.EqualTo(0.0));
            Assert.That(FollowCameraMath.LateralFollow(1.5, 2.5, 0.1), Is.EqualTo(0.15).Within(1e-12));
            Assert.That(FollowCameraMath.LateralFollow(-1.5, 2.5, 0.1), Is.EqualTo(-0.15).Within(1e-12));
        }

        [Test]
        public void LateralFollow_RiderPastTheDriftZone_CameraCatchesUpToItsEdge()
        {
            // Assert: 4 m off centre with a 2.5 m zone: the camera moves the 1.5 m overshoot at once, then eases back.
            Assert.That(FollowCameraMath.LateralFollow(4.0, 2.5, 0.0), Is.EqualTo(1.5).Within(1e-12));
            Assert.That(FollowCameraMath.LateralFollow(-4.0, 2.5, 0.0), Is.EqualTo(-1.5).Within(1e-12));
            Assert.That(FollowCameraMath.LateralFollow(4.0, 2.5, 0.1), Is.EqualTo(1.75).Within(1e-12));
        }

        [Test]
        public void LateralFollow_NoDriftZone_FollowsFully()
        {
            // Assert: a zero zone pins the rider to the centre, as before camera drift.
            Assert.That(FollowCameraMath.LateralFollow(0.7, 0.0, 0.0), Is.EqualTo(0.7).Within(1e-12));
        }

        [Test]
        public void MaxLead_RiderThatFarBehindTheAim_SitsOnTheLowestLine()
        {
            // Arrange: the default view (47 degrees down on a 17 degree groomer, 45 m, 50 mm lens), aim 1 m above the feet.
            double fov = FollowCameraMath.VerticalFovDegrees(50.0, 24.0);

            // Act
            double lead = FollowCameraMath.MaxLead(47.0, 45.0, fov, -1.0, 0.85);

            // Assert (to 1e-6: Offset places the camera in single precision)
            Assert.That(ScreenHeight(47.0, 45.0, fov, -lead, -1.0), Is.EqualTo(0.85).Within(1e-6));
            Assert.That(ScreenHeight(30.0, 45.0, fov, -FollowCameraMath.MaxLead(30.0, 45.0, fov, -1.0, 0.7), -1.0),
                Is.EqualTo(0.7).Within(1e-6));
        }

        [Test]
        public void MaxLead_FullLookAheadAtSpeedWithNoSmoothingLag_IsPastIt()
        {
            // Arrange: at 25 m/s the look-ahead is its 13 m cap (7 m + 0.25 s * v). A camera whose smoothing barely lags (a
            // low frame rate) aims the full 13 m ahead, which put the rider's feet below the bottom of the screen (cloud
            // build check, 2026-10-24); stopped, the 7 m look-ahead put them 80 percent of the way down.
            double fov = FollowCameraMath.VerticalFovDegrees(50.0, 24.0);
            double atSpeed = FollowCameraMath.LookAhead(7.0, 0.25, 13.0, 25.0);
            double stopped = FollowCameraMath.LookAhead(7.0, 0.25, 13.0, 0.0);

            // Act
            double lead = FollowCameraMath.MaxLead(47.0, 45.0, fov, -1.0, 0.85);

            // Assert: the cap binds at speed and leaves the stopped view alone.
            Assert.That(ScreenHeight(47.0, 45.0, fov, -atSpeed, -1.0), Is.GreaterThan(1.0));
            Assert.That(lead, Is.LessThan(atSpeed));
            Assert.That(ScreenHeight(47.0, 45.0, fov, -stopped, -1.0), Is.EqualTo(0.8).Within(0.01));
            Assert.That(lead, Is.GreaterThan(stopped));
        }

        [Test]
        public void MaxLead_LowerLineOrRiderHigherThanUsual_AllowsALongerLead()
        {
            // Arrange
            double fov = FollowCameraMath.VerticalFovDegrees(50.0, 24.0);
            double usual = FollowCameraMath.MaxLead(47.0, 45.0, fov, -1.0, 0.85);

            // Act
            double lowerLine = FollowCameraMath.MaxLead(47.0, 45.0, fov, -1.0, 0.95);
            double inTheAir = FollowCameraMath.MaxLead(47.0, 45.0, fov, 2.0, 0.85);

            // Assert
            Assert.That(lowerLine, Is.GreaterThan(usual));
            Assert.That(inTheAir, Is.GreaterThan(usual));
        }

        // How far down the screen (0 top, 1 bottom) a point sits, for a camera FollowCameraMath.Offset places behind an aim
        // at the origin, looking down +Z: alongView metres along +Z from the aim and aboveAim metres above it.
        private static double ScreenHeight(double pitchDegrees, double distance, double fovDegrees, double alongView, double aboveAim)
        {
            FollowCameraMath.Offset(0.0, pitchDegrees, distance, out float cx, out float cy, out float cz);
            double length = System.Math.Sqrt(cx * cx + cy * cy + cz * cz);
            double fx = -cx / length, fy = -cy / length, fz = -cz / length;
            double ux = -fy * fx, uy = 1.0 - fy * fy, uz = -fy * fz;
            double ul = System.Math.Sqrt(ux * ux + uy * uy + uz * uz);
            double rx = -cx, ry = aboveAim - cy, rz = alongView - cz;
            double up = (rx * ux + ry * uy + rz * uz) / ul;
            double depth = rx * fx + ry * fy + rz * fz;
            return 0.5 - 0.5 * (up / depth) / System.Math.Tan(fovDegrees * System.Math.PI / 360.0);
        }
    }
}
