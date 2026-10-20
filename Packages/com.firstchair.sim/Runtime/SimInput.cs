using System;

namespace FirstChair.Sim
{
    /// <summary>The held buttons in one step's input.</summary>
    [Flags]
    public enum SimButtons : uint
    {
        /// <summary>No button held.</summary>
        None = 0,

        /// <summary>Steer left.</summary>
        Left = 1u << 0,

        /// <summary>Steer right.</summary>
        Right = 1u << 1,

        /// <summary>Forward: tuck, or pole on flats.</summary>
        Forward = 1u << 2,

        /// <summary>Back: brake.</summary>
        Back = 1u << 3,

        /// <summary>Jump.</summary>
        Jump = 1u << 4,

        /// <summary>Grab.</summary>
        Grab = 1u << 5,
    }

    /// <summary>
    /// One step's input packed into one 32-bit integer, the unit the recorder stores.
    /// </summary>
    /// <remarks>
    /// <para>Bit layout: bits 0-5 are <see cref="SimButtons"/>; bits 6-7 are reserved; bits 8-15 hold the
    /// steering value as a signed byte from -127 (full left) to 127 (full right); bits 16-31 are reserved.
    /// Reserved bits are written as zero and ignored when read.</para>
    /// <para>Analog steering is quantized to this range by the caller before it reaches the sim.</para>
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

        private const uint ButtonMask = 0x3Fu;
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
