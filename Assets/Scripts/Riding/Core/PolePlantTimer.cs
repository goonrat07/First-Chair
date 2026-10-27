using System;

namespace FirstChair.Riding
{
    /// <summary>The pole plant's timing and when one may start (Story 013).</summary>
    public readonly struct PolePlantSettings
    {
        /// <summary>Creates the settings.</summary>
        /// <param name="minSpeed">The slowest a plant starts at, m/s.</param>
        /// <param name="edgeThreshold">How far onto the new edges the skis must tip before it counts as a change, radians.</param>
        /// <param name="reachSeconds">The swing forward to the snow.</param>
        /// <param name="holdSeconds">The tip on the snow.</param>
        /// <param name="releaseSeconds">The swing back into the clip.</param>
        public PolePlantSettings(double minSpeed, double edgeThreshold, double reachSeconds, double holdSeconds, double releaseSeconds)
        {
            MinSpeed = minSpeed;
            EdgeThreshold = edgeThreshold;
            ReachSeconds = Math.Max(0.0, reachSeconds);
            HoldSeconds = Math.Max(0.0, holdSeconds);
            ReleaseSeconds = Math.Max(0.0, releaseSeconds);
        }

        /// <summary>The slowest a plant starts at, m/s.</summary>
        public double MinSpeed { get; }

        /// <summary>How far onto the new edges counts as an edge change, radians.</summary>
        public double EdgeThreshold { get; }

        /// <summary>The swing forward, seconds.</summary>
        public double ReachSeconds { get; }

        /// <summary>The tip on the snow, seconds.</summary>
        public double HoldSeconds { get; }

        /// <summary>The swing back, seconds.</summary>
        public double ReleaseSeconds { get; }

        /// <summary>The whole plant, seconds.</summary>
        public double TotalSeconds => ReachSeconds + HoldSeconds + ReleaseSeconds;
    }

    /// <summary>
    /// When the skier plants a pole (Story 013): at each change from one set of edges to the other, on the snow at speed,
    /// the new turn's inside pole goes in, once per turn. A run straight into the first turn does not plant; neither does
    /// a wiggle that never tips past the threshold. Leaving the snow, tucking or crashing lets go at once.
    /// </summary>
    /// <example>
    /// <code>
    /// _plant.Update(edgeAngle, speed, onSnow &amp;&amp; !tucked, Time.deltaTime, settings);
    /// float weight = (float)_plant.Weight(settings);   // blend the arm from the clip toward the plant
    /// bool hold = _plant.Planted(settings);            // keep the tip on its snow point
    /// </code>
    /// </example>
    public struct PolePlantTimer
    {
        // The edges last settled on: -1 the left edges, +1 the right, 0 none yet.
        private int _edges;

        /// <summary>The pole being planted: +1 the right, -1 the left, 0 none.</summary>
        public int Side;

        /// <summary>Seconds since the plant began.</summary>
        public double Time;

        /// <summary>Advances the timer one frame.</summary>
        /// <param name="edgeAngle">The sim's edge angle, radians, negative on the left edges.</param>
        /// <param name="speed">Speed, m/s.</param>
        /// <param name="canPlant">False in the air, a tuck, a crash or on the lift.</param>
        /// <param name="dt">The frame, seconds.</param>
        /// <param name="settings">Timing and thresholds.</param>
        public void Update(double edgeAngle, double speed, bool canPlant, double dt, in PolePlantSettings settings)
        {
            if (Side != 0)
            {
                Time += Math.Max(0.0, dt);
                if (!canPlant || Time >= settings.TotalSeconds)
                {
                    Side = 0;
                    Time = 0.0;
                }
            }

            int edges = edgeAngle > settings.EdgeThreshold ? 1 : edgeAngle < -settings.EdgeThreshold ? -1 : 0;
            if (edges == 0 || edges == _edges)
            {
                return;
            }

            bool change = _edges != 0;
            _edges = edges;
            if (change && canPlant && speed >= settings.MinSpeed)
            {
                Side = edges;
                Time = 0.0;
            }
        }

        /// <summary>How far the arm is into the plant: 0 to 1 over the reach, 1 held, back to 0 over the release.</summary>
        public double Weight(in PolePlantSettings settings)
        {
            if (Side == 0)
            {
                return 0.0;
            }

            if (Time < settings.ReachSeconds)
            {
                return Smooth(Time / settings.ReachSeconds);
            }

            double release = Time - settings.ReachSeconds - settings.HoldSeconds;
            if (release <= 0.0)
            {
                return 1.0;
            }

            return settings.ReleaseSeconds > 0.0 ? 1.0 - Smooth(release / settings.ReleaseSeconds) : 0.0;
        }

        /// <summary>True while the tip should stay on its point in the snow: after the reach, until the release.</summary>
        public bool Planted(in PolePlantSettings settings)
        {
            return Side != 0 && Time >= settings.ReachSeconds && Time <= settings.ReachSeconds + settings.HoldSeconds;
        }

        private static double Smooth(double t)
        {
            t = Math.Max(0.0, Math.Min(1.0, t));
            return t * t * (3.0 - 2.0 * t);
        }
    }
}
