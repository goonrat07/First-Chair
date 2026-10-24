using System;
using System.IO;

namespace FirstChair.Riding.Tests
{
    /// <summary>Finds the committed test-slope folder (pulled from Git LFS) from the editor or a .NET test run.</summary>
    internal static class TestSlopeFiles
    {
        private const string Relative = "Assets/Terrain/TestSlope";

        /// <summary>The absolute path of Assets/Terrain/TestSlope.</summary>
        public static string Folder()
        {
            foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                for (DirectoryInfo dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                {
                    string candidate = Path.Combine(dir.FullName, Relative);
                    if (File.Exists(Path.Combine(candidate, TestSlopeData.LayoutFile)))
                    {
                        return candidate;
                    }
                }
            }

            throw new DirectoryNotFoundException("Could not find " + Relative + " above " + Directory.GetCurrentDirectory());
        }

        /// <summary>The repository root: the folder above Assets/Terrain/TestSlope's Assets.</summary>
        public static string RepoRoot() => Path.GetFullPath(Path.Combine(Folder(), "..", "..", ".."));

        /// <summary>Loads the slope the way the sim driver does.</summary>
        public static TestSlopeData Load() => TestSlopeData.Load(Folder());
    }
}
