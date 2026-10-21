using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using FirstChair.Sim;

namespace FirstChair.Riding
{
    /// <summary>
    /// One live ride on the test slope: the <see cref="Simulation"/>, the <see cref="Recording"/> of its inputs and the
    /// <see cref="RunScore"/> fed from its events after every step. It keeps the previous and current
    /// <see cref="RiderState"/> for interpolation and, optionally, every step's state and events so a replay can be
    /// checked against the live run step for step. It only reads the sim's state; nothing here writes rider state.
    /// </summary>
    public sealed class RideSession
    {
        private static readonly IReadOnlyList<SimEvent> NoEvents = Array.AsReadOnly(new SimEvent[0]);

        private readonly List<SimEvent> _frameEvents = new List<SimEvent>();
        private readonly List<RiderState> _stateHistory;
        private readonly List<IReadOnlyList<SimEvent>> _eventHistory;
        private RiderState _previous;
        private RiderState _current;

        /// <summary>Starts a ride.</summary>
        /// <param name="slope">The slope.</param>
        /// <param name="tuning">The sim tuning (normally <see cref="Tuning.Default"/>).</param>
        /// <param name="setup">The run setup (see <see cref="TestSlopeData.CreateSetup"/>).</param>
        /// <param name="keepHistory">Keep every step's state and events, for <see cref="ReplayCheck"/>.</param>
        public RideSession(TestSlopeData slope, Tuning tuning, RunSetup setup, bool keepHistory)
        {
            Slope = slope ?? throw new ArgumentNullException(nameof(slope));
            Tuning = tuning;
            Setup = setup;
            Simulation = slope.CreateSimulation(setup, tuning);
            Recording = slope.CreateRecording(setup, tuning);
            Score = new RunScore(tuning);
            _previous = Simulation.State;
            _current = Simulation.State;
            FrameEvents = _frameEvents.AsReadOnly();
            if (keepHistory)
            {
                _stateHistory = new List<RiderState>();
                _eventHistory = new List<IReadOnlyList<SimEvent>>();
                StateHistory = _stateHistory.AsReadOnly();
                EventHistory = _eventHistory.AsReadOnly();
            }
        }

        /// <summary>Raised after each step that produced events, with that step's events (a read-only copy).</summary>
        public event Action<IReadOnlyList<SimEvent>> StepEvents;

        /// <summary>The slope.</summary>
        public TestSlopeData Slope { get; }

        /// <summary>The tuning the ride runs on.</summary>
        public Tuning Tuning { get; }

        /// <summary>The run setup.</summary>
        public RunSetup Setup { get; }

        /// <summary>The sim. Views read <see cref="Previous"/> and <see cref="Current"/> instead of stepping it.</summary>
        internal Simulation Simulation { get; }

        /// <summary>The inputs so far, one per step, with a header that pins the slope, tuning and sim version.</summary>
        public Recording Recording { get; }

        /// <summary>The run's score, fed with every step's events.</summary>
        public RunScore Score { get; }

        /// <summary>The state before the last step (equal to <see cref="Current"/> after a reset, so a view snaps).</summary>
        public RiderState Previous => _previous;

        /// <summary>The state after the last step.</summary>
        public RiderState Current => _current;

        /// <summary>Steps run since the start.</summary>
        public int StepsRun { get; private set; }

        /// <summary>Whether a Reset event in the last <see cref="RunSteps"/> made the view snap.</summary>
        public bool SnappedThisFrame { get; private set; }

        /// <summary>Every event of the last <see cref="RunSteps"/> call, in order.</summary>
        public IReadOnlyList<SimEvent> FrameEvents { get; }

        /// <summary>Whether this session keeps per-step history.</summary>
        public bool KeepsHistory => _stateHistory != null;

        /// <summary>The state after each step, when <see cref="KeepsHistory"/>; otherwise null.</summary>
        public IReadOnlyList<RiderState> StateHistory { get; }

        /// <summary>The events of each step, when <see cref="KeepsHistory"/>; otherwise null.</summary>
        public IReadOnlyList<IReadOnlyList<SimEvent>> EventHistory { get; }

        /// <summary>
        /// Runs <paramref name="steps"/> sim steps, all with the same <paramref name="input"/> (read once for the
        /// frame), records the input once per step and folds each step's events into <see cref="Score"/>.
        /// </summary>
        /// <param name="input">The frame's packed input.</param>
        /// <param name="steps">How many steps are due; 0 runs none.</param>
        public void RunSteps(SimInput input, int steps)
        {
            _frameEvents.Clear();
            SnappedThisFrame = false;
            for (int i = 0; i < steps; i++)
            {
                _previous = _current;
                Simulation.Step(input);
                Recording.Append(input);
                IReadOnlyList<SimEvent> events = Simulation.Events;
                Score.AddAll(events);
                _current = Simulation.State;
                StepsRun++;

                IReadOnlyList<SimEvent> stepEvents = NoEvents;
                if (events.Count > 0)
                {
                    var copy = new SimEvent[events.Count];
                    for (int e = 0; e < copy.Length; e++)
                    {
                        copy[e] = events[e];
                        if (copy[e].Kind == SimEventKind.Reset)
                        {
                            SnappedThisFrame = true;
                        }
                    }

                    stepEvents = Array.AsReadOnly(copy);
                    _frameEvents.AddRange(copy);
                }

                if (SnappedThisFrame)
                {
                    _previous = _current;
                }

                if (_stateHistory != null)
                {
                    _stateHistory.Add(_current);
                    _eventHistory.Add(stepEvents);
                }

                if (stepEvents.Count > 0)
                {
                    StepEvents?.Invoke(stepEvents);
                }
            }
        }
    }
}
