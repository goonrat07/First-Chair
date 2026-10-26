using System;
using System.Collections.Generic;
using System.IO;
using FirstChair.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FirstChair.Riding
{
    /// <summary>
    /// Runs the riding sim for the test slope. Each frame it reads input once, runs as many fixed 1/120 s steps as
    /// are due (capped, with a warning when time is dropped), feeds the same packed input to every step, records it, and
    /// folds each step's events into the run score. Views read <see cref="Pose"/> or <see cref="Session"/>; nothing
    /// writes rider state back.
    /// </summary>
    /// <remarks>Debug keys: F5 checks the replay of the ride so far against the live run, F6 saves the recording.</remarks>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class SimDriver : MonoBehaviour
    {
        /// <summary>The test-slope folder under StreamingAssets in a built player; TestSlopeBuildStep copies it there.</summary>
        public const string DefaultPlayerSlopeFolder = "TestSlope";

        [Tooltip("Reads keyboard and gamepad once per frame. Leave empty to ride with no input.")]
        [SerializeField] private RiderInputReader inputReader;

        [Tooltip("The run's random seed (stored in the recording header).")]
        [SerializeField] private long seed = 1;

        [Tooltip("Assist level stored in the RunSetup.")]
        [SerializeField, Range(0, 255)] private int assistLevel;

        [Tooltip("Most sim steps one frame may run (12 = 100 ms). Time beyond it is dropped and logged.")]
        [SerializeField, Min(1)] private int maxStepsPerFrame = 12;

        [Tooltip("Keep every step's state and events so the replay check (F5) can compare step for step.")]
        [SerializeField] private bool keepReplayHistory = true;

        [Tooltip("Test-slope folder under Assets/, read in the editor.")]
        [SerializeField] private string editorSlopeFolder = "Terrain/TestSlope";

        [Tooltip("Test-slope folder under StreamingAssets/, read in a built player.")]
        [SerializeField] private string playerSlopeFolder = DefaultPlayerSlopeFolder;

        [Tooltip("The slope's files' shared name: <name>.txt, <name>.raw and <name>Snow.raw (TestSlope or TestMountain).")]
        [SerializeField] private string slopeName = TestSlopeData.TestSlopeName;

        private FixedStepClock _clock;
        private float _nextDropLogTime;
        private long _droppedSinceLog;

        /// <summary>Raised after each sim step that produced events, with that step's events (read-only).</summary>
        public event Action<IReadOnlyList<SimEvent>> StepEvents;

        /// <summary>The live ride, or null if the slope failed to load.</summary>
        public RideSession Session { get; private set; }

        /// <summary>How far the view sits between <see cref="RideSession.Previous"/> and <see cref="RideSession.Current"/>.</summary>
        public float InterpolationAlpha => _clock == null ? 1f : (float)_clock.Alpha;

        /// <summary>The rider's interpolated pose for this frame.</summary>
        public RiderPose Pose => Session == null ? default : RiderPose.Interpolate(Session.Previous, Session.Current, InterpolationAlpha);

        /// <summary>Every event of this frame's steps, read-only.</summary>
        public IReadOnlyList<SimEvent> FrameEvents => Session?.FrameEvents;

        /// <summary>
        /// While true, no sim steps run and the ride stands still: the rider is on the chairlift (Story 010). The views
        /// lay no tracks while it is set.
        /// </summary>
        public bool Paused { get; set; }

        /// <summary>The slope's name, as in its file names.</summary>
        public string SlopeName => slopeName;

        /// <summary>The riding numbers runs start with: <see cref="Tuning.Default"/> until the Feel Lab changes them.</summary>
        public Tuning RunTuning { get; private set; } = Tuning.Default;

        /// <summary>The folder the slope files are read from.</summary>
        public string SlopeFolder
        {
            get
            {
#if UNITY_EDITOR
                return Path.Combine(Application.dataPath, editorSlopeFolder);
#else
                return Path.Combine(Application.streamingAssetsPath, playerSlopeFolder);
#endif
            }
        }

        /// <summary>Loads the slope and starts a new run at the layout's start point.</summary>
        public void StartRun()
        {
            TestSlopeData slope;
            try
            {
                slope = TestSlopeData.Load(SlopeFolder, slopeName);
            }
            catch (Exception e)
            {
                Debug.LogError("SimDriver: could not load the test slope from " + SlopeFolder + ": " + e.Message, this);
                enabled = false;
                return;
            }

            Begin(slope, RunTuning, slope.CreateSetup(unchecked((ulong)seed), (byte)Mathf.Clamp(assistLevel, 0, 255)));
        }

        /// <summary>
        /// Starts a new run with <paramref name="tuning"/>: the Feel Lab's "Apply" (Story 009). From the rider's current spot
        /// (the last safe spot when airborne or crashed), at rest and facing down the fall line, or from the slope's start.
        /// The new run records its own tuning, so its replay still checks out.
        /// </summary>
        /// <param name="tuning">The riding numbers for the new run.</param>
        /// <param name="fromCurrentSpot">Start where the rider is, rather than at the top.</param>
        /// <returns>Null, or why the run could not start (the tuning holds a value a ride cannot run on).</returns>
        /// <example>
        /// <code>
        /// string error = driver.RestartRun(FeelLabValues.With(driver.RunTuning, "TurnRateLow", 2.5), true);
        /// </code>
        /// </example>
        public string RestartRun(Tuning tuning, bool fromCurrentSpot)
        {
            TestSlopeData slope = Session?.Slope;
            if (slope == null)
            {
                RunTuning = tuning;
                StartRun();
                return Session == null ? "the slope could not be loaded" : null;
            }

            RunSetup setup = slope.CreateSetup(unchecked((ulong)seed), (byte)Mathf.Clamp(assistLevel, 0, 255));
            if (fromCurrentSpot)
            {
                RiderState now = Session.Current;
                Vec3 at = now.Airborne || now.Crashed || now.Grinding ? now.SafePosition : now.Position;
                setup = new RunSetup(Sport.Skis, setup.AssistLevel, setup.Seed, at.X, at.Z);
            }

            try
            {
                Begin(slope, tuning, setup);
            }
            catch (ArgumentException e)
            {
                return e.Message;
            }

            RunTuning = tuning;
            return null;
        }

        private void Begin(TestSlopeData slope, Tuning tuning, RunSetup setup)
        {
            // Built before the old session is let go, so a tuning the sim refuses leaves the old run riding.
            var session = new RideSession(slope, tuning, setup, keepReplayHistory);
            if (Session != null)
            {
                Session.StepEvents -= ForwardStepEvents;
            }

            Session = session;
            Session.StepEvents += ForwardStepEvents;
            _clock = new FixedStepClock(Simulation.StepsPerSecond, maxStepsPerFrame);
        }

        /// <summary>Replays the ride so far and compares it with the live run step for step; logs and returns the result.</summary>
        public ReplayCheckResult VerifyReplay()
        {
            if (Session == null)
            {
                return new ReplayCheckResult(false, ReplayStatus.Ok, 0, -1, "no ride is running");
            }

            ReplayCheckResult result = ReplayCheck.Verify(Session);
            if (result.Passed)
            {
                Debug.Log("SimDriver: " + result, this);
            }
            else
            {
                Debug.LogError("SimDriver: " + result, this);
            }

            return result;
        }

        /// <summary>Writes the recording so far under the persistent data path and returns the file's path.</summary>
        public string SaveRecording()
        {
            if (Session == null)
            {
                return null;
            }

            string folder = Path.Combine(Application.persistentDataPath, "Recordings");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "ride-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".fcride");
            File.WriteAllBytes(path, Session.Recording.ToBytes());
            Debug.Log("SimDriver: saved " + Session.Recording.Inputs.Count + " steps to " + path, this);
            return path;
        }

        [ContextMenu("Verify replay of this ride")]
        private void VerifyReplayMenu() => VerifyReplay();

        [ContextMenu("Save recording")]
        private void SaveRecordingMenu() => SaveRecording();

        private void Awake() => StartRun();

        private void OnDestroy()
        {
            if (Session != null)
            {
                Session.StepEvents -= ForwardStepEvents;
            }
        }

        private void Update()
        {
            if (Session == null || Paused)
            {
                return;
            }

            // Input is read once per frame; every step of this frame gets the same packed value.
            SimInput input = inputReader != null ? inputReader.ReadInput() : SimInput.Pack(SimButtons.None, 0);
            int steps = _clock.Advance(Time.deltaTime);
            ReportDroppedTime();
            Session.RunSteps(input, steps);
            HandleDebugKeys();
        }

        private void ReportDroppedTime()
        {
            _droppedSinceLog += _clock.LastDroppedSteps;
            if (_droppedSinceLog > 0 && Time.unscaledTime >= _nextDropLogTime)
            {
                Debug.LogWarning(
                    "SimDriver: frames ran over the cap of " + _clock.MaxStepsPerFrame + " steps; dropped " + _droppedSinceLog
                    + " steps (" + (_droppedSinceLog * 1000L / Simulation.StepsPerSecond) + " ms of sim time). The ride slowed down instead of jumping.",
                    this);
                _droppedSinceLog = 0;
                _nextDropLogTime = Time.unscaledTime + 1f;
            }
        }

        private void HandleDebugKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.f5Key.wasPressedThisFrame)
            {
                VerifyReplay();
            }

            if (keyboard.f6Key.wasPressedThisFrame)
            {
                SaveRecording();
            }
        }

        private void ForwardStepEvents(IReadOnlyList<SimEvent> events) => StepEvents?.Invoke(events);
    }
}
