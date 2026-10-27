using System;

namespace FirstChair.Riding
{
    /// <summary>
    /// The follow camera's math, kept out of the MonoBehaviour so it can be tested: the Steep-style chase camera of Story 011
    /// (behind the rider along the travel, at a height above the snow, tilting toward the snow ahead, closing in with speed
    /// and tuck), with the speed effects of Story 009.
    /// </summary>
    public static class FollowCameraMath
    {
        private const double DegreesPerRadian = 180.0 / Math.PI;

        /// <summary>The vertical field of view, degrees, of a lens on a sensor of the given height.</summary>
        /// <param name="focalLengthMm">Focal length, millimetres (20 for the chase camera's wide view).</param>
        /// <param name="sensorHeightMm">Sensor height, millimetres (24 for full frame).</param>
        public static double VerticalFovDegrees(double focalLengthMm, double sensorHeightMm) =>
            2.0 * Math.Atan(sensorHeightMm / (2.0 * focalLengthMm)) * DegreesPerRadian;

        /// <summary>The yaw, degrees, of a direction on the ground: 0 = +Z, 90 = +X (Unity's yaw).</summary>
        /// <param name="x">Direction X.</param>
        /// <param name="z">Direction Z.</param>
        public static double YawDegrees(double x, double z) => Math.Atan2(x, z) * DegreesPerRadian;

        /// <summary>
        /// The ground direction the camera looks along: the fall line blended with the rider's travel direction (the chase
        /// camera takes travel only). Travel counts only at or above <paramref name="minTravelSpeed"/>; with no usable fall line (flat
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

        /// <summary>
        /// How steeply the snow drops away ahead of the rider, degrees (Story 011): the steeper of the drops from the snow
        /// at the rider to the snow at two points ahead along the travel, so over a rollover the chase camera tilts down to
        /// show what is coming before the rider gets there. Negative when both points are higher (uphill, the gentler
        /// rise); a point at no distance counts as flat.
        /// </summary>
        /// <param name="heightHere">The snow's height under the rider, metres.</param>
        /// <param name="nearDistance">How far ahead the near point is, metres.</param>
        /// <param name="heightNear">The snow's height at the near point, metres.</param>
        /// <param name="farDistance">How far ahead the far point is, metres.</param>
        /// <param name="heightFar">The snow's height at the far point, metres.</param>
        /// <returns>Degrees below the horizon; compare it with the slope here in <see cref="RolloverTiltDegrees"/>.</returns>
        public static double DropAheadDegrees(double heightHere, double nearDistance, double heightNear, double farDistance, double heightFar)
        {
            double near = nearDistance > 1e-6 ? Math.Atan((heightHere - heightNear) / nearDistance) * DegreesPerRadian : double.NegativeInfinity;
            double far = farDistance > 1e-6 ? Math.Atan((heightHere - heightFar) / farDistance) * DegreesPerRadian : double.NegativeInfinity;
            double steepest = Math.Max(near, far);
            return double.IsNegativeInfinity(steepest) ? 0.0 : steepest;
        }

        /// <summary>
        /// The chase camera's slide to the side, over the rider's shoulder (Story 011): a ground offset in Unity's axes,
        /// <paramref name="metres"/> to the right of the travel, so the rider sits a little left of the screen's middle;
        /// a negative <paramref name="metres"/> slides it left.
        /// </summary>
        /// <example>Travelling down +Z, <c>SideOffset(0, 1, 0.4, out x, out z)</c> gives x 0.4, z 0: Unity's right.</example>
        /// <param name="directionX">The travel's ground direction, x (a unit vector with <paramref name="directionZ"/>).</param>
        /// <param name="directionZ">The travel's ground direction, z.</param>
        /// <param name="metres">How far to the right, metres.</param>
        /// <param name="x">The offset's x, metres.</param>
        /// <param name="z">The offset's z, metres.</param>
        public static void SideOffset(double directionX, double directionZ, double metres, out double x, out double z)
        {
            // Unity's right of a forward (x, z) on the ground is (z, -x): up cross forward, Y up and left-handed.
            x = directionZ * metres;
            z = -directionX * metres;
        }

