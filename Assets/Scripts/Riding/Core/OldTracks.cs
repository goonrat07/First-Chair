using System;
using System.Collections.Generic;

namespace FirstChair.Riding
{
    /// <summary>
    /// Old ski tracks for the look of the slope (story 003 decisions, user 2026-10-24): pairs of lines left by earlier
    /// skiers, curving in S-turns down the fall line over the groomers and the powder, as in Grand Mountain Adventure 2.
    /// Display only: the sim never reads them. The same seed always gives the same tracks, on every platform, so the scene
    /// builds the same every time.
    /// </summary>
    /// <remarks>
    /// Each track walks down a rectangle in steps of <see cref="OldTrackSettings.StepMetres"/>, its heading swinging about
    /// the fall line (+Z) as a sine of the distance travelled, and ends at its length, at the rectangle's edge, or at the
    /// first point the caller blocks (a tree, a kicker). It leaves two lines, one per ski,
    /// <see cref="OldTrackSettings.SkiSpacingMetres"/> apart. Tracks shorter than <see cref="OldTrackSettings.MinLengthMetres"/>
    /// are dropped.
    /// </remarks>
    /// <example>
    /// <code>
    /// var lines = OldTracks.Generate(seed: 7, 40, 0, 140, 460, OldTrackSettings.Default, (x, z) => NearATree(x, z));
    /// foreach (OldTrackLine line in lines) { /* lay a ribbon along line.X, line.Z on the snow */ }
    /// </code>
    /// </example>
    public static class OldTracks
    {
        /// <summary>Generates the tracks in one rectangle of the slope, metres.</summary>
        /// <param name="seed">The seed.</param>
        /// <param name="xMin">The rectangle's least X.</param>
        /// <param name="zMin">The rectangle's least Z (uphill edge).</param>
        /// <param name="xMax">The rectangle's greatest X.</param>
        /// <param name="zMax">The rectangle's greatest Z (downhill edge).</param>
        /// <param name="settings">How many tracks and their shape.</param>
        /// <param name="blocked">Points a track must not enter; null for none.</param>
        /// <returns>Two lines per kept track, left ski then right ski.</returns>
        /// <exception cref="ArgumentException">The rectangle is empty, or a setting is not positive.</exception>
        public static List<OldTrackLine> Generate(
            ulong seed, double xMin, double zMin, double xMax, double zMax, OldTrackSettings settings, Func<double, double, bool> blocked)
        {
            if (!(xMax > xMin) || !(zMax > zMin))
            {
                throw new ArgumentException("The rectangle must have a positive size.");
            }

            if (!(settings.StepMetres > 0.0) || !(settings.SkiSpacingMetres > 0.0) || !(settings.MinLengthMetres > 0.0)
                || settings.MaxLengthMetres < settings.MinLengthMetres || !(settings.MinWavelengthMetres > 0.0)
                || settings.MaxWavelengthMetres < settings.MinWavelengthMetres)
            {
                throw new ArgumentException("Old track settings must be positive, with each maximum at or above its minimum.");
            }

            var random = new SplitMix64(seed);
            var lines = new List<OldTrackLine>();
            double half = settings.SkiSpacingMetres / 2.0;
            var centreX = new List<double>();
            var centreZ = new List<double>();
            var headings = new List<double>();
            for (int track = 0; track < settings.Count; track++)
            {
                double length = random.Range(settings.MinLengthMetres, settings.MaxLengthMetres);
                double wavelength = random.Range(settings.MinWavelengthMetres, settings.MaxWavelengthMetres);
                double amplitude = random.Range(settings.MinSwingDegrees, settings.MaxSwingDegrees) * Math.PI / 180.0;
                double phase = random.Range(0.0, 2.0 * Math.PI);
                double x = random.Range(xMin + half, xMax - half);
                double z = random.Range(zMin, Math.Max(zMin, zMax - length * 0.5));
                centreX.Clear();
                centreZ.Clear();
                headings.Clear();
                for (double travelled = 0.0; travelled <= length; travelled += settings.StepMetres)
                {
                    double heading = amplitude * Math.Sin(2.0 * Math.PI * travelled / wavelength + phase);
                    double sideX = Math.Cos(heading) * half;
                    double sideZ = -Math.Sin(heading) * half;
                    if (!Inside(x - sideX, z - sideZ, xMin, zMin, xMax, zMax) || !Inside(x + sideX, z + sideZ, xMin, zMin, xMax, zMax)
                        || (blocked != null && (blocked(x - sideX, z - sideZ) || blocked(x + sideX, z + sideZ))))
                    {
                        break;
                    }

                    centreX.Add(x);
                    centreZ.Add(z);
                    headings.Add(heading);
                    x += Math.Sin(heading) * settings.StepMetres;
                    z += Math.Cos(heading) * settings.StepMetres;
                }

                if ((centreX.Count - 1) * settings.StepMetres < settings.MinLengthMetres)
                {
                    continue;
                }

                lines.Add(Offset(centreX, centreZ, headings, -half));
                lines.Add(Offset(centreX, centreZ, headings, half));
            }

            return lines;
        }

