using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FirstChair.Sim;
using NUnit.Framework;

namespace FirstChair.Sim.Tests
{
    /// <summary>
    /// Story 003, acceptance criterion 1, sim side: the generated test slope in Assets/Terrain/TestSlope (written by
    /// tools/terrain/make_test_slope.py) loads through the sim's loaders and holds what its layout says.
    /// These tests read the committed files, so the heightmap and snow map must be pulled from Git LFS.
    /// </summary>
    public class TestSlopeTests
    {
        private const string PackagePath = "Packages/com.firstchair.sim";
        private const string SlopeDirectory = "Assets/Terrain/TestSlope";
        private const string HeightmapFile = "TestSlope.raw";
        private const string SnowFile = "TestSlopeSnow.raw";
        private const string LayoutFile = "TestSlope.txt";
        private const string LfsPointerPrefix = "version https://git-lfs";

        /// <summary>Coarser cells than 0.5 m cannot shape a kicker.</summary>
        private const int MaxCellMm = 500;

        /// <summary>Two R16 steps at the test slope's 160 m height range.</summary>
        private const double HeightToleranceM = 0.005;

        private const int RampSteps = 8;

        /// <summary>How far each key turn swings from the fall line before the other key is pressed, degrees.</summary>
        private const int KeyTurnDegrees = 50;

        /// <summary>The pace is averaged from here, once the rider is up to speed, to here, above the ice patch.</summary>
        private const int PaceStartSecond = 8, PaceEndSecond = 18;

        /// <summary>GMA2's pace on a groomer, km/h: the boundary values are the point of the test.</summary>
        private const double PaceMinKmh = 40, PaceMaxKmh = 60;

        private const double MetresPerSecondToKmh = 3.6;

        /// <summary>Every tree stands in one of these zones, and no canopy reaches a run.</summary>
        private static readonly string[] WoodedZones = { "glade", "treesWest", "treesMiddle", "treesEast" };

        private static readonly string[] RunZones = { "groomer", "park", "runout" };

        /// <summary>The traverse off the groomer's left edge: this far left of the fall line, for up to this long.</summary>
        private const int TraverseDegrees = 60;

        private const int TraverseSteps = 30 * Simulation.StepsPerSecond;

        private static readonly SimInput NoInput = SimInput.Pack(SimButtons.None, 0);

        [Test]
        public void CommittedFiles_LoadThroughTheSimLoaders()
        {
            // Arrange
            string layoutText = File.ReadAllText(SlopePath(LayoutFile));
            byte[] heightBytes = ReadBinary(HeightmapFile);
            byte[] snowBytes = ReadBinary(SnowFile);

            // Act
            SlopeLayout layout = SlopeLayout.Parse(layoutText);
            Heightmap heightmap = Heightmap.FromR16(heightBytes, layout.Spec);
            SnowMap snowMap = SnowMap.FromBytes(snowBytes, heightmap);

            // Assert
            Assert.That(snowMap.TypeAt(layout.StartX, layout.StartZ), Is.EqualTo(SnowType.Groomed));
            Assert.That(layout.Spec.SizeXMm, Is.LessThanOrEqualTo(MaxCellMm * (heightmap.Resolution - 1)), "X cells must be 0.5 m or finer");
            Assert.That(layout.Spec.SizeZMm, Is.LessThanOrEqualTo(MaxCellMm * (heightmap.Resolution - 1)), "Z cells must be 0.5 m or finer");
            foreach (string name in new[] { "groomer", "park", "bowl", "glade", "runout" })
            {
                Assert.That(layout.TryGetZone(name, out _), Is.True, "zone " + name);
            }

            Assert.That(layout.Kickers, Is.Not.Empty);
            Assert.That(layout.Trees, Is.Not.Empty);
        }

        [Test]
        public void SnowMap_HoldsAllFiveSnowTypes()
        {
            // Arrange
            (SlopeLayout layout, Heightmap heightmap, SnowMap snowMap) = LoadSlope();
            var found = new HashSet<SnowType>();

            // Act: every sample.
            for (int row = 0; row < heightmap.Resolution; row++)
            {
                for (int column = 0; column < heightmap.Resolution; column++)
                {
                    found.Add(snowMap.TypeAt(SampleX(layout, heightmap, column), SampleZ(layout, heightmap, row)));
                }
            }

            // Assert
            Assert.That(found, Is.EquivalentTo(new[] { SnowType.Groomed, SnowType.Powder, SnowType.Crud, SnowType.Ice, SnowType.Slush }));
        }

