using System;
using System.IO;
using FirstChair.Riding.Editor;
using FirstChair.Sim;
using NUnit.Framework;
using UnityEngine;

namespace FirstChair.Riding.Tests
{
    /// <summary>
    /// Story 003, acceptance criterion 2: the Unity Terrain built from <c>TestSlope.raw</c> and the sim's
    /// <see cref="Heightmap"/> agree within 1 cm at sampled points.
    /// </summary>
    /// <remarks>
    /// <para><b>Why the samples sit where they do.</b> The sim interpolates <b>bilinearly</b> inside each cell
    /// (<c>Heightmap.Sample</c>: (1-v)((1-u)h00 + u h10) + v((1-u)h01 + u h11)). Unity's terrain surface (mesh and
    /// collider) is made of two <b>triangles</b> per cell, and <c>Terrain.SampleHeight</c> /
    /// <c>TerrainData.GetInterpolatedHeight</c> are believed to follow the triangles (not confirmed in
    /// docs/engine-reference/unity/). On grid vertices and along cell edges every one of these is the same linear
    /// interpolation, so they must agree. Inside a cell they differ by up to |h00 - h10 - h01 + h11| / 4, at the
    /// centre. On the committed test slope that bound is under 7.5 mm everywhere except 146 cells on the three
    /// kicker ramps (x 195.5 to 204.5 m), where it reaches 4.6 cm.</para>
    /// <para>So: vertices and edge midpoints are checked everywhere; cell interiors are checked where the bound
    /// leaves room inside 1 cm; and the kicker cells are measured and reported, with the gap required to stay within
    /// what the triangle-versus-bilinear difference can explain.</para>
    /// <para><b>Precision.</b> Unity is believed to store terrain heights at about 15 bits (0 to 32766), which at
    /// 160 m is up to 2.5 mm of rounding; <see cref="QuantizationBudgetM"/> allows for it.</para>
    /// </remarks>
    public class TerrainParityTests
    {
        private const float ToleranceM = 0.01f;
        private const double QuantizationBudgetM = 0.0025;
        private const int VertexStride = 4;
        private const int InteriorStride = 3;
        private static readonly double[,] InteriorPoints = { { 0.25, 0.75 }, { 0.5, 0.5 }, { 0.75, 0.25 }, { 0.3, 0.3 } };

        private TestSlopeData _slope;
        private TerrainData _data;
        private GameObject _terrainObject;
        private Terrain _terrain;
        private double _cellX;
        private double _cellZ;

        [SetUp]
        public void SetUp()
        {
            string folder = TestSlopeFiles.Folder();
            _slope = TestSlopeData.Load(folder);
            byte[] r16 = File.ReadAllBytes(Path.Combine(folder, TestSlopeData.HeightmapFile));
            _data = TestSlopeTerrainBuilder.Build(r16, _slope.Layout.Spec);
            _terrainObject = Terrain.CreateTerrainGameObject(_data);
            _terrainObject.transform.position = SimCoordinatesUnity.TerrainOrigin(_slope.Layout.Spec);
            _terrain = _terrainObject.GetComponent<Terrain>();
            int cells = _slope.Heightmap.Resolution - 1;
            _cellX = SimCoordinates.ToDouble(_slope.Heightmap.SizeX) / cells;
            _cellZ = SimCoordinates.ToDouble(_slope.Heightmap.SizeZ) / cells;
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_terrainObject);
            UnityEngine.Object.DestroyImmediate(_data);
        }

        [Test]
        public void TerrainData_MatchesTheSimHeightmapSpec()
        {
            Assert.That(_data.heightmapResolution, Is.EqualTo(_slope.Heightmap.Resolution));
            Vector3 expected = SimCoordinatesUnity.TerrainSize(_slope.Layout.Spec);
            Assert.That(_data.size.x, Is.EqualTo(expected.x).Within(1e-3f));
            Assert.That(_data.size.y, Is.EqualTo(expected.y).Within(1e-3f));
            Assert.That(_data.size.z, Is.EqualTo(expected.z).Within(1e-3f));
        }

        [Test]
        public void GridVertices_AgreeWithinOneCentimetre()
        {
            var worst = new Worst();
            int last = _slope.Heightmap.Resolution - 1;
            for (int row = 0; row <= last; row += VertexStride)
            {
                for (int column = 0; column <= last; column += VertexStride)
                {
                    worst.Add(Gap(column, row), column, row);
                }
            }

            Assert.That(worst.Gap, Is.LessThanOrEqualTo(ToleranceM), worst.ToString());
        }

