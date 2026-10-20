using System;

namespace FirstChair.Sim
{
    /// <summary>
    /// The sim's one random generator (SplitMix64). It is seeded from <see cref="RunSetup.Seed"/> and
    /// lives inside <see cref="RiderState"/>, so its state is part of every replay comparison.
    /// Nothing draws from it yet; later stories that need randomness take it from here, never from
    /// System.Random, time or hash codes.
    /// </summary>
    /// <remarks>A mutable struct: call <see cref="NextU64"/> on a field or a ref, not on a copy.
    /// Not thread-safe.</remarks>
    /// <example>
    /// <code>
    /// var random = new SimRandom(setup.Seed);
    /// ulong value = random.NextU64();
    /// </code>
    /// </example>
    public struct SimRandom : IEquatable<SimRandom>
    {
        private const ulong Increment = 0x9E3779B97F4A7C15UL;
        private const ulong MixA = 0xBF58476D1CE4E5B9UL;
        private const ulong MixB = 0x94D049BB133111EBUL;

        private ulong _state;

        /// <summary>Creates a generator from a seed.</summary>
        /// <param name="seed">The seed, normally <see cref="RunSetup.Seed"/>.</param>
        public SimRandom(ulong seed)
        {
            _state = seed;
        }

        /// <summary>The generator's internal state, for comparison and hashing.</summary>
        public ulong State => _state;

        /// <summary>Advances the generator and returns 64 random bits.</summary>
        public ulong NextU64()
        {
            unchecked
            {
                _state += Increment;
                ulong z = _state;
                z = (z ^ (z >> 30)) * MixA;
                z = (z ^ (z >> 27)) * MixB;
                return z ^ (z >> 31);
            }
        }

        /// <inheritdoc/>
        public bool Equals(SimRandom other) => _state == other._state;

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is SimRandom other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => _state.GetHashCode();
    }
}
