using System;
using System.Collections.Generic;
using System.Linq;
using FirstChair.Sim;

namespace FirstChair.Tools.RidePreview
{
    /// <summary>
    /// The scripted rides. Each start comes from the layout (its start, zones, kickers, trees and rails); the numbers
    /// below are script tuning for this tool, not riding tuning, and the sim is never changed.
    /// </summary>
    internal static class Rides
    {
        // groomer-carve: full-lock turns on a timer, wide then tight blocks; a turn ends early near the groomer's edge.
        private const double CarveWideTurnS = 2.1;
        private const double CarveTightTurnS = 0.6;
        private const int CarveTurnsPerBlock = 8;
        private const double CarveEdgeMarginM = 12.0;

        // park-line: run-up before the first kicker's foot, and the approach speed held (by braking) into each kicker.
        private const double ParkRunUpM = 30.0;
        private static readonly double[] ParkApproachSpeed = { 12.0, 12.5, 13.0 };
        private const double ParkBoxSpeed = 8.0;
        private const double ParkRailTouchdownM = 3.0;

        // glade-line: run-up above the glade and the beam search over the waypoints of the line.
        private const double GladeRunUpM = 50.0;
        private const double GladeWaypointStepM = 8.0;
        private const double GladeSearchSpanM = 8.0;
        private const double GladeSearchStepM = 2.0;
        private const int GladeBeamWidth = 4;

        // ice-stop: run-up above the ice, the diagonal across it, and where braking starts past its lower edge.
        private const double IceRunUpM = 70.0;
        private const double IceMarginM = 12.0;
        private const double IceBrakeAfterM = 1.0;

        private static readonly SimInput None = SimInput.Pack(SimButtons.None, 0);

        public static IEnumerable<RideScript> All(Slope slope)
        {
            yield return GroomerTuck(slope);
            yield return GroomerCarve(slope);
            yield return ParkLine(slope);
            yield return GladeLine(slope);
            yield return IceStop(slope);
        }

        /// <summary>
        /// Diagnostic (--probe-kickers): each kicker ridden straight at a range of held approach speeds, printing the
        /// speed just before the lip, the takeoff, and the flight. Not part of rides.json.
        /// </summary>
        public static void ProbeKickers(Slope slope, System.IO.TextWriter output)
        {
            output.WriteLine("Each kicker ridden straight; from the given offset past the lip (m) the given buttons are held while on snow.");
            output.WriteLine("kicker  approach  v@lip-0.3m  v@takeoff  takeoff-lip(m)  air(s)  maxH(m)  result");
            SlopeKicker[] kickers = slope.Layout.Kickers.OrderBy(k => k.ZMm).ToArray();
            for (int i = 0; i < kickers.Length; i++)
            {
                double lx = Num.Mm(kickers[i].XMm), lipZ = Num.Mm(kickers[i].ZMm), foot = lipZ - Num.Mm(kickers[i].LengthMm);
                foreach ((SimButtons afterLip, double from) in new[] { (SimButtons.None, -0.05), (SimButtons.Forward, -0.05), (SimButtons.Forward, 0.0), (SimButtons.Forward, 0.03), (SimButtons.Forward, 0.06), (SimButtons.Forward, 0.09), (SimButtons.Back, 0.03) })
                for (double target = 10; target <= 12.01; target += 2)
                {
                    var line = new LinePath((0.0, lx));
                    double beforeLip = double.NaN;
                    double held = target;
                    var script = new RideScript
                    {
                        Name = "probe",
                        StartX = lx,
                        StartZ = foot - 40,
                        MaxTicks = 20 * Simulation.StepsPerSecond,
                        Input = sim =>
                        {
                            RiderState s = sim.State;
                            double z = Num.Z(s), v = Num.Speed(s);
                            if (z < lipZ - 0.3)
                            {
                                beforeLip = v;
                            }

                            if (s.Airborne || s.Crashed)
                            {
                                return None;
                            }

                            if (z >= lipZ + from)
                            {
                                return Num.Pack(afterLip);
                            }

                            if (z >= foot - 3)
                            {
                                return None;
                            }

                            int steer = Steering.Toward(s, line, 3.0);
                            return v > held + 0.25 ? Num.Pack(SimButtons.Back, steer) : v < held - 1.0 ? Num.Pack(SimButtons.Forward, steer) : Num.Pack(SimButtons.None, steer);
                        },
                        Done = sim => Num.Z(sim.State) > lipZ + 30,
                    };
                    RideResult ride = RideRunner.Run(slope, script);
                    RideEvent takeoff = ride.Events.FirstOrDefault(e => e.Kind == "Takeoff");
                    RideEvent down = ride.Events.FirstOrDefault(e => e.Kind == "Land" || e.Kind == "Crash");
                    double Get(RideEvent e, string key) => e == null ? double.NaN : (double)e.Data.First(d => d.Key == key).Value;
                    output.WriteLine(
                        "  " + (i + 1) + " " + afterLip + "@" + from.ToString("+0.00;-0.00") + "  " + target.ToString("00") + "        " + beforeLip.ToString("0.00") + "      " + Get(takeoff, "speed").ToString("0.00")
                        + "       " + (Get(takeoff, "z") - lipZ).ToString("0.00") + "           " + Get(down, "airTime").ToString("0.00") + "    "
                        + Get(down, "maxHeight").ToString("0.00") + "     " + (down == null ? "-" : down.Kind + " " + down.Data.First(d => d.Key == "grade" || d.Key == "cause").Value));
                }
            }
        }

