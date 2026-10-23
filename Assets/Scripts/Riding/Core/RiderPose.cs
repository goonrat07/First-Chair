using FirstChair.Sim;

namespace FirstChair.Riding
{
    /// <summary>
    /// What a view draws for the rider in one frame: the pose between the last two sim states, in Unity numbers.
    /// Built from sim state, never written back to it.
    /// </summary>
    public readonly struct RiderPose
    {
        /// <summary>Creates a pose.</summary>
        public RiderPose(
            float x, float y, float z, float yawDegrees, float rollDegrees, bool airborne, bool crashed,
            float velocityX = 0f, float velocityY = 0f, float velocityZ = 0f)
        {
            X = x;
            Y = y;
            Z = z;
            YawDegrees = yawDegrees;
            RollDegrees = rollDegrees;
            Airborne = airborne;
            Crashed = crashed;
            VelocityX = velocityX;
            VelocityY = velocityY;
            VelocityZ = velocityZ;
        }

        /// <summary>Unity x, metres.</summary>
        public float X { get; }

        /// <summary>Unity y (up), metres.</summary>
        public float Y { get; }

        /// <summary>Unity z, metres.</summary>
        public float Z { get; }

        /// <summary>Yaw about +Y, degrees, from the sim's heading.</summary>
        public float YawDegrees { get; }

        /// <summary>Lean about the rider's forward axis, degrees, from the sim's edge angle (into the turn).</summary>
        public float RollDegrees { get; }

        /// <summary>Unity velocity x, metres per second.</summary>
        public float VelocityX { get; }

        /// <summary>Unity velocity y, metres per second.</summary>
        public float VelocityY { get; }

        /// <summary>Unity velocity z, metres per second.</summary>
        public float VelocityZ { get; }

        /// <summary>Whether the current state is in the air.</summary>
        public bool Airborne { get; }

        /// <summary>Whether the current state is crashed.</summary>
        public bool Crashed { get; }

        /// <summary>
        /// Interpolates between two consecutive sim states. Position is linear, heading takes the shorter arc, and the
        /// flags come from <paramref name="current"/>.
        /// </summary>
        /// <param name="previous">The state before the last step.</param>
        /// <param name="current">The state after the last step.</param>
        /// <param name="alpha">How far between them, 0 to 1 (clamped).</param>
        public static RiderPose Interpolate(in RiderState previous, in RiderState current, double alpha)
        {
            double t = alpha < 0.0 ? 0.0 : (alpha > 1.0 ? 1.0 : alpha);
            double x = Lerp(SimCoordinates.ToDouble(previous.Position.X), SimCoordinates.ToDouble(current.Position.X), t);
            double y = Lerp(SimCoordinates.ToDouble(previous.Position.Y), SimCoordinates.ToDouble(current.Position.Y), t);
            double z = Lerp(SimCoordinates.ToDouble(previous.Position.Z), SimCoordinates.ToDouble(current.Position.Z), t);
            double heading = SimCoordinates.LerpAngle(
                SimCoordinates.ToDouble(previous.Heading), SimCoordinates.ToDouble(current.Heading), t);
            double edge = Lerp(SimCoordinates.ToDouble(previous.EdgeAngle), SimCoordinates.ToDouble(current.EdgeAngle), t);
            double vx = Lerp(SimCoordinates.ToDouble(previous.Velocity.X), SimCoordinates.ToDouble(current.Velocity.X), t);
            double vy = Lerp(SimCoordinates.ToDouble(previous.Velocity.Y), SimCoordinates.ToDouble(current.Velocity.Y), t);
            double vz = Lerp(SimCoordinates.ToDouble(previous.Velocity.Z), SimCoordinates.ToDouble(current.Velocity.Z), t);
            return new RiderPose(
                (float)x,
                (float)y,
                (float)z,
                (float)SimCoordinates.HeadingToYawDegrees(heading),
                (float)SimCoordinates.EdgeAngleToRollDegrees(edge),
                current.Airborne,
                current.Crashed,
                (float)vx,
                (float)vy,
                (float)vz);
        }

        private static double Lerp(double a, double b, double t) => a + (b - a) * t;
    }
}
