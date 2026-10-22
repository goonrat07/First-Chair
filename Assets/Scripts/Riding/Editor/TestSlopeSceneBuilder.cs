using System.IO;
using FirstChair.Sim;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FirstChair.Riding.Editor
{
    /// <summary>
    /// Builds <c>Assets/Scenes/TestSlope.unity</c> from the scripts, instead of a hand-written scene file: terrain,
    /// sim driver with input, placeholder rider and follow camera, wired together, saved and added to the build.
    /// Menu: First Chair > Build Test Slope Scene. Safe to run again; it overwrites the scene and keeps existing
    /// settings assets under <c>Assets/Data/Riding</c>.
    /// </summary>
    public static class TestSlopeSceneBuilder
    {
        /// <summary>The scene's path.</summary>
        public const string ScenePath = "Assets/Scenes/TestSlope.unity";

        /// <summary>Where the settings assets live.</summary>
        public const string SettingsFolder = "Assets/Data/Riding";

        private const string SkiModelPath = "Assets/Art/Equipment/PR_Ski/PR_Ski.fbx";
        private const string PoleModelPath = "Assets/Art/Equipment/PR_SkiPole/PR_SkiPole.fbx";

        /// <summary>Builds and saves the scene, after offering to save the open scenes.</summary>
        [MenuItem("First Chair/Build Test Slope Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            BuildScene();
        }

        /// <summary>Builds and saves the scene without asking anything (CI builds call this).</summary>
        public static void BuildScene()
        {
            // Assets are loaded after NewScene, which unloads unused assets. Settings created before it were saved as null
            // references in the CI build of 2026-10-22, so the camera never moved; Set now throws instead.
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            TerrainData terrainData = TestSlopeTerrainBuilder.RebuildAsset();
            TestSlopeData slope = TestSlopeData.Load(Path.GetFullPath(TestSlopeTerrainBuilder.SlopeFolder));
            var cameraSettings = LoadOrCreate<FollowCameraSettings>("FollowCameraSettings");
            var viewSettings = LoadOrCreate<RiderViewSettings>("RiderViewSettings");
            var inputConfig = LoadOrCreate<RiderInputConfig>("RiderInputConfig");

            GameObject terrainObject = Terrain.CreateTerrainGameObject(terrainData);
            terrainObject.name = "TestSlopeTerrain";
            terrainObject.transform.position = SimCoordinatesUnity.TerrainOrigin(slope.Layout.Spec);
            terrainObject.GetComponent<Terrain>().heightmapPixelError = 1f;

            var driverObject = new GameObject("SimDriver");
            var input = driverObject.AddComponent<RiderInputReader>();
            Set(input, "config", inputConfig);
            var driver = driverObject.AddComponent<SimDriver>();
            Set(driver, "inputReader", input);

            var rider = new GameObject("Rider");
            slope.Heightmap.Sample(slope.Layout.StartX, slope.Layout.StartZ, out Fix64 startHeight, out _);
            rider.transform.position = SimCoordinatesUnity.ToVector3(new Vec3(slope.Layout.StartX, startHeight, slope.Layout.StartZ));
            var view = rider.AddComponent<RiderView>();
            Set(view, "driver", driver);
            Set(view, "settings", viewSettings);
            Set(view, "skiModel", AssetDatabase.LoadAssetAtPath<GameObject>(SkiModelPath));
            Set(view, "poleModel", AssetDatabase.LoadAssetAtPath<GameObject>(PoleModelPath));

            Camera camera = FindCamera(scene);
            var follow = camera.gameObject.AddComponent<FollowCamera>();
            Set(follow, "target", rider.transform);
            Set(follow, "settings", cameraSettings);

            EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            Debug.Log("Built " + ScenePath + ". Press Play to ride; F5 checks the replay, F6 saves the recording.");
        }

        private static Camera FindCamera(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                var camera = root.GetComponentInChildren<Camera>();
                if (camera != null)
                {
                    return camera;
                }
            }

            var created = new GameObject("Main Camera", typeof(Camera));
            created.tag = "MainCamera";
            return created.GetComponent<Camera>();
        }

        private static T LoadOrCreate<T>(string name) where T : ScriptableObject
        {
            EnsureFolder(SettingsFolder);
            string path = SettingsFolder + "/" + name + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<T>(), path);
                AssetDatabase.SaveAssets();
                asset = AssetDatabase.LoadAssetAtPath<T>(path);
            }

            return asset;
        }

        // Throws rather than save a null reference: a missing asset or a destroyed object would otherwise ship silently.
        private static void Set(Object component, string field, Object value)
        {
            var serialized = new SerializedObject(component);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
            {
                throw new System.InvalidOperationException(component.GetType().Name + " has no serialized field '" + field + "'.");
            }

            if (value == null)
            {
                throw new System.InvalidOperationException(component.GetType().Name + "." + field + " would be saved as null: its asset is missing or was unloaded.");
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (property.objectReferenceValue == null)
            {
                throw new System.InvalidOperationException(component.GetType().Name + "." + field + " did not keep its reference.");
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static void AddToBuildSettings(string path)
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            foreach (EditorBuildSettingsScene existing in scenes)
            {
                if (existing.path == path)
                {
                    return;
                }
            }

            var updated = new EditorBuildSettingsScene[scenes.Length + 1];
            scenes.CopyTo(updated, 0);
            updated[scenes.Length] = new EditorBuildSettingsScene(path, true);
            EditorBuildSettings.scenes = updated;
        }
    }
}
