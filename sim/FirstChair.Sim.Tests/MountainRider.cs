using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FirstChair.Sim;
using NUnit.Framework;

namespace FirstChair.Sim.Tests
{
    /// <summary>
    /// Loads a mountain from <c>Assets/Terrain/&lt;Name&gt;/</c> (written by tools/mountain/mountain.py) through the sim's
    /// loaders, and rides a run's centre line with a scripted rider (Story 010; shared with Story 015's every-mountain
    /// tests). The files are the committed ones, so the heightmaps and snow maps must be pulled from Git LFS.
    /// </summary>
    internal static class MountainRider
    {
        private const string PackagePath = "Packages/com.firstchair.sim";
        private const string TerrainDirectory = "Assets/Terrain";
        private const string LfsPointerPrefix = "version https://git-lfs";

        /// <summary>The test slope's folder name: it is a slope, not a mountain, and has tests of its own.</summary>
        public const string TestSlopeName = "TestSlope";

        /// <summary>The scripted rider looks this far ahead along the centre line, plus <see cref="LookAheadPerSpeed"/>, metres.</summary>
        private const double LookAheadBase = 5.0;

        /// <summary>Extra look-ahead per m/s of speed, seconds.</summary>
        private const double LookAheadPerSpeed = 0.5;

        /// <summary>A heading error this large, radians, steers at full lock.</summary>
        private const double FullLockError = 0.35;

        /// <summary>Below this speed, m/s, the rider pushes with its poles (W) to keep moving across flats.</summary>
        private const double PushBelowSpeed = 3.5;

        /// <summary>A run is ridden within this of its edge, metres, never further off it.</summary>
        public const double OffRunAllowance = 6.0;

        private const int MaxRideSeconds = 480;

        /// <summary>Converts m/s to km/h.</summary>
        public const double MetresPerSecondToKmh = 3.6;

        /// <summary>How fast the scripted rider lets each difficulty go, m/s, braking or quick-turning above it.</summary>
        private static readonly Dictionary<RunDifficulty, double> MaxSpeed = new Dictionary<RunDifficulty, double>
        {
            { RunDifficulty.Green, 11.0 },
            { RunDifficulty.Blue, 18.0 },
            { RunDifficulty.Red, 18.0 },
            { RunDifficulty.Black, 16.0 },
            { RunDifficulty.Park, 14.0 },
        };

        /// <summary>
        /// Every mountain folder under <c>Assets/Terrain/</c> holding its three files, the test slope aside, in ordinal
        /// order: the same rule as the Unity scene builder's MountainCatalog.
        /// </summary>
        public static IReadOnlyList<string> Mountains()
        {
            var names = new List<string>();
            foreach (string folder in Directory.GetDirectories(Path.Combine(RepoRoot(), TerrainDirectory)))
            {
                string name = Path.GetFileName(folder);
                if (name != TestSlopeName && File.Exists(Path.Combine(folder, name + ".txt"))
                    && File.Exists(Path.Combine(folder, name + ".raw")) && File.Exists(Path.Combine(folder, name + "Snow.raw")))
                {
                    names.Add(name);
                }
            }

            names.Sort(StringComparer.Ordinal);
            return names;
        }

        /// <summary>Loads a mountain's layout, heightmap and snow map from its committed files.</summary>
        /// <param name="name">The mountain's name, as its folder and files are named.</param>
        public static (SlopeLayout Layout, Heightmap Heightmap, SnowMap SnowMap) Load(string name)
        {
            SlopeLayout layout = SlopeLayout.Parse(File.ReadAllText(MountainPath(name, name + ".txt")));
            Heightmap heightmap = Heightmap.FromR16(ReadBinary(name, name + ".raw"), layout.Spec);
            return (layout, heightmap, SnowMap.FromBytes(ReadBinary(name, name + "Snow.raw"), heightmap));
        }

        /// <summary>The run with this name; fails the test if there is none.</summary>
        public static SlopeRun FindRun(SlopeLayout layout, string name)
        {
            foreach (SlopeRun run in layout.Runs)
            {
                if (run.Name == name)
                {
                    return run;
                }
            }

            Assert.Fail("The mountain has no run named " + name);
            return null;
        }

