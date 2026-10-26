using UnityEngine;

namespace FirstChair.Riding
{
    /// <summary>Proportions, attachments, pose easing, ski tracks and snow spray for the skier views. Presentation only.</summary>
    [CreateAssetMenu(menuName = "First Chair/Rider View Settings", fileName = "RiderViewSettings")]
    public sealed class RiderViewSettings : ScriptableObject
    {
        [Header("Body (metres, from the snow contact point)")]
        [SerializeField] private float stanceWidth = 0.3f;
        [SerializeField] private float hipHeight = 0.9f;
        [SerializeField] private float shoulderHeight = 1.45f;
        [SerializeField] private float headHeight = 1.68f;
        [SerializeField] private float handSpread = 0.34f;
        [SerializeField] private float handHeight = 1.0f;
        [SerializeField] private float handForward = 0.3f;
        [SerializeField] private Color jacketColor = new Color(0.85f, 0.2f, 0.15f);
        [SerializeField] private Color pantsColor = new Color(0.15f, 0.18f, 0.25f);

        [Header("Motion")]
        [Tooltip("Tip of each pole trails behind the hand by this many degrees.")]
        [SerializeField] private float poleTiltDegrees = 20f;
        [Tooltip("Scales the lean taken from the sim's edge angle. A carving skier's body leans less than the skis, which "
            + "angulate at the knees and hips; at 1 the full 60 degree edge reads as a fall from the follow camera.")]
        [SerializeField] private float leanScale = 0.5f;
        [Tooltip("0 keeps the body upright; 1 tilts it fully to the snow's normal.")]
        [SerializeField, Range(0f, 1f)] private float slopeAlign = 1f;
        [Tooltip("A flip in the air turns the rider about a point this high above the feet, metres: about the middle of "
            + "the body, so the rider turns over instead of swinging about the boots.")]
        [SerializeField, Min(0f)] private float flipPivotHeight = 0.9f;

        [Header("Sockets (names and fallbacks from each asset-manifest.json)")]
        [SerializeField] private string skiSocket = "SOCKET_boot";
        [Tooltip("PR_Ski manifest: Blender (0, 0, 0.045), Unity (0, 0.045, 0).")]
        [SerializeField] private Vector3 skiSocketFallback = new Vector3(0f, 0.045f, 0f);
        [SerializeField] private string poleSocket = "SOCKET_grip";
        [Tooltip("PR_SkiPole manifest: Blender (0, 0, 1.1), Unity (0, 1.1, 0).")]
        [SerializeField] private Vector3 poleSocketFallback = new Vector3(0f, 1.1f, 0f);

        [Header("Skier model (CH_Skier.fbx)")]
        [Tooltip("Socket name prefixes on CH_Skier for the left ski, right ski, left hand and right hand.")]
        [SerializeField] private string skierSkiSocketLeft = "SOCKET_ski_L";
        [SerializeField] private string skierSkiSocketRight = "SOCKET_ski_R";
        [SerializeField] private string skierHandSocketLeft = "SOCKET_hand_L";
        [SerializeField] private string skierHandSocketRight = "SOCKET_hand_R";
        [Tooltip("How long the skier takes to ease between poses, seconds.")]
        [SerializeField, Min(0f)] private float poseSmoothTime = 0.12f;
        [Tooltip("Lift of CH_Skier's root above the snow, metres: its root is at the boot soles, and PR_Ski's base is "
            + "0.045 m below the boot socket (CH_Skier asset-manifest.json).")]
        [SerializeField] private float skierRootLift = 0.045f;

        [Header("Ski tracks")]
        [Tooltip("Width of each ski's track, metres.")]
        [SerializeField, Min(0.01f)] private float trackWidth = 0.09f;
        [Tooltip("How long a track stays on the snow, seconds.")]
        [SerializeField, Min(0f)] private float trackLifetime = 120f;
        [Tooltip("Shortest step between track points, metres.")]
        [SerializeField, Min(0.01f)] private float trackMinVertexDistance = 0.3f;
        [Tooltip("Lift above the ski base, metres, so the track doesn't flicker into the snow.")]
        [SerializeField, Min(0f)] private float trackLift = 0.015f;

