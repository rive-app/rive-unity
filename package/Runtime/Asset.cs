using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using System.Linq;
#endif
using Rive.Utils;

using UnityEngine;

namespace Rive
{



    [Serializable]
    internal class FileMetadata
    {
#if UNITY_EDITOR

        private const int CURRENT_VERSION = 1;      // Increment this whenever the structure changes 
        [SerializeField] private int m_Version = 1; //And also update this so we can check for changes in the future

        [Serializable]
        public class InputMetadata
        {
            public string Name;
            public string Type;
        }

        [Serializable]
        public class StateMachineMetadata
        {
            public string Name;
            public List<InputMetadata> Inputs = new List<InputMetadata>();
        }

        [Serializable]
        public class ViewModelPropertyMetadata
        {
            public string Name;
            public ViewModelDataType Type;

            /// <summary>
            /// The name of the nested view model if the property is a view model.
            /// </summary>
            public string NestedViewModelName;

            /// <summary>
            /// The name of the enum type if the property is an enum.
            /// </summary>
            public string EnumTypeName;

            public static ViewModelPropertyMetadata FromPropertyData(ViewModelPropertyData propertyData, ViewModel viewModel)
            {
                if (propertyData.Type == ViewModelDataType.ViewModel)
                {
                    // Get the view model name by loading a view model instance and getting the name of the view model
                    var instance = viewModel.CreateInstance();

                    ViewModelInstance nestedInstance = instance.GetProperty<ViewModelInstance>(propertyData.Name);

                    if (nestedInstance == null)
                    {
                        DebugLogger.Instance.LogWarning("Could not find nested view model instance for property " + propertyData.Name);
                        return new ViewModelPropertyMetadata
                        {
                            Name = propertyData.Name,
                            Type = propertyData.Type
                        };
                    }

                    string nestedViewModelName = nestedInstance.ViewModelName;

                    nestedInstance.Dispose(); // We don't need the instance anymore

                    return new ViewModelPropertyMetadata
                    {
                        Name = propertyData.Name,
                        Type = propertyData.Type,
                        NestedViewModelName = nestedViewModelName
                    };
                }
                else if (propertyData.Type == ViewModelDataType.Enum)
                {
                    // Get the enum name by loading a view model instance and getting the associated enum type.
                    // Unfortunately, we need to do it this way because the native API doesn't expose the enum type name directly from the property data.
                    var instance = viewModel.CreateInstance();


                    var enumType = ViewModelInstancePropertyHandlersFactory.GetEnumForPropertyAtPath(instance, propertyData.Name);

                    instance.Dispose(); // We don't need the instance anymore

                    return new ViewModelPropertyMetadata
                    {
                        Name = propertyData.Name,
                        Type = propertyData.Type,
                        EnumTypeName = enumType?.Name
                    };

                }


                return new ViewModelPropertyMetadata
                {
                    Name = propertyData.Name,
                    Type = propertyData.Type
                };
            }
        }

        [Serializable]
        public class ArtboardMetadata
        {

            [SerializeField] private string m_Name;

            [SerializeField] private float m_Width;

            [SerializeField] private float m_Height;

            [SerializeField] private List<StateMachineMetadata> m_StateMachines;

            [SerializeField] private ViewModelMetadata m_DefaultViewModel;

            public string Name { get { return m_Name; } }
            public float Width { get { return m_Width; } }
            public float Height { get { return m_Height; } }
            public List<StateMachineMetadata> StateMachines { get { return m_StateMachines; } }

            public ViewModelMetadata DefaultViewModel { get { return m_DefaultViewModel; } }

            public ArtboardMetadata(string name, float width, float height, List<StateMachineMetadata> stateMachines, ViewModelMetadata defaultViewModel)
            {
                m_Name = name;
                m_Width = width;
                m_Height = height;
                m_StateMachines = stateMachines == null ? new List<StateMachineMetadata>() : stateMachines;
                m_DefaultViewModel = defaultViewModel;
            }
        }

        [Serializable]
        public class ViewModelMetadata
        {
            /// <summary>
            /// The name of the view model.
            /// </summary>
            public string Name;

            /// <summary>
            /// The properties of the view model.
            /// </summary>
            public List<ViewModelPropertyMetadata> Properties = new List<ViewModelPropertyMetadata>();

            /// <summary>
            /// The names of the instances of this view model within the Rive file.
            /// </summary>
            public List<string> InstanceNames = new List<string>();

            public static ViewModelMetadata FromViewModel(ViewModel viewModel)
            {
                var viewModelMeta = new ViewModelMetadata
                {
                    Name = viewModel.Name
                };

                IReadOnlyList<ViewModelPropertyData> properties = viewModel.Properties;

                foreach (var property in properties)
                {
                    viewModelMeta.Properties.Add(ViewModelPropertyMetadata.FromPropertyData(property, viewModel));
                }

                viewModelMeta.InstanceNames.AddRange(viewModel.InstanceNames);

                return viewModelMeta;
            }
        }

        [Serializable]
        public class ViewModelEnumMetadata
        {
            [SerializeField] private string m_Name;

            [SerializeField] private string[] m_Values;

            public string Name { get { return m_Name; } }
            public IReadOnlyList<string> Values { get { return m_Values; } }

            public ViewModelEnumMetadata(string name, IReadOnlyList<string> values)
            {
                m_Name = name;
                m_Values = values.ToArray();
            }
        }

        public List<ArtboardMetadata> Artboards = new List<ArtboardMetadata>();

        public List<ViewModelMetadata> ViewModels = new List<ViewModelMetadata>();

