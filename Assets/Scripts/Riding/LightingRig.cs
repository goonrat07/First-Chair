using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FirstChair.Riding
{
    /// <summary>
    /// Applies <see cref="LightingSettings"/> to the scene every frame (story 007's lighting pass): the sun's direction,
    /// intensity, colour and shadows, the ambient light scaled from the colours the scene was built with, and a colour
    /// adjustment on the global post-processing volume. Lives on the sun. Presentation only.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public sealed class LightingRig : MonoBehaviour
    {
        private static readonly Color WarmSun = new Color(1f, 0.88f, 0.72f);

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
        }
    }
}
