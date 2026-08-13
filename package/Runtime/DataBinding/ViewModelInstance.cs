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

        // This holds a pointer to the underlying native ViewModelInstance*, used for identity caching. 
        // It’s set when the object is created so that Dispose and the finalizer don't need to call native code to find it.
        // Note: This is not the same pointer as the ViewModelInstanceRuntime* that the SafeHandle uses—this one points to the core object.
        private readonly CoreViewModelInstancePtr m_coreInstancePtr;

        private WeakReference<File> m_riveFile;

        // Strong references to subscribed properties keyed by native pointer.
        // The VMI owns these so that properties survive even when the user drops their reference.
        private readonly Dictionary<IntPtr, ViewModelInstancePrimitiveProperty> m_subscribedProperties = new Dictionary<IntPtr, ViewModelInstancePrimitiveProperty>();

        private readonly List<WeakReference<ViewModelInstance>> m_parents = new List<WeakReference<ViewModelInstance>>(); private readonly List<ViewModelInstance> m_children = new List<ViewModelInstance>();

        // caching nested view model instances by name
        private readonly Dictionary<string, ViewModelInstance> m_viewModelInstances = new Dictionary<string, ViewModelInstance>();

        private const char kPathSeparator = '/';

        /// <summary>
        /// Cache for split paths to avoid repeated string operations
        /// </summary>
        private static readonly ConcurrentDictionary<string, string[]> s_pathSegmentsCache = new ConcurrentDictionary<string, string[]>();

        /// <summary>
        /// Identity cache keyed on the core ViewModelInstance* (not the ViewModelInstanceRuntime*).
        /// Separate from the primitive property cache so the key spaces do not mix.
        /// </summary>
        private static readonly ConcurrentDictionary<CoreViewModelInstancePtr, WeakReference<ViewModelInstance>> s_viewModelInstanceCache =
            new ConcurrentDictionary<CoreViewModelInstancePtr, WeakReference<ViewModelInstance>>();


        private bool m_disposed = false;

        internal bool IsDisposed => m_disposed;

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

#if UNITY_EDITOR
        /// <summary>
        /// Gets the number of native references to the core ViewModelInstance managed by this wrapper.
        /// Returns 0 if this instance is disposed.
        /// </summary>
        /// <remarks>
        /// The value is only useful when comparing two readings to see how the reference count changes.
        /// The exact number can vary depending on how many places the core system is holding references
        /// (such as the data context, artboard, state machine, and each ViewModelInstanceRuntime).
        /// Tests should check that the count changes correctly, not that it hits a specific number.
        /// Only read this while the wrapper is alive; the wrapper is what keeps the core instance valid.
        /// </remarks>
        internal int DebugNativeRefCount
        {
            get
            {
                if (m_disposed || m_coreInstancePtr.IsNull)
                {
                    return 0;
                }

                return getCoreViewModelInstanceRefCount(m_coreInstancePtr.DebuggingPtrValue);
            }
        }
