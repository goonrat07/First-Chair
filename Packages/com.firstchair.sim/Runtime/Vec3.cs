using System;

namespace FirstChair.Sim
{
    /// <summary>
    /// A 3D vector of <see cref="Fix64"/> components. Y is up; X and Z run along the heightmap's
    /// columns and rows.
    /// </summary>
    /// <remarks>Immutable and thread-safe.</remarks>
    /// <example>
    /// <code>
    /// Vec3 velocity = new Vec3(Fix64.Zero, Fix64.Zero, Fix64.FromInt(3));
    /// Vec3 moved = position + velocity * Simulation.StepSeconds;
    /// Fix64 along = Vec3.Dot(velocity, normal);
    /// </code>
    /// </example>
    public readonly struct Vec3 : IEquatable<Vec3>
    {
        /// <summary>The zero vector.</summary>
        public static readonly Vec3 Zero = new Vec3(Fix64.Zero, Fix64.Zero, Fix64.Zero);

        /// <summary>X component (metres, or metres per second for velocities).</summary>
        public readonly Fix64 X;

        /// <summary>Y component, up.</summary>
        public readonly Fix64 Y;

        /// <summary>Z component.</summary>
        public readonly Fix64 Z;

        /// <summary>Creates a vector from its components.</summary>
        /// <param name="x">X component.</param>
        /// <param name="y">Y component.</param>
        /// <param name="z">Z component.</param>
        public Vec3(Fix64 x, Fix64 y, Fix64 z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>Component-wise sum.</summary>
        /// <param name="a">Left operand.</param>
        /// <param name="b">Right operand.</param>
        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        /// <summary>Component-wise difference.</summary>
        /// <param name="a">Left operand.</param>
        /// <param name="b">Right operand.</param>
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        /// <summary>Scales every component by <paramref name="scale"/>.</summary>
        /// <param name="vector">The vector.</param>
        /// <param name="scale">The scale factor.</param>
        public static Vec3 operator *(Vec3 vector, Fix64 scale) => new Vec3(vector.X * scale, vector.Y * scale, vector.Z * scale);

        /// <summary>Returns true when all components have the same raw bits.</summary>
        /// <param name="a">Left operand.</param>
        /// <param name="b">Right operand.</param>
        public static bool operator ==(Vec3 a, Vec3 b) => a.Equals(b);

        /// <summary>Returns true when any component differs.</summary>
        /// <param name="a">Left operand.</param>
        /// <param name="b">Right operand.</param>
        public static bool operator !=(Vec3 a, Vec3 b) => !a.Equals(b);

        /// <summary>Dot product.</summary>
        /// <param name="a">First vector.</param>
        /// <param name="b">Second vector.</param>
        public static Fix64 Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        /// <summary>
        /// Cross product: (a.Y b.Z - a.Z b.Y, a.Z b.X - a.X b.Z, a.X b.Y - a.Y b.X). With Y up,
        /// Cross(up, forward) points to the right in a left-handed view (Unity).
        /// </summary>
        /// <param name="a">First vector.</param>
        /// <param name="b">Second vector.</param>
        public static Vec3 Cross(Vec3 a, Vec3 b) =>
            new Vec3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

        /// <inheritdoc/>
        public bool Equals(Vec3 other) => X == other.X && Y == other.Y && Z == other.Z;

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is Vec3 other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                return (X.GetHashCode() * 397 ^ Y.GetHashCode()) * 397 ^ Z.GetHashCode();
            }
        }

        /// <inheritdoc/>
        public override string ToString() => "(" + X + ", " + Y + ", " + Z + ")";
    }
}