        [Test]
        public void SnowMap_IsTheLayoutZonesPaintedInOrder()
        {
            // Arrange
            (SlopeLayout layout, Heightmap heightmap, SnowMap snowMap) = LoadSlope();
            int mismatches = 0;
            string first = null;

            // Act: every fourth sample each way; the last zone holding a point sets its snow.
            for (int row = 0; row < heightmap.Resolution; row += 4)
            {
                for (int column = 0; column < heightmap.Resolution; column += 4)
                {
                    Fix64 x = SampleX(layout, heightmap, column);
                    Fix64 z = SampleZ(layout, heightmap, row);
                    SnowType expected = layout.Background;
                    foreach (SlopeZone zone in layout.Zones)
                    {
                        if (zone.Contains(x, z))
                        {
                            expected = zone.Snow;
                        }
                    }

                    if (snowMap.TypeAt(x, z) != expected)
                    {
                        mismatches++;
                        first ??= "row " + row + ", column " + column + ": expected " + expected + ", got " + snowMap.TypeAt(x, z);
                    }
                }
            }

            // Assert
            Assert.That(mismatches, Is.Zero, first);
        }

        [Test]
        public void EachKicker_SitsOnItsRampInThePark()
        {
            // Arrange
            (SlopeLayout layout, Heightmap heightmap, _) = LoadSlope();
            Assert.That(layout.TryGetZone("park", out SlopeZone park), Is.True);

            foreach (SlopeKicker kicker in layout.Kickers)
            {
                Fix64 forwardX = Fix64.Sin(kicker.Facing);
                Fix64 forwardZ = Fix64.Cos(kicker.Facing);
                Fix64 besideX = forwardZ * kicker.Width;
                Fix64 besideZ = -forwardX * kicker.Width;
                var rises = new double[RampSteps + 1];

                // Act: the height above the snow one kicker-width to each side, from the foot of the ramp to the lip.
                for (int step = 0; step <= RampSteps; step++)
                {
                    Fix64 back = kicker.Length * Fix64.FromRatio(RampSteps - step, RampSteps);
                    Fix64 x = kicker.X - forwardX * back;
                    Fix64 z = kicker.Z - forwardZ * back;
                    heightmap.Sample(x, z, out Fix64 on, out _);
                    heightmap.Sample(x + besideX, z + besideZ, out Fix64 left, out _);
                    heightmap.Sample(x - besideX, z - besideZ, out Fix64 right, out _);
                    rises[step] = TestTerrain.ToDouble(on) - (TestTerrain.ToDouble(left) + TestTerrain.ToDouble(right)) / 2;
                }

                // Assert
                string name = "kicker at (" + kicker.XMm + ", " + kicker.ZMm + ")";
                Assert.That(park.Contains(kicker.X, kicker.Z), Is.True, name + " is in the park");
                Assert.That(rises[0], Is.EqualTo(0.0).Within(HeightToleranceM), name + ": the ramp starts level with the slope");
                Assert.That(rises[RampSteps], Is.EqualTo(TestTerrain.ToDouble(kicker.Height)).Within(HeightToleranceM), name + ": the lip stands its height above the slope");
                for (int step = 1; step <= RampSteps; step++)
                {
                    Assert.That(rises[step], Is.GreaterThan(rises[step - 1]), name + ": the ramp rises at step " + step);
                }
            }
        }

        [Test]
        public void EachTree_StandsInsideAWoodedZoneClearOfEveryRun()
        {
            // Arrange
            (SlopeLayout layout, _, _) = LoadSlope();
            var wooded = new List<SlopeZone>();
            foreach (string name in WoodedZones)
            {
                Assert.That(layout.TryGetZone(name, out SlopeZone zone), Is.True, "zone " + name);
                wooded.Add(zone);
            }

            var runs = new List<SlopeZone>();
            foreach (string name in RunZones)
            {
                Assert.That(layout.TryGetZone(name, out SlopeZone zone), Is.True, "zone " + name);
                runs.Add(zone);
            }

            foreach (SlopeTree tree in layout.Trees)
            {
                // Act: the canopy's square, against each zone.
                Fix64 west = tree.X - tree.BranchRadius, east = tree.X + tree.BranchRadius;
                Fix64 north = tree.Z - tree.BranchRadius, south = tree.Z + tree.BranchRadius;
                bool inWood = wooded.Exists(zone => zone.Contains(west, north) && zone.Contains(east, south));
                bool onRun = runs.Exists(zone => east >= zone.XMin && west <= zone.XMax && south >= zone.ZMin && north <= zone.ZMax);

                // Assert
                string name = "tree at (" + tree.XMm + ", " + tree.ZMm + ")";
                Assert.That(inWood, Is.True, name + " stands in a wooded zone");
                Assert.That(onRun, Is.False, name + " stays off the runs");
            }
        }

