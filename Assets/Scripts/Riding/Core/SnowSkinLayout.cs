using System;
using System.Collections.Generic;

namespace FirstChair.Riding
{
    /// <summary>
    /// The layout of the snow mesh that follows the rider (Story 018): nested square grids, the finest in the middle and
    /// each level out twice as coarse, stitched where the spacing changes so no crack can open. Level 0 is a full square
    /// of 2 × <see cref="CellsPerHalf"/> cells a side; each level after it is a square ring of the same cell count a side
    /// around the one before. The whole mesh moves in steps of its coarsest spacing (<see cref="SnapOrigin"/>), so every
    /// vertex stays on its own level's grid and nothing swims as the rider moves. Engine-free, so it is testable without
    /// Unity.
    /// </summary>
    /// <remarks>
    /// Stitching: a coarse cell along a finer level's edge has that level's midpoint on its inner edge; it is fanned into
    /// three triangles from a corner off that edge, so the two levels share every edge and there is no T-junction.
    /// Every triangle winds to face up in Unity (cross(b − a, c − a).y &gt; 0).
    /// </remarks>
    /// <example>
    /// <code>
    /// var layout = new SnowSkinLayout(0.03f, 3, 128);   // 3 cm near the rider, out to 15.36 m each way
    /// var xz = new float[2 * layout.VertexCount];
    /// var indices = new int[3 * layout.TriangleCount];
    /// layout.Write(xz, indices);                         // x and z of each vertex, relative to the mesh's origin
    /// layout.SnapOrigin(rider.x, rider.z, out float x, out float z);
    /// </code>
    /// </example>
    public sealed class SnowSkinLayout
    {
        private readonly List<long> _keys = new List<long>();
        private readonly List<int> _triangles = new List<int>();

        /// <summary>A layout <paramref name="levels"/> deep, <paramref name="baseSpacing"/> apart in the middle.</summary>
        /// <param name="baseSpacing">The finest level's spacing, metres; above 0.</param>
        /// <param name="levels">How many levels, each twice as coarse as the one inside it; at least 1.</param>
        /// <param name="cellsPerHalf">Cells from a level's centre to its outer edge; even and at least 4, so each ring's
        /// hole falls on its own grid.</param>
        /// <exception cref="ArgumentOutOfRangeException">A spacing, level count or cell count out of range.</exception>
        public SnowSkinLayout(float baseSpacing, int levels, int cellsPerHalf)
        {
            if (!(baseSpacing > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(baseSpacing), baseSpacing, "The spacing must be above 0.");
            }

            if (levels < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(levels), levels, "The layout needs at least one level.");
            }

