using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using FirstChair.Sim;

namespace FirstChair.Tools.RidePreview
{
    /// <summary>
    /// Entry point. Loads the committed test slope, runs every scripted ride in <see cref="Rides"/>, prints each ride's
    /// event counts and writes one JSON file for the web viewer.
    /// </summary>
    /// <remarks>
    /// Usage, from the repo root: <c>dotnet run --project tools/ride-preview -c Release [-- output.json]</c>.
    /// The default output is <c>tools/ride-preview/out/rides.json</c> (gitignored). Exit code 0 on success, 1 on a
    /// reported error (missing or LFS-pointer slope files, a layout without the zones a ride needs).
    /// </remarks>
    internal static class Program
    {
        private const string DefaultOutput = "tools/ride-preview/out/rides.json";

        public static int Main(string[] args)
        {
            try
            {
                string root = Repo.FindRoot();
                bool probe = Array.IndexOf(args, "--probe-kickers") >= 0;
                string named = Array.Find(args, a => !a.StartsWith("--", StringComparison.Ordinal));
                string output = named != null ? Path.GetFullPath(named) : Path.Combine(root, DefaultOutput);
                var clock = Stopwatch.StartNew();
                Slope slope = Slope.Load(root);
                if (probe)
                {
                    Rides.ProbeKickers(slope, Console.Out);
                    return 0;
                }

                Console.WriteLine(
                    "Slope: " + slope.SizeX + " x " + slope.SizeZ + " m, " + slope.Layout.Kickers.Count + " kickers, "
                    + slope.Forest.Count + " trees, " + slope.Rails.Count + " rails/boxes; sim version " + Simulation.Version);

                var rides = new List<RideResult>();
                foreach (RideScript script in Rides.All(slope))
                {
                    RideResult ride = RideRunner.Run(slope, script);
                    ride.PrintSummary(Console.Out);
                    rides.Add(ride);
                }

                long bytes = JsonOutput.Write(output, slope, rides);
                Console.WriteLine("Wrote " + output + " (" + (bytes / 1024) + " KB) in " + clock.Elapsed.TotalSeconds.ToString("0.0") + " s");
                return 0;
            }
            catch (ToolException e)
            {
                Console.Error.WriteLine("ride-preview: " + e.Message);
                return 1;
            }
        }
    }

    /// <summary>An error the user can act on; printed without a stack trace.</summary>
    internal sealed class ToolException : Exception
    {
        public ToolException(string message) : base(message)
        {
        }
    }

    /// <summary>Finds the repo root: the first folder at or above the working directory (or the binary) holding the sim package.</summary>
    internal static class Repo
    {
        private const string Marker = "Packages/com.firstchair.sim/package.json";

        public static string FindRoot()
        {
            foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                var directory = new DirectoryInfo(start);
                while (directory != null)
                {
                    if (File.Exists(Path.Combine(directory.FullName, Marker)))
                    {
                        return directory.FullName;
                    }

                    directory = directory.Parent;
                }
            }

            throw new ToolException("no " + Marker + " above " + Directory.GetCurrentDirectory() + ": run from inside the First Chair repo.");
        }
    }

    /// <summary>Conversions for the tool's own use. Fix64 to double is fine here: nothing converted goes back into the sim.</summary>
    internal static class Num
    {
        private const double RawOne = 4294967296.0;

        public static double D(Fix64 value) => value.Raw / RawOne;

        public static Fix64 F(double value) => Fix64.FromRatio((long)Math.Round(value * 1_000_000.0), 1_000_000);

        public static double Mm(int millimetres) => millimetres / 1000.0;

        public static double Wrap(double angle)
        {
            while (angle > Math.PI)
            {
                angle -= 2 * Math.PI;
            }

            while (angle < -Math.PI)
            {
                angle += 2 * Math.PI;
            }

            return angle;
        }

        public static double Speed(RiderState state)
        {
            double x = D(state.Velocity.X), y = D(state.Velocity.Y), z = D(state.Velocity.Z);
            return Math.Sqrt(x * x + y * y + z * z);
        }

        public static double X(RiderState state) => D(state.Position.X);

        public static double Z(RiderState state) => D(state.Position.Z);

        /// <summary>Whole sim steps for a rotation of <paramref name="radians"/> at <paramref name="rate"/> rad/s.</summary>
        public static int StepsFor(double radians, Fix64 rate) => (int)Math.Round(radians / D(rate * Simulation.StepSeconds));

        public static int Ticks(double seconds) => (int)Math.Round(seconds * Simulation.StepsPerSecond);

        public static SimInput Pack(SimButtons buttons, int steering = 0) => SimInput.Pack(buttons, steering);
    }
}
