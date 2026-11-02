using UnityEngine;

namespace FirstChair.Riding
{
    /// <summary>
    /// The deformable snow near the rider (Story 018): a finely divided mesh (<see cref="SnowSkinLayout"/>) that follows
    /// the rider, set on the terrain and pushed down where the skis went and up beside it by its shader
    /// (<c>First Chair/Snow Skin</c>, reading <see cref="TrackGrooves"/>' track map), so the tracks have real depth, cast
    /// shadows and occlude. The terrain cuts itself away under the mesh in every pass; the track ribbons fade out where
    /// the mesh's trenches draw the track. Display only: the sim never reads it. Single-thread only (Unity's main thread).
    /// </summary>
    /// <remarks>
    /// The mesh is built once and moved in steps of its coarsest spacing, so every vertex stays on its own grid. The
    /// terrain's heights and normals go to the GPU once, as textures, so the mesh lies exactly on the terrain where no
    /// track is. With the groove strength at 0 (<see cref="LightingSettings"/>) the mesh and the cut are off and the snow
    /// draws as it did before the story. <see cref="DepthAt"/> tells the skier view how deep the snow is cut under the
    /// rider, from the terrain's splat weights kept as bytes (about 5 MB at 1,024 texels and five layers). No allocation
    /// a frame.
    /// </remarks>
    /// <example>
    /// <code>
    /// // In the scene builder, once:
    /// var skin = new GameObject("SnowSkin").AddComponent&lt;SnowSkin&gt;();   // adds its MeshFilter and MeshRenderer
    /// Set(skin, "terrain", terrain);
    /// Set(skin, "follow", rider.transform);
    /// skin.GetComponent&lt;MeshRenderer&gt;().sharedMaterials = new[] { skinMaterial, skinAddMaterial };
    /// // Each frame, in the skier view:
    /// float sink = skin.DepthAt(rider.position);
    /// </code>
    /// </example>
    [DefaultExecutionOrder(210)] // after the skier view places the rider (50)
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class SnowSkin : MonoBehaviour
    {
        private static readonly int HeightsId = Shader.PropertyToID("_FCSnowHeights");
        private static readonly int NormalsId = Shader.PropertyToID("_FCSnowNormals");
        private static readonly int Control0Id = Shader.PropertyToID("_FCSnowControl0");
        private static readonly int Control1Id = Shader.PropertyToID("_FCSnowControl1");
        private static readonly int TerrainId = Shader.PropertyToID("_FCSnowTerrain");
        private static readonly int HeightmapId = Shader.PropertyToID("_FCSnowHeightmap");
        private static readonly int SkinId = Shader.PropertyToID("_FCSnowSkin");
        private static readonly int SkinEdgeId = Shader.PropertyToID("_FCSnowSkinEdge");
        private static readonly int ClipId = Shader.PropertyToID("_FCSnowSkinClipUV");
        private static readonly int GrooveId = Shader.PropertyToID("_FCSnowGroove");
        private static readonly int GrooveLayers0Id = Shader.PropertyToID("_FCSnowGrooveLayers0");
        private static readonly int GrooveLayers1Id = Shader.PropertyToID("_FCSnowGrooveLayers1");

        // The shader's two splat maps hold four layers each (LightingRig's _FCSnowGrooveLayers0 and 1).
        private const int LayersPerMap = 4;
        private const int ShaderLayers = 2 * LayersPerMap;

        // The splat weights are read from the terrain this many rows at a time, so the read never holds the whole map
        // as floats (20 MB at 1,024 texels and five layers).
        private const int SplatRowsPerRead = 64;

        // A splat weight of 1, as a byte (the alphamap textures' 8 bits).
        private const float FullWeight = 255f;

        [Tooltip("The terrain the snow lies on; found in the scene if left empty.")]
        [SerializeField] private Terrain terrain;
        [Tooltip("What the mesh follows: the rider.")]
        [SerializeField] private Transform follow;
        [Tooltip("The finest spacing, beside the rider, metres. A 9 cm track wants three or more vertices across.")]
        [SerializeField, Range(0.01f, 0.2f)] private float baseSpacing = 0.03f;
        [Tooltip("How many levels, each twice as coarse as the one inside it.")]
        [SerializeField, Range(1, 5)] private int levels = 3;
        [Tooltip("Cells from each level's centre to its edge (even). 128 with 3 levels at 3 cm reaches 15.36 m each way.")]
        [SerializeField, Range(16, 256)] private int cellsPerHalf = 128;
        [Tooltip("How far the mesh reaches past the cut in the terrain, metres. There it lies just under the terrain, so no "
            + "crack can open between them.")]
        [SerializeField, Range(0.1f, 4f)] private float overlapMetres = 0.5f;
        [Tooltip("The trenches fade to nothing over this distance before the cut, metres, so the mesh meets the terrain flat.")]
        [SerializeField, Range(0.5f, 10f)] private float fadeMetres = 3f;
        [Tooltip("How far the overlap past the cut drops under the terrain, metres.")]
        [SerializeField, Range(0.001f, 0.1f)] private float overlapDrop = 0.02f;

        private SnowSkinLayout _layout;
        private Mesh _mesh;
        private MeshRenderer _renderer;
        private Texture2D _heights;
        private Texture2D _normals;
        private Vector3 _terrainOrigin;
        private Vector3 _terrainSize;
        private readonly float[] _layerDepths = new float[ShaderLayers];
        private byte[] _splat;
        private int _splatTexels;
        private int _splatLayers;

        private void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            if (terrain == null)
            {
                terrain = FindAnyObjectByType<Terrain>();
            }

            if (terrain == null || follow == null)
            {
                Debug.LogWarning("SnowSkin: no terrain or nothing to follow; the snow stays as the terrain draws it.");
                enabled = false;
                return;
            }

            _layout = new SnowSkinLayout(baseSpacing, levels, cellsPerHalf + cellsPerHalf % 2);
            _mesh = BuildMesh();
            GetComponent<MeshFilter>().sharedMesh = _mesh;
            BuildTerrainTextures();
            KeepSplatWeights(terrain.terrainData);
        }

