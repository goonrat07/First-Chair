using System;

namespace FirstChair.Sim
{
    /// <summary>
    /// The whole mutable state of a ride, as one value. Two runs match bit for bit exactly when their
    /// states are equal after every step.
    /// </summary>
    /// <remarks>
    /// <para><see cref="Simulation.State"/> returns a copy: the game layer reads it and draws it, and
    /// writing to the copy changes nothing.</para>
    /// <para>In this story the rider is always on the snow: <see cref="Position"/>.Y is the terrain height
    /// under the rider. Later stories add heading, edging, snow type, air, crashes and Flow.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// RiderState previous = simulation.State;
    /// simulation.Step(input);
    /// RiderState current = simulation.State;
    /// // A view interpolates between previous.Position and current.Position.
    /// </code>
    /// </example>
    public struct RiderState : IEquatable<RiderState>
    {
        /// <summary>Position, metres, in heightmap space (Y up).</summary>
        public Vec3 Position;

        /// <summary>Velocity, metres per second, kept tangent to the surface.</summary>
        public Vec3 Velocity;

        /// <summary>Steps completed since the run started.</summary>
        public int Tick;

        /// <summary>True while the rider is off the heightmap.</summary>
        public bool OutsideHeightmap;

        /// <summary>The input of the last step. It does not move the rider yet (Story 002), but it is part
        /// of the state so a replay fed different inputs cannot match.</summary>
        public SimInput LastInput;

        /// <summary>The run's one random generator, seeded from <see cref="RunSetup.Seed"/>.</summary>
        public SimRandom Rng;

        /// <inheritdoc/>
        public bool Equals(RiderState other) =>
            Position == other.Position
            && Velocity == other.Velocity
            && Tick == other.Tick
            && OutsideHeightmap == other.OutsideHeightmap
            && LastInput.Equals(other.LastInput)
            && Rng.Equals(other.Rng);

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is RiderState other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Position.GetHashCode();
                hash = hash * 397 ^ Velocity.GetHashCode();
                hash = hash * 397 ^ Tick;
                hash = hash * 397 ^ (OutsideHeightmap ? 1 : 0);
                hash = hash * 397 ^ LastInput.GetHashCode();
                return hash * 397 ^ Rng.GetHashCode();
            }
        }

        /// <inheritdoc/>
        public override string ToString() =>
            "Tick " + Tick + " pos " + Position + " vel " + Velocity
            + (OutsideHeightmap ? " outside" : string.Empty) + " input " + LastInput;
    }
}
