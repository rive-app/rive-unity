using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Rive.Utils;
using Rive.Host;

namespace Rive
{
    /// <summary>
    /// Represents a runtime instance of a view model with mutable property values.
    /// A ViewModelInstance contains the same properties as its source view model but maintains its own state that can change during execution.
    /// </summary>
    public sealed class ViewModelInstance : ViewModelInstanceProperty, IDisposable
    {
        // Native gives the same handle for the same instance, so this is also the identity key.
        private readonly NativeViewModelInstanceHandle m_handle;

        private WeakReference<File> m_riveFile;

        // Strong references to subscribed properties, by name.
        // The VMI owns these so that properties survive even when the user drops their reference.
        private readonly Dictionary<string, ViewModelInstancePrimitiveProperty> m_subscribedProperties = new Dictionary<string, ViewModelInstancePrimitiveProperty>();

        // Every property object handed out, by name, so asking twice gives the same one.
        private readonly Dictionary<string, WeakReference<ViewModelInstancePrimitiveProperty>> m_properties =
            new Dictionary<string, WeakReference<ViewModelInstancePrimitiveProperty>>();

        private readonly List<WeakReference<ViewModelInstance>> m_parents = new List<WeakReference<ViewModelInstance>>(); private readonly List<ViewModelInstance> m_children = new List<ViewModelInstance>();

        // caching nested view model instances by name
        private readonly Dictionary<string, ViewModelInstance> m_viewModelInstances = new Dictionary<string, ViewModelInstance>();

        private const char kPathSeparator = '/';

        /// <summary>
        /// Cache for split paths to avoid repeated string operations
        /// </summary>
        private static readonly ConcurrentDictionary<string, string[]> s_pathSegmentsCache = new ConcurrentDictionary<string, string[]>();

        /// <summary>
        /// One wrapper per native instance, so every path to it gives the same object.
        /// </summary>
        private static readonly Dictionary<NativeViewModelInstanceHandle, WeakReference<ViewModelInstance>> s_viewModelInstanceCache =
            new Dictionary<NativeViewModelInstanceHandle, WeakReference<ViewModelInstance>>();

        // Guards the wrapper cache and the links between instances: parents,
        // children, nested instances and subscribed properties. Never held
        // across a native call or a user callback.
        private static readonly object s_lock = new object();

        // Held across a replace or a nested lookup, native call and graph update
        // together, so two of them on this instance can't land in a different
        // order natively than in the graph. Taken before s_lock, never after.
        private readonly object m_opLock = new object();

        // Set under s_lock, read anywhere.
        private volatile bool m_disposed = false;

        internal bool IsDisposed => m_disposed;

        private string m_viewModelName = null;
        private string m_name = null;


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

        internal NativeViewModelInstanceHandle NativeHandle => m_handle;

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
                if (m_disposed)
                {
                    return 0;
                }

                return GetViewModelInstanceRefCount(m_handle);
            }
        }
