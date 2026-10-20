using System;
using FirstChair.Sim;
using NUnit.Framework;

namespace FirstChair.Sim.Tests
{
    public class HeightmapTests
    {
        // A 3 x 3 map, 4 m square (2 m cells), whose sample values are heights in millimetres
        // (range 0 to 65535 mm, so one sample step is 1 mm). Rows run along +Z, columns along +X.
        private static readonly int[,] SmallHeightsMm =
        {
            { 1000, 2000, 4000 },
            { 3000, 6000, 5000 },
            { 0, 7000, 9000 },
        };

        private static readonly HeightmapSpec SmallSpec = new HeightmapSpec(4000, 4000, 0, 65535);

        [Test]
        public void FromR16_SampleBytes_AreReadLittleEndian()
        {
            // Arrange: only the centre sample (index 4) is set, to bytes 0x34 0x12 = 0x1234 = 4660 mm.
            var bytes = new byte[3 * 3 * 2];
            bytes[8] = 0x34;
            bytes[9] = 0x12;
            Heightmap heightmap = Heightmap.FromR16(bytes, SmallSpec);

            // Act
            heightmap.Sample(Fix64.FromInt(2), Fix64.FromInt(2), out Fix64 height, out _);

            // Assert
            Assert.That(heightmap.Resolution, Is.EqualTo(3));
            Assert.That(TestTerrain.ToDouble(height), Is.EqualTo(4.660).Within(1e-5));
        }

        [TestCase(0)]
        [TestCase(2)]
        [TestCase(8)]
        [TestCase(32)]
        [TestCase(49)]
        [TestCase(51)]
        [TestCase(164)]
        public void FromR16_ByteCountNotSquareOfPowerOfTwoPlusOne_Throws(int byteCount)
        {
            // Arrange
            var bytes = new byte[byteCount];

            // Act and Assert
            Assert.That(() => Heightmap.FromR16(bytes, SmallSpec), Throws.ArgumentException);
        }

        [TestCase(0, 1000, 0, 1000)]
        [TestCase(1000, -5, 0, 1000)]
        [TestCase(1000, 1000, 10, 5)]
        public void FromR16_InvalidSpec_Throws(int sizeXMm, int sizeZMm, int minHeightMm, int maxHeightMm)
        {
            // Arrange
            var bytes = new byte[3 * 3 * 2];
            var spec = new HeightmapSpec(sizeXMm, sizeZMm, minHeightMm, maxHeightMm);

            // Act and Assert
            Assert.That(() => Heightmap.FromR16(bytes, spec), Throws.ArgumentException);
        }

        [Test]
        public void Sample_InsideCell_IsBilinearBlendWithAnalyticNormal()
        {
            // Arrange: x = 1.5 m, z = 0.5 m is cell (0, 0) at u = 0.75, v = 0.25.
            Heightmap heightmap = SmallMap();
            double u = 0.75, v = 0.25, cell = 2.0;
            double h00 = 1.0, h10 = 2.0, h01 = 3.0, h11 = 6.0;
            double expectedHeight = (1 - v) * ((1 - u) * h00 + u * h10) + v * ((1 - u) * h01 + u * h11);
            double slopeX = ((1 - v) * (h10 - h00) + v * (h11 - h01)) / cell;
            double slopeZ = ((1 - u) * (h01 - h00) + u * (h11 - h10)) / cell;
            double length = Math.Sqrt(1 + slopeX * slopeX + slopeZ * slopeZ);

            // Act
            heightmap.Sample(Fix64.FromRatio(3, 2), Fix64.FromRatio(1, 2), out Fix64 height, out Vec3 normal);

            // Assert
            Assert.That(TestTerrain.ToDouble(height), Is.EqualTo(expectedHeight).Within(1e-6));
            Assert.That(TestTerrain.ToDouble(normal.X), Is.EqualTo(-slopeX / length).Within(1e-6));
            Assert.That(TestTerrain.ToDouble(normal.Y), Is.EqualTo(1 / length).Within(1e-6));
            Assert.That(TestTerrain.ToDouble(normal.Z), Is.EqualTo(-slopeZ / length).Within(1e-6));
        }

