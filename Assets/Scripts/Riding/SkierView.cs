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

        [Tooltip("The pose clips in SkierPose order: Stance, CarveLeft, CarveRight, Tuck, Crouch, Air, Brake, Pole, Crash.")]
        [SerializeField] private AnimationClip[] poseClips = new AnimationClip[SkierPoseWeights.Count];

        [Tooltip("Double-sided material for the ski tracks.")]
        [SerializeField] private Material trackMaterial;

        private readonly float[] _target = new float[SkierPoseWeights.Count];
        private readonly float[] _shown = new float[SkierPoseWeights.Count];
        private readonly AnimationClipPlayable[] _clips = new AnimationClipPlayable[SkierPoseWeights.Count];
        private readonly float[] _clipLength = new float[SkierPoseWeights.Count];
        private PlayableGraph _graph;
        private AnimationMixerPlayable _mixer;
        private TrailRenderer _trackLeft;
        private TrailRenderer _trackRight;
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

            _trackLeft = AttachSki(skier.transform, settings.SkierSkiSocketLeft);
            _trackRight = AttachSki(skier.transform, settings.SkierSkiSocketRight);
            AttachPole(skier.transform, settings.SkierHandSocketLeft);
            AttachPole(skier.transform, settings.SkierHandSocketRight);

            Animator animator = skier.GetComponent<Animator>();
            if (animator == null)
            {
                animator = skier.AddComponent<Animator>();
            }

            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            BuildGraph(animator);
            _shown[(int)SkierPose.Stance] = 1f;
        }

        private void OnDestroy()
        {
            if (_graph.IsValid())
            {
                _graph.Destroy();
            }
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
            Quaternion tilt = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(Vector3.up, normal), settings.SlopeAlign);
            transform.SetPositionAndRotation(SimCoordinatesUnity.Position(pose), tilt * Quaternion.Euler(0f, pose.YawDegrees, 0f));

            RiderState current = session.Current;
            Tuning tuning = session.Tuning;
            SkierPoseWeights.Target(current, tuning, _target);
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

            bool onSnow = !current.Airborne && !current.Crashed;
            if (session.SnappedThisFrame)
            {
                Clear(_trackLeft);
                Clear(_trackRight);
            }

            SetEmitting(_trackLeft, onSnow);
            SetEmitting(_trackRight, onSnow);
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
            _graph.Evaluate(0f);
        }

        private TrailRenderer AttachSki(Transform skier, string socketName)
        {
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

            Transform ski = ViewHelpers.Attach(skiModel, socket, settings.SkiSocket, settings.SkiSocketFallback, this);
            return SkiTrack.Create(ski, trackMaterial, settings);
        }

        private void AttachPole(Transform skier, string socketName)
        {
            Transform socket = ViewHelpers.FindSocket(skier, socketName);
            if (socket == null)
            {
                Debug.LogWarning("SkierView: CH_Skier has no " + socketName + " socket; no pole there.", this);
                return;
            }

            if (poleModel != null)
            {
                ViewHelpers.Attach(poleModel, socket, settings.PoleSocket, settings.PoleSocketFallback, this);
            }
        }

        private static void SetEmitting(TrailRenderer track, bool emitting)
        {
            if (track != null && track.emitting != emitting)
            {
                track.emitting = emitting;
            }
        }

        private static void Clear(TrailRenderer track)
        {
            if (track != null)
            {
                track.Clear();
            }
        }
    }
}
