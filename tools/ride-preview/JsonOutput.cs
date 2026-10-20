using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FirstChair.Sim;

namespace FirstChair.Tools.RidePreview
{
    /// <summary>
    /// Writes rides.json: the slope (downsampled height and snow grids plus the layout's features) and every ride's
    /// samples, events and score. Numbers carry at most 3 decimals (heights 2). The file is written to a temporary name
    /// and moved into place, so a failed run never leaves a half-written file for the viewer.
    /// </summary>
    internal static class JsonOutput
    {
        public const int GridResolution = 257;

        public static long Write(string path, Slope slope, IReadOnlyList<RideResult> rides)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + ".tmp";
            using (FileStream stream = File.Create(temporary))
            using (var w = new Utf8JsonWriter(stream))
            {
                w.WriteStartObject();
                w.WriteNumber("format", 1);
                w.WriteStartObject("sim");
                w.WriteNumber("version", Simulation.Version);
                w.WriteNumber("stepsPerSecond", Simulation.StepsPerSecond);
                w.WriteNumber("sampleEverySteps", RideRunner.SampleEvery);
                w.WriteEndObject();
                WriteUnits(w);
                WriteSlope(w, slope);
                w.WriteStartArray("rides");
                foreach (RideResult ride in rides)
                {
                    WriteRide(w, ride);
                }

                w.WriteEndArray();
                w.WriteEndObject();
            }

            File.Move(temporary, path, true);
            return new FileInfo(path).Length;
        }

        private static void WriteUnits(Utf8JsonWriter w)
        {
            w.WriteStartObject("units");
            w.WriteString("length", "metres; x along heightmap columns, z along rows (z = 0 is the top, the fall line runs toward +z), y up");
            w.WriteString("time", "seconds (t); tick = sim step at 120 per second");
            w.WriteString("speed", "m/s");
            w.WriteString("heading", "radians, clockwise seen from above, 0 = +z, pi/2 = +x (Unity yaw); wrapped to [-pi, pi]");
            w.WriteString("edge", "radians, signed like steering (+ = right)");
            w.WriteString("eventAngles", "degrees (spinDeg, flipDeg, landingAngleDeg)");
            w.WriteString("flow", "0 to 1");
            w.WriteString("facing", "degrees, same convention as heading");
            w.WriteEndObject();
        }

