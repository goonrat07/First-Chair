using System;
using FirstChair.Sim;

namespace FirstChair.Riding
{
    /// <summary>
    /// One frame of raw controls, already read from the devices: analog axes as floats and digital controls as bools.
    /// It never reaches the sim; <see cref="RiderInputQuantizer"/> packs it into a <see cref="SimInput"/> first.
    /// </summary>
    public struct RawRiderInput
    {
        /// <summary>Analog steering, -1 (full left) to 1 (full right): the gamepad's left stick X.</summary>
        public float SteerAxis;

        /// <summary>Analog tuck, 0 to 1: left stick up or the right trigger, whichever is larger.</summary>
        public float TuckAxis;

        /// <summary>Analog brake, 0 to 1: left stick down or the left trigger, whichever is larger.</summary>
        public float BrakeAxis;

        /// <summary>Digital steer left (a key or the d-pad): full lock, through <see cref="SimButtons.Left"/>.</summary>
        public bool SteerLeft;

        /// <summary>Digital steer right: full lock, through <see cref="SimButtons.Right"/>.</summary>
        public bool SteerRight;

        /// <summary>Digital forward (tuck or pole).</summary>
        public bool Forward;

        /// <summary>Digital back (brake).</summary>
        public bool Back;

        /// <summary>Jump held: loads a pop; releasing it pops.</summary>
        public bool Jump;

        /// <summary>Grab held.</summary>
        public bool Grab;

        /// <summary>The quick-turn button (Story 009): a sharp, speed-costing turn while steering.</summary>
        public bool Quick;

        /// <summary>Reset held.</summary>
        public bool Reset;
    }

    /// <summary>The thresholds <see cref="RiderInputQuantizer"/> uses; they come from a data asset in Unity.</summary>
    public readonly struct RiderInputQuantizerSettings
    {
        /// <summary>Creates settings.</summary>
        /// <param name="steeringDeadzone">Stick deflection, 0 to under 1, that still counts as straight.</param>
        /// <param name="axisPressThreshold">Analog tuck or brake, 0 to 1, at which it counts as pressed.</param>
        public RiderInputQuantizerSettings(float steeringDeadzone, float axisPressThreshold)
        {
            SteeringDeadzone = steeringDeadzone;
            AxisPressThreshold = axisPressThreshold;
        }

        /// <summary>Stick deflection that still counts as straight.</summary>
        public float SteeringDeadzone { get; }

        /// <summary>Analog tuck or brake at which it counts as pressed.</summary>
        public float AxisPressThreshold { get; }

        /// <summary>A 0.15 deadzone and a 0.5 press threshold, the values the data asset starts with.</summary>
        public static RiderInputQuantizerSettings Default => new RiderInputQuantizerSettings(0.15f, 0.5f);
    }

    /// <summary>
    /// Packs one frame of <see cref="RawRiderInput"/> into the sim's packed integer. Analog steering is quantized here,
    /// before the sim, to a whole number in [-<see cref="SimInput.SteeringMax"/>, <see cref="SimInput.SteeringMax"/>];
    /// packing itself is <see cref="SimInput.Pack"/>, not duplicated.
    /// </summary>
    public sealed class RiderInputQuantizer
    {
        private readonly RiderInputQuantizerSettings _settings;

        /// <summary>Creates a quantizer.</summary>
        /// <param name="settings">Deadzone and press threshold.</param>
        public RiderInputQuantizer(RiderInputQuantizerSettings settings)
        {
            if (!(settings.SteeringDeadzone >= 0f && settings.SteeringDeadzone < 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(settings), "The steering deadzone must be at least 0 and below 1.");
            }

            _settings = settings;
        }

        /// <summary>
        /// Quantizes an analog steering axis: inside the deadzone is 0; outside it the rest of the travel is rescaled to
        /// 1..<see cref="SimInput.SteeringMax"/> and rounded half away from zero. NaN reads as 0.
        /// </summary>
        /// <param name="axis">Stick X, -1 to 1 (values beyond are clamped).</param>
        /// <param name="deadzone">The deadzone, 0 to under 1.</param>
        public static int QuantizeSteering(float axis, float deadzone)
        {
            if (float.IsNaN(axis))
            {
                return 0;
            }

            double magnitude = Math.Min(Math.Abs((double)axis), 1.0);
            if (magnitude <= deadzone)
            {
                return 0;
            }

            double scaled = (magnitude - deadzone) / (1.0 - deadzone);
            int steps = (int)Math.Round(scaled * SimInput.SteeringMax, MidpointRounding.AwayFromZero);
            if (steps < 1)
            {
                steps = 1;
            }

            return axis < 0f ? -steps : steps;
        }

        /// <summary>Packs one frame of controls into the value every sim step of that frame receives.</summary>
        /// <param name="raw">The frame's controls.</param>
        public SimInput Pack(in RawRiderInput raw)
        {
            SimButtons buttons = SimButtons.None;
            if (raw.SteerLeft)
            {
                buttons |= SimButtons.Left;
            }

            if (raw.SteerRight)
            {
                buttons |= SimButtons.Right;
            }

            if (raw.Forward || raw.TuckAxis >= _settings.AxisPressThreshold)
            {
                buttons |= SimButtons.Forward;
            }

            if (raw.Back || raw.BrakeAxis >= _settings.AxisPressThreshold)
            {
                buttons |= SimButtons.Back;
            }

            if (raw.Jump)
            {
                buttons |= SimButtons.Jump;
            }

            if (raw.Grab)
            {
                buttons |= SimButtons.Grab;
            }

            if (raw.Quick)
            {
                buttons |= SimButtons.Quick;
            }

            if (raw.Reset)
            {
                buttons |= SimButtons.Reset;
            }

            return SimInput.Pack(buttons, QuantizeSteering(raw.SteerAxis, _settings.SteeringDeadzone));
        }
    }
}
