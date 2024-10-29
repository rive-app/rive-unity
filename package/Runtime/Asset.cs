using System.Collections.Generic;
using UnityEngine;

namespace Rive
{


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

        /// <summary>
        /// Initializes the asset with the given bytes and embedded asset information.
        /// </summary>
        /// <param name="bytes"> The raw bytes of the Rive asset. </param>
        /// <param name="embeddedAssetsData"> The embedded asset data in the Rive asset. </param>
        internal void SetData(byte[] bytes, EmbeddedAssetData[] embeddedAssetsData)
        {
            m_Bytes = bytes;
            m_EmbeddedAssets = embeddedAssetsData;
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