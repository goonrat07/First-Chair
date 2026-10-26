using System;
using FirstChair.Sim;
using NUnit.Framework;

namespace FirstChair.Sim.Tests
{
    /// <summary>SlopeLayout.Parse reads the slope layout text and refuses anything malformed at the boundary.</summary>
    public class SlopeLayoutTests
    {
        private const string Valid =
            "# a comment\n"
            + "format 1\n"
            + "spec 100000 80000 -5000 60000\n"
            + "background crud\n"
            + "\n"
            + "zone groomer groomed 10000 0 30000 80000\n"
            + "zone patch slush 15000 20000 25000 30000\n"
            + "start 20000 1500\n"
            + "kicker 50000 40000 90000 4000 5000 800\n"
            + "tree 70000 60000 250 2000\n";

        [Test]
        public void Parse_ValidLayout_ReadsEveryRecord()
        {
            // Act
            SlopeLayout layout = SlopeLayout.Parse(Valid);

            // Assert
            Assert.That(new[] { layout.Spec.SizeXMm, layout.Spec.SizeZMm, layout.Spec.MinHeightMm, layout.Spec.MaxHeightMm }, Is.EqualTo(new[] { 100000, 80000, -5000, 60000 }));
            Assert.That(layout.Background, Is.EqualTo(SnowType.Crud));
            Assert.That(layout.StartX, Is.EqualTo(Fix64.FromInt(20)));
            Assert.That(layout.StartZ, Is.EqualTo(Fix64.FromRatio(3, 2)));
            Assert.That(layout.Zones.Count, Is.EqualTo(2));
            Assert.That(layout.Zones[1].Name, Is.EqualTo("patch"));
            Assert.That(layout.Zones[1].Snow, Is.EqualTo(SnowType.Slush));
            Assert.That(new[] { layout.Zones[1].XMinMm, layout.Zones[1].ZMinMm, layout.Zones[1].XMaxMm, layout.Zones[1].ZMaxMm }, Is.EqualTo(new[] { 15000, 20000, 25000, 30000 }));
            SlopeKicker kicker = layout.Kickers[0];
            Assert.That(new[] { kicker.XMm, kicker.ZMm, kicker.FacingMillidegrees, kicker.LengthMm, kicker.WidthMm, kicker.HeightMm }, Is.EqualTo(new[] { 50000, 40000, 90000, 4000, 5000, 800 }));
            Assert.That(kicker.Height, Is.EqualTo(Fix64.FromRatio(4, 5)));
            Assert.That(Math.Abs(kicker.Facing.Raw - Fix64.HalfPi.Raw), Is.LessThanOrEqualTo(2L), "90000 millidegrees is pi / 2");
            SlopeTree tree = layout.Trees[0];
            Assert.That(new[] { tree.XMm, tree.ZMm, tree.TrunkRadiusMm, tree.BranchRadiusMm }, Is.EqualTo(new[] { 70000, 60000, 250, 2000 }));
            Assert.That(tree.BranchRadius, Is.EqualTo(Fix64.FromInt(2)));
        }

        [Test]
        public void Parse_RunLiftAndTowerRecords_ReadsThem()
        {
            // Arrange
            string text = Valid
                + "run meadow green 8000 20000 1500 60000 30000 20000 70000\n"
                + "lift chair1 50000 75000 50000 5000\n"
                + "tower chair1 50000 60000 11000\n"
                + "tower chair1 50000 30000 12500\n";

            // Act
            SlopeLayout layout = SlopeLayout.Parse(text);

            // Assert
            Assert.That(layout.Runs.Count, Is.EqualTo(1));
            SlopeRun run = layout.Runs[0];
            Assert.That(run.Name, Is.EqualTo("meadow"));
            Assert.That(run.Difficulty, Is.EqualTo(RunDifficulty.Green));
            Assert.That(run.WidthMm, Is.EqualTo(8000));
            Assert.That(run.Width, Is.EqualTo(Fix64.FromInt(8)));
            Assert.That(run.Points.Count, Is.EqualTo(3));
            Assert.That(new[] { run.Points[1].XMm, run.Points[1].ZMm }, Is.EqualTo(new[] { 60000, 30000 }));
            Assert.That(layout.Lifts.Count, Is.EqualTo(1));
            SlopeLift lift = layout.Lifts[0];
            Assert.That(lift.Name, Is.EqualTo("chair1"));
            Assert.That(new[] { lift.BottomXMm, lift.BottomZMm, lift.TopXMm, lift.TopZMm }, Is.EqualTo(new[] { 50000, 75000, 50000, 5000 }));
            Assert.That(lift.Towers.Count, Is.EqualTo(2));
            Assert.That(new[] { lift.Towers[1].XMm, lift.Towers[1].ZMm, lift.Towers[1].HeightMm }, Is.EqualTo(new[] { 50000, 30000, 12500 }));
            Assert.That(lift.Towers[1].Height, Is.EqualTo(Fix64.FromRatio(25, 2)));
        }