        /// <summary>
        /// How deep a ski cuts the snow at a point, metres: the snow mesh's trench depth there, from the terrain's snow
        /// types and the depths and strength the lighting rig set (<see cref="SnowTrench.SplatDepth"/>, as the shader's
        /// <c>FCSnowSkinDepth</c>). 0 with the snow mesh off. Allocates nothing.
        /// </summary>
        /// <param name="position">The point, world space; only its X and Z count.</param>
        /// <returns>The depth, metres.</returns>
        /// <example>
        /// <code>
        /// float sink = skin.DepthAt(rider.position);   // 0.15 in powder at the default depths, 0.02 on the groomer
        /// </code>
        /// </example>
        public float DepthAt(Vector3 position)
        {
            Vector4 groove = Shader.GetGlobalVector(GrooveId);
            if (_splat == null || !isActiveAndEnabled || !(groove.x > 0f))
            {
                return 0f;
            }

            Vector4 first = Shader.GetGlobalVector(GrooveLayers0Id), second = Shader.GetGlobalVector(GrooveLayers1Id);
            for (int i = 0; i < LayersPerMap; i++)
            {
                _layerDepths[i] = first[i];
                _layerDepths[LayersPerMap + i] = second[i];
            }

            float u = (position.x - _terrainOrigin.x) / _terrainSize.x;
            float v = (position.z - _terrainOrigin.z) / _terrainSize.z;
            return SnowTrench.SplatDepth(_splat, _splatTexels, _splatLayers, u, v, _layerDepths, groove.x);
        }

        private void OnDisable()
        {
            // An unset cut discards nothing, and a zero mesh centre and size leave the ribbons whole.
            Shader.SetGlobalVector(ClipId, Vector4.zero);
            Shader.SetGlobalVector(SkinId, Vector4.zero);
            if (_renderer != null)
            {
                _renderer.enabled = false;
            }
        }

        private void OnDestroy()
        {
            Destroy(_mesh);
            Destroy(_heights);
            Destroy(_normals);
        }

        private void LateUpdate()
        {
            // With the grooves off the mesh draws nothing the terrain doesn't, so the terrain draws it, as before.
            bool on = Shader.GetGlobalVector(GrooveId).x > 0f;
            _renderer.enabled = on;
            if (!on)
            {
                Shader.SetGlobalVector(ClipId, Vector4.zero);
                Shader.SetGlobalVector(SkinId, Vector4.zero);
                return;
            }

            Vector3 at = follow.position;
            _layout.SnapOrigin(at.x, at.z, out float x, out float z);
            transform.position = new Vector3(x, 0f, z);
            float cut = _layout.HalfSize - overlapMetres;
            float fadeFrom = Mathf.Max(0f, cut - fadeMetres);
            Shader.SetGlobalVector(SkinId, new Vector4(x, z, cut, fadeFrom));
            Shader.SetGlobalVector(ClipId, new Vector4(
                (x - cut - _terrainOrigin.x) / _terrainSize.x, (z - cut - _terrainOrigin.z) / _terrainSize.z,
                (x + cut - _terrainOrigin.x) / _terrainSize.x, (z + cut - _terrainOrigin.z) / _terrainSize.z));
        }

