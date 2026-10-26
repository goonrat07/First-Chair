using System;

namespace FirstChair.Riding
{
    /// <summary>
    /// A ski's track as a ribbon lying on the snow, laid a point at a time as the ski moves (story 007, the user's "tracks
    /// should build up as in GMA2"). The view keeps every finished stretch of it, so a run's tracks stay on the slope and
    /// the next run's cross them. Display only: the sim never reads it. Engine-free, so it is testable without Unity; the
    /// view (<c>SkiTrack</c>) turns each stretch into a mesh.
    /// </summary>
    /// <remarks>
    /// Points are kept at least <see cref="MinStep"/> apart, each lifted <see cref="Lift"/> off the snow along the snow's
    /// normal. Each point's two vertices sit half the <see cref="Width"/> either side of it, across the line through its
    /// neighbours in the snow's plane; u runs 0 (left) to 1 (right) across, v along in widths from the stretch's start.
    /// A stretch holds <see cref="Capacity"/> points; when it is <see cref="Full"/> the view keeps its mesh and calls
    /// <see cref="NextStretch"/>, which starts the next stretch at the last point so the ribbon runs on. <see cref="Break"/>
    /// lifts the ski (a jump, a crash, a reset): the next point starts a new strip, with no ribbon across the gap, as does
    /// a step longer than <see cref="MaxJoin"/>.
    /// </remarks>
    /// <example>
    /// <code>
    /// var ribbon = new TrackRibbon(256) { Width = 0.09f, MinStep = 0.3f, Lift = 0.015f };
    /// if (ribbon.TryAdd(x, y, z, nx, ny, nz))
    /// {
    ///     int indexCount = ribbon.Write(positions, normals, uvs, indices); // then upload ribbon.VertexCount vertices
    /// }
    /// </code>
    /// </example>
    public sealed class TrackRibbon
    {
        private readonly float[] _x, _y, _z, _nx, _ny, _nz, _metres;
        private readonly bool[] _startsStrip;
        private float _width = 0.09f;
        private float _minStep = 0.3f;
        private float _lift = 0.015f;
        private float _maxJoin = 8f;
        private bool _broken = true;

        /// <summary>A ribbon whose stretches hold <paramref name="capacity"/> points each.</summary>
        /// <param name="capacity">Points per stretch, at least 2.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is below 2.</exception>
        public TrackRibbon(int capacity)
        {
            if (capacity < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "A stretch needs room for at least two points.");
            }

            Capacity = capacity;
            _x = new float[capacity];
            _y = new float[capacity];
            _z = new float[capacity];
            _nx = new float[capacity];
            _ny = new float[capacity];
            _nz = new float[capacity];
            _metres = new float[capacity];
            _startsStrip = new bool[capacity];
        }

        /// <summary>Points per stretch.</summary>
        public int Capacity { get; }

