using System;

namespace FirstChair.Sim
{
    /// <summary>The rider's sport.</summary>
    public enum Sport : byte
    {
        /// <summary>Skis. The snowboard comes later.</summary>
        Skis = 0,
    }

    /// <summary>
    /// Everything that decides a ride besides the inputs, the heightmap and the tuning. The sim copies it
    /// once at run start, and the recording header stores it ahead of the input stream.
    /// </summary>
    /// <remarks>
    /// <para>The public constructor stamps the current <see cref="Simulation.Version"/>, so game code
    /// cannot start a run under a different version. Only a recording read from bytes can carry another
    /// version, and the <see cref="Replayer"/> refuses it.</para>
    /// <para>Immutable and thread-safe.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var setup = new RunSetup(Sport.Skis, assistLevel: 0, seed: 12345UL,
    ///                          startX: Fix64.FromInt(64), startZ: Fix64.FromInt(4));
    /// var simulation = new Simulation(setup, heightmap, Tuning.Default);
    /// </code>
    /// </example>
    public readonly struct RunSetup : IEquatable<RunSetup>
    {
        /// <summary>The sim version the run was made with.</summary>
        public readonly ushort SimVersion;

        /// <summary>The sport.</summary>
        public readonly Sport Sport;

        /// <summary>Assist level; 0 is none. Not used by the sim yet.</summary>
        public readonly byte AssistLevel;

        /// <summary>Seed of the run's one random generator.</summary>
        public readonly ulong Seed;

        /// <summary>Start position X, metres, in heightmap space.</summary>
        public readonly Fix64 StartX;

        /// <summary>Start position Z, metres, in heightmap space.</summary>
        public readonly Fix64 StartZ;

        /// <summary>Creates a setup for the current <see cref="Simulation.Version"/>.</summary>
        /// <param name="sport">The sport.</param>
        /// <param name="assistLevel">Assist level; 0 is none.</param>
        /// <param name="seed">Seed of the run's random generator.</param>
        /// <param name="startX">Start X, metres.</param>
        /// <param name="startZ">Start Z, metres.</param>
        public RunSetup(Sport sport, byte assistLevel, ulong seed, Fix64 startX, Fix64 startZ)
            : this(Simulation.Version, sport, assistLevel, seed, startX, startZ)
        {
        }

        internal RunSetup(ushort simVersion, Sport sport, byte assistLevel, ulong seed, Fix64 startX, Fix64 startZ)
        {
            SimVersion = simVersion;
            Sport = sport;
            AssistLevel = assistLevel;
            Seed = seed;
            StartX = startX;
            StartZ = startZ;
        }

        /// <inheritdoc/>
        public bool Equals(RunSetup other) =>
            SimVersion == other.SimVersion
            && Sport == other.Sport
            && AssistLevel == other.AssistLevel
            && Seed == other.Seed
            && StartX == other.StartX
            && StartZ == other.StartZ;

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is RunSetup other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = SimVersion;
                hash = hash * 397 ^ (int)Sport;
                hash = hash * 397 ^ AssistLevel;
                hash = hash * 397 ^ Seed.GetHashCode();
                hash = hash * 397 ^ StartX.GetHashCode();
                return hash * 397 ^ StartZ.GetHashCode();
            }
        }
    }
}
