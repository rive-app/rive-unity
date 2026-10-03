using System;
using System.Collections.Generic;
using Rive.Utils;

namespace Rive
{
    /// <summary>
    /// View models describe a set of properties, but cannot themselves be used to get or set values
    /// </summary>
    public sealed class ViewModel
    {
        // A view model is its file plus its index there.
        private readonly NativeFileHandle m_file;
        private readonly int m_index;
        // The file's names for it, read when it loaded.
        private readonly FileContents.ViewModelInfo m_info;
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
                return m_info.InstanceNames.Length;
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
                    m_name = m_info.Name;
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

        internal ViewModel(File riveFile, int index)
        {
            m_file = riveFile.NativeFile;
            m_index = index;
            m_info = riveFile.Contents.ViewModels[index];
            m_riveFile = new WeakReference<File>(riveFile);
        }

        private ViewModelPropertyData[] InitializeProperties()
        {
            return (ViewModelPropertyData[])m_info.Properties.Clone();
        }


        private string[] GetInstanceNames()
        {
            return (string[])m_info.InstanceNames.Clone();
        }

        private ViewModelInstance GetOrCreateInstance(NativeViewModelInstanceHandle handle)
        {
            return ViewModelInstance.GetOrCreateFromHandle(
                handle,
                m_riveFile.TryGetTarget(out File file) ? file : null);
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


            NativeViewModelInstanceHandle instanceValue = ViewModelNative.Create(m_file, (uint)m_index, ViewModelNative.InstanceKind.Named, m_info.InstanceNames[index]);

            if (!instanceValue.IsValid)
            {
                DebugLogger.Instance.LogError("Failed to create instance at index: " + index);
                return null;
            }


            return GetOrCreateInstance(instanceValue);
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

            NativeViewModelInstanceHandle instanceValue = Array.IndexOf(m_info.InstanceNames, name) >= 0
                ? ViewModelNative.Create(m_file, (uint)m_index, ViewModelNative.InstanceKind.Named, name)
                : default;

            if (!instanceValue.IsValid)
            {
                DebugLogger.Instance.LogError("Failed to create instance with name: " + name);
                return null;
            }


            return GetOrCreateInstance(instanceValue);

        }

        /// <summary>
        /// Instantiates a default instance of this view model in the Rive file.
        /// </summary>
        /// <returns>The default instance of this view model.</returns>
        public ViewModelInstance CreateDefaultInstance()
        {

            NativeViewModelInstanceHandle instanceValue = ViewModelNative.Create(m_file, (uint)m_index, ViewModelNative.InstanceKind.Default, null);

            if (!instanceValue.IsValid)
            {
                DebugLogger.Instance.LogError("Failed to create default instance.");
                return null;
            }


            return GetOrCreateInstance(instanceValue);

        }


        /// <summary>
        /// Create a new instance of this view model.
        /// </summary>
        /// <returns> A new instance of this view model.</returns>
        public ViewModelInstance CreateInstance()
        {

            NativeViewModelInstanceHandle instanceValue = ViewModelNative.Create(m_file, (uint)m_index, ViewModelNative.InstanceKind.Blank, null);

            if (!instanceValue.IsValid)
            {
                DebugLogger.Instance.LogError("Failed to create instance.");
                return null;
            }

            return GetOrCreateInstance(instanceValue);

        }
    }
}
