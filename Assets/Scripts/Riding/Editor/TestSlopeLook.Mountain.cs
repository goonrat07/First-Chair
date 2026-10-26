using System;
using System.Collections.Generic;
using FirstChair.Sim;
using UnityEditor;
using UnityEngine;

namespace FirstChair.Riding.Editor
{
    /// <summary>
    /// The test mountain's lift and run markers (Story 010), built from the layout's <c>lift</c>, <c>tower</c> and
    /// <c>run</c> records. Primitives in the look's materials, like the rails and boxes; a Blender-built lift can replace
    /// them later. Presentation only: the sim never sees any of it.
    /// </summary>
    public static partial class TestSlopeLook
    {
        // The haul rope runs this far either side of the lift line, metres: up on the right (facing uphill), down on the left.
        private const float RopeSideOffset = 2.5f;

        // The rope's height above the snow at each station, metres: where riders get on and off.
        private const float StationRopeHeight = 5f;

        // About this much rope between chairs, metres.
        private const float RopePerChair = 22f;

        private const int WheelSteps = 6;

        // GMA2-style run markers: a pole either side every MarkerSpacing metres, MarkerHeight tall, in the run's colour.
        private const float MarkerSpacing = 30f;
        private const float MarkerHeight = 1.6f;

        /// <summary>
        /// Builds the layout's first chairlift: a post and crossarm at each tower, a bullwheel and a hut at each station,
        /// and the <see cref="ChairLift"/> that runs the chairs round the rope. Null when the layout has no lift.
        /// </summary>
        /// <param name="parent">Parent for the lift.</param>
        /// <param name="slope">The slope.</param>
        public static ChairLift Chairlift(Transform parent, TestSlopeData slope)
        {
            if (slope.Layout.Lifts.Count == 0)
            {
                return null;
            }

            SlopeLift lift = slope.Layout.Lifts[0];
            Material steel = LitMaterial("LiftSteel", "Universal Render Pipeline/Lit", new Color(0.42f, 0.44f, 0.48f), 0.5f, false);
            Material wood = LitMaterial("LiftHut", "Universal Render Pipeline/Lit", new Color(0.55f, 0.32f, 0.18f), 0.1f, false);
            Material roof = LitMaterial("LiftRoof", "Universal Render Pipeline/Lit", new Color(0.62f, 0.12f, 0.10f), 0.2f, false);
            Material chair = LitMaterial("LiftChair", "Universal Render Pipeline/Lit", new Color(0.13f, 0.18f, 0.30f), 0.3f, false);
            Material rope = LitMaterial("LiftRope", "Universal Render Pipeline/Lit", new Color(0.05f, 0.05f, 0.05f), 0.4f, false);

            Vector3 bottom = SnowPoint(slope, lift.BottomXMm, lift.BottomZMm);
            Vector3 top = SnowPoint(slope, lift.TopXMm, lift.TopZMm);
            Vector3 uphill = top - bottom;
            uphill.y = 0f;
            uphill.Normalize();
            var side = new Vector3(uphill.z, 0f, -uphill.x);
            var root = new GameObject("Chairlift");
            root.transform.SetParent(parent, false);

            var towerTops = new List<Vector3>();
            foreach (SlopeLiftTower tower in lift.Towers)
            {
                Vector3 foot = SnowPoint(slope, tower.XMm, tower.ZMm);
                float height = tower.HeightMm / 1000f;
                Transform post = Primitive(PrimitiveType.Cylinder, root.transform, steel);
                post.name = "Tower";
                post.position = foot + Vector3.up * (height * 0.5f - 0.5f);
                post.localScale = new Vector3(0.7f, height * 0.5f + 0.5f, 0.7f);
                Transform arm = Primitive(PrimitiveType.Cube, root.transform, steel);
                arm.name = "Crossarm";
                arm.SetPositionAndRotation(foot + Vector3.up * (height - 0.35f), Quaternion.LookRotation(uphill));
                arm.localScale = new Vector3(2f * RopeSideOffset + 1f, 0.3f, 0.4f);
                towerTops.Add(foot + Vector3.up * height);
            }

            Station(root.transform, "BottomStation", bottom, side, steel, wood, roof);
            Station(root.transform, "TopStation", top, side, steel, wood, roof);

            // The rope's loop: up the right side, round the top wheel on its uphill side, down the left, round the bottom
            // wheel on its downhill side. Riders get on at its first point and off where it reaches the top wheel.
            Vector3 bottomWheel = bottom + Vector3.up * StationRopeHeight;
            Vector3 topWheel = top + Vector3.up * StationRopeHeight;
            var points = new List<Vector3> { bottomWheel + side * RopeSideOffset };
            foreach (Vector3 towerTop in towerTops)
            {
                points.Add(towerTop + side * RopeSideOffset);
            }

            int unload = points.Count;
            points.Add(topWheel + side * RopeSideOffset);
            for (int k = 1; k < WheelSteps; k++)
            {
                float angle = Mathf.PI * k / WheelSteps;
                points.Add(topWheel + (side * Mathf.Cos(angle) + uphill * Mathf.Sin(angle)) * RopeSideOffset);
            }

            points.Add(topWheel - side * RopeSideOffset);
            for (int i = towerTops.Count - 1; i >= 0; i--)
            {
                points.Add(towerTops[i] - side * RopeSideOffset);
            }

            points.Add(bottomWheel - side * RopeSideOffset);
            for (int k = 1; k < WheelSteps; k++)
            {
                float angle = Mathf.PI * k / WheelSteps;
                points.Add(bottomWheel - (side * Mathf.Cos(angle) + uphill * Mathf.Sin(angle)) * RopeSideOffset);
            }

            float length = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                length += Vector3.Distance(points[i], points[(i + 1) % points.Count]);
            }