            if (cellsPerHalf < 4 || cellsPerHalf % 2 != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cellsPerHalf), cellsPerHalf, "Cells per half must be even and at least 4.");
            }

            BaseSpacing = baseSpacing;
            Levels = levels;
            CellsPerHalf = cellsPerHalf;
            Build();
        }

        /// <summary>The finest level's spacing, metres.</summary>
        public float BaseSpacing { get; }

        /// <summary>How many levels.</summary>
        public int Levels { get; }

        /// <summary>Cells from each level's centre to its outer edge.</summary>
        public int CellsPerHalf { get; }

        /// <summary>The coarsest level's spacing, metres: the step the mesh moves in.</summary>
        public float CoarsestSpacing => Spacing(Levels - 1);

        /// <summary>Half the side of the whole mesh, metres.</summary>
        public float HalfSize => CellsPerHalf * CoarsestSpacing;

        /// <summary>How many vertices <see cref="Write"/> writes.</summary>
        public int VertexCount => _keys.Count;

        /// <summary>How many triangles <see cref="Write"/> writes.</summary>
        public int TriangleCount => _triangles.Count / 3;

        /// <summary>A level's spacing, metres.</summary>
        /// <param name="level">0 for the finest.</param>
        public float Spacing(int level) => BaseSpacing * (1 << level);

        /// <summary>Writes the vertices' x and z, relative to the mesh's origin, and three indices per triangle.</summary>
        /// <param name="xz">Receives x, z per vertex; at least 2 × <see cref="VertexCount"/> long.</param>
        /// <param name="indices">Receives three vertex indices per triangle; at least 3 × <see cref="TriangleCount"/> long.</param>
        /// <exception cref="ArgumentException">An array is null or too short.</exception>
        public void Write(float[] xz, int[] indices)
        {
            if (xz == null || xz.Length < 2 * VertexCount || indices == null || indices.Length < _triangles.Count)
            {
                throw new ArgumentException("Each array must hold the whole layout.");
            }

            for (int v = 0; v < _keys.Count; v++)
            {
                Unpack(_keys[v], out int ix, out int iz);
                xz[2 * v] = ix * BaseSpacing;
                xz[2 * v + 1] = iz * BaseSpacing;
            }

            _triangles.CopyTo(indices);
        }

        /// <summary>
        /// The mesh's origin for a rider at (<paramref name="x"/>, <paramref name="z"/>): snapped down to the coarsest
        /// spacing, so the rider is always within one coarse cell of the middle and every vertex stays on its grid.
        /// </summary>
        /// <param name="x">The rider's x, metres.</param>
        /// <param name="z">The rider's z, metres.</param>
        /// <param name="originX">The origin's x, metres.</param>
        /// <param name="originZ">The origin's z, metres.</param>
        public void SnapOrigin(float x, float z, out float originX, out float originZ)
        {
            double step = CoarsestSpacing;
            originX = (float)(Math.Floor(x / step) * step);
            originZ = (float)(Math.Floor(z / step) * step);
        }

        // Positions are whole multiples of the base spacing: level k's grid is every 2^k of them.
        private void Build()
        {
            var index = new Dictionary<long, int>();
            int half = CellsPerHalf;
            for (int level = 0; level < Levels; level++)
            {
                int step = 1 << level;
                int outer = half * step;
                int inner = level == 0 ? 0 : half * step / 2;
                for (int cz = -outer; cz < outer; cz += step)
                {
                    for (int cx = -outer; cx < outer; cx += step)
                    {
                        bool inHole = level > 0 && cx >= -inner && cx + step <= inner && cz >= -inner && cz + step <= inner;
                        if (!inHole)
                        {
                            AddCell(index, cx, cz, step);
                        }
                    }
                }
            }
        }

        // One cell, corners in the order that winds up: (x0, z0), (x0, z1), (x1, z1), (x1, z0). A finer level's vertex in
        // the middle of an edge joins the polygon, which is then fanned from a corner off that edge.
        private void AddCell(Dictionary<long, int> index, int x0, int z0, int step)
        {
            int x1 = x0 + step, z1 = z0 + step;
            int[] cornersX = { x0, x0, x1, x1 };
            int[] cornersZ = { z0, z1, z1, z0 };
            var polygon = new List<int>(8);
            var isMid = new List<bool>(8);
            for (int c = 0; c < 4; c++)
            {
                polygon.Add(Vertex(index, cornersX[c], cornersZ[c]));
                isMid.Add(false);
                if (step > 1)
                {
                    int mx = (cornersX[c] + cornersX[(c + 1) % 4]) / 2, mz = (cornersZ[c] + cornersZ[(c + 1) % 4]) / 2;
                    if (index.TryGetValue(Pack(mx, mz), out int mid))
                    {
                        polygon.Add(mid);
                        isMid.Add(true);
                    }
                }
            }

            int n = polygon.Count;
            int fan = 0;
            while (isMid[fan] || isMid[(fan + 1) % n] || isMid[(fan + n - 1) % n])
            {
                fan++;
                if (fan == n)
                {
                    throw new InvalidOperationException("A cell has a finer vertex on more than one edge.");
                }
            }

            for (int k = 1; k < n - 1; k++)
            {
                _triangles.Add(polygon[fan]);
                _triangles.Add(polygon[(fan + k) % n]);
                _triangles.Add(polygon[(fan + k + 1) % n]);
            }
        }

        private int Vertex(Dictionary<long, int> index, int x, int z)
        {
            long key = Pack(x, z);
            if (!index.TryGetValue(key, out int v))
            {
                v = _keys.Count;
                index.Add(key, v);
                _keys.Add(key);
            }

            return v;
        }

        private static long Pack(int x, int z) => ((long)x << 32) | (uint)z;

        private static void Unpack(long key, out int x, out int z)
        {
            x = (int)(key >> 32);
            z = (int)(uint)key;
        }
    }
}
