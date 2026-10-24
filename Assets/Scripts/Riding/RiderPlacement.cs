using UnityEngine;

namespace FirstChair.Riding
{
    /// <summary>
    /// Where a rider view puts the rider each frame: at the sim's position, tilted toward the snow's normal, turned by the
    /// heading (which includes the spin in the air), and pitched by the flip about a pivot in the middle of the body, so
    /// a flip turns the rider over rather than swinging them about their boots. Read-only: nothing goes back to the sim.
    /// </summary>
    /// <example>
    /// <code>
    /// Vector3 normal = ViewHelpers.SnowNormal(driver.Session, driver.InterpolationAlpha);
    /// RiderPlacement.Place(driver.Pose, normal, settings.SlopeAlign, settings.FlipPivotHeight, out Vector3 position, out Quaternion rotation);
    /// transform.SetPositionAndRotation(position, rotation);
    /// </code>
    /// </example>
    public static class RiderPlacement
    {
        /// <summary>Computes the rider's root position and rotation. Allocates nothing.</summary>
        /// <param name="pose">The interpolated pose.</param>
        /// <param name="snowNormal">The snow's unit normal under the rider; straight up in the air.</param>
        /// <param name="slopeAlign">How far the rider tilts toward the normal, 0 (upright) to 1 (square to the snow).</param>
        /// <param name="flipPivotHeight">How far above the feet the flip turns about, metres.</param>
        /// <param name="position">The root's world position: the feet, moved so the pivot holds still through a flip.</param>
        /// <param name="rotation">The root's world rotation.</param>
        public static void Place(
            in RiderPose pose, Vector3 snowNormal, float slopeAlign, float flipPivotHeight, out Vector3 position, out Quaternion rotation)
        {
            Quaternion tilt = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(Vector3.up, snowNormal), slopeAlign);
            Quaternion upright = tilt * Quaternion.Euler(0f, pose.YawDegrees, 0f);
            rotation = upright * Quaternion.Euler(pose.FlipDegrees, 0f, 0f);
            var feet = new Vector3(pose.X, pose.Y, pose.Z);
            var pivot = new Vector3(0f, flipPivotHeight, 0f);
            position = feet + upright * pivot - rotation * pivot;
        }
    }
}
