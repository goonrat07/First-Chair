using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FirstChair.Riding.Editor
{
    /// <summary>
    /// After a Windows player build, copies the three test-slope files into the player's StreamingAssets, where
    /// <see cref="SimDriver"/> reads them in a build. The RAW files are not Unity assets a player can load, and copying
    /// after the build keeps a second copy out of the repo.
    /// </summary>
    public sealed class TestSlopeBuildStep : IPostprocessBuildWithReport
    {
        /// <inheritdoc />
        public int callbackOrder => 0;

        /// <inheritdoc />
        public void OnPostprocessBuild(BuildReport report)
        {
            BuildTarget target = report.summary.platform;
            if (target != BuildTarget.StandaloneWindows64 && target != BuildTarget.StandaloneWindows)
            {
                Debug.LogWarning("TestSlopeBuildStep copies the test slope for Windows players only; " + target + " will not find it.");
                return;
            }

            string exe = report.summary.outputPath;
            string destination = Path.Combine(
                Path.GetDirectoryName(exe), Path.GetFileNameWithoutExtension(exe) + "_Data", "StreamingAssets", SimDriver.DefaultPlayerSlopeFolder);
            Directory.CreateDirectory(destination);
            foreach (string file in new[] { TestSlopeData.LayoutFile, TestSlopeData.HeightmapFile, TestSlopeData.SnowFile })
            {
                File.Copy(Path.Combine(TestSlopeTerrainBuilder.SlopeFolder, file), Path.Combine(destination, file), true);
            }
        }
    }
}
