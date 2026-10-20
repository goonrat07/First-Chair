using System;
using System.Buffers.Binary;
using System.Text;
using FirstChair.Sim;
using NUnit.Framework;

namespace FirstChair.Sim.Tests
{
    public class RecordingTests
    {
        [Test]
        public void ToBytes_HeaderAndInputs_AreAtDocumentedOffsetsLittleEndian()
        {
            // Arrange
            Heightmap heightmap = TestTerrain.InclineDownhillPlusZ();
            Tuning tuning = Tuning.Default;
            var setup = new RunSetup(Sport.Skis, 3, 0x0102030405060708UL, Fix64.FromRaw(0x1122334455667788L), Fix64.FromRaw(-2L));
            SnowMap snowMap = TestTerrain.Bands(heightmap);
            var recording = new Recording(setup, heightmap, snowMap, tuning);
            recording.Append(SimInput.FromPacked(0xAABBCCDDu));
            recording.Append(SimInput.FromPacked(7u));

            // Act
            byte[] bytes = recording.ToBytes();

            // Assert: the RunSetup and hashes come first, the input stream after them.
            Assert.Multiple(() =>
            {
                Assert.That(bytes.Length, Is.EqualTo(Recording.HeaderSize + 2 * 4));
                Assert.That(Encoding.ASCII.GetString(bytes, 0, 4), Is.EqualTo("FCRC"));
                Assert.That(BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(4)), Is.EqualTo(Recording.FormatVersion));
                Assert.That(BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(6)), Is.EqualTo(Simulation.Version));
                Assert.That(bytes[8], Is.EqualTo((byte)Sport.Skis));
                Assert.That(bytes[9], Is.EqualTo(3));
                Assert.That(BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(10)), Is.EqualTo(0x0102030405060708UL));
                Assert.That(BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(18)), Is.EqualTo(0x1122334455667788L));
                Assert.That(BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(26)), Is.EqualTo(-2L));
                Assert.That(BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(34)), Is.EqualTo(tuning.ComputeHash()));
                Assert.That(BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(42)), Is.EqualTo(heightmap.Hash));
                Assert.That(BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(50)), Is.EqualTo(snowMap.Hash));
                Assert.That(BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(58)), Is.EqualTo(Forest.Empty.Hash));
                Assert.That(BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(66)), Is.EqualTo(RailSet.Empty.Hash));
                Assert.That(BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(74)), Is.EqualTo(2u));
                Assert.That(BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(Recording.HeaderSize)), Is.EqualTo(0xAABBCCDDu));
                Assert.That(BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(Recording.HeaderSize + 4)), Is.EqualTo(7u));
            });
        }

        [Test]
        public void TryRead_BytesFromToBytes_RoundTripsHeaderAndInputs()
        {
            // Arrange
            Heightmap heightmap = TestTerrain.InclineDownhillPlusZ();
            Recording original = ScriptedRide.Record(heightmap, TestTerrain.Bands(heightmap), Tuning.Default, 300, null);

            // Act
            bool read = Recording.TryRead(original.ToBytes(), out Recording parsed);

            // Assert
            Assert.That(read, Is.True);
            Assert.That(parsed.Setup, Is.EqualTo(original.Setup));
            Assert.That(parsed.TuningHash, Is.EqualTo(original.TuningHash));
            Assert.That(parsed.HeightmapHash, Is.EqualTo(original.HeightmapHash));
            Assert.That(parsed.SnowMapHash, Is.EqualTo(original.SnowMapHash));
            Assert.That(parsed.ForestHash, Is.EqualTo(original.ForestHash));
            Assert.That(parsed.ForestHash, Is.EqualTo(ScriptedRide.Trees.Hash));
            Assert.That(parsed.Inputs, Is.EqualTo(original.Inputs));
        }

        [TestCase("null")]
        [TestCase("shorter than header")]
        [TestCase("bad magic")]
        [TestCase("unknown format version")]
        [TestCase("truncated input")]
        [TestCase("extra byte")]
        public void TryRead_CorruptOrForeignData_ReturnsFalse(string corruption)
        {
            // Arrange
            Heightmap flat = TestTerrain.Flat();
            byte[] good = ScriptedRide.Record(flat, TestTerrain.Uniform(flat, SnowType.Groomed), Tuning.Default, 10, null).ToBytes();
            byte[] data;
            switch (corruption)
            {
                case "null":
                    data = null;
                    break;
                case "shorter than header":
                    data = good.AsSpan(0, Recording.HeaderSize - 1).ToArray();
                    break;
                case "bad magic":
                    data = (byte[])good.Clone();
                    data[0] = (byte)'X';
                    break;
                case "unknown format version":
                    data = (byte[])good.Clone();
                    data[4] = (byte)(Recording.FormatVersion + 1);
                    break;
                case "truncated input":
                    data = good.AsSpan(0, good.Length - 1).ToArray();
                    break;
                default:
                    data = new byte[good.Length + 1];
                    good.CopyTo(data, 0);
                    break;
            }

            // Act
            bool read = Recording.TryRead(data, out Recording recording);

            // Assert
            Assert.That(read, Is.False);
            Assert.That(recording, Is.Null);
        }
    }
}
