using FirstChair.Sim;
using UnityEngine;

namespace FirstChair.Riding
{
    /// <summary>
    /// The scene's light, as data (story 007's lighting pass): the sun's height, direction, strength and warmth, the
    /// shadows, the ambient light, the snow shader's glints, shade, soft light and wind ripples (story 014), and the
    /// camera's exposure, contrast and saturation. <see cref="LightingRig"/> applies them every frame, so the Feel Lab's
    /// Light tab changes them live. The light's defaults come from the pass (2026-10-26): a warm sun behind and to the
    /// right of a camera looking down the fall line, 35 degrees up, so steeper snow faces further from it and reads
    /// darker, with the shadows reaching ahead and to the left. Measured on the test slope's rollers from one held view,
    /// the rows' brightness range rose from 12 to 26 levels of 255 with the snow as bright (203 against 207); the earlier
    /// sun, ahead and to the right, lit steep and flat snow nearly alike. The snow's defaults are story 014's first
    /// pass, to be tuned against the user's look.
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

        [Header("Snow (story 014: First Chair/Snow Terrain)")]
        [Tooltip("How bright the sun's glints off snow crystals are; 0 turns them off.")]
        [SerializeField, Range(0f, 20f)] private float snowGlintStrength = 6f;
        [Tooltip("The size of the cells that each hold at most one crystal near the camera, metres. Farther away the cells "
            + "double in size as often as it takes to stay a few pixels wide.")]
        [SerializeField, Range(0.005f, 0.1f)] private float snowGlintCellMetres = 0.02f;
        [Tooltip("The fewest pixels a glint cell spans. A 2 cm cell is under a pixel from the chase camera, so cells grow "
            + "with distance to keep to this.")]
        [SerializeField, Range(1f, 8f)] private float snowGlintMinCellPixels = 2.5f;
        [Tooltip("How exactly a crystal must mirror the sun into the camera to flash; higher makes fewer, briefer glints.")]
        [SerializeField, Range(10f, 5000f)] private float snowGlintSharpness = 150f;
        [Tooltip("How far a crystal's facet can lean off the snow's surface: 0.6 is about 30 degrees, which only sparkles "
            + "toward the sun; 1.5 reaches most directions, so some crystals flash wherever the sun and camera are.")]
        [SerializeField, Range(0f, 3f)] private float snowGlintFacetSpread = 1.5f;
        [Tooltip("Glints fade out by this distance from the camera, metres.")]
        [SerializeField, Range(1f, 200f)] private float snowGlintFadeMetres = 40f;
        [Tooltip("How many cells of groomed snow hold a crystal, 0 to 1.")]
        [SerializeField, Range(0f, 1f)] private float snowGlintGroomed = 0.6f;
        [Tooltip("How many cells of powder hold a crystal, 0 to 1.")]
        [SerializeField, Range(0f, 1f)] private float snowGlintPowder = 1f;
        [Tooltip("How many cells of crud hold a crystal, 0 to 1.")]
        [SerializeField, Range(0f, 1f)] private float snowGlintCrud = 0.35f;
        [Tooltip("How many cells of ice hold a crystal, 0 to 1. Ice is glossy instead.")]
        [SerializeField, Range(0f, 1f)] private float snowGlintIce = 0f;
        [Tooltip("How many cells of slush hold a crystal, 0 to 1.")]
        [SerializeField, Range(0f, 1f)] private float snowGlintSlush = 0.15f;
        [Tooltip("How far snow out of the sun (in a shadow or turned from it) takes on the shade tint, 0 to 1; 0 turns it off.")]
        [SerializeField, Range(0f, 1f)] private float snowShadeStrength = 0.6f;
        [Tooltip("What shaded snow's colour is multiplied toward, as linear values: the sky's blue.")]
        [SerializeField] private Color snowShadeTint = new Color(0.80f, 0.89f, 1.06f, 1f);
        [Tooltip("How far the sunlit side of the snow wraps past its edge, softening the light over rollers, 0 to 1; 0 is "
            + "URP's hard edge. It also lifts snow turned partly from the sun and flattens the corduroy by 1 / (1 + this).")]
        [SerializeField, Range(0f, 1f)] private float snowWrap = 0.15f;
        [Tooltip("How steep the wind ripples in powder are, as their steepest slope; 0 turns them off.")]
        [SerializeField, Range(0f, 1f)] private float snowRippleStrength = 0.25f;
        [Tooltip("The wind ripples' wavelength, metres.")]
        [SerializeField, Range(0.1f, 3f)] private float snowRippleWavelength = 0.5f;
        [Tooltip("Which way the wind blew, degrees clockwise from +Z (down the fall line) seen from above; the ripples' "
            + "crests run across it.")]
        [SerializeField, Range(-180f, 180f)] private float snowRippleWindHeading = 70f;
        [Tooltip("Wind ripples fade out by this distance from the camera, metres.")]
        [SerializeField, Range(1f, 200f)] private float snowRippleFadeMetres = 30f;

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

        /// <summary>Snow glint brightness; 0 is off.</summary>
        public float SnowGlintStrength => snowGlintStrength;

        /// <summary>The glint cell size, metres.</summary>
        public float SnowGlintCellMetres => snowGlintCellMetres;

        /// <summary>The fewest pixels a glint cell spans; cells double in size with distance to keep to it.</summary>
        public float SnowGlintMinCellPixels => snowGlintMinCellPixels;

        /// <summary>The glint's mirror sharpness (the lobe's exponent).</summary>
        public float SnowGlintSharpness => snowGlintSharpness;

        /// <summary>How far a crystal's facet leans off the snow, as an offset added to the normal (0.6 is about 30 degrees).</summary>
        public float SnowGlintFacetSpread => snowGlintFacetSpread;

        /// <summary>The distance glints fade out by, metres.</summary>
        public float SnowGlintFadeMetres => snowGlintFadeMetres;

        /// <summary>Snow shade tint strength, 0 to 1; 0 is off.</summary>
        public float SnowShadeStrength => snowShadeStrength;

        /// <summary>The linear colour shaded snow is multiplied toward.</summary>
        public Color SnowShadeTint => snowShadeTint;

        /// <summary>How far the lit side of the snow wraps past its edge, 0 to 1; 0 is off.</summary>
        public float SnowWrap => snowWrap;

        /// <summary>The powder ripples' steepest slope; 0 is off.</summary>
        public float SnowRippleStrength => snowRippleStrength;

        /// <summary>The ripples' wavelength, metres.</summary>
        public float SnowRippleWavelength => snowRippleWavelength;

        /// <summary>The wind's heading, degrees clockwise from +Z.</summary>
        public float SnowRippleWindHeading => snowRippleWindHeading;

        /// <summary>The distance ripples fade out by, metres.</summary>
        public float SnowRippleFadeMetres => snowRippleFadeMetres;

        /// <summary>How many glint cells of a kind of snow hold a crystal, 0 to 1.</summary>
        /// <param name="kind">The kind of snow.</param>
        public float SnowGlintDensity(SnowType kind)
        {
            switch (kind)
            {
                case SnowType.Groomed: return snowGlintGroomed;
                case SnowType.Powder: return snowGlintPowder;
                case SnowType.Crud: return snowGlintCrud;
                case SnowType.Ice: return snowGlintIce;
                case SnowType.Slush: return snowGlintSlush;
                default: return 0f;
            }
        }

        /// <summary>Post exposure, stops.</summary>
        public float PostExposure => postExposure;

        /// <summary>Contrast.</summary>
        public float Contrast => contrast;

        /// <summary>Saturation.</summary>
        public float Saturation => saturation;
    }
}
