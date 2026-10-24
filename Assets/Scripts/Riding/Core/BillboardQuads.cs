using System;

namespace FirstChair.Riding
{
    /// <summary>
    /// The tree's far LOD (story 007, tree-kit Phase 5): crossed quads standing on the trunk base, one per cell of a
    /// side-by-side billboard atlas (<c>EN_Conifer_Billboard.png</c>), quad k turned k * 180 / cells degrees about the
    /// trunk and showing cell k. Two cells make the usual cross: 4 triangles, inside the 2 to 4 the params allow. Plain
    /// arrays, so it is testable without Unity; the scene builder turns them into a Mesh.
    /// </summary>
    /// <example>
    /// <code>
    /// BillboardQuads.Build(2, 3.6f, 14.4f, -0.1f, out float[] positions, out float[] uvs, out int[] triangles);
    /// // positions: x, y, z per vertex (Unity axes, Y up); uvs: u, v per vertex; triangles: three indices each
    /// </code>
    /// </example>
    public static class BillboardQuads
    {
        /// <summary>Builds the quads.</summary>
        /// <param name="cells">Atlas cells side by side, one quad each.</param>
        /// <param name="cellWidth">Width a cell covers, metres.</param>
        /// <param name="cellHeight">Height a cell covers, metres.</param>
        /// <param name="bottom">Height of a cell's bottom edge above the trunk base, metres (negative: below).</param>
        /// <param name="positions">Vertex positions, x, y, z each.</param>
        /// <param name="uvs">Vertex UVs, u, v each.</param>
        /// <param name="triangles">Triangle indices, three each, two triangles per quad.</param>
        /// <exception cref="ArgumentException">No cells, or a cell size that is not positive.</exception>
        public static void Build(int cells, float cellWidth, float cellHeight, float bottom, out float[] positions, out float[] uvs, out int[] triangles)
        {
            if (cells < 1 || !(cellWidth > 0f) || !(cellHeight > 0f))
            {
                throw new ArgumentException("A billboard needs at least one cell and a positive cell size.");
            }

            positions = new float[cells * 4 * 3];
            uvs = new float[cells * 4 * 2];
            triangles = new int[cells * 6];
            float half = cellWidth / 2f;
            for (int k = 0; k < cells; k++)
            {
                double angle = Math.PI * k / cells;
                var acrossX = (float)Math.Cos(angle);
                var acrossZ = (float)-Math.Sin(angle);
                float u0 = (float)k / cells, u1 = (float)(k + 1) / cells;
                // Bottom left, bottom right, top right, top left, seen from the side the quad's own X points right.
                Set(positions, uvs, 4 * k, -half * acrossX, bottom, -half * acrossZ, u0, 0f);
                Set(positions, uvs, 4 * k + 1, half * acrossX, bottom, half * acrossZ, u1, 0f);
                Set(positions, uvs, 4 * k + 2, half * acrossX, bottom + cellHeight, half * acrossZ, u1, 1f);
                Set(positions, uvs, 4 * k + 3, -half * acrossX, bottom + cellHeight, -half * acrossZ, u0, 1f);
                int first = 4 * k;
                triangles[6 * k] = first;
                triangles[6 * k + 1] = first + 2;
                triangles[6 * k + 2] = first + 1;
                triangles[6 * k + 3] = first;
                triangles[6 * k + 4] = first + 3;
                triangles[6 * k + 5] = first + 2;
            }
        }

        private static void Set(float[] positions, float[] uvs, int vertex, float x, float y, float z, float u, float v)
        {
            positions[3 * vertex] = x;
            positions[3 * vertex + 1] = y;
            positions[3 * vertex + 2] = z;
            uvs[2 * vertex] = u;
            uvs[2 * vertex + 1] = v;
        }
    }
}
