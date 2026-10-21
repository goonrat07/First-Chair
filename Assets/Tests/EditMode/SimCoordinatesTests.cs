using FirstChair.Sim;
using NUnit.Framework;

namespace FirstChair.Riding.Tests
{
    /// <summary>The sim-to-Unity mapping, checked against the sim's own code where it can be.</summary>
    public class SimCoordinatesTests
    {
        [Test]
        public void HeadingHalfPi_IsYawNinetyDegrees()
        {
            Assert.That(SimCoordinates.HeadingToYawDegrees(Fix64.HalfPi), Is.EqualTo(90f).Within(1e-4f));
        }

        [Test]
        public void LeftEdge_LeansLeft()
        {
            // A negative edge angle is the left edges; a positive Unity roll about +Z tips the body toward -X.
            Assert.That(SimCoordinates.EdgeAngleToRollDegrees(-0.3), Is.GreaterThan(0.0));
        }

        [Test]
        public void LerpAngle_TakesTheShorterArcAcrossPi()
        {
            double result = SimCoordinates.LerpAngle(3.0, -3.0, 0.5);
            Assert.That(System.Math.Abs(result), Is.GreaterThan(3.0));
        }

        [Test]
        public void TerrainSizeAndOrigin_FollowTheSpec()
        {
            // Arrange
            var spec = new HeightmapSpec(512000, 256000, 2000, 162000);

            // Act
            SimCoordinates.TerrainSize(spec, out float sx, out float sy, out float sz);
            SimCoordinates.TerrainOrigin(spec, out float ox, out float oy, out float oz);

            // Assert
            Assert.That(new[] { sx, sy, sz }, Is.EqualTo(new[] { 512f, 160f, 256f }));
            Assert.That(new[] { ox, oy, oz }, Is.EqualTo(new[] { 0f, 2f, 0f }));
        }

        [Test]
        public void DecodedRows_LieAlongZ_AsTheSimReadsThem()
        {
            // Arrange: a 3 x 3 heightmap with every sample different.
            var data = new byte[18];
            for (int i = 0; i < 9; i++)
            {
                ushort sample = (ushort)(1000 * (i + 1));
                data[2 * i] = (byte)(sample & 0xFF);
                data[2 * i + 1] = (byte)(sample >> 8);
            }

            var spec = new HeightmapSpec(2000, 2000, 0, 65535);
            Heightmap heightmap = Heightmap.FromR16(data, spec);

            // Act: Unity's heights[row = 2, column = 0] and the sim at x = 0, z = SizeZ.
            float[,] heights = TerrainHeights.DecodeR16(data, out int resolution);
            heightmap.Sample(Fix64.Zero, heightmap.SizeZ, out Fix64 simHeight, out _);
            SimCoordinates.TerrainSize(spec, out _, out float range, out _);

            // Assert
            Assert.That(resolution, Is.EqualTo(3));
            Assert.That(heights[2, 0] * range, Is.EqualTo(SimCoordinates.ToFloat(simHeight)).Within(1e-3f));
            Assert.That(heights[0, 2], Is.Not.EqualTo(heights[2, 0]));
        }

        [Test]
        public void FollowCameraOffset_SitsUpSlopeAndAbove()
        {
            // Act
            FollowCameraMath.Offset(0, 45, 18, out float x, out float y, out float z);

            // Assert
            Assert.That(x, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(y, Is.EqualTo(12.728f).Within(1e-3f));
            Assert.That(z, Is.EqualTo(-12.728f).Within(1e-3f));
        }

        [Test]
        public void ThirtyFiveMillimetreLens_IsAbout37Point8DegreesVertical()
        {
            Assert.That(FollowCameraMath.VerticalFovDegrees(35, 24), Is.EqualTo(37.85).Within(0.01));
        }
    }
}
