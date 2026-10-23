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
        /// that is (-d cos(el) sin(az), d sin(el), -d cos(el) cos(az)). Pass the view's yaw as the azimuth to put the
        /// camera behind and above the aim point; azimuth 0 sits up-slope (-Z) looking down the fall line (+Z).
        /// </summary>
        public static void Offset(double azimuthDegrees, double elevationDegrees, double distance, out float x, out float y, out float z)
        {
            double az = azimuthDegrees / DegreesPerRadian;
            double el = elevationDegrees / DegreesPerRadian;
            x = (float)(-distance * Math.Cos(el) * Math.Sin(az));
            y = (float)(distance * Math.Sin(el));
            z = (float)(-distance * Math.Cos(el) * Math.Cos(az));
        }

        /// <summary>The yaw, degrees, of a direction on the ground: 0 = +Z, 90 = +X (Unity's yaw).</summary>
        /// <param name="x">Direction X.</param>
        /// <param name="z">Direction Z.</param>
        public static double YawDegrees(double x, double z) => Math.Atan2(x, z) * DegreesPerRadian;

        /// <summary>
        /// The ground direction the camera looks along, as GMA2's does: the fall line blended with the rider's travel
        /// direction. Travel counts only at or above <paramref name="minTravelSpeed"/>; with no usable fall line (flat
        /// snow) travel counts fully. Returns false, with (0, 0), when neither is usable, so the caller keeps its last
        /// direction.
        /// </summary>
        /// <param name="fallX">Downhill X: the snow normal's X (any length).</param>
        /// <param name="fallZ">Downhill Z: the snow normal's Z.</param>
        /// <param name="travelX">Ground velocity X, metres per second.</param>
        /// <param name="travelZ">Ground velocity Z, metres per second.</param>
        /// <param name="travelWeight">0 = fall line only, 1 = travel only.</param>
        /// <param name="minTravelSpeed">Speed below which travel is ignored, metres per second.</param>
        /// <param name="x">Unit direction X.</param>
        /// <param name="z">Unit direction Z.</param>
        public static bool ViewDirection(
            double fallX, double fallZ, double travelX, double travelZ, double travelWeight, double minTravelSpeed, out double x, out double z)
        {
            x = 0.0;
            z = 0.0;
            double fallLength = Math.Sqrt(fallX * fallX + fallZ * fallZ);
            double speed = Math.Sqrt(travelX * travelX + travelZ * travelZ);
            bool hasFall = fallLength > 1e-4;
            bool hasTravel = speed >= minTravelSpeed && speed > 1e-6;
            if (!hasFall && !hasTravel)
            {
                return false;
            }

            double weight = !hasTravel ? 0.0 : (!hasFall ? 1.0 : Math.Max(0.0, Math.Min(1.0, travelWeight)));
            double bx = (hasTravel ? weight * travelX / speed : 0.0) + (hasFall ? (1.0 - weight) * fallX / fallLength : 0.0);
            double bz = (hasTravel ? weight * travelZ / speed : 0.0) + (hasFall ? (1.0 - weight) * fallZ / fallLength : 0.0);
            double length = Math.Sqrt(bx * bx + bz * bz);
            if (length < 1e-6)
            {
                // Travelling straight uphill against the fall line at equal weight: keep the fall line.
                if (!hasFall)
                {
                    return false;
                }

                bx = fallX / fallLength;
                bz = fallZ / fallLength;
                length = 1.0;
            }

            x = bx / length;
            z = bz / length;
            return true;
        }

        /// <summary>
        /// How steeply the snow falls away along a ground direction, degrees: positive when the direction goes downhill.
        /// </summary>
        /// <param name="normalX">Snow normal X.</param>
        /// <param name="normalY">Snow normal Y (up); at or below 0 counts as flat.</param>
        /// <param name="normalZ">Snow normal Z.</param>
        /// <param name="directionX">Unit ground direction X.</param>
        /// <param name="directionZ">Unit ground direction Z.</param>
        public static double SlopeDownAlongDegrees(double normalX, double normalY, double normalZ, double directionX, double directionZ)
        {
            if (normalY <= 1e-6)
            {
                return 0.0;
            }

            return Math.Atan((normalX * directionX + normalZ * directionZ) / normalY) * DegreesPerRadian;
        }

        /// <summary>The camera's pitch below the horizon: the slope's fall along the view plus a margin, clamped.</summary>
        /// <param name="slopeDownDegrees">From <see cref="SlopeDownAlongDegrees"/>.</param>
        /// <param name="aboveSlopeDegrees">Margin above the snow, degrees.</param>
        /// <param name="minDegrees">Least pitch.</param>
        /// <param name="maxDegrees">Most pitch.</param>
        public static double Pitch(double slopeDownDegrees, double aboveSlopeDegrees, double minDegrees, double maxDegrees) =>
            Math.Max(minDegrees, Math.Min(maxDegrees, slopeDownDegrees + aboveSlopeDegrees));

        /// <summary>How far ahead of the rider to aim, metres: grows with speed up to a cap.</summary>
        /// <param name="baseMetres">Look-ahead when stopped.</param>
        /// <param name="perSpeedSeconds">Extra metres per metre per second.</param>
        /// <param name="maxMetres">Cap.</param>
        /// <param name="speed">Rider speed, metres per second.</param>
        public static double LookAhead(double baseMetres, double perSpeedSeconds, double maxMetres, double speed) =>
            Math.Min(maxMetres, baseMetres + perSpeedSeconds * Math.Max(0.0, speed));

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
