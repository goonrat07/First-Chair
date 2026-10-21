using FirstChair.Sim;
using UnityEngine;

namespace FirstChair.Riding
{
    /// <summary>
    /// Unity-typed wrappers over <see cref="SimCoordinates"/>, which holds (and documents) the mapping: identity on
    /// axes, sim heading = Unity yaw. Nothing here adds math of its own.
    /// </summary>
    public static class SimCoordinatesUnity
    {
        /// <summary>A sim vector as a Unity Vector3.</summary>
        public static Vector3 ToVector3(Vec3 value)
        {
            SimCoordinates.ToUnity(value, out float x, out float y, out float z);
            return new Vector3(x, y, z);
        }

        /// <summary>A pose's position as a Unity Vector3.</summary>
        public static Vector3 Position(in RiderPose pose) => new Vector3(pose.X, pose.Y, pose.Z);

        /// <summary>A sim heading as a Unity rotation about +Y.</summary>
        public static Quaternion Yaw(Fix64 heading) => Quaternion.Euler(0f, SimCoordinates.HeadingToYawDegrees(heading), 0f);

        /// <summary>Where a Terrain built from a heightmap with this spec sits.</summary>
        public static Vector3 TerrainOrigin(HeightmapSpec spec)
        {
            SimCoordinates.TerrainOrigin(spec, out float x, out float y, out float z);
            return new Vector3(x, y, z);
        }

        /// <summary>The TerrainData size for a heightmap with this spec.</summary>
        public static Vector3 TerrainSize(HeightmapSpec spec)
        {
            SimCoordinates.TerrainSize(spec, out float x, out float y, out float z);
            return new Vector3(x, y, z);
        }
    }
}
