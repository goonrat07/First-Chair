using System;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace FirstChair.Riding.Editor
{
    /// <summary>
    /// Import settings for environment models under <c>Assets/Art/Environment/</c> (EN_Conifer): GPU instancing on every
    /// material the model brings in (story 007), since the slope draws hundreds of the same tree. With URP's SRP Batcher on
    /// (<c>FirstChair_URP</c>), Unity draws SRP-Batcher-compatible materials through the SRP Batcher, which already makes
    /// each repeated tree cheap; instancing applies wherever the SRP Batcher doesn't. Billboard atlases
    /// (<c>*_Billboard.png</c>, the trees' far LOD) import with alpha as transparency, clamped, and with mipmaps that keep
    /// their alpha-test coverage, so a far tree doesn't thin away as it shrinks.
    /// </summary>
    public sealed class EnvironmentModelImport : AssetPostprocessor
    {
        /// <summary>The folder these settings apply to.</summary>
        public const string EnvironmentFolder = "Assets/Art/Environment/";

        /// <summary>The suffix of a billboard atlas.</summary>
        public const string BillboardSuffix = "_Billboard.png";

        /// <inheritdoc />
        public override uint GetVersion() => 2;

        /// <summary>Runs after URP's own material-description step, which picks the URP shader.</summary>
        public override int GetPostprocessOrder() => 100;

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(EnvironmentFolder, StringComparison.Ordinal)
                || !assetPath.EndsWith(BillboardSuffix, StringComparison.Ordinal))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.mipMapsPreserveCoverage = true;
            importer.alphaTestReferenceValue = 0.5f;
            importer.wrapMode = TextureWrapMode.Clamp;
        }

        private void OnPreprocessMaterialDescription(MaterialDescription description, Material material, AnimationClip[] animations)
        {
            if (assetPath.StartsWith(EnvironmentFolder, StringComparison.Ordinal))
            {
                material.enableInstancing = true;
            }
        }
    }
}
