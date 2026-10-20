using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FirstChair.Sim;

namespace FirstChair.Tools.RidePreview
{
    /// <summary>
    /// One scripted ride: where it starts and, every step, what to press. The input and done callbacks may keep state
    /// in their closures, so a script instance is run once; build a new one to run it again.
    /// </summary>
    internal sealed class RideScript
    {
        public string Name;
        public string Description;
        public double StartX;
        public double StartZ;
        public ulong Seed = 1;
        public int MaxTicks = 90 * Simulation.StepsPerSecond;

        /// <summary>Called before each step with the state the step starts from.</summary>
        public Func<Simulation, SimInput> Input;

        /// <summary>Called after each step; true ends the ride.</summary>
        public Func<Simulation, bool> Done;

        /// <summary>Optional label for the current part of the script, used only for the printed skid shares.</summary>
        public Func<string> Phase;
    }

    /// <summary>Steering toward a line: proportional on the heading error to a point a little way ahead on it.</summary>
    internal static class Steering
    {
        /// <param name="gain">Full locks per radian of heading error.</param>
        public static int Toward(RiderState state, LinePath path, double gain = 3.0, double lookTime = 0.6, double minLook = 5.0)
        {
            double x = Num.X(state), z = Num.Z(state);
            double look = Math.Max(minLook, Num.Speed(state) * lookTime);
            double want = Math.Atan2(path.XAt(z + look) - x, look);
            return Quantize(Num.Wrap(want - Num.D(state.Heading)) * gain);
        }

        /// <summary>Full locks (-1 to 1) to the packed steering value, quantized before it reaches the sim.</summary>
        public static int Quantize(double fullLocks) => (int)Math.Round(Math.Clamp(fullLocks, -1.0, 1.0) * SimInput.SteeringMax);
    }

    /// <summary>A line down the slope: x as a piecewise-linear function of z, held flat past either end.</summary>
    internal sealed class LinePath
    {
        private readonly double[] _z;
        private readonly double[] _x;

        public LinePath(params (double Z, double X)[] points)
        {
            _z = points.Select(p => p.Z).ToArray();
            _x = points.Select(p => p.X).ToArray();
        }

        public double XAt(double z)
        {
            if (z <= _z[0])
            {
                return _x[0];
            }

            for (int i = 1; i < _z.Length; i++)
            {
                if (z <= _z[i])
                {
                    double t = (z - _z[i - 1]) / (_z[i] - _z[i - 1]);
                    return _x[i - 1] + (_x[i] - _x[i - 1]) * t;
                }
            }

            return _x[_x.Length - 1];
        }
    }

    internal static class RideRunner
    {
        public const int SampleEvery = 4;

        /// <param name="stop">An extra stop condition (used by searches that only need part of a ride).</param>
        public static RideResult Run(Slope slope, RideScript script, Func<Simulation, bool> stop = null)
        {
            var setup = new RunSetup(Sport.Skis, 0, script.Seed, Num.F(script.StartX), Num.F(script.StartZ));
            var simulation = new Simulation(setup, slope.Heightmap, slope.Snow, slope.Forest, slope.Rails, slope.Tuning);
            var ride = new RideResult(script, slope);
            ride.Sample(simulation.State);
            string end = "time cap";
            for (int i = 0; i < script.MaxTicks; i++)
            {
                simulation.Step(script.Input(simulation));
                IReadOnlyList<SimEvent> events = simulation.Events;
                for (int e = 0; e < events.Count; e++)
                {
                    ride.Event(events[e], simulation.State);
                }

                RiderState state = simulation.State;
                ride.Observe(state, script.Phase?.Invoke());
                if (state.Tick % SampleEvery == 0)
                {
                    ride.Sample(state);
                }

                if (state.OutsideHeightmap)
                {
                    end = "left heightmap";
                    break;
                }

                if (script.Done(simulation) || (stop != null && stop(simulation)))
                {
                    end = "script done";
                    break;
                }
            }

            ride.Finish(simulation.State, end);
            return ride;
        }
    }

