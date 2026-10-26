using System;
using FirstChair.Riding;
using NUnit.Framework;

namespace FirstChair.Riding.Tests
{
    /// <summary>A ski's track as a ribbon on the snow, laid a point at a time and kept (story 007, tracks that build up).</summary>
    public sealed class TrackRibbonTests
    {
        private const int Capacity = 8;
        private const float Width = 0.1f;
        private const float MinStep = 0.25f;
        private const float Lift = 0.02f;
        private const float Spacing = 0.5f;
        private const float Tolerance = 1e-4f;

        [Test]
        public void Constructor_CapacityBelowTwo_Throws()
        {
            // Act and Assert
            Assert.That(() => new TrackRibbon(1), Throws.InstanceOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void TryAdd_PointsCloserThanMinStepToTheLast_AreSkipped()
        {
            // Arrange
            TrackRibbon ribbon = NewRibbon();
            int added = 0;

            // Act: a ski moving 0.1 m a frame down +Z; points land at 0, 0.3, 0.6 and 0.9 m.
            for (int frame = 0; frame <= 10; frame++)
            {
                if (ribbon.TryAdd(0f, 0f, frame * 0.1f, 0f, 1f, 0f))
                {
                    added++;
                }
            }

            // Assert
            Assert.That(added, Is.EqualTo(4));
            Assert.That(ribbon.Count, Is.EqualTo(4));
        }

        [Test]
        public void Write_PutsEachPointHalfTheWidthEitherSide_AcrossTheTravelInTheSnowsPlane_LiftedAlongTheNormal()
        {
            // Arrange: snow tilted 30 degrees about X; the ski traverses along +X, which lies in that plane.
            V normal = new V(0f, (float)Math.Cos(Math.PI / 6.0), (float)Math.Sin(Math.PI / 6.0));
            V travel = new V(1f, 0f, 0f);
            TrackRibbon ribbon = NewRibbon();
            for (int i = 0; i < 3; i++)
            {
                ribbon.TryAdd(i * Spacing, 0f, 0f, normal.X, normal.Y, normal.Z);
            }

            var buffers = new Buffers();

            // Act
            buffers.Write(ribbon);

            // Assert: the middle point's two vertices.
            V left = buffers.Vertex(2), right = buffers.Vertex(3);
            V across = right - left;
            V middle = (left + right) * 0.5f;
            V expectedMiddle = new V(Spacing, 0f, 0f) + normal * Lift;
            Assert.That(across.Length, Is.EqualTo(Width).Within(Tolerance));
            Assert.That(V.Dot(across, travel), Is.EqualTo(0f).Within(Tolerance));
            Assert.That(V.Dot(across, normal), Is.EqualTo(0f).Within(Tolerance));
            Assert.That((middle - expectedMiddle).Length, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(buffers.Normal(2).X, Is.EqualTo(normal.X).Within(Tolerance));
            Assert.That(buffers.Normal(2).Y, Is.EqualTo(normal.Y).Within(Tolerance));
            Assert.That(buffers.Normal(2).Z, Is.EqualTo(normal.Z).Within(Tolerance));
        }

        [Test]
        public void Write_InATurn_LaysAPointAcrossTheLineThroughItsNeighbours()
        {
            // Arrange: a bend; the middle point's neighbours run (1, 0, 2) apart.
            TrackRibbon ribbon = NewRibbon();
            ribbon.TryAdd(0f, 0f, 0f, 0f, 1f, 0f);
            ribbon.TryAdd(0f, 0f, 1f, 0f, 1f, 0f);
            ribbon.TryAdd(1f, 0f, 2f, 0f, 1f, 0f);
            var buffers = new Buffers();

            // Act
            buffers.Write(ribbon);

            // Assert
            V across = buffers.Vertex(3) - buffers.Vertex(2);
            Assert.That(across.Length, Is.EqualTo(Width).Within(Tolerance));
            Assert.That(V.Dot(across, new V(1f, 0f, 2f)), Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void Write_EveryTriangleFacesUpOffTheSnow()
        {
            // Arrange: an S down the slope on tilted snow.
            V normal = new V(0f, 0.8f, -0.6f);
            TrackRibbon ribbon = NewRibbon();
            for (int i = 0; i < Capacity; i++)
            {
                float z = i * Spacing;
                ribbon.TryAdd((float)Math.Sin(z), z * 0.75f, z, normal.X, normal.Y, normal.Z);
            }

            var buffers = new Buffers();

            // Act
            buffers.Write(ribbon);

            // Assert: Unity draws a triangle's front where (b - a) x (c - a) points.
            Assert.That(buffers.IndexCount, Is.EqualTo((Capacity - 1) * 6));
            for (int t = 0; t < buffers.IndexCount; t += 3)
            {
                V a = buffers.Vertex(buffers.Indices[t]), b = buffers.Vertex(buffers.Indices[t + 1]), c = buffers.Vertex(buffers.Indices[t + 2]);
                Assert.That(V.Dot(V.Cross(b - a, c - a), normal), Is.GreaterThan(0f), "triangle " + (t / 3));
            }
        }

        [Test]
        public void Break_LeavesNoRibbonAcrossTheGap()
        {
            // Arrange: three points, a jump, three more just beyond.
            TrackRibbon ribbon = NewRibbon();
            for (int i = 0; i < 3; i++)
            {
                ribbon.TryAdd(0f, 0f, i * Spacing, 0f, 1f, 0f);
            }

            ribbon.Break();
            for (int i = 3; i < 6; i++)
            {
                ribbon.TryAdd(0f, 0f, i * Spacing, 0f, 1f, 0f);
            }

            var buffers = new Buffers();

            // Act
            buffers.Write(ribbon);

            // Assert: two strips of two segments each, and no triangle joins them.
            Assert.That(ribbon.Count, Is.EqualTo(6));
            Assert.That(buffers.IndexCount, Is.EqualTo(4 * 6));
            AssertNoTriangleJoins(buffers, firstVertexOfSecondStrip: 6);
        }

        [Test]
        public void TryAdd_AStepLongerThanMaxJoin_StartsANewStrip()
        {
            // Arrange: no break, but a step of 4.5 m with MaxJoin 2 m (a reset the view didn't flag).
            TrackRibbon ribbon = NewRibbon();
            ribbon.MaxJoin = 2f;
            ribbon.TryAdd(0f, 0f, 0f, 0f, 1f, 0f);
            ribbon.TryAdd(0f, 0f, 0.5f, 0f, 1f, 0f);
            ribbon.TryAdd(0f, 0f, 5f, 0f, 1f, 0f);
            ribbon.TryAdd(0f, 0f, 5.5f, 0f, 1f, 0f);
            var buffers = new Buffers();

            // Act
            buffers.Write(ribbon);

            // Assert
            Assert.That(buffers.IndexCount, Is.EqualTo(2 * 6));
            AssertNoTriangleJoins(buffers, firstVertexOfSecondStrip: 4);
        }

        [Test]
        public void NextStretch_StartsAtTheLastPoint_SoTheRibbonRunsOn_ButAfterABreakStartsEmpty()
        {
            // Arrange: a full stretch.
            TrackRibbon ribbon = NewRibbon();
            for (int i = 0; i < Capacity; i++)
            {
                ribbon.TryAdd(0f, 0f, i * Spacing, 0f, 1f, 0f);
            }

            bool addedWhenFull = ribbon.TryAdd(0f, 0f, Capacity * Spacing, 0f, 1f, 0f);
            var buffers = new Buffers();

            // Act
            ribbon.NextStretch();
            int countAfterNext = ribbon.Count;
            buffers.Write(ribbon);
            V firstMiddle = (buffers.Vertex(0) + buffers.Vertex(1)) * 0.5f;
            ribbon.TryAdd(0f, 0f, Capacity * Spacing, 0f, 1f, 0f);
            buffers.Write(ribbon);
            int indicesAfterOneMore = buffers.IndexCount;

            for (int i = ribbon.Count; i < Capacity; i++)
            {
                ribbon.TryAdd(0f, 0f, (Capacity + i) * Spacing, 0f, 1f, 0f);
            }

            ribbon.Break();
            ribbon.NextStretch();

            // Assert
            Assert.That(addedWhenFull, Is.False);
            Assert.That(countAfterNext, Is.EqualTo(1));
            Assert.That(firstMiddle.Z, Is.EqualTo((Capacity - 1) * Spacing).Within(Tolerance));
            Assert.That(firstMiddle.Y, Is.EqualTo(Lift).Within(Tolerance));
            Assert.That(indicesAfterOneMore, Is.EqualTo(6));
            Assert.That(ribbon.Count, Is.EqualTo(0));
        }

        [Test]
        public void Write_UvsRunZeroToOneAcross_LeftToRight_AndAlongInWidths()
        {
            // Arrange: two points down +Z on flat snow; facing +Z, left is -X.
            TrackRibbon ribbon = NewRibbon();
            ribbon.TryAdd(0f, 0f, 0f, 0f, 1f, 0f);
            ribbon.TryAdd(0f, 0f, Spacing, 0f, 1f, 0f);
            var buffers = new Buffers();

            // Act
            buffers.Write(ribbon);

            // Assert
            Assert.That(buffers.Vertex(2).X, Is.LessThan(buffers.Vertex(3).X));
            Assert.That(buffers.Uvs[4], Is.EqualTo(0f));
            Assert.That(buffers.Uvs[6], Is.EqualTo(1f));
            Assert.That(buffers.Uvs[5], Is.EqualTo(Spacing / Width).Within(Tolerance));
            Assert.That(buffers.Uvs[7], Is.EqualTo(Spacing / Width).Within(Tolerance));
        }

        private static TrackRibbon NewRibbon() => new TrackRibbon(Capacity) { Width = Width, MinStep = MinStep, Lift = Lift };

        private static void AssertNoTriangleJoins(Buffers buffers, int firstVertexOfSecondStrip)
        {
            for (int t = 0; t < buffers.IndexCount; t += 3)
            {
                bool first = buffers.Indices[t] < firstVertexOfSecondStrip;
                for (int k = 1; k < 3; k++)
                {
                    Assert.That(buffers.Indices[t + k] < firstVertexOfSecondStrip, Is.EqualTo(first), "triangle " + (t / 3));
                }
            }
        }

        // Vertex buffers sized for a full stretch, as the view keeps them.
        private sealed class Buffers
        {
            public readonly float[] Positions = new float[Capacity * 6];
            public readonly float[] Normals = new float[Capacity * 6];
            public readonly float[] Uvs = new float[Capacity * 4];
            public readonly int[] Indices = new int[(Capacity - 1) * 6];

            public int IndexCount { get; private set; }

            public void Write(TrackRibbon ribbon) => IndexCount = ribbon.Write(Positions, Normals, Uvs, Indices);

            public V Vertex(int i) => new V(Positions[3 * i], Positions[3 * i + 1], Positions[3 * i + 2]);

            public V Normal(int i) => new V(Normals[3 * i], Normals[3 * i + 1], Normals[3 * i + 2]);
        }

        private readonly struct V
        {
            public readonly float X, Y, Z;

            public V(float x, float y, float z)
            {
                X = x;
                Y = y;
                Z = z;
            }

            public float Length => (float)Math.Sqrt(Dot(this, this));

            public static V operator +(V a, V b) => new V(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

            public static V operator -(V a, V b) => new V(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

            public static V operator *(V a, float s) => new V(a.X * s, a.Y * s, a.Z * s);

            public static float Dot(V a, V b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

            public static V Cross(V a, V b) => new V(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        }
    }
}
