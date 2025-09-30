using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Rive.Utils;

namespace Rive
{
    /// <summary>
    /// View models describe a set of properties, but cannot themselves be used to get or set values
    /// </summary>
    public sealed class ViewModel
    {
        private IntPtr m_modelPtr;
        private string m_name;

        private ViewModelPropertyData[] m_propertyData;

        private WeakReference<File> m_riveFile;

        private string[] m_instanceNames;

        /// <summary>
        /// The names of the instances of this view model in the Rive file.
        /// </summary>
        public IReadOnlyList<string> InstanceNames
        {
            get
            {
                if (m_instanceNames == null)
                {
                    m_instanceNames = GetInstanceNames();
                }

                return m_instanceNames;
            }
        }

        /// <summary>
        /// The number of instances of this view model in the Rive file.
        /// </summary>
        public int InstanceCount
        {
            get
            {
                if (m_modelPtr == IntPtr.Zero)
                {
                    return 0;
                }

                return (int)getViewModelInstanceCount(m_modelPtr);
            }
        }

        /// <summary>
        /// The name of this view model.
        /// </summary>
        public string Name
        {
            get
            {
                if (m_name == null)
                {
                    m_name = Marshal.PtrToStringAnsi(getViewModelName(m_modelPtr));
                }

                return m_name;
            }
        }

        /// <summary>
        /// The properties of this view model.
        /// </summary>
        public IReadOnlyList<ViewModelPropertyData> Properties
        {
            get
            {
                if (m_propertyData == null)
                {
                    m_propertyData = InitializeProperties();
                }

                return m_propertyData;
            }
        }

        internal ViewModel(IntPtr viewModelPtr, File riveFile)

        {
            m_modelPtr = viewModelPtr;
            m_riveFile = new WeakReference<File>(riveFile);
        }

        private ViewModelPropertyData[] InitializeProperties()
        {
            nuint propertyCount = getViewModelPropertyCount(m_modelPtr);
            ViewModelPropertyData[] properties = new ViewModelPropertyData[propertyCount];

            for (nuint i = 0; i < propertyCount; i++)
            {
                IntPtr namePtr = getViewModelPropertyNameAtIndex(m_modelPtr, i);
                string name = namePtr == IntPtr.Zero ? null : Marshal.PtrToStringAnsi(namePtr);
                uint type = getViewModelPropertyTypeAtIndex(m_modelPtr, i);

                properties[i] = new ViewModelPropertyData(name, (ViewModelDataType)type);

                // Free the string in memory
                freeViewModelString(namePtr);

            }

            return properties;
        }


        private string[] GetInstanceNames()
        {
            var namesList = getViewModelInstanceNamesList(m_modelPtr);
            if (namesList == IntPtr.Zero)
            {
                return new string[0];
            }

            int count = (int)getViewModelInstanceNamesCount(namesList);
            string[] names = new string[count];

            for (int i = 0; i < count; i++)
            {
                IntPtr namePtr = getViewModelInstanceNameAtIndex(namesList, (nuint)i);
                names[i] = Marshal.PtrToStringAnsi(namePtr);
            }

            freeViewModelInstanceNamesList(namesList);
            return names;
        }

        /// <summary>
        /// Creates a new instance of this view model from the given pointer. This is also used to cache instances that are created from the native code to avoid creating multiple instances of the same view model that share the same pointer.
        /// </summary>
        /// <param name="instanceValue"></param>
        /// <returns> The view model instance.</returns>
        private ViewModelInstance GetOrCreateInstanceFromPointer(IntPtr instanceValue)
        {
            if (instanceValue == IntPtr.Zero)
            {
                DebugLogger.Instance.LogError("Failed to create instance.");
                return null;
            }

            if (ViewModelInstance.TryGetCachedViewModelInstanceForPointer(instanceValue, out ViewModelInstance existingInstance))
            {
                return existingInstance;
            }

            ViewModelInstance newInstance = ViewModelInstance.GetOrCreateFromPointer(instanceValue, m_riveFile.TryGetTarget(out File file) ? file : null);

            return newInstance;
        }

