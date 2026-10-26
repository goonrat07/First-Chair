using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace FirstChair.Sim
{
    /// <summary>A run's difficulty, as its markers and sign show it (Story 010; GMA2's colours, with the park apart).</summary>
    public enum RunDifficulty
    {
        /// <summary>The gentlest runs.</summary>
        Green = 0,

        /// <summary>Intermediate runs.</summary>
        Blue = 1,

        /// <summary>Steep runs.</summary>
        Red = 2,

        /// <summary>The steepest runs.</summary>
        Black = 3,

        /// <summary>A terrain park.</summary>
        Park = 4,
    }

    /// <summary>A point on a slope's map, millimetres in heightmap coordinates. Immutable.</summary>
    public readonly struct SlopePoint
    {
        /// <summary>X, millimetres.</summary>
        public readonly int XMm;

        /// <summary>Z, millimetres.</summary>
        public readonly int ZMm;

        /// <summary>Creates a point.</summary>
        /// <param name="xMm">X, millimetres.</param>
        /// <param name="zMm">Z, millimetres.</param>
        public SlopePoint(int xMm, int zMm)
        {
            XMm = xMm;
            ZMm = zMm;
        }

        /// <summary>X, metres.</summary>
        public Fix64 X => SlopeLayout.Metres(XMm);

        /// <summary>Z, metres.</summary>
        public Fix64 Z => SlopeLayout.Metres(ZMm);
    }

    /// <summary>
    /// A marked run (Story 010): its name, difficulty, width and centre line from top to bottom. The sim never rides on
    /// it: the snow and the heightmap already hold the run. The view draws its markers from it, and tests ride its line.
    /// </summary>
    /// <example>
    /// <code>
    /// foreach (SlopeRun run in layout.Runs)
    /// {
    ///     SlopePoint top = run.Points[0];  // where its sign stands
    /// }
    /// </code>
    /// </example>
    public sealed class SlopeRun
    {
        internal SlopeRun(string name, RunDifficulty difficulty, int widthMm, SlopePoint[] points)
        {
            Name = name;
            Difficulty = difficulty;
            WidthMm = widthMm;
            Points = new ReadOnlyCollection<SlopePoint>(points);
        }

        /// <summary>The run's name, unique within its layout.</summary>
        public string Name { get; }

        /// <summary>The run's difficulty.</summary>
        public RunDifficulty Difficulty { get; }

        /// <summary>The run's width, millimetres. Positive.</summary>
        public int WidthMm { get; }

        /// <summary>The run's width, metres.</summary>
        public Fix64 Width => SlopeLayout.Metres(WidthMm);

        /// <summary>The centre line from the top of the run to its foot: at least two points, no two in a row the same.</summary>
        public IReadOnlyList<SlopePoint> Points { get; }
    }

    /// <summary>A lift tower: where it stands and how high its top is above the snow, millimetres. Immutable.</summary>
    public readonly struct SlopeLiftTower
    {
        /// <summary>X of the tower's foot, millimetres.</summary>
        public readonly int XMm;

        /// <summary>Z of the tower's foot, millimetres.</summary>
        public readonly int ZMm;

        /// <summary>Height of the tower's top above the snow, millimetres. Positive.</summary>
        public readonly int HeightMm;

        /// <summary>Creates a tower.</summary>
        /// <param name="xMm">X of the foot, millimetres.</param>
        /// <param name="zMm">Z of the foot, millimetres.</param>
        /// <param name="heightMm">Height of the top above the snow, millimetres.</param>
        public SlopeLiftTower(int xMm, int zMm, int heightMm)
        {
            XMm = xMm;
            ZMm = zMm;
            HeightMm = heightMm;
        }

        /// <summary>X of the foot, metres.</summary>
        public Fix64 X => SlopeLayout.Metres(XMm);

        /// <summary>Z of the foot, metres.</summary>
        public Fix64 Z => SlopeLayout.Metres(ZMm);

        /// <summary>Height of the top above the snow, metres.</summary>
        public Fix64 Height => SlopeLayout.Metres(HeightMm);
    }

    /// <summary>
    /// A chairlift (Story 010): its bottom and top stations and its towers, bottom to top in file order. The sim never
    /// rides on it; the view draws it and carries the rider up it between runs.
    /// </summary>
    /// <example>
    /// <code>
    /// SlopeLift lift = layout.Lifts[0];
    /// var bottom = new SlopePoint(lift.BottomXMm, lift.BottomZMm);
    /// foreach (SlopeLiftTower tower in lift.Towers) { /* stand a tower there, tower.Height tall */ }
    /// </code>
    /// </example>
    public sealed class SlopeLift
    {
        private readonly List<SlopeLiftTower> _towers = new List<SlopeLiftTower>();

        internal SlopeLift(string name, int bottomXMm, int bottomZMm, int topXMm, int topZMm)
        {
            Name = name;
            BottomXMm = bottomXMm;
            BottomZMm = bottomZMm;
            TopXMm = topXMm;
            TopZMm = topZMm;
            Towers = _towers.AsReadOnly();
        }

        /// <summary>The lift's name, unique within its layout.</summary>
        public string Name { get; }

        /// <summary>X of the bottom station, millimetres.</summary>
        public int BottomXMm { get; }

        /// <summary>Z of the bottom station, millimetres.</summary>
        public int BottomZMm { get; }

        /// <summary>X of the top station, millimetres.</summary>
        public int TopXMm { get; }

        /// <summary>Z of the top station, millimetres.</summary>
        public int TopZMm { get; }

        /// <summary>The towers, in file order (the generator writes them bottom to top).</summary>
        public IReadOnlyList<SlopeLiftTower> Towers { get; }

        internal void AddTower(SlopeLiftTower tower) => _towers.Add(tower);
    }
}
