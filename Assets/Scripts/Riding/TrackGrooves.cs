using UnityEngine;
using UnityEngine.Rendering;

namespace FirstChair.Riding
{
    /// <summary>
    /// Stamps the ski tracks into a map of where the skis went (Story 017; Story 018's input). Every frame, each track
    /// ribbon (<see cref="GrooveSource"/>) over a square window of snow around what it follows (<see cref="TrackGrooveWindow"/>)
    /// is drawn from above into a render texture by the stamp shader (<see cref="StampShaderName"/>), which turns its
    /// across-track coordinate into a rounded profile, the deeper kept where two cross; then the map is blurred, one axis
    /// at a time (<see cref="BlurShaderName"/>). The snow mesh near the rider (<see cref="SnowSkin"/>) pushes the snow down
    /// by the map and up where the blurred map reaches past it, as deep as <see cref="LightingSettings"/> sets for each
    /// snow type. Runs after the rider and the camera move. Display only: the sim never reads it. Single-thread only
    /// (Unity's main thread).
    /// </summary>
    /// <remarks>
    /// The maps are cleared and stamped whole every frame, so they hold no history and a camera cut needs nothing. Cost:
    /// three R8 render textures (4 MB each at 2,048 texels), a clear, a draw per ribbon mesh over the window and two blur
    /// passes of seven taps; no allocation a frame. Without its shaders, or where they can't run, the component turns
    /// itself off and the snow has no tracks cut into it.
    /// </remarks>
    /// <example>
    /// <code>
    /// // In the scene builder, once:
    /// var grooves = camera.gameObject.AddComponent&lt;TrackGrooves&gt;();
    /// Set(grooves, "stampShader", Shader.Find(TrackGrooves.StampShaderName));
    /// Set(grooves, "blurShader", Shader.Find(TrackGrooves.BlurShaderName));
    /// Set(grooves, "follow", rider.transform);
    /// // Each ribbon's object, after its renderer:
    /// stretch.AddComponent&lt;GrooveSource&gt;();
    /// </code>
    /// </example>
    [DefaultExecutionOrder(200)] // after the skier view paints the tracks (50) and the follow camera moves (100)
    public sealed class TrackGrooves : MonoBehaviour
    {
        /// <summary>The stamp shader's name, for <see cref="Shader.Find"/>.</summary>
        public const string StampShaderName = "Hidden/First Chair/Track Groove Stamp";

        /// <summary>The blur shader's name, for <see cref="Shader.Find"/>.</summary>
        public const string BlurShaderName = "Hidden/First Chair/Track Groove Blur";

        // Seven taps a blur pass: the centre and three each side (SH_Env_TrackGrooveBlur.shader).
        private const int BlurSideTaps = 3;

        // A Gaussian is near nothing two widths out, so the three side taps span two widths.
        private const float BlurWidthsSpanned = 2f;

        private static readonly int GroovesId = Shader.PropertyToID("_FCTrackGrooves");
        private static readonly int GroovesBlurId = Shader.PropertyToID("_FCTrackGroovesBlur");
        private static readonly int WindowId = Shader.PropertyToID("_FCTrackGrooveWindow");
        private static readonly int StampMatrixId = Shader.PropertyToID("_FCGrooveStampMatrix");
        private static readonly int BlurSourceId = Shader.PropertyToID("_FCGrooveBlurSource");
        private static readonly int BlurTexelId = Shader.PropertyToID("_FCGrooveBlurTexel");
        private static readonly int BlurStepId = Shader.PropertyToID("_FCGrooveBlurStep");
        private static readonly int BlurWeightsId = Shader.PropertyToID("_FCGrooveBlurWeights");

