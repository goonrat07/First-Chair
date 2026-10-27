using System;
using FirstChair.Riding;
using NUnit.Framework;

namespace FirstChair.Riding.Tests
{
    /// <summary>The square of snow the ski grooves are stamped into (Story 017): a window ahead of the camera, texel-snapped.</summary>
    public sealed class TrackGrooveWindowTests
    {
        // The story's window: 32 m at 2,048 texels (1.6 cm a texel), its centre 0.375 of its side ahead of the camera.
        private const float Size = 32f;
        private const int Texels = 2048;
        private const float Ahead = 0.375f;
        private const float TexelSize = Size / Texels;
        private const float Tolerance = 1e-4f;

        // A camera somewhere on a mountain, looking down the fall line (+Z) and a little across.
        private const float CameraX = 412.37f;
        private const float CameraZ = 288.91f;
        private const float ForwardX = 0.3f;
        private const float ForwardZ = 0.8f;

        [Test]
        public void Follow_AnyCamera_SnapsTheOriginToWholeTexels()
        {
            // Arrange
            var window = new TrackGrooveWindow(Size, Texels, Ahead);

            // Act
            window.Follow(CameraX, CameraZ, ForwardX, ForwardZ);

            // Assert
            double texelsX = window.OriginX / TexelSize, texelsZ = window.OriginZ / TexelSize;
            Assert.That(texelsX, Is.EqualTo(Math.Round(texelsX)).Within(Tolerance));
            Assert.That(texelsZ, Is.EqualTo(Math.Round(texelsZ)).Within(Tolerance));
        }

        [Test]
        public void Follow_ACameraLookingDownhill_HasItInsideAndMostOfTheWindowAhead()
        {
            // Arrange
            var window = new TrackGrooveWindow(Size, Texels, Ahead);

            // Act
            window.Follow(CameraX, CameraZ, 0f, 1f);
            bool inside = window.ToTexture(CameraX, CameraZ, out float u, out float v);

            // Assert: the camera sits an eighth of the way in from the window's back edge, centred across it
            Assert.That(inside, Is.True);
            Assert.That(v, Is.EqualTo(0.5f - Ahead).Within(TexelSize / Size + Tolerance));
            Assert.That(u, Is.EqualTo(0.5f).Within(TexelSize / Size + Tolerance));
        }

        [Test]
        public void Follow_ACameraMovingWithinATexel_KeepsTheWindowStill()
        {
            // Arrange: a camera looking straight down, a third and then two thirds of a texel past a texel edge
            var window = new TrackGrooveWindow(Size, Texels, Ahead);
            float edge = 400f;
            window.Follow(edge + TexelSize / 3f, edge, 0f, 0f);
            float originX = window.OriginX, originZ = window.OriginZ;

            // Act
            window.Follow(edge + 2f * TexelSize / 3f, edge, 0f, 0f);

            // Assert
            Assert.That(window.OriginX, Is.EqualTo(originX));
            Assert.That(window.OriginZ, Is.EqualTo(originZ));
        }

        [Test]
        public void Follow_ACameraLookingStraightDown_CentresTheWindowOnIt()
        {
            // Arrange
            var window = new TrackGrooveWindow(Size, Texels, Ahead);

            // Act
            window.Follow(CameraX, CameraZ, 0f, 0f);
            window.ToTexture(CameraX, CameraZ, out float u, out float v);

            // Assert
            Assert.That(u, Is.EqualTo(0.5f).Within(TexelSize / Size + Tolerance));
            Assert.That(v, Is.EqualTo(0.5f).Within(TexelSize / Size + Tolerance));
        }

        [Test]
        public void ToTexture_TheWindowsCorners_MapToZeroAndOne()
        {
            // Arrange
            var window = new TrackGrooveWindow(Size, Texels, Ahead);
            window.Follow(CameraX, CameraZ, ForwardX, ForwardZ);

            // Act
            bool near = window.ToTexture(window.OriginX, window.OriginZ, out float u0, out float v0);
            bool far = window.ToTexture(window.OriginX + Size, window.OriginZ + Size, out float u1, out float v1);
            bool outside = window.ToTexture(window.OriginX - 1f, window.OriginZ + 1f, out _, out _);

            // Assert
            Assert.That(near && far, Is.True);
            Assert.That(new[] { u0, v0, u1, v1 }, Is.EqualTo(new[] { 0f, 0f, 1f, 1f }).Within(Tolerance));
            Assert.That(outside, Is.False);
        }

        [Test]
        public void Constructor_ASizeTexelCountOrAheadOutOfRange_Throws()
        {
            // Act and assert
            Assert.Throws<ArgumentOutOfRangeException>(() => new TrackGrooveWindow(0f, Texels, Ahead));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TrackGrooveWindow(Size, 0, Ahead));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TrackGrooveWindow(Size, Texels, 0.6f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TrackGrooveWindow(Size, Texels, -0.1f));
        }
    }
}
