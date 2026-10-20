using System.IO;
using System.Text.Json;
using NUnit.Framework;

namespace FirstChair.Sim.Tests
{
    /// <summary>
    /// Checks the Unity package files that Unity, not dotnet, reads: the asmdef must keep the sim away from
    /// UnityEngine. These tests read two small files from the repo; everything else in the suite is in memory.
    /// </summary>
    public class PackageLayoutTests
    {
        private const string PackagePath = "Packages/com.firstchair.sim";

        [Test]
        public void Asmdef_SimAssembly_SetsNoEngineReferencesAndReferencesNothing()
        {
            // Arrange
            string path = Path.Combine(RepoRoot(), PackagePath, "Runtime", "FirstChair.Sim.asmdef");

            // Act
            using JsonDocument asmdef = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement root = asmdef.RootElement;

            // Assert
            Assert.That(root.GetProperty("name").GetString(), Is.EqualTo("FirstChair.Sim"));
            Assert.That(root.GetProperty("noEngineReferences").GetBoolean(), Is.True);
            Assert.That(root.GetProperty("references").GetArrayLength(), Is.EqualTo(0));
            // overrideReferences with no precompiled references: Unity adds no auto-referenced plugin DLLs,
            // so it compiles the sim against the same references as the dotnet build.
            Assert.That(root.GetProperty("overrideReferences").GetBoolean(), Is.True);
            Assert.That(root.GetProperty("precompiledReferences").GetArrayLength(), Is.EqualTo(0));
            Assert.That(root.GetProperty("allowUnsafeCode").GetBoolean(), Is.False);
        }

        [Test]
        public void PackageJson_NamesTheSimPackageForUnity6()
        {
            // Arrange
            string path = Path.Combine(RepoRoot(), PackagePath, "package.json");

            // Act
            using JsonDocument package = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement root = package.RootElement;

            // Assert
            Assert.That(root.GetProperty("name").GetString(), Is.EqualTo("com.firstchair.sim"));
            Assert.That(root.GetProperty("unity").GetString(), Is.EqualTo("6000.3"));
        }

        /// <summary>Walks up from the test binaries to the folder that holds the sim package.</summary>
        private static string RepoRoot()
        {
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, PackagePath, "package.json")))
            {
                directory = directory.Parent;
            }

            Assert.That(directory, Is.Not.Null, "No " + PackagePath + "/package.json above " + TestContext.CurrentContext.TestDirectory);
            return directory.FullName;
        }
    }
}