        /// <summary>
        /// Instantiates a view model instance at the given index.
        /// </summary>
        /// <param name="index">The index of the instance to instantiate.</param>
        /// <returns> The view model instance at the given index.</returns>
        public ViewModelInstance CreateInstanceAt(int index)
        {
            if (index < 0 || index >= InstanceCount)
            {
                DebugLogger.Instance.LogError("Invalid instance index: " + index);
                return null;
            }


            IntPtr instanceValue = createViewModelInstanceAtIndex(m_modelPtr, (nuint)index);

            if (instanceValue == IntPtr.Zero)
            {
                DebugLogger.Instance.LogError("Failed to create instance at index: " + index);
                return null;
            }


            return GetOrCreateInstanceFromPointer(instanceValue);
        }

        /// <summary>
        /// Instantiates an instance of this view model with the given name.
        /// </summary>
        /// <param name="name">The name of the model to instantiate.</param>
        /// <returns>
        public ViewModelInstance CreateInstanceByName(string name)
        {
            if (name == null)
            {
                DebugLogger.Instance.LogError("Invalid instance name: " + name);
                return null;
            }

            IntPtr instanceValue = createViewModelInstanceByName(m_modelPtr, name);

            if (instanceValue == IntPtr.Zero)
            {
                DebugLogger.Instance.LogError("Failed to create instance with name: " + name);
                return null;
            }


            return GetOrCreateInstanceFromPointer(instanceValue);

        }

        /// <summary>
        /// Instantiates a default instance of this view model in the Rive file.
        /// </summary>
        /// <returns>The default instance of this view model.</returns>
        public ViewModelInstance CreateDefaultInstance()
        {

            IntPtr instanceValue = createDefaultViewModelInstance(m_modelPtr);

            if (instanceValue == IntPtr.Zero)
            {
                DebugLogger.Instance.LogError("Failed to create default instance.");
                return null;
            }


            return GetOrCreateInstanceFromPointer(instanceValue);

        }


        /// <summary>
        /// Create a new instance of this view model.
        /// </summary>
        /// <returns> A new instance of this view model.</returns>
        public ViewModelInstance CreateInstance()
        {

            IntPtr instanceValue = createViewModelInstance(m_modelPtr);

            if (instanceValue == IntPtr.Zero)
            {
                DebugLogger.Instance.LogError("Failed to create instance.");
                return null;
            }

            return GetOrCreateInstanceFromPointer(instanceValue);

        }



        [DllImport(NativeLibrary.name)]
        private static extern nuint getViewModelInstanceCount(IntPtr modelPtr);

        [DllImport(NativeLibrary.name)]
        private static extern IntPtr getViewModelName(IntPtr modelPtr);

        [DllImport(NativeLibrary.name)]
        private static extern nuint getViewModelPropertyCount(IntPtr modelPtr);

        [DllImport(NativeLibrary.name)]
        private static extern IntPtr getViewModelPropertyNameAtIndex(IntPtr modelPtr, nuint index);

        [DllImport(NativeLibrary.name)]
        private static extern uint getViewModelPropertyTypeAtIndex(IntPtr modelPtr, nuint index);

        #region Instance
        [DllImport(NativeLibrary.name)]
        private static extern IntPtr createViewModelInstanceAtIndex(IntPtr modelPtr, nuint index);

        [DllImport(NativeLibrary.name)]
        private static extern IntPtr createViewModelInstanceByName(IntPtr modelPtr, string name);


        [DllImport(NativeLibrary.name)]
        private static extern IntPtr createDefaultViewModelInstance(IntPtr modelPtr);

        [DllImport(NativeLibrary.name)]
        private static extern IntPtr createViewModelInstance(IntPtr modelPtr);



        [DllImport(NativeLibrary.name)]
        private static extern IntPtr getViewModelInstanceNamesList(IntPtr modelPtr);

        [DllImport(NativeLibrary.name)]
        private static extern void freeViewModelInstanceNamesList(IntPtr namesList);

        [DllImport(NativeLibrary.name)]
        private static extern nuint getViewModelInstanceNamesCount(IntPtr namesList);

        [DllImport(NativeLibrary.name)]
        private static extern IntPtr getViewModelInstanceNameAtIndex(IntPtr namesList, nuint index);


        #endregion

        #region Cleanup
        [DllImport(NativeLibrary.name)]
        private static extern void freeViewModelString(IntPtr stringPtr);
        #endregion  
    }
}
