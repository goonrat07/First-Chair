using System;
using System.Collections.Generic;

namespace FirstChair.Sim
{
    /// <summary>What a <see cref="ScoredTrick"/> was.</summary>
    public enum ScoredTrickKind : byte
    {
        /// <summary>A landed jump (<see cref="SimEventKind.Land"/>).</summary>
        Jump = 1,

        /// <summary>A finished grind (<see cref="SimEventKind.GrindEnd"/>).</summary>
        Grind = 2,
    }

    /// <summary>One scored trick of a run: its name, ID, repeat count and points.</summary>
    /// <remarks>Immutable and thread-safe.</remarks>
    public readonly struct ScoredTrick
    {
        /// <summary>Tick of the event that scored it.</summary>
        public readonly int Tick;

        /// <summary>A jump or a grind.</summary>
        public readonly ScoredTrickKind Kind;

        /// <summary>The display name, for example "Left 720 Japan" or "Rail Grind".</summary>
        public readonly string Name;

        /// <summary>The name in kebab-case (<see cref="TrickScoring.Id"/>): repeats are counted by it.</summary>
        public readonly string Id;

        /// <summary>Earlier scorings of the same ID in the run; zero for the first. Grinds are not decayed and report zero.</summary>
        public readonly int Repeat;

        /// <summary>Whole points added to the run total.</summary>
        public readonly int Points;

        /// <summary>Creates a scored trick.</summary>
        /// <param name="tick">Tick of the event.</param>
        /// <param name="kind">Jump or grind.</param>
        /// <param name="name">Display name.</param>
        /// <param name="id">Kebab-case ID.</param>
        /// <param name="repeat">Earlier scorings of the ID.</param>
        /// <param name="points">Points scored.</param>
        public ScoredTrick(int tick, ScoredTrickKind kind, string name, string id, int repeat, int points)
        {
            Tick = tick;
            Kind = kind;
            Name = name;
            Id = id;
            Repeat = repeat;
            Points = points;
        }

        /// <inheritdoc/>
        public override string ToString() => Name + " " + Points + (Repeat > 0 ? " (repeat " + Repeat + ")" : string.Empty) + " @" + Tick;
    }

    /// <summary>
    /// A run's score (Story 006) as a deterministic fold over the sim's event stream: feed it every step's
    /// <see cref="Simulation.Events"/> in order and it names and scores each landed jump and each finished grind, keeps
    /// the repeat history, and sums the run total.
    /// </summary>
    /// <remarks>
    /// <para>The score lives outside <see cref="Simulation"/> so <see cref="Simulation.Step"/> stays free of strings and
    /// allocation. It depends only on the events and the <see cref="Tuning"/>, so the same recording always folds to the
    /// same total; <see cref="Replayer"/> folds every replay and reports it in <see cref="ReplayResult.TotalScore"/>.</para>
    /// <para><b>Rules.</b> A landed jump scores <see cref="TrickScoring.JumpValue"/> times
    /// <see cref="Tuning.TrickRepeatFactor"/> once per earlier landing of the same trick ID in the run. A finished grind
    /// scores <see cref="TrickScoring.GrindValue"/>; grinds have no repeat decay. Crashes take nothing away: there are no
    /// combos in the MVP, so everything already scored stays. A landing crash scores nothing.</para>
    /// <para>The repeat history is two parallel lists searched in order (the sim assembly uses no hash tables), so
    /// nothing depends on hash order. Not thread-safe.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var score = new RunScore(tuning);
    /// simulation.Step(input);
    /// score.AddAll(simulation.Events);
    /// hud.ShowTotal(score.Total);
    /// </code>
    /// </example>
    public sealed class RunScore
    {
        private readonly Tuning _tuning;
        private readonly List<string> _ids = new List<string>();
        private readonly List<int> _landings = new List<int>();
        private readonly List<ScoredTrick> _tricks = new List<ScoredTrick>();

        /// <summary>Starts an empty run score.</summary>
        /// <param name="tuning">The tuning the run rides on, copied once.</param>
        /// <exception cref="ArgumentException"><paramref name="tuning"/> holds a value a ride cannot run on.</exception>
        public RunScore(Tuning tuning)
        {
            Simulation.Validate(tuning);
            _tuning = tuning;
        }

        /// <summary>The run total: the sum of every scored trick's points.</summary>
        public long Total { get; private set; }

        /// <summary>Every scored trick, in order.</summary>
        public IReadOnlyList<ScoredTrick> Tricks => _tricks;

        /// <summary>How many times a trick ID has scored as a jump so far in the run.</summary>
        /// <param name="id">The trick ID (<see cref="TrickScoring.Id"/>).</param>
        public int TimesLanded(string id)
        {
            int index = IndexOf(id);
            return index < 0 ? 0 : _landings[index];
        }

        /// <summary>Folds one event in: scores <see cref="SimEventKind.Land"/> and <see cref="SimEventKind.GrindEnd"/>, ignores the rest.</summary>
        /// <param name="simEvent">The event.</param>
        public void Add(SimEvent simEvent)
        {
            if (simEvent.Kind == SimEventKind.Land)
            {
                string name = TrickScoring.Name(simEvent.Air, _tuning);
                string id = TrickScoring.Id(name);
                int index = IndexOf(id);
                int repeat = index < 0 ? 0 : _landings[index];
                if (index < 0)
                {
                    _ids.Add(id);
                    _landings.Add(1);
                }
                else
                {
                    _landings[index] = repeat + 1;
                }

                int points = TrickScoring.Points(TrickScoring.JumpValue(simEvent.Grade, simEvent.Air, _tuning), repeat, _tuning);
                Record(new ScoredTrick(simEvent.Tick, ScoredTrickKind.Jump, name, id, repeat, points));
            }
            else if (simEvent.Kind == SimEventKind.GrindEnd)
            {
                string name = simEvent.Grind.Box ? "Box Slide" : "Rail Grind";
                int points = TrickScoring.Points(TrickScoring.GrindValue(simEvent.Grind, _tuning), 0, _tuning);
                Record(new ScoredTrick(simEvent.Tick, ScoredTrickKind.Grind, name, TrickScoring.Id(name), 0, points));
            }
        }

        /// <summary>Folds a step's events in, in order (<see cref="Simulation.Events"/>).</summary>
        /// <param name="events">The events.</param>
        /// <exception cref="ArgumentNullException"><paramref name="events"/> is null.</exception>
        public void AddAll(IReadOnlyList<SimEvent> events)
        {
            if (events == null)
            {
                throw new ArgumentNullException(nameof(events));
            }

            for (int i = 0; i < events.Count; i++)
            {
                Add(events[i]);
            }
        }

        private void Record(ScoredTrick trick)
        {
            _tricks.Add(trick);
            Total += trick.Points;
        }

        private int IndexOf(string id)
        {
            for (int i = 0; i < _ids.Count; i++)
            {
                if (string.Equals(_ids[i], id, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
