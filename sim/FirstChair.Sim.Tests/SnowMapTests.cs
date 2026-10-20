using System;
using FirstChair.Sim;
using NUnit.Framework;

namespace FirstChair.Sim.Tests
{
    public class SnowMapTests
    {
        // A 3 x 3 heightmap, 4 m square: samples sit at 0, 2 and 4 m along each axis.
        private static readonly HeightmapSpec SmallSpec = new HeightmapSpec(4000, 4000, 0, 65535);

        // Row-major, row 0 at z = 0: Groomed Powder Crud / Ice Slush Groomed / Powder Crud Ice.
        private static readonly byte[] SmallTypes = { 0, 1, 2, 3, 4, 0, 1, 2, 3 };

        [TestCase(0)]
        [TestCase(8)]
        [TestCase(10)]
        [TestCase(18)]
        public void FromBytes_ByteCountNotTheHeightmapSampleCount_Throws(int byteCount)
        {
            // Arrange
            var bytes = new byte[byteCount];

            // Act and Assert
            Assert.That(() => SnowMap.FromBytes(bytes, SmallMap()), Throws.ArgumentException);
        }

        [TestCase((byte)5)]
        [TestCase((byte)255)]
        public void FromBytes_ByteThatIsNotASnowType_Throws(byte value)
        {
            // Arrange
            var bytes = new byte[9];
            bytes[4] = value;

            // Act and Assert
            Assert.That(() => SnowMap.FromBytes(bytes, SmallMap()), Throws.ArgumentException);
        }

        [TestCase(0, 0, SnowType.Groomed)]
        [TestCase(2000, 0, SnowType.Powder)]
        [TestCase(0, 2000, SnowType.Ice)]
        [TestCase(2000, 2000, SnowType.Slush)]
        [TestCase(4000, 4000, SnowType.Ice)]
        [TestCase(990, 0, SnowType.Groomed)]
        [TestCase(1000, 0, SnowType.Powder)]
        [TestCase(2900, 3100, SnowType.Crud)]
        [TestCase(-5000, 0, SnowType.Groomed)]
        [TestCase(10000, 4000, SnowType.Ice)]
        [TestCase(2000, -3000, SnowType.Powder)]
        public void TypeAt_Point_IsTheTypeOfTheNearestSampleOrEdgeSample(int xMm, int zMm, SnowType expected)
        {
            // Arrange: halves round toward +X and +Z; off the map the nearest edge sample counts.
            SnowMap snowMap = SnowMap.FromBytes(SmallTypes, SmallMap());

            // Act
            SnowType type = snowMap.TypeAt(Fix64.FromRatio(xMm, 1000), Fix64.FromRatio(zMm, 1000));

            // Assert
            Assert.That(type, Is.EqualTo(expected));
        }

        [Test]
        public void Hash_BytesOrHeightmapChanged_ChangesTheHash()
        {
            // Arrange
            byte[] changed = (byte[])SmallTypes.Clone();
            changed[8] = 4;
            Heightmap otherMap = Heightmap.FromR16(new byte[3 * 3 * 2], new HeightmapSpec(4001, 4000, 0, 65535));

            // Act
            ulong baseline = SnowMap.FromBytes(SmallTypes, SmallMap()).Hash;
            ulong same = SnowMap.FromBytes((byte[])SmallTypes.Clone(), SmallMap()).Hash;
            ulong byteChanged = SnowMap.FromBytes(changed, SmallMap()).Hash;
            ulong heightmapChanged = SnowMap.FromBytes(SmallTypes, otherMap).Hash;

            // Assert
            Assert.That(same, Is.EqualTo(baseline));
            Assert.That(byteChanged, Is.Not.EqualTo(baseline));
            Assert.That(heightmapChanged, Is.Not.EqualTo(baseline));
        }

        [Test]
        public void FromBytes_CallerChangesItsArrayAfterwards_MapIsUnchanged()
        {
            // Arrange
            byte[] bytes = (byte[])SmallTypes.Clone();
            SnowMap snowMap = SnowMap.FromBytes(bytes, SmallMap());
            ulong hash = snowMap.Hash;

            // Act
            Array.Clear(bytes, 0, bytes.Length);

            // Assert
            Assert.That(snowMap.TypeAt(Fix64.FromInt(2), Fix64.FromInt(2)), Is.EqualTo(SnowType.Slush));
            Assert.That(snowMap.Hash, Is.EqualTo(hash));
        }

        private static Heightmap SmallMap() => Heightmap.FromR16(new byte[3 * 3 * 2], SmallSpec);
    }
}
