using FirstChair.Sim;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FirstChair.Riding
{
    /// <summary>
    /// A Steep-style chase camera (Story 011): behind the rider along the travel, at a height above the snow under it,
    /// looking at a point just ahead of the rider, so the rider sits in the lower middle of the screen and the view
    /// follows the slope by itself. It tilts further down where the snow ahead drops away more steeply than under the
    /// rider, closes in with speed and comes in close, lower and a little over the rider's right shoulder in a fast
    /// tuck, widens, banks, shakes gently and blurs at the screen's edges with speed, and rises to keep itself and its
    /// line to the rider above the snow. Nearly stopped, it sits behind the skis; on the chairlift it follows the chair.
    /// Presentation only: it reads the rider view's transform and the sim's state, and writes nothing back.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(Camera))]
    public sealed class FollowCamera : MonoBehaviour
    {
        // Where along the camera's line to the aim (0 the aim, 1 the camera) it checks the snow between.
        private static readonly float[] SightlineSamples = { 0.35f, 0.65f };

        // The camera is never closer than this to the snow under it, metres, whatever the smoothing.
        private const float HardClearance = 0.3f;

        // The blur's strengths are set as at 60 frames a second; other frame rates scale to streaks as long.
        private const double BlurReferenceFrameSeconds = 1.0 / 60;

        [SerializeField] private Transform target;
        [Tooltip("Gives the rider's velocity, stance and heading, and the snow. Without it the camera follows the target down +Z.")]
        [SerializeField] private SimDriver driver;
        [SerializeField] private FollowCameraSettings settings;

        private Camera _camera;
        private MotionBlur _blur;
        private Vector3 _offset;
        private Vector3 _lastRider;
        private bool _hasPosition;
        private float _yaw;
        private float _tuck;
        private float _rise;
        private float _tilt;
        private float _fov = -1f;
        private float _roll;
        private float _travelYaw;
        private bool _hasTravelYaw;
        private float _shakeTime;

        /// <summary>
        /// While set, the camera follows this instead of the sim's rider: the chair the rider sits on (Story 010), looking
        /// down the fall line at it.
        /// </summary>
        public Transform RideTarget { get; set; }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            if (settings == null)
            {
                Debug.LogWarning("FollowCamera: no settings asset; using the defaults.", this);
                settings = ScriptableObject.CreateInstance<FollowCameraSettings>();
            }

            // The edge blur at speed: URP's motion blur on the global volume, from the camera's motion and the objects', so
            // the rider, moving with the camera, stays sharp while the snow streaks past.
            Volume volume = FindAnyObjectByType<Volume>();
            if (volume != null)
            {
                VolumeProfile profile = volume.profile;
                if (!profile.TryGet(out _blur))
                {
                    _blur = profile.Add<MotionBlur>(true);
                }

                _blur.mode.Override(MotionBlurMode.CameraAndObjects);
                _blur.quality.Override(MotionBlurQuality.Medium);
                _blur.intensity.Override(0f);
            }
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            _camera.nearClipPlane = settings.NearClip;
            _camera.farClipPlane = settings.FarClip;

            float dt = Time.deltaTime;
            RideSession session = driver != null ? driver.Session : null;
            bool onLift = RideTarget != null;
            Vector3 rider = target.position;
            Vector3 velocity = Vector3.zero;
            bool tucked = false;
            if (onLift)
            {
                rider = RideTarget.position;
            }
            else if (session != null)
            {
                // The rider's feet from the sim, not the view's root, which a flip swings about the middle of the body.
                RiderPose pose = driver.Pose;
                rider = SimCoordinatesUnity.Position(pose);
                velocity = new Vector3(pose.VelocityX, 0f, pose.VelocityZ);
                tucked = session.Current.Stance == Stance.Tuck;
            }

            Vector3 normal = SnowAt(session, rider, out float snowUnderRider);
            bool snap = !_hasPosition || (session != null && session.SnappedThisFrame)
                || (rider - _lastRider).sqrMagnitude > settings.SnapDistance * settings.SnapDistance;
            _lastRider = rider;
            float speed = velocity.magnitude;

            // Speed effects (Stories 009, 011): the view widens and the screen's edges blur with speed, the camera banks into
            // turns, and a fast rider shakes it gently.
            float lens = (float)FollowCameraMath.VerticalFovDegrees(settings.FocalLengthMm, settings.SensorHeightMm);
            float fov = (float)FollowCameraMath.FovAtSpeed(lens, settings.FovPerSpeed, settings.FovSpeedMax, speed);
            _fov = snap || _fov < 0f ? fov : Mathf.Lerp(_fov, fov, FollowCameraMath.SmoothingFactor(settings.FovSmoothTime, dt));
            _camera.fieldOfView = _fov;
            if (_blur != null)
            {
                double frameSeconds = driver != null ? driver.ViewDeltaSeconds : dt;
                _blur.intensity.Override((float)FollowCameraMath.MotionBlurIntensity(
                    speed, settings.BlurFromSpeed, settings.BlurFullSpeed, settings.BlurMost, frameSeconds, BlurReferenceFrameSeconds));
            }

            float turnAcceleration = 0f;
            if (speed >= settings.MinTravelSpeed && dt > 0f)
            {
                float travelYaw = Mathf.Atan2(velocity.x, velocity.z);
                if (_hasTravelYaw && !snap)
                {
                    turnAcceleration = speed * Mathf.DeltaAngle(_travelYaw * Mathf.Rad2Deg, travelYaw * Mathf.Rad2Deg) * Mathf.Deg2Rad / dt;
                }

                _travelYaw = travelYaw;
                _hasTravelYaw = true;
            }

            double gravity = session != null ? SimCoordinates.ToDouble(session.Tuning.Gravity) : 9.81;
            float roll = (float)FollowCameraMath.RollDegrees(turnAcceleration, gravity, settings.RollPerG, settings.RollMax);
            _roll = snap ? 0f : Mathf.Lerp(_roll, roll, FollowCameraMath.SmoothingFactor(settings.RollSmoothTime, dt));
            _shakeTime += dt;
            float shake = (float)FollowCameraMath.ShakeAmount(speed, settings.ShakeFromSpeed, settings.ShakeFullSpeed) * settings.ShakeDegrees;
            float phase = 2f * Mathf.PI * settings.ShakeFrequency * _shakeTime;
            float shakePitch = shake * (Mathf.Sin(phase) + 0.5f * Mathf.Sin(2.3f * phase + 1f));
            float shakeYaw = shake * 0.7f * (Mathf.Sin(1.7f * phase + 2f) + 0.5f * Mathf.Sin(3.1f * phase));

            // Direction: along the travel; nearly stopped, behind the skis; on the lift, down the fall line at the chair.
            float desiredYaw = _yaw;
            if (!onLift && session != null && speed < settings.MinTravelSpeed)
            {
                desiredYaw = SimCoordinates.ToFloat(session.Current.Heading) * Mathf.Rad2Deg;
            }
            else if (FollowCameraMath.ViewDirection(
                normal.x, normal.z, velocity.x, velocity.z, settings.TravelWeight, settings.MinTravelSpeed, out double dx, out double dz))
            {
                desiredYaw = (float)FollowCameraMath.YawDegrees(dx, dz);
            }

            _yaw = snap ? desiredYaw : Mathf.LerpAngle(_yaw, desiredYaw, FollowCameraMath.SmoothingFactor(settings.YawSmoothTime, dt));
            var direction = new Vector3(Mathf.Sin(_yaw * Mathf.Deg2Rad), 0f, Mathf.Cos(_yaw * Mathf.Deg2Rad));

            // Closer and lower the faster, more so in a fast tuck.
            _tuck = snap ? (tucked ? 1f : 0f) : Mathf.Lerp(_tuck, tucked ? 1f : 0f, FollowCameraMath.SmoothingFactor(settings.TuckSmoothTime, dt));
            float back = (float)FollowCameraMath.ChaseValue(
                settings.BackDistance, settings.FastBackDistance, settings.TuckBackDistance, speed, settings.CloseInSpeed, _tuck);
            float height = (float)FollowCameraMath.ChaseValue(
                settings.Height, settings.FastHeight, settings.TuckHeight, speed, settings.CloseInSpeed, _tuck);
            float aimHeight = (float)FollowCameraMath.ChaseValue(
                settings.AimHeight, settings.AimHeight, settings.TuckAimHeight, speed, settings.CloseInSpeed, _tuck);
            float lookAhead = (float)FollowCameraMath.LookAhead(settings.LookAheadBase, settings.LookAheadPerSpeed, settings.LookAheadMax, speed);

            // Over the shoulder: closing in, the camera and its aim slide a little to the rider's right, so it still looks
            // along the travel and the rider sits a little left of the screen's middle.
            float sideMetres = (float)FollowCameraMath.ChaseValue(
                0.0, settings.FastSideOffset, settings.TuckSideOffset, speed, settings.CloseInSpeed, _tuck);
            FollowCameraMath.SideOffset(direction.x, direction.z, sideMetres, out double sideX, out double sideZ);
            var side = new Vector3((float)sideX, 0f, (float)sideZ);

            // Behind the rider along the travel, at its height above the snow there; in the air it rises with the rider.
            // The offset from the rider is what smooths, so the camera keeps up however fast the rider goes.
            Vector3 behind = rider - direction * back + side;
            SnowAt(session, behind, out float snowBehind, snowUnderRider);
            var offset = new Vector3(behind.x - rider.x, snowBehind - snowUnderRider + height, behind.z - rider.z);
            _offset = snap ? offset : Vector3.Lerp(_offset, offset, FollowCameraMath.SmoothingFactor(settings.PositionSmoothTime, dt));
            Vector3 position = rider + _offset;
            Vector3 aim = rider + Vector3.up * aimHeight + direction * lookAhead + side;

            // Never under the snow: rise until the camera clears the snow under it and its line to the aim the snow between.
            float rise = 0f;
            float floor = 0f;
            if (session != null)
            {
                rise = RiseToClear(session, aim, position, 1f, settings.CameraClearance);
                floor = RiseToClear(session, aim, position, 1f, HardClearance);
                foreach (float fraction in SightlineSamples)
                {
                    rise = Mathf.Max(rise, RiseToClear(session, aim, position, fraction, settings.SightlineClearance));
                }
            }

            float riseTime = rise > _rise ? settings.RiseSmoothTime : settings.SettleSmoothTime;
            _rise = snap ? rise : Mathf.Lerp(_rise, rise, FollowCameraMath.SmoothingFactor(riseTime, dt));
            position.y += Mathf.Max(_rise, floor);

            // Look at the aim, tilted further down where the snow ahead drops away more steeply than here.
            Vector3 toAim = aim - position;
            float yaw = Mathf.Atan2(toAim.x, toAim.z) * Mathf.Rad2Deg;
            float lookPitch = Mathf.Atan2(-toAim.y, new Vector2(toAim.x, toAim.z).magnitude) * Mathf.Rad2Deg;
            float tilt = 0f;
            if (session != null && !onLift)
            {
                SnowAt(session, rider + direction * settings.AheadNearMetres, out float snowNear, snowUnderRider);
                SnowAt(session, rider + direction * settings.AheadFarMetres, out float snowFar, snowUnderRider);
                double dropAhead = FollowCameraMath.DropAheadDegrees(
                    snowUnderRider, settings.AheadNearMetres, snowNear, settings.AheadFarMetres, snowFar);
                double slopeHere = FollowCameraMath.SlopeDownAlongDegrees(normal.x, normal.y, normal.z, direction.x, direction.z);
                tilt = (float)FollowCameraMath.RolloverTiltDegrees(dropAhead, slopeHere, settings.RolloverTiltFactor, settings.RolloverTiltMax);
            }

            _tilt = snap ? tilt : Mathf.Lerp(_tilt, tilt, FollowCameraMath.SmoothingFactor(settings.PitchSmoothTime, dt));
            float pitch = Mathf.Clamp(lookPitch + _tilt, settings.MinPitchDegrees, settings.MaxPitchDegrees);
            transform.SetPositionAndRotation(position, Quaternion.Euler(pitch + shakePitch, yaw + shakeYaw, _roll));
            _hasPosition = true;
        }

        // How far to raise the camera for its line to the aim to clear the snow at one point along it.
        private static float RiseToClear(RideSession session, Vector3 aim, Vector3 camera, float fraction, float margin)
        {
            Vector3 point = Vector3.Lerp(aim, camera, fraction);
            SnowAt(session, point, out float snow, point.y);
            return (float)FollowCameraMath.RiseToClear(aim.y, camera.y, fraction, snow, margin);
        }

        // The snow's height and normal at a point; without a ride, the fallback height and straight up.
        private static Vector3 SnowAt(RideSession session, Vector3 position, out float height, float fallback)
        {
            if (session == null)
            {
                height = fallback;
                return Vector3.up;
            }

            session.Slope.Heightmap.Sample(
                SimCoordinates.FromMetres(position.x), SimCoordinates.FromMetres(position.z), out Fix64 snow, out Vec3 normal);
            height = SimCoordinates.ToFloat(snow);
            return new Vector3(SimCoordinates.ToFloat(normal.X), SimCoordinates.ToFloat(normal.Y), SimCoordinates.ToFloat(normal.Z));
        }

        private static Vector3 SnowAt(RideSession session, Vector3 position, out float height) => SnowAt(session, position, out height, position.y);
    }
}