        /// <summary>The ribbon's width, metres; applies to the stretch being laid.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Not positive.</exception>
        public float Width
        {
            get => _width;
            set => _width = value > 0f ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "Width must be positive.");
        }

        /// <summary>The shortest step between points, metres.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Not positive.</exception>
        public float MinStep
        {
            get => _minStep;
            set => _minStep = value > 0f ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "MinStep must be positive.");
        }

        /// <summary>How far each point sits off the snow along its normal, metres, so the ribbon never sinks into it.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Negative.</exception>
        public float Lift
        {
            get => _lift;
            set => _lift = value >= 0f ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "Lift must not be negative.");
        }

        /// <summary>A step longer than this, metres, starts a new strip: the ski was moved, not ridden.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Not positive.</exception>
        public float MaxJoin
        {
            get => _maxJoin;
            set => _maxJoin = value > 0f ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "MaxJoin must be positive.");
        }

        /// <summary>Points in the stretch being laid.</summary>
        public int Count { get; private set; }

        /// <summary>Whether the stretch is full; <see cref="TryAdd"/> refuses points until <see cref="NextStretch"/>.</summary>
        public bool Full => Count >= Capacity;

        /// <summary>Vertices <see cref="Write"/> fills: two per point.</summary>
        public int VertexCount => 2 * Count;

        /// <summary>
        /// Adds a point where the ski meets the snow, unless the last point is closer than <see cref="MinStep"/> or the
        /// stretch is full.
        /// </summary>
        /// <param name="x">The point's X, metres.</param>
        /// <param name="y">The point's Y, metres.</param>
        /// <param name="z">The point's Z, metres.</param>
        /// <param name="nx">The snow's normal there, X; it need not be unit length, and zero means straight up.</param>
        /// <param name="ny">The normal's Y.</param>
        /// <param name="nz">The normal's Z.</param>
        /// <returns>Whether the point was added.</returns>
        public bool TryAdd(float x, float y, float z, float nx, float ny, float nz)
        {
            if (Full)
            {
                return false;
            }

            float length = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
            if (length > 1e-6f)
            {
                nx /= length;
                ny /= length;
                nz /= length;
            }
            else
            {
                nx = 0f;
                ny = 1f;
                nz = 0f;
            }

            x += nx * _lift;
            y += ny * _lift;
            z += nz * _lift;
            bool startsStrip = _broken || Count == 0;
            float metres = 0f;
            if (!startsStrip)
            {
                int last = Count - 1;
                float dx = x - _x[last], dy = y - _y[last], dz = z - _z[last];
                float step = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
                if (step < _minStep)
                {
                    return false;
                }

                startsStrip = step > _maxJoin;
                metres = startsStrip ? 0f : _metres[last] + step;
            }

            int i = Count++;
            _x[i] = x;
            _y[i] = y;
            _z[i] = z;
            _nx[i] = nx;
            _ny[i] = ny;
            _nz[i] = nz;
            _metres[i] = metres;
            _startsStrip[i] = startsStrip;
            _broken = false;
            return true;
        }

        /// <summary>The ski left the snow, or was moved: the next point starts a new strip.</summary>
        public void Break() => _broken = true;

        /// <summary>
        /// Starts the next stretch. Unless the ribbon is broken, it starts at the last point, so the two stretches' meshes
        /// meet; otherwise it starts empty.
        /// </summary>
        public void NextStretch()
        {
            if (Count == 0)
            {
                return;
            }

            if (_broken)
            {
                Count = 0;
                return;
            }

            int last = Count - 1;
            _x[0] = _x[last];
            _y[0] = _y[last];
            _z[0] = _z[last];
            _nx[0] = _nx[last];
            _ny[0] = _ny[last];
            _nz[0] = _nz[last];
            _metres[0] = 0f;
            _startsStrip[0] = true;
            Count = 1;
        }

        /// <summary>
        /// Writes the stretch being laid as a triangle list: <see cref="VertexCount"/> vertices, left then right for each
        /// point, with triangles facing off the snow wherever two points of a strip are joined.
        /// </summary>
        /// <param name="positions">Receives x, y, z per vertex; at least 6 × <see cref="Capacity"/> long.</param>
        /// <param name="normals">Receives the snow's normal per vertex; the same length.</param>
        /// <param name="uvs">Receives u, v per vertex; at least 4 × <see cref="Capacity"/> long.</param>
        /// <param name="indices">Receives three vertex indices per triangle; at least 6 × (<see cref="Capacity"/> - 1) long.</param>
        /// <returns>How many indices were written.</returns>
        /// <exception cref="ArgumentException">An array is null or too short.</exception>
        public int Write(float[] positions, float[] normals, float[] uvs, int[] indices)
        {
            if (positions == null || positions.Length < 6 * Capacity || normals == null || normals.Length < 6 * Capacity
                || uvs == null || uvs.Length < 4 * Capacity || indices == null || indices.Length < 6 * (Capacity - 1))
            {
                throw new ArgumentException("Each array must hold a full stretch.");
            }

            float half = _width * 0.5f;
            int indexCount = 0;
            int stripStart = 0;
            for (int i = 0; i < Count; i++)
            {
                if (_startsStrip[i])
                {
                    stripStart = i;
                }

                int before = i > stripStart ? i - 1 : i;
                int after = i + 1 < Count && !_startsStrip[i + 1] ? i + 1 : i;
                Across(i, _x[after] - _x[before], _y[after] - _y[before], _z[after] - _z[before], out float sx, out float sy, out float sz);
                sx *= half;
                sy *= half;
                sz *= half;

                int left = 2 * i, right = left + 1;
                Put(positions, left, _x[i] - sx, _y[i] - sy, _z[i] - sz);
                Put(positions, right, _x[i] + sx, _y[i] + sy, _z[i] + sz);
                Put(normals, left, _nx[i], _ny[i], _nz[i]);
                Put(normals, right, _nx[i], _ny[i], _nz[i]);
                float v = _metres[i] / _width;
                uvs[2 * left] = 0f;
                uvs[2 * left + 1] = v;
                uvs[2 * right] = 1f;
                uvs[2 * right + 1] = v;

                if (i > stripStart)
                {
                    indices[indexCount++] = left - 2;
                    indices[indexCount++] = left;
                    indices[indexCount++] = left - 1;
                    indices[indexCount++] = left - 1;
                    indices[indexCount++] = left;
                    indices[indexCount++] = right;
                }
            }

            return indexCount;
        }

        // The unit vector across the ribbon at point i: the normal crossed with the direction along it, so it lies in the
        // snow's plane and points to the rider's right. A lone point has no direction; any vector in the plane will do.
        private void Across(int i, float ax, float ay, float az, out float sx, out float sy, out float sz)
        {
            float nx = _nx[i], ny = _ny[i], nz = _nz[i];
            sx = ny * az - nz * ay;
            sy = nz * ax - nx * az;
            sz = nx * ay - ny * ax;
            float length = (float)Math.Sqrt(sx * sx + sy * sy + sz * sz);
            if (length < 1e-6f)
            {
                // The normal crossed with +Z, or with +X when the normal lies along Z.
                sx = Math.Abs(nz) < 0.9f ? ny : 0f;
                sy = Math.Abs(nz) < 0.9f ? -nx : nz;
                sz = Math.Abs(nz) < 0.9f ? 0f : -ny;
                length = (float)Math.Sqrt(sx * sx + sy * sy + sz * sz);
            }

            sx /= length;
            sy /= length;
            sz /= length;
        }

        private static void Put(float[] array, int vertex, float x, float y, float z)
        {
            array[3 * vertex] = x;
            array[3 * vertex + 1] = y;
            array[3 * vertex + 2] = z;
        }
    }
}
