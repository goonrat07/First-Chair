using FirstChair.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FirstChair.Riding
{
    /// <summary>
    /// Riding the chairlift back up (Story 010), as in GMA2: ski into the bottom station and the rider waits for the next
    /// chair, sits on it and rides it to the top (hold W, or the gamepad's south button, to hurry the lift), then a new
    /// run starts at the top station. While the rider is on the lift the sim is paused, the view sits the skier on the
    /// chair and the camera follows the chair. Presentation only: each run is still one recording, top to bottom.
    /// </summary>
    [DefaultExecutionOrder(75)] // after the skier view places the rider (50), before the camera reads it (100)
    public sealed class LiftRide : MonoBehaviour
    {
        [SerializeField] private SimDriver driver;
        [SerializeField] private ChairLift lift;

        [Tooltip("The rider's view (the skier), moved onto the chair while riding.")]
        [SerializeField] private Transform rider;

        [SerializeField] private FollowCamera followCamera;

        [Tooltip("Ride within this distance of the boarding point, metres, to get on the lift.")]
        [SerializeField, Min(1f)] private float boardingRadius = 9f;

        [Tooltip("The lift runs this many times faster while W is held.")]
        [SerializeField, Min(1f)] private float hurry = 5f;

        [Tooltip("Where the rider's feet sit relative to the rope grip, in the chair's frame (y is up, z forward).")]
        [SerializeField] private Vector3 feetOnChair = new Vector3(0f, -2.9f, 0.35f);

        private enum Phase
        {
            Skiing,
            Waiting,
            Seated,
        }

        private Phase _phase;
        private int _chair;
        private double _ropeAtStart;
        private double _ropeToCome;
        private GUIStyle _hint;

        /// <summary>Whether the rider is at or on the lift.</summary>
        public bool Riding => _phase != Phase.Skiing;

        private void LateUpdate()
        {
            if (driver == null || driver.Session == null || lift == null || lift.Loop == null || rider == null)
            {
                return;
            }

            switch (_phase)
            {
                case Phase.Skiing:
                    RiderState state = driver.Session.Current;
                    Vector3 at = SimCoordinatesUnity.ToVector3(state.Position);
                    Vector3 boarding = lift.BoardingPoint;
                    float dx = at.x - boarding.x, dz = at.z - boarding.z;
                    if (!state.Airborne && !state.Crashed && dx * dx + dz * dz <= boardingRadius * boardingRadius)
                    {
                        Board();
                    }

                    break;

                case Phase.Waiting:
                    lift.SpeedMultiplier = 1f;
                    if (lift.Offset - _ropeAtStart >= _ropeToCome)
                    {
                        // The chair has reached the boarding point: sit down and ride it to the unload.
                        _phase = Phase.Seated;
                        _ropeAtStart = lift.Offset;
                        _ropeToCome = Wrap(lift.UnloadDistance - lift.BoardingDistance, lift.Loop.Length);
                        if (followCamera != null)
                        {
                            followCamera.RideTarget = lift.Chair(_chair);
                        }
                    }

                    break;

                case Phase.Seated:
                    Transform chair = lift.Chair(_chair);
                    rider.SetPositionAndRotation(chair.TransformPoint(feetOnChair), chair.rotation);
                    lift.SpeedMultiplier = Hurrying() ? hurry : 1f;
                    if (lift.Offset - _ropeAtStart >= _ropeToCome)
                    {
                        GetOff();
                    }

                    break;
            }
        }

        private void Board()
        {
            driver.Paused = true;
            _chair = lift.NextChairToBoard(out double wait);
            _ropeAtStart = lift.Offset;
            _ropeToCome = wait;
            _phase = Phase.Waiting;
        }

        private void GetOff()
        {
            _phase = Phase.Skiing;
            lift.SpeedMultiplier = 1f;
            if (followCamera != null)
            {
                followCamera.RideTarget = null;
            }

            driver.Paused = false;
            string error = driver.RestartRun(driver.RunTuning, false);
            if (error != null)
            {
                Debug.LogError("LiftRide: could not start the next run at the top: " + error, this);
            }
        }

        private static bool Hurrying()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;
            return (keyboard != null && (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed))
                || (gamepad != null && gamepad.buttonSouth.isPressed);
        }

        private static double Wrap(double value, double length)
        {
            double wrapped = value % length;
            return wrapped < 0.0 ? wrapped + length : wrapped;
        }

        private void OnGUI()
        {
            if (_phase == Phase.Skiing)
            {
                return;
            }

            if (_hint == null)
            {
                _hint = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                _hint.normal.textColor = Color.white;
            }

            float scale = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = 1080f * Screen.width / Mathf.Max(1, Screen.height);
            string text = _phase == Phase.Waiting ? "Waiting for the next chair" : "On the lift. Hold W to hurry it up";
            GUI.Label(new Rect(0f, 960f, width, 40f), text, _hint);
        }
    }
}
