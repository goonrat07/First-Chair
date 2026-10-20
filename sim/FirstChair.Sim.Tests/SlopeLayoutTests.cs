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
