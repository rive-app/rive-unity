using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Rive.Utils;

namespace Rive
{
    /// <summary>
    /// Represents a runtime instance of a view model with mutable property values.
    /// A ViewModelInstance contains the same properties as its source view model but maintains its own state that can change during execution.
    /// </summary>
    public sealed class ViewModelInstance : ViewModelInstanceProperty, IDisposable
    {
        private ViewModelInstanceSafeHandle m_safeHandle;

        private ViewModelInstance m_parentInstance;

        private WeakReference<File> m_riveFile;

        private readonly List<ViewModelInstancePrimitiveProperty> m_subscribedProperties = new List<ViewModelInstancePrimitiveProperty>();

        private readonly List<WeakReference<ViewModelInstance>> m_parents = new List<WeakReference<ViewModelInstance>>(); private readonly List<ViewModelInstance> m_children = new List<ViewModelInstance>();

        // caching nested view model instances by name
        private readonly Dictionary<string, ViewModelInstance> m_viewModelInstances = new Dictionary<string, ViewModelInstance>();

        private const char kPathSeparator = '/';

        /// <summary>
        /// Cache for split paths to avoid repeated string operations
        /// </summary>
        private static readonly ConcurrentDictionary<string, string[]> s_pathSegmentsCache = new ConcurrentDictionary<string, string[]>();


        private bool m_disposed = false;

        private string m_viewModelName = null;


        internal File RiveFile
        {
            get
            {
                if (m_riveFile != null && m_riveFile.TryGetTarget(out var file))
                {
                    return file;
                }
                return null;
            }
        }

        internal ViewModelInstanceSafeHandle NativeSafeHandle => m_safeHandle;

        internal string ViewModelName
        {
            get
            {
                if (m_viewModelName == null && !m_disposed)
                {
                    m_viewModelName = Marshal.PtrToStringAnsi(getViewModelNameFromViewModelInstance(NativeSafeHandle));
                }

                return m_viewModelName;
            }
        }




        internal ViewModelInstance(IntPtr instanceValue, File riveFile, ViewModelInstance parentInstance = null)
        {
            m_safeHandle = new ViewModelInstanceSafeHandle(instanceValue);

            m_parentInstance = parentInstance;
            m_riveFile = new WeakReference<File>(riveFile);


            if (parentInstance != null)
            {
                AddParent(parentInstance);
            }
        }

        ~ViewModelInstance()
        {
            Dispose(false);
        }

        private static string[] GetPathSegments(string path)
        {
            // For very frequent calls, caching the split results can improve performance
            // If the user tries to get all the properties of a view model instance, this can be called a lot
            // We cache the split results to avoid repeated string operations
            if (!s_pathSegmentsCache.TryGetValue(path, out var segments))
            {
                segments = path.Split(kPathSeparator);
                s_pathSegmentsCache[path] = segments;
            }

            return segments;
        }


        private T GetPropertyFromPathSegments<T>(string[] pathSegments, int index) where T : ViewModelInstanceProperty
        {
            if (index < pathSegments.Length - 1)
            {
                // We need to navigate to a nested view model instance so we can propagate callbacks
                var nestedInstance = GetInternalViewModelInstance(pathSegments[index]);
                if (nestedInstance != null)
                {
                    return nestedInstance.GetPropertyFromPathSegments<T>(pathSegments, index + 1);
                }
                else
                {
                    return null;
                }
            }

            // We're at the final segment, get the property directly
            return ViewModelInstancePropertyHandlersFactory.GetPrimitiveProperty<T>(this, pathSegments[index]);
        }

        private ViewModelInstance GetViewModelInstanceFromPathSegments(string[] pathSegments, int index)
        {
            if (index >= pathSegments.Length)
            {
                return this;
            }

            var viewModelInstance = GetInternalViewModelInstance(pathSegments[index]);
            if (viewModelInstance != null)
            {
                if (index == pathSegments.Length - 1)
                {
                    return viewModelInstance;
                }
                else
                {
                    return viewModelInstance.GetViewModelInstanceFromPathSegments(pathSegments, index + 1);
                }
            }

            return null;
        }


        private bool HasParent(ViewModelInstance parent)
        {
            for (int i = 0; i < m_parents.Count; i++)
            {
                if (m_parents[i].TryGetTarget(out var existingParent) && existingParent == parent)
                {
                    return true;
                }
            }
            return false;
        }