        [Header("Snow spray (Story 008)")]
        [Tooltip("Flakes per metre the skis slide sideways (quick turns and skids; a clean carve slides none).")]
        [SerializeField, Min(0f)] private float sprayPerSlideMetre = 80f;
        [Tooltip("Flakes per metre travelled while braking.")]
        [SerializeField, Min(0f)] private float sprayPerBrakeMetre = 12f;
        [Tooltip("Flakes per metre a carve at full edge throws (Story 009); softer edges throw fewer, down to none at the carve spray edge.")]
        [SerializeField, Min(0f)] private float sprayPerCarveMetre = 4f;
        [Tooltip("Edge fraction, 0 to 1, below which a carve throws no snow.")]
        [SerializeField, Range(0f, 0.99f)] private float sprayCarveEdge = 0.6f;
        [Tooltip("Slowest carve that throws snow, m/s.")]
        [SerializeField, Min(0f)] private float sprayCarveMinSpeed = 10f;
        [Tooltip("The most flakes per second.")]
        [SerializeField, Min(0f)] private float sprayMaxRate = 300f;
        [Tooltip("Powder throws this much more snow.")]
        [SerializeField, Min(0f)] private float sprayPowderFactor = 1.6f;
        [Tooltip("Ice throws this much: a few chips.")]
        [SerializeField, Min(0f)] private float sprayIceFactor = 0.3f;
        [Tooltip("A flake's lifetime, seconds (each varies by 30% either way).")]
        [SerializeField, Min(0.05f)] private float sprayLifetime = 0.7f;
        [Tooltip("A flake's size, metres (each varies by 40% either way).")]
        [SerializeField, Min(0.01f)] private float spraySize = 0.3f;
        [Tooltip("Flakes take a colour between this one, snow in shade, and the next. Unlit white alone reads as the sunlit "
            + "snow behind it once tonemapped (build 40117971068 showed no spray), so the shade is what makes the spray show.")]
        [SerializeField] private Color sprayShadeColor = new Color(0.60f, 0.68f, 0.84f);
        [Tooltip("The bright end of the flakes' colours, sunlit snow.")]
        [SerializeField] private Color sprayLightColor = new Color(0.95f, 0.97f, 1f);
        [Tooltip("Speed the flakes are thrown at the way the skis slide, metres per second (0.5 to 1.5 times this).")]
        [SerializeField, Min(0f)] private float sprayThrow = 2.5f;
        [Tooltip("Speed the flakes are thrown up at, metres per second (0.5 to 1.5 times this).")]
        [SerializeField, Min(0f)] private float sprayLift = 1.8f;
        [Tooltip("Share of the rider's own velocity the flakes keep.")]
        [SerializeField, Range(0f, 1f)] private float sprayCarry = 0.6f;
        [Tooltip("Gravity on the flakes, times Unity's.")]
        [SerializeField, Min(0f)] private float sprayGravity = 0.9f;
        [Tooltip("Flakes start up to this far behind the boots, along the skis, metres.")]
        [SerializeField, Min(0f)] private float sprayBehind = 0.6f;
        [Tooltip("Flakes start across this width, metres.")]
        [SerializeField, Min(0f)] private float sprayWidth = 0.5f;
        [Tooltip("Flakes start this far above the snow, metres.")]
        [SerializeField, Min(0f)] private float sprayRise = 0.05f;
        [Tooltip("The most flakes alive at once.")]
        [SerializeField, Min(1)] private int sprayMaxParticles = 600;

