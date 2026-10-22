using UnityEngine;

namespace FirstChair.Riding
{
    /// <summary>
    /// A high, pulled-back follow camera like Grand Mountain Adventure 2's: a fixed azimuth and elevation around a
    /// smoothed aim point a little ahead of the rider. Presentation only; it reads the rider view's transform.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(Camera))]
    public sealed class FollowCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private FollowCameraSettings settings;

        private Camera _camera;
        private Vector3 _aim;
        private Vector3 _lookDirection = Vector3.forward;
        private bool _hasAim;

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
            if (target == null || settings == null)
            {
                return;
            }

            _camera.fieldOfView = (float)FollowCameraMath.VerticalFovDegrees(settings.FocalLengthMm, settings.SensorHeightMm);
            _camera.nearClipPlane = settings.NearClip;
            _camera.farClipPlane = settings.FarClip;

            float dt = Time.deltaTime;
            Vector3 heading = target.forward;
            heading.y = 0f;
            if (heading.sqrMagnitude > 1e-4f)
            {
                _lookDirection = Vector3.Slerp(_lookDirection, heading.normalized, FollowCameraMath.SmoothingFactor(settings.LookAheadSmoothTime, dt));
            }

            Vector3 desired = target.position + Vector3.up * settings.AimHeight + _lookDirection * settings.LookAhead;
            if (!_hasAim || (desired - _aim).sqrMagnitude > settings.SnapDistance * settings.SnapDistance)
            {
                _aim = desired;
                _hasAim = true;
            }
            else
            {
                _aim = Vector3.Lerp(_aim, desired, FollowCameraMath.SmoothingFactor(settings.PositionSmoothTime, dt));
            }

            FollowCameraMath.Offset(settings.AzimuthDegrees, settings.ElevationDegrees, settings.Distance, out float x, out float y, out float z);
            transform.SetPositionAndRotation(_aim + new Vector3(x, y, z), Quaternion.Euler(settings.ElevationDegrees, settings.AzimuthDegrees, 0f));
        }
    }
}
