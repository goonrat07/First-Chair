using System;

namespace FirstChair.Riding
{
    /// <summary>
    /// A chairlift's haul rope as a closed loop (story 010): up one side from the bottom station over the towers to the
    /// top, round the top wheel, down the other side and round the bottom wheel. Chairs sit at even spacing along it and
    /// move with it. Presentation only; engine-free, so it is testable without Unity.
    /// </summary>
    /// <remarks>
    /// Distances along the loop start at its first point and wrap: <see cref="Evaluate"/> takes any distance, negative
    /// or past <see cref="Length"/>. A chair k of n, on a rope moved on by offset, is at offset + k × Length / n.
    /// </remarks>
    /// <example>
    /// <code>
    /// var rope = new CableLoop(xs, ys, zs);
    /// rope.Evaluate(offset + k * rope.Length / chairs, out double x, out double y, out double z, out double dx, out double dz);
    /// double wait = CableLoop.NextArrival(offset, rope.Length / chairs, rope.DistanceAt(boardingPoint));
    /// </code>
    /// </example>
    public sealed class CableLoop
    {
        private readonly double[] _x, _y, _z, _along;

        /// <summary>A loop through the points in order, closing from the last back to the first.</summary>
        /// <param name="x">X of each point, metres.</param>
        /// <param name="y">Y of each point.</param>
        /// <param name="z">Z of each point.</param>
        /// <exception cref="ArgumentException">Fewer than three points, arrays of different lengths, or no length at all.</exception>
        public CableLoop(double[] x, double[] y, double[] z)
        {
            if (x == null || y == null || z == null || x.Length < 3 || y.Length != x.Length || z.Length != x.Length)
            {
                throw new ArgumentException("A cable loop needs three or more points, with x, y and z the same length.");
            }

            int count = x.Length;
            _x = (double[])x.Clone();
            _y = (double[])y.Clone();
            _z = (double[])z.Clone();
            _along = new double[count + 1];
            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                double dx = _x[next] - _x[i], dy = _y[next] - _y[i], dz = _z[next] - _z[i];
                _along[i + 1] = _along[i] + Math.Sqrt(dx * dx + dy * dy + dz * dz);
            }

            Length = _along[count];
            if (!(Length > 0.0))
            {
                throw new ArgumentException("A cable loop must have some length.");
            }
        }

        /// <summary>The loop's length, metres.</summary>
        public double Length { get; }

        /// <summary>How many points the loop runs through.</summary>
        public int Count => _x.Length;

        /// <summary>The distance along the loop to point <paramref name="index"/>, metres.</summary>
        /// <param name="index">A point's index, 0 to <see cref="Count"/> - 1.</param>
        public double DistanceAt(int index) => _along[index];

        /// <summary>Where the rope is at a distance along the loop, and which way it runs there (level, unit length).</summary>
        /// <param name="distance">Metres along the loop from its first point; wraps.</param>
        /// <param name="x">X there.</param>
        /// <param name="y">Y there.</param>
        /// <param name="z">Z there.</param>
        /// <param name="directionX">The rope's direction, X, with Y dropped and the rest made unit length.</param>
        /// <param name="directionZ">The rope's direction, Z.</param>
        public void Evaluate(double distance, out double x, out double y, out double z, out double directionX, out double directionZ)
        {
            double s = distance % Length;
            if (s < 0.0)
            {
                s += Length;
            }

            int low = 0, high = _x.Length;
            while (high - low > 1)
            {
                int middle = (low + high) / 2;
                if (_along[middle] <= s)
                {
                    low = middle;
                }
                else
                {
                    high = middle;
                }
            }

            int next = (low + 1) % _x.Length;
            double span = _along[low + 1] - _along[low];
            double t = span > 0.0 ? (s - _along[low]) / span : 0.0;
            x = _x[low] + (_x[next] - _x[low]) * t;
            y = _y[low] + (_y[next] - _y[low]) * t;
            z = _z[low] + (_z[next] - _z[low]) * t;
            double dx = _x[next] - _x[low], dz = _z[next] - _z[low];
            double level = Math.Sqrt(dx * dx + dz * dz);
            directionX = level > 0.0 ? dx / level : 0.0;
            directionZ = level > 0.0 ? dz / level : 1.0;
        }

        /// <summary>
        /// How much further the rope must move before one of its chairs reaches <paramref name="target"/>: chairs sit every
        /// <paramref name="spacing"/> metres from <paramref name="offset"/>.
        /// </summary>
        /// <param name="offset">How far the rope has moved: chair 0 is at this distance along the loop.</param>
        /// <param name="spacing">Metres between chairs. Positive.</param>
        /// <param name="target">The distance along the loop to reach.</param>
        /// <returns>Metres, from 0 up to (not including) <paramref name="spacing"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="spacing"/> is not positive.</exception>
        public static double NextArrival(double offset, double spacing, double target)
        {
            if (!(spacing > 0.0))
            {
                throw new ArgumentOutOfRangeException(nameof(spacing), spacing, "Chairs must be some distance apart.");
            }

            double gap = (target - offset) % spacing;
            return gap < 0.0 ? gap + spacing : gap;
        }
    }
}
