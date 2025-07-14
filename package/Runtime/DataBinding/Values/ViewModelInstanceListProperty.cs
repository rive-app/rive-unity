using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Rive.Utils;

namespace Rive
{
    /// <summary>
    /// A view model instance property that holds a list of view model instances.
    /// </summary>
    public sealed class ViewModelInstanceListProperty : ViewModelInstancePrimitiveProperty
    {

        internal ViewModelInstanceListProperty(IntPtr instanceValuePtr, ViewModelInstance rootInstance)
            : base(instanceValuePtr, rootInstance)
        {
        }

        /// <summary>
        /// The number of items in the list.
        /// </summary>
        public int Count
        {
            get
            {
                if (InstancePropertyPtr == IntPtr.Zero)
                {
                    DebugLogger.Instance.LogWarning("Trying to get length of a null list property.");
                    return 0;
                }
                return (int)getViewModelInstanceListSize(InstancePropertyPtr);
            }
        }

        /// <summary>
        /// Gets the view model instance at the specified index.
        /// </summary>
        /// <param name="index">The index of the item to get.</param>
        /// <returns>The view model instance at the specified index, or null if the index is out of bounds.</returns>
        public ViewModelInstance GetInstanceAt(int index)
        {
            if (InstancePropertyPtr == IntPtr.Zero)
            {
                DebugLogger.Instance.LogWarning("Trying to get item from a null list property.");
                return null;
            }

            if (index < 0 || index >= Count)
            {
                DebugLogger.Instance.LogWarning($"Index {index} is out of bounds for list of length {Count}.");
                return null;
            }

            IntPtr instancePtr = getViewModelInstanceListItemAt(InstancePropertyPtr, index);
            if (instancePtr == IntPtr.Zero)
            {
                return null;
            }

            ViewModelInstance vmi = GetOrCreateVMInstanceFromPtr(instancePtr);

            return vmi;

        }

        private ViewModelInstance GetOrCreateVMInstanceFromPtr(IntPtr instancePtr)
        {
            if (instancePtr == IntPtr.Zero)
            {
                return null;
            }

            // Check if we already have a cached instance for this pointer, we want to return the same C# instance if it exists for the same pointer.
            if (ViewModelInstance.TryGetCachedViewModelInstanceForPointer(instancePtr, out ViewModelInstance existingInstance))
            {
                return existingInstance;
            }

            // Create a new instance and cache it
            var newInstance = new ViewModelInstance(instancePtr, this.RootInstance?.RiveFile);
            ViewModelInstance.AddCachedViewModelInstanceForPointer(instancePtr, newInstance);


            return newInstance;
        }



        /// <summary>
        /// Adds a view model instance to the end of the list.
        /// </summary>
        /// <param name="instance">The view model instance to add.</param>
        public void Add(ViewModelInstance instance)
        {
            if (InstancePropertyPtr == IntPtr.Zero)
            {
                DebugLogger.Instance.LogWarning("Trying to add to a null list property.");
                return;
            }

            if (instance == null || instance.NativeSafeHandle.IsInvalid)
            {
                DebugLogger.Instance.LogWarning("Cannot add null or invalid view model instance to list.");
                return;
            }

            addViewModelInstanceToList(InstancePropertyPtr, instance.NativeSafeHandle.DangerousGetHandle());

            instance.AddParent(this.RootInstance);

        }

