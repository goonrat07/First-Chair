using System;

namespace FirstChair.Riding
{
    /// <summary>
    /// A point or direction for the procedural pose math (Story 013), in double precision and in whatever frame the caller
    /// works in (the view passes Unity world space). Core math cannot see UnityEngine, so it has its own small vector.
    /// </summary>
    public readonly struct PoseVector
    {
        /// <summary>X component.</summary>
        public readonly double X;

        /// <summary>Y component.</summary>
        public readonly double Y;

        /// <summary>Z component.</summary>
        public readonly double Z;

        /// <summary>Creates a vector.</summary>
        public PoseVector(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>The zero vector.</summary>
        public static PoseVector Zero => new PoseVector(0.0, 0.0, 0.0);

        /// <summary>Length.</summary>
        public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);

        /// <summary>Sum.</summary>
        public static PoseVector operator +(PoseVector a, PoseVector b) => new PoseVector(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        /// <summary>Difference.</summary>
        public static PoseVector operator -(PoseVector a, PoseVector b) => new PoseVector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        /// <summary>Negation.</summary>
        public static PoseVector operator -(PoseVector a) => new PoseVector(-a.X, -a.Y, -a.Z);

        /// <summary>Scaled.</summary>
        public static PoseVector operator *(PoseVector a, double s) => new PoseVector(a.X * s, a.Y * s, a.Z * s);

        /// <summary>Scaled.</summary>
        public static PoseVector operator *(double s, PoseVector a) => a * s;

        /// <summary>Dot product.</summary>
        public static double Dot(PoseVector a, PoseVector b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        /// <summary>Cross product.</summary>
        public static PoseVector Cross(PoseVector a, PoseVector b) =>
            new PoseVector(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

        /// <summary>The unit vector the same way, or zero for a vector too short to have a direction.</summary>
        public PoseVector Normalized()
        {
            double length = Length;
            return length > 1e-12 ? this * (1.0 / length) : Zero;
        }

        /// <inheritdoc />
        public override string ToString() => "(" + X.ToString("0.####") + ", " + Y.ToString("0.####") + ", " + Z.ToString("0.####") + ")";
    }
}