        [Test]
        public void CellEdgeMidpoints_AgreeWithinOneCentimetre()
        {
            var worst = new Worst();
            int last = _slope.Heightmap.Resolution - 1;
            for (int row = 0; row < last; row += VertexStride)
            {
                for (int column = 0; column < last; column += VertexStride)
                {
                    worst.Add(Gap(column + 0.5, row), column + 0.5, row);
                    worst.Add(Gap(column, row + 0.5), column, row + 0.5);
                }
            }

            Assert.That(worst.Gap, Is.LessThanOrEqualTo(ToleranceM), worst.ToString());
        }

        [Test]
        public void CellInteriors_AgreeWithinOneCentimetre_WhereTheInterpolationsCannotDiverge()
        {
            var worst = new Worst();
            int checkedCells = 0;
            int last = _slope.Heightmap.Resolution - 1;
            for (int row = 0; row < last; row += InteriorStride)
            {
                for (int column = 0; column < last; column += InteriorStride)
                {
                    if (TwistBound(column, row) > ToleranceM - QuantizationBudgetM)
                    {
                        continue;
                    }

                    checkedCells++;
                    for (int p = 0; p < InteriorPoints.GetLength(0); p++)
                    {
                        double c = column + InteriorPoints[p, 0];
                        double r = row + InteriorPoints[p, 1];
                        worst.Add(Gap(c, r), c, r);
                    }
                }
            }

            Assert.That(checkedCells, Is.GreaterThan(100000));
            Assert.That(worst.Gap, Is.LessThanOrEqualTo(ToleranceM), worst.ToString());
        }

        [Test]
        public void KickerRampCellCentres_DifferOnlyByTheTriangleBilinearGap()
        {
            var worst = new Worst();
            int kickerCells = 0;
            int last = _slope.Heightmap.Resolution - 1;
            for (int row = 0; row < last; row++)
            {
                for (int column = 0; column < last; column++)
                {
                    double bound = TwistBound(column, row);
                    if (bound <= ToleranceM - QuantizationBudgetM)
                    {
                        continue;
                    }

                    kickerCells++;
                    double gap = Gap(column + 0.5, row + 0.5);
                    worst.Add(gap, column + 0.5, row + 0.5);
                    Assert.That(gap, Is.LessThanOrEqualTo(bound + QuantizationBudgetM), "cell " + column + ", " + row);
                }
            }

            TestContext.WriteLine(kickerCells + " cells where triangle and bilinear can differ by more than "
                + (ToleranceM - QuantizationBudgetM) + " m; " + worst);
            Assert.That(kickerCells, Is.GreaterThan(0), "the kicker ramps should be the cells this test exists for");
        }

        /// <summary>The largest of |SampleHeight - sim| and |GetInterpolatedHeight - sim| at a grid position.</summary>
        private double Gap(double column, double row)
        {
            double x = column * _cellX;
            double z = row * _cellZ;
            _slope.Heightmap.Sample(SimCoordinates.FromMetres(x), SimCoordinates.FromMetres(z), out Fix64 simHeight, out _);
            double sim = SimCoordinates.ToDouble(simHeight);
            float originY = _terrainObject.transform.position.y;
            double sampled = originY + _terrain.SampleHeight(new Vector3((float)x, 0f, (float)z));
            double interpolated = originY + _data.GetInterpolatedHeight((float)(x / _data.size.x), (float)(z / _data.size.z));
            return Math.Max(Math.Abs(sampled - sim), Math.Abs(interpolated - sim));
        }

        /// <summary>The most bilinear and any two-triangle split of this cell can differ: |h00 - h10 - h01 + h11| / 4.</summary>
        private double TwistBound(int column, int row)
        {
            double h00 = SimHeightAt(column, row);
            double h10 = SimHeightAt(column + 1, row);
            double h01 = SimHeightAt(column, row + 1);
            double h11 = SimHeightAt(column + 1, row + 1);
            return Math.Abs(h00 - h10 - h01 + h11) / 4.0;
        }

        private double SimHeightAt(int column, int row)
        {
            _slope.Heightmap.Sample(SimCoordinates.FromMetres(column * _cellX), SimCoordinates.FromMetres(row * _cellZ), out Fix64 height, out _);
            return SimCoordinates.ToDouble(height);
        }

        private sealed class Worst
        {
            public double Gap { get; private set; }
            private double _column;
            private double _row;

            public void Add(double gap, double column, double row)
            {
                if (gap > Gap)
                {
                    Gap = gap;
                    _column = column;
                    _row = row;
                }
            }

            public override string ToString() => "worst gap " + Gap.ToString("F4") + " m at grid (" + _column + ", " + _row + ")";
        }
    }
}