        /// <summary>
        /// The tree lines are in the sim's layout, not scenery (story 003 decisions, 2026-10-24): a rider who steers off
        /// the groomer's left edge rides into the trees there and clips or hits one (story 005's rules).
        /// </summary>
        [Test]
        public void RiderTraversingOffGroomerLeftEdge_RunsIntoTheTreeLine()
        {
            // Arrange
            (SlopeLayout layout, Heightmap heightmap, SnowMap snowMap) = LoadSlope();
            Assert.That(layout.TryGetZone("groomer", out SlopeZone groomer), Is.True);
            Assert.That(layout.TryGetZone("treesWest", out SlopeZone treeLine), Is.True);
            Forest forest = Forest.FromLayout(layout);
            var simulation = new Simulation(new RunSetup(Sport.Skis, 0, 7UL, layout.StartX, layout.StartZ), heightmap, snowMap, forest, Tuning.Default);
            Fix64 traverse = -Fix64.Pi * Fix64.FromRatio(TraverseDegrees, 180);
            int hitTree = -1;

            // Act: hold left until the skis point the traverse angle left of the fall line, then let go and run on.
            for (int step = 0; step < TraverseSteps && hitTree < 0; step++)
            {
                SimButtons key = simulation.State.Heading > traverse ? SimButtons.Left : SimButtons.None;
                simulation.Step(SimInput.Pack(key, 0));
                for (int i = 0; i < simulation.Events.Count; i++)
                {
                    SimEvent hit = simulation.Events[i];
                    if (hit.Kind == SimEventKind.BranchHit || (hit.Kind == SimEventKind.Crash && hit.Cause == CrashCause.Tree))
                    {
                        hitTree = hit.Tree;
                    }
                }
            }

            // Assert
            Assert.That(hitTree, Is.GreaterThanOrEqualTo(0), "the rider must meet a tree");
            SlopeTree tree = forest[hitTree];
            Assert.That(treeLine.Contains(tree.X, tree.Z), Is.True, "the tree is in the line along the groomer's left edge");
            Assert.That(groomer.Contains(tree.X, tree.Z), Is.False);
        }

        [Test]
        public void RiderAtTopOfGroomer_NoInput_SlidesDownhill()
        {
            // Arrange
            (SlopeLayout layout, Heightmap heightmap, SnowMap snowMap) = LoadSlope();
            Assert.That(layout.TryGetZone("groomer", out SlopeZone groomer), Is.True);
            var simulation = new Simulation(new RunSetup(Sport.Skis, 0, 7UL, layout.StartX, layout.StartZ), heightmap, snowMap, Tuning.Default);
            RiderState start = simulation.State;

            // Act
            for (int step = 0; step < 5 * Simulation.StepsPerSecond; step++)
            {
                simulation.Step(NoInput);
            }

            // Assert
            RiderState end = simulation.State;
            Assert.That(groomer.Contains(start.Position.X, start.Position.Z), Is.True, "the start is on the groomer");
            Assert.That(end.Position.Z.Raw, Is.GreaterThan((start.Position.Z + Fix64.FromInt(10)).Raw), "the rider runs down the fall line, +Z");
            Assert.That(end.Position.Y.Raw, Is.LessThan(start.Position.Y.Raw));
            Assert.That(end.Velocity.Z.Raw, Is.GreaterThan(0L));
            Assert.That(end.Position.X, Is.EqualTo(start.Position.X), "the groomer has no cross-slope");
            Assert.That(groomer.Contains(end.Position.X, end.Position.Z), Is.True, "the rider is still on the groomer");
        }