        private Mesh BuildMesh()
        {
            var xz = new float[2 * _layout.VertexCount];
            var indices = new int[3 * _layout.TriangleCount];
            _layout.Write(xz, indices);
            var vertices = new Vector3[_layout.VertexCount];
            var normals = new Vector3[_layout.VertexCount];
            for (int v = 0; v < vertices.Length; v++)
            {
                vertices[v] = new Vector3(xz[2 * v], 0f, xz[2 * v + 1]);
                normals[v] = Vector3.up;
            }

            var mesh = new Mesh { name = "Snow skin", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.SetIndices(indices, MeshTopology.Triangles, 0);

            // The shader lifts every vertex onto the terrain, so the bounds take the terrain's whole height.
            TerrainData data = terrain.terrainData;
            float bottom = terrain.transform.position.y, height = data.size.y;
            float side = 2f * _layout.HalfSize;
            mesh.bounds = new Bounds(new Vector3(0f, bottom + 0.5f * height, 0f), new Vector3(side, height + 2f, side));
            return mesh;
        }

        // The terrain's heights (world metres) and normals as textures, built once, and its splat maps, set as globals.
        private void BuildTerrainTextures()
        {
            TerrainData data = terrain.terrainData;
            int res = data.heightmapResolution;
            _terrainOrigin = terrain.transform.position;
            _terrainSize = data.size;
            float[,] samples = data.GetHeights(0, 0, res, res);
            var heights = new float[res * res];
            for (int row = 0; row < res; row++)
            {
                for (int col = 0; col < res; col++)
                {
                    heights[row * res + col] = _terrainOrigin.y + samples[row, col] * _terrainSize.y;
                }
            }

            float stepX = _terrainSize.x / (res - 1), stepZ = _terrainSize.z / (res - 1);
            var normals = new Color32[res * res];
            for (int row = 0; row < res; row++)
            {
                int down = Mathf.Max(0, row - 1), up = Mathf.Min(res - 1, row + 1);
                for (int col = 0; col < res; col++)
                {
                    int left = Mathf.Max(0, col - 1), right = Mathf.Min(res - 1, col + 1);
                    float slopeX = (heights[row * res + right] - heights[row * res + left]) / ((right - left) * stepX);
                    float slopeZ = (heights[up * res + col] - heights[down * res + col]) / ((up - down) * stepZ);
                    Vector3 n = new Vector3(-slopeX, 1f, -slopeZ).normalized;
                    normals[row * res + col] = new Color32(Encode(n.x), Encode(n.y), Encode(n.z), 255);
                }
            }

            _heights = new Texture2D(res, res, TextureFormat.RFloat, false, true)
            {
                name = "Snow skin heights", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
            };
            _heights.SetPixelData(heights, 0);
            _heights.Apply(false, true);
            _normals = new Texture2D(res, res, TextureFormat.RGBA32, false, true)
            {
                name = "Snow skin normals", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
            };
            _normals.SetPixels32(normals);
            _normals.Apply(false, true);

            Texture2D[] controls = data.alphamapTextures;
            Shader.SetGlobalTexture(HeightsId, _heights);
            Shader.SetGlobalTexture(NormalsId, _normals);
            Shader.SetGlobalTexture(Control0Id, controls.Length > 0 ? controls[0] : Texture2D.blackTexture);
            Shader.SetGlobalTexture(Control1Id, controls.Length > 1 ? controls[1] : Texture2D.blackTexture);
            Shader.SetGlobalVector(TerrainId, new Vector4(_terrainOrigin.x, _terrainOrigin.z, _terrainSize.x, _terrainSize.z));
            Shader.SetGlobalVector(HeightmapId, new Vector4(res, data.alphamapResolution, 0f, 0f));
            Shader.SetGlobalVector(SkinEdgeId, new Vector4(overlapDrop, 0f, 0f, 0f));
        }

        // The terrain's splat weights as the GPU reads them (8 bits a layer), kept for DepthAt: a byte a layer a texel,
        // rows along Z, as TerrainData.GetAlphamaps orders them.
        private void KeepSplatWeights(TerrainData data)
        {
            int texels = data.alphamapResolution, layers = data.alphamapLayers;
            if (texels < 1 || layers < 1)
            {
                return;
            }

            var weights = new byte[texels * texels * layers];
            for (int first = 0; first < texels; first += SplatRowsPerRead)
            {
                int rows = Mathf.Min(SplatRowsPerRead, texels - first);
                float[,,] block = data.GetAlphamaps(0, first, texels, rows);
                for (int row = 0; row < rows; row++)
                {
                    for (int column = 0; column < texels; column++)
                    {
                        int at = ((first + row) * texels + column) * layers;
                        for (int layer = 0; layer < layers; layer++)
                        {
                            weights[at + layer] = (byte)Mathf.Clamp(Mathf.RoundToInt(block[row, column, layer] * FullWeight), 0, byte.MaxValue);
                        }
                    }
                }
            }

            _splat = weights;
            _splatTexels = texels;
            _splatLayers = layers;
        }

        private static byte Encode(float component) => (byte)Mathf.Clamp(Mathf.RoundToInt((component * 0.5f + 0.5f) * 255f), 0, 255);
    }
}
