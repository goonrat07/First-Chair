using UnityEngine;

namespace FirstChair.Riding
{
    /// <summary>
    /// The follow camera's tuning, as data. The defaults are the FIRST-CHAIR.md estimate of Grand Mountain Adventure 2's
    /// view: azimuth 30°, elevation 45°, a 35 mm lens, about 18 m from the rider (which puts it about 12.7 m above).
    /// </summary>
    [CreateAssetMenu(menuName = "First Chair/Follow Camera Settings", fileName = "FollowCameraSettings")]
    public sealed class FollowCameraSettings : ScriptableObject
    {
        [Header("Placement")]
        [Tooltip("Swing around the rider, degrees. 0 = straight up-slope (-Z) looking down the fall line.")]
        [SerializeField] private float azimuthDegrees = 30f;
        [Tooltip("Angle above the horizon, degrees. Height above the aim point = distance x sin(elevation).")]
        [SerializeField, Range(5f, 89f)] private float elevationDegrees = 45f;
        [Tooltip("Distance from the aim point, metres.")]
        [SerializeField, Min(1f)] private float distance = 18f;
        [Tooltip("Aim point height above the rider's feet, metres.")]
        [SerializeField] private float aimHeight = 1f;
        [Tooltip("Aim this far ahead of the rider along their heading, metres, to frame the slope ahead.")]
        [SerializeField, Min(0f)] private float lookAhead = 4f;

        [Header("Lens")]
        [SerializeField, Min(1f)] private float focalLengthMm = 35f;
        [Tooltip("24 mm is full-frame height.")]
        [SerializeField, Min(1f)] private float sensorHeightMm = 24f;
        [SerializeField, Min(0.01f)] private float nearClip = 0.3f;
        [SerializeField, Min(10f)] private float farClip = 1500f;

        [Header("Smoothing (seconds; 0 snaps)")]
        [SerializeField, Min(0f)] private float positionSmoothTime = 0.15f;
        [SerializeField, Min(0f)] private float lookAheadSmoothTime = 0.4f;
        [Tooltip("A jump of the aim point larger than this, metres (a reset), snaps instead of smoothing.")]
        [SerializeField, Min(0f)] private float snapDistance = 20f;

        /// <summary>Azimuth, degrees.</summary>
        public float AzimuthDegrees => azimuthDegrees;
        /// <summary>Elevation, degrees.</summary>
        public float ElevationDegrees => elevationDegrees;
        /// <summary>Distance, metres.</summary>
        public float Distance => distance;
        /// <summary>Camera height above the aim point, metres (derived).</summary>
        public float Height => distance * Mathf.Sin(elevationDegrees * Mathf.Deg2Rad);
        /// <summary>Aim height, metres.</summary>
        public float AimHeight => aimHeight;
        /// <summary>Look-ahead, metres.</summary>
        public float LookAhead => lookAhead;
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
        /// <summary>Look-ahead direction smoothing time, seconds.</summary>
        public float LookAheadSmoothTime => lookAheadSmoothTime;
        /// <summary>Snap distance, metres.</summary>
        public float SnapDistance => snapDistance;
    }
}
