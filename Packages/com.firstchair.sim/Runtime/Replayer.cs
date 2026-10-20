using System;

namespace FirstChair.Sim
{
    /// <summary>The outcome of <see cref="Replayer.Replay(Recording, Heightmap, SnowMap, Forest, Tuning, Action{Simulation})"/>.</summary>
    public enum ReplayStatus
    {
        /// <summary>The recording replayed.</summary>
        Ok = 0,

        /// <summary>The recording was made with another sim version; nothing was replayed.</summary>
        SimVersionMismatch = 1,

        /// <summary>The recording was made with other tuning; nothing was replayed.</summary>
        TuningMismatch = 2,

        /// <summary>The recording was made on another heightmap; nothing was replayed.</summary>
        HeightmapMismatch = 3,

        /// <summary>The recording was made on another snow map; nothing was replayed.</summary>
        SnowMapMismatch = 4,

        /// <summary>The recording was made among other trees; nothing was replayed.</summary>
        ForestMismatch = 5,
    }

    /// <summary>What a replay did.</summary>
    /// <remarks>Immutable and thread-safe.</remarks>
    /// <example>
    /// <code>
    /// ReplayResult result = Replayer.Replay(recording, heightmap, snowMap, forest, tuning);
    /// if (result.Status != ReplayStatus.Ok) { /* tell the player the recording is from another version */ }
    /// </code>
    /// </example>
    public readonly struct ReplayResult
    {
        /// <summary>Whether the recording replayed, or why not.</summary>
        public readonly ReplayStatus Status;

        /// <summary>Steps run; zero on any mismatch.</summary>
        public readonly int StepsRun;

        /// <summary>The state after the last step; default on any mismatch.</summary>
        public readonly RiderState FinalState;

        /// <summary>Creates a result.</summary>
        /// <param name="status">The outcome.</param>
        /// <param name="stepsRun">Steps run.</param>
        /// <param name="finalState">State after the last step.</param>
        public ReplayResult(ReplayStatus status, int stepsRun, RiderState finalState)
        {
            Status = status;
            StepsRun = stepsRun;
            FinalState = finalState;
        }
    }

    /// <summary>
    /// Replays a <see cref="Recording"/>: checks that its sim version, tuning hash, heightmap hash, snow map hash and
    /// forest hash match the ones given, then rebuilds the ride from its header and inputs with a fresh
    /// <see cref="Simulation"/>. A mismatch runs no step at all.
    /// </summary>
    /// <remarks>Thread-safe as long as each call gets its own <see cref="Recording"/> or nobody appends to it.</remarks>
    /// <example>
    /// <code>
    /// ReplayResult result = Replayer.Replay(recording, heightmap, snowMap, forest, tuning,
    ///     simulation => ghost.Draw(simulation.State));
    /// </code>
    /// </example>
    public static class Replayer
    {
        /// <summary>Replays a recording made with no trees (<see cref="Forest.Empty"/>).</summary>
        /// <param name="recording">The recording.</param>
        /// <param name="heightmap">The terrain; must have the recorded hash.</param>
        /// <param name="snowMap">The snow types; must have the recorded hash.</param>
        /// <param name="tuning">The tuning; must have the recorded hash.</param>
        /// <param name="onStep">Optional callback after each step, given the simulation.</param>
        public static ReplayResult Replay(Recording recording, Heightmap heightmap, SnowMap snowMap, Tuning tuning, Action<Simulation> onStep = null) =>
            Replay(recording, heightmap, snowMap, Forest.Empty, tuning, onStep);

        /// <summary>Replays a recording.</summary>
        /// <param name="recording">The recording.</param>
        /// <param name="heightmap">The terrain; must have the recorded hash.</param>
        /// <param name="snowMap">The snow types; must have the recorded hash.</param>
        /// <param name="forest">The trees; must have the recorded hash.</param>
        /// <param name="tuning">The tuning; must have the recorded hash.</param>
        /// <param name="onStep">Optional callback after each step, given the simulation.</param>
        public static ReplayResult Replay(
            Recording recording, Heightmap heightmap, SnowMap snowMap, Forest forest, Tuning tuning, Action<Simulation> onStep = null)
        {
            if (recording.Setup.SimVersion != Simulation.Version)
            {
                return new ReplayResult(ReplayStatus.SimVersionMismatch, 0, default);
            }

            if (recording.TuningHash != tuning.ComputeHash())
            {
                return new ReplayResult(ReplayStatus.TuningMismatch, 0, default);
            }

            if (recording.HeightmapHash != heightmap.Hash)
            {
                return new ReplayResult(ReplayStatus.HeightmapMismatch, 0, default);
            }

            if (recording.SnowMapHash != snowMap.Hash)
            {
                return new ReplayResult(ReplayStatus.SnowMapMismatch, 0, default);
            }

            if (recording.ForestHash != forest.Hash)
            {
                return new ReplayResult(ReplayStatus.ForestMismatch, 0, default);
            }

            var simulation = new Simulation(recording.Setup, heightmap, snowMap, forest, tuning);
            var inputs = recording.Inputs;
            for (int i = 0; i < inputs.Count; i++)
            {
                simulation.Step(inputs[i]);
                onStep?.Invoke(simulation);
            }

            return new ReplayResult(ReplayStatus.Ok, inputs.Count, simulation.State);
        }
    }
}