#endif

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




        private ViewModelInstance(IntPtr instanceValue, CoreViewModelInstancePtr coreInstancePtr, File riveFile)
        {
            m_safeHandle = new ViewModelInstanceSafeHandle(instanceValue);
            m_coreInstancePtr = coreInstancePtr;
            m_riveFile = new WeakReference<File>(riveFile);
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
                // GetOrCreateFromPointer owns cache lookup, ref balancing, and parent linking.
                var newInstance = GetOrCreateFromPointer(ptr, RiveFile, this);
                m_viewModelInstances[name] = newInstance;

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
            foreach (var kvp in m_subscribedProperties)
            {
                kvp.Value.ClearDelegatesOnly();
                PropertyCallbacksHub.Instance.Unregister(kvp.Key);
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
                for (int i = 0; i < m_parents.Count; i++)
                {
                    var parent = m_parents[i];
                    if (parent != null && parent.TryGetTarget(out var parentInstance) && parentInstance != null)
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
                for (int i = 0; i < m_parents.Count; i++)
                {
                    var parent = m_parents[i];
                    if (parent != null && parent.TryGetTarget(out var parentInstance) && parentInstance != null)
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
            if (property == null)
            {
                return;
            }

            IntPtr ptr = property.InstancePropertyPtr;
            if (ptr == IntPtr.Zero)
            {
                return;
            }

            bool wasFirst = m_subscribedProperties.Count == 0;
            bool added = !m_subscribedProperties.ContainsKey(ptr);

            m_subscribedProperties[ptr] = property;

            PropertyCallbacksHub.Instance.Register(property);

            if (added && wasFirst)
            {
                for (int i = 0; i < m_parents.Count; i++)
                {
                    var parent = m_parents[i];

                    if (parent != null && parent.TryGetTarget(out var parentInstance) && parentInstance != null)
                    {
                        parentInstance.AddChildToCallbacks(this);
                    }
                }
            }
        }

        /// <summary>
        /// Called by a property when it transitions from non-zero to zero subscribers.
        /// </summary>
        internal void UnregisterPropertyForCallbacks(ViewModelInstancePrimitiveProperty property)
        {
            if (property == null)
            {
                return;
            }

            IntPtr ptr = property.InstancePropertyPtr;
            if (ptr != IntPtr.Zero)
            {
                m_subscribedProperties.Remove(ptr);
                PropertyCallbacksHub.Instance.Unregister(ptr);
            }

            // If no more properties with callbacks and no children with callbacks, notify parents
            if (m_subscribedProperties.Count == 0 && m_children.Count == 0)
            {
                for (int i = 0; i < m_parents.Count; i++)
                {
                    var parent = m_parents[i];
                    if (parent != null && parent.TryGetTarget(out var parentInstance) && parentInstance != null)
                    {
                        parentInstance.RemoveChildFromCallbacks(this);
                    }
                }
            }
        }


        /// <summary>
        /// Gets a cached view model instance for a given core ViewModelInstance pointer.
        /// </summary>
        internal static bool TryGetCachedViewModelInstanceForPointer(CoreViewModelInstancePtr corePtr, out ViewModelInstance instance)
        {
            if (!corePtr.IsNull &&
                s_viewModelInstanceCache.TryGetValue(corePtr, out var weakReference) &&
                weakReference.TryGetTarget(out instance))
            {
                return true;
            }

            instance = null;
            return false;
        }

        /// <summary>
        /// Removes the cache entry for a core ViewModelInstance pointer, but only if it still belongs
        /// to the given owner (or has already been collected).
        /// </summary>
        /// <remarks>
        /// We don't remove the cache entry if another live wrapper for the same core pointer exists. Unconditional removal risks duplicate wrappers.
        /// </remarks>
        private static void RemoveGloballyCachedViewModelInstanceForPointer(CoreViewModelInstancePtr corePtr, ViewModelInstance owner)
        {
            if (corePtr.IsNull ||
                !s_viewModelInstanceCache.TryGetValue(corePtr, out var weakReference))
            {
                return;
            }

            if (!weakReference.TryGetTarget(out var cachedInstance) || ReferenceEquals(cachedInstance, owner))
            {
                s_viewModelInstanceCache.TryRemove(corePtr, out _);
            }
        }


        /// <summary>
        /// Adds a cached view model instance for a given core ViewModelInstance pointer.
        /// </summary>
        internal static void AddCachedViewModelInstanceForPointer(CoreViewModelInstancePtr corePtr, ViewModelInstance instance)
        {
            if (!corePtr.IsNull && instance != null)
            {
                s_viewModelInstanceCache[corePtr] = new WeakReference<ViewModelInstance>(instance);
            }
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
                throw new ObjectDisposedException(nameof(ViewModelInstance), "Cannot get property from a disposed ViewModelInstance.");
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
            if (m_disposed)
            {
                return;
            }

            foreach (var kvp in m_subscribedProperties)
            {
                var prop = kvp.Value;
                if (prop.HasChanged)
                {
                    prop.RaiseChangedEvent();
                }
            }

            foreach (var kvp in m_subscribedProperties)
            {
                var prop = kvp.Value;
                if (prop.HasChanged)
                {
                    prop.ClearChanges();
                }
            }

            // Propagate to children
            for (int i = 0; i < m_children.Count; i++)
            {
                m_children[i]?.HandleCallbacks();
            }
        }

        /// <summary>
        /// Replaces a nested view model instance property with a new instance.
        /// </summary>
        /// <param name="path">The path to the property to replace.</param>
        /// <param name="newInstance">The new instance to replace the property with.</param>
        public void SetViewModelInstance(string path, ViewModelInstance newInstance)
        {
            if (m_disposed)
            {
                throw new ObjectDisposedException(nameof(ViewModelInstance), "Cannot set view model instance on a disposed ViewModelInstance.");
            }

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
        /// Gets a font property of the view model instance.
        /// </summary>
        /// <param name="path">The path to the property.</param>
        /// <returns>The font property, or null if the property doesn't exist or is not a font type.</returns>
        public ViewModelInstanceFontProperty GetFontProperty(string path)
        {
            return GetProperty<ViewModelInstanceFontProperty>(path);
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

            if (disposing)
            {
                // ClearCallbacks() is intentionally only called on the explicit Dispose() path.
                // On the finalizer path, the managed objects in m_subscribedProperties may already be finalized,
                // and taking the lock inside PropertyCallbacksHub.Unregister() from a finalizer thread risks deadlocks.
                // The hub uses weak references, so the hub won't keep the properties alive. It will also clean up any dead properties during the next CaptureChanges() call.
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
            }

            if (m_safeHandle != null && !m_safeHandle.IsInvalid)
            {
                m_safeHandle.Dispose();
            }

            // Remove by the stored core pointer, we avoid P/Invoking from the finalizer path.
            RemoveGloballyCachedViewModelInstanceForPointer(m_coreInstancePtr, this);

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

        /// <summary>
        /// Helper method to get or create a ViewModelInstance from a native ViewModelInstanceRuntime pointer.
        /// Identity is keyed on the core ViewModelInstance* so every wrapping path resolves to the same C# object.
        /// </summary>
        /// <param name="instancePtr"> The native pointer to the ViewModelInstanceRuntime.</param>
        /// <param name="riveFile"> The Rive file associated with the ViewModelInstance. This is used to resolve the file context for the instance.</param>
        /// <param name="parent"> The parent ViewModelInstance, if any. The parent is used to propagate callbacks to this instance. A vm instance can have multiple parents.</param>
        /// <returns>The ViewModelInstance associated with the native pointer.</returns>
        internal static ViewModelInstance GetOrCreateFromPointer(IntPtr instancePtr, File riveFile, ViewModelInstance parent = null)
        {
            if (instancePtr == IntPtr.Zero)
            {
                return null;
            }

            var corePtr = new CoreViewModelInstancePtr(getCoreViewModelInstancePointer(instancePtr));
            if (TryGetCachedViewModelInstanceForPointer(corePtr, out ViewModelInstance existingInstance))
            {
                // Unity already owns this, we balance the extra ref from the freshly wrapped runtime.
                ViewModelInstanceSafeHandle.unrefViewModelInstance(instancePtr);
                if (parent != null)
                {
                    existingInstance.AddParent(parent);
                }
                return existingInstance;
            }

            var newInstance = new ViewModelInstance(instancePtr, corePtr, riveFile);
            if (parent != null)
            {
                newInstance.AddParent(parent);
            }
            AddCachedViewModelInstanceForPointer(corePtr, newInstance);
            return newInstance;
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

        // Returns the borrowed core ViewModelInstance*. Wrap in CoreViewModelInstancePtr at the call
        // site rather than typing the extern, so nothing depends on struct return marshalling.
        [DllImport(NativeLibrary.name)]
        private static extern IntPtr getCoreViewModelInstancePointer(IntPtr instancePtr);

#if UNITY_EDITOR
        [DllImport(NativeLibrary.name)]
        private static extern int getCoreViewModelInstanceRefCount(IntPtr coreInstancePtr);
#endif


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
    /// A borrowed pointer to the core native ViewModelInstance that a ViewModelInstanceRuntime wraps.
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="ViewModelInstanceSafeHandle"/>, which owns a ViewModelInstanceRuntime*
    /// and unrefs it on release. This pointer is only ever used as an identity key: it is never ref'd,
    /// unref'd, or dereferenced, so it deliberately is not a SafeHandle. The separate type keeps the two
    /// pointer kinds from being passed to each other's APIs.
    /// </remarks>
    internal readonly struct CoreViewModelInstancePtr : IEquatable<CoreViewModelInstancePtr>
    {
        private readonly IntPtr m_value;

        internal CoreViewModelInstancePtr(IntPtr value)
        {
            m_value = value;
        }

        internal bool IsNull => m_value == IntPtr.Zero;

#if UNITY_EDITOR
        // Only the refcount checker actually uses the pointer value as an address.
        // In all other cases, this pointer just acts as a unique identifier (key) and isn't accessed directly.
        internal IntPtr DebuggingPtrValue => m_value;
#endif

        public bool Equals(CoreViewModelInstancePtr other) => m_value == other.m_value;

        public override bool Equals(object obj) => obj is CoreViewModelInstancePtr other && Equals(other);

        public override int GetHashCode() => m_value.GetHashCode();
    }

    /// <summary>
    /// SafeHandle implementation for ViewModelInstance native resources
    /// </summary>
    internal sealed class ViewModelInstanceSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        // The P/Invoke marshaller throws ArgumentNullException if a SafeHandle argument is null.
        // We use this reusable invalid handle to represent IntPtr.Zero for optional parameters.
        internal static readonly ViewModelInstanceSafeHandle Null = new ViewModelInstanceSafeHandle();

        public ViewModelInstanceSafeHandle() : base(true)
        {
        }

        public ViewModelInstanceSafeHandle(IntPtr handle) : base(true)
        {
            SetHandle(handle);
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
        internal static extern void unrefViewModelInstance(IntPtr instancePtr);
    }
}