            var chairLift = root.AddComponent<ChairLift>();
            var serialized = new SerializedObject(chairLift);
            SerializedProperty ropePoints = serialized.FindProperty("rope");
            ropePoints.arraySize = points.Count;
            for (int i = 0; i < points.Count; i++)
            {
                ropePoints.GetArrayElementAtIndex(i).vector3Value = points[i];
            }

            serialized.FindProperty("boardingPoint").intValue = 0;
            serialized.FindProperty("unloadPoint").intValue = unload;
            serialized.FindProperty("chairs").intValue = Mathf.Max(2, Mathf.RoundToInt(length / RopePerChair));
            serialized.FindProperty("chairMaterial").objectReferenceValue = chair;
            serialized.FindProperty("ropeMaterial").objectReferenceValue = rope;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return chairLift;
        }

        /// <summary>
        /// Stands GMA2-style markers along each run: a pole in the run's colour either side every
        /// <see cref="MarkerSpacing"/> metres, and a sign at its top.
        /// </summary>
        /// <param name="parent">Parent for the markers.</param>
        /// <param name="slope">The slope.</param>
        public static void RunMarkers(Transform parent, TestSlopeData slope)
        {
            if (slope.Layout.Runs.Count == 0)
            {
                return;
            }

            var root = new GameObject("RunMarkers");
            root.transform.SetParent(parent, false);
            Material post = LitMaterial("SignPost", "Universal Render Pipeline/Lit", new Color(0.45f, 0.30f, 0.18f), 0.1f, false);
            var xs = new List<double>();
            var zs = new List<double>();
            foreach (SlopeRun run in slope.Layout.Runs)
            {
                Material colour = LitMaterial("Run" + run.Difficulty, "Universal Render Pipeline/Lit", RunColour(run.Difficulty), 0.3f, false);
                RunLine.Sample(run.Points, 1.0, xs, zs);
                float half = run.WidthMm / 2000f + 0.5f;
                double since = MarkerSpacing;
                for (int i = 1; i < xs.Count; i++)
                {
                    double dx = xs[i] - xs[i - 1], dz = zs[i] - zs[i - 1];
                    double step = Math.Sqrt(dx * dx + dz * dz);
                    since += step;
                    if (since < MarkerSpacing || step <= 0.0)
                    {
                        continue;
                    }

                    since = 0.0;
                    var right = new Vector3((float)(dz / step), 0f, (float)(-dx / step));
                    foreach (float sign in new[] { -1f, 1f })
                    {
                        Vector3 at = new Vector3((float)xs[i], 0f, (float)zs[i]) + right * (half * sign);
                        Transform pole = Primitive(PrimitiveType.Cylinder, root.transform, colour);
                        pole.name = "Marker";
                        pole.position = SnowPoint(slope, Mathf.RoundToInt(at.x * 1000f), Mathf.RoundToInt(at.z * 1000f)) + Vector3.up * (MarkerHeight * 0.5f);
                        pole.localScale = new Vector3(0.08f, MarkerHeight * 0.5f, 0.08f);
                    }
                }

                // The sign: a post at the run's top, to its right, with a board in the run's colour.
                double sx = xs[1] - xs[0], sz = zs[1] - zs[0];
                double length = Math.Sqrt(sx * sx + sz * sz);
                var signRight = new Vector3((float)(sz / length), 0f, (float)(-sx / length));
                Vector3 spot = new Vector3((float)xs[0], 0f, (float)zs[0]) + signRight * (half + 2f);
                Vector3 foot = SnowPoint(slope, Mathf.RoundToInt(spot.x * 1000f), Mathf.RoundToInt(spot.z * 1000f));
                var facing = Quaternion.LookRotation(new Vector3((float)(-sx / length), 0f, (float)(-sz / length)));
                Transform signPost = Primitive(PrimitiveType.Cylinder, root.transform, post);
                signPost.name = "Sign_" + run.Name;
                signPost.position = foot + Vector3.up * 1.2f;
                signPost.localScale = new Vector3(0.12f, 1.2f, 0.12f);
                Transform board = Primitive(PrimitiveType.Cube, root.transform, colour);
                board.name = "SignBoard_" + run.Name;
                board.SetPositionAndRotation(foot + Vector3.up * 2.2f, facing);
                board.localScale = new Vector3(1.4f, 0.9f, 0.08f);
            }
        }

