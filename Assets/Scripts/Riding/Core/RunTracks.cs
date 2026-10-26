using System;
using System.Collections.Generic;
using FirstChair.Sim;

namespace FirstChair.Riding
{
    /// <summary>
    /// Old ski tracks down a marked run (Story 010): <see cref="OldTracks"/> lays S-turns on a rectangle with the fall
    /// line along +Z; here the rectangle is the run itself, its length along the centre line (<see cref="RunLine"/>) and
    /// its width across, and every track is bent onto the run, so the tracks follow it round its curves. Display only:
    /// the sim never reads them. Engine-free, so it is testable without Unity; the same seed gives the same tracks.
    /// </summary>
    /// <example>
    /// <code>
    /// OldTrackSettings settings = OldTrackSettings.Default;
    /// List&lt;OldTrackLine&gt; lines = RunTracks.Generate(run, 7, settings, 350.0, 1.0, (x, z) => RunTracks.NearAFeature(layout, x, z, 1.5));
    /// </code>
    /// </example>
    public static class RunTracks
    {
        /// <summary>Old tracks down the run, metres in world X and Z.</summary>
        /// <param name="run">The run.</param>
        /// <param name="seed">The seed.</param>
        /// <param name="settings">The tracks' shape; its count is replaced by one track per <paramref name="areaPerTrack"/>.</param>
        /// <param name="areaPerTrack">Square metres of run per track.</param>
        /// <param name="edgeMargin">How far the tracks keep from the run's edges, metres.</param>
        /// <param name="blocked">World points (X, Z, metres) a track must not enter, such as a kicker; null for none.</param>
        /// <returns>Two lines per track, left ski then right; none when the run is too narrow or short for a track.</returns>
        /// <exception cref="ArgumentException"><paramref name="areaPerTrack"/> is not positive.</exception>
        public static List<OldTrackLine> Generate(
            SlopeRun run, ulong seed, OldTrackSettings settings, double areaPerTrack, double edgeMargin, Func<double, double, bool> blocked)
        {
            if (!(areaPerTrack > 0.0))
            {
                throw new ArgumentException("Each track needs some area.", nameof(areaPerTrack));
            }

            var line = new CentreLine(run);
            double half = run.WidthMm / 2000.0 - edgeMargin;
            var bent = new List<OldTrackLine>();
            if (half <= settings.SkiSpacingMetres || line.Length <= settings.MinLengthMetres)
            {
                return bent;
            }

            settings.Count = Math.Max(1, (int)Math.Round(line.Length * 2.0 * half / areaPerTrack));
            Func<double, double, bool> blockedAcross = null;
            if (blocked != null)
            {
                blockedAcross = (across, distance) =>
                {
                    line.Place(across, distance, out double x, out double z);
                    return blocked(x, z);
                };
            }

            foreach (OldTrackLine straight in OldTracks.Generate(seed, -half, 0.0, half, line.Length, settings, blockedAcross))
            {
                var x = new double[straight.Count];
                var z = new double[straight.Count];
                for (int p = 0; p < straight.Count; p++)
                {
                    line.Place(straight.X[p], straight.Z[p], out x[p], out z[p]);
                }

                bent.Add(new OldTrackLine(x, z));
            }

            return bent;
        }

        /// <summary>
        /// Whether a point is on or within <paramref name="margin"/> of a kicker's ramp, or of a rail or box, so a track
        /// keeps off the park's features.
        /// </summary>
        /// <param name="layout">The slope's layout.</param>
        /// <param name="x">World X, metres.</param>
        /// <param name="z">World Z, metres.</param>
        /// <param name="margin">The clearance, metres.</param>
        /// <returns>True when the point is too close to a feature.</returns>
        public static bool NearAFeature(SlopeLayout layout, double x, double z, double margin)
        {
            foreach (SlopeKicker kicker in layout.Kickers)
            {
                double facing = kicker.FacingMillidegrees / 1000.0 * Math.PI / 180.0;
                double dx = x - kicker.XMm / 1000.0, dz = z - kicker.ZMm / 1000.0;
                double along = dx * Math.Sin(facing) + dz * Math.Cos(facing);
                double across = dx * Math.Cos(facing) - dz * Math.Sin(facing);
                if (along >= -kicker.LengthMm / 1000.0 - margin && along <= margin && Math.Abs(across) <= kicker.WidthMm / 2000.0 + margin)
                {
                    return true;
                }
            }

            foreach (SlopeRail rail in layout.Rails)
            {
                double x0 = rail.X0Mm / 1000.0, z0 = rail.Z0Mm / 1000.0, x1 = rail.X1Mm / 1000.0, z1 = rail.Z1Mm / 1000.0;
                double ax = x1 - x0, az = z1 - z0;
                double t = Math.Max(0.0, Math.Min(1.0, ((x - x0) * ax + (z - z0) * az) / (ax * ax + az * az)));
                double nx = x - (x0 + ax * t), nz = z - (z0 + az * t);
                double reach = rail.WidthMm / 2000.0 + margin;
                if (nx * nx + nz * nz <= reach * reach)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The run's centre line sampled every metre, with the distance along it and its right-hand side at each sample.
        /// Between samples both are interpolated, so a point beside the line moves smoothly as the line turns.
        /// </summary>
        private sealed class CentreLine
        {
            private readonly List<double> _x = new List<double>();
            private readonly List<double> _z = new List<double>();
            private readonly double[] _along;
            private readonly double[] _rightX;
            private readonly double[] _rightZ;

            public CentreLine(SlopeRun run)
            {
                RunLine.Sample(run.Points, 1.0, _x, _z);
                int count = _x.Count;
                _along = new double[count];
                _rightX = new double[count];
                _rightZ = new double[count];
                for (int i = 0; i < count; i++)
                {
                    if (i > 0)
                    {
                        _along[i] = _along[i - 1] + Math.Sqrt((_x[i] - _x[i - 1]) * (_x[i] - _x[i - 1]) + (_z[i] - _z[i - 1]) * (_z[i] - _z[i - 1]));
                    }

                    // Right of a direction (dx, dz) seen from above, as in OldTracks: (dz, -dx).
                    int a = Math.Max(0, i - 1), b = Math.Min(count - 1, i + 1);
                    double dx = _x[b] - _x[a], dz = _z[b] - _z[a];
                    double size = Math.Sqrt(dx * dx + dz * dz);
                    _rightX[i] = dz / size;
                    _rightZ[i] = -dx / size;
                }
            }

            /// <summary>The centre line's length, metres.</summary>
            public double Length => _along[_along.Length - 1];

            /// <summary>The point <paramref name="across"/> metres right of the line, <paramref name="distance"/> metres along it.</summary>
            public void Place(double across, double distance, out double x, out double z)
            {
                int low = 0, high = _along.Length - 1;
                while (high - low > 1)
                {
                    int middle = (low + high) / 2;
                    if (_along[middle] <= distance)
                    {
                        low = middle;
                    }
                    else
                    {
                        high = middle;
                    }
                }

                double span = _along[high] - _along[low];
                double t = span > 0.0 ? Math.Max(0.0, Math.Min(1.0, (distance - _along[low]) / span)) : 0.0;
                double rightX = _rightX[low] + (_rightX[high] - _rightX[low]) * t;
                double rightZ = _rightZ[low] + (_rightZ[high] - _rightZ[low]) * t;
                double size = Math.Sqrt(rightX * rightX + rightZ * rightZ);
                x = _x[low] + (_x[high] - _x[low]) * t + rightX / size * across;
                z = _z[low] + (_z[high] - _z[low]) * t + rightZ / size * across;
            }
        }
    }
}
