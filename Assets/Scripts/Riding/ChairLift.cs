using UnityEngine;
using UnityEngine.Rendering;

namespace FirstChair.Riding
{
    /// <summary>
    /// The test mountain's chairlift in motion (Story 010): chairs at even spacing on the haul rope's loop
    /// (<see cref="CableLoop"/>), going up one side and down the other, and the rope drawn as a line. The scene builder
    /// gives it the loop's points (the stations, the tower tops, the bullwheels) and which of them riders get on and off
    /// at; the towers and stations are scenery it doesn't touch. Presentation only: the sim never sees the lift.
    /// </summary>
    /// <example>
    /// <code>
    /// int chair = lift.NextChairToBoard(out double wait);  // the rope moves wait metres before it arrives
    /// Transform seat = lift.Chair(chair);
    /// </code>
    /// </example>
    public sealed class ChairLift : MonoBehaviour
    {
        [Tooltip("The haul rope's loop, world space: up the first side from the bottom station, round the top wheel, down, round the bottom wheel.")]
        [SerializeField] private Vector3[] rope = new Vector3[0];

        [Tooltip("Index of the rope point where riders get on, at the bottom station.")]
        [SerializeField, Min(0)] private int boardingPoint;

        [Tooltip("Index of the rope point where riders get off, at the top station.")]
        [SerializeField, Min(0)] private int unloadPoint;

        [Tooltip("Rope speed, m/s.")]
        [SerializeField, Min(0.5f)] private float speed = 6f;

        [Tooltip("How many chairs ride the loop.")]
        [SerializeField, Min(2)] private int chairs = 80;

        [Tooltip("How far the seat hangs below the rope, metres.")]
        [SerializeField, Min(0.5f)] private float hang = 2.3f;

        [Tooltip("URP Lit material for the chairs.")]
        [SerializeField] private Material chairMaterial;

        [Tooltip("Material for the rope line.")]
        [SerializeField] private Material ropeMaterial;

        private CableLoop _loop;
        private Transform[] _chairs = new Transform[0];
        private double _offset;

        /// <summary>The rope's speed is multiplied by this: the rider holds W to hurry the ride up.</summary>
        public float SpeedMultiplier { get; set; } = 1f;

        /// <summary>The rope's loop, or null before the lift has built itself.</summary>
        public CableLoop Loop => _loop;

        /// <summary>How far the rope has moved since the lift started, metres; chair 0 is this far along the loop.</summary>
        public double Offset => _offset;

        /// <summary>Metres of rope between chairs.</summary>
        public double Spacing => _loop.Length / _chairs.Length;

        /// <summary>The distance along the loop where riders get on.</summary>
        public double BoardingDistance => _loop.DistanceAt(boardingPoint);

        /// <summary>The distance along the loop where riders get off.</summary>
        public double UnloadDistance => _loop.DistanceAt(unloadPoint);

        /// <summary>Where riders get on: the rope point at the bottom station.</summary>
        public Vector3 BoardingPoint => rope[boardingPoint];

        /// <summary>How far each seat hangs below the rope, metres.</summary>
        public float Hang => hang;

        /// <summary>A chair: its origin is the grip on the rope, its forward the way it travels.</summary>
        /// <param name="index">0 to the number of chairs - 1.</param>
        public Transform Chair(int index) => _chairs[index];

        /// <summary>The chair that next reaches the boarding point, and how far the rope moves before it does.</summary>
        /// <param name="wait">Metres of rope to come, 0 up to the spacing.</param>
        public int NextChairToBoard(out double wait)
        {
            double spacing = Spacing;
            wait = CableLoop.NextArrival(_offset, spacing, BoardingDistance);
            double from = (BoardingDistance - wait - _offset) % _loop.Length;
            if (from < 0.0)
            {
                from += _loop.Length;
            }

            return (int)System.Math.Round(from / spacing) % _chairs.Length;
        }

        private void Awake()
        {
            if (rope == null || rope.Length < 3)
            {
                Debug.LogError("ChairLift: no rope; the lift stands still.", this);
                enabled = false;
                return;
            }

            var x = new double[rope.Length];
            var y = new double[rope.Length];
            var z = new double[rope.Length];
            for (int i = 0; i < rope.Length; i++)
            {
                x[i] = rope[i].x;
                y[i] = rope[i].y;
                z[i] = rope[i].z;
            }

            _loop = new CableLoop(x, y, z);
            DrawRope();
            _chairs = new Transform[chairs];
            for (int i = 0; i < chairs; i++)
            {
                _chairs[i] = BuildChair(i);
            }

            PlaceChairs();
        }

        private void Update()
        {
            _offset += speed * SpeedMultiplier * Time.deltaTime;
            PlaceChairs();
        }

        private void PlaceChairs()
        {
            double spacing = Spacing;
            for (int i = 0; i < _chairs.Length; i++)
            {
                _loop.Evaluate(_offset + i * spacing, out double px, out double py, out double pz, out double dx, out double dz);
                _chairs[i].SetPositionAndRotation(
                    new Vector3((float)px, (float)py, (float)pz), Quaternion.LookRotation(new Vector3((float)dx, 0f, (float)dz)));
            }
        }

        private void DrawRope()
        {
            var line = gameObject.AddComponent<LineRenderer>();
            line.loop = true;
            line.positionCount = rope.Length;
            line.SetPositions(rope);
            line.widthMultiplier = 0.06f;
            line.numCornerVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            if (ropeMaterial != null)
            {
                line.sharedMaterial = ropeMaterial;
            }
        }

        // A chair from primitives: the hanger down from the grip, the seat, the back behind it and the safety bar's rest.
        private Transform BuildChair(int index)
        {
            var chair = new GameObject("Chair " + index).transform;
            chair.SetParent(transform, false);
            Part(PrimitiveType.Cylinder, chair, new Vector3(0f, -hang / 2f, 0f), new Vector3(0.06f, hang / 2f, 0.06f));
            Part(PrimitiveType.Cube, chair, new Vector3(0f, -hang, 0.05f), new Vector3(1.3f, 0.08f, 0.5f));
            Part(PrimitiveType.Cube, chair, new Vector3(0f, -hang + 0.3f, -0.2f), new Vector3(1.3f, 0.55f, 0.06f));
            Part(PrimitiveType.Cube, chair, new Vector3(0f, -hang + 0.45f, 0.35f), new Vector3(1.2f, 0.04f, 0.04f));
            return chair;
        }

        private void Part(PrimitiveType type, Transform parent, Vector3 position, Vector3 scale)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            var renderer = part.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.On;
            if (chairMaterial != null)
            {
                renderer.sharedMaterial = chairMaterial;
            }
        }
    }
}
