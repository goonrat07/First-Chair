using System;

namespace FirstChair.Sim
{
    /// <summary>What the rider does with their body on the snow, chosen each step from the input and the slope.</summary>
    public enum Stance : byte
    {
        /// <summary>Standing: neither forward nor back held.</summary>
        Stand = 0,

        /// <summary>
        /// Tucked: forward held on a slope, or on a flat at or above <see cref="Tuning.PoleSpeedMax"/>. Less air
        /// drag (<see cref="Tuning.TuckAirDragFactor"/>) and slower turning (<see cref="Tuning.TuckTurnFactor"/>).
        /// </summary>
        Tuck = 1,

        /// <summary>Braking: back held. Back wins over forward.</summary>
        Brake = 2,

        /// <summary>Poling: forward held on a flat below <see cref="Tuning.PoleSpeedMax"/>.</summary>
        Pole = 3,
    }

    /// <summary>
    /// The whole mutable state of a ride, as one value. Two runs match bit for bit exactly when their
    /// states are equal after every step.
    /// </summary>
    /// <remarks>
    /// <para><see cref="Simulation.State"/> returns a copy: the game layer reads it and draws it, and
    /// writing to the copy changes nothing.</para>
    /// <para>The rider is always on the snow: <see cref="Position"/>.Y is the terrain height under the rider.
    /// Air, crashes and Flow arrive in later stories.</para>
    /// <para><b>Angles</b> are radians. <see cref="Heading"/> 0 faces +Z and pi/2 faces +X, the yaw a Y-up,
    /// left-handed view (Unity) turns the rider by; positive steering increases it.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// RiderState previous = simulation.State;
    /// simulation.Step(input);
    /// RiderState current = simulation.State;
    /// // A view interpolates between previous and current position and heading,
    /// // leans the skier by current.EdgeAngle and sprays snow while current.Skidding.
    /// </code>
    /// </example>
    public struct RiderState : IEquatable<RiderState>
    {
        /// <summary>Position, metres, in heightmap space (Y up).</summary>
        public Vec3 Position;

        /// <summary>Velocity, metres per second, kept tangent to the surface.</summary>
        public Vec3 Velocity;

        /// <summary>Ski heading seen from above, radians in (-pi, pi]; see the class remarks for the convention.</summary>
        public Fix64 Heading;

        /// <summary>
        /// Edge angle, radians: how far the skis are tipped onto their edges, up to
        /// <see cref="Tuning.MaxEdgeAngle"/> either way. Negative is the left edges (turning left).
        /// </summary>
        public Fix64 EdgeAngle;

        /// <summary>True when the edges could not hold in the last step and the skis skidded.</summary>
        public bool Skidding;

        /// <summary>
        /// After a skidding step, the angle left between the velocity and the skis, radians, 0 to pi/2; zero
        /// while carving.
        /// </summary>
        public Fix64 SlipAngle;

        /// <summary>
        /// The snow the last step rode on: the type at the rider's position when the step began. At the start
        /// of a run, the type at the start position.
        /// </summary>
        public SnowType Snow;

        /// <summary>The stance of the last step.</summary>
        public Stance Stance;

        /// <summary>Steps completed since the run started.</summary>
        public int Tick;

        /// <summary>True while the rider is off the heightmap.</summary>
        public bool OutsideHeightmap;

        /// <summary>The input of the last step.</summary>
        public SimInput LastInput;

        /// <summary>The run's one random generator, seeded from <see cref="RunSetup.Seed"/>.</summary>
        public SimRandom Rng;

        /// <inheritdoc/>
        public bool Equals(RiderState other) =>
            Position == other.Position
            && Velocity == other.Velocity
            && Heading == other.Heading
            && EdgeAngle == other.EdgeAngle
            && Skidding == other.Skidding
            && SlipAngle == other.SlipAngle
            && Snow == other.Snow
            && Stance == other.Stance
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
                hash = hash * 397 ^ Heading.GetHashCode();
                hash = hash * 397 ^ EdgeAngle.GetHashCode();
                hash = hash * 397 ^ (Skidding ? 1 : 0);
                hash = hash * 397 ^ SlipAngle.GetHashCode();
                hash = hash * 397 ^ (int)Snow;
                hash = hash * 397 ^ (int)Stance;
                hash = hash * 397 ^ Tick;
                hash = hash * 397 ^ (OutsideHeightmap ? 1 : 0);
                hash = hash * 397 ^ LastInput.GetHashCode();
                return hash * 397 ^ Rng.GetHashCode();
            }
        }

        /// <inheritdoc/>
        public override string ToString() =>
            "Tick " + Tick + " pos " + Position + " vel " + Velocity + " heading " + Heading + " edge " + EdgeAngle
            + (Skidding ? " skid " + SlipAngle : string.Empty) + " " + Snow + " " + Stance
            + (OutsideHeightmap ? " outside" : string.Empty) + " input " + LastInput;
    }
}
