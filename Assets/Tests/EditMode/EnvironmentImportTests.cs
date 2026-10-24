using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FirstChair.Riding.Tests
{
    /// <summary>Story 007: the conifer the slope draws hundreds of times imports with GPU instancing on.</summary>
    public class EnvironmentImportTests
    {
        private const string ConiferPath = "Assets/Art/Environment/EN_Conifer/EN_Conifer.fbx";

        [Test]
        public void ConiferMaterials_HaveGpuInstancingOn()
        {
            // Arrange
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(ConiferPath);
            Assert.That(assets, Is.Not.Empty, ConiferPath + " did not load");
            int materials = 0;

            foreach (Object asset in assets)
            {
                if (!(asset is Material material))
                {
                    continue;
                }

                // Act
                materials++;

                // Assert
                Assert.That(material.enableInstancing, Is.True, material.name);
            }

            Assert.That(materials, Is.GreaterThan(0), "the conifer imports no materials");
        }
    }
}
