using System;
using Rive.Utils;
using UnityEngine;

namespace Rive
{
    /// <summary>
    /// Represents the type of an embedded asset in a Rive asset.
    /// </summary>
    public enum EmbeddedAssetType : ushort
    {
        Unknown = 0,
        Image = 105,
        Font = 141,
        Audio = 406,
        Script = 529
    }

    /// <summary>
    /// Represents information about inband/out of band asset (OOB) that is embedded/referenced in a Rive asset.
    /// </summary>
    [Serializable]
    public class EmbeddedAssetData
    {
        [SerializeField]
        private EmbeddedAssetType m_AssetType;

        [SerializeField]
        private uint m_Id;

        [SerializeField]
        private string m_Name;

        [SerializeField]
        private uint m_AssetSizeInBytes;

        [SerializeField]
        private OutOfBandAsset m_OutOfBandAsset;


        /// <summary>
        /// The type of the embedded asset.
        /// </summary>
        public EmbeddedAssetType AssetType { get { return m_AssetType; } }

        /// <summary>
        /// The unique identifier of the embedded asset.
        /// </summary>
        public uint Id { get { return m_Id; } }

        /// <summary>
        /// The name of the embedded asset.
        /// </summary>
        public string Name { get { return m_Name; } }

        /// <summary>
        /// The size of the bytes embedded in the asset. If the asset is only referenced (not embedded), this will be 0.
        /// </summary>
        public uint InBandBytesSize { get { return m_AssetSizeInBytes; } }

        /// <summary>
        /// The out of band asset that is referenced by this embedded asset.
        /// </summary>
        public OutOfBandAsset OutOfBandAsset { get { return m_OutOfBandAsset; } internal set { m_OutOfBandAsset = value; } }


        /// <summary>
        /// Creates a new EmbeddedAssetData instance.
        /// </summary>
        /// <param name="assetType"> The type of the embedded asset. </param>
        /// <param name="id"> The unique identifier of the embedded asset. </param>
        /// <param name="name"> The name of the embedded asset. </param>
        /// <param name="assetSizeInBytes"> The number of bytes in the embedded asset. </param>
        public EmbeddedAssetData(EmbeddedAssetType assetType, uint id, string name, uint assetSizeInBytes)
        {
            m_AssetType = assetType;
            m_Id = id;
            m_Name = name;
            m_AssetSizeInBytes = assetSizeInBytes;
        }


    }




}