        /// <summary>
        /// Rides the run's centre line from its top, steering toward a point ahead on it, braking or quick-turning above
        /// its difficulty's speed and poling below a walking pace, until the rider is well into the base area, crashes
        /// or runs out of time. A run that ends on another carries on down that one.
        /// </summary>
        public static RideResult RideCentreLine(SlopeLayout layout, Heightmap heightmap, SnowMap snowMap, SlopeRun run, SlopeZone baseArea)
        {
            List<(double X, double Z)> path = CentreLine(run, 1.0);
            ContinueDownTheRunItEndsOn(layout, run, baseArea, path);
            var setup = new RunSetup(Sport.Skis, 0, 7UL, Fix64.FromRatio(run.Points[0].XMm, 1000), Fix64.FromRatio(run.Points[0].ZMm, 1000));
            var simulation = new Simulation(setup, heightmap, snowMap, new Forest(layout.Trees), new RailSet(layout.Rails), Tuning.Default);
            double maxSpeed = MaxSpeed[run.Difficulty];
            var result = new RideResult();
            int nearest = 0;
            double speedSum = 0;
            int steps = 0;
            for (; steps < MaxRideSeconds * Simulation.StepsPerSecond; steps++)
            {
                RiderState state = simulation.State;
                double x = TestTerrain.ToDouble(state.Position.X), z = TestTerrain.ToDouble(state.Position.Z);
                double speed = Math.Sqrt(TestTerrain.ToDouble(Vec3.Dot(state.Velocity, state.Velocity)));
                result.Where = $"({x:F0}, {z:F0})";
                if (baseArea.Contains(state.Position.X, state.Position.Z) && z >= TestTerrain.ToDouble(baseArea.ZMin) + 10.0)
                {
                    result.ReachedBase = true;
                    break;
                }

                // The nearest centre-line point, searched a little behind and ahead of the last one.
                double best = double.MaxValue;
                int from = Math.Max(0, nearest - 20), to = Math.Min(path.Count - 1, nearest + 60);
                for (int i = from; i <= to; i++)
                {
                    double dx = path[i].X - x, dz = path[i].Z - z, d = dx * dx + dz * dz;
                    if (d < best)
                    {
                        best = d;
                        nearest = i;
                    }
                }

                result.FurthestOff = Math.Max(result.FurthestOff, Math.Sqrt(best));
                int target = Math.Min(path.Count - 1, nearest + (int)(LookAheadBase + LookAheadPerSpeed * speed));
                double desired = Math.Atan2(path[target].X - x, path[target].Z - z);
                double error = Wrap(desired - TestTerrain.ToDouble(state.Heading));
                int steering = (int)Math.Round(Math.Max(-1.0, Math.Min(1.0, error / FullLockError)) * SimInput.SteeringMax);
                SimButtons buttons = SimButtons.None;
                if (state.Airborne)
                {
                    // Steering in the air spins the rider; a rider following a run holds still until it lands.
                    steering = 0;
                }
                else if (speed > maxSpeed)
                {
                    // Straight: brake. Turning: quick-turn, the speed check that still steers.
                    if (Math.Abs(error) < 0.2)
                    {
                        buttons = SimButtons.Back;
                        steering = 0;
                    }
                    else
                    {
                        buttons = SimButtons.Quick;
                    }
                }
                else if (speed < PushBelowSpeed)
                {
                    buttons = SimButtons.Forward;
                }

                simulation.Step(SimInput.Pack(buttons, steering));
                RiderState after = simulation.State;
                if (after.Airborne && !state.Airborne)
                {
                    result.LastTakeoff = $"took off at ({x:F0}, {z:F0}) at {speed * MetresPerSecondToKmh:F0} km/h, step {steps}";
                }

                if (after.Crashed)
                {
                    result.Crashed = true;
                    string cause = "";
                    foreach (SimEvent e in simulation.Events)
                    {
                        if (e.Kind == SimEventKind.Crash)
                        {
                            cause = " (" + e.Cause + ")";
                        }
                    }

                    result.Where = $"({TestTerrain.ToDouble(after.Position.X):F0}, {TestTerrain.ToDouble(after.Position.Z):F0}), step {steps}{cause}; last {result.LastTakeoff}";
                    break;
                }

                double afterSpeed = Math.Sqrt(TestTerrain.ToDouble(Vec3.Dot(after.Velocity, after.Velocity)));
                speedSum += afterSpeed;
                result.TopSpeed = Math.Max(result.TopSpeed, afterSpeed);
            }

            result.Seconds = (double)steps / Simulation.StepsPerSecond;
            result.MeanSpeed = steps > 0 ? speedSum / steps : 0.0;
            return result;
        }

        /// <summary>
        /// A run that ends on another rather than in the base area (The Chute ends on Kettle Run) carries on down that
        /// one: the path gets the other run's centre line from its point nearest this run's end.
        /// </summary>
        private static void ContinueDownTheRunItEndsOn(SlopeLayout layout, SlopeRun run, SlopeZone baseArea, List<(double X, double Z)> path)
        {
            (double X, double Z) end = path[path.Count - 1];
            if (baseArea.Contains(Fix64.FromRatio((long)(end.X * 1000.0), 1000), Fix64.FromRatio((long)(end.Z * 1000.0), 1000)))
            {
                return;
            }

            List<(double X, double Z)> best = null;
            int bestIndex = 0;
            double bestDistance = double.MaxValue;
            foreach (SlopeRun other in layout.Runs)
            {
                if (other == run)
                {
                    continue;
                }

                List<(double X, double Z)> line = CentreLine(other, 1.0);
                for (int i = 0; i < line.Count; i++)
                {
                    double distance = Distance(line[i], end);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = line;
                        bestIndex = i;
                    }
                }
            }

