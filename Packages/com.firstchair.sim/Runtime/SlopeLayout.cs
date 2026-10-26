using System;
using System.Collections.Generic;

namespace FirstChair.Sim
{
    /// <summary>
    /// A named rectangle of a slope and the snow painted on it: a groomer, a park, a bowl, a glade, a patch of ice.
    /// </summary>
    /// <remarks>Edges are included. A <see cref="SlopeLayout"/> lists its zones in paint order: where two overlap,
    /// the later zone's snow is the one on the snow map. Immutable.</remarks>
    public readonly struct SlopeZone
    {
        /// <summary>The zone's name, unique within its layout.</summary>
        public readonly string Name;

        /// <summary>The snow painted on the zone.</summary>
        public readonly SnowType Snow;

        /// <summary>Smallest X, millimetres.</summary>
        public readonly int XMinMm;

        /// <summary>Smallest Z, millimetres.</summary>
        public readonly int ZMinMm;

        /// <summary>Largest X, millimetres.</summary>
        public readonly int XMaxMm;

        /// <summary>Largest Z, millimetres.</summary>
        public readonly int ZMaxMm;

        internal SlopeZone(string name, SnowType snow, int xMinMm, int zMinMm, int xMaxMm, int zMaxMm)
        {
            Name = name;
            Snow = snow;
            XMinMm = xMinMm;
            ZMinMm = zMinMm;
            XMaxMm = xMaxMm;
            ZMaxMm = zMaxMm;
        }

        /// <summary>Smallest X, metres.</summary>
        public Fix64 XMin => SlopeLayout.Metres(XMinMm);

        /// <summary>Smallest Z, metres.</summary>
        public Fix64 ZMin => SlopeLayout.Metres(ZMinMm);

        /// <summary>Largest X, metres.</summary>
        public Fix64 XMax => SlopeLayout.Metres(XMaxMm);

        /// <summary>Largest Z, metres.</summary>
        public Fix64 ZMax => SlopeLayout.Metres(ZMaxMm);

        /// <summary>True when (x, z) lies in the zone, edges included.</summary>
        /// <param name="x">X, metres.</param>
        /// <param name="z">Z, metres.</param>
        public bool Contains(Fix64 x, Fix64 z) => x >= XMin && x <= XMax && z >= ZMin && z <= ZMax;
    }

    /// <summary>
    /// A kicker shaped into the heightmap: a ramp that rises <see cref="HeightMm"/> above the slope over
    /// <see cref="LengthMm"/>, ending at its lip.
    /// </summary>
    /// <remarks>Immutable. The ramp itself is in the heightmap; this record says where it is and which way it
    /// launches.</remarks>
    public readonly struct SlopeKicker
    {
        /// <summary>X of the centre of the lip, millimetres.</summary>
        public readonly int XMm;

        /// <summary>Z of the centre of the lip, millimetres.</summary>
        public readonly int ZMm;

        /// <summary>Direction of travel off the lip, millidegrees clockwise seen from above: 0 is +Z, 90000 is +X.</summary>
        public readonly int FacingMillidegrees;

        /// <summary>Distance from the foot of the ramp to the lip, millimetres.</summary>
        public readonly int LengthMm;

        /// <summary>Width of the ramp, millimetres.</summary>
        public readonly int WidthMm;

        /// <summary>Height of the lip above the surrounding slope, millimetres.</summary>
        public readonly int HeightMm;

        internal SlopeKicker(int xMm, int zMm, int facingMillidegrees, int lengthMm, int widthMm, int heightMm)
        {
            XMm = xMm;
            ZMm = zMm;
            FacingMillidegrees = facingMillidegrees;
            LengthMm = lengthMm;
            WidthMm = widthMm;
            HeightMm = heightMm;
        }

        /// <summary>X of the centre of the lip, metres.</summary>
        public Fix64 X => SlopeLayout.Metres(XMm);

        /// <summary>Z of the centre of the lip, metres.</summary>
        public Fix64 Z => SlopeLayout.Metres(ZMm);

        /// <summary>
        /// Direction of travel off the lip, radians, in the sim's heading convention: 0 faces +Z and π/2 faces +X,
        /// so the direction is (sin, cos) in (X, Z).
        /// </summary>
        public Fix64 Facing => Fix64.Pi * Fix64.FromInt(FacingMillidegrees) / Fix64.FromInt(SlopeLayout.HalfTurnMillidegrees);

        /// <summary>Distance from the foot of the ramp to the lip, metres.</summary>
        public Fix64 Length => SlopeLayout.Metres(LengthMm);

        /// <summary>Width of the ramp, metres.</summary>
        public Fix64 Width => SlopeLayout.Metres(WidthMm);

        /// <summary>Height of the lip above the surrounding slope, metres.</summary>
        public Fix64 Height => SlopeLayout.Metres(HeightMm);
    }

