using System;

namespace FirstChair.Riding
{
    /// <summary>
    /// Turns variable frame times into a whole number of fixed sim steps, keeps the remainder for the next frame, and
    /// reports how far the view sits between the last two sim states. Presentation timing only: the sim never sees it.
    /// </summary>
    public sealed class FixedStepClock
    {
        // Absorbs float noise in frame times so a frame of exactly n steps does not come out as n - 1.
        private const double Epsilon = 1e-9;

        private double _accumulator;

        /// <summary>Creates a clock.</summary>
        /// <param name="stepsPerSecond">Sim steps per second (the sim's is <c>Simulation.StepsPerSecond</c>, 120).</param>
        /// <param name="maxStepsPerFrame">The most steps one frame may run; time beyond it is dropped.</param>
        public FixedStepClock(int stepsPerSecond, int maxStepsPerFrame)
        {
            if (stepsPerSecond <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(stepsPerSecond), "Steps per second must be positive.");
            }

            if (maxStepsPerFrame <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxStepsPerFrame), "The step cap must be positive.");
            }

            StepSeconds = 1.0 / stepsPerSecond;
            MaxStepsPerFrame = maxStepsPerFrame;
        }

        /// <summary>The length of one step, seconds.</summary>
        public double StepSeconds { get; }

        /// <summary>The most steps one frame may run.</summary>
        public int MaxStepsPerFrame { get; }

        /// <summary>
        /// How far the view is from the previous sim state toward the current one, 0 to just under 1: the unspent
        /// time divided by a step.
        /// </summary>
        public double Alpha => Math.Min(_accumulator / StepSeconds, 1.0);

        /// <summary>Steps the last <see cref="Advance"/> dropped because of the cap; 0 on a normal frame.</summary>
        public int LastDroppedSteps { get; private set; }

        /// <summary>Steps dropped since the clock was made.</summary>
        public long TotalDroppedSteps { get; private set; }

        /// <summary>
        /// Adds one frame's time and returns how many steps are due. A negative, zero or non-finite frame time runs
        /// none. When more than <see cref="MaxStepsPerFrame"/> are due, the extra time is dropped (the ride slows down
        /// for that frame instead of the game spiralling) and <see cref="LastDroppedSteps"/> says how much.
        /// </summary>
        /// <param name="frameSeconds">The frame's duration, seconds.</param>
        public int Advance(double frameSeconds)
        {
            LastDroppedSteps = 0;
            if (double.IsNaN(frameSeconds) || double.IsInfinity(frameSeconds) || frameSeconds <= 0.0)
            {
                return 0;
            }

            _accumulator += frameSeconds;
            double due = Math.Floor((_accumulator + Epsilon) / StepSeconds);
            int steps;
            if (due > MaxStepsPerFrame)
            {
                double dropped = due - MaxStepsPerFrame;
                LastDroppedSteps = dropped > int.MaxValue ? int.MaxValue : (int)dropped;
                TotalDroppedSteps += LastDroppedSteps;
                steps = MaxStepsPerFrame;
                _accumulator -= due * StepSeconds;
            }
            else
            {
                steps = (int)due;
                _accumulator -= steps * StepSeconds;
            }

            if (_accumulator < 0.0)
            {
                _accumulator = 0.0;
            }

            return steps;
        }

        /// <summary>Clears the unspent time, for a new run.</summary>
        public void Reset()
        {
            _accumulator = 0.0;
            LastDroppedSteps = 0;
        }
    }
}
