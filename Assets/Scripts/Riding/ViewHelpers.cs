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
