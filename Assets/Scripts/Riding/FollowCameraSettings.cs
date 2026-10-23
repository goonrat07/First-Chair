using UnityEngine;

namespace FirstChair.Riding
{
    /// <summary>
    /// The follow camera's tuning, as data. The defaults match Grand Mountain Adventure 2's gameplay view as measured
    /// from the user's screenshots (<c>art/reference/style-notes-gma2.md</c>, "Gameplay camera"): straight behind the
    /// rider, uphill, looking down the line of travel at about 40°, far enough that the rider is about 5 percent of the
    /// screen height, centred left to right and a little below the middle.
    /// </summary>
    [CreateAssetMenu(menuName = "First Chair/Follow Camera Settings", fileName = "FollowCameraSettings")]
    public sealed class FollowCameraSettings : ScriptableObject
    {
        [Header("Placement")]
        [Tooltip("Angle below the horizon the camera looks down, degrees.")]
        [SerializeField, Range(5f, 89f)] private float pitchDegrees = 40f;
        [Tooltip("Distance from the aim point, metres. 50 m with the 35 mm lens makes the rider about 5% of the screen height.")]
        [SerializeField, Min(1f)] private float distance = 50f;
        [Tooltip("Aim point height above the rider's feet, metres.")]
        [SerializeField] private float aimHeight = 1f;

        [Header("Direction")]
        [Tooltip("0 looks straight down the fall line; 1 looks along the rider's travel. GMA2 sits behind the rider, so "
            + "mostly travel, with some fall line so S-turns don't swing the view.")]
        [SerializeField, Range(0f, 1f)] private float travelWeight = 0.6f;
        [Tooltip("Below this speed, metres per second, the camera looks down the fall line only.")]
        [SerializeField, Min(0f)] private float minTravelSpeed = 2f;
        [Tooltip("How slowly the view direction follows, seconds.")]
        [SerializeField, Min(0f)] private float yawSmoothTime = 1.2f;

        [Header("Look-ahead (puts the rider below the middle of the screen)")]
        [Tooltip("Aim this far ahead of the rider along the view direction when stopped, metres.")]
        [SerializeField, Min(0f)] private float lookAheadBase = 3f;
        [Tooltip("Extra look-ahead per metre per second of speed, seconds.")]
        [SerializeField, Min(0f)] private float lookAheadPerSpeed = 0.2f;
        [Tooltip("Most look-ahead, metres.")]
        [SerializeField, Min(0f)] private float lookAheadMax = 10f;

        [Header("Lens")]
        [SerializeField, Min(1f)] private float focalLengthMm = 35f;
        [Tooltip("24 mm is full-frame height.")]
        [SerializeField, Min(1f)] private float sensorHeightMm = 24f;
        [SerializeField, Min(0.01f)] private float nearClip = 1f;
        [SerializeField, Min(10f)] private float farClip = 2000f;

        [Header("Smoothing (seconds; 0 snaps)")]
        [SerializeField, Min(0f)] private float positionSmoothTime = 0.15f;
        [Tooltip("A jump of the aim point larger than this, metres (a reset), snaps instead of smoothing.")]
        [SerializeField, Min(0f)] private float snapDistance = 30f;

        /// <summary>Pitch below the horizon, degrees.</summary>
        public float PitchDegrees => pitchDegrees;
        /// <summary>Distance, metres.</summary>
        public float Distance => distance;
        /// <summary>Aim height, metres.</summary>
        public float AimHeight => aimHeight;
        /// <summary>Weight of the travel direction against the fall line, 0 to 1.</summary>
        public float TravelWeight => travelWeight;
        /// <summary>Speed below which only the fall line counts, metres per second.</summary>
        public float MinTravelSpeed => minTravelSpeed;
        /// <summary>View direction smoothing time, seconds.</summary>
        public float YawSmoothTime => yawSmoothTime;
        /// <summary>Look-ahead when stopped, metres.</summary>
        public float LookAheadBase => lookAheadBase;
        /// <summary>Look-ahead per unit of speed, seconds.</summary>
        public float LookAheadPerSpeed => lookAheadPerSpeed;
        /// <summary>Most look-ahead, metres.</summary>
        public float LookAheadMax => lookAheadMax;
        /// <summary>Focal length, millimetres.</summary>
        public float FocalLengthMm => focalLengthMm;
        /// <summary>Sensor height, millimetres.</summary>
        public float SensorHeightMm => sensorHeightMm;
        /// <summary>Near clip plane, metres.</summary>
        public float NearClip => nearClip;
        /// <summary>Far clip plane, metres.</summary>
        public float FarClip => farClip;
        /// <summary>Aim smoothing time, seconds.</summary>
        public float PositionSmoothTime => positionSmoothTime;
        /// <summary>Snap distance, metres.</summary>
        public float SnapDistance => snapDistance;
    }
}