            Assert.That(bestDistance, Is.LessThan(30.0), run.Name + " ends neither in the base area nor on another run");
            path.AddRange(best.GetRange(bestIndex + 1, best.Count - bestIndex - 1));
        }

        /// <summary>
        /// The run's centre line every <paramref name="step"/> metres or so, as the generator draws it: a centripetal
        /// Catmull-Rom spline through the layout's points. The rider only follows it, so doubles are fine here.
        /// </summary>
        private static List<(double X, double Z)> CentreLine(SlopeRun run, double step)
        {
            int count = run.Points.Count;
            var p = new (double X, double Z)[count + 2];
            for (int i = 0; i < count; i++)
            {
                p[i + 1] = (run.Points[i].XMm / 1000.0, run.Points[i].ZMm / 1000.0);
            }

            p[0] = (2 * p[1].X - p[2].X, 2 * p[1].Z - p[2].Z);
            p[count + 1] = (2 * p[count].X - p[count - 1].X, 2 * p[count].Z - p[count - 1].Z);
            var line = new List<(double X, double Z)> { p[1] };
            for (int i = 1; i < count; i++)
            {
                (double X, double Z) p0 = p[i - 1], p1 = p[i], p2 = p[i + 1], p3 = p[i + 2];
                double t0 = 0.0;
                double t1 = t0 + Math.Sqrt(Distance(p0, p1));
                double t2 = t1 + Math.Sqrt(Distance(p1, p2));
                double t3 = t2 + Math.Sqrt(Distance(p2, p3));
                int pieces = Math.Max(2, (int)(Distance(p1, p2) / step));
                for (int k = 1; k <= pieces; k++)
                {
                    double t = t1 + (t2 - t1) * k / pieces;
                    var a1 = Mix(p0, p1, (t1 - t) / (t1 - t0), (t - t0) / (t1 - t0));
                    var a2 = Mix(p1, p2, (t2 - t) / (t2 - t1), (t - t1) / (t2 - t1));
                    var a3 = Mix(p2, p3, (t3 - t) / (t3 - t2), (t - t2) / (t3 - t2));
                    var b1 = Mix(a1, a2, (t2 - t) / (t2 - t0), (t - t0) / (t2 - t0));
                    var b2 = Mix(a2, a3, (t3 - t) / (t3 - t1), (t - t1) / (t3 - t1));
                    line.Add(Mix(b1, b2, (t2 - t) / (t2 - t1), (t - t1) / (t2 - t1)));
                }
            }

            return line;
        }

        private static (double X, double Z) Mix((double X, double Z) a, (double X, double Z) b, double wa, double wb) =>
            (a.X * wa + b.X * wb, a.Z * wa + b.Z * wb);

        private static double Distance((double X, double Z) a, (double X, double Z) b) =>
            Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));

        private static double Wrap(double angle)
        {
            while (angle > Math.PI)
            {
                angle -= 2.0 * Math.PI;
            }

            while (angle < -Math.PI)
            {
                angle += 2.0 * Math.PI;
            }

            return angle;
        }

        private static byte[] ReadBinary(string mountain, string file)
        {
            byte[] bytes = File.ReadAllBytes(MountainPath(mountain, file));
            int prefixLength = Math.Min(bytes.Length, LfsPointerPrefix.Length);
            if (Encoding.ASCII.GetString(bytes, 0, prefixLength) == LfsPointerPrefix)
            {
                Assert.Fail(file + " is a Git LFS pointer, not the file: run `git lfs pull` (CI: lfs: true on checkout).");
            }

            return bytes;
        }

        private static string MountainPath(string mountain, string file) => Path.Combine(RepoRoot(), TerrainDirectory, mountain, file);

        /// <summary>
        /// The repository's root: the folder above the test assembly holding the sim package. Found from the assembly's
        /// own location, so it also works while NUnit is still discovering tests.
        /// </summary>
        private static string RepoRoot()
        {
            string start = Path.GetDirectoryName(typeof(MountainRider).Assembly.Location) ?? Directory.GetCurrentDirectory();
            var directory = new DirectoryInfo(start);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, PackagePath, "package.json")))
            {
                directory = directory.Parent;
            }

            Assert.That(directory, Is.Not.Null, "No " + PackagePath + "/package.json above " + start);
            return directory.FullName;
        }

        /// <summary>How a scripted ride went.</summary>
        internal sealed class RideResult
        {
            public bool ReachedBase;
            public bool Crashed;
            public double Seconds;
            public double MeanSpeed;
            public double TopSpeed;
            public double FurthestOff;
            public string Where = "";
            public string LastTakeoff = "none";
        }
    }
}