    /// <summary>A tree standing on the slope: a trunk the rider hits and a wider ring of branches.</summary>
    /// <remarks>Immutable. Trees are not in the heightmap.</remarks>
    public readonly struct SlopeTree
    {
        /// <summary>X of the trunk's centre, millimetres.</summary>
        public readonly int XMm;

        /// <summary>Z of the trunk's centre, millimetres.</summary>
        public readonly int ZMm;

        /// <summary>Trunk radius, millimetres. Positive.</summary>
        public readonly int TrunkRadiusMm;

        /// <summary>Branch radius, millimetres. Never below <see cref="TrunkRadiusMm"/>.</summary>
        public readonly int BranchRadiusMm;

        /// <summary>Creates a tree. <see cref="Forest"/> checks the radii when it takes the tree.</summary>
        /// <param name="xMm">X of the trunk's centre, millimetres.</param>
        /// <param name="zMm">Z of the trunk's centre, millimetres.</param>
        /// <param name="trunkRadiusMm">Trunk radius, millimetres; positive.</param>
        /// <param name="branchRadiusMm">Branch radius, millimetres; not below the trunk radius.</param>
        public SlopeTree(int xMm, int zMm, int trunkRadiusMm, int branchRadiusMm)
        {
            XMm = xMm;
            ZMm = zMm;
            TrunkRadiusMm = trunkRadiusMm;
            BranchRadiusMm = branchRadiusMm;
        }

        /// <summary>X of the trunk's centre, metres.</summary>
        public Fix64 X => SlopeLayout.Metres(XMm);

        /// <summary>Z of the trunk's centre, metres.</summary>
        public Fix64 Z => SlopeLayout.Metres(ZMm);

        /// <summary>Trunk radius, metres.</summary>
        public Fix64 TrunkRadius => SlopeLayout.Metres(TrunkRadiusMm);

        /// <summary>Branch radius, metres.</summary>
        public Fix64 BranchRadius => SlopeLayout.Metres(BranchRadiusMm);
    }

    /// <summary>
    /// A rail or a box on the slope (Story 006): a straight run from (<see cref="X0Mm"/>, <see cref="Z0Mm"/>) to
    /// (<see cref="X1Mm"/>, <see cref="Z1Mm"/>) seen from above, whose top follows the snow <see cref="HeightMm"/> above it.
    /// </summary>
    /// <remarks>Immutable. Rails are not in the heightmap; <see cref="RailSet"/> checks the sizes when it takes a rail.</remarks>
    public readonly struct SlopeRail
    {
        /// <summary>X of the start, millimetres.</summary>
        public readonly int X0Mm;

        /// <summary>Z of the start, millimetres.</summary>
        public readonly int Z0Mm;

        /// <summary>X of the end, millimetres.</summary>
        public readonly int X1Mm;

        /// <summary>Z of the end, millimetres.</summary>
        public readonly int Z1Mm;

        /// <summary>Height of the top above the snow, millimetres. Positive.</summary>
        public readonly int HeightMm;

        /// <summary>Width of the top, millimetres. Positive.</summary>
        public readonly int WidthMm;

        /// <summary>True for a box, false for a rail. Scored and named apart; they ride the same.</summary>
        public readonly bool Box;

        /// <summary>Creates a rail or box.</summary>
        /// <param name="x0Mm">X of the start, millimetres.</param>
        /// <param name="z0Mm">Z of the start, millimetres.</param>
        /// <param name="x1Mm">X of the end, millimetres.</param>
        /// <param name="z1Mm">Z of the end, millimetres.</param>
        /// <param name="heightMm">Height of the top above the snow, millimetres.</param>
        /// <param name="widthMm">Width of the top, millimetres.</param>
        /// <param name="box">True for a box.</param>
        public SlopeRail(int x0Mm, int z0Mm, int x1Mm, int z1Mm, int heightMm, int widthMm, bool box)
        {
            X0Mm = x0Mm;
            Z0Mm = z0Mm;
            X1Mm = x1Mm;
            Z1Mm = z1Mm;
            HeightMm = heightMm;
            WidthMm = widthMm;
            Box = box;
        }

        /// <summary>X of the start, metres.</summary>
        public Fix64 X0 => SlopeLayout.Metres(X0Mm);

        /// <summary>Z of the start, metres.</summary>
        public Fix64 Z0 => SlopeLayout.Metres(Z0Mm);

        /// <summary>X of the end, metres.</summary>
        public Fix64 X1 => SlopeLayout.Metres(X1Mm);

        /// <summary>Z of the end, metres.</summary>
        public Fix64 Z1 => SlopeLayout.Metres(Z1Mm);

        /// <summary>Height of the top above the snow, metres.</summary>
        public Fix64 Height => SlopeLayout.Metres(HeightMm);

        /// <summary>Width of the top, metres.</summary>
        public Fix64 Width => SlopeLayout.Metres(WidthMm);
    }