        [Tooltip("Hidden/First Chair/Track Groove Stamp.")]
        [SerializeField] private Shader stampShader;
        [Tooltip("Hidden/First Chair/Track Groove Blur.")]
        [SerializeField] private Shader blurShader;
        [Tooltip("What the window centres on: the rider, for the snow mesh around it. Empty: this object (the camera).")]
        [SerializeField] private Transform follow;
        [Tooltip("The side of the square of snow the tracks are stamped into, metres. It must cover the snow mesh (SnowSkin).")]
        [SerializeField, Range(8f, 256f)] private float windowMetres = 32f;
        [Tooltip("Texels a side of the map. 2,048 over 32 m is 1.6 cm a texel: about six across a 9 cm track.")]
        [SerializeField, Range(256, 4096)] private int texels = 2048;
        [Tooltip("How far the window's centre sits ahead of what it follows, along its view, as a fraction of its side. 0 "
            + "centres it, as the snow mesh around the rider needs.")]
        [SerializeField, Range(0f, 0.5f)] private float ahead = 0f;
        [Tooltip("How far the snow a ski pushes aside reaches past the track's edge, metres: the blur's width.")]
        [SerializeField, Range(0.01f, 0.3f)] private float rimMetres = 0.05f;

        private readonly float[] _weights = new float[BlurSideTaps + 1];
        private TrackGrooveWindow _window;
        private RenderTexture _target;
        private RenderTexture _blurTemp;
        private RenderTexture _blur;
        private Material _stamp;
        private Material _blurMaterial;
        private Mesh _quad;
        private CommandBuffer _commands;

        private void OnEnable()
        {
            if (stampShader == null || !stampShader.isSupported || blurShader == null || !blurShader.isSupported)
            {
                Debug.LogWarning("TrackGrooves: no supported " + StampShaderName + " or " + BlurShaderName + "; the tracks stay ribbons only.");
                enabled = false;
                return;
            }

            _stamp = new Material(stampShader) { name = "Track groove stamp", hideFlags = HideFlags.HideAndDontSave };
            _blurMaterial = new Material(blurShader) { name = "Track groove blur", hideFlags = HideFlags.HideAndDontSave };
            _quad = ClipSpaceQuad();
            _commands = new CommandBuffer { name = "Track grooves" };
            Rebuild();
        }

        private void OnDisable()
        {
            // The snow mesh reads a window side of 0 as nothing stamped.
            Shader.SetGlobalVector(WindowId, Vector4.zero);
            ReleaseTargets();
            DestroyOwned(ref _stamp);
            DestroyOwned(ref _blurMaterial);
            if (_quad != null)
            {
                Destroy(_quad);
                _quad = null;
            }

            _commands?.Release();
            _commands = null;
        }

        private void LateUpdate()
        {
            if (_window.Size != windowMetres || _window.Texels != texels || _window.Ahead != ahead)
            {
                Rebuild();
            }

            if (!_target.IsCreated() || !_blurTemp.IsCreated() || !_blur.IsCreated())
            {
                Rebuild();
            }

            Transform view = follow != null ? follow : transform;
            Vector3 position = view.position, forward = view.forward;
            _window.Follow(position.x, position.z, forward.x, forward.z);
            float minX = _window.OriginX, minZ = _window.OriginZ, size = _window.Size;
            _stamp.SetMatrix(StampMatrixId, GL.GetGPUProjectionMatrix(WorldToWindow(minX, minZ, size), true));

            _commands.Clear();
            _commands.SetRenderTarget(_target);
            _commands.ClearRenderTarget(false, true, Color.clear);
            var sources = GrooveSource.Active;
            for (int i = 0; i < sources.Count; i++)
            {
                Renderer ribbon = sources[i].Renderer;
                if (ribbon == null || !ribbon.enabled)
                {
                    continue;
                }

                // Only ribbons over the window; an emptied stretch has no extent and is skipped.
                Bounds bounds = ribbon.bounds;
                Vector3 min = bounds.min, max = bounds.max;
                bool empty = bounds.extents.x <= 0f && bounds.extents.z <= 0f;
                if (empty || max.x < minX || min.x > minX + size || max.z < minZ || min.z > minZ + size)
                {
                    continue;
                }

                _commands.DrawRenderer(ribbon, _stamp, 0, 0);
            }

            Blur(_target, _blurTemp, 1f, 0f);
            Blur(_blurTemp, _blur, 0f, 1f);
            Graphics.ExecuteCommandBuffer(_commands);
            Shader.SetGlobalVector(WindowId, new Vector4(minX, minZ, size, _window.TexelSize));
        }

