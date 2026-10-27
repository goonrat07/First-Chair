using UnityEngine;
using UnityEngine.Rendering;

namespace FirstChair.Riding
{
    /// <summary>
    /// Stamps the ski tracks into the snow as grooves (Story 017). Every frame, each track ribbon (<see cref="GrooveSource"/>)
    /// over a square window of snow ahead of the camera (<see cref="TrackGrooveWindow"/>) is drawn from above into a render
    /// texture by the stamp shader (<see cref="StampShaderName"/>), which turns its across-track coordinate into a rounded
    /// groove, the deeper kept where two cross. First Chair/Snow Terrain reads the texture as relief (SnowGrooves in
    /// <c>Assets/Shaders/SnowTerrain/SH_Env_SnowEffects.hlsl</c>), as deep as <see cref="LightingSettings"/> sets for each
    /// snow type. Lives on the camera and runs after it moves. Display only: the sim never reads it. Single-thread only
    /// (Unity's main thread).
    /// </summary>
    /// <remarks>
    /// The texture is cleared and stamped whole every frame, so it holds no history and a camera cut needs nothing. Cost:
    /// one R8 render texture (4 MB at 2,048 texels), a clear and a draw per ribbon mesh over the window; no allocation a
    /// frame. Without its shader, or where the shader can't run, the component turns itself off and the snow has no grooves.
    /// </remarks>
    /// <example>
    /// <code>
    /// // In the scene builder, once:
    /// var grooves = camera.gameObject.AddComponent&lt;TrackGrooves&gt;();
    /// Set(grooves, "stampShader", Shader.Find(TrackGrooves.StampShaderName));
    /// // Each ribbon's object, after its renderer:
    /// stretch.AddComponent&lt;GrooveSource&gt;();
    /// </code>
    /// </example>
    [DefaultExecutionOrder(200)] // after the skier view paints the tracks (50) and the follow camera moves (100)
    public sealed class TrackGrooves : MonoBehaviour
    {
        /// <summary>The stamp shader's name, for <see cref="Shader.Find"/>.</summary>
        public const string StampShaderName = "Hidden/First Chair/Track Groove Stamp";

        private static readonly int GroovesId = Shader.PropertyToID("_FCTrackGrooves");
        private static readonly int WindowId = Shader.PropertyToID("_FCTrackGrooveWindow");
        private static readonly int StampMatrixId = Shader.PropertyToID("_FCGrooveStampMatrix");

        [Tooltip("Hidden/First Chair/Track Groove Stamp.")]
        [SerializeField] private Shader stampShader;
        [Tooltip("The side of the square of snow the grooves are stamped into, metres. Larger reaches farther but is coarser.")]
        [SerializeField, Range(8f, 256f)] private float windowMetres = 32f;
        [Tooltip("Texels a side of the groove texture. 2,048 over 32 m is 1.6 cm a texel: about six across a 9 cm track, "
            + "and close to a pixel's size on the snow beside the rider from the chase camera.")]
        [SerializeField, Range(256, 4096)] private int texels = 2048;
        [Tooltip("How far the window's centre sits ahead of the camera, as a fraction of its side: 0.375 keeps an eighth "
            + "of it behind the camera, which the chase camera never sees, and the rest ahead.")]
        [SerializeField, Range(0f, 0.5f)] private float ahead = 0.375f;

        private TrackGrooveWindow _window;
        private RenderTexture _target;
        private Material _stamp;
        private CommandBuffer _commands;

        private void OnEnable()
        {
            if (stampShader == null || !stampShader.isSupported)
            {
                Debug.LogWarning("TrackGrooves: no supported " + StampShaderName + "; the tracks stay ribbons only.");
                enabled = false;
                return;
            }

            _stamp = new Material(stampShader) { name = "Track groove stamp", hideFlags = HideFlags.HideAndDontSave };
            _commands = new CommandBuffer { name = "Track grooves" };
            Rebuild();
        }

        private void OnDisable()
        {
            // The terrain reads a window side of 0 as no grooves.
            Shader.SetGlobalVector(WindowId, Vector4.zero);
            ReleaseTarget();
            if (_stamp != null)
            {
                Destroy(_stamp);
                _stamp = null;
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

            if (!_target.IsCreated())
            {
                _target.Create();
                Shader.SetGlobalTexture(GroovesId, _target);
            }

            Transform view = transform;
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

            Graphics.ExecuteCommandBuffer(_commands);
            Shader.SetGlobalVector(WindowId, new Vector4(minX, minZ, size, _window.TexelSize));
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

        private void Rebuild()
        {
            _window = new TrackGrooveWindow(windowMetres, texels, ahead);
            ReleaseTarget();
            RenderTextureFormat format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R8)
                ? RenderTextureFormat.R8
                : RenderTextureFormat.ARGB32;
            _target = new RenderTexture(texels, texels, 0, format, RenderTextureReadWrite.Linear)
            {
                name = "Track grooves",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false,
                hideFlags = HideFlags.HideAndDontSave,
            };
            _target.Create();
            Shader.SetGlobalTexture(GroovesId, _target);
        }

        private void ReleaseTarget()
        {
            if (_target == null)
            {
                return;
            }

            _target.Release();
            Destroy(_target);
            _target = null;
        }
    }
}