    /// <summary>
    /// What a slope holds besides its heightmap and snow map: the <see cref="HeightmapSpec"/>, the snow zones, the
    /// start point, the kickers and the trees. Read from the layout text file a slope generator writes next to its
    /// heightmap (<c>tools/terrain/make_test_slope.py</c> for the test slope).
    /// </summary>
    /// <remarks>
    /// <para><b>Text format.</b> One record per line, fields separated by spaces or tabs, every number a whole
    /// (possibly negative) integer. Blank lines and lines whose first non-blank character is <c>#</c> are skipped;
    /// LF and CRLF line endings both work. Lengths and positions are millimetres, in heightmap coordinates (X along
    /// columns, Z along rows). Facing is millidegrees clockwise seen from above: 0 is +Z, 90000 is +X.</para>
    /// <code>
    /// format 1                                          first record, exactly once
    /// spec &lt;sizeX&gt; &lt;sizeZ&gt; &lt;minHeight&gt; &lt;maxHeight&gt;     exactly once, before any zone, start, kicker or tree
    /// background &lt;snow&gt;                                 exactly once: the snow outside every zone
    /// zone &lt;name&gt; &lt;snow&gt; &lt;xMin&gt; &lt;zMin&gt; &lt;xMax&gt; &lt;zMax&gt;   any number, in paint order; names unique
    /// start &lt;x&gt; &lt;z&gt;                                     exactly once
    /// kicker &lt;lipX&gt; &lt;lipZ&gt; &lt;facing&gt; &lt;length&gt; &lt;width&gt; &lt;height&gt;   any number
    /// tree &lt;x&gt; &lt;z&gt; &lt;trunkRadius&gt; &lt;branchRadius&gt;            any number
    /// rail &lt;x0&gt; &lt;z0&gt; &lt;x1&gt; &lt;z1&gt; &lt;height&gt; &lt;width&gt;      any number (Story 006)
    /// box &lt;x0&gt; &lt;z0&gt; &lt;x1&gt; &lt;z1&gt; &lt;height&gt; &lt;width&gt;       any number (Story 006)
    /// run &lt;name&gt; &lt;difficulty&gt; &lt;width&gt; &lt;x0&gt; &lt;z0&gt; &lt;x1&gt; &lt;z1&gt; ...   any number; names unique (Story 010)
    /// lift &lt;name&gt; &lt;bottomX&gt; &lt;bottomZ&gt; &lt;topX&gt; &lt;topZ&gt;      any number; names unique (Story 010)
    /// tower &lt;lift&gt; &lt;x&gt; &lt;z&gt; &lt;height&gt;                    any number, after its lift (Story 010)
    /// </code>
    /// <para>A snow name is one of <c>groomed powder crud ice slush</c>; a difficulty one of <c>green blue red black
    /// park</c>. Every position must lie on the map. Sizes, lengths, widths, heights and radii must be positive; a zone's
    /// minimum must be below its maximum; a facing runs from 0 to 359999; a branch radius may not be below its trunk
    /// radius; a rail's or box's two ends must differ, and so must a lift's; a run has at least two points, no two in a
    /// row the same. Runs, lifts and towers describe the mountain for the view and for tests; the sim never rides on
    /// them.</para>
    /// <para>The sim never reads files: the caller passes the text. Anything malformed throws
    /// <see cref="ArgumentException"/> naming the line. Immutable after parsing, so one instance may be shared by any
    /// number of simulations and threads.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// SlopeLayout layout = SlopeLayout.Parse(layoutText);          // text loaded by the game layer
    /// Heightmap heightmap = Heightmap.FromR16(r16Bytes, layout.Spec);
    /// SnowMap snowMap = SnowMap.FromBytes(snowBytes, heightmap);
    /// var setup = new RunSetup(Sport.Skis, 0, seed, layout.StartX, layout.StartZ);
    /// </code>
    /// </example>
    public sealed class SlopeLayout
    {
        /// <summary>The layout format this parser reads, as named by the <c>format</c> record.</summary>
        public const int FormatVersion = 1;

