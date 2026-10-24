using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FirstChair.Riding.Tests
{
    /// <summary>
    /// Story 007: the conifer the slope draws hundreds of times imports with GPU instancing on, and its billboard atlas
    /// (the far LOD) imports so alpha clipping survives the mipmaps.
    /// </summary>
    public class EnvironmentImportTests
    {
        private const string ConiferPath = "Assets/Art/Environment/EN_Conifer/EN_Conifer.fbx";
        private const string BillboardPath = "Assets/Art/Environment/EN_Conifer/EN_Conifer_Billboard.png";

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

        [Test]
        public void ConiferBillboardAtlas_ImportsWithAlphaAndCoveragePreservingMipmaps()
        {
            // Arrange
            var importer = AssetImporter.GetAtPath(BillboardPath) as TextureImporter;

            // Assert
            Assert.That(importer, Is.Not.Null, BillboardPath + " is not imported as a texture");
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.mipmapEnabled, Is.True);
            Assert.That(importer.mipMapsPreserveCoverage, Is.True);
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
        }
    }
}
