using UnityEngine;

namespace FirstChair.Riding
{
    /// <summary>
    /// The placeholder skier: a body of primitives wearing <c>PR_Ski</c> on its boots and holding <c>PR_SkiPole</c>
    /// by the grips, both attached at their sockets. Each frame it takes the interpolated pose from
    /// <see cref="SimDriver"/> (position, heading, edge lean) and the snow normal from the sim's heightmap. Read only:
    /// it never writes rider state.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class RiderView : MonoBehaviour
    {
        [SerializeField] private SimDriver driver;
        [SerializeField] private RiderViewSettings settings;

        [Tooltip("Assets/Art/Equipment/PR_Ski/PR_Ski.fbx. Its tip faces +Z in model space.")]
        [SerializeField] private GameObject skiModel;

        [Tooltip("Assets/Art/Equipment/PR_SkiPole/PR_SkiPole.fbx. Tip at the origin, shaft up +Y.")]
        [SerializeField] private GameObject poleModel;

        [Tooltip("A URP Lit material for the body parts, tinted per part. Without it the primitives keep Unity's built-in "
            + "default material, which URP draws magenta in a player.")]
        [SerializeField] private Material bodyMaterial;

        [Tooltip("Double-sided material for the ski tracks.")]
        [SerializeField] private Material trackMaterial;

        private Transform _lean;
        private TrailRenderer _trackLeft;
        private TrailRenderer _trackRight;

        private void Awake()
        {
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<RiderViewSettings>();
            }

            BuildPlaceholder();
        }

        private void LateUpdate()
        {
            if (driver == null || driver.Session == null)
            {
                return;
            }

            RiderPose pose = driver.Pose;
            Vector3 normal = ViewHelpers.SnowNormal(driver.Session, driver.InterpolationAlpha);
            Quaternion tilt = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(Vector3.up, normal), settings.SlopeAlign);
            transform.SetPositionAndRotation(SimCoordinatesUnity.Position(pose), tilt * Quaternion.Euler(0f, pose.YawDegrees, 0f));
            _lean.localRotation = Quaternion.Euler(0f, 0f, pose.RollDegrees * settings.LeanScale);

            bool onSnow = !pose.Airborne && !pose.Crashed;
            UpdateTrack(_trackLeft, onSnow, driver.Session.SnappedThisFrame);
            UpdateTrack(_trackRight, onSnow, driver.Session.SnappedThisFrame);
        }

        private static void UpdateTrack(TrailRenderer track, bool onSnow, bool snapped)
        {
            if (track == null)
            {
                return;
            }

            if (snapped)
            {
                track.Clear();
            }

            track.emitting = onSnow;
        }

        private void BuildPlaceholder()
        {
            _lean = new GameObject("Lean").transform;
            _lean.SetParent(transform, false);

            float half = settings.StanceWidth * 0.5f;
            for (int side = -1; side <= 1; side += 2)
            {
                // Ski under each foot; the leg starts at the ski's boot socket.
                Vector3 skiOrigin = new Vector3(side * half, 0f, 0f);
                Vector3 foot = skiOrigin + settings.SkiSocketFallback;
                if (skiModel != null)
                {
                    Transform ski = Instantiate(skiModel, _lean, false).transform;
                    ski.name = side < 0 ? "Ski_L" : "Ski_R";
                    ski.localPosition = skiOrigin;
                    ski.localRotation = Quaternion.identity;
                    TrailRenderer track = SkiTrack.Create(ski, trackMaterial, settings);
                    if (side < 0)
                    {
                        _trackLeft = track;
                    }
                    else
                    {
                        _trackRight = track;
                    }

                    Transform socket = ViewHelpers.FindSocket(ski, settings.SkiSocket);
                    if (socket != null)
                    {
                        foot = _lean.InverseTransformPoint(socket.position);
                    }
                    else
                    {
                        Debug.LogWarning("RiderView: " + skiModel.name + " has no " + settings.SkiSocket + " node; using the manifest fallback.", this);
                    }
                }

                Vector3 hip = new Vector3(side * half * 0.8f, settings.HipHeight, 0f);
                Limb(PrimitiveType.Capsule, foot, hip, 0.07f, settings.PantsColor);

                // Pole in each hand, gripped at its socket, tip trailing.
                Vector3 shoulder = new Vector3(side * 0.2f, settings.ShoulderHeight, 0f);
                Vector3 hand = new Vector3(side * settings.HandSpread, settings.HandHeight, settings.HandForward);
                Limb(PrimitiveType.Capsule, shoulder, hand, 0.045f, settings.JacketColor);
                if (poleModel != null)
                {
                    var anchor = new GameObject(side < 0 ? "Hand_L" : "Hand_R").transform;
                    anchor.SetParent(_lean, false);
                    anchor.localPosition = hand;
                    anchor.localRotation = Quaternion.Euler(settings.PoleTiltDegrees, 0f, 0f);
                    Transform pole = Instantiate(poleModel, anchor, false).transform;
                    pole.localRotation = Quaternion.identity;
                    pole.localPosition = Vector3.zero;
                    Transform socket = ViewHelpers.FindSocket(pole, settings.PoleSocket);
                    Vector3 grip = socket != null ? pole.InverseTransformPoint(socket.position) : settings.PoleSocketFallback;
                    pole.localPosition = -Vector3.Scale(pole.localScale, grip);
                }
            }

            Limb(PrimitiveType.Capsule, new Vector3(0f, settings.HipHeight - 0.05f, 0f), new Vector3(0f, settings.ShoulderHeight + 0.05f, 0f), 0.17f, settings.JacketColor);
            Transform head = Primitive(PrimitiveType.Sphere, settings.JacketColor);
            head.localPosition = new Vector3(0f, settings.HeadHeight, 0f);
            head.localScale = Vector3.one * 0.24f;
        }

        private Transform Limb(PrimitiveType type, Vector3 from, Vector3 to, float radius, Color color)
        {
            Transform limb = Primitive(type, color);
            Vector3 span = to - from;
            limb.localPosition = (from + to) * 0.5f;
            limb.localRotation = Quaternion.FromToRotation(Vector3.up, span);
            limb.localScale = new Vector3(radius * 2f, span.magnitude * 0.5f, radius * 2f);
            return limb;
        }

        private Transform Primitive(PrimitiveType type, Color color)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            Destroy(part.GetComponent<Collider>());
            var partRenderer = part.GetComponent<Renderer>();
            if (bodyMaterial != null)
            {
                partRenderer.sharedMaterial = bodyMaterial;
            }

            partRenderer.material.color = color;
            part.transform.SetParent(_lean, false);
            return part.transform;
        }
    }
}
