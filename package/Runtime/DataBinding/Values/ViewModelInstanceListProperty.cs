using System;
using System.Collections.Generic;
using Rive.Utils;

namespace Rive
{
    /// <summary>
    /// A view model instance property that holds a list of view model instances.
    /// </summary>
    public sealed class ViewModelInstanceListProperty : ViewModelInstancePrimitiveProperty
    {
        // Held across each operation's native call and its bookkeeping, so two
        // threads changing the list can't leave native and the tracking out of step.
        private readonly object m_opLock = new object();

        // Only touched under m_opLock.
        private readonly HashSet<ViewModelInstance> m_trackedInstances = new HashSet<ViewModelInstance>();

        internal ViewModelInstanceListProperty(ViewModelInstance rootInstance, string name, int slot) : base(rootInstance, name, slot)
        {
        }

        /// <summary>
        /// The number of items in the list.
        /// </summary>
        public int Count
        {
            get
            {
                ThrowIfOwnerDisposed();

                if (!IsAttached)
                {
                    DebugLogger.Instance.LogWarning("Trying to get length of a null list property.");
                    return 0;
                }
                return GetViewModelInstanceListSize(InstanceHandle, Name);
            }
        }

        /// <summary>
        /// Gets the view model instance at the specified index.
        /// </summary>
        /// <param name="index">The index of the item to get.</param>
        /// <returns>The view model instance at the specified index, or null if the index is out of bounds.</returns>
        public ViewModelInstance GetInstanceAt(int index)
        {
            lock (m_opLock)
            {
                ThrowIfOwnerDisposed();

                if (!IsAttached)
                {
                    DebugLogger.Instance.LogError("Trying to get item from a null list property.");
                    return null;
                }

                if (index < 0 || index >= Count)
                {
                    DebugLogger.Instance.LogError($"Index {index} is out of bounds for list of length {Count}.");
                    return null;
                }

                NativeViewModelInstanceHandle item = GetViewModelInstanceListItemAt(InstanceHandle, Name, index);
                ViewModelInstance vmi = ViewModelInstance.GetOrCreateFromHandle(item, RootInstance?.RiveFile, RootInstance);
                if (vmi != null)
                {
                    m_trackedInstances.Add(vmi);
                }

                return vmi;
            }
        }

        /// <summary>
        /// Adds a view model instance to the end of the list.
        /// </summary>
        /// <param name="instance">The view model instance to add.</param>
        public void Add(ViewModelInstance instance)
        {
            lock (m_opLock)
            {
                ThrowIfOwnerDisposed();

                if (!IsAttached)
                {
                    DebugLogger.Instance.LogError("Trying to add to a null list property.");
                    return;
                }

                if (instance == null || instance.IsDisposed)
                {
                    DebugLogger.Instance.LogError("Cannot add null or invalid view model instance to list.");
                    return;
                }

                AddViewModelInstanceToList(InstanceHandle, Name, instance.NativeHandle);

                instance.AddParent(this.RootInstance);
                m_trackedInstances.Add(instance);
            }
        }

        /// <summary>
        /// Inserts a view model instance at the specified index.
        /// </summary>
        /// <param name="instance">The view model instance to insert.</param>
        /// <param name="index">The index at which to insert the instance.</param>
        public void Insert(ViewModelInstance instance, int index)
        {
            lock (m_opLock)
            {
                ThrowIfOwnerDisposed();

                if (instance == null)
                {
                    DebugLogger.Instance.LogError("Cannot insert null or invalid view model instance into list.");
                    return;
                }

                if (!IsAttached)
                {
                    DebugLogger.Instance.LogError("Trying to insert into a null list property.");
                    return;
                }

                if (index < 0)
                {
                    DebugLogger.Instance.LogError($"Index {index} is out of bounds for list of length {Count}.");
                    return;
                }

                if (!AddViewModelInstanceToListAt(InstanceHandle, Name, instance.NativeHandle, index))
                {
                    DebugLogger.Instance.LogError($"Failed to insert view model instance at index {index}.");
                    return;
                }

                instance.AddParent(this.RootInstance);
                m_trackedInstances.Add(instance);
            }
        }

        /// <summary>
        /// Removes a view model instance from the list.
        /// </summary>
        /// <param name="instance">The view model instance to remove.</param>
        /// <remarks>
        /// This method will remove every occurrence of the instance from the list.
        /// </remarks>
        public void Remove(ViewModelInstance instance)
        {
            lock (m_opLock)
            {
                ThrowIfOwnerDisposed();

                if (!IsAttached)
                {
                    DebugLogger.Instance.LogError("Trying to remove from a null list property.");
                    return;
                }

                if (instance == null || instance.IsDisposed)
                {
                    DebugLogger.Instance.LogError("Cannot remove null or invalid view model instance from list.");
                    return;
                }

                RemoveViewModelInstanceFromList(InstanceHandle, Name, instance.NativeHandle);

                instance.RemoveParent(this.RootInstance);
                m_trackedInstances.Remove(instance);
            }
        }

