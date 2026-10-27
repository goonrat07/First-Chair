using FirstChair.Sim;
using UnityEngine;

namespace FirstChair.Riding
{
    /// <summary>
    /// The skier's procedural layers over the blended pose clips (Story 013), applied each frame after the blend, in
    /// order: lean with the turn and angulation, springs (hips compressing, upper body pitching and lagging the roll),
    /// legs on the snow (two-bone IK and a hip drop, holding the boots where the clip and the snow put them while the
    /// body leans over them), and pole plants at each turn's start. Presentation only: it reads
    /// the sim and writes bones, nothing else. Every value comes from <see cref="RiderViewSettings"/>; a layer at weight
    /// 0 leaves the clip pose as it was, and a missing bone switches its layer off with one log line.
    /// </summary>
    /// <remarks>The math (lean, springs, IK, hip drop, plant timing) is in Riding/Core and tested in EditMode.</remarks>
    /// <example>
    /// <code>
    /// _procedural = new SkierProceduralPose(skier.transform, settings, poleLeft, poleRight, this);
    /// // each LateUpdate, after the clip blend:
    /// _procedural.Apply(driver.Session, _shown, driver.Paused, Time.deltaTime);
    /// </code>
    /// </example>
    internal sealed class SkierProceduralPose
    {
        private const float Gravity = 9.81f;
        private const float PoleLength = 1.1f;          // PR_SkiPole: tip to SOCKET_grip (asset-manifest.json)
        private const float SnowFadeSeconds = 0.15f;    // the layers ease out leaving the snow, and back in

        private readonly Transform _model;
        private readonly RiderViewSettings _settings;
        private readonly Transform _hips;
        private readonly Transform _spine;
        private readonly Transform _chest;
        private readonly Transform _head;
        private readonly Leg _legLeft;
        private readonly Leg _legRight;
        private readonly Arm _armLeft;
        private readonly Arm _armRight;
        private DampedSpring _lean;
        private DampedSpring _compression;
        private DampedSpring _pitch;
        private DampedSpring _sway;
        private PolePlantTimer _plant;
        private Vector3 _plantTip;
        private bool _plantFixed;
        private float _onSnow;
        private Vector3 _lastPosition;
        private Vector3 _lastVelocity;
        private int _history;

        /// <summary>Finds the bones and sockets; layers whose bones are missing stay off.</summary>
        /// <param name="model">The CH_Skier instance, child of the skier view, facing its +Z.</param>
        /// <param name="settings">The rider view settings.</param>
        /// <param name="poleLeft">The left pole's instance (its origin is the tip), or null.</param>
        /// <param name="poleRight">The right pole's instance, or null.</param>
        /// <param name="context">The component to name in a log line.</param>
        public SkierProceduralPose(Transform model, RiderViewSettings settings, Transform poleLeft, Transform poleRight, Object context)
        {
            _model = model;
            _settings = settings;
            _hips = Bone(model, "Hips");
            _spine = Bone(model, "Spine");
            _chest = Bone(model, "Chest");
            _head = Bone(model, "Head");
            _legLeft = Leg.Find(model, "_L", settings.SkierSkiSocketLeft);
            _legRight = Leg.Find(model, "_R", settings.SkierSkiSocketRight);
            _armLeft = Arm.Find(model, "_L", settings.SkierHandSocketLeft, poleLeft);
            _armRight = Arm.Find(model, "_R", settings.SkierHandSocketRight, poleRight);
            string missing = (_hips == null ? " Hips" : "") + (_spine == null ? " Spine" : "") + (_chest == null ? " Chest" : "") +
                             (_head == null ? " Head" : "") + (_legLeft == null ? " left leg" : "") + (_legRight == null ? " right leg" : "") +
                             (_armLeft == null ? " left arm or pole" : "") + (_armRight == null ? " right arm or pole" : "");
            if (missing.Length > 0)
            {
                Debug.Log("SkierProceduralPose: CH_Skier has no" + missing + "; the layers that need them stay off.", context);
            }
        }