        private static bool Inside(double x, double z, double xMin, double zMin, double xMax, double zMax) =>
            x >= xMin && x <= xMax && z >= zMin && z <= zMax;

        /// <summary>A line beside the centre line: <paramref name="side"/> metres to the right (negative: to the left).</summary>
        private static OldTrackLine Offset(List<double> x, List<double> z, List<double> heading, double side)
        {
            var lineX = new double[x.Count];
            var lineZ = new double[x.Count];
            for (int i = 0; i < x.Count; i++)
            {
                // Right of a heading h (0 = +Z, pi/2 = +X) is (cos h, -sin h).
                lineX[i] = x[i] + Math.Cos(heading[i]) * side;
                lineZ[i] = z[i] - Math.Sin(heading[i]) * side;
            }

            return new OldTrackLine(lineX, lineZ);
        }

        /// <summary>SplitMix64: a small, fixed generator, so the tracks are the same on every runtime.</summary>
        private struct SplitMix64
        {
            private ulong _state;

            public SplitMix64(ulong seed) => _state = seed;

            /// <summary>A double in [low, high).</summary>
            public double Range(double low, double high) => low + (high - low) * ((Next() >> 11) * (1.0 / (1UL << 53)));

            private ulong Next()
            {
                _state += 0x9E3779B97F4A7C15UL;
                ulong value = _state;
                value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
                value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
                return value ^ (value >> 31);
            }
        }
    }

    /// <summary>One ski's line of an old track: points on the snow, metres, in order down the track.</summary>
    public readonly struct OldTrackLine
    {
        /// <summary>Creates a line.</summary>
        public OldTrackLine(double[] x, double[] z)
        {
            X = x;
            Z = z;
        }

        /// <summary>World X of each point.</summary>
        public double[] X { get; }

        /// <summary>World Z of each point.</summary>
        public double[] Z { get; }

        /// <summary>The number of points.</summary>
        public int Count => X.Length;
    }

    /// <summary>How many old tracks to lay and their shape.</summary>
    public struct OldTrackSettings
    {
        /// <summary>Tracks to try; some end early and are dropped.</summary>
        public int Count;

        /// <summary>Distance between points along a track, metres.</summary>
        public double StepMetres;

        /// <summary>Distance between a track's two ski lines, metres.</summary>
        public double SkiSpacingMetres;

        /// <summary>Shortest and longest track, metres; a track that ends before the shortest is dropped.</summary>
        public double MinLengthMetres, MaxLengthMetres;

        /// <summary>Length of one full S (a turn each way), metres.</summary>
        public double MinWavelengthMetres, MaxWavelengthMetres;

        /// <summary>How far the heading swings from the fall line at the top of each turn, degrees.</summary>
        public double MinSwingDegrees, MaxSwingDegrees;

        /// <summary>
        /// The test slope's tracks: S-turns 18 to 40 m long swinging 15 to 55 degrees, like a range of skiers, half a
        /// metre between points (the heightmap's cell) and 0.24 m between the skis.
        /// </summary>
        public static OldTrackSettings Default => new OldTrackSettings
        {
            Count = 20,
            StepMetres = 0.5,
            SkiSpacingMetres = 0.24,
            MinLengthMetres = 60.0,
            MaxLengthMetres = 260.0,
            MinWavelengthMetres = 18.0,
            MaxWavelengthMetres = 40.0,
            MinSwingDegrees = 15.0,
            MaxSwingDegrees = 55.0,
        };
    }
}