        private ViewModelInstance GetInternalViewModelInstance(string name)
        {
            if (m_viewModelInstances.TryGetValue(name, out var instance))
            {
                return instance;
            }

            // Otherwise, create and cache it
            var ptr = getViewModelInstanceViewModelProperty(NativeSafeHandle, name);
            if (ptr != IntPtr.Zero)
            {
                if (TryGetCachedViewModelInstanceForPointer(ptr, out var cachedInstance))
                {
                    // If we have already created this instance for this pointer, use it
                    m_viewModelInstances[name] = cachedInstance;

                    // Let's make sure the parent relationship is set

                    if (!cachedInstance.HasParent(this))
                    {
                        cachedInstance.AddParent(this);
                    }

                    return cachedInstance;
                }

                var newInstance = new ViewModelInstance(ptr, RiveFile, this);
                m_viewModelInstances[name] = newInstance;
                ViewModelInstanceProperty.AddGloballyCachedVMPropertyForPointer(ptr, newInstance);

                return newInstance;
            }

            return null;
        }

        /// <summary>
        /// Gets a nested view model instance property.
        /// </summary>
        /// <param name="path"> The path to the nested property. If the property is on the current instance, the path is the property name. </param>
        /// <returns> The nested view model instance property. </returns>
        private ViewModelInstance GetNestedViewModelInstance(string path)
        {
            // Fast path for simple names (no path separator)
            if (!path.Contains(kPathSeparator))
            {
                return GetInternalViewModelInstance(path);
            }

            string[] pathSegments = GetPathSegments(path);
            return GetViewModelInstanceFromPathSegments(pathSegments, 0);
        }


        /// <summary>
        /// Replaces a nested view model instance property with a new instance.
        /// </summary>
        /// <param name="name"> The name of the property to replace.</param>
        /// <param name="value"> The new view model instance to replace the property with.</param>
        /// <returns> True if the view model property was replaced, false otherwise. E.g. If the view model instance provided is for a different view model, the replacement will fail.</returns>
        private bool InternalReplaceViewModel(string name, ViewModelInstance value)
        {
            if (value == null || value.NativeSafeHandle.IsInvalid)
            {
                return false;
            }

            bool result = replaceViewModelInstanceViewModelProperty(NativeSafeHandle, name, value.NativeSafeHandle);

            if (result)
            {

                // Clean up the old instance if it exists
                if (m_viewModelInstances.TryGetValue(name, out var oldInstance))
                {
                    oldInstance.RemoveParent(this);

                    // Remove from children list if present
                    if (m_children.Contains(oldInstance))
                    {
                        m_children.Remove(oldInstance);
                    }

                }

                m_viewModelInstances[name] = value;

                value.AddParent(this);
            }

            return result;
        }

        private void ClearCallbacks()
        {
            // We're iterating backwards to safely remove items while iterating
            for (int i = m_subscribedProperties.Count - 1; i >= 0; i--)
            {
                var property = m_subscribedProperties[i];
                property.ClearAllCallbacks();
            }
            m_subscribedProperties.Clear();
        }

        internal void AddParent(ViewModelInstance parent)
        {
            // Check if parent already exists
            if (HasParent(parent))
            {
                return;
            }

            m_parents.Add(new WeakReference<ViewModelInstance>(parent));

            // If we have properties or children with callbacks, notify parent
            if (m_subscribedProperties.Count > 0 || m_children.Count > 0)
            {
                parent.AddChildToCallbacks(this);
            }
        }

        internal void RemoveParent(ViewModelInstance parent)
        {
            for (int i = m_parents.Count - 1; i >= 0; i--)
            {
                if (m_parents[i].TryGetTarget(out var existingParent) && existingParent == parent)
                {
                    parent.RemoveChildFromCallbacks(this);
                    m_parents.RemoveAt(i);
                    return;
                }
            }
        }

        internal void AddChildToCallbacks(ViewModelInstance child)
        {
            if (!m_children.Contains(child))
            {
                m_children.Add(child);

                // Propagate up to parents
                foreach (var parent in m_parents)
                {
                    if (parent.TryGetTarget(out var parentInstance) && parentInstance != null)
                    {

                        parentInstance.AddChildToCallbacks(this);

                    }
                }
            }
        }

        internal void RemoveChildFromCallbacks(ViewModelInstance child)
        {
            m_children.Remove(child);

            // If no more children or properties need callbacks, notify parents
            if (m_children.Count == 0 && m_subscribedProperties.Count == 0)
            {
                foreach (var parent in m_parents)
                {
                    if (parent.TryGetTarget(out var parentInstance) && parentInstance != null)
                    {
                        parentInstance.RemoveChildFromCallbacks(this);
                    }
                }
            }
        }

