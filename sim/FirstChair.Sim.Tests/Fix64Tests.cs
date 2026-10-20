using System;
using System.Collections.Generic;
using System.Numerics;
using FirstChair.Sim;
using NUnit.Framework;

namespace FirstChair.Sim.Tests
{
    /// <summary>
    /// Proves Fix64's integer-only arithmetic against exact System.Numerics.BigInteger references
    /// (test code only; the sim never uses BigInteger, Int128 or floating point).
    /// </summary>
    public class Fix64Tests
    {
        private const long One = 1L << 32;

        private static readonly long[] EdgeRaws =
        {
            0L, 1L, -1L, 2L, -2L, long.MaxValue, long.MinValue, long.MaxValue - 1, long.MinValue + 1,
            One, -One, One + 1, One - 1, 0x7FFFFFFFL, 0x80000000L, -0x80000000L, 3L << 31, 1L << 62, -(1L << 62),
        };

        [Test]
        public void Multiply_EdgeAndPseudoRandomOperands_MatchesBigIntegerRoundHalfUp()
        {
            // Arrange
            var failures = new List<string>();

            // Act
            foreach ((long a, long b) in OperandPairs(200_000))
            {
                long expected = WrapToInt64(FloorDivide((BigInteger)a * b + (BigInteger.One << 31), BigInteger.One << 32));
                long actual = (Fix64.FromRaw(a) * Fix64.FromRaw(b)).Raw;
                if (actual != expected)
                {
                    failures.Add(a + " * " + b + ": expected " + expected + ", got " + actual);
                }
            }

            // Assert
            Assert.That(failures, Is.Empty);
        }

        [Test]
        public void Divide_EdgeAndPseudoRandomOperands_MatchesBigIntegerRoundHalfAwayFromZero()
        {
            // Arrange
            var failures = new List<string>();
            int compared = 0;

            // Act
            foreach ((long a, long b) in OperandPairs(200_000))
            {
                if (b == 0)
                {
                    continue;
                }

                BigInteger expected = RoundedQuotient((BigInteger)a << 32, b);
                if (expected > long.MaxValue || expected < long.MinValue)
                {
                    continue; // out of range: wraps by design, not compared
                }

                compared++;
                long actual = (Fix64.FromRaw(a) / Fix64.FromRaw(b)).Raw;
                if (actual != (long)expected)
                {
                    failures.Add(a + " / " + b + ": expected " + expected + ", got " + actual);
                }
            }

            // Assert
            Assert.That(compared, Is.GreaterThan(100_000));
            Assert.That(failures, Is.Empty);
        }

        [Test]
        public void Divide_ByZero_ThrowsDivideByZeroException()
        {
            // Arrange
            Fix64 numerator = Fix64.One;

            // Act and Assert
            Assert.That(() => numerator / Fix64.Zero, Throws.TypeOf<DivideByZeroException>());
        }

        [Test]
        public void Add_PastMaximum_WrapsLikeInt64()
        {
            // Arrange
            Fix64 maximum = Fix64.FromRaw(long.MaxValue);

            // Act
            Fix64 sum = maximum + Fix64.FromRaw(1L);

            // Assert
            Assert.That(sum.Raw, Is.EqualTo(long.MinValue));
        }

        [Test]
        public void Sqrt_EdgeAndPseudoRandomValues_MatchesExactIntegerFloorRoot()
        {
            // Arrange
            var failures = new List<string>();

            // Act
            foreach (long raw in PositiveValues(200_000))
            {
                long expected = (long)IntegerSqrt((BigInteger)raw << 32);
                long actual = Fix64.Sqrt(Fix64.FromRaw(raw)).Raw;
                if (actual != expected)
                {
                    failures.Add("sqrt " + raw + ": expected " + expected + ", got " + actual);
                }
            }

            // Assert
            Assert.That(failures, Is.Empty);
        }

        [Test]
        public void Sqrt_PseudoRandomValues_IsWithinOneRawStepOfDoubleSqrt()
        {
            // Arrange
            double worst = 0.0;

            // Act
            foreach (long raw in PositiveValues(100_000))
            {
                double expectedRaw = Math.Sqrt(raw / 4294967296.0) * 4294967296.0;
                double actualRaw = Fix64.Sqrt(Fix64.FromRaw(raw)).Raw;
                worst = Math.Max(worst, Math.Abs(actualRaw - expectedRaw));
            }

            // Assert
            Assert.That(worst, Is.LessThanOrEqualTo(1.0));
        }

