using System.Collections.Generic;
using FirstChair.Riding;
using FirstChair.Sim;
using NUnit.Framework;

namespace FirstChair.Riding.Tests
{
    /// <summary>The Feel Lab's values: every riding number by name, and the text it copies out and loads back (Story 009).</summary>
    public sealed class FeelLabValuesTests
    {
        [Test]
        public void TuningNames_ListEveryRidingNumber_InDeclarationOrder()
        {
            // Act
            IReadOnlyList<string> names = FeelLabValues.TuningNames();

            // Assert
            Assert.That(names, Does.Contain(nameof(Tuning.TurnRateLow)));
            Assert.That(names, Does.Contain(nameof(Tuning.GroomedCarveGrip)));
            Assert.That(names[0], Is.EqualTo(nameof(Tuning.Gravity)));
            Assert.That(names, Is.Unique);
        }

        [Test]
        public void WithThenGet_RoundTripsAValue_AndChangesOnlyThatNumber()
        {
            // Arrange
            Tuning baseline = Tuning.Default;

            // Act
            Tuning changed = FeelLabValues.With(baseline, nameof(Tuning.TurnRateLow), 2.75);

            // Assert
            Assert.That(FeelLabValues.Get(changed, nameof(Tuning.TurnRateLow)), Is.EqualTo(2.75).Within(1e-9));
            foreach (string name in FeelLabValues.TuningNames())
            {
                if (name != nameof(Tuning.TurnRateLow))
                {
                    Assert.That(FeelLabValues.Get(changed, name), Is.EqualTo(FeelLabValues.Get(baseline, name)), name);
                }
            }
        }

        [Test]
        public void With_UnknownName_ThrowsArgumentException()
        {
            // Act and Assert
            Assert.That(() => FeelLabValues.With(Tuning.Default, "NoSuchNumber", 1.0), Throws.ArgumentException);
        }

        [Test]
        public void FormatThenParse_RoundTrips_AndParseSkipsCommentsBlanksAndJunk()
        {
            // Arrange
            var values = new List<KeyValuePair<string, double>>
            {
                new KeyValuePair<string, double>("Tuning.TurnRateLow", 2.75),
                new KeyValuePair<string, double>("Camera.distance", 30.5),
            };

            // Act
            string text = FeelLabValues.Format(values);
            Dictionary<string, double> parsed = FeelLabValues.Parse("# from the Feel Lab\n\n" + text + "not a line\nx = y\n");

            // Assert
            Assert.That(text, Does.Contain("Tuning.TurnRateLow = 2.75"));
            Assert.That(parsed.Count, Is.EqualTo(2));
            Assert.That(parsed["Tuning.TurnRateLow"], Is.EqualTo(2.75));
            Assert.That(parsed["Camera.distance"], Is.EqualTo(30.5));
        }

        [TestCase("lowestRiderOnScreen", "Lowest rider on screen")]
        [TestCase("TurnRateLow", "Turn rate low")]
        [TestCase("GroomedCarveGrip", "Groomed carve grip")]
        public void Label_SplitsCamelCaseIntoWords(string name, string expected)
        {
            // Act and Assert
            Assert.That(FeelLabValues.Label(name), Is.EqualTo(expected));
        }
    }
}
