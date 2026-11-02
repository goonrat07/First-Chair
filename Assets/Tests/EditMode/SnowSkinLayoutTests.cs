using System;
using System.Collections.Generic;
using FirstChair.Riding;
using NUnit.Framework;

namespace FirstChair.Riding.Tests
{
    /// <summary>The snow mesh that follows the rider (Story 018): nested square grids, stitched, snapped.</summary>
    public sealed class SnowSkinLayoutTests
    {
        // A small layout keeps the edge checks fast; the story's (3 cm, 3 levels, 128 cells a half) is checked for size.
        private const float Spacing = 0.03f;
        private const int Levels = 3;
        private const int CellsPerHalf = 8;
        private const float Tolerance = 1e-5f;

        // How near a snapped origin 400 m out must be to a whole number of steps, metres: a float there is good to about
        // 0.03 mm. The check is a distance, not the quotient x / step, whose rounding differs between runtimes.
        private const double SnapTolerance = 1e-4;

        [Test]
        public void Write_OneLevel_IsAPlainGrid()
        {
            // Arrange
            var layout = new SnowSkinLayout(Spacing, 1, 4);

            // Act
            float[] xz = new float[2 * layout.VertexCount];
            int[] indices = new int[3 * layout.TriangleCount];
            layout.Write(xz, indices);

            // Assert: 9 x 9 vertices, 8 x 8 cells of two triangles
            Assert.That(layout.VertexCount, Is.EqualTo(81));
            Assert.That(layout.TriangleCount, Is.EqualTo(128));
        }

        [Test]
        public void Write_NestedLevels_ShareEveryInnerEdgeBetweenTwoTriangles()
        {
            // Arrange
            var layout = new SnowSkinLayout(Spacing, Levels, CellsPerHalf);
            float[] xz = new float[2 * layout.VertexCount];
            int[] indices = new int[3 * layout.TriangleCount];

            // Act
            layout.Write(xz, indices);

            // Assert: an edge used once must lie on the outer boundary; a T-junction or a crack would leave an inner
            // edge used once, where a coarse cell meets two fine ones
            var uses = new Dictionary<(int, int), int>();
            for (int t = 0; t < layout.TriangleCount; t++)
            {
                for (int e = 0; e < 3; e++)
                {
                    int a = indices[3 * t + e], b = indices[3 * t + (e + 1) % 3];
                    var key = a < b ? (a, b) : (b, a);
                    uses[key] = uses.TryGetValue(key, out int n) ? n + 1 : 1;
                }
            }

            float half = layout.HalfSize;
            foreach (KeyValuePair<(int, int), int> edge in uses)
            {
                Assert.That(edge.Value, Is.LessThanOrEqualTo(2), "an edge shared by more than two triangles");
                if (edge.Value == 1)
                {
                    int a = edge.Key.Item1, b = edge.Key.Item2;
                    bool onBoundary = OnBoundary(xz, a, half) && OnBoundary(xz, b, half);
                    Assert.That(onBoundary, Is.True, $"inner edge {a}-{b} used by one triangle only");
                }
            }
        }

        [Test]
        public void Write_AnyLayout_FacesEveryTriangleUpAndCoversTheSquare()
        {
            // Arrange
            var layout = new SnowSkinLayout(Spacing, Levels, CellsPerHalf);
            float[] xz = new float[2 * layout.VertexCount];
            int[] indices = new int[3 * layout.TriangleCount];

            // Act
            layout.Write(xz, indices);

            // Assert: every triangle winds so its normal points up in Unity (cross(b - a, c - a).y > 0), and together
            // they cover the square exactly once
            double area = 0;
            for (int t = 0; t < layout.TriangleCount; t++)
            {
                int a = indices[3 * t], b = indices[3 * t + 1], c = indices[3 * t + 2];
                double ux = xz[2 * b] - xz[2 * a], uz = xz[2 * b + 1] - xz[2 * a + 1];
                double vx = xz[2 * c] - xz[2 * a], vz = xz[2 * c + 1] - xz[2 * a + 1];
                double crossY = uz * vx - ux * vz;
                Assert.That(crossY, Is.GreaterThan(0), $"triangle {t} faces down or is degenerate");
                area += crossY / 2;
            }

            double side = 2 * layout.HalfSize;
            Assert.That(area, Is.EqualTo(side * side).Within(1e-6 * side * side));
        }

        [Test]
        public void Write_EachLevel_PutsItsVerticesOnItsOwnSpacing()
        {
            // Arrange
            var layout = new SnowSkinLayout(Spacing, Levels, CellsPerHalf);
            float[] xz = new float[2 * layout.VertexCount];
            int[] indices = new int[3 * layout.TriangleCount];

            // Act
            layout.Write(xz, indices);

            // Assert: a vertex outside level k's inner square lies on level k's spacing, so once the origin is on the
            // coarsest spacing every vertex keeps to its own grid as the mesh moves
            for (int v = 0; v < layout.VertexCount; v++)
            {
                float x = xz[2 * v], z = xz[2 * v + 1];
                float reach = Math.Max(Math.Abs(x), Math.Abs(z));
                int level = 0;
                while (level < Levels - 1 && reach > CellsPerHalf * layout.Spacing(level) + Tolerance)
                {
                    level++;
                }

                float s = layout.Spacing(level);
                Assert.That(Math.Abs(x / s - Math.Round(x / s)), Is.LessThan(1e-3), $"x of vertex {v} off level {level}'s grid");
                Assert.That(Math.Abs(z / s - Math.Round(z / s)), Is.LessThan(1e-3), $"z of vertex {v} off level {level}'s grid");
            }
        }

        [Test]
        public void Size_TheStorysLayout_ReachesAbout15MetresWithUnder400kTriangles()
        {
            // Arrange and act
            var layout = new SnowSkinLayout(0.03f, 3, 128);

            // Assert
            Assert.That(layout.HalfSize, Is.EqualTo(15.36f).Within(Tolerance));
            Assert.That(layout.CoarsestSpacing, Is.EqualTo(0.12f).Within(Tolerance));
            Assert.That(layout.TriangleCount, Is.LessThan(400000));
        }

        [Test]
        public void SnapOrigin_AnyPosition_LandsOnTheCoarsestSpacingWithinOneCellOfIt()
        {
            // Arrange
            var layout = new SnowSkinLayout(Spacing, Levels, CellsPerHalf);
            float coarse = layout.CoarsestSpacing;

            // Act
            layout.SnapOrigin(412.371f, -288.917f, out float x, out float z);

            // Assert
            Assert.That(Math.Abs(x - Math.Round(x / (double)coarse) * coarse), Is.LessThan(SnapTolerance));
            Assert.That(Math.Abs(z - Math.Round(z / (double)coarse) * coarse), Is.LessThan(SnapTolerance));
            Assert.That(412.371f - x, Is.InRange(0f, coarse));
            Assert.That(-288.917f - z, Is.InRange(0f, coarse));
        }

        [Test]
        public void Constructor_ASpacingLevelOrCellCountOutOfRange_Throws()
        {
            // Act and assert
            Assert.Throws<ArgumentOutOfRangeException>(() => new SnowSkinLayout(0f, Levels, CellsPerHalf));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SnowSkinLayout(Spacing, 0, CellsPerHalf));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SnowSkinLayout(Spacing, Levels, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SnowSkinLayout(Spacing, Levels, 7));
        }

        private static bool OnBoundary(float[] xz, int v, float half) =>
            Math.Abs(Math.Abs(xz[2 * v]) - half) < Tolerance || Math.Abs(Math.Abs(xz[2 * v + 1]) - half) < Tolerance;
    }
}
