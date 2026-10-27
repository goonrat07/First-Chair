using FirstChair.Sim;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FirstChair.Riding
{
    /// <summary>
    /// Applies <see cref="LightingSettings"/> to the scene every frame (story 007's lighting pass): the sun's direction,
    /// intensity, colour and shadows, the ambient light scaled from the colours the scene was built with, a colour
    /// adjustment on the global post-processing volume, and the snow terrain shader's globals (story 014,
    /// <c>Assets/Shaders/SnowTerrain/SH_Env_SnowEffects.hlsl</c>). Lives on the sun. Presentation only.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public sealed class LightingRig : MonoBehaviour
    {
        private static readonly Color WarmSun = new Color(1f, 0.88f, 0.72f);

        // The snow terrain shader's globals (SH_Env_SnowEffects.hlsl names and documents each one).
        private static readonly int SnowGlintId = Shader.PropertyToID("_FCSnowGlint");
        private static readonly int SnowGlintShapeId = Shader.PropertyToID("_FCSnowGlintShape");
        private static readonly int SnowGlintLayers0Id = Shader.PropertyToID("_FCSnowGlintLayers0");
        private static readonly int SnowGlintLayers1Id = Shader.PropertyToID("_FCSnowGlintLayers1");
        private static readonly int SnowShadeId = Shader.PropertyToID("_FCSnowShade");
        private static readonly int SnowShadeTintId = Shader.PropertyToID("_FCSnowShadeTint");
        private static readonly int SnowWrapId = Shader.PropertyToID("_FCSnowWrap");
        private static readonly int SnowRippleId = Shader.PropertyToID("_FCSnowRipple");
        private static readonly int SnowRippleLayers0Id = Shader.PropertyToID("_FCSnowRippleLayers0");
        private static readonly int SnowRippleLayers1Id = Shader.PropertyToID("_FCSnowRippleLayers1");

        // Per terrain layer, 8 for the two passes. Terrain layer i is SnowType i (TestSlopeLook.SnowLayers).
        private readonly float[] _glintByLayer = new float[8];
        private readonly float[] _rippleByLayer = new float[8];

        [SerializeField] private LightingSettings settings;

        private Light _sun;
        private Color _sky, _equator, _ground;
        private ColorAdjustments _grade;

        private void Awake()
        {
            _sun = GetComponent<Light>();
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<LightingSettings>();
            }

            _sky = RenderSettings.ambientSkyColor;
            _equator = RenderSettings.ambientEquatorColor;
            _ground = RenderSettings.ambientGroundColor;
            Volume volume = FindAnyObjectByType<Volume>();
            if (volume != null)
            {
                VolumeProfile profile = volume.profile;
                if (!profile.TryGet(out _grade))
                {
                    _grade = profile.Add<ColorAdjustments>(true);
                }
            }
        }

        private void LateUpdate()
        {
            transform.rotation = Quaternion.Euler(settings.SunElevationDegrees, settings.SunHeadingDegrees, 0f);
            _sun.intensity = settings.SunIntensity;
            _sun.color = Color.Lerp(Color.white, WarmSun, settings.SunWarmth);
            _sun.shadowStrength = settings.ShadowStrength;
            float ambient = settings.AmbientIntensity;
            RenderSettings.ambientSkyColor = _sky * ambient;
            RenderSettings.ambientEquatorColor = _equator * ambient;
            RenderSettings.ambientGroundColor = _ground * ambient;
            if (_grade != null)
            {
                _grade.postExposure.Override(settings.PostExposure);
                _grade.contrast.Override(settings.Contrast);
                _grade.saturation.Override(settings.Saturation);
            }

            PushSnow();
        }

        // The snow terrain's globals. The terrain draws layers 0 to 3 in its first pass and 4 to 7 in its add pass, so
        // each per-layer amount goes out as two vectors. Ripples are the powder's alone.
        private void PushSnow()
        {
            for (int i = 0; i < _glintByLayer.Length; i++)
            {
                _glintByLayer[i] = i < SnowSurface.LayerCount ? settings.SnowGlintDensity((SnowType)i) : 0f;
                _rippleByLayer[i] = i == (int)SnowType.Powder ? 1f : 0f;
            }

            Shader.SetGlobalVector(SnowGlintId, new Vector4(
                settings.SnowGlintStrength, settings.SnowGlintCellMetres, settings.SnowGlintSharpness, settings.SnowGlintFadeMetres));
            Shader.SetGlobalVector(SnowGlintShapeId, new Vector4(settings.SnowGlintFacetSpread, settings.SnowGlintMinCellPixels, 0f, 0f));
            Shader.SetGlobalVector(SnowGlintLayers0Id, Layers(_glintByLayer, 0));
            Shader.SetGlobalVector(SnowGlintLayers1Id, Layers(_glintByLayer, 4));
            Shader.SetGlobalVector(SnowShadeId, new Vector4(settings.SnowShadeStrength, 0f, 0f, 0f));
            Color tint = settings.SnowShadeTint;
            Shader.SetGlobalVector(SnowShadeTintId, new Vector4(tint.r, tint.g, tint.b, 1f));
            Shader.SetGlobalVector(SnowWrapId, new Vector4(settings.SnowWrap, 0f, 0f, 0f));
            Shader.SetGlobalVector(SnowRippleId, new Vector4(settings.SnowRippleStrength, settings.SnowRippleWavelength,
                settings.SnowRippleWindHeading * Mathf.Deg2Rad, settings.SnowRippleFadeMetres));
            Shader.SetGlobalVector(SnowRippleLayers0Id, Layers(_rippleByLayer, 0));
            Shader.SetGlobalVector(SnowRippleLayers1Id, Layers(_rippleByLayer, 4));
        }

        private static Vector4 Layers(float[] byLayer, int first) =>
            new Vector4(byLayer[first], byLayer[first + 1], byLayer[first + 2], byLayer[first + 3]);
    }
}
