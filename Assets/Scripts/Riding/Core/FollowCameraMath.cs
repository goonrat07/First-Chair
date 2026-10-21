using System;

namespace FirstChair.Riding
{
    /// <summary>The follow camera's math, kept out of the MonoBehaviour so it can be tested.</summary>
    public static class FollowCameraMath
    {
        private const double DegreesPerRadian = 180.0 / Math.PI;

        /// <summary>The vertical field of view, degrees, of a lens on a sensor of the given height.</summary>
        /// <param name="focalLengthMm">Focal length, millimetres (35 for the GMA2-like view).</param>
        /// <param name="sensorHeightMm">Sensor height, millimetres (24 for full frame).</param>
        public static double VerticalFovDegrees(double focalLengthMm, double sensorHeightMm) =>
            2.0 * Math.Atan(sensorHeightMm / (2.0 * focalLengthMm)) * DegreesPerRadian;

        /// <summary>
        /// The camera's offset from its aim point: Unity's <c>Quaternion.Euler(elevation, azimuth, 0) * (0, 0, -distance)</c>,
        /// that is (-d cos(el) sin(az), d sin(el), -d cos(el) cos(az)). Azimuth 0 puts the camera straight up-slope
        /// (-Z) of the rider, looking down the fall line (+Z); a positive azimuth swings it toward -X.
        /// </summary>
        public static void Offset(double azimuthDegrees, double elevationDegrees, double distance, out float x, out float y, out float z)
        {
            double az = azimuthDegrees / DegreesPerRadian;
            double el = elevationDegrees / DegreesPerRadian;
            x = (float)(-distance * Math.Cos(el) * Math.Sin(az));
            y = (float)(distance * Math.Sin(el));
            z = (float)(-distance * Math.Cos(el) * Math.Cos(az));
        }

        /// <summary>
        /// The frame-rate-independent blend toward a target for exponential smoothing: 1 - e^(-dt / smoothTime).
        /// A smoothing time of 0 or less snaps (returns 1).
        /// </summary>
        public static float SmoothingFactor(float smoothTimeSeconds, float deltaSeconds)
        {
            if (smoothTimeSeconds <= 0f)
            {
                return 1f;
            }

            if (deltaSeconds <= 0f)
            {
                return 0f;
            }

            return (float)(1.0 - Math.Exp(-deltaSeconds / smoothTimeSeconds));
        }
    }
}