        /// <summary>Applies the layers to the pose the clips just wrote. Allocates nothing.</summary>
        /// <param name="session">The ride: its last two sim states and the heightmap.</param>
        /// <param name="shown">The clip weights shown this frame, in <see cref="SkierPose"/> order.</param>
        /// <param name="paused">True on the chairlift.</param>
        /// <param name="dt">The frame, seconds.</param>
        public void Apply(RideSession session, float[] shown, bool paused, float dt)
        {
            _model.localRotation = Quaternion.identity;
            if (session == null || !(dt > 0f))
            {
                return;
            }

            RiderState current = session.Current;
            RiderState previous = session.Previous;
            Heightmap heightmap = session.Slope != null ? session.Slope.Heightmap : null;
            bool onSnow = !current.Airborne && !current.Crashed && !current.Grinding && !paused;
            _onSnow = Mathf.MoveTowards(_onSnow, onSnow ? 1f : 0f, dt / SnowFadeSeconds);
            if (session.SnappedThisFrame)
            {
                Reset();
            }

            Transform root = _model.parent != null ? _model.parent : _model;
            Vector3 up = root.up;
            float speed = Speed(current.Velocity);
            float shownLean = (float)TurnLean.Shown(
                shown[(int)SkierPose.CarveLeft], shown[(int)SkierPose.CarveRight], shown[(int)SkierPose.RaceCarveLeft],
                shown[(int)SkierPose.RaceCarveRight], _settings.LeanCarveClipDegrees * Mathf.Deg2Rad,
                _settings.LeanRaceClipDegrees * Mathf.Deg2Rad);

            // The feet's targets come from the clip pose, before the lean: the lean then tips the body over planted skis,
            // and the leg solve below bends the inside knee and stretches the outside leg, as a carving skier angulates.
            Vector3 targetLeft = default;
            Vector3 targetRight = default;
            bool legs = _legLeft != null && _legRight != null && _hips != null && heightmap != null && _settings.LegWeight > 0f && _onSnow > 0f;
            if (legs)
            {
                targetLeft = FootTarget(_legLeft, heightmap, root);
                targetRight = FootTarget(_legRight, heightmap, root);
            }

            float extra = Lean(previous, current, root, speed, shownLean, onSnow, dt);
            Springs(root, up, (shownLean + extra) * Mathf.Rad2Deg, dt);
            if (legs)
            {
                Legs(heightmap, up, targetLeft, targetRight);
            }

            Plants(current, heightmap, root, speed, onSnow, dt);
        }

        // 1. LEAN: roll the whole skier about the skis' line on the snow by the lean the turn needs over what it already
        // shows (the carve clips' own lean and its frame's tilt to the snow), then give part of it back at the chest and
        // head. Returns the extra lean shown, radians, positive right.
        private float Lean(in RiderState previous, in RiderState current, Transform root, float speed, float shownLean, bool onSnow, float dt)
        {
            float target = 0f;
            if (onSnow && _settings.LeanWeight > 0f)
            {
                double physical = TurnLean.Physical(speed, TurnRate(previous.Velocity, current.Velocity, speed),
                    Gravity, _settings.LeanMaxDegrees * Mathf.Deg2Rad);
                Vector3 up = root.InverseTransformDirection(Vector3.up);
                double needed = TurnLean.ExtraOverFrame(physical, shownLean, TurnLean.FrameRoll(up.x, up.y),
                    _settings.LeanMaxExtraDegrees * Mathf.Deg2Rad);
                double fade = TurnLean.SpeedFade(speed, _settings.LeanFromSpeed, _settings.LeanFullSpeed);
                target = (float)(needed * fade) * _settings.LeanWeight;
            }

            _lean.Step(target, _settings.LeanFrequency, 1.0, dt);
            float extra = (float)_lean.Value * _onSnow;
            if (Mathf.Abs(extra) < 1e-5f)
            {
                return 0f;
            }

            float degrees = extra * Mathf.Rad2Deg;
            _model.localRotation = Quaternion.AngleAxis(-degrees, Vector3.forward);   // the sim's roll sign: right is negative
            Vector3 axis = _model.forward;
            if (_chest != null)
            {
                _chest.rotation = Quaternion.AngleAxis(degrees * _settings.Angulation, axis) * _chest.rotation;
            }

            if (_head != null)
            {
                _head.rotation = Quaternion.AngleAxis(degrees * _settings.HeadLevel, axis) * _head.rotation;
            }

            return extra;
        }

