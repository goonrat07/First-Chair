using System;

namespace FirstChair.Sim
{
    /// <summary>What a <see cref="SimEvent"/> reports.</summary>
    public enum SimEventKind : byte
    {
        /// <summary>The rider crossed from on the heightmap to off it.</summary>
        LeftHeightmap = 1,
    }

    /// <summary>
    /// Something that happened during one step. <see cref="Simulation.Events"/> holds the events of the
    /// last step; the game layer drains them after each step instead of polling rider state.
    /// </summary>
    /// <remarks>Immutable and thread-safe.</remarks>
    /// <example>
    /// <code>
    /// simulation.Step(input);
    /// foreach (SimEvent simEvent in simulation.Events)
    /// {
    ///     if (simEvent.Kind == SimEventKind.LeftHeightmap) { /* game layer reacts */ }
    /// }
    /// </code>
    /// </example>
    public readonly struct SimEvent : IEquatable<SimEvent>
    {
        /// <summary><see cref="RiderState.Tick"/> after the step that raised the event.</summary>
        public readonly int Tick;

        /// <summary>What happened.</summary>
        public readonly SimEventKind Kind;

        /// <summary>Creates an event.</summary>
        /// <param name="tick">Tick after the step that raised it.</param>
        /// <param name="kind">What happened.</param>
        public SimEvent(int tick, SimEventKind kind)
        {
            Tick = tick;
            Kind = kind;
        }

        /// <inheritdoc/>
        public bool Equals(SimEvent other) => Tick == other.Tick && Kind == other.Kind;

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is SimEvent other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => unchecked(Tick * 397 ^ (int)Kind);

        /// <inheritdoc/>
        public override string ToString() => Kind + "@" + Tick;
    }
}
