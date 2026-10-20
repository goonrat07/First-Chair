using FirstChair.Sim;
using NUnit.Framework;

namespace FirstChair.Sim.Tests
{
    public class SimRandomTests
    {
        [Test]
        public void NextU64_SeedZero_MatchesSplitMix64ReferenceVectors()
        {
            // Arrange: reference outputs of SplitMix64 (Vigna) for seed 0.
            var random = new SimRandom(0UL);

            // Act
            ulong first = random.NextU64();
            ulong second = random.NextU64();
            ulong third = random.NextU64();

            // Assert
            Assert.That(first, Is.EqualTo(0xE220A8397B1DCDAFUL));
            Assert.That(second, Is.EqualTo(0x6E789E6AA1B965F4UL));
            Assert.That(third, Is.EqualTo(0x06C45D188009454FUL));
        }

        [Test]
        public void Simulation_NewRun_SeedsItsRandomGeneratorFromRunSetupSeed()
        {
            // Arrange
            Heightmap heightmap = TestTerrain.Flat();
            var setupA = new RunSetup(Sport.Skis, 0, 1234UL, Fix64.FromInt(64), Fix64.FromInt(64));
            var setupB = new RunSetup(Sport.Skis, 0, 1235UL, Fix64.FromInt(64), Fix64.FromInt(64));

            // Act
            var simulationA = new Simulation(setupA, heightmap, Tuning.Default);
            var simulationB = new Simulation(setupB, heightmap, Tuning.Default);

            // Assert
            Assert.That(simulationA.State.Rng, Is.EqualTo(new SimRandom(1234UL)));
            Assert.That(simulationB.State.Rng, Is.EqualTo(new SimRandom(1235UL)));
        }
    }
}