        // 2. SPRINGS: each change of the body's velocity kicks a spring (taken whole, so the motion is the same at any
        // frame rate); constant acceleration holds it at gain x acceleration. The hips drop on a landing or a bump, the
        // upper body pitches forward when slowing and lags the body's roll across an edge change.
        private void Springs(Transform root, Vector3 up, float leanDegrees, float dt)
        {
            Vector3 position = root.position;
            Vector3 velocity = _history >= 1 ? (position - _lastPosition) / dt : Vector3.zero;
            Vector3 change = _history >= 2 ? velocity - _lastVelocity : Vector3.zero;
            if (change.magnitude > _settings.SpeedChangeLimit)
            {
                change = Vector3.zero;
            }

            _lastPosition = position;
            _lastVelocity = velocity;
            _history = Mathf.Min(_history + 1, 2);

            Vector3 forward = Vector3.ProjectOnPlane(root.forward, up).normalized;
            Kick(ref _compression, -_settings.CompressionGain, Vector3.Dot(change, up), _settings.CompressionFrequency);
            Kick(ref _pitch, -_settings.PitchGain, Vector3.Dot(change, forward), _settings.PitchFrequency);
            _compression.Step(0.0, _settings.CompressionFrequency, _settings.CompressionDamping, dt);
            _pitch.Step(0.0, _settings.PitchFrequency, _settings.PitchDamping, dt);
            _sway.Step(leanDegrees, _settings.SwayFrequency, _settings.SwayDamping, dt);
            float weight = _settings.SpringWeight * _onSnow;
            if (!(weight > 0f))
            {
                return;
            }

            float drop = Mathf.Clamp((float)_compression.Value, -_settings.CompressionMax, _settings.CompressionMax) * weight;
            float pitch = Mathf.Clamp((float)_pitch.Value, -_settings.PitchMaxDegrees, _settings.PitchMaxDegrees) * weight;
            float sway = Mathf.Clamp(((float)_sway.Value - leanDegrees) * _settings.SwayGain, -_settings.SwayMaxDegrees,
                _settings.SwayMaxDegrees) * weight;
            if (_hips != null)
            {
                _hips.position += up * drop;
            }

            if (_spine != null)
            {
                _spine.rotation = Quaternion.AngleAxis(pitch, _model.right) * Quaternion.AngleAxis(-sway, _model.forward) * _spine.rotation;
            }
        }

        // A spring holds at gain x acceleration under constant acceleration a: x'' = w^2 (gain a - x) - ..., so a change
        // of velocity dv adds w^2 gain dv to its velocity.
        private static void Kick(ref DampedSpring spring, float gain, float velocityChange, float frequency)
        {
            double w = 2.0 * Mathf.PI * frequency;
            spring.Velocity += w * w * gain * velocityChange;
        }

        // 3. LEGS: where each ankle goes so its ski meets the real snow. The clips stood the skis on the plane through the
        // rider's point on the snow; the ankle moves up or down by how far the snow under the boot lies off that plane.
        // Read from the clip pose, before the lean moves the feet.
        private Vector3 FootTarget(Leg leg, Heightmap heightmap, Transform root)
        {
            Vector3 sole = leg.Sole.position;
            Vector3 point = root.position;
            Vector3 normal = root.up;
            float plane = normal.y > 0.2f ? point.y - (normal.x * (sole.x - point.x) + normal.z * (sole.z - point.z)) / normal.y : point.y;
            float off = SnowHeight(heightmap, sole.x, sole.z) - plane;
            float shift = Mathf.Clamp(off, -_settings.FootMaxShift, _settings.FootMaxShift) * _settings.LegWeight * _onSnow;
            return leg.Foot.position + Vector3.up * shift;
        }

        private void Legs(Heightmap heightmap, Vector3 up, Vector3 targetLeft, Vector3 targetRight)
        {
            double drop = LegReach.HipDrop(
                ToPose(_legLeft.Upper.position - targetLeft), ToPose(_legRight.Upper.position - targetRight), ToPose(up),
                _legLeft.Length * _settings.LegStraightness, _legRight.Length * _settings.LegStraightness, _settings.HipMaxDrop);
            if (drop > 0.0)
            {
                _hips.position -= up * (float)drop;
            }

            Solve(_legLeft, targetLeft, heightmap, up);
            Solve(_legRight, targetRight, heightmap, up);
        }