        private static RideScript GroomerTuck(Slope slope)
        {
            SlopeZone runout = slope.Zone("runout");
            double sx = Num.Mm(slope.Layout.StartXMm), sz = Num.Mm(slope.Layout.StartZMm);
            var line = new LinePath((sz, sx));
            double runoutZ = Num.Mm(runout.ZMinMm);
            return new RideScript
            {
                Name = "groomer-tuck",
                Description = "From the layout's start, tuck straight down the groomer (holding the start's x) to the run-out.",
                StartX = sx,
                StartZ = sz,
                // Tuck to the run-out, then stand and let it run: the run-out is meant to stop the rider.
                Input = sim => Num.Pack(Num.Z(sim.State) < runoutZ ? SimButtons.Forward : SimButtons.None, Steering.Toward(sim.State, line, 2.0)),
                Done = sim => Num.Z(sim.State) > runoutZ && Num.Speed(sim.State) < 0.3,
            };
        }

        private static RideScript GroomerCarve(Slope slope)
        {
            SlopeZone groomer = slope.Zone("groomer");
            SlopeZone runout = slope.Zone("runout");
            double center = (Num.Mm(groomer.XMinMm) + Num.Mm(groomer.XMaxMm)) / 2;
            double guard = (Num.Mm(groomer.XMaxMm) - Num.Mm(groomer.XMinMm)) / 2 - CarveEdgeMarginM;
            double runoutZ = Num.Mm(runout.ZMinMm);

            // The timer: half a wide turn first so the S stays centred, then blocks of wide and tight turns, with a
            // turn of the average length at each change of block.
            var schedule = new List<(string Phase, int Ticks)> { ("wide-1", Num.Ticks(CarveWideTurnS / 2)) };
            for (int block = 0; block < 6; block++)
            {
                bool wide = block % 2 == 0;
                if (block > 0)
                {
                    schedule.Add(((wide ? "wide-" : "tight-") + (block + 1), Num.Ticks((CarveWideTurnS + CarveTightTurnS) / 2)));
                }

                for (int i = 0; i < CarveTurnsPerBlock; i++)
                {
                    schedule.Add(wide ? ("wide-" + (block + 1), Num.Ticks(CarveWideTurnS)) : ("tight-" + (block + 1), Num.Ticks(CarveTightTurnS)));
                }
            }

            int index = 0, left = schedule[0].Ticks, direction = 1;
            return new RideScript
            {
                Name = "groomer-carve",
                Description = "S-turns down the groomer: full-lock left and right edges on a timer, wide turns then tight ones in blocks.",
                StartX = Num.Mm(slope.Layout.StartXMm),
                StartZ = Num.Mm(slope.Layout.StartZMm),
                MaxTicks = 150 * Simulation.StepsPerSecond,
                Phase = () => schedule[index].Phase,
                Input = sim =>
                {
                    RiderState s = sim.State;
                    if (s.Crashed || s.Airborne)
                    {
                        return None;
                    }

                    double x = Num.X(s), heading = Num.Wrap(Num.D(s.Heading));
                    bool pastRight = x > center + guard && direction > 0 && heading > 0;
                    bool pastLeft = x < center - guard && direction < 0 && heading < 0;
                    if (--left <= 0 || pastRight || pastLeft)
                    {
                        index = Math.Min(index + 1, schedule.Count - 1);
                        left = schedule[index].Ticks;
                        direction = -direction;
                    }

                    return Num.Pack(direction > 0 ? SimButtons.Right : SimButtons.Left);
                },
                Done = sim => Num.Z(sim.State) > runoutZ || (sim.State.Tick > 5 * Simulation.StepsPerSecond && Num.Speed(sim.State) < 0.2),
            };
        }