        [Test]
        public void Sample_OnIncline_ReturnsUnitNormalOfThePlane()
        {
            // Arrange: the plane falls 0.4 m per metre toward +Z, so its normal is (0, 1, 0.4) / sqrt(1.16).
            Heightmap heightmap = TestTerrain.InclineDownhillPlusZ();
            double length = Math.Sqrt(1 + TestTerrain.Gradient * TestTerrain.Gradient);

            // Act
            heightmap.Sample(Fix64.FromRatio(643, 10), Fix64.FromRatio(637, 10), out _, out Vec3 normal);
            double x = TestTerrain.ToDouble(normal.X), y = TestTerrain.ToDouble(normal.Y), z = TestTerrain.ToDouble(normal.Z);

            // Assert: 16-bit height quantization allows a small slope error.
            Assert.That(x, Is.EqualTo(0.0));
            Assert.That(y, Is.EqualTo(1 / length).Within(1e-3));
            Assert.That(z, Is.EqualTo(TestTerrain.Gradient / length).Within(1e-3));
            Assert.That(x * x + y * y + z * z, Is.EqualTo(1.0).Within(1e-8));
        }

        [Test]
        public void Sample_BeyondDownhillEdge_ReturnsEdgeHeightAndNoSlopeAcrossEdge()
        {
            // Arrange
            Heightmap heightmap = TestTerrain.InclineDownhillPlusZ();
            heightmap.Sample(Fix64.FromInt(64), Fix64.FromInt(128), out Fix64 edgeHeight, out _);

            // Act
            heightmap.Sample(Fix64.FromInt(64), Fix64.FromInt(140), out Fix64 height, out Vec3 normal);

            // Assert
            Assert.That(height, Is.EqualTo(edgeHeight));
            Assert.That(normal, Is.EqualTo(new Vec3(Fix64.Zero, Fix64.One, Fix64.Zero)));
        }

        [Test]
        public void Sample_BeyondSideEdge_KeepsSlopeAlongTheEdge()
        {
            // Arrange: at x = 0, z = 0.5 the only slope is along Z: (h01 - h00) / cell = 1.
            Heightmap heightmap = SmallMap();
            heightmap.Sample(Fix64.Zero, Fix64.FromRatio(1, 2), out Fix64 edgeHeight, out _);
            double expected = 1 / Math.Sqrt(2.0);

            // Act
            heightmap.Sample(Fix64.FromInt(-1), Fix64.FromRatio(1, 2), out Fix64 height, out Vec3 normal);

            // Assert
            Assert.That(height, Is.EqualTo(edgeHeight));
            Assert.That(normal.X, Is.EqualTo(Fix64.Zero));
            Assert.That(TestTerrain.ToDouble(normal.Y), Is.EqualTo(expected).Within(1e-6));
            Assert.That(TestTerrain.ToDouble(normal.Z), Is.EqualTo(-expected).Within(1e-6));
        }

        [Test]
        public void Contains_PointsOnAndOffTheMap_IncludesEdgesExcludesOutside()
        {
            // Arrange
            Heightmap heightmap = TestTerrain.Flat();
            Fix64 size = Fix64.FromInt(128);
            Fix64 tiny = Fix64.FromRaw(1L);

            // Act and Assert
            Assert.That(heightmap.Contains(Fix64.Zero, Fix64.Zero), Is.True);
            Assert.That(heightmap.Contains(size, size), Is.True);
            Assert.That(heightmap.Contains(-tiny, Fix64.FromInt(64)), Is.False);
            Assert.That(heightmap.Contains(Fix64.FromInt(64), size + tiny), Is.False);
        }

        [Test]
        public void Hash_SampleOrSpecChanged_ChangesTheHash()
        {
            // Arrange
            byte[] bytes = SmallBytes();
            byte[] changedSample = SmallBytes();
            changedSample[4] ^= 1;
            var otherSpec = new HeightmapSpec(4001, 4000, 0, 65535);

            // Act
            ulong baseline = Heightmap.FromR16(bytes, SmallSpec).Hash;
            ulong same = Heightmap.FromR16(SmallBytes(), SmallSpec).Hash;
            ulong sampleChanged = Heightmap.FromR16(changedSample, SmallSpec).Hash;
            ulong specChanged = Heightmap.FromR16(bytes, otherSpec).Hash;

            // Assert
            Assert.That(same, Is.EqualTo(baseline));
            Assert.That(sampleChanged, Is.Not.EqualTo(baseline));
            Assert.That(specChanged, Is.Not.EqualTo(baseline));
        }

        private static Heightmap SmallMap() => Heightmap.FromR16(SmallBytes(), SmallSpec);

        private static byte[] SmallBytes() =>
            TestTerrain.R16Bytes(3, 0, 65535, (row, column) => SmallHeightsMm[row, column]);
    }
}
