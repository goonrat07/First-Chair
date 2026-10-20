using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using FirstChair.Sim;

namespace FirstChair.Sim.Tests
{
    /// <summary>
    /// The scripted ride the determinism tests record and replay: 12 s (1440 steps) from a fixed start on
    /// the +Z incline over banded snow (all five types), with a fixed, varied input pattern built from integer
    /// arithmetic (no random source). It carves, skids, tucks and brakes, and leaves the 128 m map once, at
    /// about 10.7 s, so the ride raises one event.
    /// </summary>
    internal static class ScriptedRide
    {
        public const int Steps = 12 * Simulation.StepsPerSecond;

        public static readonly RunSetup Setup =
            new RunSetup(Sport.Skis, 0, 0x5EED_F1A5_C0DE_0001UL, Fix64.FromInt(64), Fix64.FromInt(20));

        /// <summary>
        /// The input for a step: every button and the full steering range get exercised. Left and right come as
        /// 15-step taps half a period apart and the analog value is a triangle wave, so the line swings evenly
        /// about the fall line; one-sided steering would turn the carving rider in circles on the map.
        /// </summary>
        public static SimInput InputAt(int step)
        {
            SimButtons buttons = SimButtons.None;
            if (step % 160 < 15)
            {
                buttons |= SimButtons.Left;
            }

            if ((step + 80) % 160 < 15)
            {
                buttons |= SimButtons.Right;
            }

            if (step % 240 < 120)
            {
                buttons |= SimButtons.Forward;
            }

            if (step % 400 >= 390)
            {
                buttons |= SimButtons.Back;
            }

            if (step % 300 == 7)
            {
                buttons |= SimButtons.Jump;
            }

            if (step % 500 < 25)
            {
                buttons |= SimButtons.Grab;
            }

            int steering = 127 - Math.Abs(step * 21 % 508 - 254);
            return SimInput.Pack(buttons, steering);
        }

        /// <summary>Rides the script live, recording it. Adds a snapshot after every step when a list is given.</summary>
        public static Recording Record(Heightmap heightmap, SnowMap snowMap, Tuning tuning, int steps, List<StepSnapshot> snapshots)
        {
            var simulation = new Simulation(Setup, heightmap, snowMap, tuning);
            var recording = new Recording(Setup, heightmap, snowMap, tuning);
            for (int step = 0; step < steps; step++)
            {
                SimInput input = InputAt(step);
                recording.Append(input);
                simulation.Step(input);
                snapshots?.Add(StepSnapshot.Of(simulation));
            }

            return recording;
        }

        /// <summary>Serializes a trace (every field of every snapshot, little-endian) for hashing.</summary>
        public static byte[] TraceBytes(IReadOnlyList<StepSnapshot> trace)
        {
            var bytes = new List<byte>();
            foreach (StepSnapshot snapshot in trace)
            {
                RiderState state = snapshot.State;
                foreach (long raw in new[]
                {
                    state.Position.X.Raw, state.Position.Y.Raw, state.Position.Z.Raw,
                    state.Velocity.X.Raw, state.Velocity.Y.Raw, state.Velocity.Z.Raw,
                    state.Heading.Raw, state.EdgeAngle.Raw, state.Skidding ? 1L : 0L, state.SlipAngle.Raw,
                    (long)state.Snow, (long)state.Stance,
                    state.Tick, state.OutsideHeightmap ? 1L : 0L, state.LastInput.Packed, unchecked((long)state.Rng.State),
                    snapshot.Events.Length,
                })
                {
                    AddLittleEndian(bytes, raw);
                }

                foreach (SimEvent simEvent in snapshot.Events)
                {
                    AddLittleEndian(bytes, simEvent.Tick);
                    bytes.Add((byte)simEvent.Kind);
                }
            }

            return bytes.ToArray();
        }

        private static void AddLittleEndian(List<byte> bytes, long value)
        {
            var buffer = new byte[8];
            BinaryPrimitives.WriteInt64LittleEndian(buffer, value);
            bytes.AddRange(buffer);
        }
    }

    /// <summary>The rider state and the events of one step, compared bit for bit.</summary>
    internal sealed class StepSnapshot : IEquatable<StepSnapshot>
    {
        public StepSnapshot(RiderState state, SimEvent[] events)
        {
            State = state;
            Events = events;
        }

        public RiderState State { get; }

        public SimEvent[] Events { get; }

        public static StepSnapshot Of(Simulation simulation) => new StepSnapshot(simulation.State, simulation.Events.ToArray());

        /// <summary>
        /// Position and velocity compare raw Q32.32 bits; the rest of the state (heading, edge, skid, snow,
        /// stance, tick, off-map flag, last input, random generator) and the event list must match too.
        /// </summary>
        public bool Equals(StepSnapshot other) =>
            other != null
            && State.Position.X.Raw == other.State.Position.X.Raw
            && State.Position.Y.Raw == other.State.Position.Y.Raw
            && State.Position.Z.Raw == other.State.Position.Z.Raw
            && State.Velocity.X.Raw == other.State.Velocity.X.Raw
            && State.Velocity.Y.Raw == other.State.Velocity.Y.Raw
            && State.Velocity.Z.Raw == other.State.Velocity.Z.Raw
            && State.Equals(other.State)
            && Events.SequenceEqual(other.Events);

        public override bool Equals(object obj) => Equals(obj as StepSnapshot);

        public override int GetHashCode() => State.GetHashCode();

        public override string ToString() => State + " events [" + string.Join(", ", Events) + "]";
    }
}
