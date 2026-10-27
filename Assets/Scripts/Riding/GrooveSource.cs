using System.Collections.Generic;
using UnityEngine;

namespace FirstChair.Riding
{
    /// <summary>
    /// Marks a track ribbon's renderer as a source of ski grooves (Story 017): while it is enabled,
    /// <see cref="TrackGrooves"/> stamps its mesh into the snow. The mesh's first UV channel must run across the track,
    /// u 0 at one edge and 1 at the other, as the ribbons' do (<see cref="TrackRibbon"/>, and the scene builder's old
    /// tracks). Display only.
    /// </summary>
    /// <example>
    /// <code>
    /// stretch.AddComponent&lt;MeshRenderer&gt;().sharedMaterial = trackMaterial;
    /// stretch.AddComponent&lt;GrooveSource&gt;(); // after the renderer, which it finds when it wakes
    /// </code>
    /// </example>
    [DisallowMultipleComponent]
    public sealed class GrooveSource : MonoBehaviour
    {
        private static readonly List<GrooveSource> Enabled = new List<GrooveSource>();

        private int _index = -1;

        /// <summary>The sources enabled now, in no order. Read them by index each frame; don't keep the list.</summary>
        public static IReadOnlyList<GrooveSource> Active => Enabled;

        /// <summary>The ribbon's renderer, found when the source wakes; null if its object has none.</summary>
        public Renderer Renderer { get; private set; }

        // With domain reloading off, statics outlive play mode: start each run with no sources.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearSources() => Enabled.Clear();

        private void Awake() => Renderer = GetComponent<Renderer>();

        private void OnEnable()
        {
            _index = Enabled.Count;
            Enabled.Add(this);
        }

        private void OnDisable()
        {
            if (_index < 0 || _index >= Enabled.Count || Enabled[_index] != this)
            {
                return;
            }

            // The last source takes this one's place: the order doesn't matter, since the stamp keeps the deeper groove.
            int last = Enabled.Count - 1;
            GrooveSource moved = Enabled[last];
            Enabled[_index] = moved;
            moved._index = _index;
            Enabled.RemoveAt(last);
            _index = -1;
        }
    }
}
