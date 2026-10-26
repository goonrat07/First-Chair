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
    /// crashed. It leaves the 128 m map. Since Story 005 it rides among two trees (<see cref="Trees"/>): it passes
    /// the first close (a near miss) and clips the second's branches (a branch hit, and a near miss of its trunk). Since
    /// Story 006 it rides onto a low box (<see cref="Rails"/>) after the reset, grinds it and leaves it, and its landed jumps
    /// fold to scored tricks. Since Story 008 (sim version 7) it carves and quick-turns, poles on the level ground past
    /// the map's edge, and falls off the end of the grind. Since Story 009 (sim version 8) plain steering carves, grab with
    /// steering on snow drifts, a window of the quick button quick-turns, and the third jump flies off the map's edge, so
    /// the reset tap comes later, while the rider lies crashed beyond it.
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

        /// <summary>The step whose input taps reset: the rider is lying crashed after the third jump (it lands at step 1224).</summary>
        public const int ResetStep = 1260;

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
        /// One ride-on box (Story 006) along the line the rider takes after the reset tap, about 5.9 m long from 132.4 m
        /// down, on the level ground beyond the map's edge (the reset spot is at the edge). The rider rides onto it at about
        /// 3 m/s and grinds it to its end (sim version 8).
        /// </summary>
        public static readonly RailSet Rails = new RailSet(new[]
        {
            new SlopeRail(46_300, 132_400, 44_200, 137_900, 300, 600, true),
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
                // The reset tap falls just after this window, while the rider lies crashed.
                SimButtons air = step - CrashJumpRelease < FlipSteps ? SimButtons.Forward : SimButtons.None;
                return SimInput.Pack(step == ResetStep ? air | SimButtons.Reset : air, 0);
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

            // Quick turns (Story 009): a window every 300 steps; where forward isn't held, the steering quick-turns.
            if (step % 300 >= 200 && step % 300 < 245)
            {
                buttons |= SimButtons.Quick;
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
            var simulation = new Simulation(Setup, heightmap, snowMap, Trees, Rails, tuning);
            var recording = new Recording(Setup, heightmap, snowMap, Trees, Rails, tuning);
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
                    state.SwitchTakeoff ? 1L : 0L, state.Grinding ? 1L : 0L, state.GrindRail, state.GrindTicks, state.Balance.Raw,
                    state.GrindLeanSum.Raw, state.EdgeFlip, state.DriftTicks, state.SkateTicks,
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
                        simEvent.Air.Switch ? 1L : 0L, simEvent.Air.Flow.Raw, simEvent.Rail, simEvent.Grind.Ticks,
                        simEvent.Grind.Balance.Raw, simEvent.Grind.Fell ? 1L : 0L, simEvent.Grind.Box ? 1L : 0L,
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
        public StepSnapshot(RiderState state, SimEvent[] events, ulong treePassHash)
        {
            State = state;
            Events = events;
            TreePassHash = treePassHash;
        }

        public RiderState State { get; }

        public SimEvent[] Events { get; }

        /// <summary>Simulation.TreePassHash: per-tree pass state that lives outside RiderState.</summary>
        public ulong TreePassHash { get; }

        public static StepSnapshot Of(Simulation simulation) =>
            new StepSnapshot(simulation.State, simulation.Events.ToArray(), simulation.TreePassHash);

        /// <summary>
        /// Position and velocity compare raw Q32.32 bits; the rest of the state (heading, edge, skid, snow,
        /// stance, tick, off-map flag, last input, random generator), the event list and the per-tree pass state
        /// must match too. The golden trace (TraceBytes) reads the state and events only.
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
            && Events.SequenceEqual(other.Events)
            && TreePassHash == other.TreePassHash;

        public override bool Equals(object obj) => Equals(obj as StepSnapshot);

        public override int GetHashCode() => State.GetHashCode();

        public override string ToString() =>
            State + " events [" + string.Join(", ", Events) + "] tree pass " + TreePassHash.ToString("X16");
    }
}
