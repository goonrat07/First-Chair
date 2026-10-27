using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FirstChair.Riding.Editor
{
    /// <summary>
    /// After a Windows or Linux player build, copies the three test-slope files, and every mountain's (Story 010's test
    /// mountain, and since Story 015 each one <see cref="MountainCatalog"/> finds), into the player's StreamingAssets,
    /// where <see cref="SimDriver"/> reads them in a build. The RAW files are not Unity assets a player can load, and
    /// copying after the build keeps a second copy out of the repo.
    /// </summary>
    public sealed class TestSlopeBuildStep : IPostprocessBuildWithReport
    {
        /// <inheritdoc />
        public int callbackOrder => 0;

        /// <inheritdoc />
        public void OnPostprocessBuild(BuildReport report)
        {
            BuildTarget target = report.summary.platform;
            // Windows and Linux players keep their data in <name>_Data next to the executable; macOS does not.
            if (target != BuildTarget.StandaloneWindows64 && target != BuildTarget.StandaloneWindows && target != BuildTarget.StandaloneLinux64)
            {
                Debug.LogWarning("TestSlopeBuildStep copies the test slope for Windows and Linux players only; " + target + " will not find it.");
                return;
            }

            string exe = report.summary.outputPath;
            string streamingAssets = Path.Combine(Path.GetDirectoryName(exe), Path.GetFileNameWithoutExtension(exe) + "_Data", "StreamingAssets");
            Copy(TestSlopeTerrainBuilder.SlopeFolder, TestSlopeData.TestSlopeName, Path.Combine(streamingAssets, SimDriver.DefaultPlayerSlopeFolder));
            foreach (string mountain in MountainCatalog.Names())
            {
                // Each mountain rides from its own folder, named as its files are.
                Copy(MountainCatalog.FolderOf(mountain), mountain, Path.Combine(streamingAssets, mountain));
            }
        }

        private static void Copy(string folder, string name, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (string file in new[] { TestSlopeData.LayoutFileOf(name), TestSlopeData.HeightmapFileOf(name), TestSlopeData.SnowFileOf(name) })
            {
                File.Copy(Path.Combine(folder, file), Path.Combine(destination, file), true);
            }
        }
    }
}