        /// <summary>
        /// A chase camera number that changes with speed and tuck (Story 011), such as its distance or its pitch over the
        /// snow: <paramref name="atRest"/> standing still, easing (smoothstep) to <paramref name="atSpeed"/> at
        /// <paramref name="fullSpeed"/>, and toward <paramref name="tucked"/> as much as the rider is tucked and fast, so a
        /// fast tuck pulls the camera in and down and a tuck at rest changes nothing.
        /// </summary>
        /// <param name="atRest">The value standing still.</param>
        /// <param name="atSpeed">The value at full speed, not tucked.</param>
        /// <param name="tucked">The value at full speed, fully tucked.</param>
        /// <param name="speed">Rider speed, metres per second.</param>
        /// <param name="fullSpeed">The speed where the speed and tuck values apply in full, metres per second; at or below
        /// 0 they always do.</param>
        /// <param name="tuck">How tucked the rider is, 0 to 1 (smoothed by the caller).</param>
        public static double ChaseValue(double atRest, double atSpeed, double tucked, double speed, double fullSpeed, double tuck)
        {
            double share = fullSpeed > 0.0 ? Math.Max(0.0, Math.Min(1.0, speed / fullSpeed)) : 1.0;
            share = share * share * (3.0 - 2.0 * share);
            double open = atRest + (atSpeed - atRest) * share;
            return open + (tucked - open) * Math.Max(0.0, Math.Min(1.0, tuck)) * share;
        }

        /// <summary>
        /// How far to raise the chase camera so its line to the aim clears the snow at one point (Story 011). The point sits
        /// <paramref name="fraction"/> of the way from the aim (0) to the camera (1) along the ground, and the line must
        /// pass at least <paramref name="margin"/> above the snow there; raising the camera lifts the line in proportion to
        /// the fraction. Take the largest over the points sampled, with the camera's own point at fraction 1.
        /// </summary>
        /// <param name="aimHeight">The aim point's height, metres.</param>
        /// <param name="cameraHeight">The camera's height before any rise, metres.</param>
        /// <param name="fraction">Where the point is, from the aim (0) to the camera (1); at the aim nothing can clear it.</param>
        /// <param name="snowHeight">The snow's height at the point, metres.</param>
        /// <param name="margin">The clearance wanted there, metres.</param>
        /// <returns>The rise, metres; 0 when the line already clears.</returns>
        public static double RiseToClear(double aimHeight, double cameraHeight, double fraction, double snowHeight, double margin)
        {
            if (!(fraction > 1e-6))
            {
                return 0.0;
            }

            double shortfall = snowHeight + margin - (aimHeight + fraction * (cameraHeight - aimHeight));
            return shortfall > 0.0 ? shortfall / fraction : 0.0;
        }

        /// <summary>
        /// The extra downward tilt over a rollover, degrees (Story 011): a share of how much steeper the snow ahead drops
        /// than the slope under the rider, at most <paramref name="most"/>. On a steady slope the chase camera's place above
        /// the snow already frames the slope, so only a steeper drop ahead (a rollover, the lip of a steep face) tilts it.
        /// </summary>
        /// <param name="dropAheadDegrees">From <see cref="DropAheadDegrees"/>.</param>
        /// <param name="slopeHereDegrees">The slope's fall under the rider along the travel (<see cref="SlopeDownAlongDegrees"/>).</param>
        /// <param name="factor">The share of the difference, 0 to 1.</param>
        /// <param name="most">The most tilt, degrees.</param>
        public static double RolloverTiltDegrees(double dropAheadDegrees, double slopeHereDegrees, double factor, double most) =>
            Math.Max(0.0, Math.Min(Math.Max(0.0, most), Math.Max(0.0, factor) * (dropAheadDegrees - slopeHereDegrees)));