        private static RideScript ParkLine(Slope slope)
        {
            Tuning tuning = slope.Tuning;
            SlopeKicker[] kickers = slope.Layout.Kickers.OrderBy(k => k.ZMm).ToArray();
            if (kickers.Length < 3)
            {
                throw new ToolException("park-line needs 3 kickers; the layout has " + kickers.Length + ".");
            }

            double lineX = Num.Mm(kickers[0].XMm);
            double startZ = Num.Mm(kickers[0].ZMm) - Num.Mm(kickers[0].LengthMm) - ParkRunUpM;
            var line = new LinePath((startZ, lineX));
            var rails = new List<(int Index, SlopeRail Rail)>();
            for (int i = 0; i < slope.Rails.Count; i++)
            {
                if (Math.Abs(Num.Mm(slope.Rails[i].X0Mm) - lineX) < 1.0)
                {
                    rails.Add((i, slope.Rails[i]));
                }
            }

            double stepUp = Num.D(tuning.GrindStepUpHeight);
            double lastZ = Math.Max(kickers.Max(k => Num.Mm(k.ZMm)), rails.Count == 0 ? 0 : rails.Max(r => Num.Mm(r.Rail.Z1Mm)));
            int spinSteps = Num.StepsFor(2 * Math.PI, tuning.SpinRate);
            int flipSteps = Num.StepsFor(2 * Math.PI, tuning.FlipRate);
            int fullLoad = (int)Math.Ceiling(Num.D(tuning.PopLoadTime) * Simulation.StepsPerSecond - 1e-9);
            double popSpeed = Num.D(tuning.PopSpeedMax), gravity = Num.D(tuning.Gravity);

            bool wasAirborne = false, popping = false;
            int airStep = 0, load = 0;
            string plan = "none";
            var popped = new HashSet<int>();

            SimInput Input(Simulation sim)
            {
                RiderState s = sim.State;
                double z = Num.Z(s), speed = Num.Speed(s);
                if (s.Crashed)
                {
                    wasAirborne = false;
                    return None;
                }

                if (s.Airborne)
                {
                    if (!wasAirborne)
                    {
                        wasAirborne = true;
                        airStep = 0;
                        int k = Array.FindIndex(kickers, kk => Math.Abs(z - Num.Mm(kk.ZMm)) < 3.0);
                        plan = k == 0 ? "japan" : k == 1 ? "360" : k == 2 ? "backflip" : popping ? "pop" : "none";
                        popping = false;
                    }

                    airStep++;
                    switch (plan)
                    {
                        case "japan": return Num.Pack(SimButtons.Grab | SimButtons.Left);
                        case "360": return airStep <= spinSteps ? Num.Pack(SimButtons.Right) : None;
                        case "backflip": return airStep <= flipSteps ? Num.Pack(SimButtons.Back) : None;
                        default: return None;
                    }
                }

                wasAirborne = false;
                if (s.Grinding)
                {
                    // Counter-steer the lean, as the rail tests do.
                    return Num.Pack(SimButtons.None, s.Balance > Fix64.Zero ? -SimInput.SteeringMax : s.Balance < Fix64.Zero ? SimInput.SteeringMax : 0);
                }

                int steer = Steering.Toward(s, line, 3.0);

                // Pop onto rails too high to ride onto: load fully, release so the descent reaches the rail's top just past its start.
                foreach ((int index, SlopeRail rail) in rails)
                {
                    double height = Num.Mm(rail.HeightMm), z0 = Num.Mm(rail.Z0Mm);
                    if (height <= stepUp || popped.Contains(index) || z >= z0)
                    {
                        continue;
                    }

                    slope.HeightAt(Num.X(s), z, out double normalY);
                    double g = gravity * normalY;
                    double clear = height + 0.1;
                    double down = (popSpeed + Math.Sqrt(Math.Max(0, popSpeed * popSpeed - 2 * g * clear))) / g;
                    double touchdown = z0 + ParkRailTouchdownM;
                    if (load > 0)
                    {
                        if (load >= fullLoad && z + speed * down >= touchdown)
                        {
                            load = 0;
                            popped.Add(index);
                            popping = true;
                            return Num.Pack(SimButtons.None, steer);
                        }

                        load++;
                        return Num.Pack(SimButtons.Jump, steer);
                    }

                    if (z + speed * (fullLoad / (double)Simulation.StepsPerSecond + down) >= touchdown)
                    {
                        load = 1;
                        return Num.Pack(SimButtons.Jump, steer);
                    }
                }

                // Hold the approach speed by braking until the next kicker's ramp; no input on the ramp itself.
                // Nothing is pressed from 3 m before a ramp's foot until the rider leaves the snow past its lip.
                int next = Array.FindIndex(kickers, k => Num.Mm(k.ZMm) > z);
                double target = next < 0 ? ParkBoxSpeed : ParkApproachSpeed[Math.Min(next, ParkApproachSpeed.Length - 1)];
                bool onRamp = kickers.Any(k => z > Num.Mm(k.ZMm) - Num.Mm(k.LengthMm) - 3.0 && z < Num.Mm(k.ZMm) + 2.0);
                if (onRamp)
                {
                    return Num.Pack(SimButtons.None, steer);
                }

                return speed > target + 0.25 ? Num.Pack(SimButtons.Back, steer)
                    : speed < target - 1.0 ? Num.Pack(SimButtons.Forward, steer)
                    : Num.Pack(SimButtons.None, steer);
            }

            return new RideScript
            {
                Name = "park-line",
                Description = "Down the kicker line: Japan grab on kicker 1, box, 360 on kicker 2, pop onto the rail, backflip on kicker 3, box.",
                StartX = lineX,
                StartZ = startZ,
                MaxTicks = 60 * Simulation.StepsPerSecond,
                Input = Input,
                Done = sim => Num.Z(sim.State) > lastZ + 12 && !sim.State.Airborne && !sim.State.Grinding,
            };
        }

