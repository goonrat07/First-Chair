using UnityEditor;

namespace FirstChair.Riding.Editor
{
    /// <summary>
    /// Import settings for character models under <c>Assets/Art/Characters/</c> (CH_Skier): a Generic rig, the clips
    /// imported, and the bone transforms kept (not optimized away), so <see cref="SkierView"/> can find the
    /// <c>SOCKET_</c> nodes and play the pose clips. Props and trees keep Unity's defaults.
    /// </summary>
    public sealed class CharacterModelImport : AssetPostprocessor
    {
        /// <summary>The folder these settings apply to.</summary>
        public const string CharacterFolder = "Assets/Art/Characters/";

        /// <inheritdoc />
        public override uint GetVersion() => 1;

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(CharacterFolder, System.StringComparison.Ordinal))
            {
                return;
            }

            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.optimizeGameObjects = false;
        }
    }
}
