using System.IO;
using System.Text;
using FirstChair.Sim;

namespace FirstChair.Tools.RidePreview
{
    /// <summary>The committed test slope, loaded through the sim's own loaders exactly as the sim tests load it.</summary>
    internal sealed class Slope
    {
        private const string Directory = "Assets/Terrain/TestSlope";
        private const string HeightmapFile = "TestSlope.raw";
        private const string SnowFile = "TestSlopeSnow.raw";
        private const string LayoutFile = "TestSlope.txt";
        private const string LfsPointerPrefix = "version https://git-lfs";

        private Slope(SlopeLayout layout, Heightmap heightmap, SnowMap snow)
        {
            Layout = layout;
            Heightmap = heightmap;
            Snow = snow;
            Forest = Forest.FromLayout(layout);
            Rails = RailSet.FromLayout(layout);
            Tuning = Tuning.Default;
        }

        public SlopeLayout Layout { get; }

        public Heightmap Heightmap { get; }

        public SnowMap Snow { get; }

        public Forest Forest { get; }

        public RailSet Rails { get; }

        public Tuning Tuning { get; }

        public double SizeX => Num.D(Heightmap.SizeX);

        public double SizeZ => Num.D(Heightmap.SizeZ);

        public static Slope Load(string root)
        {
            SlopeLayout layout = SlopeLayout.Parse(Encoding.UTF8.GetString(ReadFile(root, LayoutFile)));
            Heightmap heightmap = Heightmap.FromR16(ReadFile(root, HeightmapFile), layout.Spec);
            return new Slope(layout, heightmap, SnowMap.FromBytes(ReadFile(root, SnowFile), heightmap));
        }

        /// <summary>A zone the rides need; a clear error when the layout lacks it.</summary>
        public SlopeZone Zone(string name)
        {
            if (!Layout.TryGetZone(name, out SlopeZone zone))
            {
                throw new ToolException("the test slope layout has no zone '" + name + "' (" + Directory + "/" + LayoutFile + ").");
            }

            return zone;
        }

        /// <summary>Terrain height (m) and surface normal's Y at a point, clamped onto the map.</summary>
        public double HeightAt(double x, double z, out double normalY)
        {
            x = System.Math.Clamp(x, 0, SizeX);
            z = System.Math.Clamp(z, 0, SizeZ);
            Heightmap.Sample(Num.F(x), Num.F(z), out Fix64 height, out Vec3 normal);
            normalY = Num.D(normal.Y);
            return Num.D(height);
        }

        public int SnowAt(double x, double z) =>
            (int)Snow.TypeAt(Num.F(System.Math.Clamp(x, 0, SizeX)), Num.F(System.Math.Clamp(z, 0, SizeZ)));

        private static byte[] ReadFile(string root, string name)
        {
            string path = Path.Combine(root, Directory, name);
            if (!File.Exists(path))
            {
                throw new ToolException("missing " + path + ".");
            }

            byte[] bytes = File.ReadAllBytes(path);
            int prefix = System.Math.Min(bytes.Length, LfsPointerPrefix.Length);
            if (Encoding.ASCII.GetString(bytes, 0, prefix) == LfsPointerPrefix)
            {
                throw new ToolException(path + " is a Git LFS pointer, not the file: run `git lfs pull`.");
            }

            return bytes;
        }
    }
}