        [Test]
        public void Parse_LayoutWithoutRunsOrLifts_HasNone()
        {
            // Act
            SlopeLayout layout = SlopeLayout.Parse(Valid);

            // Assert
            Assert.That(layout.Runs.Count, Is.EqualTo(0));
            Assert.That(layout.Lifts.Count, Is.EqualTo(0));
        }

        [TestCase("green", RunDifficulty.Green)]
        [TestCase("blue", RunDifficulty.Blue)]
        [TestCase("red", RunDifficulty.Red)]
        [TestCase("black", RunDifficulty.Black)]
        [TestCase("park", RunDifficulty.Park)]
        public void Parse_RunDifficultyName_MapsToItsDifficulty(string name, RunDifficulty expected)
        {
            // Act
            SlopeLayout layout = SlopeLayout.Parse(Valid + "run a " + name + " 8000 0 0 1000 1000\n");

            // Assert
            Assert.That(layout.Runs[0].Difficulty, Is.EqualTo(expected));
        }

        [TestCase("lift a 0 0 1000 1000\ntower a 1000 1000 0\n")]
        [TestCase("lift a 0 0 1000 1000\ntower a 1000 1000 -500\n")]
        [TestCase("lift a 0 0 1000 1000\ntower a 100001 1000 9000\n")]
        [TestCase("lift a 0 0 1000 1000\ntower a 1000 1000\n")]
        [TestCase("lift a 0 0 1000 1000\nlift a 0 0 2000 2000\n")]
        [TestCase("run a green 8000 0 0 1000 1000\nrun a blue 8000 0 0 1000 1000\n")]
        public void Parse_BadSecondMountainRecord_Throws(string records)
        {
            // Arrange: line 11 is a good record, line 12 the bad one.
            string text = Valid + records;

            // Act and Assert
            Assert.That(() => SlopeLayout.Parse(text), Throws.ArgumentException.With.Message.Contains("line 12"));
        }

        [TestCase("groomed", SnowType.Groomed)]
        [TestCase("powder", SnowType.Powder)]
        [TestCase("crud", SnowType.Crud)]
        [TestCase("ice", SnowType.Ice)]
        [TestCase("slush", SnowType.Slush)]
        public void Parse_SnowName_MapsToItsSnowType(string name, SnowType expected)
        {
            // Act
            SlopeLayout layout = SlopeLayout.Parse(Valid.Replace("background crud", "background " + name));

            // Assert
            Assert.That(layout.Background, Is.EqualTo(expected));
        }

        [Test]
        public void Parse_CrlfLineEndingsAndTabs_ReadTheSameLayout()
        {
            // Arrange
            string crlf = Valid.Replace("\n", "\r\n").Replace("tree ", "tree\t");

            // Act
            SlopeLayout layout = SlopeLayout.Parse(crlf);

            // Assert
            Assert.That(layout.Trees[0].BranchRadiusMm, Is.EqualTo(2000));
            Assert.That(layout.Zones[0].Name, Is.EqualTo("groomer"));
        }