        private void Solve(Leg leg, Vector3 target, Heightmap heightmap, Vector3 up)
        {
            Quaternion foot = leg.Foot.rotation;
            Chain(leg.Upper, leg.Lower, leg.Foot, target);
            leg.Foot.rotation = foot;

            // the ski pitches to the snow along its length, as far as the plane under the clip differs from it
            float weight = _settings.SkiPitchWeight * _settings.LegWeight * _onSnow;
            if (!(weight > 0f) || leg.Ski == null)
            {
                return;
            }

            Vector3 along = Vector3.ProjectOnPlane(leg.Ski.forward, Vector3.up);
            if (along.sqrMagnitude < 1e-6f)
            {
                return;
            }

            along.Normalize();
            float reach = _settings.SkiPitchReach;
            Vector3 sole = leg.Sole.position;
            float slope = (SnowHeight(heightmap, sole.x + along.x * reach, sole.z + along.z * reach) -
                           SnowHeight(heightmap, sole.x - along.x * reach, sole.z - along.z * reach)) / (2f * reach);
            float planeSlope = up.y > 0.2f ? -(up.x * along.x + up.z * along.z) / up.y : 0f;
            float change = (Mathf.Atan(slope) - Mathf.Atan(planeSlope)) * weight;
            if (Mathf.Abs(change) < 1e-4f)
            {
                return;
            }

            Vector3 forward = leg.Ski.forward;
            float pitch = Mathf.Atan2(forward.y, new Vector2(forward.x, forward.z).magnitude) + change;
            Vector3 wanted = along * Mathf.Cos(pitch) + Vector3.up * Mathf.Sin(pitch);
            leg.Foot.rotation = Quaternion.FromToRotation(forward, wanted) * leg.Foot.rotation;
        }

