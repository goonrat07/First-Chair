using FirstChair.Sim;
using UnityEngine;

namespace FirstChair.Riding
{
    /// <summary>Helpers the skier views share: finding sockets, attaching props at them, and the snow's normal.</summary>
    internal static class ViewHelpers
    {
        /// <summary>The first transform under <paramref name="root"/> whose name starts with <paramref name="socketName"/>.</summary>
        /// <param name="root">Where to search, itself included.</param>
        /// <param name="socketName">A socket name such as <c>SOCKET_boot</c>; importers may add suffixes.</param>
        public static Transform FindSocket(Transform root, string socketName)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.StartsWith(socketName, System.StringComparison.Ordinal))
                {
                    return child;
                }
            }

            return null;
        }

        /// <summary>
        /// Instantiates a prop under <paramref name="parent"/> so the prop's own socket lands on the parent's origin,
        /// with the parent's axes. If the prop has no such socket, <paramref name="fallback"/> (the socket's position in
        /// the prop's model space, from its asset-manifest.json) is used, and a warning names the prop.
        /// </summary>
        /// <param name="prop">The prop's model, such as PR_Ski.fbx.</param>
        /// <param name="parent">The attachment point.</param>
        /// <param name="propSocket">The prop's socket name, such as <c>SOCKET_boot</c>.</param>
        /// <param name="fallback">The socket's model-space position if the node is missing.</param>
        /// <param name="context">The component to name in a warning.</param>
        public static Transform Attach(GameObject prop, Transform parent, string propSocket, Vector3 fallback, Object context)
        {
            Transform instance = Object.Instantiate(prop, parent, false).transform;
            instance.localRotation = Quaternion.identity;
            instance.localPosition = Vector3.zero;
            Transform socket = FindSocket(instance, propSocket);
            Vector3 offset = socket != null ? instance.InverseTransformPoint(socket.position) : fallback;
            if (socket == null)
            {
                Debug.LogWarning(context.GetType().Name + ": " + prop.name + " has no " + propSocket + " node; using the manifest fallback.", context);
            }

            instance.localPosition = -Vector3.Scale(instance.localScale, offset);
            return instance;
        }

        /// <summary>
        /// How far a ski's tail and tip reach behind and ahead of its origin, metres, along its +Z (the way <c>PR_Ski</c>'s
        /// tip faces), from its meshes' bounds; both zero for a ski with no mesh. Measured once, when the ski is attached.
        /// </summary>
        /// <param name="ski">The ski's root, as attached.</param>
        /// <param name="tailBehind">The tail's distance behind the origin.</param>
        /// <param name="tipAhead">The tip's distance ahead of it.</param>
        public static void SkiEnds(Transform ski, out float tailBehind, out float tipAhead)
        {
            tailBehind = 0f;
            tipAhead = 0f;
            foreach (MeshFilter filter in ski.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null)
                {
                    continue;
                }

                Bounds bounds = filter.sharedMesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    var sign = new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f);
                    Vector3 point = filter.transform.TransformPoint(bounds.center + Vector3.Scale(bounds.extents, sign));
                    float along = Vector3.Dot(point - ski.position, ski.forward);
                    tailBehind = Mathf.Max(tailBehind, -along);
                    tipAhead = Mathf.Max(tipAhead, along);
                }
            }
        }

        /// <summary>The snow's unit normal under the rider, between the last two sim states; straight up in the air.</summary>
        /// <param name="session">The ride.</param>
        /// <param name="alpha">The interpolation factor between the previous and current state.</param>
        public static Vector3 SnowNormal(RideSession session, float alpha)
        {
            RiderState current = session.Current;
            if (current.Airborne)
            {
                return Vector3.up;
            }

            RiderState previous = session.Previous;
            Heightmap heightmap = session.Slope.Heightmap;
            heightmap.Sample(previous.Position.X, previous.Position.Z, out _, out Vec3 before);
            heightmap.Sample(current.Position.X, current.Position.Z, out _, out Vec3 after);
            return Vector3.Lerp(SimCoordinatesUnity.ToVector3(before), SimCoordinatesUnity.ToVector3(after), alpha).normalized;
        }
    }
}
