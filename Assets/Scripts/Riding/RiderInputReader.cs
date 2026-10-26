using FirstChair.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FirstChair.Riding
{
    /// <summary>
    /// Reads keyboard and gamepad through the Input System package (not the legacy Input Manager) and packs them into
    /// one <see cref="SimInput"/>. <see cref="SimDriver"/> calls <see cref="ReadInput"/> once per frame.
    /// </summary>
    /// <remarks>
    /// <para>Keyboard (each action is one key; a combination such as steer + tuck is at most two):
    /// A / Left arrow steer left, D / Right arrow steer right, W / Up tuck or pole, S / Down brake, Space jump (hold to
    /// load, release to pop), Left Shift grab, R reset.</para>
    /// <para>Gamepad: left stick X steers (quantized to -127..127 before the sim), stick up or right trigger tucks,
    /// stick down or left trigger brakes, d-pad left/right full lock, South (A / Cross) jump, West (X / Square) or right
    /// shoulder grab, Select / View reset.</para>
    /// </remarks>
    public sealed class RiderInputReader : MonoBehaviour
    {
        [Tooltip("Deadzone and press thresholds. Leave empty for the defaults.")]
        [SerializeField] private RiderInputConfig config;

        private RiderInputQuantizer _quantizer;

        /// <summary>The value the last <see cref="ReadInput"/> returned.</summary>
        public SimInput LastInput { get; private set; }

        /// <summary>Reads the devices and returns the frame's packed input.</summary>
        public SimInput ReadInput()
        {
            if (_quantizer == null)
            {
                _quantizer = new RiderInputQuantizer(config != null ? config.ToSettings() : RiderInputQuantizerSettings.Default);
            }

            var raw = new RawRiderInput();
            // While a Feel Lab text box has the keyboard, typing doesn't ride.
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && !FeelLab.KeyboardCaptured)
            {
                raw.SteerLeft = keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed;
                raw.SteerRight = keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed;
                raw.Forward = keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed;
                raw.Back = keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed;
                raw.Jump = keyboard.spaceKey.isPressed;
                // GMA's keys (Story 009): Shift with a turn drifts on the snow (and grabs in the air), Ctrl or Alt with a
                // turn is a quick turn.
                raw.Grab = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
                raw.Quick = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed
                    || keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed;
                raw.Reset = keyboard.rKey.isPressed;
            }

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                Vector2 stick = gamepad.leftStick.ReadValue();
                raw.SteerAxis = stick.x;
                raw.TuckAxis = Mathf.Max(stick.y, gamepad.rightTrigger.ReadValue());
                raw.BrakeAxis = Mathf.Max(-stick.y, gamepad.leftTrigger.ReadValue());
                raw.SteerLeft |= gamepad.dpad.left.isPressed;
                raw.SteerRight |= gamepad.dpad.right.isPressed;
                raw.Jump |= gamepad.buttonSouth.isPressed;
                raw.Grab |= gamepad.buttonWest.isPressed || gamepad.rightShoulder.isPressed;
                raw.Quick |= gamepad.leftShoulder.isPressed;
                raw.Reset |= gamepad.selectButton.isPressed;
            }

            LastInput = _quantizer.Pack(raw);
            return LastInput;
        }
    }
}
