using System;

namespace FirstChair.Riding
{
    /// <summary>
    /// The square of snow the ski grooves are stamped into (Story 017): a window ahead of the camera, its edges snapped
    /// to whole texels so a groove lands on the same texels from frame to frame and never swims, with the mapping from
    /// world X and Z to the texture's coordinates. Engine-free, so it is testable without Unity.
    /// </summary>
    /// <example>
    /// <code>
    /// var window = new TrackGrooveWindow(64f, 2048, 0.375f);
    /// window.Follow(camera.position.x, camera.position.z, camera.forward.x, camera.forward.z);
    /// Shader.SetGlobalVector(windowId, new Vector4(window.OriginX, window.OriginZ, window.Size, window.TexelSize));
    /// </code>
    /// </example>
    public sealed class TrackGrooveWindow
    {
        // A forward direction shorter than this, flattened onto the snow, is a camera looking straight down.
        private const float MinForward = 1e-4f;

        /// <summary>A window <paramref name="size"/> metres square, <paramref name="texels"/> texels a side.</summary>
        /// <param name="size">The window's side, metres; above 0.</param>
        /// <param name="texels">Texels a side; at least 1.</param>
        /// <param name="ahead">How far the window's centre sits ahead of the camera, as a fraction of its side, 0 to 0.5.</param>
        /// <exception cref="ArgumentOutOfRangeException">A size or texel count out of range, or ahead outside 0 to 0.5.</exception>
        public TrackGrooveWindow(float size, int texels, float ahead)
        {
            if (!(size > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(size), size, "The window needs a size above 0.");
            }

            if (texels < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(texels), texels, "The window needs at least one texel.");
            }

            if (!(ahead >= 0f && ahead <= 0.5f))
            {
                throw new ArgumentOutOfRangeException(nameof(ahead), ahead, "Ahead runs from 0 (centred) to 0.5 (from the camera on).");
            }

            Size = size;
            Texels = texels;
            Ahead = ahead;
            TexelSize = size / texels;
        }

        /// <summary>The window's side, metres.</summary>
        public float Size { get; }

        /// <summary>Texels a side.</summary>
        public int Texels { get; }

        /// <summary>A texel's side, metres.</summary>
        public float TexelSize { get; }

        /// <summary>How far the window's centre sits ahead of the camera, as a fraction of its side.</summary>
        public float Ahead { get; }

        /// <summary>The window's edge of least X, metres; a whole number of texels.</summary>
        public float OriginX { get; private set; }

        /// <summary>The window's edge of least Z, metres; a whole number of texels.</summary>
        public float OriginZ { get; private set; }

        /// <summary>
        /// Moves the window to the camera: centred <see cref="Ahead"/> of its side ahead of it along its view flattened onto
        /// the snow (centred on it when it looks straight down), then snapped down to whole texels.
        /// </summary>
        /// <param name="cameraX">The camera's X, metres.</param>
        /// <param name="cameraZ">The camera's Z, metres.</param>
        /// <param name="forwardX">The camera's forward direction's X.</param>
        /// <param name="forwardZ">The camera's forward direction's Z.</param>
        public void Follow(float cameraX, float cameraZ, float forwardX, float forwardZ)
        {
            float length = (float)Math.Sqrt(forwardX * forwardX + forwardZ * forwardZ);
            float aheadMetres = length > MinForward ? Ahead * Size / length : 0f;
            OriginX = Snap(cameraX + forwardX * aheadMetres - 0.5f * Size);
            OriginZ = Snap(cameraZ + forwardZ * aheadMetres - 0.5f * Size);
        }

        /// <summary>A world position's texture coordinates in the window, (0, 0) at its origin and (1, 1) at the far corner.</summary>
        /// <param name="x">X, metres.</param>
        /// <param name="z">Z, metres.</param>
        /// <param name="u">The coordinate along X.</param>
        /// <param name="v">The coordinate along Z.</param>
        /// <returns>True if the position is inside the window.</returns>
        public bool ToTexture(float x, float z, out float u, out float v)
        {
            u = (x - OriginX) / Size;
            v = (z - OriginZ) / Size;
            return u >= 0f && u <= 1f && v >= 0f && v <= 1f;
        }

        private float Snap(float metres) => (float)Math.Floor(metres / TexelSize) * TexelSize;
    }
}
