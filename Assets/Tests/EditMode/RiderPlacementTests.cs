using NUnit.Framework;
using UnityEngine;

namespace FirstChair.Riding.Tests
{
    /// <summary>
    /// Where a view puts the rider: on the snow at the sim's position, turned by the heading, and in the air pitched
    /// by the flip about the middle of the body, so a flip turns the rider over rather than swinging them about their
    /// boots.
    /// </summary>
    public class RiderPlacementTests
    {
        private const float PivotHeight = 0.9f;
        private const float Tolerance = 1e-4f;

        private static readonly Vector3 Feet = new Vector3(10f, 20f, 30f);

        [Test]
        public void NoFlip_IsTheFeetTurnedByTheHeading()
        {
            // Arrange
            RiderPose pose = Pose(yawDegrees: 90f, flipDegrees: 0f);

            // Act
            RiderPlacement.Place(pose, Vector3.up, 1f, PivotHeight, out Vector3 position, out Quaternion rotation);

            // Assert
            AssertNear(position, Feet);
            AssertNear(rotation * Vector3.forward, Vector3.right);
        }

        [Test]
        public void QuarterForwardFlip_TipsTheSkisDownAboutTheMiddleOfTheBody()
        {
            // Arrange
            RiderPose pose = Pose(yawDegrees: 0f, flipDegrees: 90f);

            // Act
            RiderPlacement.Place(pose, Vector3.up, 1f, PivotHeight, out Vector3 position, out Quaternion rotation);

            // Assert: the ski tips point down, the head points ahead, and the pivot stays where it was.
            AssertNear(rotation * Vector3.forward, Vector3.down);
            AssertNear(rotation * Vector3.up, Vector3.forward);
            AssertNear(position + rotation * (Vector3.up * PivotHeight), Feet + Vector3.up * PivotHeight);
        }

        [Test]
        public void HalfFlip_PutsTheFeetTwoPivotHeightsUp()
        {
            // Arrange
            RiderPose pose = Pose(yawDegrees: 0f, flipDegrees: 180f);

            // Act
            RiderPlacement.Place(pose, Vector3.up, 1f, PivotHeight, out Vector3 position, out Quaternion rotation);

            // Assert
            AssertNear(position, Feet + Vector3.up * (2f * PivotHeight));
            AssertNear(rotation * Vector3.up, Vector3.down);
        }

        [Test]
        public void FullFlip_IsUprightOnTheFeetAgain()
        {
            // Arrange
            RiderPose pose = Pose(yawDegrees: 45f, flipDegrees: -360f);

            // Act
            RiderPlacement.Place(pose, Vector3.up, 1f, PivotHeight, out Vector3 position, out Quaternion rotation);

            // Assert
            AssertNear(position, Feet);
            AssertNear(rotation * Vector3.up, Vector3.up);
        }

        [Test]
        public void OnASlope_TiltsTheRiderByTheSlopeAlignShareOfTheNormal()
        {
            // Arrange: snow tilted 30 degrees about +X, the rider aligned halfway.
            Vector3 normal = Quaternion.Euler(30f, 0f, 0f) * Vector3.up;
            RiderPose pose = Pose(yawDegrees: 0f, flipDegrees: 0f);

            // Act
            RiderPlacement.Place(pose, normal, 0.5f, PivotHeight, out Vector3 position, out Quaternion rotation);

            // Assert
            AssertNear(position, Feet);
            Assert.That(Vector3.Angle(rotation * Vector3.up, Vector3.up), Is.EqualTo(15f).Within(0.01f));
        }

        private static RiderPose Pose(float yawDegrees, float flipDegrees) =>
            new RiderPose(Feet.x, Feet.y, Feet.z, yawDegrees, 0f, flipDegrees != 0f, false, flipDegrees: flipDegrees);

        private static void AssertNear(Vector3 actual, Vector3 expected) =>
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(Tolerance), "expected " + expected.ToString("F4") + ", got " + actual.ToString("F4"));
    }
}