        internal const int HalfTurnMillidegrees = 180000;

        private const int FullTurnMillidegrees = 360000;
        private const int MillimetresPerMetre = 1000;
        private const string TextParameter = "text";

        private static readonly char[] FieldSeparators = { ' ', '\t' };

        // Indexed by SnowType value.
        private static readonly string[] SnowNames = { "groomed", "powder", "crud", "ice", "slush" };

        // Indexed by RunDifficulty value.
        private static readonly string[] DifficultyNames = { "green", "blue", "red", "black", "park" };

        private readonly SlopeZone[] _zones;
        private readonly SlopeKicker[] _kickers;
        private readonly SlopeTree[] _trees;
        private readonly SlopeRail[] _rails;

        private SlopeLayout(
            HeightmapSpec spec, SnowType background, int startXMm, int startZMm, SlopeZone[] zones, SlopeKicker[] kickers, SlopeTree[] trees,
            SlopeRail[] rails, SlopeRun[] runs, SlopeLift[] lifts)
        {
            _rails = rails;
            Runs = Array.AsReadOnly(runs);
            Lifts = Array.AsReadOnly(lifts);
            Spec = spec;
            Background = background;
            StartXMm = startXMm;
            StartZMm = startZMm;
            _zones = zones;
            _kickers = kickers;
            _trees = trees;
        }

        /// <summary>World size and height range of the slope's heightmap.</summary>
        public HeightmapSpec Spec { get; }

        /// <summary>The snow outside every zone.</summary>
        public SnowType Background { get; }

        /// <summary>X where a run begins, millimetres.</summary>
        public int StartXMm { get; }

        /// <summary>Z where a run begins, millimetres.</summary>
        public int StartZMm { get; }

        /// <summary>X where a run begins, metres: pass it to <see cref="RunSetup"/>.</summary>
        public Fix64 StartX => Metres(StartXMm);

        /// <summary>Z where a run begins, metres: pass it to <see cref="RunSetup"/>.</summary>
        public Fix64 StartZ => Metres(StartZMm);

        /// <summary>The snow zones, in paint order.</summary>
        public IReadOnlyList<SlopeZone> Zones => Array.AsReadOnly(_zones);

        /// <summary>The kickers, in file order.</summary>
        public IReadOnlyList<SlopeKicker> Kickers => Array.AsReadOnly(_kickers);

        /// <summary>The trees, in file order.</summary>
        public IReadOnlyList<SlopeTree> Trees => Array.AsReadOnly(_trees);

        /// <summary>The rails and boxes, in file order.</summary>
        public IReadOnlyList<SlopeRail> Rails => Array.AsReadOnly(_rails);

        /// <summary>The marked runs, in file order (Story 010).</summary>
        public IReadOnlyList<SlopeRun> Runs { get; }

        /// <summary>The lifts, in file order, each with its towers (Story 010).</summary>
        public IReadOnlyList<SlopeLift> Lifts { get; }

        /// <summary>Finds a zone by name (ordinal, case-sensitive).</summary>
        /// <param name="name">The zone's name.</param>
        /// <param name="zone">The zone, or default when there is none.</param>
        /// <returns>True when the layout has a zone of that name.</returns>
        public bool TryGetZone(string name, out SlopeZone zone)
        {
            for (int i = 0; i < _zones.Length; i++)
            {
                if (string.Equals(_zones[i].Name, name, StringComparison.Ordinal))
                {
                    zone = _zones[i];
                    return true;
                }
            }

            zone = default;
            return false;
        }

        /// <summary>
        /// Parses a layout (format in the class remarks). Throws <see cref="ArgumentNullException"/> for null text and
        /// <see cref="ArgumentException"/>, naming the line, for anything malformed or out of range.
        /// </summary>
        /// <param name="text">The layout file's contents.</param>
        public static SlopeLayout Parse(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(TextParameter);
            }

