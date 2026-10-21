using System;
using System.IO;
using System.Text;
using FirstChair.Sim;

namespace FirstChair.Riding
{
    /// <summary>
    /// Everything the sim needs from the test slope, loaded the way the sim tests load it: the layout from
    /// <c>TestSlope.txt</c>, the heightmap from <c>TestSlope.raw</c>, the snow map from <c>TestSlopeSnow.raw</c>, and the
    /// trees and rails from the layout (<see cref="Forest.FromLayout"/>, <see cref="RailSet.FromLayout"/>).
    /// </summary>
    public sealed class TestSlopeData
    {
        /// <summary>The layout file name.</summary>
        public const string LayoutFile = "TestSlope.txt";

        /// <summary>The 16-bit heightmap file name.</summary>
        public const string HeightmapFile = "TestSlope.raw";

        /// <summary>The snow-type map file name.</summary>
        public const string SnowFile = "TestSlopeSnow.raw";

        private const string LfsPointerPrefix = "version https://git-lfs";

        private TestSlopeData(SlopeLayout layout, Heightmap heightmap, SnowMap snowMap, Forest forest, RailSet rails)
        {
            Layout = layout;
            Heightmap = heightmap;
            SnowMap = snowMap;
            Forest = forest;
            Rails = rails;
        }

        /// <summary>The parsed layout: spec, zones, start, kickers, trees, rails.</summary>
        public SlopeLayout Layout { get; }

        /// <summary>The terrain.</summary>
        public Heightmap Heightmap { get; }

        /// <summary>The snow types.</summary>
        public SnowMap SnowMap { get; }

        /// <summary>The trees, from the layout.</summary>
        public Forest Forest { get; }

        /// <summary>The rails and boxes, from the layout.</summary>
        public RailSet Rails { get; }

        /// <summary>Loads the three test-slope files from a folder.</summary>
        /// <param name="folder">The folder holding them (in the editor, <c>Assets/Terrain/TestSlope</c>).</param>
        /// <exception cref="InvalidDataException">A binary file is a Git LFS pointer, not the data.</exception>
        public static TestSlopeData Load(string folder)
        {
            string layoutText = File.ReadAllText(Path.Combine(folder, LayoutFile));
            byte[] heights = ReadBinary(folder, HeightmapFile);
            byte[] snow = ReadBinary(folder, SnowFile);
            return FromContents(layoutText, heights, snow);
        }

        /// <summary>Builds the slope from file contents already in memory.</summary>
        /// <param name="layoutText">The text of <c>TestSlope.txt</c>.</param>
        /// <param name="heightmapR16">The bytes of <c>TestSlope.raw</c>.</param>
        /// <param name="snowBytes">The bytes of <c>TestSlopeSnow.raw</c>.</param>
        public static TestSlopeData FromContents(string layoutText, byte[] heightmapR16, byte[] snowBytes)
        {
            SlopeLayout layout = SlopeLayout.Parse(layoutText);
            Heightmap heightmap = Heightmap.FromR16(heightmapR16, layout.Spec);
            SnowMap snowMap = SnowMap.FromBytes(snowBytes, heightmap);
            return new TestSlopeData(layout, heightmap, snowMap, Forest.FromLayout(layout), RailSet.FromLayout(layout));
        }

        /// <summary>The run setup for a skier starting at the layout's start point.</summary>
        /// <param name="seed">The run's random seed.</param>
        /// <param name="assistLevel">The assist level.</param>
        public RunSetup CreateSetup(ulong seed, byte assistLevel) =>
            new RunSetup(Sport.Skis, assistLevel, seed, Layout.StartX, Layout.StartZ);

        /// <summary>A simulation of this slope with its trees and rails.</summary>
        public Simulation CreateSimulation(RunSetup setup, Tuning tuning) =>
            new Simulation(setup, Heightmap, SnowMap, Forest, Rails, tuning);

        /// <summary>An empty recording whose header matches <see cref="CreateSimulation"/>.</summary>
        public Recording CreateRecording(RunSetup setup, Tuning tuning) =>
            new Recording(setup, Heightmap, SnowMap, Forest, Rails, tuning);

        private static byte[] ReadBinary(string folder, string name)
        {
            string path = Path.Combine(folder, name);
            byte[] data = File.ReadAllBytes(path);
            int probe = Math.Min(data.Length, LfsPointerPrefix.Length);
            if (Encoding.ASCII.GetString(data, 0, probe) == LfsPointerPrefix)
            {
                throw new InvalidDataException(path + " is a Git LFS pointer, not the data. Run `git lfs pull`.");
            }

            return data;
        }
    }
}
