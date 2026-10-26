using UnityEngine;

namespace FirstChair.Riding
{
    /// <summary>
    /// The follow camera's tuning, as data: a Steep-style chase camera (Story 011, from the user's Steep screenshots,
    /// 2026-10-26). It sits behind the rider along the travel, at a height above the snow under it, and looks at a point
    /// just ahead of the rider, so the rider sits in the lower middle of the screen and the view follows the slope by
    /// itself; it tilts further down over a rollover, closes in and drops lower with speed and in a fast tuck, widens and
    /// blurs at the edges with speed, and never goes under the snow. Every number is live in the Feel Lab's Camera tab.
    /// </summary>
    [CreateAssetMenu(menuName = "First Chair/Follow Camera Settings", fileName = "FollowCameraSettings")]
    public sealed class FollowCameraSettings : ScriptableObject
    {
        [Header("Placement (behind the rider along the travel)")]
        [Tooltip("How far behind the rider, metres along the ground, standing still.")]
        [SerializeField, Min(0.5f)] private float backDistance = 4.8f;
        [Tooltip("How far behind at full speed (Close-in speed), not tucked, metres.")]
        [SerializeField, Min(0.5f)] private float fastBackDistance = 4.2f;
        [Tooltip("How far behind at full speed in a tuck, metres: a fast tuck pulls the camera in, so it reads faster.")]
        [SerializeField, Min(0.5f)] private float tuckBackDistance = 3.4f;
        [Tooltip("The camera's height above the snow under it, standing still, metres: high enough to look over the rider's "
            + "head, so the slope ahead shows between the rider and the horizon.")]
        [SerializeField, Min(0.1f)] private float height = 2.5f;
        [Tooltip("Its height at full speed, not tucked, metres.")]
        [SerializeField, Min(0.1f)] private float fastHeight = 2.1f;
        [Tooltip("Its height at full speed in a tuck, metres.")]
        [SerializeField, Min(0.1f)] private float tuckHeight = 1.4f;
        [Tooltip("The speed where the full-speed and tuck numbers apply in full, m/s (25 is 90 km/h); between, they ease in.")]
        [SerializeField, Min(0.1f)] private float closeInSpeed = 25f;
        [Tooltip("Aim point height above the rider's feet, metres.")]
        [SerializeField] private float aimHeight = 1.2f;
        [Tooltip("Aim point height above the feet in a fast tuck, metres (the tucked rider is lower).")]
        [SerializeField] private float tuckAimHeight = 0.8f;
        [Tooltip("How quickly the camera moves in and out as the rider tucks and stands, seconds.")]
        [SerializeField, Min(0f)] private float tuckSmoothTime = 0.4f;

        [Header("Direction")]
        [Tooltip("0 looks straight down the fall line; 1 looks along the rider's travel, as a chase camera does.")]
        [SerializeField, Range(0f, 1f)] private float travelWeight = 1f;
        [Tooltip("Below this speed, metres per second, the camera keeps its last direction (or looks down the fall line).")]
        [SerializeField, Min(0f)] private float minTravelSpeed = 1.5f;
        [Tooltip("How slowly the view direction follows the travel, seconds.")]
        [SerializeField, Min(0f)] private float yawSmoothTime = 0.3f;

        [Header("Looking ahead")]
        [Tooltip("Aim this far ahead of the rider along the travel when stopped, metres.")]
        [SerializeField, Min(0f)] private float lookAheadBase = 1.5f;
        [Tooltip("Extra look-ahead per metre per second of speed, seconds.")]
        [SerializeField, Min(0f)] private float lookAheadPerSpeed = 0.05f;
        [Tooltip("Most look-ahead, metres. More pushes a fast rider down the screen.")]
        [SerializeField, Min(0f)] private float lookAheadMax = 3f;
        [Tooltip("The near and far points ahead along the travel where the camera reads the snow, metres: where it drops away "
            + "more steeply than under the rider (a rollover, a steep face), the camera tilts down to show it.")]
        [SerializeField, Min(0.5f)] private float aheadNearMetres = 6f;
        [SerializeField, Min(0.5f)] private float aheadFarMetres = 16f;
        [Tooltip("The share of the extra drop ahead the camera tilts down by, 0 to 1.")]
        [SerializeField, Range(0f, 1f)] private float rolloverTiltFactor = 0.6f;
        [Tooltip("The most it tilts down for the drop ahead, degrees.")]
        [SerializeField, Min(0f)] private float rolloverTiltMax = 15f;
        [Tooltip("Least and most the camera looks down below the horizon, degrees (negative looks up).")]
        [SerializeField, Range(-45f, 89f)] private float minPitchDegrees = -10f;
        [SerializeField, Range(-45f, 89f)] private float maxPitchDegrees = 60f;
        [Tooltip("How slowly the pitch follows, seconds.")]
        [SerializeField, Min(0f)] private float pitchSmoothTime = 0.25f;