        private static RideScript GladeLine(Slope slope)
        {
            SlopeZone glade = slope.Zone("glade");
            double x0 = Num.Mm(glade.XMinMm), x1 = Num.Mm(glade.XMaxMm), z0 = Num.Mm(glade.ZMinMm), z1 = Num.Mm(glade.ZMaxMm);
            double startX = (x0 + x1) / 2, startZ = z0 - GladeRunUpM;
            var zs = new List<double>();
            for (double z = z0 - 6; z <= z1 + 1e-9; z += GladeWaypointStepM)
            {
                zs.Add(z);
            }

            // Beam search with the sim in the loop: extend each kept line by one waypoint, ride it from the start to that
            // waypoint, and keep the lines with the most near misses for the fewest branch clips and crashes.
            var beam = new List<(double[] Xs, double Score)> { (Array.Empty<double>(), 0) };
            for (int i = 0; i < zs.Count; i++)
            {
                var next = new List<(double[] Xs, double Score)>();
                double evaluateZ = zs[i];
                foreach ((double[] xs, _) in beam)
                {
                    double previous = i == 0 ? startX : xs[i - 1];
                    for (double dx = -GladeSearchSpanM; dx <= GladeSearchSpanM + 1e-9; dx += GladeSearchStepM)
                    {
                        double[] candidate = xs.Append(Math.Clamp(previous + dx, x0 + 2, x1 - 2)).ToArray();
                        if (next.Any(n => n.Xs.SequenceEqual(candidate)))
                        {
                            continue;
                        }

                        RideScript trial = GladeScript(slope, startX, startZ, zs.Take(i + 1).ToArray(), candidate, z1);
                        RideResult ride = RideRunner.Run(slope, trial, sim => Num.Z(sim.State) >= evaluateZ);
                        next.Add((candidate, GladeScore(ride)));
                    }
                }

                beam = next.OrderByDescending(n => n.Score).Take(GladeBeamWidth).ToList();
            }

            // The waypoint scores are partial rides; ride each kept line to the end and keep the best whole ride.
            double[] best = beam
                .Select(b => (b.Xs, Score: GladeScore(RideRunner.Run(slope, GladeScript(slope, startX, startZ, zs.ToArray(), b.Xs, z1)))))
                .OrderByDescending(b => b.Score)
                .First().Xs;
            return GladeScript(slope, startX, startZ, zs.ToArray(), best, z1);
        }

