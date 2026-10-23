using FirstChair.Riding;
using FirstChair.Sim;
using NUnit.Framework;

namespace FirstChair.Riding.Tests
{
    /// <summary>The test slope's snow look: textures per snow type and layer weights from the snow map (Story 003).</summary>
    public sealed class SnowSurfaceTests
    {
        [Test]
        public void Texture_SameInput_IsTheSameEveryTime()
        {
            // Arrange
            const int size = 32;
            var albedoA = new byte[size * size * 4];
            var normalA = new byte[size * size * 4];
            var albedoB = new byte[size * size * 4];
            var normalB = new byte[size * size * 4];

            // Act
            SnowSurface.Texture(SnowType.Powder, size, albedoA, normalA);
            SnowSurface.Texture(SnowType.Powder, size, albedoB, normalB);

            // Assert
            Assert.That(albedoB, Is.EqualTo(albedoA));
            Assert.That(normalB, Is.EqualTo(normalA));
        }

        [Test]
        public void Texture_GroomedCorduroy_VariesAcrossButBarelyAlong()
        {
            // Arrange: corduroy ridges run down the fall line (+V), so brightness changes along U, not V.
            const int size = 64;
            var albedo = new byte[size * size * 4];
            var normal = new byte[size * size * 4];

            // Act
            SnowSurface.Texture(SnowType.Groomed, size, albedo, normal);

            // Assert
            int across = 0;
            int along = 0;
            for (int i = 0; i < size - 1; i++)
            {
                across += System.Math.Abs(albedo[(10 * size + i) * 4] - albedo[(10 * size + i + 1) * 4]);
                along += System.Math.Abs(albedo[(i * size + 10) * 4] - albedo[((i + 1) * size + 10) * 4]);
            }

            Assert.That(across, Is.GreaterThan(along * 2));
        }

        [Test]
        public void Texture_NormalsPointOutOfTheSurface()
        {
            // Arrange
            const int size = 16;
            var albedo = new byte[size * size * 4];
            var normal = new byte[size * size * 4];

            // Act
            SnowSurface.Texture(SnowType.Crud, size, albedo, normal);

            // Assert: the blue (Z) channel of every normal is above 0.5, so no normal faces into the snow.
            for (int p = 0; p < size * size; p++)
            {
                Assert.That(normal[p * 4 + 2], Is.GreaterThan(128));
            }
        }

        [Test]
        public void LayerWeights_HardEdges_PickTheNearestSampleAndSumToOne()
        {
            // Arrange: a 3 x 3 snow map, ice in the middle, groomed around it.
            byte[] snow = { 0, 0, 0, 0, (byte)SnowType.Ice, 0, 0, 0, 0 };
            var weights = new float[3, 3, SnowSurface.LayerCount];

            // Act
            SnowSurface.LayerWeights(snow, 3, weights, 0);

            // Assert
            Assert.That(weights[1, 1, (int)SnowType.Ice], Is.EqualTo(1f));
            Assert.That(weights[0, 0, (int)SnowType.Groomed], Is.EqualTo(1f));
            for (int z = 0; z < 3; z++)
            {
                for (int x = 0; x < 3; x++)
                {
                    float sum = 0f;
                    for (int layer = 0; layer < SnowSurface.LayerCount; layer++)
                    {
                        sum += weights[z, x, layer];
                    }

                    Assert.That(sum, Is.EqualTo(1f).Within(1e-5f));
                }
            }
        }

        [Test]
        public void LayerWeights_Blur_SoftensTheEdge()
        {
            // Arrange
            byte[] snow = { 0, 0, 0, 0, (byte)SnowType.Ice, 0, 0, 0, 0 };
            var weights = new float[3, 3, SnowSurface.LayerCount];

            // Act
            SnowSurface.LayerWeights(snow, 3, weights, 1);

            // Assert: the corner sees the ice in one of its four neighbours.
            Assert.That(weights[0, 0, (int)SnowType.Ice], Is.EqualTo(0.25f).Within(1e-5f));
            Assert.That(weights[1, 1, (int)SnowType.Ice], Is.EqualTo(1f / 9f).Within(1e-5f));
        }
    }
}
