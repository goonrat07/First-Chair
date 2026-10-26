using System;
using System.IO;
using FirstChair.Riding.Editor;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace FirstChair.Ci.Editor
{
    /// <summary>
    /// The player build that GitHub Actions runs (<c>.github/workflows/unity.yml</c>, game-ci unity-builder's
    /// <c>buildMethod</c>). <see cref="Build"/> builds the test slope terrain and scene and a player for the active
    /// target, to the path game-ci passes in <c>-customBuildPath</c>. Any failure exits the editor with code 1, so the CI
    /// job fails. The committed ProjectSettings already carry a URP asset and the Input System backend;
    /// <see cref="SetUpProject"/> restores them if they go missing, in a Unity session of its own, because Unity reads the
    /// input setting only at startup.
    /// </summary>
    public static class CiBuild
    {
        private const string SettingsFolder = "Assets/Settings";
        private const string RendererPath = SettingsFolder + "/FirstChair_Renderer.asset";
        private const string PipelinePath = SettingsFolder + "/FirstChair_URP.asset";

        // PlayerSettings.activeInputHandler: 0 = Input Manager (old), 1 = Input System package (new), 2 = both.
        private const int InputHandlerBoth = 2;

        /// <summary>Entry point for <c>-executeMethod FirstChair.Ci.Editor.CiBuild.Build</c>.</summary>
        public static void Build()
        {
            try
            {
                EnsureUrp();
                if (EnsureInputSystemBackend())
                {
                    // The editor compiled its scripts without the Input System backend; a player compiled with it has
                    // a different class layout (SRP Core's DebugActionDesc), and Unity refuses that build.
                    AssetDatabase.SaveAssets();
                    Debug.LogError("CiBuild: the Input System backend was off when Unity started. It is on now; run "
                        + "FirstChair.Ci.Editor.CiBuild.SetUpProject in a Unity session of its own before Build.");
                    EditorApplication.Exit(1);
                    return;
                }

                AssetDatabase.SaveAssets();
                TestSlopeSceneBuilder.BuildScene();
                TestSlopeSceneBuilder.BuildMountainScene();
                if (!BuildPlayer())
                {
                    EditorApplication.Exit(1);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Assigns a URP asset to the graphics and quality settings and turns on the Input System backend, each only
        /// when the project lacks it, and saves. Safe to run again. A changed input setting takes effect when Unity
        /// next starts. Entry point for <c>-executeMethod FirstChair.Ci.Editor.CiBuild.SetUpProject</c>.
        /// </summary>
        [MenuItem("First Chair/Set Up Project (URP and Input System)")]
        public static void SetUpProject()
        {
            EnsureUrp();
            if (EnsureInputSystemBackend())
            {
                Debug.Log("CiBuild: restart Unity for the input setting to take effect.");
            }

            AssetDatabase.SaveAssets();
        }

        private static void EnsureUrp()
        {
            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset)
            {
                return;
            }

            if (!AssetDatabase.IsValidFolder(SettingsFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Settings");
            }

            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
            }

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            int current = QualitySettings.GetQualityLevel();
            for (int level = 0; level < QualitySettings.names.Length; level++)
            {
                QualitySettings.SetQualityLevel(level, false);
                QualitySettings.renderPipeline = pipeline;
            }

            QualitySettings.SetQualityLevel(current, false);
            Debug.Log("CiBuild: assigned " + PipelinePath + " to the graphics settings and every quality level.");
        }

        // Returns true when it changed the setting.
        private static bool EnsureInputSystemBackend()
        {
            Object playerSettings = Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings");
            var serialized = new SerializedObject(playerSettings);
            SerializedProperty handler = serialized.FindProperty("activeInputHandler");
            if (handler == null)
            {
                Debug.LogWarning("CiBuild: PlayerSettings has no activeInputHandler property; input handling left as it is.");
                return false;
            }

            if (handler.intValue != 0)
            {
                return false;
            }

            handler.intValue = InputHandlerBoth;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("CiBuild: active input handling set to Both, so the Input System package receives input.");
            return true;
        }

        private static bool BuildPlayer()
        {
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            string path = Argument("-customBuildPath");
            if (string.IsNullOrEmpty(path))
            {
                path = Path.Combine("Builds", target.ToString(), "FirstChair");
            }

            if (Path.GetExtension(path).Length == 0)
            {
                if (target == BuildTarget.StandaloneWindows64 || target == BuildTarget.StandaloneWindows)
                {
                    path += ".exe";
                }
                else if (target == BuildTarget.StandaloneLinux64)
                {
                    path += ".x86_64";
                }
            }

            var options = new BuildPlayerOptions
            {
                scenes = new[] { TestSlopeSceneBuilder.ScenePath, TestSlopeSceneBuilder.MountainScenePath },
                locationPathName = path,
                target = target,
                targetGroup = BuildPipeline.GetBuildTargetGroup(target),
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            Debug.Log("CiBuild: " + summary.result + " for " + target + " at " + path + ", " + summary.totalErrors + " errors, "
                + summary.totalWarnings + " warnings, " + (summary.totalSize / (1024 * 1024)) + " MB.");
            return summary.result == BuildResult.Succeeded;
        }

        private static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                {
                    return args[i + 1];
                }
            }

            return null;
        }
    }
}