#endif

        /// <summary>
        /// The name of the view model that defines this instance.
        /// </summary>
        /// <remarks>
        /// Unlike <see cref="Name"/>, which is this specific instance's editor-assigned name,
        /// this is the name of the view model definition. Multiple instances can share the
        /// same view model name while having different instance names.
        /// </remarks>
        public string ViewModelName
        {
            get
            {
                if (m_viewModelName == null && !m_disposed)
                {
                    m_viewModelName = ViewModelNative.GetInfo(NativeHandle).ViewModelName ?? string.Empty;
                }

                return m_viewModelName ?? string.Empty;
            }
        }

        /// <summary>
        /// The Rive editor-assigned name of this view model instance.
        /// </summary>
        /// <remarks>
        /// Returns an empty string for instances without a name, such as those created with
        /// <see cref="ViewModel.CreateInstance"/>.
        /// </remarks>
        public string Name
        {
            get
            {
                if (m_name == null && !m_disposed)
                {
                    m_name = ViewModelNative.GetInfo(NativeHandle).Name ?? string.Empty;
                }

                return m_name ?? string.Empty;
            }
        }

        private ViewModelInstance(NativeViewModelInstanceHandle handle, File riveFile)
        {
            m_handle = handle;
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


        // Call inside s_lock.
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
            lock (m_opLock)
            {
                lock (s_lock)
                {
                    if (m_viewModelInstances.TryGetValue(name, out var instance))
                    {
                        return instance;
                    }
                }

                var newInstance = GetOrCreateFromHandle(GetViewModelInstanceViewModelProperty(m_handle, name), RiveFile, this);
                if (newInstance != null)
                {
                    lock (s_lock)
                    {
                        m_viewModelInstances[name] = newInstance;
                    }
                }
                return newInstance;
            }
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
            if (value == null || value.IsDisposed)
            {
                return false;
            }

            lock (m_opLock)
            {
                bool result = ReplaceViewModelInstanceViewModelProperty(m_handle, name, value.NativeHandle);

                if (result)
                {
                    lock (s_lock)
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
                }

                return result;
            }
        }

        private void ClearCallbacks()
        {
            lock (s_lock)
            {
                foreach (var kvp in m_subscribedProperties)
                {
                    kvp.Value.ClearDelegatesOnly();
                    PropertyCallbacksHub.Instance.Unregister(kvp.Value);
                }

                m_subscribedProperties.Clear();
            }
        }

        internal void AddParent(ViewModelInstance parent)
        {
            lock (s_lock)
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
        }

        internal void RemoveParent(ViewModelInstance parent)
        {
            lock (s_lock)
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
        }

        internal void AddChildToCallbacks(ViewModelInstance child)
        {
            lock (s_lock)
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
        }

        internal void RemoveChildFromCallbacks(ViewModelInstance child)
        {
            lock (s_lock)
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

            string name = property.Name;
            if (name == null)
            {
                return;
            }

            lock (s_lock)
            {
                bool wasFirst = m_subscribedProperties.Count == 0;
                bool added = !m_subscribedProperties.ContainsKey(name);

                m_subscribedProperties[name] = property;

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

            lock (s_lock)
            {
                if (property.Name != null)
                {
                    m_subscribedProperties.Remove(property.Name);
                    PropertyCallbacksHub.Instance.Unregister(property);
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
        }


        // Call inside s_lock.
        private static bool TryGetCachedViewModelInstance(NativeViewModelInstanceHandle handle, out ViewModelInstance instance)
        {
            if (s_viewModelInstanceCache.TryGetValue(handle, out var weakReference) &&
                weakReference.TryGetTarget(out instance))
            {
                return true;
            }

            instance = null;
            return false;
        }

        /// <summary>
        /// Drops the cache entry, but only if it is still this wrapper's, or has been collected.
        /// Call inside s_lock.
        /// </summary>
        private static void RemoveCachedViewModelInstance(NativeViewModelInstanceHandle handle, ViewModelInstance owner)
        {
            if (!s_viewModelInstanceCache.TryGetValue(handle, out var weakReference))
            {
                return;
            }

            if (!weakReference.TryGetTarget(out var cachedInstance) || ReferenceEquals(cachedInstance, owner))
            {
                s_viewModelInstanceCache.Remove(handle);
            }
        }

        internal bool TryGetCachedProperty(string name, out ViewModelInstancePrimitiveProperty property)
        {
            lock (m_properties)
            {
                if (m_properties.TryGetValue(name, out var weakReference) && weakReference.TryGetTarget(out property))
                {
                    return true;
                }
            }
            property = null;
            return false;
        }

        /// <summary>
        /// Caches the property unless another thread got there first, and returns whichever is cached.
        /// </summary>
        internal ViewModelInstancePrimitiveProperty CacheProperty(string name, ViewModelInstancePrimitiveProperty property)
        {
            lock (m_properties)
            {
                if (m_properties.TryGetValue(name, out var weakReference) && weakReference.TryGetTarget(out var existing))
                {
                    return existing;
                }
                m_properties[name] = new WeakReference<ViewModelInstancePrimitiveProperty>(property);
                return property;
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
        [Obsolete(ObsoleteMessages.HandleCallbacks)]
        public void HandleCallbacks()
        {
            if (m_disposed)
            {
                return;
            }

            // Copied under the lock, because the callbacks below are user code.
            List<ViewModelInstancePrimitiveProperty> properties;
            List<ViewModelInstance> children;
            lock (s_lock)
            {
                properties = new List<ViewModelInstancePrimitiveProperty>(m_subscribedProperties.Values);
                children = new List<ViewModelInstance>(m_children);
            }

            foreach (var prop in properties)
            {
                if (prop.HasChanged)
                {
                    prop.RaiseChangedEvent();
                }
            }

            foreach (var prop in properties)
            {
                if (prop.HasChanged)
                {
                    prop.ClearChanges();
                }
            }

            // Propagate to children
            foreach (var child in children)
            {
                child?.HandleCallbacks();
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
            // Claimed under the lock, so two disposes can't both release the handle,
            // and a fetch on another thread either returns this before it goes or never sees it.
            lock (s_lock)
            {
                if (m_disposed)
                {
                    return;
                }
                m_disposed = true;
                RemoveCachedViewModelInstance(m_handle, this);

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
            }

            if (m_handle.IsValid)
            {
                UnrefViewModelInstance(m_handle);
            }

            if (disposing)
            {
                GC.SuppressFinalize(this);
            }
        }

        public void Dispose()
        {
            // Waits out a replace or lookup still running on this instance.
            lock (m_opLock)
            {
                Dispose(true);
            }
        }

        /// <summary>
        /// Wraps a handle native just handed out, or returns the wrapper that already has it.
        /// Every handle native returns is counted, so this releases the extra when there is one.
        /// </summary>
        /// <param name="handle">A handle native returned to this call.</param>
        /// <param name="riveFile">The file the instance came from.</param>
        /// <param name="parent">Gets this instance's callbacks. An instance can have several.</param>
        internal static ViewModelInstance GetOrCreateFromHandle(NativeViewModelInstanceHandle handle, File riveFile, ViewModelInstance parent = null)
        {
            if (!handle.IsValid)
            {
                return null;
            }

            ViewModelInstance instance;
            bool alreadyWrapped;
            lock (s_lock)
            {
                alreadyWrapped = TryGetCachedViewModelInstance(handle, out instance);
                if (!alreadyWrapped)
                {
                    instance = new ViewModelInstance(handle, riveFile);
                    s_viewModelInstanceCache[handle] = new WeakReference<ViewModelInstance>(instance);
                }
                if (parent != null)
                {
                    instance.AddParent(parent);
                }
            }

            if (alreadyWrapped)
            {
                UnrefViewModelInstance(handle);
            }
            return instance;
        }

        #region Native Calls

        // Finalizers land here too, so it doesn't wait.
        internal static void UnrefViewModelInstance(NativeViewModelInstanceHandle instance)
        {
            ViewModelNative.Release(instance);
        }

        private static NativeViewModelInstanceHandle GetViewModelInstanceViewModelProperty(NativeViewModelInstanceHandle instance, string name)
        {
            return ViewModelNative.Nested(instance, name);
        }

#if UNITY_EDITOR
        private static int GetViewModelInstanceRefCount(NativeViewModelInstanceHandle instance)
        {
            return ViewModelNative.GetInfo(instance).RefCount;
        }
#endif

        /// <summary>
        /// Replaces a nested view model instance property. False if the new instance is for a different view model.
        /// </summary>
        private static bool ReplaceViewModelInstanceViewModelProperty(
            NativeViewModelInstanceHandle instance,
            string name,
            NativeViewModelInstanceHandle newInstance)
        {
            return ViewModelNative.ReplaceNested(instance, name, newInstance);
        }

        #endregion
    }
}
