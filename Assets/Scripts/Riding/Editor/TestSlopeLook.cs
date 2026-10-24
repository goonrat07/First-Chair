using System.IO;
using FirstChair.Sim;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FirstChair.Riding.Editor
{
    /// <summary>
    /// The test slope's look, built by script so the slope reads at the follow camera the way Grand Mountain Adventure 2's
    /// does (<c>art/reference/style-notes-gma2.md</c>): snow layers painted from the sim's snow map (corduroy on the
    /// groomers), a low warm sun with long blue shadows, distance haze, a tilt-shift blur toward the top of the screen,
    /// the spruce at the sim's tree positions, old ski tracks on the groomer and in the bowl, and the rails and boxes. Everything it makes goes in <see cref="Folder"/>,
    /// which is generated and not committed. Presentation only: the sim reads none of it.
    /// </summary>
    /// <example>
    /// <code>
    /// TerrainLayer[] layers = TestSlopeLook.SnowLayers();
    /// TestSlopeLook.PaintSnow(terrainData, layers, slopeFolder, slope.Heightmap.Resolution);
    /// TestSlopeLook.Lighting(sun);
    /// </code>
    /// </example>
    public static class TestSlopeLook
    {
        /// <summary>Where the generated textures, layers, materials and volume profile go.</summary>
        public const string Folder = "Assets/Data/Riding/Look";

        private const int TextureSize = 512;
        private const int AlphamapResolution = 1024;
        private const int SnowBlurTexels = 2;
        private const string TreeModelPath = "Assets/Art/Environment/EN_Conifer/EN_Conifer.fbx";

        // EN_Conifer asset-manifest.json: crown width 3.6958 m at LOD0.
        private const float TreeCrownRadius = 1.848f;

        // The tree's far LOD (story 007): crossed quads from art/source/EN_Conifer/build/billboard_atlas.py. The 12 m tree
        // fills about 25 / d of the follow camera's screen height at d metres, so LOD0 shows to about 100 m, LOD1 to
        // about 300 m and the billboard out to the 2 km far clip.
        private const string BillboardAtlasPath = "Assets/Art/Environment/EN_Conifer/EN_Conifer_Billboard.png";
        private const string BillboardLayoutPath = "Assets/Art/Environment/EN_Conifer/EN_Conifer_Billboard.json";
        private static readonly float[] TreeLodHeights = { 0.25f, 0.08f, 0.0125f };

        // Old tracks (story 003 decisions, 2026-10-24): on the groomer and in the bowl, about one track per 800 m^2 (58 on
        // the groomer; one per 2300 m^2 left barely one in view in build 40058662563), each ski's line 10 cm wide, 2 cm
        // off the snow, and 1 m clear of any canopy.
        private static readonly string[] OldTrackZones = { "groomer", "bowl" };
        private const double OldTrackAreaPerTrack = 800.0;
        private const ulong OldTrackSeed = 2026_10_24;
        private const float OldTrackWidth = 0.10f;
        private const float OldTrackLift = 0.02f;
        private const double OldTrackTreeClearance = 1.0;

        /// <summary>Generates one terrain layer per <see cref="SnowType"/>, in its numeric order.</summary>
        public static TerrainLayer[] SnowLayers()
        {
            EnsureFolder(Folder);
            var layers = new TerrainLayer[SnowSurface.LayerCount];
            var paths = new string[SnowSurface.LayerCount];
            var albedo = new byte[TextureSize * TextureSize * 4];
            var normal = new byte[TextureSize * TextureSize * 4];
            for (int i = 0; i < SnowSurface.LayerCount; i++)
            {
                var kind = (SnowType)i;
                SnowSurface.Texture(kind, TextureSize, albedo, normal);
                Texture2D albedoTexture = WriteTexture("Snow_" + kind + "_Albedo", albedo, false);
                Texture2D normalTexture = WriteTexture("Snow_" + kind + "_Normal", normal, true);

                paths[i] = Folder + "/Snow_" + kind + ".terrainlayer";
                TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(paths[i]);
                bool created = layer == null;
                if (created)
                {
                    layer = new TerrainLayer();
                }

                float tile = SnowSurface.TileMetres(kind);
                layer.diffuseTexture = albedoTexture;
                layer.normalMapTexture = normalTexture;
                layer.normalScale = 1f;
                layer.tileSize = new Vector2(tile, tile);
                layer.smoothness = SnowSurface.Smoothness(kind);
                // Smoothness per texel from the albedo's alpha, so the snow crystals glint (SnowSurface, sparkle).
                layer.smoothnessSource = TerrainLayerSmoothnessSource.DiffuseAlphaChannel;
                layer.metallic = 0f;
                if (created)
                {
                    AssetDatabase.CreateAsset(layer, paths[i]);
                }
                else
                {
                    EditorUtility.SetDirty(layer);
                }
            }

            AssetDatabase.SaveAssets();
            for (int i = 0; i < SnowSurface.LayerCount; i++)
            {
                layers[i] = AssetDatabase.LoadAssetAtPath<TerrainLayer>(paths[i]);
            }

            return layers;
        }

        /// <summary>Paints the terrain's layer weights from the sim's snow map, with softened edges.</summary>
        /// <param name="data">The terrain data.</param>
        /// <param name="layers">The layers from <see cref="SnowLayers"/>.</param>
        /// <param name="slopeFolder">The folder holding <c>TestSlopeSnow.raw</c>.</param>
        /// <param name="resolution">Samples a side of the snow map (the heightmap's resolution).</param>
        public static void PaintSnow(TerrainData data, TerrainLayer[] layers, string slopeFolder, int resolution)
        {
            byte[] snow = File.ReadAllBytes(Path.Combine(slopeFolder, TestSlopeData.SnowFile));
            data.terrainLayers = layers;
            data.alphamapResolution = AlphamapResolution;
            var weights = new float[AlphamapResolution, AlphamapResolution, SnowSurface.LayerCount];
            SnowSurface.LayerWeights(snow, resolution, weights, SnowBlurTexels);
            data.SetAlphamaps(0, 0, weights);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
        }

        /// <summary>The URP terrain material, so the build carries the terrain shader.</summary>
        public static Material TerrainMaterial() =>
            LitMaterial("TerrainSnow", "Universal Render Pipeline/Terrain/Lit", Color.white, 0.1f, false);

        /// <summary>The ski track material: a groove in shade, a little bluer and darker than the snow, double-sided.</summary>
        public static Material TrackMaterial() =>
            LitMaterial("SkiTrack", "Universal Render Pipeline/Lit", new Color(0.70f, 0.77f, 0.89f), 0f, true);

        /// <summary>A low, warm sun with long soft shadows; cool ambient so shade reads blue; haze with distance.</summary>
        /// <param name="sun">The scene's directional light.</param>
        public static void Lighting(Light sun)
        {
            // About 25 degrees up, ahead and to the right of a camera looking down the fall line (+Z), so shadows stretch
            // across the slope toward the bottom left, as in the GMA2 forest shot. Bright enough that lit snow is near
            // white after neutral tonemapping (the first build at 1.3 read grey).
            sun.transform.rotation = Quaternion.Euler(25f, -122f, 0f);
            sun.color = new Color(1f, 0.94f, 0.86f);
            sun.intensity = 2f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 1f;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.92f);
            RenderSettings.ambientEquatorColor = new Color(0.78f, 0.83f, 0.92f);
            RenderSettings.ambientGroundColor = new Color(0.55f, 0.60f, 0.70f);
            RenderSettings.skybox = Sky();
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.80f, 0.86f, 0.94f);
            RenderSettings.fogStartDistance = 150f;
            RenderSettings.fogEndDistance = 900f;
        }

        // Unity's procedural sky with a snow-coloured lower half, so any view past the terrain's edge reads as distant
        // snow in the haze instead of brown ground.
        private static Material Sky()
        {
            Material sky = LitMaterial("Sky", "Skybox/Procedural", Color.white, 0f, false);
            sky.SetColor("_GroundColor", new Color(0.82f, 0.86f, 0.92f));
            sky.SetColor("_SkyTint", new Color(0.50f, 0.60f, 0.75f));
            sky.SetFloat("_AtmosphereThickness", 0.8f);
            sky.SetFloat("_Exposure", 1.2f);
            EditorUtility.SetDirty(sky);
            AssetDatabase.SaveAssets();
            return sky;
        }

        /// <summary>
        /// Turns on post-processing for the camera and adds a global volume: a Gaussian depth of field that blurs the far
        /// slope (the tilt-shift look), a little bloom, neutral tonemapping that keeps snow white, and a light vignette.
        /// </summary>
        /// <param name="camera">The follow camera.</param>
        public static void PostProcessing(Camera camera)
        {
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            EnsureFolder(Folder);
            string path = Folder + "/SlopeVolume.asset";
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            var depthOfField = profile.Add<DepthOfField>(true);
            depthOfField.mode.Override(DepthOfFieldMode.Gaussian);
            depthOfField.gaussianStart.Override(110f);
            depthOfField.gaussianEnd.Override(320f);
            depthOfField.gaussianMaxRadius.Override(1.2f);
            depthOfField.highQualitySampling.Override(true);
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.25f);
            bloom.threshold.Override(1.1f);
            var tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.Neutral);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.18f);
            foreach (VolumeComponent component in profile.components)
            {
                AssetDatabase.AddObjectToAsset(component, profile);
            }

            AssetDatabase.SaveAssets();
            var volumeObject = new GameObject("PostProcessing");
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        }

        /// <summary>
        /// Places the spruce (<c>EN_Conifer.fbx</c>) at every tree in the slope layout, scaled so its crown matches the
        /// sim's branch radius. Without the model, a simple cone stands in. The sim, not these objects, decides collisions.
        /// </summary>
        /// <param name="parent">Parent for the trees.</param>
        /// <param name="slope">The slope.</param>
        public static void Trees(Transform parent, TestSlopeData slope)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(TreeModelPath);
            Material fallback = model == null ? LitMaterial("TreeFallback", "Universal Render Pipeline/Lit", new Color(0.12f, 0.22f, 0.16f), 0.1f, false) : null;
            Mesh billboardMesh = null;
            Material billboardMaterial = null;
            bool billboard = model != null && TreeBillboard(out billboardMesh, out billboardMaterial);
            for (int i = 0; i < slope.Layout.Trees.Count; i++)
            {
                SlopeTree tree = slope.Layout.Trees[i];
                slope.Heightmap.Sample(tree.X, tree.Z, out Fix64 height, out _);
                float scale = Mathf.Clamp(SimCoordinates.ToFloat(tree.BranchRadius) / TreeCrownRadius, 0.5f, 2f);
                GameObject instance = model != null ? (GameObject)PrefabUtility.InstantiatePrefab(model) : FallbackTree(fallback);
                instance.name = "Tree_" + i;
                instance.transform.SetParent(parent, false);
                instance.transform.SetPositionAndRotation(
                    new Vector3(SimCoordinates.ToFloat(tree.X), SimCoordinates.ToFloat(height), SimCoordinates.ToFloat(tree.Z)),
                    Quaternion.Euler(0f, i * 137.508f % 360f, 0f));
                instance.transform.localScale = Vector3.one * scale;
                if (billboard)
                {
                    AddBillboardLod(instance, billboardMesh, billboardMaterial);
                }
            }
        }

        /// <summary>The billboard's mesh and alpha-clipped material, made in <see cref="Folder"/>; false if its files are missing.</summary>
        private static bool TreeBillboard(out Mesh mesh, out Material material)
        {
            mesh = null;
            material = null;
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(BillboardAtlasPath);
            var layoutText = AssetDatabase.LoadAssetAtPath<TextAsset>(BillboardLayoutPath);
            if (atlas == null || layoutText == null)
            {
                Debug.LogWarning("TestSlopeLook: no tree billboard (" + BillboardAtlasPath + "); trees keep two LODs.");
                return false;
            }

            var layout = JsonUtility.FromJson<BillboardLayout>(layoutText.text);
            BillboardQuads.Build(layout.cells, layout.cellWidthMetres, layout.cellHeightMetres, layout.bottomMetres,
                out float[] positions, out float[] uvs, out int[] triangles);
            var vertices = new Vector3[positions.Length / 3];
            var texcoords = new Vector2[vertices.Length];
            var normals = new Vector3[vertices.Length];
            for (int v = 0; v < vertices.Length; v++)
            {
                vertices[v] = new Vector3(positions[3 * v], positions[3 * v + 1], positions[3 * v + 2]);
                texcoords[v] = new Vector2(uvs[2 * v], uvs[2 * v + 1]);
                normals[v] = Vector3.up;  // lit like the snow from above, the same on every side and from both faces
            }

            mesh = new Mesh { name = "EN_Conifer_Billboard", vertices = vertices, uv = texcoords, normals = normals, triangles = triangles };
            mesh.RecalculateBounds();
            string meshPath = Folder + "/EN_Conifer_Billboard.asset";
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);

            material = LitMaterial("ConiferBillboard", "Universal Render Pipeline/Lit", Color.white, 0f, true);
            material.SetTexture("_BaseMap", atlas);
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", layout.alphaCutoff);
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetOverrideTag("RenderType", "TransparentCutout");
            material.renderQueue = (int)RenderQueue.AlphaTest;
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return true;
        }

        /// <summary>Adds the billboard as the tree's last LOD and sets the LOD switch heights.</summary>
        private static void AddBillboardLod(GameObject tree, Mesh mesh, Material material)
        {
            var group = tree.GetComponent<LODGroup>();
            if (group == null)
            {
                Debug.LogWarning("TestSlopeLook: " + tree.name + " has no LODGroup; no billboard.");
                return;
            }

            var quads = new GameObject("EN_Conifer_Billboard");
            quads.transform.SetParent(tree.transform, false);
            quads.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = quads.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            LOD[] lods = group.GetLODs();
            var withBillboard = new LOD[lods.Length + 1];
            for (int i = 0; i < withBillboard.Length; i++)
            {
                Renderer[] renderers = i < lods.Length ? lods[i].renderers : new Renderer[] { renderer };
                withBillboard[i] = new LOD(TreeLodHeights[Mathf.Min(i, TreeLodHeights.Length - 1)], renderers);
            }

            group.SetLODs(withBillboard);
            group.RecalculateBounds();
        }

        /// <summary><c>EN_Conifer_Billboard.json</c>, written by billboard_atlas.py.</summary>
        [System.Serializable]
        private sealed class BillboardLayout
        {
            public int cells = 2;
            public float cellWidthMetres = 3.6f;
            public float cellHeightMetres = 14.4f;
            public float bottomMetres = -0.1f;
            public float alphaCutoff = 0.5f;
        }

        /// <summary>
        /// Draws the layout's rails (a metal bar on two posts) and boxes (a blue box from the snow to the top), following
        /// the snow under each end. The sim, not these objects, decides grinds.
        /// </summary>
        /// <param name="parent">Parent for the features.</param>
        /// <param name="slope">The slope.</param>
        public static void RailsAndBoxes(Transform parent, TestSlopeData slope)
        {
            Material metal = LitMaterial("RailMetal", "Universal Render Pipeline/Lit", new Color(0.55f, 0.58f, 0.62f), 0.7f, false);
            Material boxSide = LitMaterial("BoxSide", "Universal Render Pipeline/Lit", new Color(0.12f, 0.32f, 0.72f), 0.3f, false);
            for (int i = 0; i < slope.Layout.Rails.Count; i++)
            {
                SlopeRail rail = slope.Layout.Rails[i];
                float height = rail.HeightMm / 1000f;
                float width = rail.WidthMm / 1000f;
                Vector3 start = SnowPoint(slope, rail.X0Mm, rail.Z0Mm);
                Vector3 end = SnowPoint(slope, rail.X1Mm, rail.Z1Mm);
                Vector3 topStart = start + Vector3.up * height;
                Vector3 topEnd = end + Vector3.up * height;
                Vector3 along = topEnd - topStart;
                var feature = new GameObject((rail.Box ? "Box_" : "Rail_") + i);
                feature.transform.SetParent(parent, false);
                if (rail.Box)
                {
                    Transform box = Primitive(PrimitiveType.Cube, feature.transform, boxSide);
                    box.SetPositionAndRotation((topStart + topEnd) * 0.5f - Vector3.up * (height * 0.5f), Quaternion.LookRotation(along, Vector3.up));
                    box.localScale = new Vector3(width, height, along.magnitude);
                }
                else
                {
                    Transform bar = Primitive(PrimitiveType.Cylinder, feature.transform, metal);
                    bar.SetPositionAndRotation((topStart + topEnd) * 0.5f, Quaternion.FromToRotation(Vector3.up, along));
                    bar.localScale = new Vector3(width, along.magnitude * 0.5f, width);
                    foreach (Vector3 foot in new[] { start, end })
                    {
                        Transform post = Primitive(PrimitiveType.Cylinder, feature.transform, metal);
                        post.position = foot + Vector3.up * (height * 0.5f);
                        post.localScale = new Vector3(width * 0.8f, height * 0.5f, width * 0.8f);
                    }
                }
            }
        }

        /// <summary>
        /// Lays old ski tracks (<see cref="FirstChair.Riding.OldTracks"/>) over the groomer and the bowl: each ski's line
        /// is a thin ribbon a little bluer and darker than the snow, following the snow, clear of the trees. One mesh per zone, saved
        /// in <see cref="Folder"/>. Display only: they cast no shadow, and the sim never reads them.
        /// </summary>
        /// <param name="parent">Parent for the track meshes.</param>
        /// <param name="slope">The slope.</param>
        public static void OldTracks(Transform parent, TestSlopeData slope)
        {
            // A little lighter than the live track (TrackMaterial), so old tracks read as older.
            Material material = LitMaterial("OldTrack", "Universal Render Pipeline/Lit", new Color(0.74f, 0.80f, 0.91f), 0f, false);
            for (int z = 0; z < OldTrackZones.Length; z++)
            {
                if (!slope.Layout.TryGetZone(OldTrackZones[z], out SlopeZone zone))
                {
                    Debug.LogWarning("TestSlopeLook: the layout has no " + OldTrackZones[z] + " zone; no old tracks there.");
                    continue;
                }

                double xMin = SimCoordinates.ToDouble(zone.XMin), xMax = SimCoordinates.ToDouble(zone.XMax);
                double zMin = SimCoordinates.ToDouble(zone.ZMin), zMax = SimCoordinates.ToDouble(zone.ZMax);
                OldTrackSettings settings = OldTrackSettings.Default;
                settings.Count = Mathf.Max(1, Mathf.RoundToInt((float)((xMax - xMin) * (zMax - zMin) / OldTrackAreaPerTrack)));
                var lines = FirstChair.Riding.OldTracks.Generate(
                    OldTrackSeed + (ulong)z, xMin, zMin, xMax, zMax, settings, (x, zz) => NearTree(slope, x, zz));
                Mesh mesh = TrackRibbons(slope, lines, "OldTracks_" + zone.Name);
                string path = Folder + "/OldTracks_" + zone.Name + ".asset";
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(mesh, path);

                var tracks = new GameObject("OldTracks_" + zone.Name);
                tracks.transform.SetParent(parent, false);
                tracks.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = tracks.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        private static bool NearTree(TestSlopeData slope, double x, double z)
        {
            foreach (SlopeTree tree in slope.Layout.Trees)
            {
                double dx = x - tree.XMm / 1000.0, dz = z - tree.ZMm / 1000.0;
                double reach = tree.BranchRadiusMm / 1000.0 + OldTrackTreeClearance;
                if (dx * dx + dz * dz < reach * reach)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>One ribbon per line, <see cref="OldTrackWidth"/> wide, lifted off the snow so it never sinks into it.</summary>
        private static Mesh TrackRibbons(TestSlopeData slope, System.Collections.Generic.List<OldTrackLine> lines, string name)
        {
            var vertices = new System.Collections.Generic.List<Vector3>();
            var normals = new System.Collections.Generic.List<Vector3>();
            var triangles = new System.Collections.Generic.List<int>();
            foreach (OldTrackLine line in lines)
            {
                int first = vertices.Count;
                for (int i = 0; i < line.Count; i++)
                {
                    int a = Mathf.Max(0, i - 1), b = Mathf.Min(line.Count - 1, i + 1);
                    var along = new Vector2((float)(line.X[b] - line.X[a]), (float)(line.Z[b] - line.Z[a])).normalized;
                    var side = new Vector3(along.y, 0f, -along.x) * (OldTrackWidth / 2f);
                    slope.Heightmap.Sample(
                        SimCoordinates.FromMetres(line.X[i]), SimCoordinates.FromMetres(line.Z[i]), out Fix64 height, out Vec3 normal);
                    var up = new Vector3(SimCoordinates.ToFloat(normal.X), SimCoordinates.ToFloat(normal.Y), SimCoordinates.ToFloat(normal.Z));
                    var centre = new Vector3((float)line.X[i], SimCoordinates.ToFloat(height), (float)line.Z[i]) + up * OldTrackLift;
                    vertices.Add(centre - side);
                    vertices.Add(centre + side);
                    normals.Add(up);
                    normals.Add(up);
                    if (i > 0)
                    {
                        int v = first + 2 * i;
                        triangles.AddRange(new[] { v - 2, v, v - 1, v - 1, v, v + 1 });
                    }
                }
            }

            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static GameObject FallbackTree(Material material)
        {
            var root = new GameObject("TreeFallback");
            Transform crown = Primitive(PrimitiveType.Capsule, root.transform, material);
            crown.localPosition = new Vector3(0f, 6.5f, 0f);
            crown.localScale = new Vector3(3.6f, 5.5f, 3.6f);
            return root;
        }

        private static Vector3 SnowPoint(TestSlopeData slope, int xMm, int zMm)
        {
            Fix64 x = Fix64.FromRatio(xMm, 1000);
            Fix64 z = Fix64.FromRatio(zMm, 1000);
            slope.Heightmap.Sample(x, z, out Fix64 height, out _);
            return new Vector3(SimCoordinates.ToFloat(x), SimCoordinates.ToFloat(height), SimCoordinates.ToFloat(z));
        }

        private static Transform Primitive(PrimitiveType type, Transform parent, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.GetComponent<Renderer>().sharedMaterial = material;
            part.transform.SetParent(parent, false);
            return part.transform;
        }

        private static Material LitMaterial(string name, string shaderName, Color color, float smoothness, bool doubleSided)
        {
            EnsureFolder(Folder);
            string path = Folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find(shaderName);
                if (shader == null)
                {
                    throw new System.InvalidOperationException("Shader '" + shaderName + "' not found; is URP installed?");
                }

                AssetDatabase.CreateAsset(new Material(shader) { name = name }, path);
                material = AssetDatabase.LoadAssetAtPath<Material>(path);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }

            if (doubleSided && material.HasProperty("_Cull"))
            {
                material.SetFloat("_Cull", (float)CullMode.Off);
            }

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static Texture2D WriteTexture(string name, byte[] rgba, bool normalMap)
        {
            string path = Folder + "/" + name + ".png";
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false, normalMap);
            texture.SetPixelData(rgba, 0);
            texture.Apply(false);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normalMap;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
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
    }
}