        private static void WriteSlope(Utf8JsonWriter w, Slope slope)
        {
            SlopeLayout layout = slope.Layout;
            int n = GridResolution;
            w.WriteStartObject("slope");
            Number(w, "sizeX", slope.SizeX);
            Number(w, "sizeZ", slope.SizeZ);
            Number(w, "minHeight", Num.Mm(layout.Spec.MinHeightMm));
            Number(w, "maxHeight", Num.Mm(layout.Spec.MaxHeightMm));

            w.WriteStartObject("grid");
            w.WriteNumber("resolution", n);
            Number(w, "cellX", slope.SizeX / (n - 1));
            Number(w, "cellZ", slope.SizeZ / (n - 1));
            w.WriteString("order", "row-major: index = row * resolution + column; x = column * cellX, z = row * cellZ");
            w.WriteEndObject();

            w.WriteStartArray("heights");
            for (int row = 0; row < n; row++)
            {
                for (int column = 0; column < n; column++)
                {
                    double height = slope.HeightAt(column * slope.SizeX / (n - 1), row * slope.SizeZ / (n - 1), out _);
                    w.WriteNumberValue(Math.Round(height, 2) + 0.0);
                }
            }

            w.WriteEndArray();

            w.WriteStartArray("snow");
            for (int row = 0; row < n; row++)
            {
                for (int column = 0; column < n; column++)
                {
                    w.WriteNumberValue(slope.SnowAt(column * slope.SizeX / (n - 1), row * slope.SizeZ / (n - 1)));
                }
            }

            w.WriteEndArray();

            w.WriteStartObject("snowTypes");
            foreach (SnowType type in Enum.GetValues(typeof(SnowType)).Cast<SnowType>().OrderBy(t => (int)t))
            {
                w.WriteString(((int)type).ToString(), type.ToString());
            }

            w.WriteEndObject();
            w.WriteString("background", layout.Background.ToString());

            w.WriteStartObject("start");
            Number(w, "x", Num.Mm(layout.StartXMm));
            Number(w, "z", Num.Mm(layout.StartZMm));
            w.WriteEndObject();

            w.WriteStartArray("zones");
            foreach (SlopeZone zone in layout.Zones)
            {
                w.WriteStartObject();
                w.WriteString("name", zone.Name);
                w.WriteString("snow", zone.Snow.ToString());
                Number(w, "xMin", Num.Mm(zone.XMinMm));
                Number(w, "zMin", Num.Mm(zone.ZMinMm));
                Number(w, "xMax", Num.Mm(zone.XMaxMm));
                Number(w, "zMax", Num.Mm(zone.ZMaxMm));
                w.WriteEndObject();
            }

            w.WriteEndArray();

            w.WriteStartArray("kickers");
            foreach (SlopeKicker kicker in layout.Kickers)
            {
                w.WriteStartObject();
                Number(w, "x", Num.Mm(kicker.XMm));
                Number(w, "z", Num.Mm(kicker.ZMm));
                Number(w, "y", slope.HeightAt(Num.Mm(kicker.XMm), Num.Mm(kicker.ZMm), out _));
                Number(w, "facing", kicker.FacingMillidegrees / 1000.0);
                Number(w, "length", Num.Mm(kicker.LengthMm));
                Number(w, "width", Num.Mm(kicker.WidthMm));
                Number(w, "height", Num.Mm(kicker.HeightMm));
                w.WriteEndObject();
            }

            w.WriteEndArray();

            w.WriteStartArray("trees");
            foreach (SlopeTree tree in layout.Trees)
            {
                w.WriteStartObject();
                Number(w, "x", Num.Mm(tree.XMm));
                Number(w, "z", Num.Mm(tree.ZMm));
                Number(w, "y", slope.HeightAt(Num.Mm(tree.XMm), Num.Mm(tree.ZMm), out _));
                Number(w, "trunk", Num.Mm(tree.TrunkRadiusMm));
                Number(w, "branch", Num.Mm(tree.BranchRadiusMm));
                w.WriteEndObject();
            }

            w.WriteEndArray();

            w.WriteStartArray("rails");
            foreach (SlopeRail rail in layout.Rails)
            {
                w.WriteStartObject();
                w.WriteString("kind", rail.Box ? "box" : "rail");
                Number(w, "x0", Num.Mm(rail.X0Mm));
                Number(w, "z0", Num.Mm(rail.Z0Mm));
                Number(w, "x1", Num.Mm(rail.X1Mm));
                Number(w, "z1", Num.Mm(rail.Z1Mm));
                Number(w, "height", Num.Mm(rail.HeightMm));
                Number(w, "width", Num.Mm(rail.WidthMm));
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteEndObject();
        }

        private static void WriteRide(Utf8JsonWriter w, RideResult ride)
        {
            w.WriteStartObject();
            w.WriteString("name", ride.Script.Name);
            w.WriteString("description", ride.Script.Description);
            w.WriteStartObject("start");
            Number(w, "x", ride.Script.StartX);
            Number(w, "z", ride.Script.StartZ);
            w.WriteEndObject();
            Number(w, "duration", ride.Last.Tick / (double)Simulation.StepsPerSecond);
            w.WriteString("end", ride.EndReason);
            w.WriteNumber("score", ride.Score.Total);

            w.WriteStartObject("counts");
            foreach (KeyValuePair<string, int> count in ride.Counts)
            {
                w.WriteNumber(count.Key, count.Value);
            }

            w.WriteEndObject();

            w.WriteStartArray("samples");
            foreach (RideSample s in ride.Samples)
            {
                w.WriteStartObject();
                Number(w, "t", s.Tick / (double)Simulation.StepsPerSecond);
                Number(w, "x", s.X);
                Number(w, "y", s.Y);
                Number(w, "z", s.Z);
                Number(w, "speed", s.Speed);
                Number(w, "heading", s.Heading);
                Number(w, "edge", s.Edge);
                w.WriteString("stance", s.Stance.ToString());
                w.WriteBoolean("airborne", s.Airborne);
                w.WriteBoolean("grinding", s.Grinding);
                w.WriteBoolean("crashed", s.Crashed);
                Number(w, "flow", s.Flow);
                w.WriteBoolean("skidding", s.Skidding);
                w.WriteNumber("snow", s.Snow);
                w.WriteEndObject();
            }

            w.WriteEndArray();

            w.WriteStartArray("events");
            foreach (RideEvent e in ride.Events)
            {
                w.WriteStartObject();
                w.WriteNumber("tick", e.Tick);
                Number(w, "t", e.Tick / (double)Simulation.StepsPerSecond);
                w.WriteString("kind", e.Kind);
                foreach (KeyValuePair<string, object> d in e.Data)
                {
                    switch (d.Value)
                    {
                        case double v:
                            Number(w, d.Key, v);
                            break;
                        case int i:
                            w.WriteNumber(d.Key, i);
                            break;
                        case bool b:
                            w.WriteBoolean(d.Key, b);
                            break;
                        default:
                            w.WriteString(d.Key, Convert.ToString(d.Value, System.Globalization.CultureInfo.InvariantCulture));
                            break;
                    }
                }

                w.WriteEndObject();
            }

            w.WriteEndArray();

            w.WriteStartArray("tricks");
            foreach (ScoredTrick trick in ride.Score.Tricks)
            {
                w.WriteStartObject();
                w.WriteNumber("tick", trick.Tick);
                w.WriteString("name", trick.Name);
                w.WriteNumber("points", trick.Points);
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteEndObject();
        }

        /// <summary>A number with at most 3 decimals; -0 is written as 0.</summary>
        private static void Number(Utf8JsonWriter w, string name, double value) => w.WriteNumber(name, Math.Round(value, 3) + 0.0);
    }
}
