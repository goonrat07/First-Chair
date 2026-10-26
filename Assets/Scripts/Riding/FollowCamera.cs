using FirstChair.Sim;
using UnityEngine;

namespace FirstChair.Riding
{
    /// <summary>
    /// A high, pulled-back follow camera like Grand Mountain Adventure 2's: behind the rider, uphill, looking down the
    /// line of travel a fixed angle steeper than the snow (so steep and gentle slopes frame the same), with the aim point
    /// a little ahead so the rider sits below the middle of the screen, never lower than a set line. The view direction blends the slope's fall line with the rider's travel direction and turns slowly.
    /// Presentation only; it reads the rider view's transform and the sim's state, and writes nothing back.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(Camera))]
    public sealed class FollowCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [Tooltip("Gives the rider's velocity and the snow's fall line. Without it the camera looks down +Z.")]
        [SerializeField] private SimDriver driver;
        [SerializeField] private FollowCameraSettings settings;

        private Camera _camera;
        private Vector3 _aim;
        private Vector3 _viewDirection = Vector3.forward;
        private float _pitch = -1f;
        private bool _hasAim;
        private float _fov = -1f;
        private float _roll;
        private float _travelYaw;
        private bool _hasTravelYaw;
        private float _shakeTime;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            if (settings == null)
            {
                Debug.LogWarning("FollowCamera: no settings asset; using the defaults.", this);
                settings = ScriptableObject.CreateInstance<FollowCameraSettings>();
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
            bool snap = !_hasAim || (driver != null && driver.Session != null && driver.Session.SnappedThisFrame);

            Vector3 normal = Vector3.up;
            Vector3 velocity = Vector3.zero;
            // The rider's feet from the sim, not the view's root, which a flip swings about the middle of the body.
            Vector3 rider = target.position;
            if (driver != null && driver.Session != null)
            {
                RiderPose pose = driver.Pose;
                velocity = new Vector3(pose.VelocityX, 0f, pose.VelocityZ);
                rider = SimCoordinatesUnity.Position(pose);
                normal = SnowNormal(driver.Session, rider);
            }

            // Speed effects (Story 009): the view widens with speed and banks into turns, and a fast rider shakes it gently.
            float speed = velocity.magnitude;
            float lens = (float)FollowCameraMath.VerticalFovDegrees(settings.FocalLengthMm, settings.SensorHeightMm);
            float fov = (float)FollowCameraMath.FovAtSpeed(lens, settings.FovPerSpeed, settings.FovSpeedMax, speed);
            _fov = snap || _fov < 0f ? fov : Mathf.Lerp(_fov, fov, FollowCameraMath.SmoothingFactor(settings.FovSmoothTime, dt));
            _camera.fieldOfView = _fov;
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

            double gravity = driver != null && driver.Session != null ? SimCoordinates.ToDouble(driver.Session.Tuning.Gravity) : 9.81;
            float roll = (float)FollowCameraMath.RollDegrees(turnAcceleration, gravity, settings.RollPerG, settings.RollMax);
            _roll = snap ? 0f : Mathf.Lerp(_roll, roll, FollowCameraMath.SmoothingFactor(settings.RollSmoothTime, dt));
            _shakeTime += dt;
            float shake = (float)FollowCameraMath.ShakeAmount(speed, settings.ShakeFromSpeed, settings.ShakeFullSpeed) * settings.ShakeDegrees;
            float phase = 2f * Mathf.PI * settings.ShakeFrequency * _shakeTime;
            float shakePitch = shake * (Mathf.Sin(phase) + 0.5f * Mathf.Sin(2.3f * phase + 1f));
            float shakeYaw = shake * 0.7f * (Mathf.Sin(1.7f * phase + 2f) + 0.5f * Mathf.Sin(3.1f * phase));

            // Downhill on the ground: the snow normal's horizontal part (the normal leans toward the fall line).
            var fall = new Vector3(normal.x, 0f, normal.z);

            if (FollowCameraMath.ViewDirection(
                fall.x, fall.z, velocity.x, velocity.z, settings.TravelWeight, settings.MinTravelSpeed, out double dx, out double dz))
            {
                var desired = new Vector3((float)dx, 0f, (float)dz);
                _viewDirection = snap
                    ? desired
                    : Vector3.Slerp(_viewDirection, desired, FollowCameraMath.SmoothingFactor(settings.YawSmoothTime, dt)).normalized;
            }

            float lookAhead = (float)FollowCameraMath.LookAhead(
                settings.LookAheadBase, settings.LookAheadPerSpeed, settings.LookAheadMax, velocity.magnitude);
            Vector3 desiredAim = rider + Vector3.up * settings.AimHeight + _viewDirection * lookAhead;
            if (snap || (desiredAim - _aim).sqrMagnitude > settings.SnapDistance * settings.SnapDistance)
            {
                _aim = desiredAim;
                _hasAim = true;
            }
            else
            {
                // Along the view and up, the aim follows smoothly; sideways, the rider has the drift zone's room.
                var right = new Vector3(_viewDirection.z, 0f, -_viewDirection.x);
                Vector3 delta = desiredAim - _aim;
                float sideways = Vector3.Dot(delta, right);
                _aim += (delta - right * sideways) * FollowCameraMath.SmoothingFactor(settings.PositionSmoothTime, dt);
                _aim += right * (float)FollowCameraMath.LateralFollow(
                    sideways, settings.DriftMetres, FollowCameraMath.SmoothingFactor(settings.DriftReturnTime, dt));
            }

            double slopeDown = FollowCameraMath.SlopeDownAlongDegrees(normal.x, normal.y, normal.z, _viewDirection.x, _viewDirection.z);
            float pitch = (float)FollowCameraMath.Pitch(
                slopeDown, settings.PitchAboveSlopeDegrees, settings.MinPitchDegrees, settings.MaxPitchDegrees);
            _pitch = snap || _pitch < 0f ? pitch : Mathf.Lerp(_pitch, pitch, FollowCameraMath.SmoothingFactor(settings.PitchSmoothTime, dt));

            // The look-ahead grows with speed and only the smoothing's lag pulls the rider back up the screen, so at a low
            // frame rate a fast rider fell off its bottom: the aim never leads the rider past the lowest line.
            Vector3 toRider = rider - _aim;
            float lead = -Vector3.Dot(toRider, _viewDirection);
            float maxLead = (float)FollowCameraMath.MaxLead(
                _pitch, settings.Distance, _camera.fieldOfView, toRider.y, settings.LowestRiderOnScreen);
            if (lead > maxLead)
            {
                _aim += _viewDirection * (maxLead - lead);
            }

            float yaw = (float)FollowCameraMath.YawDegrees(_viewDirection.x, _viewDirection.z);
            FollowCameraMath.Offset(yaw, _pitch, settings.Distance, out float x, out float y, out float z);
            transform.SetPositionAndRotation(_aim + new Vector3(x, y, z), Quaternion.Euler(_pitch + shakePitch, yaw + shakeYaw, _roll));
        }

        private static Vector3 SnowNormal(RideSession session, Vector3 position)
        {
            session.Slope.Heightmap.Sample(
                SimCoordinates.FromMetres(position.x), SimCoordinates.FromMetres(position.z), out _, out Vec3 normal);
            return new Vector3(SimCoordinates.ToFloat(normal.X), SimCoordinates.ToFloat(normal.Y), SimCoordinates.ToFloat(normal.Z));
        }
    }
}