        /// <summary>
        /// Called by a property when the user subscribes to it for callbacks.
        /// This is used to notify parents that they need to subscribe to this property for callbacks as well.
        /// </summary>
        internal void RegisterPropertyForCallbacks(ViewModelInstancePrimitiveProperty property)
        {
            if (!m_subscribedProperties.Contains(property))
            {
                m_subscribedProperties.Add(property);

                // If this is our first property with callbacks, notify parents
                if (m_subscribedProperties.Count == 1)
                {
                    foreach (var parent in m_parents)
                    {
                        if (parent.TryGetTarget(out var parentInstance) && parentInstance != null)
                        {
                            parentInstance.AddChildToCallbacks(this);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Called by a property when it transitions from non-zero to zero subscribers.
        /// </summary>
        internal void UnregisterPropertyForCallbacks(ViewModelInstancePrimitiveProperty property)
        {
            m_subscribedProperties.Remove(property);

            // If no more properties with callbacks and no children with callbacks, notify parents
            if (m_subscribedProperties.Count == 0 && m_children.Count == 0)
            {
                foreach (var parent in m_parents)
                {
                    if (parent.TryGetTarget(out var parentInstance) && parentInstance != null)
                    {
                        parentInstance.RemoveChildFromCallbacks(this);
                    }
                }
            }
        }


        /// <summary>
        /// Gets a cached nested view model instance for a given pointer. This is used to avoid creating multiple C# instances of the same underlying native instance.
        /// </summary>
        internal static bool TryGetCachedViewModelInstanceForPointer(IntPtr ptr, out ViewModelInstance instance)
        {
            if (ViewModelInstanceProperty.TryGetGloballyCachedVMPropertyForPointer(ptr, out var property))
            {
                instance = property as ViewModelInstance;

                if (instance != null)
                {
                    return true;
                }
                return false;
            }

            instance = null;
            return false;
        }

        /// <summary>
        /// Removes a cached view model instance for a given pointer.
        /// /// </summary>
        internal static void RemoveGloballyCachedViewModelInstanceForPointer(IntPtr ptr)
        {
            ViewModelInstanceProperty.RemoveCachedPropertyForPointer(ptr);
        }


        /// <summary>
        /// Adds a cached view model instance for a given pointer. This is used to avoid creating multiple C# instances of the same underlying native instance.
        /// </summary>
        /// <param name="ptr"></param>
        /// <param name="instance"></param>
        internal static void AddCachedViewModelInstanceForPointer(IntPtr ptr, ViewModelInstance instance)
        {
            ViewModelInstanceProperty.AddGloballyCachedVMPropertyForPointer(ptr, instance);
        }


        /// <summary>
        /// Gets a property of the view model instance.
        /// </summary>
        /// <typeparam name="T"> The type of the property to get. </typeparam>
        /// <param name="path"> The path to the property. If the property is on the current instance, the path is the property name. </param>
        /// <remarks> The path can be a nested path, e.g. "nestedInstance/propertyName". </remarks>
        /// <returns> The property of the view model instance. </returns>
        public T GetProperty<T>(string path) where T : ViewModelInstanceProperty
        {
            if (string.IsNullOrEmpty(path))
            {
                DebugLogger.Instance.LogError("Property path cannot be null or empty");
                return null;
            }

            if (m_disposed)
            {
                DebugLogger.Instance.LogError("Cannot get property from disposed ViewModelInstance");
                return null;
            }

            // Handle ViewModelInstance type specially since we do a few things differently for non-primitive properties
            if (typeof(T) == typeof(ViewModelInstance))
            {
                return GetNestedViewModelInstance(path) as T;
            }

            // Fast path for simple property names (no nested path separator)
            if (!path.Contains(kPathSeparator))
            {
                return ViewModelInstancePropertyHandlersFactory.GetPrimitiveProperty<T>(this, path);
            }

            string[] pathSegments = GetPathSegments(path);
            return GetPropertyFromPathSegments<T>(pathSegments, 0);
        }

        /// <summary>
        /// Detects property value changes
        /// Call this after advancing wherever you handle your per-frame logic.
        /// </summary>
        public void HandleCallbacks()
        {

            // First trigger callbacks for all properties that have changed on the current instance
            for (int i = 0; i < m_subscribedProperties.Count; i++)
            {
                var prop = m_subscribedProperties[i];
                if (prop.HasChanged)
                {
                    prop.RaiseChangedEvent();
                }
            }

            // Then clear changes for all properties so we can detect changes in the next frame
            for (int i = 0; i < m_subscribedProperties.Count; i++)
            {
                var prop = m_subscribedProperties[i];
                if (prop.HasChanged)
                {
                    prop.ClearChanges();
                }
            }

            // Propagate to all children
            foreach (var child in m_children)
            {
                child.HandleCallbacks();
            }

        }

        /// <summary>
        /// Replaces a nested view model instance property with a new instance.
        /// </summary>
        /// <param name="path">The path to the property to replace.</param>
        /// <param name="newInstance">The new instance to replace the property with.</param>
        public void SetViewModelInstance(string path, ViewModelInstance newInstance)
        {
            if (string.IsNullOrEmpty(path))
            {
                DebugLogger.Instance.LogError("Property path cannot be null or empty");
                return;
            }

            bool wasReplaced = false;
            // Fast path for simple names (no path separator)
            if (!path.Contains(kPathSeparator))
            {
                wasReplaced = InternalReplaceViewModel(path, newInstance);

                if (!wasReplaced)
                {
                    DebugLogger.Instance.LogError($"Failed to replace nested view model instance property at path: {path}. The property may not exist or the new instance may be of a different view model type.");
                }
                return;
            }

            string[] pathSegments = GetPathSegments(path);

            ViewModelInstance currentViewModel = this;

            // Navigate to the parent of the target instance (all segments except the last)
            for (int i = 0; i < pathSegments.Length - 1; i++)
            {
                currentViewModel = currentViewModel.GetInternalViewModelInstance(pathSegments[i]);
                if (currentViewModel == null)
                {
                    DebugLogger.Instance.LogError($"View model not found at segment '{pathSegments[i]}' in path: {path}");
                    return;
                }
            }

            // Now currentViewModel is the parent of our target, so lets replace the final segment
            wasReplaced = currentViewModel.InternalReplaceViewModel(
               pathSegments[pathSegments.Length - 1],
               newInstance);

            if (!wasReplaced)
            {
                DebugLogger.Instance.LogError($"Failed to replace nested view model instance property at path: {path}. The property may not exist or the new instance may be of a different view model type.");
            }


        }


        #region  Convenience methods
        /// <summary>
        /// Gets a number property of the view model instance.
        /// </summary>
        /// <param name="path">The path to the property.</param>
        /// <returns>The number property, or null if the property doesn't exist or is not a number.</returns>
        public ViewModelInstanceNumberProperty GetNumberProperty(string path)
        {
            return GetProperty<ViewModelInstanceNumberProperty>(path);
        }

        /// <summary>
        /// Gets a boolean property of the view model instance.
        /// </summary>
        /// <param name="path">The path to the property.</param>
        /// <returns>The boolean property, or null if the property doesn't exist or is not a boolean.</returns>
        public ViewModelInstanceBooleanProperty GetBooleanProperty(string path)
        {
            return GetProperty<ViewModelInstanceBooleanProperty>(path);
        }

        /// <summary>
        /// Gets a string property of the view model instance.
        /// </summary>
        /// <param name="path">The path to the property.</param>
        /// <returns>The string property, or null if the property doesn't exist or is not a string.</returns>
        public ViewModelInstanceStringProperty GetStringProperty(string path)
        {
            return GetProperty<ViewModelInstanceStringProperty>(path);
        }

        /// <summary>
        /// Gets a color property of the view model instance.
        /// </summary>
        /// <param name="path">The path to the property.</param>
        /// <returns>The color property, or null if the property doesn't exist or is not a color.</returns>
        public ViewModelInstanceColorProperty GetColorProperty(string path)
        {
            return GetProperty<ViewModelInstanceColorProperty>(path);
        }

        /// <summary>
        /// Gets an enum property of the view model instance.
        /// </summary>
        /// <param name="path">The path to the property.</param>
        /// <returns>The enum property, or null if the property doesn't exist or is not an enum.</returns>
        public ViewModelInstanceEnumProperty GetEnumProperty(string path)
        {
            return GetProperty<ViewModelInstanceEnumProperty>(path);
        }

        /// <summary>
        /// Gets a trigger property of the view model instance.
        /// </summary>
        /// <param name="path">The path to the property.</param>
        /// <returns>The trigger property, or null if the property doesn't exist or is not a trigger.</returns>
        public ViewModelInstanceTriggerProperty GetTriggerProperty(string path)
        {
            return GetProperty<ViewModelInstanceTriggerProperty>(path);
        }

        /// <summary>
        /// Gets an image property of the view model instance.
        /// </summary>
        /// <param name="path">The path to the property.</param>
        /// <returns>The image property, or null if the property doesn't exist or is not an image.</returns>
        public ViewModelInstanceImageProperty GetImageProperty(string path)
        {
            return GetProperty<ViewModelInstanceImageProperty>(path);
        }

        /// <summary>
        /// Gets a list property of the view model instance.
        /// </summary>
        /// <param name="path">The path to the property.</param>
        /// <returns>The list property, or null if the property doesn't exist or is not a list.</returns>
        public ViewModelInstanceListProperty GetListProperty(string path)
        {
            return GetProperty<ViewModelInstanceListProperty>(path);
        }

        /// <summary>
        /// Gets an artboard property of the view model instance.
        /// </summary>
        /// <param name="path">The path to the property.</param>
        /// <returns>The artboard property, or null if the property doesn't exist or is not an artboard.</returns>
        public ViewModelInstanceArtboardProperty GetArtboardProperty(string path)
        {
            return GetProperty<ViewModelInstanceArtboardProperty>(path);
        }

        /// <summary>
        /// Gets a nested view model instance property.
        /// </summary>
        /// <param name="path">The path to the property.</param>
        /// <returns>The nested view model instance, or null if the property doesn't exist or is not a view model.</returns>
        public ViewModelInstance GetViewModelInstanceProperty(string path)
        {
            return GetProperty<ViewModelInstance>(path);
        }
        #endregion

        private void Dispose(bool disposing)
        {

            if (m_disposed)
            {
                return;
            }

            ClearCallbacks();

            foreach (var kvp in m_viewModelInstances)
            {
                var childViewModelInstance = kvp.Value;
                childViewModelInstance.RemoveParent(this);
            }

            m_viewModelInstances.Clear();

            for (int i = m_parents.Count - 1; i >= 0; i--)
            {
                if (m_parents[i].TryGetTarget(out var parentInstance) && parentInstance != null)
                {
                    RemoveParent(parentInstance);

                }
            }

            m_children.Clear();



            if (m_safeHandle != null && !m_safeHandle.IsInvalid)
            {
                // Get the IntPtr for cache removal before disposing
                IntPtr nativePtr = m_safeHandle.DangerousGetHandle();
                m_safeHandle.Dispose();

                RemoveGloballyCachedViewModelInstanceForPointer(nativePtr);

            }

            m_disposed = true;

            if (disposing)
            {
                GC.SuppressFinalize(this);
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }




        #region Native Calls


        [DllImport(NativeLibrary.name)]
        private static extern nuint getViewModelInstancePropertyCount(ViewModelInstanceSafeHandle instanceValue);

        [DllImport(NativeLibrary.name)]
        private static extern IntPtr getViewModelInstancePropertyAtPath(ViewModelInstanceSafeHandle instanceValue, string path);




        [DllImport(NativeLibrary.name)]
        private static extern IntPtr getViewModelInstanceViewModelProperty(ViewModelInstanceSafeHandle instanceValue, string path);




        [DllImport(NativeLibrary.name)]
        private static extern IntPtr getViewModelNameFromViewModelInstance(ViewModelInstanceSafeHandle instanceValue);


        /// <summary>
        /// Replaces a nested view model instance property with a new instance.
        /// </summary>
        /// <param name="baseInstanceValue">The instance that contains the property to replace.</param>
        /// <param name="path">The path to the property to replace.</param>
        /// <param name="newInstance">The new instance to replace the property with.</param>
        /// <returns>True if the view model property was replaced, false otherwise. E.g. If the view model instance provided is for a different view model, the
        /// replacement will fail.</returns>
        [DllImport(NativeLibrary.name)]
        private static extern bool replaceViewModelInstanceViewModelProperty(
            ViewModelInstanceSafeHandle baseInstanceValue,
            string path,
            ViewModelInstanceSafeHandle newInstance);


        #endregion
    }

    /// <summary>
    /// SafeHandle implementation for ViewModelInstance native resources
    /// </summary>
    internal sealed class ViewModelInstanceSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public ViewModelInstanceSafeHandle() : base(true)
        {
        }

        public ViewModelInstanceSafeHandle(IntPtr handle) : base(true)
        {
            SetHandle(handle);

            // We want to keep the native VM instance alive as long as the C# instance is alive so ref() it during construction and unref() it during destruction.
            // This is important because it allows users to reuse the VM instance as long as they have a reference to the class.
            // The alternative would be to have the user manually call ref() on the instance, which is error-prone and would require them to keep track of the reference count along with all the other resources they have to manage.
            if (!IsInvalid)
            {
                incrementViewModelInstanceReference(handle);
            }
        }

        protected override bool ReleaseHandle()
        {
            if (!IsInvalid)
            {
                unrefViewModelInstance(handle);
                return true;
            }
            return false;
        }

        [DllImport(NativeLibrary.name)]
        private static extern void unrefViewModelInstance(IntPtr instancePtr);

        [DllImport(NativeLibrary.name)]
        private static extern void incrementViewModelInstanceReference(IntPtr instancePtr);
    }
}