        public List<ViewModelEnumMetadata> Enums = new List<ViewModelEnumMetadata>();

        public string[] GetArtboardNames()
        {
            return Artboards.Select(a => a.Name).ToArray();
        }

        public string[] GetStateMachineNames(string artboardName)
        {
            var artboard = Artboards.FirstOrDefault(a => a.Name == artboardName);
            return artboard?.StateMachines.Select(sm => sm.Name).ToArray() ?? new string[0];
        }

        public ArtboardMetadata GetArtboard(string name)
        {
            return Artboards.FirstOrDefault(a => a.Name == name);
        }

        public bool NeedsReload()
        {
            // Force reload for unversioned (0) or outdated data
            return m_Version != CURRENT_VERSION;
        }
#endif
    }


    /// <summary>
    /// Represents a Rive asset (.riv)
    /// </summary>
    public class Asset : ScriptableObject
    {
        [HideInInspector]
        [SerializeField]
        private byte[] m_Bytes;

        /// <summary>
        /// The raw bytes of the Rive asset
        /// </summary>
        public byte[] Bytes { get { return m_Bytes; } }

        [HideInInspector]
        [SerializeField]
        private EmbeddedAssetData[] m_EmbeddedAssets;

        /// <summary>
        /// An array of all the embedded asset data in this Rive asset
        /// </summary>
        public IReadOnlyList<EmbeddedAssetData> EmbeddedAssets { get { return m_EmbeddedAssets; } }

        /// <summary>
        /// The number of embedded asset data in this Rive asset
        /// </summary>
        public int EmbeddedAssetCount { get { return m_EmbeddedAssets == null ? 0 : m_EmbeddedAssets.Length; } }

#if UNITY_EDITOR
        [SerializeField]
        private FileMetadata m_FileMetadata;

        /// <summary>
        /// Metadata about the contents of the Rive file. Available only in the Unity editor.
        /// </summary>
        internal FileMetadata EditorOnlyMetadata
        {
            get
            {
                if (m_FileMetadata == null || m_FileMetadata.NeedsReload())
                {
                    GenerateFileMetadata();
                }
                return m_FileMetadata;
            }
        }

        private void GenerateFileMetadata()
        {
            m_FileMetadata = new FileMetadata();

            using (var file = File.Load(this))
            {
                if (file == null) return;

                // View models

                IReadOnlyList<ViewModel> viewModels = file.ViewModels;

                foreach (var viewModel in viewModels)
                {
                    var viewModelMeta = FileMetadata.ViewModelMetadata.FromViewModel(viewModel);

                    m_FileMetadata.ViewModels.Add(viewModelMeta);
                }

                // Enums

                foreach (var enumData in file.ViewModelEnums)
                {
                    var enumMeta = new FileMetadata.ViewModelEnumMetadata(enumData.Name, enumData.Values);
                    m_FileMetadata.Enums.Add(enumMeta);
                }


                for (uint i = 0; i < file.ArtboardCount; i++)
                {
                    var artboard = file.Artboard(i);
                    if (artboard == null) continue;

                    FileMetadata.ViewModelMetadata defaultViewModel = null;

                    if (artboard.DefaultViewModel != null)
                    {
                        defaultViewModel = FileMetadata.ViewModelMetadata.FromViewModel(artboard.DefaultViewModel);

                    }

                    var artboardMeta = new FileMetadata.ArtboardMetadata(name: file.ArtboardName(i), width: artboard.Width, height: artboard.Height, stateMachines: new List<FileMetadata.StateMachineMetadata>(), defaultViewModel: defaultViewModel);


                    for (uint j = 0; j < artboard.StateMachineCount; j++)
                    {
                        var stateMachine = artboard.StateMachine(j);
                        if (stateMachine == null) continue;

                        var smMeta = new FileMetadata.StateMachineMetadata
                        {
                            Name = artboard.StateMachineName(j)
                        };

                        foreach (var input in stateMachine.Inputs())
                        {
                            var inputMeta = new FileMetadata.InputMetadata
                            {
                                Name = input.Name,
                                Type = input.IsBoolean ? "Boolean" :
                                       input.IsNumber ? "Number" :
                                       input.IsTrigger ? "Trigger" : "Unknown"
                            };
                            smMeta.Inputs.Add(inputMeta);
                        }


                        artboardMeta.StateMachines.Add(smMeta);
                    }

                    m_FileMetadata.Artboards.Add(artboardMeta);
                }
            }
        }
#endif

        /// <summary>
        /// Initializes the asset with the given bytes and embedded asset information.
        /// </summary>
        /// <param name="bytes"> The raw bytes of the Rive asset. </param>
        /// <param name="embeddedAssetsData"> The embedded asset data in the Rive asset. </param>
        internal void SetData(byte[] bytes, EmbeddedAssetData[] embeddedAssetsData)
        {
            m_Bytes = bytes;
            m_EmbeddedAssets = embeddedAssetsData;

#if UNITY_EDITOR
            GenerateFileMetadata();
#endif
        }

        /// <summary>
        /// Create a new Rive asset instance from the given bytes and embedded asset data.
        /// </summary>
        /// <param name="bytes"> The raw bytes of the Rive asset. </param>
        /// <param name="embeddedAssetsData"> The embedded asset data in the Rive asset. </param>
        /// <returns> The created Rive asset instance. </returns>
        public static Asset Create(byte[] bytes, EmbeddedAssetData[] embeddedAssetsData)
        {
            var asset = ScriptableObject.CreateInstance<Asset>();
            asset.SetData(bytes, embeddedAssetsData);
            return asset;
        }

    }


}