        // 4. POLE PLANTS: at each change of edges on the snow, the new turn's inside pole swings to a point ahead of and
        // beside the boot, stays on it while the skier passes, and swings back into the clip.
        private void Plants(in RiderState current, Heightmap heightmap, Transform root, float speed, bool onSnow, float dt)
        {
            var timing = new PolePlantSettings(_settings.PlantMinSpeed, _settings.PlantEdgeDegrees * Mathf.Deg2Rad,
                _settings.PlantReachSeconds, _settings.PlantHoldSeconds, _settings.PlantReleaseSeconds);
            bool canPlant = onSnow && current.Stance != Stance.Tuck && _settings.PlantWeight > 0f;
            _plant.Update(SimCoordinates.ToDouble(current.EdgeAngle), speed, canPlant, dt, timing);
            if (_plant.Side == 0)
            {
                _plantFixed = false;
                return;
            }

            Arm arm = _plant.Side > 0 ? _armRight : _armLeft;
            Leg leg = _plant.Side > 0 ? _legRight : _legLeft;
            float weight = (float)_plant.Weight(timing) * _settings.PlantWeight * _onSnow;
            if (arm == null || leg == null || heightmap == null || !(weight > 0f))
            {
                return;
            }

            Vector3 forward = Vector3.ProjectOnPlane(root.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 goal = leg.Sole.position + forward * _settings.PlantForward + right * (_plant.Side * _settings.PlantOut);
            goal.y = SnowHeight(heightmap, goal.x, goal.z);
            if (_plant.Planted(timing) && !_plantFixed)
            {
                _plantTip = goal;
                _plantFixed = true;
            }

            Vector3 tip = Vector3.Lerp(arm.Pole.position, _plantFixed ? _plantTip : goal, weight);
            Vector3 grip = arm.Grip.position;
            Vector3 gripTarget = tip + (grip - tip).normalized * PoleLength;
            Chain(arm.Upper, arm.Lower, arm.Hand, gripTarget + (arm.Hand.position - grip));
            Vector3 pole = arm.Pole.position - arm.Grip.position;
            Vector3 wanted = tip - arm.Grip.position;
            if (pole.sqrMagnitude > 1e-6f && wanted.sqrMagnitude > 1e-6f)
            {
                arm.Hand.rotation = Quaternion.FromToRotation(pole, wanted) * arm.Hand.rotation;
            }
        }

        // Turns a two-bone chain so its end reaches the target, bending in the plane the clip bent it in.
        private static void Chain(Transform upper, Transform lower, Transform end, Vector3 target)
        {
            Vector3 root = upper.position;
            Vector3 mid = lower.position;
            TwoBoneIk.Solve(ToPose(root), ToPose(mid), ToPose(end.position), ToPose(target), ToPose(mid),
                out PoseVector newMid, out PoseVector newEnd);
            upper.rotation = Quaternion.FromToRotation(mid - root, ToVector(newMid) - root) * upper.rotation;
            Vector3 midNow = lower.position;
            lower.rotation = Quaternion.FromToRotation(end.position - midNow, ToVector(newEnd) - midNow) * lower.rotation;
        }

        private void Reset()
        {
            _lean = default;
            _compression = default;
            _pitch = default;
            _sway = default;
            _plant = default;
            _plantFixed = false;
            _history = 0;
        }

        private static float Speed(Vec3 velocity)
        {
            float x = SimCoordinates.ToFloat(velocity.X);
            float y = SimCoordinates.ToFloat(velocity.Y);
            float z = SimCoordinates.ToFloat(velocity.Z);
            return Mathf.Sqrt(x * x + y * y + z * z);
        }

        // How fast the path turns, rad/s, positive to the right: the travel's heading across the last sim step.
        private static float TurnRate(Vec3 before, Vec3 after, float speed)
        {
            if (speed < 1f)
            {
                return 0f;
            }

            float h0 = Mathf.Atan2(SimCoordinates.ToFloat(before.X), SimCoordinates.ToFloat(before.Z));
            float h1 = Mathf.Atan2(SimCoordinates.ToFloat(after.X), SimCoordinates.ToFloat(after.Z));
            return Mathf.DeltaAngle(h0 * Mathf.Rad2Deg, h1 * Mathf.Rad2Deg) * Mathf.Deg2Rad * Simulation.StepsPerSecond;
        }

        // The snow's height at a Unity point (Unity and sim share their axes; see SimCoordinates).
        private static float SnowHeight(Heightmap heightmap, float x, float z)
        {
            heightmap.Sample(SimCoordinates.FromMetres(x), SimCoordinates.FromMetres(z), out Fix64 height, out _);
            return SimCoordinates.ToFloat(height);
        }

        private static PoseVector ToPose(Vector3 v) => new PoseVector(v.x, v.y, v.z);

        private static Vector3 ToVector(PoseVector v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);

        private static Transform Bone(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                {
                    return child;
                }
            }

            return null;
        }

        private sealed class Leg
        {
            public Transform Upper;
            public Transform Lower;
            public Transform Foot;
            public Transform Sole;
            public Transform Ski;
            public float Length;

            public static Leg Find(Transform model, string side, string socket)
            {
                var leg = new Leg
                {
                    Upper = Bone(model, "UpperLeg" + side), Lower = Bone(model, "LowerLeg" + side), Foot = Bone(model, "Foot" + side),
                    Sole = ViewHelpers.FindSocket(model, socket),
                };
                if (leg.Upper == null || leg.Lower == null || leg.Foot == null || leg.Sole == null)
                {
                    return null;
                }

                leg.Ski = leg.Sole.childCount > 0 ? leg.Sole.GetChild(0) : null;
                leg.Length = (leg.Lower.position - leg.Upper.position).magnitude + (leg.Foot.position - leg.Lower.position).magnitude;
                return leg;
            }
        }

        private sealed class Arm
        {
            public Transform Upper;
            public Transform Lower;
            public Transform Hand;
            public Transform Grip;
            public Transform Pole;

            public static Arm Find(Transform model, string side, string socket, Transform pole)
            {
                var arm = new Arm
                {
                    Upper = Bone(model, "UpperArm" + side), Lower = Bone(model, "LowerArm" + side), Hand = Bone(model, "Hand" + side),
                    Grip = ViewHelpers.FindSocket(model, socket), Pole = pole,
                };
                return arm.Upper == null || arm.Lower == null || arm.Hand == null || arm.Grip == null || arm.Pole == null ? null : arm;
            }
        }
    }
}