        private static void Station(Transform parent, string name, Vector3 ground, Vector3 side, Material steel, Material wood, Material roof)
        {
            var station = new GameObject(name).transform;
            station.SetParent(parent, false);
            Transform post = Primitive(PrimitiveType.Cylinder, station, steel);
            post.position = ground + Vector3.up * (StationRopeHeight * 0.5f);
            post.localScale = new Vector3(0.8f, StationRopeHeight * 0.5f, 0.8f);
            Transform wheel = Primitive(PrimitiveType.Cylinder, station, steel);
            wheel.position = ground + Vector3.up * StationRopeHeight;
            wheel.localScale = new Vector3(2f * RopeSideOffset + 0.4f, 0.08f, 2f * RopeSideOffset + 0.4f);
            Transform hut = Primitive(PrimitiveType.Cube, station, wood);
            hut.SetPositionAndRotation(ground - side * 9f + Vector3.up * 1.5f, Quaternion.LookRotation(side));
            hut.localScale = new Vector3(5f, 3f, 5f);
            Transform hutRoof = Primitive(PrimitiveType.Cube, station, roof);
            hutRoof.SetPositionAndRotation(ground - side * 9f + Vector3.up * 3.2f, Quaternion.LookRotation(side));
            hutRoof.localScale = new Vector3(5.8f, 0.4f, 5.8f);
        }

        private static Color RunColour(RunDifficulty difficulty)
        {
            switch (difficulty)
            {
                case RunDifficulty.Green:
                    return new Color(0.16f, 0.62f, 0.25f);
                case RunDifficulty.Blue:
                    return new Color(0.12f, 0.36f, 0.85f);
                case RunDifficulty.Red:
                    return new Color(0.85f, 0.12f, 0.12f);
                case RunDifficulty.Black:
                    return new Color(0.06f, 0.06f, 0.07f);
                default:
                    return new Color(1.0f, 0.55f, 0.05f);
            }
        }
    }
}