        [TestCase(0L)]
        [TestCase(-1L)]
        [TestCase(-4L << 32)]
        [TestCase(long.MinValue)]
        public void Sqrt_ZeroOrNegative_ReturnsZero(long raw)
        {
            // Arrange
            Fix64 value = Fix64.FromRaw(raw);

            // Act
            Fix64 root = Fix64.Sqrt(value);

            // Assert
            Assert.That(root, Is.EqualTo(Fix64.Zero));
        }

        [TestCase(1L, 120L)]
        [TestCase(2L, 3L)]
        [TestCase(-2L, 3L)]
        [TestCase(2L, -3L)]
        [TestCase(981L, 100L)]
        [TestCase(-7L, 2L)]
        [TestCase(1L, 65535000L)]
        [TestCase(128000L, 1000L)]
        public void FromRatio_WholeNumbers_RoundsToNearestRawStep(long numerator, long denominator)
        {
            // Arrange
            long expected = (long)RoundedQuotient((BigInteger)numerator << 32, denominator);

            // Act
            Fix64 value = Fix64.FromRatio(numerator, denominator);

            // Assert
            Assert.That(value.Raw, Is.EqualTo(expected));
        }

        [TestCase(1L, 2L, 0)]
        [TestCase(-1L, 2L, -1)]
        [TestCase(-3L, 1L, -3)]
        [TestCase(7L, 2L, 3)]
        [TestCase(-7L, 2L, -4)]
        public void FloorToInt_FractionalValues_RoundsTowardNegativeInfinity(long numerator, long denominator, int expected)
        {
            // Arrange
            Fix64 value = Fix64.FromRatio(numerator, denominator);

            // Act
            int floor = value.FloorToInt();

            // Assert
            Assert.That(floor, Is.EqualTo(expected));
        }

        [TestCase(int.MaxValue)]
        [TestCase(int.MinValue)]
        [TestCase(0)]
        [TestCase(-123)]
        public void FromInt_WholeNumber_RoundTripsThroughFloorToInt(int value)
        {
            // Arrange and Act
            int roundTrip = Fix64.FromInt(value).FloorToInt();

            // Assert
            Assert.That(roundTrip, Is.EqualTo(value));
        }

        /// <summary>Edge pairs plus a fixed pseudo-random sequence over several magnitudes. Same every run.</summary>
        private static IEnumerable<(long, long)> OperandPairs(int count)
        {
            foreach (long a in EdgeRaws)
            {
                foreach (long b in EdgeRaws)
                {
                    yield return (a, b);
                }
            }

            ulong state = 0x9E3779B97F4A7C15UL;
            for (int i = 0; i < count; i++)
            {
                yield return (Scaled(ref state, i), Scaled(ref state, i / 4 + 1));
            }
        }

        private static IEnumerable<long> PositiveValues(int count)
        {
            foreach (long raw in EdgeRaws)
            {
                if (raw > 0)
                {
                    yield return raw;
                }
            }

            ulong state = 0x2545F4914F6CDD1DUL;
            for (int i = 0; i < count; i++)
            {
                var raw = (long)(NextXorShift(ref state) >> (1 + i % 63));
                if (raw > 0)
                {
                    yield return raw;
                }
            }
        }

        private static long Scaled(ref ulong state, int mode)
        {
            ulong bits = NextXorShift(ref state);
            switch (mode % 4)
            {
                case 0: return unchecked((long)bits);
                case 1: return (long)(bits >> 20) - (1L << 43);
                case 2: return (long)(bits >> 36) - (1L << 27);
                default: return (long)(bits % 9) - 4;
            }
        }

        private static ulong NextXorShift(ref ulong state)
        {
            state ^= state << 13;
            state ^= state >> 7;
            state ^= state << 17;
            return state;
        }

        private static long WrapToInt64(BigInteger value) => unchecked((long)(ulong)(value & ulong.MaxValue));

        private static BigInteger FloorDivide(BigInteger a, BigInteger b)
        {
            BigInteger quotient = BigInteger.DivRem(a, b, out BigInteger remainder);
            return remainder.Sign != 0 && (remainder.Sign < 0) != (b.Sign < 0) ? quotient - 1 : quotient;
        }

        /// <summary>a / b rounded half away from zero.</summary>
        private static BigInteger RoundedQuotient(BigInteger a, BigInteger b)
        {
            BigInteger magnitude = (2 * BigInteger.Abs(a) + BigInteger.Abs(b)) / (2 * BigInteger.Abs(b));
            return (a.Sign < 0) != (b.Sign < 0) ? -magnitude : magnitude;
        }

        private static BigInteger IntegerSqrt(BigInteger n)
        {
            BigInteger x = BigInteger.One << (int)((n.GetBitLength() + 1) / 2);
            while (true)
            {
                BigInteger y = (x + n / x) >> 1;
                if (y >= x)
                {
                    return x;
                }

                x = y;
            }
        }
    }
}
