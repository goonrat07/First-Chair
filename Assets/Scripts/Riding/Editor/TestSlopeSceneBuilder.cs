using System.IO;
using FirstChair.Sim;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FirstChair.Riding.Editor
{
    /// <summary>
    /// Builds <c>Assets/Scenes/TestSlope.unity</c> and <c>Assets/Scenes/TestMountain.unity</c> (Story 010) from the
    /// scripts, instead of hand-written scene files: terrain, sim driver with input, rider and follow camera, wired
    /// together, saved and added to the build; the mountain also gets its chairlift, run markers and the lift ride.
    /// Menu: First Chair > Build Test Slope Scene / Build Test Mountain Scene. Safe to run again; it overwrites the scene
    /// and keeps existing settings assets under <c>Assets/Data/Riding</c>.
    /// </summary>
    public static class TestSlopeSceneBuilder
    {
        /// <summary>The scene's path.</summary>
        public const string ScenePath = "Assets/Scenes/TestSlope.unity";

        /// <summary>The test mountain scene's path (Story 010).</summary>
        public const string MountainScenePath = "Assets/Scenes/TestMountain.unity";

        /// <summary>The test mountain's name in its files and in the player's StreamingAssets.</summary>
        public const string MountainName = "TestMountain";

        /// <summary>Where the settings assets live.</summary>
        public const string SettingsFolder = "Assets/Data/Riding";

        private const string SkiModelPath = "Assets/Art/Equipment/PR_Ski/PR_Ski.fbx";
        private const string PoleModelPath = "Assets/Art/Equipment/PR_SkiPole/PR_SkiPole.fbx";
        private const string BodyShader = "Universal Render Pipeline/Lit";
        private const string SkierModelPath = "Assets/Art/Characters/CH_Skier/CH_Skier.fbx";

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

        /// <summary>Builds and saves the test mountain scene, after offering to save the open scenes.</summary>
        [MenuItem("First Chair/Build Test Mountain Scene")]
        public static void BuildMountain()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            BuildMountainScene();
        }

        /// <summary>Builds and saves the scene without asking anything (CI builds call this).</summary>
        public static void BuildScene() =>
            BuildScene(ScenePath, TestSlopeTerrainBuilder.SlopeFolder, TestSlopeData.TestSlopeName, false);

        /// <summary>Builds and saves the test mountain scene without asking anything (CI builds call this).</summary>
        public static void BuildMountainScene() =>
            BuildScene(MountainScenePath, TestSlopeTerrainBuilder.MountainFolder, MountainName, true);

        private static void BuildScene(string scenePath, string slopeFolder, string slopeName, bool mountain)
        {
            // Assets are loaded after NewScene, which unloads unused assets. Settings created before it were saved as null
            // references in the CI build of 2026-10-22, so the camera never moved; Set now throws instead.
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            TerrainData terrainData = TestSlopeTerrainBuilder.RebuildAsset(slopeFolder, slopeName);
            TestSlopeData slope = TestSlopeData.Load(Path.GetFullPath(slopeFolder), slopeName);
            var cameraSettings = LoadOrCreate<FollowCameraSettings>("FollowCameraSettings");
            var viewSettings = LoadOrCreate<RiderViewSettings>("RiderViewSettings");
            var inputConfig = LoadOrCreate<RiderInputConfig>("RiderInputConfig");
            Material bodyMaterial = LoadOrCreateMaterial("RiderPlaceholder", BodyShader);
            Material trackMaterial = TestSlopeLook.TrackMaterial();
            TerrainLayer[] snowLayers = TestSlopeLook.SnowLayers();
            TestSlopeLook.PaintSnow(terrainData, snowLayers, slopeFolder, slope.Heightmap.Resolution, TestSlopeData.SnowFileOf(slopeName));
            Material terrainMaterial = TestSlopeLook.TerrainMaterial();

            GameObject terrainObject = Terrain.CreateTerrainGameObject(terrainData);
            terrainObject.name = slopeName + "Terrain";
            terrainObject.transform.position = SimCoordinatesUnity.TerrainOrigin(slope.Layout.Spec);
            var terrain = terrainObject.GetComponent<Terrain>();
            terrain.heightmapPixelError = 1f;
            terrain.basemapDistance = 600f;
            terrain.materialTemplate = terrainMaterial;
            Light sun = FindSun(scene);
            TestSlopeLook.Lighting(sun);

            // Story 007's lighting pass: the sun, sky light and grade as data, live in the Feel Lab's Light tab.
            var lighting = sun.gameObject.AddComponent<LightingRig>();
            Set(lighting, "settings", LoadOrCreate<LightingSettings>("LightingSettings"));

            var driverObject = new GameObject("SimDriver");
            var input = driverObject.AddComponent<RiderInputReader>();
            Set(input, "config", inputConfig);
            var driver = driverObject.AddComponent<SimDriver>();
            Set(driver, "inputReader", input);
            SetString(driver, "editorSlopeFolder", slopeFolder.Substring("Assets/".Length));
            SetString(driver, "playerSlopeFolder", slopeName);
            SetString(driver, "slopeName", slopeName);

            var rider = new GameObject("Rider");
            slope.Heightmap.Sample(slope.Layout.StartX, slope.Layout.StartZ, out Fix64 startHeight, out _);
            rider.transform.position = SimCoordinatesUnity.ToVector3(new Vec3(slope.Layout.StartX, startHeight, slope.Layout.StartZ));
            var skierModel = AssetDatabase.LoadAssetAtPath<GameObject>(SkierModelPath);
            if (skierModel != null)
            {
                var skier = rider.AddComponent<SkierView>();
                Set(skier, "driver", driver);
                Set(skier, "settings", viewSettings);
                Set(skier, "skierModel", skierModel);
                Set(skier, "skiModel", AssetDatabase.LoadAssetAtPath<GameObject>(SkiModelPath));
                Set(skier, "poleModel", AssetDatabase.LoadAssetAtPath<GameObject>(PoleModelPath));
                Set(skier, "trackMaterial", trackMaterial);
                SetArray(skier, "poseClips", LoadPoseClips(SkierModelPath));
            }
            else
            {
                Debug.LogWarning("TestSlopeSceneBuilder: no " + SkierModelPath + "; the placeholder skier rides instead.");
                var view = rider.AddComponent<RiderView>();
                Set(view, "driver", driver);
                Set(view, "settings", viewSettings);
                Set(view, "skiModel", AssetDatabase.LoadAssetAtPath<GameObject>(SkiModelPath));
                Set(view, "poleModel", AssetDatabase.LoadAssetAtPath<GameObject>(PoleModelPath));
                Set(view, "bodyMaterial", bodyMaterial);
                Set(view, "trackMaterial", trackMaterial);
            }

            // Story 008: quick turns, skids and brakes throw snow; a clean carve throws none.
            var spray = rider.AddComponent<SnowSpray>();
            Set(spray, "driver", driver);
            Set(spray, "settings", viewSettings);
            Set(spray, "material", TestSlopeLook.SprayMaterial());

            Camera camera = FindCamera(scene);
            var follow = camera.gameObject.AddComponent<FollowCamera>();
            Set(follow, "target", rider.transform);
            Set(follow, "driver", driver);
            Set(follow, "settings", cameraSettings);
            TestSlopeLook.PostProcessing(camera);

            var hud = new GameObject("HUD").AddComponent<RiderHud>();
            Set(hud, "driver", driver);

            var scenery = new GameObject("Scenery");
            TestSlopeLook.Trees(scenery.transform, slope);
            if (!mountain)
            {
                TestSlopeLook.OldTracks(scenery.transform, slope);
            }

            TestSlopeLook.RailsAndBoxes(scenery.transform, slope);
            if (mountain)
            {
                // Story 010: the chairlift, the runs' markers, and riding the lift back up.
                ChairLift lift = TestSlopeLook.Chairlift(scenery.transform, slope);
                TestSlopeLook.RunMarkers(scenery.transform, slope);
                if (lift != null)
                {
                    var ride = driverObject.AddComponent<LiftRide>();
                    Set(ride, "driver", driver);
                    Set(ride, "lift", lift);
                    Set(ride, "rider", rider.transform);
                    Set(ride, "followCamera", follow);
                }
            }

            EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, scenePath);
            AddToBuildSettings(scenePath);
            Debug.Log("Built " + scenePath + ". Press Play to ride; F5 checks the replay, F6 saves the recording, F1 hides the HUD.");
        }

        // The pose clips in SkierPose order, matched by name; Blender may prefix a take with "Armature|".
        private static AnimationClip[] LoadPoseClips(string modelPath)
        {
            var clips = new AnimationClip[SkierPoseWeights.Count];
            foreach (Object asset in AssetDatabase.LoadAllAssetRepresentationsAtPath(modelPath))
            {
                if (!(asset is AnimationClip clip) || clip.name.StartsWith("__preview__", System.StringComparison.Ordinal))
                {
                    continue;
                }

                string name = clip.name.Substring(clip.name.LastIndexOf('|') + 1);
                for (int i = 0; i < SkierPoseWeights.Count; i++)
                {
                    if (name == SkierPoseWeights.ClipName((SkierPose)i))
                    {
                        clips[i] = clip;
                    }
                }
            }

            for (int i = 0; i < SkierPoseWeights.Count; i++)
            {
                if (clips[i] == null)
                {
                    Debug.LogWarning("TestSlopeSceneBuilder: " + modelPath + " has no " + SkierPoseWeights.ClipName((SkierPose)i) + " clip.");
                }
            }

            return clips;
        }

        private static Light FindSun(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                var light = root.GetComponentInChildren<Light>();
                if (light != null && light.type == LightType.Directional)
                {
                    return light;
                }
            }

            var created = new GameObject("Sun").AddComponent<Light>();
            created.type = LightType.Directional;
            return created;
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

        private static Material LoadOrCreateMaterial(string name, string shaderName)
        {
            EnsureFolder(SettingsFolder);
            string path = SettingsFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find(shaderName);
                if (shader == null)
                {
                    throw new System.InvalidOperationException("Shader '" + shaderName + "' not found; is URP installed?");
                }

                AssetDatabase.CreateAsset(new Material(shader) { name = name }, path);
                AssetDatabase.SaveAssets();
                material = AssetDatabase.LoadAssetAtPath<Material>(path);
            }

            return material;
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

        // Sets a string field.
        private static void SetString(Object component, string field, string value)
        {
            var serialized = new SerializedObject(component);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null || property.propertyType != SerializedPropertyType.String)
            {
                throw new System.InvalidOperationException(component.GetType().Name + " has no serialized string '" + field + "'.");
            }

            property.stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // Sets an object array field; null entries are allowed (SkierView falls back to the stance clip).
        private static void SetArray(Object component, string field, Object[] values)
        {
            var serialized = new SerializedObject(component);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null || !property.isArray)
            {
                throw new System.InvalidOperationException(component.GetType().Name + " has no serialized array '" + field + "'.");
            }

            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
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
