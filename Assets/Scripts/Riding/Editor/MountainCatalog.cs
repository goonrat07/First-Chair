using System;
using System.Collections.Generic;
using System.IO;

namespace FirstChair.Riding.Editor
{
    /// <summary>
    /// The mountains in the project (Story 015): every folder under <c>Assets/Terrain/</c> that holds a mountain's three
    /// files, <c>Name.txt</c>, <c>Name.raw</c> and <c>NameSnow.raw</c> as <c>tools/mountain/mountain.py</c> builds them,
    /// the test slope aside (it has its own scene). Each one gets a scene, rides in the build and shows in the Feel
    /// Lab's places, so adding a mountain is a data change: build its file and commit the folder.
    /// </summary>
    /// <example>
    /// <code>
    /// foreach (string name in MountainCatalog.Names())
    /// {
    ///     TestSlopeSceneBuilder.BuildMountainScene(name);   // Assets/Scenes/&lt;name&gt;.unity from Assets/Terrain/&lt;name&gt;/
    /// }
    /// </code>
    /// </example>
    public static class MountainCatalog
    {
        /// <summary>Where mountain folders live.</summary>
        public const string TerrainRoot = "Assets/Terrain";

        /// <summary>Where mountain scenes are saved.</summary>
        public const string SceneFolder = "Assets/Scenes";

        /// <summary>The mountains' names, in ordinal order: each folder under the terrain root holding its three files.</summary>
        public static IReadOnlyList<string> Names()
        {
            var names = new List<string>();
            if (!Directory.Exists(TerrainRoot))
            {
                return names;
            }

            foreach (string folder in Directory.GetDirectories(TerrainRoot))
            {
                string name = Path.GetFileName(folder);
                if (name == TestSlopeData.TestSlopeName)
                {
                    continue;
                }

                if (File.Exists(Path.Combine(folder, TestSlopeData.LayoutFileOf(name)))
                    && File.Exists(Path.Combine(folder, TestSlopeData.HeightmapFileOf(name)))
                    && File.Exists(Path.Combine(folder, TestSlopeData.SnowFileOf(name))))
                {
                    names.Add(name);
                }
            }

            names.Sort(StringComparer.Ordinal);
            return names;
        }

        /// <summary>A mountain's folder, such as <c>Assets/Terrain/PineBowl</c>.</summary>
        /// <param name="name">The mountain's name.</param>
        public static string FolderOf(string name) => TerrainRoot + "/" + name;

        /// <summary>A mountain's scene, such as <c>Assets/Scenes/PineBowl.unity</c>.</summary>
        /// <param name="name">The mountain's name.</param>
        public static string ScenePathOf(string name) => SceneFolder + "/" + name + ".unity";
    }
}
