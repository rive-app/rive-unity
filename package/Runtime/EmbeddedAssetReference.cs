using System;
using Rive.Utils;

namespace Rive
{


    /// <summary>
    /// Represents a file asset within a specific Rive file. 
    /// </summary>
    public abstract class EmbeddedAssetReference
    {
        public class WarningCodes
        {
            public const string FILE_NOT_LOADED = "RIVE_FILE_NOT_YET_LOADED";

            public const string FILE_RELEASED = "RIVE_FILE_RELEASED";

            public const string NULL_OOB_ASSET = "NULL_OOB_ASSET";
        }


        private EmbeddedAssetType m_assetType = EmbeddedAssetType.Unknown;

        private uint m_Id;

        private string m_Name;

        private uint m_EmbeddededBytesSize;


        /// <summary>
        /// The index of the embedded asset in the Rive file.
        /// </summary>
        private uint m_Index;

        private WeakReference<Rive.File> loadedFileReference;

        private OutOfBandAsset m_OutOfBandAssetToLoad;


        /// <summary>
        /// The type of the embedded asset.
        /// </summary>
        public EmbeddedAssetType AssetType { get { return m_assetType; } }

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
        public uint EmbeddededBytesSize { get { return m_EmbeddededBytesSize; } }

        /// <summary>
        /// The out of band asset that will be loaded when the asset is needed.
        /// </summary>
        public OutOfBandAsset OutOfBandAssetToLoad { get { return m_OutOfBandAssetToLoad; } }

        /// <summary>
        /// The index of the embedded/referenced asset in the Rive file.
        /// </summary>
        public uint IndexInRiveFile { get { return m_Index; } }



        public EmbeddedAssetReference(EmbeddedAssetData embeddedAssetData, uint index)
        {
            m_assetType = embeddedAssetData.AssetType;
            m_Id = embeddedAssetData.Id;
            m_Name = embeddedAssetData.Name;
            m_EmbeddededBytesSize = embeddedAssetData.InBandBytesSize;
            m_Index = index;
            m_OutOfBandAssetToLoad = embeddedAssetData.OutOfBandAsset;
        }

        public EmbeddedAssetReference(EmbeddedAssetType assetType, uint id, string name, uint embeddededBytesSize, uint indexInRiveFile, OutOfBandAsset outOfBandAsset)
        {
            m_assetType = assetType;
            m_Id = id;
            m_Name = name;
            m_EmbeddededBytesSize = embeddededBytesSize;
            m_Index = indexInRiveFile;
            m_OutOfBandAssetToLoad = outOfBandAsset;
        }



        /// <summary>
        /// Sets the loaded file reference for this embedded asset, so we can update the asset reference later.
        /// </summary>
        /// <param name="file"></param>
        internal void SetRiveFileReference(File file)
        {
            loadedFileReference = new WeakReference<File>(file);
        }

        protected void UpdateEmbeddedAssetReferenceInFile(OutOfBandAsset outOfBandAsset)
        {
            if (outOfBandAsset == null)
            {
                DebugLogger.Instance.LogWarning($"{WarningCodes.NULL_OOB_ASSET}: Tried to update embedded asset reference in file, but the out of band asset is null.");
                return;
            }

            if (loadedFileReference == null)
            {
                DebugLogger.Instance.LogWarning($"{WarningCodes.FILE_NOT_LOADED}: Tried to update embedded asset reference in file, but the file has not been loaded yet.");
                return;
            }

            if (loadedFileReference.TryGetTarget(out Rive.File file))
            {
                file.UpdateEmbeddedAssetReference(this.IndexInRiveFile, outOfBandAsset);
            }
            else
            {
                loadedFileReference = null;
                DebugLogger.Instance.LogWarning($"{WarningCodes.FILE_RELEASED}: Tried to update embedded asset reference in file, but the file has already been released.");
            }



        }

        /// <summary>
        /// Checks if the embedded asset reference has a file reference set.
        /// </summary>
        /// <returns></returns>
        public bool HasFileReference()
        {
            return loadedFileReference != null && loadedFileReference.TryGetTarget(out Rive.File file);
        }



    }


}