            bool formatSeen = false;
            bool specSeen = false;
            bool backgroundSeen = false;
            bool startSeen = false;
            HeightmapSpec spec = default;
            SnowType background = SnowType.Groomed;
            int startX = 0;
            int startZ = 0;
            var zones = new List<SlopeZone>();
            var kickers = new List<SlopeKicker>();
            var trees = new List<SlopeTree>();
            var rails = new List<SlopeRail>();
            var runs = new List<SlopeRun>();
            var lifts = new List<SlopeLift>();

            string[] lines = text.Split('\n');
            for (int index = 0; index < lines.Length; index++)
            {
                int line = index + 1;
                string record = lines[index].Trim();
                if (record.Length == 0 || record[0] == '#')
                {
                    continue;
                }

                string[] fields = record.Split(FieldSeparators, StringSplitOptions.RemoveEmptyEntries);
                string keyword = fields[0];

                if (!formatSeen)
                {
                    if (!Is(keyword, "format") || fields.Length != 2 || ParseInt(fields[1], line) != FormatVersion)
                    {
                        throw Error(line, "the first record must be 'format " + FormatVersion + "'");
                    }

                    formatSeen = true;
                }
                else if (Is(keyword, "format"))
                {
                    throw Error(line, "'format' may appear only once");
                }
                else if (Is(keyword, "spec"))
                {
                    ExpectFields(fields, 5, line);
                    RequireOnce(specSeen, keyword, line);
                    spec = new HeightmapSpec(ParseInt(fields[1], line), ParseInt(fields[2], line), ParseInt(fields[3], line), ParseInt(fields[4], line));
                    if (spec.SizeXMm <= 0 || spec.SizeZMm <= 0 || spec.MaxHeightMm < spec.MinHeightMm)
                    {
                        throw Error(line, "sizes must be positive and the maximum height must not be below the minimum");
                    }

                    specSeen = true;
                }
                else if (Is(keyword, "background"))
                {
                    ExpectFields(fields, 2, line);
                    RequireOnce(backgroundSeen, keyword, line);
                    background = ParseSnow(fields[1], line);
                    backgroundSeen = true;
                }
                else if (Is(keyword, "zone"))
                {
                    ExpectFields(fields, 7, line);
                    RequireSpec(specSeen, keyword, line);
                    var zone = new SlopeZone(
                        fields[1], ParseSnow(fields[2], line), ParseInt(fields[3], line), ParseInt(fields[4], line), ParseInt(fields[5], line), ParseInt(fields[6], line));
                    RequireOnMap(spec, zone.XMinMm, zone.ZMinMm, line);
                    RequireOnMap(spec, zone.XMaxMm, zone.ZMaxMm, line);
                    if (zone.XMinMm >= zone.XMaxMm || zone.ZMinMm >= zone.ZMaxMm)
                    {
                        throw Error(line, "a zone's minimum must be below its maximum");
                    }

                    for (int i = 0; i < zones.Count; i++)
                    {
                        if (string.Equals(zones[i].Name, zone.Name, StringComparison.Ordinal))
                        {
                            throw Error(line, "there is already a zone named '" + zone.Name + "'");
                        }
                    }

                    zones.Add(zone);
                }
                else if (Is(keyword, "start"))
                {
                    ExpectFields(fields, 3, line);
                    RequireSpec(specSeen, keyword, line);
                    RequireOnce(startSeen, keyword, line);
                    startX = ParseInt(fields[1], line);
                    startZ = ParseInt(fields[2], line);
                    RequireOnMap(spec, startX, startZ, line);
                    startSeen = true;
                }
                else if (Is(keyword, "kicker"))
                {
                    ExpectFields(fields, 7, line);
                    RequireSpec(specSeen, keyword, line);
                    var kicker = new SlopeKicker(
                        ParseInt(fields[1], line), ParseInt(fields[2], line), ParseInt(fields[3], line), ParseInt(fields[4], line), ParseInt(fields[5], line), ParseInt(fields[6], line));
                    RequireOnMap(spec, kicker.XMm, kicker.ZMm, line);
                    if (kicker.FacingMillidegrees < 0 || kicker.FacingMillidegrees >= FullTurnMillidegrees)
                    {
                        throw Error(line, "a facing runs from 0 to " + (FullTurnMillidegrees - 1) + " millidegrees");
                    }

                    if (kicker.LengthMm <= 0 || kicker.WidthMm <= 0 || kicker.HeightMm <= 0)
                    {
                        throw Error(line, "a kicker's length, width and height must be positive");
                    }

                    kickers.Add(kicker);
                }
                else if (Is(keyword, "tree"))
                {
                    ExpectFields(fields, 5, line);
                    RequireSpec(specSeen, keyword, line);
                    var tree = new SlopeTree(ParseInt(fields[1], line), ParseInt(fields[2], line), ParseInt(fields[3], line), ParseInt(fields[4], line));
                    RequireOnMap(spec, tree.XMm, tree.ZMm, line);
                    if (tree.TrunkRadiusMm <= 0 || tree.BranchRadiusMm < tree.TrunkRadiusMm)
                    {
                        throw Error(line, "a trunk radius must be positive and the branch radius must not be below it");
                    }

                    trees.Add(tree);
                }
                else if (Is(keyword, "rail") || Is(keyword, "box"))
                {
                    ExpectFields(fields, 7, line);
                    RequireSpec(specSeen, keyword, line);
                    var rail = new SlopeRail(
                        ParseInt(fields[1], line), ParseInt(fields[2], line), ParseInt(fields[3], line), ParseInt(fields[4], line), ParseInt(fields[5], line),
                        ParseInt(fields[6], line), Is(keyword, "box"));
                    RequireOnMap(spec, rail.X0Mm, rail.Z0Mm, line);
                    RequireOnMap(spec, rail.X1Mm, rail.Z1Mm, line);
                    if (rail.HeightMm <= 0 || rail.WidthMm <= 0)
                    {
                        throw Error(line, "a rail's or box's height and width must be positive");
                    }

                    if (rail.X0Mm == rail.X1Mm && rail.Z0Mm == rail.Z1Mm)
                    {
                        throw Error(line, "a rail's or box's two ends must differ");
                    }

                    rails.Add(rail);
                }
                else if (Is(keyword, "run"))
                {
                    RequireSpec(specSeen, keyword, line);
                    if (fields.Length < 8 || (fields.Length - 4) % 2 != 0)
                    {
                        throw Error(line, "a run needs a name, a difficulty, a width and at least two x z points");
                    }

                    RunDifficulty difficulty = ParseDifficulty(fields[2], line);
                    int width = ParseInt(fields[3], line);
                    if (width <= 0)
                    {
                        throw Error(line, "a run's width must be positive");
                    }

                    var points = new SlopePoint[(fields.Length - 4) / 2];
                    for (int i = 0; i < points.Length; i++)
                    {
                        points[i] = new SlopePoint(ParseInt(fields[4 + 2 * i], line), ParseInt(fields[5 + 2 * i], line));
                        RequireOnMap(spec, points[i].XMm, points[i].ZMm, line);
                        if (i > 0 && points[i].XMm == points[i - 1].XMm && points[i].ZMm == points[i - 1].ZMm)
                        {
                            throw Error(line, "a run's points must differ from the one before");
                        }
                    }

                    foreach (SlopeRun other in runs)
                    {
                        if (string.Equals(other.Name, fields[1], StringComparison.Ordinal))
                        {
                            throw Error(line, "there is already a run named '" + fields[1] + "'");
                        }
                    }

                    runs.Add(new SlopeRun(fields[1], difficulty, width, points));
                }
                else if (Is(keyword, "lift"))
                {
                    ExpectFields(fields, 6, line);
                    RequireSpec(specSeen, keyword, line);
                    var lift = new SlopeLift(fields[1], ParseInt(fields[2], line), ParseInt(fields[3], line), ParseInt(fields[4], line), ParseInt(fields[5], line));
                    RequireOnMap(spec, lift.BottomXMm, lift.BottomZMm, line);
                    RequireOnMap(spec, lift.TopXMm, lift.TopZMm, line);
                    if (lift.BottomXMm == lift.TopXMm && lift.BottomZMm == lift.TopZMm)
                    {
                        throw Error(line, "a lift's two stations must differ");
                    }

                    if (FindLift(lifts, lift.Name) != null)
                    {
                        throw Error(line, "there is already a lift named '" + lift.Name + "'");
                    }

                    lifts.Add(lift);
                }
                else if (Is(keyword, "tower"))
                {
                    ExpectFields(fields, 5, line);
                    RequireSpec(specSeen, keyword, line);
                    SlopeLift lift = FindLift(lifts, fields[1]) ?? throw Error(line, "no lift named '" + fields[1] + "' comes before this tower");
                    var tower = new SlopeLiftTower(ParseInt(fields[2], line), ParseInt(fields[3], line), ParseInt(fields[4], line));
                    RequireOnMap(spec, tower.XMm, tower.ZMm, line);
                    if (tower.HeightMm <= 0)
                    {
                        throw Error(line, "a tower's height must be positive");
                    }

                    lift.AddTower(tower);
                }
                else
                {
                    throw Error(line, "unknown record '" + keyword + "'");
                }
            }

