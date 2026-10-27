using FirstChair.Riding;
using NUnit.Framework;

namespace FirstChair.Riding.Tests
{
    /// <summary>Which tree model stands at each tree of a layout (Story 016): a hash of its position, weighted by a mix.</summary>
    public sealed class TreeVariantPickerTests
    {
        // The story's mix: spruce, slim fir, broad spruce, young, snag, per cent.
        private static readonly int[] Mix = { 45, 25, 20, 7, 3 };
        private const int Grid = 200;           // a 200 x 200 forest on a 3 m grid: 40,000 trees
        private const int SpacingMm = 3000;

        // A model 14 m tall with a 1.25 m crown radius, as the slim fir is, and the story's 16.5 m height limit.
        private const float CrownRadius = 1.25f;
        private const float Height = 14f;
        private const float MaxHeight = 16.5f;
        private const float Tolerance = 1e-5f;

        [Test]
        public void Pick_SamePosition_GivesTheSameVariant()
        {
            // Act
            int first = TreeVariantPicker.Pick(123456, -98765, Mix);
            int again = TreeVariantPicker.Pick(123456, -98765, Mix);

            // Assert
            Assert.That(again, Is.EqualTo(first));
        }

        [Test]
        public void Pick_ManyTrees_FollowsTheMix()
        {
            // Arrange
            var counts = new int[Mix.Length];

            // Act
            for (int i = 0; i < Grid; i++)
            {
                for (int j = 0; j < Grid; j++)
                {
                    counts[TreeVariantPicker.Pick(i * SpacingMm, j * SpacingMm, Mix)]++;
                }
            }

            // Assert: each share within 1.5 points of its weight
            for (int v = 0; v < Mix.Length; v++)
            {
                double share = 100.0 * counts[v] / (Grid * Grid);
                Assert.That(share, Is.EqualTo(Mix[v]).Within(1.5), "variant " + v);
            }
        }

        [Test]
        public void Pick_ARowOfNeighbours_IsNotAllOneVariant()
        {
            // Arrange: ten trees a metre apart, as along a tree line
            var seen = new bool[Mix.Length];

            // Act
            for (int i = 0; i < 10; i++)
            {
                seen[TreeVariantPicker.Pick(50000 + i * 1000, 70000, Mix)] = true;
            }

            // Assert
            int kinds = 0;
            foreach (bool s in seen)
            {
                kinds += s ? 1 : 0;
            }

            Assert.That(kinds, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void Pick_AVariantWeightedZero_IsNeverChosen()
        {
            // Arrange
            int[] noSnags = { 45, 25, 20, 7, 0 };

            // Act and assert
            for (int i = 0; i < 5000; i++)
            {
                Assert.That(TreeVariantPicker.Pick(i * 2311, i * 7919, noSnags), Is.Not.EqualTo(4));
            }
        }

        [Test]
        public void Pick_EveryWeightZeroOrNoVariants_IsMinusOne()
        {
            // Act and assert
            Assert.That(TreeVariantPicker.Pick(1000, 2000, new[] { 0, 0, 0 }), Is.EqualTo(-1));
            Assert.That(TreeVariantPicker.Pick(1000, 2000, new int[0]), Is.EqualTo(-1));
            Assert.That(TreeVariantPicker.Pick(1000, 2000, null), Is.EqualTo(-1));
        }

        [Test]
        public void Pick_ANegativeWeight_CountsAsZero()
        {
            // Act and assert: only the second variant has a share
            for (int i = 0; i < 200; i++)
            {
                Assert.That(TreeVariantPicker.Pick(i * 4001, i * 3001, new[] { -5, 3 }), Is.EqualTo(1));
            }
        }

        [Test]
        public void Scale_ABranchRadiusTheModelCanReach_MatchesTheCrownToIt()
        {
            // Act: a 1.5 m branch radius needs 1.2 times the crown, 16.8 m tall, but no height limit applies
            float scale = TreeVariantPicker.Scale(1.5f, CrownRadius, Height, float.MaxValue);

            // Assert
            Assert.That(scale, Is.EqualTo(1.2f).Within(Tolerance));
        }

        [Test]
        public void Scale_ABranchRadiusOutOfReach_StopsAtHalfOrTwice()
        {
            // Act
            float small = TreeVariantPicker.Scale(0.1f, CrownRadius, Height, float.MaxValue);
            float large = TreeVariantPicker.Scale(10f, CrownRadius, Height, float.MaxValue);

            // Assert
            Assert.That(small, Is.EqualTo(TreeVariantPicker.MinScale));
            Assert.That(large, Is.EqualTo(TreeVariantPicker.MaxScale));
        }

        [Test]
        public void Scale_AModelThatWouldStandTooTall_IsLoweredToTheLimit()
        {
            // Act: a 2.5 m branch radius would make it 28 m tall
            float scale = TreeVariantPicker.Scale(2.5f, CrownRadius, Height, MaxHeight);

            // Assert
            Assert.That(scale * Height, Is.EqualTo(MaxHeight).Within(Tolerance));
        }

        [Test]
        public void Scale_AModelUnderTheLimit_KeepsItsCrownMatch()
        {
            // Act: 1.1 times, 15.4 m tall
            float scale = TreeVariantPicker.Scale(1.375f, CrownRadius, Height, MaxHeight);

            // Assert
            Assert.That(scale, Is.EqualTo(1.1f).Within(Tolerance));
        }

        [Test]
        public void Scale_AnUnknownHeight_SkipsTheLimit()
        {
            // Act
            float scale = TreeVariantPicker.Scale(2.5f, CrownRadius, 0f, MaxHeight);

            // Assert
            Assert.That(scale, Is.EqualTo(2f).Within(Tolerance));
        }
    }
}
