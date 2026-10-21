using System.IO;
using FirstChair.Sim;
using UnityEditor;
using UnityEngine;

namespace FirstChair.Riding.Editor
{
    /// <summary>
    /// Builds Unity's TerrainData from the same <c>TestSlope.raw</c> the sim reads: resolution from the file
    /// (1025), size and height scale from the layout's spec (512 x 160 x 512 m), heights in file order with rows on Z
    /// (see <see cref="SimCoordinates"/>). Menu: First Chair > Rebuild Test Slope Terrain.
    /// </summary>
    public static class TestSlopeTerrainBuilder
    {
        /// <summary>The slope folder, relative to the project root.</summary>
        public const string SlopeFolder = "Assets/Terrain/TestSlope";

        /// <summary>Where the TerrainData asset is written.</summary>
        public const string TerrainAssetPath = SlopeFolder + "/TestSlopeTerrain.asset";

        /// <summary>Rebuilds the TerrainData asset from the RAW file, creating it if needed, and returns it.</summary>
        public static TerrainData RebuildAsset()
        {
            SlopeLayout layout = SlopeLayout.Parse(File.ReadAllText(Path.Combine(SlopeFolder, TestSlopeData.LayoutFile)));
            byte[] r16 = File.ReadAllBytes(Path.Combine(SlopeFolder, TestSlopeData.HeightmapFile));
            TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainAssetPath);
            bool created = data == null;
            if (created)
            {
                data = new TerrainData();
            }

            Fill(data, r16, layout.Spec);
            if (created)
            {
                AssetDatabase.CreateAsset(data, TerrainAssetPath);
            }
            else
            {
                EditorUtility.SetDirty(data);
            }

            AssetDatabase.SaveAssets();
            return data;
        }

        /// <summary>A new, unsaved TerrainData built from R16 bytes (used by the parity test).</summary>
        /// <param name="r16">The heightmap file's bytes.</param>
        /// <param name="spec">The heightmap's spec from the layout.</param>
        public static TerrainData Build(byte[] r16, HeightmapSpec spec)
        {
            var data = new TerrainData();
            Fill(data, r16, spec);
            return data;
        }

        /// <summary>Fills a TerrainData from R16 bytes and a spec.</summary>
        public static void Fill(TerrainData data, byte[] r16, HeightmapSpec spec)
        {
            float[,] heights = TerrainHeights.DecodeR16(r16, out int resolution);

            // Resolution first: setting heightmapResolution afterwards would rescale size.
            data.heightmapResolution = resolution;
            data.size = SimCoordinatesUnity.TerrainSize(spec);
            data.SetHeights(0, 0, heights);
        }

        [MenuItem("First Chair/Rebuild Test Slope Terrain")]
        private static void RebuildMenu()
        {
            TerrainData data = RebuildAsset();
            Debug.Log("Rebuilt " + TerrainAssetPath + ": " + data.heightmapResolution + " samples a side, size " + data.size);
        }
    }
}
