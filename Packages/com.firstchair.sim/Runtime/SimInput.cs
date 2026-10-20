using System;

namespace FirstChair.Sim
{
    /// <summary>The held buttons in one step's input.</summary>
    [Flags]
    public enum SimButtons : uint
    {
        /// <summary>No button held.</summary>
        None = 0,

        /// <summary>Steer left: adds full left to the analog steering (see <see cref="SimInput.CombinedSteering"/>).</summary>
        Left = 1u << 0,

        /// <summary>Steer right: adds full right to the analog steering (see <see cref="SimInput.CombinedSteering"/>).</summary>
        Right = 1u << 1,

        /// <summary>
        /// Forward: tuck on slopes. On flats (gradient below <see cref="Tuning.FlatGradientMax"/>) it poles while the
        /// rider is slower than <see cref="Tuning.PoleSpeedMax"/> and tucks at or above it.
        /// </summary>
        Forward = 1u << 2,

        /// <summary>Back: brake. Wins over <see cref="Forward"/> when both are held.</summary>
        Back = 1u << 3,

        /// <summary>
        /// Jump: held on snow it loads a pop, up to <see cref="Tuning.PopLoadTime"/>; releasing it pops the rider off the
        /// snow. Ignored in the air and while crashed.
        /// </summary>
        Jump = 1u << 4,

        /// <summary>
        /// Grab: in the air, held with a direction it grabs (none = mute, forward = tip, back = tail, left = Japan,
        /// right = safety) and stops rotating. Ignored on snow.
        /// </summary>
        Grab = 1u << 5,

        /// <summary>
        /// Reset: the step it goes down (held now and not in the previous step) returns the rider, at rest, to the last
        /// safe spot, from any state. Holding it does nothing more. Added in sim version 3 in a bit that was reserved.
        /// </summary>
        Reset = 1u << 6,
    }

    /// <summary>
    /// One step's input packed into one 32-bit integer, the unit the recorder stores.
    /// </summary>
    /// <remarks>
    /// <para>Bit layout: bits 0-6 are <see cref="SimButtons"/> (bit 6, <see cref="SimButtons.Reset"/>, since sim
    /// version 3); bit 7 is reserved; bits 8-15 hold the
    /// steering value as a signed byte from -127 (full left) to 127 (full right); bits 16-31 are reserved.
    /// Reserved bits are written as zero and ignored when read.</para>
    /// <para>Analog steering is quantized to this range by the caller before it reaches the sim. The sim steers by
    /// <see cref="CombinedSteering"/>, which folds the left and right buttons into it.</para>
    /// <para>Immutable and thread-safe.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// SimInput input = SimInput.Pack(SimButtons.Left | SimButtons.Forward, steering: -64);
    /// recording.Append(input);
    /// simulation.Step(input);
    /// </code>
    /// </example>
    public readonly struct SimInput : IEquatable<SimInput>
    {
        /// <summary>The largest steering magnitude; steering runs from -SteeringMax to SteeringMax.</summary>
        public const int SteeringMax = 127;

        private const uint ButtonMask = 0x7Fu;
        private const int SteeringShift = 8;

        /// <summary>The packed bits.</summary>
        public readonly uint Packed;

        private SimInput(uint packed)
        {
            Packed = packed;
        }

        /// <summary>The held buttons.</summary>
        public SimButtons Buttons => (SimButtons)(Packed & ButtonMask);

        /// <summary>Steering, -127 (full left) to 127 (full right).</summary>
        public int Steering => unchecked((sbyte)(byte)(Packed >> SteeringShift));

        /// <summary>
        /// The steering the sim rides with, -127 to 127: <see cref="Steering"/>, minus 127 when
        /// <see cref="SimButtons.Left"/> is held, plus 127 when <see cref="SimButtons.Right"/> is held, clamped to
        /// +-127. A keyboard sends buttons with steering 0 and gets full lock; a gamepad sends its quantized stick
        /// with no buttons; left and right held together cancel, leaving the analog value.
        /// </summary>
        public int CombinedSteering
        {
            get
            {
                int steering = Steering;
                SimButtons buttons = Buttons;
                if ((buttons & SimButtons.Left) != 0)
                {
                    steering -= SteeringMax;
                }

                if ((buttons & SimButtons.Right) != 0)
                {
                    steering += SteeringMax;
                }

                return steering > SteeringMax ? SteeringMax : (steering < -SteeringMax ? -SteeringMax : steering);
            }
        }

        /// <summary>Packs buttons and steering. Steering outside +-127 is clamped.</summary>
        /// <param name="buttons">Held buttons.</param>
        /// <param name="steering">Quantized steering, -127 to 127.</param>
        public static SimInput Pack(SimButtons buttons, int steering)
        {
            if (steering > SteeringMax)
            {
                steering = SteeringMax;
            }
            else if (steering < -SteeringMax)
            {
                steering = -SteeringMax;
            }

            return new SimInput(((uint)buttons & ButtonMask) | ((unchecked((uint)steering) & 0xFFu) << SteeringShift));
        }

        /// <summary>Wraps packed bits read from a recording.</summary>
        /// <param name="packed">The packed bits.</param>
        public static SimInput FromPacked(uint packed) => new SimInput(packed);

        /// <inheritdoc/>
        public bool Equals(SimInput other) => Packed == other.Packed;

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is SimInput other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => Packed.GetHashCode();

        /// <inheritdoc/>
        public override string ToString() => "0x" + Packed.ToString("X8", System.Globalization.CultureInfo.InvariantCulture);
    }
}