    /// <summary>A sample of the rider every <see cref="RideRunner.SampleEvery"/> steps, in metres, seconds and radians.</summary>
    internal readonly struct RideSample
    {
        public readonly int Tick;
        public readonly double X, Y, Z, Speed, Heading, Edge, Flow;
        public readonly Stance Stance;
        public readonly bool Airborne, Grinding, Crashed, Skidding;
        public readonly int Snow;

        public RideSample(RiderState s)
        {
            Tick = s.Tick;
            X = Num.D(s.Position.X);
            Y = Num.D(s.Position.Y);
            Z = Num.D(s.Position.Z);
            Speed = Num.Speed(s);
            Heading = Num.Wrap(Num.D(s.Heading));
            Edge = Num.D(s.EdgeAngle);
            Flow = Num.D(s.Flow);
            Stance = s.Stance;
            Airborne = s.Airborne;
            Grinding = s.Grinding;
            Crashed = s.Crashed;
            Skidding = s.Skidding;
            Snow = (int)s.Snow;
        }
    }

    /// <summary>An event as the viewer gets it: tick, kind, and the key data of that kind as ordered name/value pairs.</summary>
    internal sealed class RideEvent
    {
        public RideEvent(int tick, string kind)
        {
            Tick = tick;
            Kind = kind;
        }

        public int Tick { get; }

        public string Kind { get; }

        public List<KeyValuePair<string, object>> Data { get; } = new List<KeyValuePair<string, object>>();

        public void Add(string name, object value) => Data.Add(new KeyValuePair<string, object>(name, value));
    }

    /// <summary>Everything one ride produced, plus the counts the console summary prints.</summary>
    internal sealed class RideResult
    {
        private readonly Slope _slope;
        private readonly SortedDictionary<string, int> _counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        private readonly SortedDictionary<string, (int Snow, int Skid)> _skidBySnow = new SortedDictionary<string, (int, int)>(StringComparer.Ordinal);
        private readonly List<string> _phaseOrder = new List<string>();
        private readonly Dictionary<string, (int Snow, int Skid, double SpeedSum)> _skidByPhase = new Dictionary<string, (int, int, double)>(StringComparer.Ordinal);

        public RideResult(RideScript script, Slope slope)
        {
            Script = script;
            _slope = slope;
            Score = new RunScore(slope.Tuning);
        }

        public RideScript Script { get; }

        public RunScore Score { get; }

        public List<RideSample> Samples { get; } = new List<RideSample>();

        public List<RideEvent> Events { get; } = new List<RideEvent>();

        public double MaxSpeed { get; private set; }

        public RiderState Last { get; private set; }

        public string EndReason { get; private set; }

        public int Count(SimEventKind kind) => _counts.TryGetValue(kind.ToString(), out int n) ? n : 0;

        public IReadOnlyDictionary<string, int> Counts => _counts;

        public void Sample(RiderState state) => Samples.Add(new RideSample(state));

        public void Observe(RiderState state, string phase)
        {
            Last = state;
            MaxSpeed = Math.Max(MaxSpeed, Num.Speed(state));
            if (state.Airborne || state.Grinding || state.Crashed)
            {
                return;
            }

            int skid = state.Skidding ? 1 : 0;
            string snow = state.Snow.ToString();
            _skidBySnow[snow] = _skidBySnow.TryGetValue(snow, out var s) ? (s.Snow + 1, s.Skid + skid) : (1, skid);
            if (phase != null)
            {
                if (!_skidByPhase.TryGetValue(phase, out var p))
                {
                    _phaseOrder.Add(phase);
                    p = (0, 0, 0.0);
                }

                _skidByPhase[phase] = (p.Snow + 1, p.Skid + skid, p.SpeedSum + Num.Speed(state));
            }
        }

