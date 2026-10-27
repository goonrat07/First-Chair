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
    public static partial class TestSlopeLook
    {
        /// <summary>Where the generated textures, layers, materials and volume profile go.</summary>
        public const string Folder = "Assets/Data/Riding/Look";

        /// <summary>The snow terrain's shader (Assets/Shaders/SnowTerrain/SH_Env_SnowTerrain.shader).</summary>
        public const string SnowTerrainShader = "First Chair/Snow Terrain";

        private const int TextureSize = 512;
        private const int AlphamapResolution = 1024;
        private const int SnowBlurTexels = 2;
        private const string TreeFolder = "Assets/Art/Environment";

        // The forest's tree models (story 016), each a tree-kit asset in TreeFolder/<asset>/: its share of the forest,
        // per cent, and the crown radius it is scaled from, metres (0: half its manifest's crown width). Each tree picks
        // one from a hash of its position (TreeVariantPicker). The snag has stubs, not a crown, so it is scaled as the
        // spruce is rather than blown up to the layout's branch radius.
        private static readonly (string Asset, int Share, float ScaleRadius)[] TreeVariants =
        {
            ("EN_Conifer", 45, 0f),
            ("EN_ConiferSlim", 25, 0f),
            ("EN_ConiferBroad", 20, 0f),
            ("EN_ConiferYoung", 7, 0f),
            ("EN_Snag", 3, 1.848f),
        };

        // No tree stands taller than this, metres: a little above the tallest spruce the test slope grows (12 m at its
        // 2.5 m branch radius: 16.2 m), so no spruce changes. A tree is scaled so its crown matches the sim's branch
        // radius, which would make the slim fir 28 m tall there; above this height it is scaled down instead, and its
        // crown falls short of the branch circle (its lowest tiers are 3 m up, over a passing rider's head).
        private const float MaxTreeHeightMetres = 16.5f;

        // A tree's far LOD (story 007): crossed quads from tools/blender/trees/billboard_atlas.py. The 12 m spruce
        // fills about 25 / d of the follow camera's screen height at d metres, so LOD0 shows to about 100 m, LOD1 to
        // about 300 m and the billboard out to the 2 km far clip; the switch heights are fractions of each tree's own
        // size on screen, so they hold for every variant.
        private static readonly float[] TreeLodHeights = { 0.25f, 0.08f, 0.0125f };

        // Old tracks (story 003 decisions, 2026-10-24): on the groomer and in the bowl, each ski's line 10 cm wide, 2 cm off
        // the snow, and 1 m clear of any canopy. Story 009: about one track per 350 m^2 (one per 800 left the snow too bare
        // to show motion at speed), swinging wider and more often, so more of them cross the rider's path.
        private static readonly string[] OldTrackZones = { "groomer", "bowl" };
        private const double OldTrackAreaPerTrack = 350.0;
        private const double OldTrackMinSwingDegrees = 25.0;
        private const double OldTrackMaxSwingDegrees = 70.0;
        private const double OldTrackMinWavelengthMetres = 14.0;
        private const double OldTrackMaxWavelengthMetres = 32.0;
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
        /// <param name="snowFile">The snow map's file name (the test mountain's is <c>TestMountainSnow.raw</c>).</param>
        public static void PaintSnow(TerrainData data, TerrainLayer[] layers, string slopeFolder, int resolution, string snowFile = TestSlopeData.SnowFile)
        {
            byte[] snow = File.ReadAllBytes(Path.Combine(slopeFolder, snowFile));
            data.terrainLayers = layers;
            data.alphamapResolution = AlphamapResolution;
            var weights = new float[AlphamapResolution, AlphamapResolution, SnowSurface.LayerCount];
            SnowSurface.LayerWeights(snow, resolution, weights, SnowBlurTexels);
            data.SetAlphamaps(0, 0, weights);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// The terrain material, on First Chair's snow terrain shader (story 014: URP's terrain with glints, blue shade,
        /// soft light and wind ripples), so the build carries the shader. A material made before story 014, on URP's
        /// stock terrain shader, is switched over.
        /// </summary>
        public static Material TerrainMaterial()
        {
            Material material = LitMaterial("TerrainSnow", SnowTerrainShader, Color.white, 0.1f, false);
            Shader shader = Shader.Find(SnowTerrainShader);
            if (shader != null && material.shader != shader)
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssets();
            }

            return material;
        }

        /// <summary>The ski track material: a groove in shade, a little bluer and darker than the snow, translucent and double-sided.</summary>
        public static Material TrackMaterial() =>
            Translucent(LitMaterial("SkiTrack", "Universal Render Pipeline/Lit", TrackColour, 0f, true));

        // Tracks are translucent (story 007, the user's "tracks should build up as in GMA2"): where tracks cross or ride over
        // each other they darken, so a well-ridden line reads as worn in. One layer over white snow comes out about as the
        // old opaque colours did, (0.70, 0.77, 0.89) live and (0.74, 0.80, 0.91) old; the old ones stay a little fainter.
        private static readonly Color TrackColour = new Color(0.55f, 0.65f, 0.86f, 0.55f);
        private static readonly Color OldTrackColour = new Color(0.58f, 0.66f, 0.87f, 0.50f);

        // URP Lit as an alpha-blended transparent surface, set the way URP's own material editor sets it.
        private static Material Translucent(Material material)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_AlphaClip", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        /// <summary>
        /// The snow spray's flake (Story 008): URP's unlit particle shader, white, a round flake cut out by alpha clipping,
        /// as the tree billboards are.
        /// </summary>
        public static Material SprayMaterial()
        {
            Material material = LitMaterial("SnowSpray", "Universal Render Pipeline/Particles/Unlit", Color.white, 0f, false);
            material.SetTexture("_BaseMap", FlakeTexture());
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", 0.5f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetOverrideTag("RenderType", "TransparentCutout");
            material.renderQueue = (int)RenderQueue.AlphaTest;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        // A white disc with a soft rim on a clear ground. The alpha clip cuts it round, and the spray's fade, which scales
        // the alpha, shrinks it away.
        private static Texture2D FlakeTexture()
        {
            const int size = 64;
            string path = Folder + "/SnowFlake.png";
            var rgba = new byte[size * size * 4];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float alpha = Mathf.Clamp01((1f - Mathf.Sqrt(dx * dx + dy * dy)) / 0.35f);
                    int i = 4 * (y * size + x);
                    rgba[i] = rgba[i + 1] = rgba[i + 2] = 255;
                    rgba[i + 3] = (byte)Mathf.RoundToInt(255f * alpha);
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixelData(rgba, 0);
            texture.Apply(false);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

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
        /// Places a tree model at every tree in the slope layout (story 016: each picks one of <see cref="TreeVariants"/>
        /// from a hash of its position), scaled so its crown matches the sim's branch radius but no taller than
        /// <see cref="MaxTreeHeightMetres"/>. A variant whose files are missing drops out of the mix; with none, a simple
        /// cone stands in. The sim, not these objects, decides collisions.
        /// </summary>
        /// <param name="parent">Parent for the trees.</param>
        /// <param name="slope">The slope.</param>
        public static void Trees(Transform parent, TestSlopeData slope)
        {
            int count = TreeVariants.Length;
            var models = new GameObject[count];
            var radii = new float[count];
            var heights = new float[count];
            var shares = new int[count];
            var billboardMeshes = new Mesh[count];
            var billboardMaterials = new Material[count];
            bool anyVariant = false;
            for (int v = 0; v < count; v++)
            {
                (string asset, int share, float scaleRadius) = TreeVariants[v];
                models[v] = AssetDatabase.LoadAssetAtPath<GameObject>(TreeFolder + "/" + asset + "/" + asset + ".fbx");
                TreeFields size = TreeSize(asset);
                radii[v] = scaleRadius > 0f ? scaleRadius : 0.5f * size.crown_width_m;
                heights[v] = size.height_m;
                shares[v] = models[v] != null && radii[v] > 0f ? share : 0;
                if (shares[v] > 0)
                {
                    anyVariant = true;
                    TreeBillboard(asset, out billboardMeshes[v], out billboardMaterials[v]);
                }
                else
                {
                    Debug.LogWarning("TestSlopeLook: tree variant " + asset + " is missing its model or manifest; left out of the forest.");
                }
            }

            Material fallback = anyVariant
                ? null
                : LitMaterial("TreeFallback", "Universal Render Pipeline/Lit", new Color(0.12f, 0.22f, 0.16f), 0.1f, false);
            for (int i = 0; i < slope.Layout.Trees.Count; i++)
            {
                SlopeTree tree = slope.Layout.Trees[i];
                slope.Heightmap.Sample(tree.X, tree.Z, out Fix64 height, out _);
                int v = TreeVariantPicker.Pick(tree.XMm, tree.ZMm, shares);
                GameObject instance = v >= 0 ? (GameObject)PrefabUtility.InstantiatePrefab(models[v]) : FallbackTree(fallback);
                float scale = v >= 0
                    ? TreeVariantPicker.Scale(SimCoordinates.ToFloat(tree.BranchRadius), radii[v], heights[v], MaxTreeHeightMetres)
                    : 1f;
                instance.name = "Tree_" + i;
                instance.transform.SetParent(parent, false);
                instance.transform.SetPositionAndRotation(
                    new Vector3(SimCoordinates.ToFloat(tree.X), SimCoordinates.ToFloat(height), SimCoordinates.ToFloat(tree.Z)),
                    Quaternion.Euler(0f, i * 137.508f % 360f, 0f));
                instance.transform.localScale = Vector3.one * scale;
                if (v >= 0 && billboardMeshes[v] != null)
                {
                    AddBillboardLod(instance, billboardMeshes[v], billboardMaterials[v], TreeVariants[v].Asset + "_Billboard");
                }
            }
        }

        /// <summary>A tree asset's LOD0 crown width and height, metres, from its <c>asset-manifest.json</c>; zeros if it has none.</summary>
        private static TreeFields TreeSize(string asset)
        {
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(TreeFolder + "/" + asset + "/asset-manifest.json");
            return text != null ? JsonUtility.FromJson<TreeManifest>(text.text).tree : new TreeFields();
        }

        /// <summary>
        /// A tree asset's billboard mesh and alpha-clipped material, made in <see cref="Folder"/> from its
        /// <c>&lt;asset&gt;_Billboard</c> atlas and layout; false (and nulls) if those files are missing.
        /// </summary>
        private static bool TreeBillboard(string asset, out Mesh mesh, out Material material)
        {
            mesh = null;
            material = null;
            string name = asset + "_Billboard";
            string atlasPath = TreeFolder + "/" + asset + "/" + name + ".png";
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
            var layoutText = AssetDatabase.LoadAssetAtPath<TextAsset>(TreeFolder + "/" + asset + "/" + name + ".json");
            if (atlas == null || layoutText == null)
            {
                Debug.LogWarning("TestSlopeLook: no tree billboard (" + atlasPath + "); " + asset + " keeps two LODs.");
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

            mesh = new Mesh { name = name, vertices = vertices, uv = texcoords, normals = normals, triangles = triangles };
            mesh.RecalculateBounds();
            string meshPath = Folder + "/" + name + ".asset";
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);

            material = LitMaterial(asset == "EN_Conifer" ? "ConiferBillboard" : asset + "Billboard", "Universal Render Pipeline/Lit", Color.white, 0f, true);
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
        private static void AddBillboardLod(GameObject tree, Mesh mesh, Material material, string name)
        {
            var group = tree.GetComponent<LODGroup>();
            if (group == null)
            {
                Debug.LogWarning("TestSlopeLook: " + tree.name + " has no LODGroup; no billboard.");
                return;
            }

            var quads = new GameObject(name);
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

        /// <summary><c>&lt;asset&gt;_Billboard.json</c>, written by billboard_atlas.py.</summary>
        [System.Serializable]
        private sealed class BillboardLayout
        {
            public int cells = 2;
            public float cellWidthMetres = 3.6f;
            public float cellHeightMetres = 14.4f;
            public float bottomMetres = -0.1f;
            public float alphaCutoff = 0.5f;
        }

        /// <summary>The part of a tree asset's <c>asset-manifest.json</c> the forest reads (manifest_extras.py writes it).</summary>
        [System.Serializable]
        private sealed class TreeManifest
        {
            public TreeFields tree = new TreeFields();
        }

        /// <summary>The manifest's <c>tree</c> object.</summary>
        [System.Serializable]
        private sealed class TreeFields
        {
            public float crown_width_m;
            public float height_m;
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
            // Fainter than the live track (TrackMaterial), so old tracks read as older; translucent, so crossings build up.
            Material material = Translucent(LitMaterial("OldTrack", "Universal Render Pipeline/Lit", OldTrackColour, 0f, false));
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
                settings.MinSwingDegrees = OldTrackMinSwingDegrees;
                settings.MaxSwingDegrees = OldTrackMaxSwingDegrees;
                settings.MinWavelengthMetres = OldTrackMinWavelengthMetres;
                settings.MaxWavelengthMetres = OldTrackMaxWavelengthMetres;
                var lines = FirstChair.Riding.OldTracks.Generate(
                    OldTrackSeed + (ulong)z, xMin, zMin, xMax, zMax, settings, (x, zz) => NearTree(slope, x, zz));
                OldTracksObject(parent, slope, lines, "OldTracks_" + zone.Name, material);
            }
        }

        /// <summary>Saves the lines' ribbons as a mesh in <see cref="Folder"/> and shows it under the parent.</summary>
        private static void OldTracksObject(
            Transform parent, TestSlopeData slope, System.Collections.Generic.List<OldTrackLine> lines, string name, Material material)
        {
            Mesh mesh = TrackRibbons(slope, lines, name);
            string path = Folder + "/" + name + ".asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);

            var tracks = new GameObject(name);
            tracks.transform.SetParent(parent, false);
            tracks.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = tracks.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
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
