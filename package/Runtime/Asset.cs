using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using System.Linq;
#endif
using UnityEngine;

namespace Rive
{



    [Serializable]
    internal class FileMetadata
    {
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
        public class ArtboardMetadata
        {
            public string Name;
            public float Width;
            public float Height;
            public List<StateMachineMetadata> StateMachines = new List<StateMachineMetadata>();
        }
        public List<ArtboardMetadata> Artboards = new List<ArtboardMetadata>();

#if UNITY_EDITOR
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
        internal FileMetadata EditorOnlyMetadata { get { return m_FileMetadata; } }

        private void GenerateFileMetadata()
        {
            m_FileMetadata = new FileMetadata();

            using (var file = File.Load(this))
            {
                if (file == null) return;

                for (uint i = 0; i < file.ArtboardCount; i++)
                {
                    var artboard = file.Artboard(i);
                    if (artboard == null) continue;

                    var artboardMeta = new FileMetadata.ArtboardMetadata
                    {
                        Name = file.ArtboardName(i),
                        Width = artboard.Width,
                        Height = artboard.Height
                    };

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