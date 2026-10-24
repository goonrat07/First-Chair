using System;
using NUnit.Framework;

namespace FirstChair.Riding.Tests
{
    /// <summary>
    /// The tree's far LOD (story 007, tree-kit Phase 5): crossed quads, one per atlas cell, each standing on the trunk
    /// base and showing its own cell of the atlas.
    /// </summary>
    public class BillboardQuadsTests
    {
        private const int Cells = 2;
        private const float CellWidth = 3.6f, CellHeight = 14.4f, Bottom = -0.1f;
        private const float Tolerance = 1e-5f;

        [Test]
        public void TwoCells_AreFourTrianglesOnEightVertices()
        {
            // Act
            BillboardQuads.Build(Cells, CellWidth, CellHeight, Bottom, out float[] positions, out float[] uvs, out int[] triangles);

            // Assert
            Assert.That(positions.Length, Is.EqualTo(8 * 3));
            Assert.That(uvs.Length, Is.EqualTo(8 * 2));
            Assert.That(triangles.Length, Is.EqualTo(4 * 3));
            foreach (int index in triangles)
            {
                Assert.That(index, Is.InRange(0, 7));
            }
        }

        [Test]
        public void EachQuad_SpansTheCellFromBottomToTop_CentredOnTheTrunk()
        {
            // Act
            BillboardQuads.Build(Cells, CellWidth, CellHeight, Bottom, out float[] positions, out _, out _);

            // Assert: every vertex is half a cell from the trunk's axis, at the bottom or the top.
            for (int v = 0; v < 8; v++)
            {
                float x = positions[3 * v], y = positions[3 * v + 1], z = positions[3 * v + 2];
                Assert.That((float)Math.Sqrt(x * x + z * z), Is.EqualTo(CellWidth / 2f).Within(Tolerance), "vertex " + v);
                Assert.That(Math.Abs(y - Bottom) < Tolerance || Math.Abs(y - (Bottom + CellHeight)) < Tolerance, Is.True, "vertex " + v);
            }
        }

        [Test]
        public void SecondQuad_IsTheFirstTurnedAQuarter_AndShowsTheSecondCell()
        {
            // Act
            BillboardQuads.Build(Cells, CellWidth, CellHeight, Bottom, out float[] positions, out float[] uvs, out _);

            // Assert: quad 0 lies along X with U in [0, 0.5]; quad 1 along Z with U in [0.5, 1].
            for (int v = 0; v < 4; v++)
            {
                Assert.That(positions[3 * v + 2], Is.EqualTo(0f).Within(Tolerance), "quad 0 lies in the XY plane");
                Assert.That(uvs[2 * v], Is.InRange(0f, 0.5f));
                Assert.That(positions[3 * (v + 4)], Is.EqualTo(0f).Within(Tolerance), "quad 1 lies in the ZY plane");
                Assert.That(uvs[2 * (v + 4)], Is.InRange(0.5f, 1f));
            }
        }

        [Test]
        public void NoCells_Throws()
        {
            // Act and Assert
            Assert.That(() => BillboardQuads.Build(0, CellWidth, CellHeight, Bottom, out _, out _, out _), Throws.ArgumentException);
        }
    }
}