            if (!formatSeen)
            {
                throw new ArgumentException("A slope layout must start with 'format " + FormatVersion + "'.", TextParameter);
            }

            if (!specSeen || !backgroundSeen || !startSeen)
            {
                throw new ArgumentException("A slope layout needs one 'spec', one 'background' and one 'start' record.", TextParameter);
            }

            return new SlopeLayout(
                spec, background, startX, startZ, zones.ToArray(), kickers.ToArray(), trees.ToArray(), rails.ToArray(), runs.ToArray(), lifts.ToArray());
        }

        internal static Fix64 Metres(int millimetres) => Fix64.FromRatio(millimetres, MillimetresPerMetre);

        private static bool Is(string keyword, string expected) => string.Equals(keyword, expected, StringComparison.Ordinal);

        private static void ExpectFields(string[] fields, int count, int line)
        {
            if (fields.Length != count)
            {
                throw Error(line, "'" + fields[0] + "' takes " + (count - 1) + " fields; got " + (fields.Length - 1));
            }
        }

        private static void RequireOnce(bool seen, string keyword, int line)
        {
            if (seen)
            {
                throw Error(line, "'" + keyword + "' may appear only once");
            }
        }

        private static void RequireSpec(bool specSeen, string keyword, int line)
        {
            if (!specSeen)
            {
                throw Error(line, "'" + keyword + "' must come after 'spec'");
            }
        }

