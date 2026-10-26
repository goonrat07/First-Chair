using System;
using FirstChair.Sim;

namespace FirstChair.Riding
{
    /// <summary>
    /// How much snow the skis throw (Story 008, presentation): skis sliding sideways in a quick turn or a skid throw snow
    /// in proportion to the slide, a brake throws it ahead, and a clean carve throws none, so the two kinds of turn read
    /// apart. Plain numbers in and out, so it is testable without Unity; <c>SnowSpray</c> emits the flakes.
    /// </summary>
    /// <example>
    /// <code>
    /// float factor = SnowSprayMath.SnowFactor(state.Snow, 1.6f, 0.3f);
    /// float perSecond = SnowSprayMath.Rate(slipAngle, speed, state.Stance == Stance.Brake, onSnow, factor, 60f, 12f, 300f);
    /// </code>
    /// </example>
    public static class SnowSprayMath
    {
        /// <summary>Flakes per second.</summary>
        /// <param name="slipAngle">The sim's slip angle, radians: the angle between the velocity and the skis.</param>
        /// <param name="speed">The rider's speed, m/s.</param>
        /// <param name="braking">The sim's stance is <see cref="Stance.Brake"/>.</param>
        /// <param name="onSnow">On the snow: neither airborne nor crashed.</param>
        /// <param name="snowFactor">The snow's factor (<see cref="SnowFactor"/>).</param>
        /// <param name="perSlideMetre">Flakes per metre the skis slide sideways.</param>
        /// <param name="perBrakeMetre">Flakes per metre travelled while braking.</param>
        /// <param name="maxRate">The most flakes per second.</param>
        public static float Rate(double slipAngle, double speed, bool braking, bool onSnow, float snowFactor,
            float perSlideMetre, float perBrakeMetre, float maxRate)
        {
            if (!onSnow || !(speed > 0.0))
            {
                return 0f;
            }

            double slide = speed * Math.Sin(Math.Min(Math.Abs(slipAngle), Math.PI / 2));
            double rate = (perSlideMetre * slide + (braking ? perBrakeMetre * speed : 0.0)) * snowFactor;
            return (float)Math.Min(rate, maxRate);
        }

        /// <summary>
        /// Flakes per second a hard carve throws (Story 009, after the Bluebird prototype, which sprayed at 40% on fast
        /// carves): none below <paramref name="minSpeed"/> or with less edge than <paramref name="edgeFrom"/>, then rising
        /// to <paramref name="perMetre"/> per metre travelled at full edge.
        /// </summary>
        /// <param name="edgeFraction">The edge, -1 to 1 (the sim's edge angle over its maximum).</param>
        /// <param name="speed">The rider's speed, m/s.</param>
        /// <param name="onSnow">On the snow: neither airborne nor crashed.</param>
        /// <param name="snowFactor">The snow's factor (<see cref="SnowFactor"/>).</param>
        /// <param name="perMetre">Flakes per metre travelled at full edge.</param>
        /// <param name="edgeFrom">Edge fraction, 0 to 1, below which a carve throws nothing.</param>
        /// <param name="minSpeed">Slowest carve that throws snow, m/s.</param>
        public static float CarveRate(double edgeFraction, double speed, bool onSnow, float snowFactor, float perMetre,
            float edgeFrom, float minSpeed)
        {
            double edge = Math.Min(Math.Abs(edgeFraction), 1.0);
            if (!onSnow || !(speed >= minSpeed) || !(edge > edgeFrom) || edgeFrom >= 1f)
            {
                return 0f;
            }

            return (float)(perMetre * speed * (edge - edgeFrom) / (1.0 - edgeFrom) * snowFactor);
        }

        /// <summary>The snow's factor on the spray: powder throws more, ice less, the rest 1.</summary>
        /// <param name="snow">The snow under the skis.</param>
        /// <param name="powder">Powder's factor.</param>
        /// <param name="ice">Ice's factor.</param>
        public static float SnowFactor(SnowType snow, float powder, float ice) =>
            snow == SnowType.Powder ? powder : snow == SnowType.Ice ? ice : 1f;
    }
}
