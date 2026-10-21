using System;
using FirstChair.Sim;

namespace FirstChair.Riding
{
    /// <summary>
    /// The one place that turns sim coordinates (Q32.32 <see cref="Fix64"/>, metres, radians) into Unity numbers
    /// (float metres and degrees, Y up, left-handed). Every view, the terrain builder and the parity test go through it.
    /// </summary>
    /// <remarks>
    /// <para><b>The mapping is the identity on axes: Unity (x, y, z) = sim (X, Y, Z), and Unity yaw in degrees =
    /// <see cref="RiderState.Heading"/> in degrees.</b> No axis is swapped and no sign is flipped. Derived from the sim
    /// code, not assumed:</para>
    /// <list type="bullet">
    /// <item><c>Heightmap.Sample</c> reads sample <c>row * Resolution + column</c>, with the column growing with X and
    /// the row growing with Z, row 0 at Z = 0. The height is Y, and the normal is (-dh/dX, 1, -dh/dZ) normalised, so +Y
    /// is up.</item>
    /// <item><c>RiderState</c> remarks: Heading 0 faces +Z and pi/2 faces +X. <c>Simulation</c> builds the facing
    /// vector as (sin h, 0, cos h). Unity's <c>Quaternion.Euler(0, h°, 0) * Vector3.forward</c> is also
    /// (sin h, 0, cos h), so the sim's heading is Unity's yaw, clockwise seen from above.</item>
    /// <item><c>TestSlope.txt</c> and <c>tools/terrain/make_test_slope.py</c>: row 0 is Z = 0 at the top (about 145 m)
    /// and the fall line runs toward +Z, down to the 10 m run-out at row 1024.</item>
    /// </list>
    /// <para>So a Unity Terrain matches the sim when its <c>heights[row, column]</c> array is filled from the R16 file in
    /// file order (first index = row = Z, second = column = X), its size is (SizeX, MaxHeight - MinHeight, SizeZ) and
    /// it sits at (0, MinHeight, 0). Transposing rows and columns, or flipping the row order, mirrors the slope.</para>
    /// <para>Fix64 to double is exact for any value under 2^21 m, so the only rounding is the final cast to float
    /// (about 30 µm at 512 m).</para>
    /// </remarks>
    public static class SimCoordinates
    {
        private const double RawPerUnit = 1L << Fix64.FractionalBits;
        private const double DegreesPerRadian = 180.0 / Math.PI;
        private const double MillimetresPerMetre = 1000.0;

        /// <summary>A Fix64 as a double, exactly for ordinary magnitudes.</summary>
        public static double ToDouble(Fix64 value) => value.Raw / RawPerUnit;

        /// <summary>A Fix64 as a float, for drawing.</summary>
        public static float ToFloat(Fix64 value) => (float)(value.Raw / RawPerUnit);

        /// <summary>
        /// The nearest Fix64 to a length in metres. Presentation and tests only: nothing built from this may feed a
        /// ride, because a float or double path into the sim breaks determinism.
        /// </summary>
        public static Fix64 FromMetres(double metres) => Fix64.FromRaw((long)Math.Round(metres * RawPerUnit));

        /// <summary>A sim position or direction as Unity x, y, z (identity on axes; see the class remarks).</summary>
        public static void ToUnity(Vec3 value, out float x, out float y, out float z)
        {
            x = ToFloat(value.X);
            y = ToFloat(value.Y);
            z = ToFloat(value.Z);
        }

        /// <summary>A sim heading (radians, 0 = +Z, pi/2 = +X) as a Unity yaw about +Y, degrees.</summary>
        public static double HeadingToYawDegrees(double headingRadians) => headingRadians * DegreesPerRadian;

        /// <summary>A sim heading as a Unity yaw about +Y, degrees.</summary>
        public static float HeadingToYawDegrees(Fix64 heading) => (float)HeadingToYawDegrees(ToDouble(heading));

        /// <summary>
        /// A sim edge angle (radians, negative = left edges, turning left) as a Unity roll about the rider's forward
        /// (+Z) axis, degrees. A positive Unity roll about +Z tips +Y toward -X, which is a lean to the left, so the
        /// sign flips: the rider leans into the turn.
        /// </summary>
        public static double EdgeAngleToRollDegrees(double edgeRadians) => -edgeRadians * DegreesPerRadian;

        /// <summary>Interpolates between two angles in radians along the shorter arc.</summary>
        /// <param name="from">The angle at t = 0.</param>
        /// <param name="to">The angle at t = 1.</param>
        /// <param name="t">The fraction, 0 to 1.</param>
        public static double LerpAngle(double from, double to, double t)
        {
            double delta = to - from;
            while (delta > Math.PI)
            {
                delta -= 2.0 * Math.PI;
            }

            while (delta < -Math.PI)
            {
                delta += 2.0 * Math.PI;
            }

            return from + delta * t;
        }

        /// <summary>Where a Unity Terrain built from a heightmap with this spec must sit: (0, MinHeight, 0).</summary>
        public static void TerrainOrigin(HeightmapSpec spec, out float x, out float y, out float z)
        {
            x = 0f;
            y = (float)(spec.MinHeightMm / MillimetresPerMetre);
            z = 0f;
        }

        /// <summary>The Unity <c>TerrainData.size</c> for a heightmap with this spec: (SizeX, MaxHeight - MinHeight, SizeZ).</summary>
        public static void TerrainSize(HeightmapSpec spec, out float x, out float y, out float z)
        {
            x = (float)(spec.SizeXMm / MillimetresPerMetre);
            y = (float)(((long)spec.MaxHeightMm - spec.MinHeightMm) / MillimetresPerMetre);
            z = (float)(spec.SizeZMm / MillimetresPerMetre);
        }
    }
}
