using UnityEngine;

namespace Rive.Utils
{
        /// <summary>
        /// Version-safe helpers for working with Unity objects across Unity versions.
        /// </summary>
        internal static class ObjectHelper
        {
                /// <summary>
                /// Returns a stable 64-bit identifier for a Unity object
                /// </summary>
                /// <remarks>
                /// On Unity 6.4+, GetInstanceID() is obsolete in favor of the 64-bit EntityId.
                /// </remarks>
                public static long GetInstanceId(Object obj)
                {
#if UNITY_6000_4_OR_NEWER
            return (long)EntityId.ToULong(obj.GetEntityId());
#else
                        return obj.GetInstanceID();
#endif
                }

                /// <summary>
                /// Returns any active loaded object of the given type, or null if none exists.
                /// </summary>
                /// <remarks>
                /// FindObjectOfType/FindFirstObjectByType are deprecated on newer Unity versions because
                /// they rely on instance ID ordering, so we use FindAnyObjectByType where available.
                /// </remarks>
                public static T FindAny<T>() where T : Object
                {
#if UNITY_2022_2_OR_NEWER
                        return Object.FindAnyObjectByType<T>();
#else
                        return Object.FindObjectOfType<T>();
#endif
                }

                /// <summary>
                /// Returns all active loaded objects of the given type.
                /// </summary>
                public static T[] FindAll<T>() where T : Object
                {
#if UNITY_6000_4_OR_NEWER
            // 6.4 deprecated the FindObjectsSortMode overload (sort order can't be kept once InstanceID becomes EntityId).
            return Object.FindObjectsByType<T>();
#elif UNITY_2022_2_OR_NEWER
                        return Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#else
                        return Object.FindObjectsOfType<T>();
#endif
                }
        }
}
