using System.Collections.Generic;
using FirstChair.Sim;

namespace FirstChair.Riding
{
    /// <summary>The outcome of <see cref="ReplayCheck.Verify(RideSession)"/>.</summary>
    public readonly struct ReplayCheckResult
    {
        /// <summary>Creates a result.</summary>
        public ReplayCheckResult(bool passed, ReplayStatus status, int stepsCompared, int firstMismatchStep, string detail)
        {
            Passed = passed;
            Status = status;
            StepsCompared = stepsCompared;
            FirstMismatchStep = firstMismatchStep;
            Detail = detail;
        }

        /// <summary>True when every step's state and events, and the final score, matched.</summary>
        public bool Passed { get; }

        /// <summary>The replayer's status (a header mismatch shows here).</summary>
        public ReplayStatus Status { get; }

        /// <summary>Steps replayed and compared.</summary>
        public int StepsCompared { get; }

        /// <summary>The first step (0-based) that differed, or -1.</summary>
        public int FirstMismatchStep { get; }

        /// <summary>What differed, or a one-line summary when it passed.</summary>
        public string Detail { get; }

        /// <inheritdoc />
        public override string ToString() => (Passed ? "Replay check passed: " : "Replay check FAILED: ") + Detail;
    }

    /// <summary>
    /// Story 003, acceptance criterion 7: replays a live ride's recording (after a round trip through its bytes, as a
    /// saved file would be) with <see cref="Replayer"/>, and compares the full <see cref="RiderState"/> (bit for bit)
    /// and the event list at every step, then the final score.
    /// </summary>
    public static class ReplayCheck
    {
        /// <summary>Checks a session that kept its history.</summary>
        /// <param name="session">The live ride.</param>
        public static ReplayCheckResult Verify(RideSession session)
        {
            if (!session.KeepsHistory)
            {
                return new ReplayCheckResult(false, ReplayStatus.Ok, 0, -1, "the session was started without history, so there is nothing to compare");
            }

            return Verify(session.Recording, session.Slope, session.Tuning, session.StateHistory, session.EventHistory, session.Score.Total);
        }

        /// <summary>Checks a recording against a live run's per-step states and events.</summary>
        /// <param name="recording">The live run's recording.</param>
        /// <param name="slope">The slope it ran on.</param>
        /// <param name="tuning">The tuning it ran on.</param>
        /// <param name="liveStates">The state after each live step.</param>
        /// <param name="liveEvents">The events of each live step.</param>
        /// <param name="liveScore">The live run's total score.</param>
        public static ReplayCheckResult Verify(
            Recording recording,
            TestSlopeData slope,
            Tuning tuning,
            IReadOnlyList<RiderState> liveStates,
            IReadOnlyList<IReadOnlyList<SimEvent>> liveEvents,
            long liveScore)
        {
            if (!Recording.TryRead(recording.ToBytes(), out Recording loaded))
            {
                return new ReplayCheckResult(false, ReplayStatus.Ok, 0, -1, "the recording did not survive a round trip through its bytes");
            }

            int step = 0;
            int mismatch = -1;
            string detail = null;
            ReplayResult result = Replayer.Replay(loaded, slope.Heightmap, slope.SnowMap, slope.Forest, slope.Rails, tuning, simulation =>
            {
                if (mismatch < 0)
                {
                    if (step >= liveStates.Count)
                    {
                        mismatch = step;
                        detail = "the replay ran past the " + liveStates.Count + " live steps";
                    }
                    else if (!simulation.State.Equals(liveStates[step]))
                    {
                        mismatch = step;
                        detail = "rider state differs at step " + step + ": live position " + liveStates[step].Position
                            + ", replayed " + simulation.State.Position;
                    }
                    else if (!SameEvents(simulation.Events, liveEvents[step]))
                    {
                        mismatch = step;
                        detail = "events differ at step " + step + ": live " + liveEvents[step].Count + ", replayed " + simulation.Events.Count;
                    }
                }

                step++;
            });

            if (result.Status != ReplayStatus.Ok)
            {
                return new ReplayCheckResult(false, result.Status, 0, -1, "the replayer refused the recording: " + result.Status);
            }

            if (mismatch < 0 && step != liveStates.Count)
            {
                mismatch = step;
                detail = "the live run kept " + liveStates.Count + " steps but the replay ran " + step;
            }

            if (mismatch < 0 && result.TotalScore != liveScore)
            {
                return new ReplayCheckResult(false, result.Status, step, step, "the final score differs: live " + liveScore + ", replayed " + result.TotalScore);
            }

            if (mismatch >= 0)
            {
                return new ReplayCheckResult(false, result.Status, step, mismatch, detail);
            }

            return new ReplayCheckResult(true, result.Status, step, -1, step + " steps matched, final position " + result.FinalState.Position + ", score " + liveScore);
        }

        private static bool SameEvents(IReadOnlyList<SimEvent> replayed, IReadOnlyList<SimEvent> live)
        {
            if (replayed.Count != live.Count)
            {
                return false;
            }

            for (int i = 0; i < live.Count; i++)
            {
                if (!replayed[i].Equals(live[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
