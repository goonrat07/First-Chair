using System;
using System.Globalization;

namespace FirstChair.Sim
{
    /// <summary>
    /// A signed Q32.32 fixed-point number: a 64-bit integer holding the value times 2^32.
    /// Everything that moves the rider uses it, so every machine and runtime computes the same bits.
    /// </summary>
    /// <remarks>
    /// <para>Range is about -2,147,483,648 to 2,147,483,648 with a step of 2^-32 (about 2.3e-10).
    /// Sim values (metres, metres per second) stay far inside it.</para>
    /// <para>Overflow wraps silently and deterministically: add, subtract, multiply and divide keep
    /// the low 64 bits of the exact result. There is no saturation. Division by zero throws
    /// <see cref="DivideByZeroException"/>.</para>
    /// <para>Any arithmetic that can wrap is done on <c>ulong</c> and cast back. In C# that gives the same
    /// bits as signed math, and it keeps IL2CPP's generated C++ away from signed overflow and shifts of
    /// negative numbers, which C++ leaves undefined. The zero-divisor check is explicit for the same
    /// reason: IL2CPP builds skip the runtime's own check by default.</para>
    /// <para>Rounding: multiply rounds half up, divide rounds half away from zero, <see cref="Sqrt"/>
    /// rounds down. All of it is integer arithmetic; no float, double or System.Math is involved.</para>
    /// <para>There is deliberately no conversion to or from float or double. A view layer that needs a
    /// float computes <c>Raw / 4294967296.0</c> on its own side.</para>
    /// <para>Immutable and thread-safe.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// Fix64 gravity = Fix64.FromRatio(981, 100);     // 9.81
    /// Fix64 step = Fix64.FromRatio(1, 120);          // 1/120 s
    /// Fix64 speedGain = gravity * step;
    /// Fix64 diagonal = Fix64.Sqrt(Fix64.FromInt(2)); // 1.414...
    /// </code>
    /// </example>
    public readonly struct Fix64 : IEquatable<Fix64>
    {
        /// <summary>Number of fractional bits in <see cref="Raw"/>.</summary>
        public const int FractionalBits = 32;

        private const ulong LowMask = 0xFFFFFFFFUL;
        private const ulong RoundingHalf = 0x80000000UL;

        /// <summary>The value 0.</summary>
        public static readonly Fix64 Zero = new Fix64(0L);

        /// <summary>The value 1.</summary>
        public static readonly Fix64 One = new Fix64(1L << FractionalBits);

        /// <summary>The raw Q32.32 bits: the value times 2^32.</summary>
        public readonly long Raw;

        private Fix64(long raw)
        {
            Raw = raw;
        }

        /// <summary>Wraps raw Q32.32 bits without conversion.</summary>
        /// <param name="raw">The value times 2^32.</param>
        public static Fix64 FromRaw(long raw) => new Fix64(raw);

        /// <summary>Converts a whole number exactly.</summary>
        /// <param name="value">The whole number.</param>
        public static Fix64 FromInt(int value) => new Fix64(unchecked((long)((ulong)(long)value << FractionalBits)));

        /// <summary>
        /// Returns <paramref name="numerator"/> / <paramref name="denominator"/>, rounded to the nearest
        /// raw step (half away from zero). Use it for constants: <c>FromRatio(981, 100)</c> is 9.81.
        /// </summary>
        /// <param name="numerator">Whole-number numerator.</param>
        /// <param name="denominator">Whole-number denominator; zero throws <see cref="DivideByZeroException"/>.</param>
        public static Fix64 FromRatio(long numerator, long denominator) => new Fix64(numerator) / new Fix64(denominator);

        /// <summary>Returns the largest whole number not greater than this value.</summary>
        public int FloorToInt() => unchecked((int)(Raw >> FractionalBits));

        /// <summary>Adds two values; wraps on overflow.</summary>
        /// <param name="a">Left operand.</param>
        /// <param name="b">Right operand.</param>
        public static Fix64 operator +(Fix64 a, Fix64 b) => new Fix64(unchecked((long)((ulong)a.Raw + (ulong)b.Raw)));

        /// <summary>Subtracts two values; wraps on overflow.</summary>
        /// <param name="a">Left operand.</param>
        /// <param name="b">Right operand.</param>
        public static Fix64 operator -(Fix64 a, Fix64 b) => new Fix64(unchecked((long)((ulong)a.Raw - (ulong)b.Raw)));

        /// <summary>Negates a value; wraps for the minimum value.</summary>
        /// <param name="value">The value to negate.</param>
        public static Fix64 operator -(Fix64 value) => new Fix64(unchecked((long)(0UL - (ulong)value.Raw)));

        /// <summary>
        /// Multiplies two values, rounding half up. The 128-bit product is built from four 32-bit partial
        /// products, so no wide integer type is needed; the low 64 bits of the shifted result are kept.
        /// </summary>
        /// <param name="a">Left operand.</param>
        /// <param name="b">Right operand.</param>
        public static Fix64 operator *(Fix64 a, Fix64 b)
        {
            unchecked
            {
                long aHigh = a.Raw >> FractionalBits;
                ulong aLow = (ulong)a.Raw & LowMask;
                long bHigh = b.Raw >> FractionalBits;
                ulong bLow = (ulong)b.Raw & LowMask;

                ulong lowLow = aLow * bLow;
                long lowHigh = (long)aLow * bHigh;
                long highLow = aHigh * (long)bLow;
                long highHigh = aHigh * bHigh;

                return new Fix64((long)(((ulong)highHigh << FractionalBits) + (ulong)lowHigh + (ulong)highLow + ((lowLow + RoundingHalf) >> FractionalBits)));
            }
        }

        /// <summary>
        /// Divides two values, rounding half away from zero. Works on magnitudes: the whole part comes from
        /// one integer division, the 32 fractional bits from a shift-subtract loop.
        /// Throws <see cref="DivideByZeroException"/> when <paramref name="b"/> is zero.
        /// </summary>
        /// <param name="a">Dividend.</param>
        /// <param name="b">Divisor.</param>
        public static Fix64 operator /(Fix64 a, Fix64 b)
        {
            if (b.Raw == 0L)
            {
                throw new DivideByZeroException();
            }

            unchecked
            {
                bool negative = (a.Raw < 0) != (b.Raw < 0);
                ulong dividend = a.Raw < 0 ? 0UL - (ulong)a.Raw : (ulong)a.Raw;
                ulong divisor = b.Raw < 0 ? 0UL - (ulong)b.Raw : (ulong)b.Raw;

                ulong whole = dividend / divisor;
                ulong remainder = dividend % divisor;
                ulong fraction = 0UL;
                for (int bit = 0; bit < FractionalBits; bit++)
                {
                    remainder <<= 1;
                    fraction <<= 1;
                    if (remainder >= divisor)
                    {
                        remainder -= divisor;
                        fraction |= 1UL;
                    }
                }

                if ((remainder << 1) >= divisor)
                {
                    fraction++;
                }

                ulong magnitude = (whole << FractionalBits) + fraction;
                return new Fix64((long)(negative ? 0UL - magnitude : magnitude));
            }
        }

        /// <summary>Returns true when both values have the same raw bits.</summary>
        /// <param name="a">Left operand.</param>
        /// <param name="b">Right operand.</param>
        public static bool operator ==(Fix64 a, Fix64 b) => a.Raw == b.Raw;

        /// <summary>Returns true when the raw bits differ.</summary>
        /// <param name="a">Left operand.</param>
        /// <param name="b">Right operand.</param>
        public static bool operator !=(Fix64 a, Fix64 b) => a.Raw != b.Raw;

        /// <summary>Less than.</summary>
        /// <param name="a">Left operand.</param>
        /// <param name="b">Right operand.</param>
        public static bool operator <(Fix64 a, Fix64 b) => a.Raw < b.Raw;

        /// <summary>Greater than.</summary>
        /// <param name="a">Left operand.</param>
        /// <param name="b">Right operand.</param>
        public static bool operator >(Fix64 a, Fix64 b) => a.Raw > b.Raw;

        /// <summary>Less than or equal.</summary>
        /// <param name="a">Left operand.</param>
        /// <param name="b">Right operand.</param>
        public static bool operator <=(Fix64 a, Fix64 b) => a.Raw <= b.Raw;

        /// <summary>Greater than or equal.</summary>
        /// <param name="a">Left operand.</param>
        /// <param name="b">Right operand.</param>
        public static bool operator >=(Fix64 a, Fix64 b) => a.Raw >= b.Raw;

        /// <summary>Absolute value; wraps for the minimum value.</summary>
        /// <param name="value">The value.</param>
        public static Fix64 Abs(Fix64 value) => value.Raw < 0 ? -value : value;

        /// <summary>The smaller of two values.</summary>
        /// <param name="a">First value.</param>
        /// <param name="b">Second value.</param>
        public static Fix64 Min(Fix64 a, Fix64 b) => a.Raw <= b.Raw ? a : b;

        /// <summary>The larger of two values.</summary>
        /// <param name="a">First value.</param>
        /// <param name="b">Second value.</param>
        public static Fix64 Max(Fix64 a, Fix64 b) => a.Raw >= b.Raw ? a : b;

        /// <summary>Limits a value to the range [<paramref name="min"/>, <paramref name="max"/>].</summary>
        /// <param name="value">The value.</param>
        /// <param name="min">Lower bound.</param>
        /// <param name="max">Upper bound; must not be below <paramref name="min"/>.</param>
        public static Fix64 Clamp(Fix64 value, Fix64 min, Fix64 max) => value < min ? min : (value > max ? max : value);

        /// <summary>
        /// Square root, rounded down to the raw step. Computes the exact integer floor square root of
        /// <c>Raw * 2^32</c> two bits at a time, using only 64-bit integers. Returns zero for zero or
        /// negative input.
        /// </summary>
        /// <param name="value">The value.</param>
        public static Fix64 Sqrt(Fix64 value)
        {
            if (value.Raw <= 0)
            {
                return Zero;
            }

            unchecked
            {
                ulong raw = (ulong)value.Raw;
                ulong root = 0UL;
                ulong remainder = 0UL;

                // The radicand is raw << 32: 96 bits, or 48 two-bit groups. The 16 lowest groups are zero.
                for (int group = 47; group >= 0; group--)
                {
                    ulong bits = group >= 16 ? (raw >> (2 * group - 32)) & 3UL : 0UL;
                    remainder = (remainder << 2) | bits;
                    ulong trial = (root << 2) | 1UL;
                    root <<= 1;
                    if (remainder >= trial)
                    {
                        remainder -= trial;
                        root |= 1UL;
                    }
                }

                return new Fix64((long)root);
            }
        }

        /// <inheritdoc/>
        public bool Equals(Fix64 other) => Raw == other.Raw;

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is Fix64 other && Raw == other.Raw;

        /// <inheritdoc/>
        public override int GetHashCode() => Raw.GetHashCode();

        /// <summary>Decimal text with nine fractional digits (truncated), for logs and test messages.</summary>
        public override string ToString()
        {
            ulong magnitude = Raw < 0 ? unchecked((ulong)(-Raw)) : (ulong)Raw;
            ulong whole = magnitude >> FractionalBits;
            ulong billionths = ((magnitude & LowMask) * 1000000000UL) >> FractionalBits;
            return (Raw < 0 ? "-" : string.Empty)
                + whole.ToString(CultureInfo.InvariantCulture)
                + "."
                + billionths.ToString("D9", CultureInfo.InvariantCulture);
        }
    }
}