        /// <param name="after">The state after the step that raised the event: its position is where the event happened.</param>
        public void Event(SimEvent e, RiderState after)
        {
            string kind = e.Kind.ToString();
            _counts[kind] = _counts.TryGetValue(kind, out int n) ? n + 1 : 1;
            var record = new RideEvent(e.Tick, kind);
            record.Add("x", Num.X(after));
            record.Add("y", Num.D(after.Position.Y));
            record.Add("z", Num.Z(after));
            switch (e.Kind)
            {
                case SimEventKind.Takeoff:
                    record.Add("speed", Num.D(e.Speed));
                    break;
                case SimEventKind.Land:
                    record.Add("grade", e.Grade.ToString());
                    AddAir(record, e.Air);
                    break;
                case SimEventKind.Crash:
                    record.Add("cause", e.Cause.ToString());
                    if (e.Cause == CrashCause.Tree)
                    {
                        record.Add("tree", e.Tree);
                        record.Add("speed", Num.D(e.Speed));
                    }
                    else if (e.Cause == CrashCause.RailFall)
                    {
                        record.Add("rail", e.Rail);
                    }
                    else
                    {
                        AddAir(record, e.Air);
                    }

                    break;
                case SimEventKind.NearMiss:
                    record.Add("tree", e.Tree);
                    record.Add("gap", Num.D(e.Gap));
                    record.Add("speed", Num.D(e.Speed));
                    break;
                case SimEventKind.BranchHit:
                    record.Add("tree", e.Tree);
                    record.Add("speed", Num.D(e.Speed));
                    break;
                case SimEventKind.GrindStart:
                    record.Add("rail", e.Rail);
                    record.Add("box", _slope.Rails[e.Rail].Box);
                    break;
                case SimEventKind.GrindEnd:
                    record.Add("rail", e.Rail);
                    record.Add("box", e.Grind.Box);
                    record.Add("fell", e.Grind.Fell);
                    record.Add("seconds", Num.D(e.Grind.Seconds));
                    record.Add("balance", Num.D(e.Grind.Balance));
                    break;
            }

            int before = Score.Tricks.Count;
            Score.Add(e);
            if (Score.Tricks.Count > before)
            {
                ScoredTrick trick = Score.Tricks[Score.Tricks.Count - 1];
                record.Add("trick", trick.Name);
                record.Add("points", trick.Points);
                if (trick.Repeat > 0)
                {
                    record.Add("repeat", trick.Repeat);
                }
            }

            Events.Add(record);
        }

        public void Finish(RiderState state, string endReason)
        {
            Last = state;
            EndReason = endReason;
            if (Samples.Count == 0 || Samples[Samples.Count - 1].Tick != state.Tick)
            {
                Sample(state);
            }
        }

        public void PrintSummary(TextWriter output)
        {
            double seconds = Last.Tick / (double)Simulation.StepsPerSecond;
            output.WriteLine();
            output.WriteLine(
                "== " + Script.Name + ": " + seconds.ToString("0.0") + " s, end " + EndReason + " at (" + Num.X(Last).ToString("0.0") + ", "
                + Num.Z(Last).ToString("0.0") + "), speed " + Num.Speed(Last).ToString("0.0") + " m/s, max " + MaxSpeed.ToString("0.0")
                + " m/s, final flow " + Num.D(Last.Flow).ToString("0.00") + ", score " + Score.Total);
            output.WriteLine("   events: " + (_counts.Count == 0 ? "none" : string.Join(", ", _counts.Select(c => c.Key + " " + c.Value))));
            output.WriteLine("   skid share on snow by snow type: " + string.Join(", ", _skidBySnow.Select(s => s.Key + " " + Share(s.Value))));
            if (_phaseOrder.Count > 0)
            {
                output.WriteLine("   skid share by phase: " + string.Join(", ", _phaseOrder.Select(p => p + " " + Share((_skidByPhase[p].Snow, _skidByPhase[p].Skid)) + " at mean " + (_skidByPhase[p].SpeedSum / _skidByPhase[p].Snow).ToString("0.0") + " m/s")));
            }

            foreach (RideEvent e in Events.Where(e => e.Kind != "NearMiss"))
            {
                output.WriteLine("   t " + (e.Tick / 120.0).ToString("0.00") + " " + e.Kind + " "
                    + string.Join(" ", e.Data.Select(d => d.Key + "=" + (d.Value is double v ? v.ToString("0.###") : d.Value))));
            }
        }

        private static string Share((int Snow, int Skid) counts) =>
            (100.0 * counts.Skid / Math.Max(1, counts.Snow)).ToString("0") + "% of " + counts.Snow;

        private static void AddAir(RideEvent record, AirSummary air)
        {
            record.Add("airTime", air.AirTicks / (double)Simulation.StepsPerSecond);
            record.Add("maxHeight", Num.D(air.MaxHeight));
            record.Add("spinDeg", Num.D(air.Spin) * 180 / Math.PI);
            record.Add("flipDeg", Num.D(air.Flip) * 180 / Math.PI);
            record.Add("grab", air.Grab.ToString());
            record.Add("grabTime", air.GrabTicks / (double)Simulation.StepsPerSecond);
            record.Add("landingAngleDeg", Num.D(air.LandingAngle) * 180 / Math.PI);
            record.Add("switch", air.Switch);
            record.Add("flow", Num.D(air.Flow));
        }
    }
}