        /// <summary>Distance between the two skis' centres.</summary>
        public float StanceWidth => stanceWidth;
        /// <summary>Hip height above the snow.</summary>
        public float HipHeight => hipHeight;
        /// <summary>Shoulder height above the snow.</summary>
        public float ShoulderHeight => shoulderHeight;
        /// <summary>Head centre height above the snow.</summary>
        public float HeadHeight => headHeight;
        /// <summary>Each hand's distance from the centre line.</summary>
        public float HandSpread => handSpread;
        /// <summary>Hand height above the snow.</summary>
        public float HandHeight => handHeight;
        /// <summary>Hands ahead of the hips.</summary>
        public float HandForward => handForward;
        /// <summary>Jacket colour.</summary>
        public Color JacketColor => jacketColor;
        /// <summary>Trouser colour.</summary>
        public Color PantsColor => pantsColor;
        /// <summary>Pole tilt, degrees.</summary>
        public float PoleTiltDegrees => poleTiltDegrees;
        /// <summary>Lean scale.</summary>
        public float LeanScale => leanScale;
        /// <summary>Slope alignment, 0 to 1.</summary>
        public float SlopeAlign => slopeAlign;
        /// <summary>Height of the flip's pivot above the feet, metres.</summary>
        public float FlipPivotHeight => flipPivotHeight;
        /// <summary>The ski's boot socket name.</summary>
        public string SkiSocket => skiSocket;
        /// <summary>The boot socket in the ski's model space if the FBX has no socket node.</summary>
        public Vector3 SkiSocketFallback => skiSocketFallback;
        /// <summary>The pole's grip socket name.</summary>
        public string PoleSocket => poleSocket;
        /// <summary>The grip socket in the pole's model space if the FBX has no socket node.</summary>
        public Vector3 PoleSocketFallback => poleSocketFallback;
        /// <summary>CH_Skier's left ski socket.</summary>
        public string SkierSkiSocketLeft => skierSkiSocketLeft;
        /// <summary>CH_Skier's right ski socket.</summary>
        public string SkierSkiSocketRight => skierSkiSocketRight;
        /// <summary>CH_Skier's left hand socket.</summary>
        public string SkierHandSocketLeft => skierHandSocketLeft;
        /// <summary>CH_Skier's right hand socket.</summary>
        public string SkierHandSocketRight => skierHandSocketRight;
        /// <summary>Pose easing time, seconds.</summary>
        public float PoseSmoothTime => poseSmoothTime;
        /// <summary>CH_Skier's root height above the snow, metres.</summary>
        public float SkierRootLift => skierRootLift;
        /// <summary>Track width, metres.</summary>
        public float TrackWidth => trackWidth;
        /// <summary>Track lifetime, seconds.</summary>
        public float TrackLifetime => trackLifetime;
        /// <summary>Shortest step between track points, metres.</summary>
        public float TrackMinVertexDistance => trackMinVertexDistance;
        /// <summary>Track lift above the ski base, metres.</summary>
        public float TrackLift => trackLift;
        /// <summary>Spray flakes per metre of sideways slide.</summary>
        public float SprayPerSlideMetre => sprayPerSlideMetre;
        /// <summary>Spray flakes per metre travelled while braking.</summary>
        public float SprayPerBrakeMetre => sprayPerBrakeMetre;
        /// <summary>The most spray flakes per second.</summary>
        public float SprayMaxRate => sprayMaxRate;
        /// <summary>Flakes per metre a carve at full edge throws.</summary>
        public float SprayPerCarveMetre => sprayPerCarveMetre;
        /// <summary>Edge fraction below which a carve throws no snow.</summary>
        public float SprayCarveEdge => sprayCarveEdge;
        /// <summary>Slowest carve that throws snow, m/s.</summary>
        public float SprayCarveMinSpeed => sprayCarveMinSpeed;
        /// <summary>Powder's spray factor.</summary>
        public float SprayPowderFactor => sprayPowderFactor;
        /// <summary>Ice's spray factor.</summary>
        public float SprayIceFactor => sprayIceFactor;
        /// <summary>A flake's lifetime, seconds.</summary>
        public float SprayLifetime => sprayLifetime;
        /// <summary>A flake's size, metres.</summary>
        public float SpraySize => spraySize;
        /// <summary>The shaded end of the flakes' colours.</summary>
        public Color SprayShadeColor => sprayShadeColor;
        /// <summary>The bright end of the flakes' colours.</summary>
        public Color SprayLightColor => sprayLightColor;
        /// <summary>Sideways throw speed, m/s.</summary>
        public float SprayThrow => sprayThrow;
        /// <summary>Upward throw speed, m/s.</summary>
        public float SprayLift => sprayLift;
        /// <summary>Share of the rider's velocity the flakes keep.</summary>
        public float SprayCarry => sprayCarry;
        /// <summary>Gravity on the flakes, times Unity's.</summary>
        public float SprayGravity => sprayGravity;
        /// <summary>How far behind the boots flakes start, metres.</summary>
        public float SprayBehind => sprayBehind;
        /// <summary>Width flakes start across, metres.</summary>
        public float SprayWidth => sprayWidth;
        /// <summary>Height above the snow flakes start at, metres.</summary>
        public float SprayRise => sprayRise;
        /// <summary>The most flakes alive at once.</summary>
        public int SprayMaxParticles => sprayMaxParticles;
    }
}
