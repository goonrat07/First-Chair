using FirstChair.Riding;
using NUnit.Framework;

namespace FirstChair.Riding.Tests
{
    /// <summary>The GMA2-style follow camera's math: view direction, look-ahead and yaw (Story 003).</summary>
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
    }
}
