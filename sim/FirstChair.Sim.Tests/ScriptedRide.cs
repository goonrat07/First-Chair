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
    /// arithmetic (no random source). It carves, skids, tucks and brakes, and since Story 004 (18 s) it pops three
    /// times: a Japan grab and a 360 that land, and a flip that crashes, with a reset tap while the rider lies
    /// crashed. It leaves the 128 m map once. Since Story 005 it rides among two trees (<see cref="Trees"/>): it passes
    /// the first close (a near miss) and clips the second's branches (a branch hit, and a near miss of its trunk).
    /// </summary>
    internal static class ScriptedRide
    {
        public const int Steps = 18 * Simulation.StepsPerSecond;

        /// <summary>Release steps of the three full-load pops (Story 004).</summary>
        public const int GrabJumpRelease = 300, SpinJumpRelease = 700, CrashJumpRelease = 1100;

        /// <summary>A full load at the default <see cref="Tuning.PopLoadTime"/> (0.5 s).</summary>
        public const int PopLoadSteps = 60;

        /// <summary>Steps of each jump's scripted air input, from the release step; longer than the flight.</summary>
        public const int AirWindow = 150;

        /// <summary>One turn at the default <see cref="Tuning.SpinRate"/> (2.5 pi rad/s): lands clean.</summary>
        public const int SpinSteps = 96;

        /// <summary>A 60 degree forward flip at the default <see cref="Tuning.FlipRate"/>: past sketchy, a crash.</summary>
        public const int FlipSteps = 20;

        /// <summary>The step whose input taps reset: the rider is lying crashed after the third jump.</summary>
        public const int ResetStep = 1300;

        private static bool Loading(int step, int release) => step >= release - PopLoadSteps && step < release;

        private static bool InWindow(int step, int release) => step >= release && step < release + AirWindow;

        public static readonly RunSetup Setup =
            new RunSetup(Sport.Skis, 0, 0x5EED_F1A5_C0DE_0001UL, Fix64.FromInt(64), Fix64.FromInt(20));

        /// <summary>
        /// Two trees beside the line before the first pop (Story 005): tree 0, 1.7 m right of it with no branches past its
        /// trunk (a near miss), and tree 1, 1.5 m left of it with 1.5 m branches (a branch clip, then a near miss).
        /// </summary>
        public static readonly Forest Trees = new Forest(new[]
        {
            new SlopeTree(65_200, 25_500, 200, 200),
            new SlopeTree(62_000, 26_600, 200, 1_500),
        });

        /// <summary>
        /// The input for a step: every button and the full steering range get exercised. Left and right come as
        /// 15-step taps half a period apart and the analog value is a triangle wave, so the line swings evenly
        /// about the fall line; one-sided steering would turn the carving rider in circles on the map.
        /// </summary>
        public static SimInput InputAt(int step)
        {
            // Each jump's air window replaces the riding pattern: a Japan grab, a 360 to the right, a 60 degree flip.
            if (InWindow(step, GrabJumpRelease))
            {
                return SimInput.Pack(step - GrabJumpRelease < 40 ? SimButtons.Grab | SimButtons.Left : SimButtons.None, 0);
            }

            if (InWindow(step, SpinJumpRelease))
            {
                return SimInput.Pack(step - SpinJumpRelease < SpinSteps ? SimButtons.Right : SimButtons.None, 0);
            }

            if (InWindow(step, CrashJumpRelease))
            {
                return SimInput.Pack(step - CrashJumpRelease < FlipSteps ? SimButtons.Forward : SimButtons.None, 0);
            }

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

            // Jump: loaded for PopLoadSteps before each release step.
            if (Loading(step, GrabJumpRelease) || Loading(step, SpinJumpRelease) || Loading(step, CrashJumpRelease))
            {
                buttons |= SimButtons.Jump;
            }

            // Reset: one tap, while the rider lies crashed after the third jump.
            if (step == ResetStep)
            {
                buttons |= SimButtons.Reset;
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
            var simulation = new Simulation(Setup, heightmap, snowMap, Trees, tuning);
            var recording = new Recording(Setup, heightmap, snowMap, Trees, tuning);
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
                    state.Airborne ? 1L : 0L, state.PopLoadTicks, state.AirTicks, state.MaxHeight.Raw,
                    state.SpinAngle.Raw, state.FlipAngle.Raw, (long)state.Grab, state.GrabTicks,
                    (long)state.BestGrab, state.BestGrabTicks, state.Crashed ? 1L : 0L, state.CrashTicks,
                    state.SafePosition.X.Raw, state.SafePosition.Y.Raw, state.SafePosition.Z.Raw, state.SafeHeading.Raw,
                    state.Flow.Raw, state.FlowIdleTicks,
                    snapshot.Events.Length,
                })
                {
                    AddLittleEndian(bytes, raw);
                }

                foreach (SimEvent simEvent in snapshot.Events)
                {
                    AddLittleEndian(bytes, simEvent.Tick);
                    bytes.Add((byte)simEvent.Kind);
                    bytes.Add((byte)simEvent.Grade);
                    bytes.Add((byte)simEvent.Cause);
                    bytes.Add((byte)simEvent.Air.Grab);
                    foreach (long raw in new[]
                    {
                        simEvent.Speed.Raw, simEvent.Air.AirTicks, simEvent.Air.MaxHeight.Raw, simEvent.Air.Spin.Raw,
                        simEvent.Air.Flip.Raw, simEvent.Air.GrabTicks, simEvent.Air.LandingAngle.Raw,
                        simEvent.Tree, simEvent.Gap.Raw,
                    })
                    {
                        AddLittleEndian(bytes, raw);
                    }
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