        [Header("Clearance (never under the snow)")]
        [Tooltip("The least height above the snow under the camera, metres.")]
        [SerializeField, Min(0f)] private float cameraClearance = 0.8f;
        [Tooltip("The least height of the camera's line to the rider above the snow in between, metres.")]
        [SerializeField, Min(0f)] private float sightlineClearance = 0.4f;
        [Tooltip("How quickly the camera rises to clear the snow, seconds (short, so it never dips into it).")]
        [SerializeField, Min(0f)] private float riseSmoothTime = 0.05f;
        [Tooltip("How slowly it settles back down once clear, seconds.")]
        [SerializeField, Min(0f)] private float settleSmoothTime = 0.5f;

        [Header("Lens")]
        [Tooltip("Focal length, millimetres: 26 on the 24 mm sensor is a 50 degree vertical view (about 80 degrees across "
            + "a 16:9 screen), as in Steep; wider shrinks the rider.")]
        [SerializeField, Min(1f)] private float focalLengthMm = 26f;
        [Tooltip("24 mm is full-frame height.")]
        [SerializeField, Min(1f)] private float sensorHeightMm = 24f;
        [SerializeField, Min(0.01f)] private float nearClip = 0.15f;
        [SerializeField, Min(10f)] private float farClip = 2000f;

        [Header("Speed effects")]
        [Tooltip("Degrees the view widens per m/s of speed: a wider view makes speed read faster.")]
        [SerializeField, Min(0f)] private float fovPerSpeed = 0.45f;
        [Tooltip("The most degrees the view widens.")]
        [SerializeField, Min(0f)] private float fovSpeedMax = 12f;
        [Tooltip("How slowly the view's width follows the speed, seconds.")]
        [SerializeField, Min(0f)] private float fovSmoothTime = 0.5f;
        [Tooltip("Speed where the screen's edges start to blur with the camera's motion, m/s (15 is 54 km/h).")]
        [SerializeField, Min(0f)] private float blurFromSpeed = 15f;
        [Tooltip("Speed of the full blur, m/s.")]
        [SerializeField, Min(0f)] private float blurFullSpeed = 30f;
        [Tooltip("The full blur's strength, 0 to 1 (URP motion blur intensity); 0 turns it off.")]
        [SerializeField, Range(0f, 1f)] private float blurMost = 0.5f;
        [Tooltip("Degrees the camera banks into a turn per g of sideways acceleration.")]
        [SerializeField, Min(0f)] private float rollPerG = 1.5f;
        [Tooltip("The most the camera banks, degrees; 0 turns it off.")]
        [SerializeField, Min(0f)] private float rollMax = 3f;
        [Tooltip("How slowly the bank follows the turn, seconds.")]
        [SerializeField, Min(0f)] private float rollSmoothTime = 0.3f;
        [Tooltip("Speed where a gentle shake starts, m/s.")]
        [SerializeField, Min(0f)] private float shakeFromSpeed = 18f;
        [Tooltip("Speed of the full shake, m/s.")]
        [SerializeField, Min(0f)] private float shakeFullSpeed = 35f;
        [Tooltip("The full shake's size, degrees; 0 turns it off.")]
        [SerializeField, Min(0f)] private float shakeDegrees = 0.15f;
        [Tooltip("The shake's rate, cycles per second.")]
        [SerializeField, Min(0f)] private float shakeFrequency = 7f;

        [Header("Smoothing (seconds; 0 snaps)")]
        [Tooltip("How closely the camera's place follows the rider, seconds.")]
        [SerializeField, Min(0f)] private float positionSmoothTime = 0.08f;
        [Tooltip("A jump of the rider larger than this, metres (a reset), snaps instead of smoothing.")]
        [SerializeField, Min(0f)] private float snapDistance = 30f;