        /// <summary>
        /// Inserts a view model instance at the specified index.
        /// </summary>
        /// <param name="instance">The view model instance to insert.</param>
        /// <param name="index">The index at which to insert the instance.</param>
        public void Insert(ViewModelInstance instance, int index)
        {

            if (instance == null)
            {
                DebugLogger.Instance.LogWarning("Cannot insert null or invalid view model instance into list.");
                return;
            }

            if (InstancePropertyPtr == IntPtr.Zero)
            {
                DebugLogger.Instance.LogWarning("Trying to insert into a null list property.");
                return;
            }

            if (index < 0)
            {
                DebugLogger.Instance.LogWarning($"Index {index} is out of bounds for list of length {Count}.");
                return;
            }

            if (!addViewModelInstanceToListAt(InstancePropertyPtr, instance.NativeSafeHandle.DangerousGetHandle(), index))
            {
                DebugLogger.Instance.LogWarning($"Failed to insert view model instance at index {index}.");
                return;
            }

            instance.AddParent(this.RootInstance);
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
            if (InstancePropertyPtr == IntPtr.Zero)
            {
                DebugLogger.Instance.LogWarning("Trying to remove from a null list property.");
                return;
            }

            if (instance == null || instance.NativeSafeHandle.IsInvalid)
            {
                DebugLogger.Instance.LogWarning("Cannot remove null or invalid view model instance from list.");
                return;
            }

            removeViewModelInstanceFromList(InstancePropertyPtr, instance.NativeSafeHandle.DangerousGetHandle());

            instance.RemoveParent(this.RootInstance);

        }

        /// <summary>
        /// Removes the view model instance at the specified index.
        /// </summary>
        /// <param name="index">The index of the item to remove.</param>
        public void RemoveAt(int index)
        {
            if (InstancePropertyPtr == IntPtr.Zero)
            {
                DebugLogger.Instance.LogWarning("Trying to remove from a null list property.");
                return;
            }

            if (index < 0 || index >= Count)
            {
                DebugLogger.Instance.LogWarning($"Index {index} is out of bounds for list of length {Count}.");
                return;
            }

            // Get the instance pointer at the specified index so we can track whether we've incremented the ref count
            IntPtr instancePtr = getViewModelInstanceListItemAt(InstancePropertyPtr, index);
            if (instancePtr == IntPtr.Zero)
            {
                DebugLogger.Instance.LogWarning($"No instance found at index {index}.");
                return;
            }

            removeViewModelInstanceFromListAt(InstancePropertyPtr, index);

        }

        /// <summary>
        /// Swaps the view model instances at the specified indices.
        /// </summary>
        /// <param name="indexA">The index of the first item to swap.</param>
        /// <param name="indexB">The index of the second item to swap.</param>
        public void Swap(int indexA, int indexB)
        {
            if (InstancePropertyPtr == IntPtr.Zero)
            {
                DebugLogger.Instance.LogWarning("Trying to swap instances in a null list property.");
                return;
            }

            if (indexA == indexB)
            {
                DebugLogger.Instance.LogWarning("Cannot swap instances at the same index.");
                return;
            }

            if (indexA < 0 || indexA >= Count)
            {
                DebugLogger.Instance.LogWarning($"Index {indexA} is out of bounds for list of length {Count}.");
                return;
            }

            if (indexB < 0 || indexB >= Count)
            {
                DebugLogger.Instance.LogWarning($"Index {indexB} is out of bounds for list of length {Count}.");
                return;
            }

            swapViewModelInstancesInList(InstancePropertyPtr, indexA, indexB);
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

        [DllImport(NativeLibrary.name)]
        private static extern nuint getViewModelInstanceListSize(IntPtr listProperty);

        [DllImport(NativeLibrary.name)]
        private static extern IntPtr getViewModelInstanceListItemAt(IntPtr listProperty, int index);

        [DllImport(NativeLibrary.name)]
        private static extern void addViewModelInstanceToList(IntPtr listProperty, IntPtr instance);

        [DllImport(NativeLibrary.name)]
        private static extern bool addViewModelInstanceToListAt(IntPtr listProperty, IntPtr instance, int index);

        [DllImport(NativeLibrary.name)]
        private static extern void removeViewModelInstanceFromList(IntPtr listProperty, IntPtr instance);

        [DllImport(NativeLibrary.name)]
        private static extern void removeViewModelInstanceFromListAt(IntPtr listProperty, int index);

        [DllImport(NativeLibrary.name)]
        private static extern void swapViewModelInstancesInList(IntPtr listProperty, int indexA, int indexB);
    }
}