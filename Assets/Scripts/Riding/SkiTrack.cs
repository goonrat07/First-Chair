using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FirstChair.Riding
{
    /// <summary>
    /// A ski's track in the snow, as in Grand Mountain Adventure 2: a ribbon laid on the snow behind the ski that stays,
    /// so each run's tracks build up on the slope and the next run crosses them (the track material is translucent, so
    /// crossings show darker). The view paints it every frame. Presentation only.
    /// </summary>
    /// <remarks>
    /// The ribbon (<see cref="TrackRibbon"/>) is laid in stretches of <see cref="StretchPoints"/> points. The newest
    /// stretch's mesh is rebuilt when a point is added; finished stretches are kept as they are, up to
    /// <see cref="RiderViewSettings.TrackKeptMetres"/> of track, after which the oldest is reused for the newest. The
    /// meshes sit under one scene object of their own, not under the ski, so they stay where they were laid. The ribbon
    /// runs up to the ski's trailing end (<see cref="SkiTrailingEnd"/>), its tail riding forward and its tip riding
    /// switch: it sits a little off the snow, above most of the ski's thickness, so laid from under the boot it showed
    /// through the back half of the ski. Each stretch is also a <see cref="GrooveSource"/>, so near the camera the snow
    /// under it is cut into a groove (Story 017, <see cref="TrackGrooves"/>).
    /// </remarks>
    /// <example>
    /// <code>
    /// ViewHelpers.SkiEnds(ski, out float tailBehind, out float tipAhead);
    /// var track = new SkiTrack("Ski track L", trackMaterial, settings, tailBehind, tipAhead);
    /// track.Paint(ski.position, ski.forward, velocity, snowPoint, snowNormal, onSnow, jumped: session.SnappedThisFrame);
    /// track.Dispose(); // when the view goes
    /// </code>
    /// </example>
    public sealed class SkiTrack : IDisposable
    {
        /// <summary>Points per stretch: one mesh each.</summary>
        public const int StretchPoints = 256;

        // The track changes ends only when the rider travels within 60 degrees of the ski's own line (the cosine).
        private const double SwitchCosine = 0.5;

        private readonly TrackRibbon _ribbon = new TrackRibbon(StretchPoints);
        private readonly RiderViewSettings _settings;
        private readonly Material _material;
        private readonly Transform _root;
        private readonly Queue<Mesh> _kept = new Queue<Mesh>();
        private readonly List<Mesh> _meshes = new List<Mesh>();
        private readonly float[] _positions = new float[StretchPoints * 6];
        private readonly float[] _normals = new float[StretchPoints * 6];
        private readonly float[] _uvs = new float[StretchPoints * 4];
        private readonly int[] _indices = new int[(StretchPoints - 1) * 6];
        private readonly List<Vector3> _vertexList = new List<Vector3>(StretchPoints * 2);
        private readonly List<Vector3> _normalList = new List<Vector3>(StretchPoints * 2);
        private readonly List<Vector2> _uvList = new List<Vector2>(StretchPoints * 2);
        private readonly float _tailBehind;
        private readonly float _tipAhead;
        private SkiTrailingEnd _end;
        private Mesh _live;

        /// <summary>A track with nothing laid yet, under a new scene object named <paramref name="name"/>.</summary>
        /// <param name="name">The scene object's name, such as "Ski track L".</param>
        /// <param name="material">The track material (translucent, double-sided); null leaves Unity's default.</param>
        /// <param name="settings">Width, point spacing, lift and how much track to keep; read every frame.</param>
        /// <param name="tailBehind">How far the ski's tail lies behind the base point it is painted from, metres.</param>
        /// <param name="tipAhead">How far its tip lies ahead of that point, metres.</param>
        public SkiTrack(string name, Material material, RiderViewSettings settings, float tailBehind, float tipAhead)
        {
            _settings = settings != null ? settings : throw new ArgumentNullException(nameof(settings));
            _material = material;
            _tailBehind = Mathf.Max(0f, tailBehind);
            _tipAhead = Mathf.Max(0f, tipAhead);
            _root = new GameObject(name).transform;
            _live = NewStretch();
        }

        /// <summary>
        /// Lays the track up to the ski's trailing end, if the ski is on the snow. Call once a frame, after the skier is
        /// placed.
        /// </summary>
        /// <param name="skiBase">
        /// The ski's base under the boot, world space. Its trailing end is moved onto the snow's plane, since the pose can
        /// lift it.
        /// </param>
        /// <param name="tipDirection">The way from the ski's tail to its tip, world space.</param>
        /// <param name="velocity">The rider's velocity, m/s, world space: the end that trails is the end it leaves behind.</param>
        /// <param name="snowPoint">A point on the snow under the rider, world space.</param>
        /// <param name="snowNormal">The snow's normal there.</param>
        /// <param name="onSnow">Whether the ski is on the snow; in the air or crashed, the track breaks.</param>
        /// <param name="jumped">Whether the rider was moved this frame (a reset, a new run): the track breaks.</param>
        public void Paint(
            Vector3 skiBase, Vector3 tipDirection, Vector3 velocity, Vector3 snowPoint, Vector3 snowNormal, bool onSnow, bool jumped)
        {
            // A change of end breaks the track too, so no strip runs the ski's length from one end's track to the other's.
            var travel = new PoseVector(velocity.x, velocity.y, velocity.z);
            var tip = new PoseVector(tipDirection.x, tipDirection.y, tipDirection.z);
            bool switched = _end.Update(travel, tip, SwitchCosine);
            if (switched || jumped || !onSnow)
            {
                _ribbon.Break();
            }

            if (!onSnow)
            {
                return;
            }

            _ribbon.Width = _settings.TrackWidth;
            _ribbon.MinStep = _settings.TrackMinVertexDistance;
            _ribbon.Lift = _settings.TrackLift;
            Vector3 trailing = skiBase + tipDirection.normalized * (float)_end.Offset(_tailBehind, _tipAhead);
            Vector3 onPlane = trailing - snowNormal * Vector3.Dot(trailing - snowPoint, snowNormal);
            if (!_ribbon.TryAdd(onPlane.x, onPlane.y, onPlane.z, snowNormal.x, snowNormal.y, snowNormal.z))
            {
                return;
            }

            Upload(_live);
            if (_ribbon.Full)
            {
                // Finished stretches kept: enough for TrackKeptMetres at the closest spacing, and at least one.
                _kept.Enqueue(_live);
                int keep = Mathf.Max(1, Mathf.CeilToInt(_settings.TrackKeptMetres / (StretchPoints * _settings.TrackMinVertexDistance)));
                _live = _kept.Count > keep ? _kept.Dequeue() : NewStretch();
                _ribbon.NextStretch();
                Upload(_live);
            }
        }

        /// <summary>Destroys the track's meshes and its scene object.</summary>
        public void Dispose()
        {
            foreach (Mesh mesh in _meshes)
            {
                UnityEngine.Object.Destroy(mesh);
            }

            _meshes.Clear();
            _kept.Clear();
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root.gameObject);
            }
        }

        private Mesh NewStretch()
        {
            var stretch = new GameObject("Stretch " + _meshes.Count);
            stretch.transform.SetParent(_root, false);
            var mesh = new Mesh { name = stretch.name };
            mesh.MarkDynamic();
            _meshes.Add(mesh);
            stretch.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = stretch.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            if (_material != null)
            {
                renderer.sharedMaterial = _material;
            }

            // Story 017: TrackGrooves stamps the stretch into the snow as a groove; its u already runs across the track.
            stretch.AddComponent<GrooveSource>();
            return mesh;
        }

        private void Upload(Mesh mesh)
        {
            int indexCount = _ribbon.Write(_positions, _normals, _uvs, _indices);
            _vertexList.Clear();
            _normalList.Clear();
            _uvList.Clear();
            for (int v = 0; v < _ribbon.VertexCount; v++)
            {
                _vertexList.Add(new Vector3(_positions[3 * v], _positions[3 * v + 1], _positions[3 * v + 2]));
                _normalList.Add(new Vector3(_normals[3 * v], _normals[3 * v + 1], _normals[3 * v + 2]));
                _uvList.Add(new Vector2(_uvs[2 * v], _uvs[2 * v + 1]));
            }

            mesh.Clear();
            mesh.SetVertices(_vertexList);
            mesh.SetNormals(_normalList);
            mesh.SetUVs(0, _uvList);
            mesh.SetTriangles(_indices, 0, indexCount, 0);
        }
    }
}