        /// <summary>
        /// How strongly the screen's edges blur at a speed, as URP's motion blur intensity, 0 to 1 (Story 011): nothing
        /// below <paramref name="fromSpeed"/>, rising on the shake's squared ramp to <paramref name="most"/> at
        /// <paramref name="fullSpeed"/>, at the reference frame rate. URP blurs across a share of each frame's motion, so
        /// the share scales by the reference frame over this frame's ride time: the streaks are as long at any frame
        /// rate, as far as URP's most of 1 allows. A frame that moved the view nowhere needs none.
        /// </summary>
        /// <example><c>MotionBlurIntensity(30, 15, 30, 0.5, 1.0 / 120, 1.0 / 60)</c> is 1: twice the share at 120 Hz.</example>
        /// <param name="speed">Rider speed, m/s.</param>
        /// <param name="fromSpeed">Speed where the blur starts, m/s.</param>
        /// <param name="fullSpeed">Speed of the full blur, m/s.</param>
        /// <param name="most">The strongest blur at the reference frame rate; 0 or less turns it off.</param>
        /// <param name="frameSeconds">The ride time the view moved through this frame, seconds.</param>
        /// <param name="referenceFrameSeconds">The frame the strengths are set for, seconds.</param>
        public static double MotionBlurIntensity(
            double speed, double fromSpeed, double fullSpeed, double most, double frameSeconds, double referenceFrameSeconds)
        {
            if (!(frameSeconds > 0.0) || !(referenceFrameSeconds > 0.0))
            {
                return 0.0;
            }

            return Math.Min(1.0, Math.Max(0.0, most) * ShakeAmount(speed, fromSpeed, fullSpeed) * referenceFrameSeconds / frameSeconds);
        }

        /// <summary>How far ahead of the rider to aim, metres: grows with speed up to a cap.</summary>
        /// <param name="baseMetres">Look-ahead when stopped.</param>
        /// <param name="perSpeedSeconds">Extra metres per metre per second.</param>
        /// <param name="maxMetres">Cap.</param>
        /// <param name="speed">Rider speed, metres per second.</param>
        public static double LookAhead(double baseMetres, double perSpeedSeconds, double maxMetres, double speed) =>
            Math.Min(maxMetres, baseMetres + perSpeedSeconds * Math.Max(0.0, speed));

        /// <summary>
        /// The vertical field of view at a speed, degrees (Story 009): the lens's own plus <paramref name="perSpeed"/> per m/s,
        /// at most <paramref name="maxExtra"/> more. A wider view makes speed read faster.
        /// </summary>
        /// <param name="baseDegrees">The lens's own field of view, degrees.</param>
        /// <param name="perSpeed">Degrees added per metre per second.</param>
        /// <param name="maxExtra">The most degrees added.</param>
        /// <param name="speed">Rider speed, metres per second.</param>
        public static double FovAtSpeed(double baseDegrees, double perSpeed, double maxExtra, double speed) =>
            baseDegrees + Math.Min(Math.Max(0.0, maxExtra), Math.Max(0.0, perSpeed) * Math.Max(0.0, speed));

        /// <summary>
        /// The camera's roll into a turn, degrees (Story 009, after the Bluebird prototype): <paramref name="perG"/> per g of
        /// sideways acceleration, at most <paramref name="max"/> either way. Positive sideways acceleration turns right, and a
        /// positive roll in Unity tips the view left, so a right turn gives a negative roll: the camera banks into the turn.
        /// </summary>
        /// <param name="sidewaysAcceleration">Speed times the path's turn rate, m/s^2; positive turning right.</param>
        /// <param name="gravity">Gravity, m/s^2: one g.</param>
        /// <param name="perG">Degrees per g.</param>
        /// <param name="max">The most degrees either way; 0 turns the roll off.</param>
        public static double RollDegrees(double sidewaysAcceleration, double gravity, double perG, double max)
        {
            double cap = Math.Max(0.0, max);
            double roll = gravity > 0.0 ? -perG * sidewaysAcceleration / gravity : 0.0;
            return Math.Max(-cap, Math.Min(cap, roll));
        }

        /// <summary>
        /// How much the camera shakes at a speed, 0 to 1 (Story 009): nothing below <paramref name="fromSpeed"/>, rising to 1
        /// at <paramref name="fullSpeed"/>, squared so it creeps in.
        /// </summary>
        /// <param name="speed">Rider speed, m/s.</param>
        /// <param name="fromSpeed">Speed where the shake starts, m/s.</param>
        /// <param name="fullSpeed">Speed of the full shake, m/s; at or below fromSpeed the shake switches on fully there.</param>
        public static double ShakeAmount(double speed, double fromSpeed, double fullSpeed)
        {
            if (!(speed > fromSpeed))
            {
                return 0.0;
            }

            double share = fullSpeed > fromSpeed ? Math.Min(1.0, (speed - fromSpeed) / (fullSpeed - fromSpeed)) : 1.0;
            return share * share;
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
