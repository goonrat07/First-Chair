using FirstChair.Sim;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace FirstChair.Riding
{
    /// <summary>
    /// The real skier (<c>CH_Skier.fbx</c>) wearing <c>PR_Ski</c> and holding <c>PR_SkiPole</c> at its sockets. Each frame
    /// it follows the sim's interpolated position and heading on the snow's slope, and blends its pose clips (stance,
    /// carves, tuck, crouch, air, brake, pole, crash) by weights from <see cref="SkierPoseWeights"/>, so the skier
    /// angulates into turns, tucks, loads a pop and falls. It leaves ski tracks while on the snow. Read only: it never
    /// writes rider state.
    /// </summary>
    /// <remarks>
    /// The clips are played through a manual <see cref="PlayableGraph"/>: one <see cref="AnimationMixerPlayable"/> input
    /// per <see cref="SkierPose"/>, each clip's time driven here so the looping ones (stance, pole) cycle and the held
    /// poses stay put. A missing clip falls back to the stance clip.
    /// </remarks>
    [DefaultExecutionOrder(50)]
    public sealed class SkierView : MonoBehaviour
    {
        [SerializeField] private SimDriver driver;
        [SerializeField] private RiderViewSettings settings;

        [Tooltip("Assets/Art/Characters/CH_Skier/CH_Skier.fbx, imported as a Generic rig.")]
        [SerializeField] private GameObject skierModel;

        [Tooltip("Assets/Art/Equipment/PR_Ski/PR_Ski.fbx. Its tip faces +Z in model space.")]
        [SerializeField] private GameObject skiModel;

        [Tooltip("Assets/Art/Equipment/PR_SkiPole/PR_SkiPole.fbx. Tip at the origin, shaft up +Y.")]
        [SerializeField] private GameObject poleModel;

        [Tooltip("The pose clips in SkierPose order: Stance, CarveLeft, CarveRight, Tuck, Crouch, Air, Brake, Pole, Crash, the five grabs, RaceCarveLeft, RaceCarveRight.")]
        [SerializeField] private AnimationClip[] poseClips = new AnimationClip[SkierPoseWeights.Count];

        [Tooltip("Translucent, double-sided material for the ski tracks.")]
        [SerializeField] private Material trackMaterial;

        private readonly float[] _target = new float[SkierPoseWeights.Count];
        private readonly float[] _shown = new float[SkierPoseWeights.Count];
        private readonly AnimationClipPlayable[] _clips = new AnimationClipPlayable[SkierPoseWeights.Count];
        private readonly float[] _clipLength = new float[SkierPoseWeights.Count];
        private PlayableGraph _graph;
        private AnimationMixerPlayable _mixer;
        private Transform _skiLeft;
        private Transform _skiRight;
        private SkiTrack _trackLeft;
        private SkiTrack _trackRight;
        private RideSession _paintedSession;
        private float _clock;

        private void Awake()
        {
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<RiderViewSettings>();
            }

            if (skierModel == null)
            {
                Debug.LogError("SkierView: no skier model; nothing to draw.", this);
                enabled = false;
                return;
            }

            GameObject skier = Instantiate(skierModel, transform, false);
            skier.name = "CH_Skier";
            skier.transform.localPosition = new Vector3(0f, settings.SkierRootLift, 0f);
            skier.transform.localRotation = Quaternion.identity;

            Animator animator = skier.GetComponent<Animator>();
            if (animator == null)
            {
                animator = skier.AddComponent<Animator>();
            }

            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _shown[(int)SkierPose.Stance] = 1f;
            BuildGraph(animator);

            // The props are attached in the Stance pose, where both feet are flat and point forward (see AttachSki).
            Quaternion skierRotation = skier.transform.rotation;
            _skiLeft = AttachSki(skier.transform, settings.SkierSkiSocketLeft, skierRotation, out Quaternion leftFrame);
            _skiRight = AttachSki(skier.transform, settings.SkierSkiSocketRight, skierRotation, out Quaternion rightFrame);
            _trackLeft = _skiLeft != null ? new SkiTrack("Ski track L", trackMaterial, settings) : null;
            _trackRight = _skiRight != null ? new SkiTrack("Ski track R", trackMaterial, settings) : null;
            AttachPole(skier.transform, settings.SkierHandSocketLeft, leftFrame);
            AttachPole(skier.transform, settings.SkierHandSocketRight, rightFrame);
        }

        private void OnDestroy()
        {
            if (_graph.IsValid())
            {
                _graph.Destroy();
            }

            _trackLeft?.Dispose();
            _trackRight?.Dispose();
        }

        private void LateUpdate()
        {
            if (driver == null || driver.Session == null)
            {
                return;
            }

            RideSession session = driver.Session;
            RiderPose pose = driver.Pose;
            Vector3 normal = ViewHelpers.SnowNormal(session, driver.InterpolationAlpha);
            RiderPlacement.Place(pose, normal, settings.SlopeAlign, settings.FlipPivotHeight, out Vector3 position, out Quaternion rotation);
            transform.SetPositionAndRotation(position, rotation);

            RiderState current = session.Current;
            Tuning tuning = session.Tuning;
            SkierPoseWeights.Target(current, tuning, _target);
            if (driver.Paused)
            {
                // Riding the chairlift (Story 010): sitting, near enough, in the crouch.
                System.Array.Clear(_target, 0, _target.Length);
                _target[(int)SkierPose.Crouch] = 1f;
            }

            SkierPoseWeights.Smooth(_shown, _target, Time.deltaTime, settings.PoseSmoothTime);
            if (_graph.IsValid())
            {
                _clock += Time.deltaTime;
                for (int i = 0; i < SkierPoseWeights.Count; i++)
                {
                    if (_clips[i].IsValid())
                    {
                        _clips[i].SetTime(Mathf.Repeat(_clock, _clipLength[i]));
                    }

                    _mixer.SetInputWeight(i, _shown[i]);
                }

                _graph.Evaluate(0f);
            }

            // The tracks break across a reset or a new run, so no ribbon is drawn between the two spots.
            bool onSnow = !current.Airborne && !current.Crashed && !driver.Paused;
            bool jumped = session.SnappedThisFrame || session != _paintedSession;
            _paintedSession = session;
            Paint(_trackLeft, _skiLeft, position, normal, onSnow, jumped);
            Paint(_trackRight, _skiRight, position, normal, onSnow, jumped);
        }

        private void BuildGraph(Animator animator)
        {
            AnimationClip stance = poseClips != null && poseClips.Length > 0 ? poseClips[(int)SkierPose.Stance] : null;
            if (stance == null)
            {
                Debug.LogWarning("SkierView: no Stance clip; the skier stays in its bind pose.", this);
                return;
            }

            _graph = PlayableGraph.Create("SkierPoses");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            _mixer = AnimationMixerPlayable.Create(_graph, SkierPoseWeights.Count);
            for (int i = 0; i < SkierPoseWeights.Count; i++)
            {
                AnimationClip assigned = i < poseClips.Length ? poseClips[i] : null;
                AnimationClip clip = assigned != null ? assigned : stance;
                if (assigned == null)
                {
                    Debug.LogWarning("SkierView: no " + (SkierPose)i + " clip; using Stance.", this);
                }

                _clips[i] = AnimationClipPlayable.Create(_graph, clip);
                _clips[i].SetApplyFootIK(false);
                _clips[i].SetSpeed(0.0);
                _clipLength[i] = Mathf.Max(clip.length, 1f / 30f);
                _graph.Connect(_clips[i], 0, _mixer, i);
            }

            var output = AnimationPlayableOutput.Create(_graph, "Skier", animator);
            output.SetSourcePlayable(_mixer);
            for (int i = 0; i < SkierPoseWeights.Count; i++)
            {
                _mixer.SetInputWeight(i, _shown[i]);
            }

            _graph.Evaluate(0f);
        }

        // CH_Skier keeps Blender's axes on its armature (its sockets' Unity axes are not the props' Unity axes), so a prop
        // attached with an identity rotation stood the skis on end and laid the poles along the fingers (playtest,
        // 2026-10-23). Instead the ski is lined up with the skier itself in the Stance pose, where the feet are flat and
        // point forward, and the resulting frame (skier socket to prop socket) is handed to the pole on the same side.
        private Transform AttachSki(Transform skier, string socketName, Quaternion skierRotation, out Quaternion socketFrame)
        {
            socketFrame = Quaternion.identity;
            Transform socket = ViewHelpers.FindSocket(skier, socketName);
            if (socket == null)
            {
                Debug.LogWarning("SkierView: CH_Skier has no " + socketName + " socket; no ski there.", this);
                return null;
            }

            if (skiModel == null)
            {
                return null;
            }

            Transform ski = Instantiate(skiModel, socket, false).transform;
            ReadSocket(ski, settings.SkiSocket, settings.SkiSocketFallback, out Vector3 bootPosition, out Quaternion bootRotation);
            Quaternion local = Quaternion.Inverse(socket.rotation) * skierRotation;
            Place(ski, local, bootPosition);
            socketFrame = local * bootRotation;
            return ski;
        }

        private void AttachPole(Transform skier, string socketName, Quaternion socketFrame)
        {
            Transform socket = ViewHelpers.FindSocket(skier, socketName);
            if (socket == null)
            {
                Debug.LogWarning("SkierView: CH_Skier has no " + socketName + " socket; no pole there.", this);
                return;
            }

            if (poleModel == null)
            {
                return;
            }

            Transform pole = Instantiate(poleModel, socket, false).transform;
            ReadSocket(pole, settings.PoleSocket, settings.PoleSocketFallback, out Vector3 gripPosition, out Quaternion gripRotation);
            Place(pole, socketFrame * Quaternion.Inverse(gripRotation), gripPosition);
        }

        // A prop's own socket (SOCKET_boot, SOCKET_grip) in its model space; the manifest fallback if the node is missing.
        private void ReadSocket(Transform prop, string socketName, Vector3 fallback, out Vector3 position, out Quaternion rotation)
        {
            Transform socket = ViewHelpers.FindSocket(prop, socketName);
            if (socket == null)
            {
                Debug.LogWarning("SkierView: " + prop.name + " has no " + socketName + " node; using the manifest fallback.", this);
                position = fallback;
                rotation = Quaternion.identity;
                return;
            }

            position = prop.InverseTransformPoint(socket.position);
            rotation = Quaternion.Inverse(prop.rotation) * socket.rotation;
        }

        // Rotates a prop under its parent socket and moves it so its own socket lands on the parent's origin.
        private static void Place(Transform prop, Quaternion localRotation, Vector3 socketInModel)
        {
            prop.localRotation = localRotation;
            prop.localPosition = -(localRotation * Vector3.Scale(prop.localScale, socketInModel));
        }

        private static void Paint(SkiTrack track, Transform ski, Vector3 snowPoint, Vector3 snowNormal, bool onSnow, bool jumped)
        {
            if (track != null && ski != null)
            {
                track.Paint(ski.position, snowPoint, snowNormal, onSnow, jumped);
            }
        }
    }
}
