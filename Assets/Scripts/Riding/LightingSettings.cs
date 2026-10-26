using UnityEngine;

namespace FirstChair.Riding
{
    /// <summary>
    /// The scene's light, as data (story 007's lighting pass): the sun's height, direction, strength and warmth, the
    /// shadows, the ambient light, and the camera's exposure, contrast and saturation. <see cref="LightingRig"/> applies
    /// them every frame, so the Feel Lab's Light tab changes them live. The defaults come from the pass (2026-10-26): a
    /// warm sun behind and to the right of a camera looking down the fall line, 35 degrees up, so steeper snow faces
    /// further from it and reads darker, with the shadows reaching ahead and to the left. Measured on the test slope's
    /// rollers from one held view, the rows' brightness range rose from 12 to 26 levels of 255 with the snow as bright
    /// (203 against 207); the earlier sun, ahead and to the right, lit steep and flat snow nearly alike.
    /// </summary>
    [CreateAssetMenu(menuName = "First Chair/Lighting Settings", fileName = "LightingSettings")]
    public sealed class LightingSettings : ScriptableObject
    {
        [Header("Sun")]
        [Tooltip("How high the sun stands above the horizon, degrees. Lower makes longer shadows and more shading on slopes.")]
        [SerializeField, Range(3f, 85f)] private float sunElevationDegrees = 35f;
        [Tooltip("Which way the sunlight travels, degrees clockwise from +Z (down the fall line) seen from above. -30 "
            + "comes from behind and to the right of a camera looking down the fall line, so steeper snow reads darker; "
            + "-122 comes from ahead and to the right, lighting steep and flat snow nearly alike.")]
        [SerializeField, Range(-180f, 180f)] private float sunHeadingDegrees = -30f;
        [Tooltip("The sun's intensity.")]
        [SerializeField, Range(0f, 5f)] private float sunIntensity = 2.2f;
        [Tooltip("0 is white sunlight, 1 a warm late-afternoon sun.")]
        [SerializeField, Range(0f, 1f)] private float sunWarmth = 0.5f;
        [Tooltip("How dark the shadows are, 0 to 1.")]
        [SerializeField, Range(0f, 1f)] private float shadowStrength = 1f;

        [Header("Sky light")]
        [Tooltip("The blue sky light's strength: how much light reaches the shadows and the faces turned from the sun.")]
        [SerializeField, Range(0f, 2f)] private float ambientIntensity = 0.8f;

        [Header("Camera grade")]
        [Tooltip("Exposure, in stops. Lower keeps lit snow out of the tonemapper's shoulder, where its shading flattens.")]
        [SerializeField, Range(-3f, 3f)] private float postExposure = 0.3f;
        [Tooltip("Contrast, -100 to 100.")]
        [SerializeField, Range(-100f, 100f)] private float contrast = 10f;
        [Tooltip("Saturation, -100 to 100.")]
        [SerializeField, Range(-100f, 100f)] private float saturation = 0f;

        /// <summary>Sun elevation, degrees.</summary>
        public float SunElevationDegrees => sunElevationDegrees;

        /// <summary>Sun heading, degrees.</summary>
        public float SunHeadingDegrees => sunHeadingDegrees;

        /// <summary>Sun intensity.</summary>
        public float SunIntensity => sunIntensity;

        /// <summary>Sun warmth, 0 to 1.</summary>
        public float SunWarmth => sunWarmth;

        /// <summary>Shadow strength, 0 to 1.</summary>
        public float ShadowStrength => shadowStrength;

        /// <summary>Ambient light multiplier.</summary>
        public float AmbientIntensity => ambientIntensity;

        /// <summary>Post exposure, stops.</summary>
        public float PostExposure => postExposure;

        /// <summary>Contrast.</summary>
        public float Contrast => contrast;

        /// <summary>Saturation.</summary>
        public float Saturation => saturation;
    }
}