        /// <summary>
        /// Removes the view model instance at the specified index.
        /// </summary>
        /// <param name="index">The index of the item to remove.</param>
        public void RemoveAt(int index)
        {
            lock (m_opLock)
            {
                ThrowIfOwnerDisposed();

                if (!IsAttached)
                {
                    DebugLogger.Instance.LogError("Trying to remove from a null list property.");
                    return;
                }

                if (index < 0 || index >= Count)
                {
                    DebugLogger.Instance.LogError($"Index {index} is out of bounds for list of length {Count}.");
                    return;
                }

                ViewModelInstance instance = GetInstanceAt(index);
                if (instance == null)
                {
                    DebugLogger.Instance.LogError($"No instance found at index {index}.");
                    return;
                }

                RemoveViewModelInstanceFromListAt(InstanceHandle, Name, index);
                m_trackedInstances.Remove(instance);
            }
        }

        /// <summary>
        /// Removes all view model instances from the list.
        /// </summary>
        public void Clear()
        {
            lock (m_opLock)
            {
                ThrowIfOwnerDisposed();

                if (!IsAttached)
                {
                    DebugLogger.Instance.LogError("Trying to clear a null list property.");
                    return;
                }

                ClearViewModelInstanceList(InstanceHandle, Name);

                foreach (var instance in m_trackedInstances)
                {
                    instance?.RemoveParent(this.RootInstance);
                }
                m_trackedInstances.Clear();
            }
        }

        /// <summary>
        /// Swaps the view model instances at the specified indices.
        /// </summary>
        /// <param name="indexA">The index of the first item to swap.</param>
        /// <param name="indexB">The index of the second item to swap.</param>
        public void Swap(int indexA, int indexB)
        {
            lock (m_opLock)
            {
                ThrowIfOwnerDisposed();

                if (!IsAttached)
                {
                    DebugLogger.Instance.LogError("Trying to swap instances in a null list property.");
                    return;
                }

                if (indexA == indexB)
                {
                    DebugLogger.Instance.LogError("Cannot swap instances at the same index.");
                    return;
                }

                if (indexA < 0 || indexA >= Count)
                {
                    DebugLogger.Instance.LogError($"Index {indexA} is out of bounds for list of length {Count}.");
                    return;
                }

                if (indexB < 0 || indexB >= Count)
                {
                    DebugLogger.Instance.LogError($"Index {indexB} is out of bounds for list of length {Count}.");
                    return;
                }

                SwapViewModelInstancesInList(InstanceHandle, Name, indexA, indexB);
            }
        }

        /// <summary>
        /// Called when the list property value changes.
        /// </summary>
        internal override void RaiseChangedEvent()
        {
            // List changes don't have a specific value, just notify that the list changed
            m_onTriggered?.Invoke();
        }



        /// <summary>
        /// Event that is raised when the list changes.
        /// </summary>
        public event Action OnChanged
        {
            add => AddPropertyCallback(value, ref m_onTriggered);
            remove => RemovePropertyCallback(value, ref m_onTriggered);
        }
        private Action m_onTriggered;


        /// <summary>
        /// Clears all callbacks registered with this property.
        /// </summary>
        internal override void ClearAllCallbacks()
        {
            m_onTriggered = null;
            base.ClearAllCallbacks();
        }

        internal override void ClearDelegatesOnly()
        {
            m_onTriggered = null;
        }

        // Each waits, like the rest of the plain API.

        private static int GetViewModelInstanceListSize(NativeViewModelInstanceHandle instance, string path)
        {
            return ViewModelNative.List(instance, path, ViewModelNative.ListOp.Size).Count;
        }

        private static NativeViewModelInstanceHandle GetViewModelInstanceListItemAt(NativeViewModelInstanceHandle instance, string path, int index)
        {
            return ViewModelNative.ListItem(instance, path, index);
        }

        private static void AddViewModelInstanceToList(NativeViewModelInstanceHandle instance, string path, NativeViewModelInstanceHandle item)
        {
            ViewModelNative.List(instance, path, ViewModelNative.ListOp.Add, item);
        }

        private static bool AddViewModelInstanceToListAt(NativeViewModelInstanceHandle instance, string path, NativeViewModelInstanceHandle item, int index)
        {
            return ViewModelNative.List(instance, path, ViewModelNative.ListOp.InsertAt, item, index).Ok;
        }

        private static void RemoveViewModelInstanceFromList(NativeViewModelInstanceHandle instance, string path, NativeViewModelInstanceHandle item)
        {
            ViewModelNative.List(instance, path, ViewModelNative.ListOp.Remove, item);
        }

        private static void RemoveViewModelInstanceFromListAt(NativeViewModelInstanceHandle instance, string path, int index)
        {
            ViewModelNative.List(instance, path, ViewModelNative.ListOp.RemoveAt, default, index);
        }

        private static void ClearViewModelInstanceList(NativeViewModelInstanceHandle instance, string path)
        {
            ViewModelNative.List(instance, path, ViewModelNative.ListOp.Clear);
        }

        private static void SwapViewModelInstancesInList(NativeViewModelInstanceHandle instance, string path, int indexA, int indexB)
        {
            ViewModelNative.List(instance, path, ViewModelNative.ListOp.Swap, default, indexA, indexB);
        }
    }
}