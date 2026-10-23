using UnityEngine;
using UnityEngine.Rendering;

namespace FirstChair.Riding
{
    /// <summary>
    /// A ski's track in the snow: a flat ribbon trailing behind the ski, like the grooves in Grand Mountain Adventure 2
    /// that show speed and line. The view turns it on while the ski is on the snow. Presentation only.
    /// </summary>
    /// <example>
    /// <code>
    /// TrailRenderer track = SkiTrack.Create(skiTransform, trackMaterial, settings);
    /// track.emitting = !state.Airborne &amp;&amp; !state.Crashed;
    /// </code>
    /// </example>
    public static class SkiTrack
    {
        /// <summary>Adds a track under a ski and returns its trail, not emitting yet.</summary>
        /// <param name="ski">The ski's transform; its origin is the ski base, +Y up.</param>
        /// <param name="material">The track material (double-sided); null leaves Unity's default.</param>
        /// <param name="settings">Width, lifetime and lift.</param>
        public static TrailRenderer Create(Transform ski, Material material, RiderViewSettings settings)
        {
            var track = new GameObject("Track");
            track.transform.SetParent(ski, false);
            track.transform.localPosition = new Vector3(0f, settings.TrackLift, 0f);

            // TransformZ alignment lays the ribbon flat when the object's Z axis points up.
            track.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var trail = track.AddComponent<TrailRenderer>();
            trail.alignment = LineAlignment.TransformZ;
            trail.time = settings.TrackLifetime;
            trail.minVertexDistance = settings.TrackMinVertexDistance;
            trail.widthMultiplier = settings.TrackWidth;
            trail.numCapVertices = 0;
            trail.autodestruct = false;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = true;
            trail.generateLightingData = true;
            trail.emitting = false;
            if (material != null)
            {
                trail.sharedMaterial = material;
            }

            return trail;
        }
    }
}