        [Test]
        public void Zone_Contains_IncludesItsEdgesOnly()
        {
            // Arrange
            SlopeZone zone = SlopeLayout.Parse(Valid).Zones[0];

            // Act and Assert
            Assert.That(zone.Contains(Fix64.FromInt(10), Fix64.Zero), Is.True);
            Assert.That(zone.Contains(Fix64.FromInt(30), Fix64.FromInt(80)), Is.True);
            Assert.That(zone.Contains(Fix64.FromInt(10) - Fix64.FromRaw(1), Fix64.FromInt(40)), Is.False);
            Assert.That(zone.Contains(Fix64.FromInt(20), Fix64.FromInt(80) + Fix64.FromRaw(1)), Is.False);
        }

        [Test]
        public void TryGetZone_UnknownName_ReturnsFalse()
        {
            // Act
            bool found = SlopeLayout.Parse(Valid).TryGetZone("Groomer", out _);

            // Assert
            Assert.That(found, Is.False);
        }

        [Test]
        public void Parse_Null_Throws()
        {
            Assert.That(() => SlopeLayout.Parse(null), Throws.TypeOf<ArgumentNullException>());
        }

        [TestCase("format 1")]
        [TestCase("spec 1000 1000 0 1000")]
        [TestCase("background powder")]
        [TestCase("start 0 0")]
        [TestCase("lift 0 0")]
        [TestCase("tree 1000 1000 300")]
        [TestCase("tree 1000 1000 x 2000")]
        [TestCase("tree 1000 1000 1.5 2000")]
        [TestCase("tree 1000 1000 - 2000")]
        [TestCase("tree 1000 1000 2147483648 2147483648")]
        [TestCase("tree -1 1000 300 2000")]
        [TestCase("tree 1000 80001 300 2000")]
        [TestCase("tree 1000 1000 0 2000")]
        [TestCase("tree 1000 1000 300 299")]
        [TestCase("kicker 1000 1000 0 0 1000 500")]
        [TestCase("kicker 1000 1000 0 1000 1000 -500")]
        [TestCase("kicker 1000 1000 360000 1000 1000 500")]
        [TestCase("kicker 1000 1000 -1 1000 1000 500")]
        [TestCase("zone groomer groomed 0 0 1000 1000")]
        [TestCase("zone other snowy 0 0 1000 1000")]
        [TestCase("zone other ice 1000 0 1000 1000")]
        [TestCase("zone other ice 0 2000 1000 1000")]
        [TestCase("zone other ice 0 0 100001 1000")]
        [TestCase("run a green 8000 0 0")]
        [TestCase("run a purple 8000 0 0 1000 1000")]
        [TestCase("run a green 0 0 0 1000 1000")]
        [TestCase("run a green 8000 0 0 1000")]
        [TestCase("run a green 8000 0 0 100001 1000")]
        [TestCase("run a green 8000 0 0 0 0")]
        [TestCase("lift a 0 0 0 0")]
        [TestCase("lift a 0 0 1000")]
        [TestCase("lift a 0 0 100001 1000")]
        [TestCase("tower nolift 1000 1000 10000")]
        public void Parse_BadExtraRecord_Throws(string record)
        {
            // Arrange
            string text = Valid + record + "\n";

            // Act and Assert
            Assert.That(() => SlopeLayout.Parse(text), Throws.ArgumentException.With.Message.Contains("line 11"));
        }

        [TestCase("")]
        [TestCase("# only a comment\n")]
        [TestCase("spec 1000 1000 0 1000\nformat 1\nbackground crud\nstart 0 0\n")]
        [TestCase("format 2\nspec 1000 1000 0 1000\nbackground crud\nstart 0 0\n")]
        [TestCase("format 1\nspec 1000 1000 0 1000\nstart 0 0\n")]
        [TestCase("format 1\nspec 1000 1000 0 1000\nbackground crud\n")]
        [TestCase("format 1\nbackground crud\nstart 0 0\n")]
        [TestCase("format 1\nbackground crud\nzone a ice 0 0 1 1\nspec 1000 1000 0 1000\nstart 0 0\n")]
        [TestCase("format 1\nspec 0 1000 0 1000\nbackground crud\nstart 0 0\n")]
        [TestCase("format 1\nspec 1000 1000 10 9\nbackground crud\nstart 0 0\n")]
        public void Parse_MissingOrMisplacedRecord_Throws(string text)
        {
            Assert.That(() => SlopeLayout.Parse(text), Throws.ArgumentException);
        }
    }
}
