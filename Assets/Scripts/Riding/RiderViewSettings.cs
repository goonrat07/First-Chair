using UnityEngine;

namespace FirstChair.Riding
{
    /// <summary>Proportions, attachments, pose easing and ski tracks for the skier views. Presentation only.</summary>
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

        [Header("Ski tracks")]
        [Tooltip("Width of each ski's track, metres.")]
        [SerializeField, Min(0.01f)] private float trackWidth = 0.09f;
        [Tooltip("How long a track stays on the snow, seconds.")]
        [SerializeField, Min(0f)] private float trackLifetime = 45f;
        [Tooltip("Shortest step between track points, metres.")]
        [SerializeField, Min(0.01f)] private float trackMinVertexDistance = 0.3f;
        [Tooltip("Lift above the ski base, metres, so the track doesn't flicker into the snow.")]
        [SerializeField, Min(0f)] private float trackLift = 0.015f;

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
        /// <summary>Track width, metres.</summary>
        public float TrackWidth => trackWidth;
        /// <summary>Track lifetime, seconds.</summary>
        public float TrackLifetime => trackLifetime;
        /// <summary>Shortest step between track points, metres.</summary>
        public float TrackMinVertexDistance => trackMinVertexDistance;
        /// <summary>Track lift above the ski base, metres.</summary>
        public float TrackLift => trackLift;
    }
}