        /// <summary>
        /// The user's playtest (2026-10-23, story 003): riding felt slow. On a keyboard every turn is full lock, and at
        /// the first tuning a full-lock turn out-turned groomed grip above 32 km/h, so key turns skidded down to about
        /// 33 km/h. GMA2's pace on a groomer is 40 to 60 km/h; the 2026-10-24 retune keeps key S-turns inside it.
        /// </summary>
        [Test]
        public void KeyboardSTurnsDownGroomer_FiftyDegreesEachWay_HoldFortyToSixtyKmh()
        {
            // Arrange
            (SlopeLayout layout, Heightmap heightmap, SnowMap snowMap) = LoadSlope();
            Assert.That(layout.TryGetZone("groomer", out SlopeZone groomer), Is.True);
            var simulation = new Simulation(new RunSetup(Sport.Skis, 0, 7UL, layout.StartX, layout.StartZ), heightmap, snowMap, Tuning.Default);
            Fix64 headingLimit = Fix64.Pi * Fix64.FromRatio(KeyTurnDegrees, 180);
            SimButtons key = SimButtons.Right;
            double speedSum = 0;
            int measured = 0;

            // Act: straight for the first second, then hold one key until the heading passes the limit, then the other.
            for (int step = 0; step < PaceEndSecond * Simulation.StepsPerSecond; step++)
            {
                Fix64 heading = simulation.State.Heading;
                if (key == SimButtons.Right && heading > headingLimit)
                {
                    key = SimButtons.Left;
                }
                else if (key == SimButtons.Left && heading < -headingLimit)
                {
                    key = SimButtons.Right;
                }

                simulation.Step(step < Simulation.StepsPerSecond ? NoInput : SimInput.Pack(key, 0));
                RiderState state = simulation.State;
                Assert.That(groomer.Contains(state.Position.X, state.Position.Z) && state.Snow == SnowType.Groomed, Is.True,
                    "the turns must stay on the groomer's own snow, at step " + step);
                if (step >= PaceStartSecond * Simulation.StepsPerSecond)
                {
                    speedSum += TestTerrain.ToDouble(Fix64.Sqrt(Vec3.Dot(state.Velocity, state.Velocity)));
                    measured++;
                }
            }

            // Assert
            double meanKmh = speedSum / measured * MetresPerSecondToKmh;
            Assert.That(meanKmh, Is.InRange(PaceMinKmh, PaceMaxKmh));
        }

        [Test]
        public void Runout_IsLevelForPoling()
        {
            // Arrange
            (SlopeLayout layout, Heightmap heightmap, _) = LoadSlope();
            Assert.That(layout.TryGetZone("runout", out SlopeZone runout), Is.True);
            Fix64 spacing = Fix64.FromInt(8);
            int points = 0;

            // Act and Assert: a grid of points 8 m apart over the whole run-out.
            for (Fix64 z = runout.ZMin; z <= runout.ZMax; z += spacing)
            {
                for (Fix64 x = runout.XMin; x <= runout.XMax; x += spacing)
                {
                    heightmap.Sample(x, z, out _, out Vec3 normal);
                    Assert.That(normal, Is.EqualTo(new Vec3(Fix64.Zero, Fix64.One, Fix64.Zero)), "at (" + x + ", " + z + ")");
                    points++;
                }
            }

            Assert.That(points, Is.GreaterThan(0));
        }

        private static (SlopeLayout Layout, Heightmap Heightmap, SnowMap SnowMap) LoadSlope()
        {
            SlopeLayout layout = SlopeLayout.Parse(File.ReadAllText(SlopePath(LayoutFile)));
            Heightmap heightmap = Heightmap.FromR16(ReadBinary(HeightmapFile), layout.Spec);
            return (layout, heightmap, SnowMap.FromBytes(ReadBinary(SnowFile), heightmap));
        }

        /// <summary>World X of a sample column: column * size / (N - 1), exact for the test slope's 0.5 m cells.</summary>
        private static Fix64 SampleX(SlopeLayout layout, Heightmap heightmap, int column) =>
            Fix64.FromRatio((long)column * layout.Spec.SizeXMm, (long)(heightmap.Resolution - 1) * 1000);

        private static Fix64 SampleZ(SlopeLayout layout, Heightmap heightmap, int row) =>
            Fix64.FromRatio((long)row * layout.Spec.SizeZMm, (long)(heightmap.Resolution - 1) * 1000);

        private static byte[] ReadBinary(string name)
        {
            byte[] bytes = File.ReadAllBytes(SlopePath(name));
            int prefixLength = Math.Min(bytes.Length, LfsPointerPrefix.Length);
            if (Encoding.ASCII.GetString(bytes, 0, prefixLength) == LfsPointerPrefix)
            {
                Assert.Fail(name + " is a Git LFS pointer, not the file: run `git lfs pull` (CI: lfs: true on checkout).");
            }

            return bytes;
        }

        private static string SlopePath(string name) => Path.Combine(RepoRoot(), SlopeDirectory, name);

        private static string RepoRoot()
        {
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, PackagePath, "package.json")))
            {
                directory = directory.Parent;
            }

            Assert.That(directory, Is.Not.Null, "No " + PackagePath + "/package.json above " + TestContext.CurrentContext.TestDirectory);
            return directory.FullName;
        }
    }
}