        // One blur pass along (alongX, alongZ) in the map's texels, recorded into the command buffer. Taps step more than
        // a texel apart when the rim is wider than three texels' worth of Gaussian, read between texels by the filter.
        private void Blur(RenderTexture source, RenderTexture destination, float alongX, float alongZ)
        {
            float sigmaTexels = rimMetres / _window.TexelSize / BlurWidthsSpanned;
            float stepTexels = Mathf.Max(1f, sigmaTexels * BlurWidthsSpanned / BlurSideTaps);
            SnowTrench.GaussianWeights(sigmaTexels / stepTexels, _weights);
            float texel = 1f / texels;
            _commands.SetRenderTarget(destination);
            _commands.SetGlobalTexture(BlurSourceId, source);
            _commands.SetGlobalVector(BlurTexelId, new Vector4(texel, texel, texels, texels));
            _commands.SetGlobalVector(BlurStepId, new Vector4(alongX * stepTexels * texel, alongZ * stepTexels * texel, 0f, 0f));
            _commands.SetGlobalVector(BlurWeightsId, new Vector4(_weights[0], _weights[1], _weights[2], _weights[3]));
            _commands.DrawMesh(_quad, Matrix4x4.identity, _blurMaterial, 0, 0);
        }

        // World space to the window's clip space in OpenGL's convention: x from the window's least X (-1) to its greatest
        // (1), y the same along Z, so the texture's v runs with Z; depth 0 everywhere, since the stamp keeps the deepest
        // groove by blending, not by depth. GL.GetGPUProjectionMatrix then adapts it to the platform.
        private static Matrix4x4 WorldToWindow(float minX, float minZ, float size)
        {
            float scale = 2f / size;
            var m = Matrix4x4.zero;
            m.m00 = scale;
            m.m03 = -1f - scale * minX;
            m.m12 = scale;
            m.m13 = -1f - scale * minZ;
            m.m33 = 1f;
            return m;
        }

        // A quad covering clip space; the blur shader passes its positions straight through.
        private static Mesh ClipSpaceQuad()
        {
            var quad = new Mesh { name = "Track groove blur quad", hideFlags = HideFlags.HideAndDontSave };
            quad.vertices = new[] { new Vector3(-1f, -1f, 0f), new Vector3(-1f, 1f, 0f), new Vector3(1f, 1f, 0f), new Vector3(1f, -1f, 0f) };
            quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            quad.bounds = new Bounds(Vector3.zero, new Vector3(2f, 2f, 1f));
            return quad;
        }

        private void Rebuild()
        {
            _window = new TrackGrooveWindow(windowMetres, texels, ahead);
            ReleaseTargets();
            RenderTextureFormat format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R8)
                ? RenderTextureFormat.R8
                : RenderTextureFormat.ARGB32;
            _target = NewTarget("Track grooves", format);
            _blurTemp = NewTarget("Track grooves blur (X)", format);
            _blur = NewTarget("Track grooves blur", format);
            Shader.SetGlobalTexture(GroovesId, _target);
            Shader.SetGlobalTexture(GroovesBlurId, _blur);
        }

        private RenderTexture NewTarget(string targetName, RenderTextureFormat format)
        {
            var target = new RenderTexture(texels, texels, 0, format, RenderTextureReadWrite.Linear)
            {
                name = targetName,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false,
                hideFlags = HideFlags.HideAndDontSave,
            };
            target.Create();
            return target;
        }

        private void ReleaseTargets()
        {
            ReleaseTarget(ref _target);
            ReleaseTarget(ref _blurTemp);
            ReleaseTarget(ref _blur);
        }

        private static void ReleaseTarget(ref RenderTexture target)
        {
            if (target == null)
            {
                return;
            }

            target.Release();
            Destroy(target);
            target = null;
        }

        private static void DestroyOwned(ref Material material)
        {
            if (material != null)
            {
                Destroy(material);
                material = null;
            }
        }
    }
}