        /// <summary>Distance behind the rider standing still, metres.</summary>
        public float BackDistance => backDistance;
        /// <summary>Distance behind at full speed, not tucked, metres.</summary>
        public float FastBackDistance => fastBackDistance;
        /// <summary>Distance behind at full speed in a tuck, metres.</summary>
        public float TuckBackDistance => tuckBackDistance;
        /// <summary>Height above the snow standing still, metres.</summary>
        public float Height => height;
        /// <summary>Height above the snow at full speed, not tucked, metres.</summary>
        public float FastHeight => fastHeight;
        /// <summary>Height above the snow at full speed in a tuck, metres.</summary>
        public float TuckHeight => tuckHeight;
        /// <summary>The speed where the full-speed and tuck numbers apply in full, m/s.</summary>
        public float CloseInSpeed => closeInSpeed;
        /// <summary>Aim height above the feet, metres.</summary>
        public float AimHeight => aimHeight;
        /// <summary>Aim height above the feet in a fast tuck, metres.</summary>
        public float TuckAimHeight => tuckAimHeight;
        /// <summary>Tuck smoothing time, seconds.</summary>
        public float TuckSmoothTime => tuckSmoothTime;
        /// <summary>Weight of the travel direction against the fall line, 0 to 1.</summary>
        public float TravelWeight => travelWeight;
        /// <summary>Speed below which travel doesn't count, metres per second.</summary>
        public float MinTravelSpeed => minTravelSpeed;
        /// <summary>View direction smoothing time, seconds.</summary>
        public float YawSmoothTime => yawSmoothTime;
        /// <summary>Look-ahead when stopped, metres.</summary>
        public float LookAheadBase => lookAheadBase;
        /// <summary>Look-ahead per unit of speed, seconds.</summary>
        public float LookAheadPerSpeed => lookAheadPerSpeed;
        /// <summary>Most look-ahead, metres.</summary>
        public float LookAheadMax => lookAheadMax;
        /// <summary>The near point ahead where the snow is read, metres.</summary>
        public float AheadNearMetres => aheadNearMetres;
        /// <summary>The far point ahead where the snow is read, metres.</summary>
        public float AheadFarMetres => aheadFarMetres;
        /// <summary>The share of the extra drop ahead the camera tilts by.</summary>
        public float RolloverTiltFactor => rolloverTiltFactor;
        /// <summary>The most tilt for the drop ahead, degrees.</summary>
        public float RolloverTiltMax => rolloverTiltMax;
        /// <summary>Least pitch below the horizon, degrees.</summary>
        public float MinPitchDegrees => minPitchDegrees;
        /// <summary>Most pitch below the horizon, degrees.</summary>
        public float MaxPitchDegrees => maxPitchDegrees;
        /// <summary>Pitch smoothing time, seconds.</summary>
        public float PitchSmoothTime => pitchSmoothTime;
        /// <summary>Least height above the snow under the camera, metres.</summary>
        public float CameraClearance => cameraClearance;
        /// <summary>Least height of the line to the rider above the snow, metres.</summary>
        public float SightlineClearance => sightlineClearance;
        /// <summary>Rise smoothing time, seconds.</summary>
        public float RiseSmoothTime => riseSmoothTime;
        /// <summary>Settle smoothing time, seconds.</summary>
        public float SettleSmoothTime => settleSmoothTime;
        /// <summary>Focal length, millimetres.</summary>
        public float FocalLengthMm => focalLengthMm;
        /// <summary>Sensor height, millimetres.</summary>
        public float SensorHeightMm => sensorHeightMm;
        /// <summary>Near clip plane, metres.</summary>
        public float NearClip => nearClip;
        /// <summary>Far clip plane, metres.</summary>
        public float FarClip => farClip;
        /// <summary>Degrees the view widens per m/s of speed.</summary>
        public float FovPerSpeed => fovPerSpeed;
        /// <summary>The most degrees the view widens.</summary>
        public float FovSpeedMax => fovSpeedMax;
        /// <summary>View-width smoothing time, seconds.</summary>
        public float FovSmoothTime => fovSmoothTime;
        /// <summary>Speed where the edge blur starts, m/s.</summary>
        public float BlurFromSpeed => blurFromSpeed;
        /// <summary>Speed of the full edge blur, m/s.</summary>
        public float BlurFullSpeed => blurFullSpeed;
        /// <summary>The full blur's strength, 0 to 1.</summary>
        public float BlurMost => blurMost;
        /// <summary>Degrees of bank per g of sideways acceleration.</summary>
        public float RollPerG => rollPerG;
        /// <summary>The most bank, degrees.</summary>
        public float RollMax => rollMax;
        /// <summary>Bank smoothing time, seconds.</summary>
        public float RollSmoothTime => rollSmoothTime;
        /// <summary>Speed where the shake starts, m/s.</summary>
        public float ShakeFromSpeed => shakeFromSpeed;
        /// <summary>Speed of the full shake, m/s.</summary>
        public float ShakeFullSpeed => shakeFullSpeed;
        /// <summary>The full shake's size, degrees.</summary>
        public float ShakeDegrees => shakeDegrees;
        /// <summary>The shake's rate, cycles per second.</summary>
        public float ShakeFrequency => shakeFrequency;
        /// <summary>Position smoothing time, seconds.</summary>
        public float PositionSmoothTime => positionSmoothTime;
        /// <summary>Snap distance, metres.</summary>
        public float SnapDistance => snapDistance;
    }
}
