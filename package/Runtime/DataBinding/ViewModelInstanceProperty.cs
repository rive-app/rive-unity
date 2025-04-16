using System;
using System.Collections.Concurrent;

namespace Rive
{
    /// <summary>
    /// Base class representing any value that can be a property of a ViewModelInstance.
    /// This includes both primitive property values and nested view model instances.
    /// </summary>
    public abstract class ViewModelInstanceProperty
    {
        /// <summary>
        /// Cache of property instances to avoid creating multiple instances for the same native property
        /// </summary>
        private static readonly ConcurrentDictionary<IntPtr, WeakReference<ViewModelInstanceProperty>> s_propertiesCache =
            new ConcurrentDictionary<IntPtr, WeakReference<ViewModelInstanceProperty>>();

        /// <summary>
        /// Adds a globally cached view model property for a given pointer.
        /// </summary>
        /// <param name="ptr"> The pointer to the property.</param>
        /// <param name="property"> The C# property to cache.</param>
        internal static void AddGloballyCachedVMPropertyForPointer(IntPtr ptr, ViewModelInstanceProperty property)
        {
            s_propertiesCache[ptr] = new WeakReference<ViewModelInstanceProperty>(property);
        }

        /// <summary>
        /// Tries to get a globally cached view model property for a given pointer.
        /// </summary>
        /// <param name="ptr"> The pointer to the property.</param>
        /// <param name="property"> The C# property to cache.</param>
        /// <returns></returns>
        internal static bool TryGetGloballyCachedVMPropertyForPointer(IntPtr ptr, out ViewModelInstanceProperty property)
        {
            if (s_propertiesCache.TryGetValue(ptr, out var weakReference) &&
                weakReference.TryGetTarget(out property))
            {
                return true;
            }

            property = null;
            return false;
        }

        /// <summary>
        /// Removes a cached view model property for a given pointer.
        /// /// </summary>
        /// <param name="ptr"> The pointer to the property.</param>
        internal static void RemoveCachedPropertyForPointer(IntPtr ptr)
        {
            s_propertiesCache.TryRemove(ptr, out _);
        }
    }
}
