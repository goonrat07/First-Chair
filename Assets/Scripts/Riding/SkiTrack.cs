using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FirstChair.Riding
{
    /// <summary>
    /// A ski's track in the snow, as in Grand Mountain Adventure 2: a ribbon laid on the snow under the ski that stays,
    /// so each run's tracks build up on the slope and the next run crosses them (the track material is translucent, so
    /// crossings show darker). The view paints it every frame. Presentation only.
    /// </summary>
    /// <remarks>
    /// The ribbon (<see cref="TrackRibbon"/>) is laid in stretches of <see cref="StretchPoints"/> points. The newest
    /// stretch's mesh is rebuilt when a point is added; finished stretches are kept as they are, up to
    /// <see cref="RiderViewSettings.TrackKeptMetres"/> of track, after which the oldest is reused for the newest. The
    /// meshes sit under one scene object of their own, not under the ski, so they stay where they were laid.
    /// </remarks>
    /// <example>
    /// <code>
    /// var track = new SkiTrack("Ski track L", trackMaterial, settings);
    /// track.Paint(ski.position, snowPoint, snowNormal, onSnow, jumped: session.SnappedThisFrame);
    /// track.Dispose(); // when the view goes
    /// </code>
    /// </example>
    public sealed class SkiTrack : IDisposable
    {
        /// <summary>Points per stretch: one mesh each.</summary>
        public const int StretchPoints = 256;

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
        private Mesh _live;

        /// <summary>A track with nothing laid yet, under a new scene object named <paramref name="name"/>.</summary>
        /// <param name="name">The scene object's name, such as "Ski track L".</param>
        /// <param name="material">The track material (translucent, double-sided); null leaves Unity's default.</param>
        /// <param name="settings">Width, point spacing, lift and how much track to keep; read every frame.</param>
        public SkiTrack(string name, Material material, RiderViewSettings settings)
        {
            _settings = settings != null ? settings : throw new ArgumentNullException(nameof(settings));
            _material = material;
            _root = new GameObject(name).transform;
            _live = NewStretch();
        }

        /// <summary>
        /// Lays the track up to the ski, if it is on the snow. Call once a frame, after the skier is placed.
        /// </summary>
        /// <param name="skiBase">The ski's base, world space. It is moved onto the snow's plane, since the pose can lift it.</param>
        /// <param name="snowPoint">A point on the snow under the rider, world space.</param>
        /// <param name="snowNormal">The snow's normal there.</param>
        /// <param name="onSnow">Whether the ski is on the snow; in the air or crashed, the track breaks.</param>
        /// <param name="jumped">Whether the rider was moved this frame (a reset, a new run): the track breaks.</param>
        public void Paint(Vector3 skiBase, Vector3 snowPoint, Vector3 snowNormal, bool onSnow, bool jumped)
        {
            if (jumped || !onSnow)
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
            Vector3 onPlane = skiBase - snowNormal * Vector3.Dot(skiBase - snowPoint, snowNormal);
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