        private static void RequireOnMap(HeightmapSpec spec, int xMm, int zMm, int line)
        {
            if (xMm < 0 || xMm > spec.SizeXMm || zMm < 0 || zMm > spec.SizeZMm)
            {
                throw Error(line, "(" + xMm + ", " + zMm + ") is off the " + spec.SizeXMm + " x " + spec.SizeZMm + " mm map");
            }
        }

        private static SnowType ParseSnow(string name, int line)
        {
            for (int i = 0; i < SnowNames.Length; i++)
            {
                if (Is(name, SnowNames[i]))
                {
                    return (SnowType)i;
                }
            }

            throw Error(line, "unknown snow '" + name + "'; use groomed, powder, crud, ice or slush");
        }

        private static RunDifficulty ParseDifficulty(string name, int line)
        {
            for (int i = 0; i < DifficultyNames.Length; i++)
            {
                if (Is(name, DifficultyNames[i]))
                {
                    return (RunDifficulty)i;
                }
            }

            throw Error(line, "unknown difficulty '" + name + "'; use green, blue, red, black or park");
        }

        private static SlopeLift FindLift(List<SlopeLift> lifts, string name)
        {
            foreach (SlopeLift lift in lifts)
            {
                if (string.Equals(lift.Name, name, StringComparison.Ordinal))
                {
                    return lift;
                }
            }

            return null;
        }

        // Culture-free: digits with an optional leading minus, within int range.
        private static int ParseInt(string token, int line)
        {
            int start = token.Length > 0 && token[0] == '-' ? 1 : 0;
            if (start == token.Length)
            {
                throw Error(line, "'" + token + "' is not a whole number");
            }

            long value = 0;
            for (int i = start; i < token.Length; i++)
            {
                char digit = token[i];
                if (digit < '0' || digit > '9')
                {
                    throw Error(line, "'" + token + "' is not a whole number");
                }

                value = value * 10 + (digit - '0');
                if (value > 1L + int.MaxValue)
                {
                    throw Error(line, "'" + token + "' is out of range");
                }
            }

            value = start == 1 ? -value : value;
            if (value > int.MaxValue)
            {
                throw Error(line, "'" + token + "' is out of range");
            }

            return (int)value;
        }

        private static ArgumentException Error(int line, string message) =>
            new ArgumentException("Slope layout line " + line + ": " + message + ".", TextParameter);
    }
}
