using FirstChair.Riding;
using FirstChair.Sim;
using NUnit.Framework;

namespace FirstChair.Riding.Tests
{
    /// <summary>Where the Feel Lab's "Go to the park" starts a run (Story 010).</summary>
    public sealed class SlopeSpotsTests
    {
        private const string Header = "format 1\nspec 1024000 1024000 0 400000\nbackground crud\nstart 1000 1000\n";

        [Test]
        public void AboveTheKickers_KickersDownTheFallLine_StartsUpTheRunInOfTheFirst()
        {
            // Arrange: the lower kicker listed first; the first down the hill has its lip at z 472, 4 m long, facing +Z.
            SlopeLayout layout = SlopeLayout.Parse(Header + "kicker 762000 540000 0 5000 6000 1200\nkicker 762000 472000 0 4000 5000 800\n");

            // Act
            bool found = SlopeSpots.AboveTheKickers(layout, 40.0, out double x, out double z);

            // Assert: 40 m above its foot, which is 4 m above its lip.
            Assert.That(found, Is.True);
            Assert.That(x, Is.EqualTo(762.0).Within(1e-9));
            Assert.That(z, Is.EqualTo(472.0 - 4.0 - 40.0).Within(1e-9));
        }

        [Test]
        public void AboveTheKickers_KickerFacingPlusX_StartsBackAlongItsFacing()
        {
            // Arrange: a kicker launching toward +X.
            SlopeLayout layout = SlopeLayout.Parse(Header + "kicker 300000 200000 90000 6000 7000 1600\n");

            // Act
            SlopeSpots.AboveTheKickers(layout, 30.0, out double x, out double z);

            // Assert
            Assert.That(x, Is.EqualTo(300.0 - 36.0).Within(1e-9));
            Assert.That(z, Is.EqualTo(200.0).Within(1e-9));
        }

        [Test]
        public void AboveTheKickers_NoKickers_False()
        {
            // Arrange
            SlopeLayout layout = SlopeLayout.Parse(Header);

            // Act and Assert
            Assert.That(SlopeSpots.AboveTheKickers(layout, 40.0, out _, out _), Is.False);
        }
    }
}
