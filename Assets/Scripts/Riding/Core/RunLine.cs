using System;
using System.Collections.Generic;
using FirstChair.Sim;

namespace FirstChair.Riding
{
    /// <summary>
    /// A run's centre line as the test mountain's generator draws it (Story 010): a centripetal Catmull-Rom spline through
    /// the layout's points (<see cref="SlopeRun.Points"/>), sampled about every step metres. The scene builder stands the
    /// run's markers along it. Engine-free, so it is testable without Unity.
    /// </summary>
    /// <example>
    /// <code>
    /// var xs = new List&lt;double&gt;();
    /// var zs = new List&lt;double&gt;();
    /// RunLine.Sample(run.Points, 2.0, xs, zs);  // metres, top of the run first
    /// </code>
    /// </example>
    public static class RunLine
    {
        /// <summary>Samples the spline through the points, the first point first; every point is itself a sample.</summary>
        /// <param name="points">The run's points, millimetres, at least two.</param>
        /// <param name="step">About how far apart the samples are, metres. Positive.</param>
        /// <param name="xs">Receives the samples' X, metres (cleared first).</param>
        /// <param name="zs">Receives the samples' Z, metres (cleared first).</param>
        /// <exception cref="ArgumentException">Fewer than two points, or a step that is not positive.</exception>
        public static void Sample(IReadOnlyList<SlopePoint> points, double step, List<double> xs, List<double> zs)
        {
            if (points == null || points.Count < 2 || !(step > 0.0))
            {
                throw new ArgumentException("A run line needs two or more points and a positive step.");
            }

            int count = points.Count;
            var px = new double[count + 2];
            var pz = new double[count + 2];
            for (int i = 0; i < count; i++)
            {
                px[i + 1] = points[i].XMm / 1000.0;
                pz[i + 1] = points[i].ZMm / 1000.0;
            }

            // The ends are extended straight on, as the generator does.
            px[0] = 2.0 * px[1] - px[2];
            pz[0] = 2.0 * pz[1] - pz[2];
            px[count + 1] = 2.0 * px[count] - px[count - 1];
            pz[count + 1] = 2.0 * pz[count] - pz[count - 1];
            xs.Clear();
            zs.Clear();
            xs.Add(px[1]);
            zs.Add(pz[1]);
            for (int i = 1; i < count; i++)
            {
                double t0 = 0.0;
                double t1 = t0 + Math.Sqrt(Distance(px, pz, i - 1, i));
                double t2 = t1 + Math.Sqrt(Distance(px, pz, i, i + 1));
                double t3 = t2 + Math.Sqrt(Distance(px, pz, i + 1, i + 2));
                int pieces = Math.Max(2, (int)(Distance(px, pz, i, i + 1) / step));
                for (int k = 1; k <= pieces; k++)
                {
                    double t = t1 + (t2 - t1) * k / pieces;
                    xs.Add(Point(px, i, t, t0, t1, t2, t3));
                    zs.Add(Point(pz, i, t, t0, t1, t2, t3));
                }
            }
        }

        // The Barry-Goldman pyramid for one coordinate of the span from point i to i + 1.
        private static double Point(double[] p, int i, double t, double t0, double t1, double t2, double t3)
        {
            double a1 = ((t1 - t) * p[i - 1] + (t - t0) * p[i]) / (t1 - t0);
            double a2 = ((t2 - t) * p[i] + (t - t1) * p[i + 1]) / (t2 - t1);
            double a3 = ((t3 - t) * p[i + 1] + (t - t2) * p[i + 2]) / (t3 - t2);
            double b1 = ((t2 - t) * a1 + (t - t0) * a2) / (t2 - t0);
            double b2 = ((t3 - t) * a2 + (t - t1) * a3) / (t3 - t1);
            return ((t2 - t) * b1 + (t - t1) * b2) / (t2 - t1);
        }

        private static double Distance(double[] x, double[] z, int a, int b) =>
            Math.Sqrt((x[b] - x[a]) * (x[b] - x[a]) + (z[b] - z[a]) * (z[b] - z[a]));
    }
}
