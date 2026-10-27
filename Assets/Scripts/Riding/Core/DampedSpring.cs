using System;

namespace FirstChair.Riding
{
    /// <summary>
    /// A damped spring toward a target, solved exactly over each frame (Story 013), so its motion does not depend on the
    /// frame rate: one 1/30 s step lands where two 1/60 s steps do. The skier's secondary motion (hips compressing on a
    /// landing, the upper body pitching and swaying) is one of these per channel.
    /// </summary>
    /// <remarks>
    /// x'' = -w^2 (x - target) - 2 zeta w x', with w = 2 pi frequency. Under-damped (zeta below 1) it swings past the
    /// target and back, critically damped (zeta 1) it arrives without overshoot as fast as it can, over-damped it creeps.
    /// The target is held constant across a step.
    /// </remarks>
    /// <example>
    /// <code>
    /// _hips.Step(target: -gain * upwardAcceleration, frequency: 2.5, damping: 0.6, dt: Time.deltaTime);
    /// hipsOffset = _hips.Value;
    /// </code>
    /// </example>
    public struct DampedSpring
    {
        /// <summary>Where the spring is.</summary>
        public double Value;

        /// <summary>How fast it moves, per second.</summary>
        public double Velocity;

        /// <summary>Moves the spring <paramref name="dt"/> seconds toward <paramref name="target"/>.</summary>
        /// <param name="target">Where it pulls toward, held through the step.</param>
        /// <param name="frequency">Its natural frequency, Hz; zero or less leaves it as it is.</param>
        /// <param name="damping">Its damping ratio: 1 is critical; clamped to 0 or more.</param>
        /// <param name="dt">The step, seconds; zero, negative or not a number leaves it as it is.</param>
        public void Step(double target, double frequency, double damping, double dt)
        {
            if (!(dt > 0.0) || !(frequency > 0.0) || double.IsInfinity(dt))
            {
                return;
            }

            double w = 2.0 * Math.PI * frequency;
            double zeta = Math.Max(0.0, damping);
            double y0 = Value - target;
            double v0 = Velocity;
            double y, v;
            if (Math.Abs(zeta - 1.0) < 1e-4)
            {
                double e = Math.Exp(-w * dt);
                double c = v0 + w * y0;
                y = e * (y0 + c * dt);
                v = e * (c - w * (y0 + c * dt));
            }
            else if (zeta < 1.0)
            {
                double wd = w * Math.Sqrt(1.0 - zeta * zeta);
                double e = Math.Exp(-zeta * w * dt);
                double cos = Math.Cos(wd * dt);
                double sin = Math.Sin(wd * dt);
                double b = (v0 + zeta * w * y0) / wd;
                y = e * (y0 * cos + b * sin);
                v = e * ((b * wd - zeta * w * y0) * cos - (y0 * wd + zeta * w * b) * sin);
            }
            else
            {
                double root = Math.Sqrt(zeta * zeta - 1.0);
                double r1 = -w * (zeta - root);
                double r2 = -w * (zeta + root);
                double c2 = (v0 - r1 * y0) / (r2 - r1);
                double c1 = y0 - c2;
                double e1 = Math.Exp(r1 * dt);
                double e2 = Math.Exp(r2 * dt);
                y = c1 * e1 + c2 * e2;
                v = c1 * r1 * e1 + c2 * r2 * e2;
            }

            Value = target + y;
            Velocity = v;
        }
    }
}