        private static double GladeScore(RideResult ride) =>
            10 * ride.Count(SimEventKind.NearMiss) - 4 * ride.Count(SimEventKind.BranchHit) - 60 * ride.Count(SimEventKind.Crash)
            + 5 * Num.D(ride.Last.Flow) + 0.2 * Num.Speed(ride.Last);

        private static RideScript GladeScript(Slope slope, double startX, double startZ, double[] zs, double[] xs, double gladeEndZ)
        {
            var points = new List<(double, double)> { (startZ, startX) };
            for (int i = 0; i < zs.Length; i++)
            {
                points.Add((zs[i], xs[i]));
            }

            var line = new LinePath(points.ToArray());
            double tuckUntil = zs[0] - 10;
            return new RideScript
            {
                Name = "glade-line",
                Description = "Through the glade on a gap-threading line (found by a search with the sim in the loop) for near misses that build Flow.",
                StartX = startX,
                StartZ = startZ,
                MaxTicks = 60 * Simulation.StepsPerSecond,
                Input = sim =>
                {
                    RiderState s = sim.State;
                    if (s.Crashed || s.Airborne)
                    {
                        return None;
                    }

                    int steer = Steering.Toward(s, line, 3.0, 0.45, 4.0);
                    return Num.Pack(Num.Z(s) < tuckUntil ? SimButtons.Forward : SimButtons.None, steer);
                },
                Done = sim => Num.Z(sim.State) > gladeEndZ + 10,
            };
        }

        private static RideScript IceStop(Slope slope)
        {
            SlopeZone ice = slope.Zone("ice");
            double x0 = Num.Mm(ice.XMinMm), x1 = Num.Mm(ice.XMaxMm), z0 = Num.Mm(ice.ZMinMm), z1 = Num.Mm(ice.ZMaxMm);
            double center = (x0 + x1) / 2, startZ = z0 - IceRunUpM;
            var line = new LinePath((startZ, center), (z0 - 12, center), (z0, x0 + IceMarginM), (z1, x1 - IceMarginM));
            bool braking = false;
            int still = 0;
            return new RideScript
            {
                Name = "ice-stop",
                Description = "Tuck into the ice patch, cross it on a diagonal at speed, then brake to a stop below it.",
                StartX = center,
                StartZ = startZ,
                MaxTicks = 60 * Simulation.StepsPerSecond,
                Phase = () => braking ? "brake" : "ride",
                Input = sim =>
                {
                    RiderState s = sim.State;
                    if (s.Crashed || s.Airborne)
                    {
                        return None;
                    }

                    double z = Num.Z(s);
                    int steer = Steering.Toward(s, line, 3.0);
                    braking |= z > z1 + IceBrakeAfterM;
                    return braking ? Num.Pack(SimButtons.Back, steer)
                        : z < z0 - 2 ? Num.Pack(SimButtons.Forward, steer)
                        : Num.Pack(SimButtons.None, steer);
                },
                Done = sim =>
                {
                    still = braking && Num.Speed(sim.State) < 0.05 ? still + 1 : 0;
                    return still >= Simulation.StepsPerSecond / 2;
                },
            };
        }
    }
}
