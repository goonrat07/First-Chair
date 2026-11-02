using System;
using System.Collections.Generic;
using UnityEngine;

namespace FirstChair.Riding
{
    /// <summary>
    /// A ski's footprint in the snow (Story 018): a quad under the ski, from its trailing end to its leading end, that
    /// <see cref="TrackGrooves"/> stamps as it does a track while the ski is on the snow. The track ribbon starts at the
    /// ski's trailing end (<see cref="SkiTrack"/>), so without it the snow mesh would cut its trench only behind the ski;
    /// with it the trench runs under the ski too, and the ski sinks into it (<see cref="SkierView"/>). No renderer: only
    /// the stamp draws it. Display only. Single-thread only (Unity's main thread).
    /// </summary>
    /// <remarks>
    /// The quad's corners are in world space and its first UV channel runs across the ski, u 0 at one edge and 1 at the
    /// other, as a track ribbon's does, so the stamp gives it the same rounded profile. Placing it each frame moves four
    /// vertices and allocates nothing.
    /// </remarks>
    /// <example>
    /// <code>
    /// var footprint = new GrooveFootprint("Ski footprint L");
    /// footprint.Place(trailingEnd, leadingEnd, snowNormal, settings.TrackWidth);   // each frame the ski is on the snow
    /// footprint.Lift();                                                            // in the air
    /// footprint.Dispose();                                                         // when the ski goes
    /// </code>
    /// </example>
    public sealed class GrooveFootprint : IDisposable
    {
        private static readonly List<GrooveFootprint> Placed = new List<GrooveFootprint>();

        // A ski shorter than this (m) has no side to its footprint.
        private const float MinLength = 1e-4f;

        private readonly Vector3[] _corners = new Vector3[4];
        private int _index = -1;

        /// <summary>A footprint, lifted, with its own mesh named <paramref name="name"/>.</summary>
        /// <param name="name">The mesh's name, such as "Ski footprint L".</param>
        public GrooveFootprint(string name)
        {
            Mesh = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave };
            Mesh.MarkDynamic();
            Mesh.SetVertices(_corners);
            Mesh.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) });
            Mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
        }

        /// <summary>The footprints on the snow now, in no order. Read them by index each frame; don't keep the list.</summary>
        public static IReadOnlyList<GrooveFootprint> Down => Placed;

        /// <summary>The quad, in world space; null once disposed.</summary>
        public Mesh Mesh { get; private set; }

        // With domain reloading off, statics outlive play mode: start each run with no footprints.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearFootprints() => Placed.Clear();

        /// <summary>Puts the footprint on the snow under a ski, and keeps it there until it is placed again or lifted.</summary>
        /// <param name="trailing">The ski's trailing end on the snow, world space.</param>
        /// <param name="leading">Its leading end on the snow.</param>
        /// <param name="snowNormal">The snow's normal under the ski.</param>
        /// <param name="width">The footprint's width, metres: the track's, so the trench runs on into the track.</param>
        public void Place(Vector3 trailing, Vector3 leading, Vector3 snowNormal, float width)
        {
            if (Mesh == null)
            {
                return;
            }

            Vector3 side = Vector3.Cross(snowNormal, leading - trailing);
            float length = side.magnitude;
            side = length > MinLength ? side * (0.5f * width / length) : Vector3.zero;
            _corners[0] = trailing - side;
            _corners[1] = trailing + side;
            _corners[2] = leading + side;
            _corners[3] = leading - side;
            Mesh.SetVertices(_corners);
            Mesh.RecalculateBounds();
            if (_index < 0)
            {
                _index = Placed.Count;
                Placed.Add(this);
            }
        }

        /// <summary>Takes the footprint off the snow: nothing stamps it until it is placed again.</summary>
        public void Lift()
        {
            if (_index < 0 || _index >= Placed.Count || Placed[_index] != this)
            {
                _index = -1;
                return;
            }

            // The last footprint takes this one's place: the order doesn't matter, since the stamp keeps the deeper trench.
            int last = Placed.Count - 1;
            GrooveFootprint moved = Placed[last];
            Placed[_index] = moved;
            moved._index = _index;
            Placed.RemoveAt(last);
            _index = -1;
        }

        /// <summary>Lifts the footprint and destroys its mesh.</summary>
        public void Dispose()
        {
            Lift();
            if (Mesh != null)
            {
                UnityEngine.Object.Destroy(Mesh);
                Mesh = null;
            }
        }
    }
}
