using System;
using Rive.Utils;

namespace Rive
{


    /// <summary>
    /// Represents a file asset within a specific Rive file. 
    /// </summary>
    public abstract class EmbeddedAssetReference
    {

        internal struct InitializationData
        {
            public EmbeddedAssetType AssetType { get; }
            public uint Id { get; }
            public string Name { get; }
            public uint InBandBytesSize { get; }
            public OutOfBandAsset OutOfBandAsset { get; }
            public uint IndexInRiveFile { get; }

            public InitializationData(EmbeddedAssetType assetType, uint id, string name, uint inBandBytesSize, uint indexInRiveFile, OutOfBandAsset outOfBandAsset)
            {
                AssetType = assetType;
                Id = id;
                Name = name;
                InBandBytesSize = inBandBytesSize;
                OutOfBandAsset = outOfBandAsset;
                IndexInRiveFile = indexInRiveFile;
            }

            public static InitializationData FromEmbeddedAssetData(EmbeddedAssetData embeddedAssetData, uint index)
            {
                return new InitializationData(embeddedAssetData.AssetType, embeddedAssetData.Id, embeddedAssetData.Name, embeddedAssetData.InBandBytesSize, index, embeddedAssetData.OutOfBandAsset);
            }
        }

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
        [Obsolete("Use OutOfBandAsset instead.")]
        public OutOfBandAsset OutOfBandAssetToLoad { get { return m_OutOfBandAssetToLoad; } }

        /// <summary>
        /// The index of the embedded/referenced asset in the Rive file.
        /// </summary>
        [Obsolete]
        public uint IndexInRiveFile { get { return m_Index; } }

        /// <summary>
        /// The index of the embedded/referenced asset in the Rive file.
        /// </summary>
        internal uint Index { get { return m_Index; } }

        public OutOfBandAsset OutOfBandAsset { get { return m_OutOfBandAssetToLoad; } }


        [Obsolete]
        public EmbeddedAssetReference(EmbeddedAssetData embeddedAssetData, uint index)
        {
            m_assetType = embeddedAssetData.AssetType;
            m_Id = embeddedAssetData.Id;
            m_Name = embeddedAssetData.Name;
            m_EmbeddededBytesSize = embeddedAssetData.InBandBytesSize;
            m_Index = index;
            m_OutOfBandAssetToLoad = embeddedAssetData.OutOfBandAsset;
        }

        [Obsolete]
        public EmbeddedAssetReference(EmbeddedAssetType assetType, uint id, string name, uint embeddededBytesSize, uint indexInRiveFile, OutOfBandAsset outOfBandAsset)
        {
            m_assetType = assetType;
            m_Id = id;
            m_Name = name;
            m_EmbeddededBytesSize = embeddededBytesSize;
            m_Index = indexInRiveFile;
            m_OutOfBandAssetToLoad = outOfBandAsset;
        }





        internal EmbeddedAssetReference(InitializationData initializationData)
        {
            m_assetType = initializationData.AssetType;
            m_Id = initializationData.Id;
            m_Name = initializationData.Name;
            m_EmbeddededBytesSize = initializationData.InBandBytesSize;
            m_Index = initializationData.IndexInRiveFile;
            m_OutOfBandAssetToLoad = initializationData.OutOfBandAsset;
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
                file.UpdateEmbeddedAssetReference(m_Index, outOfBandAsset);
                m_OutOfBandAssetToLoad = outOfBandAsset;

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
        internal bool HasFileReference()
        {
            return loadedFileReference != null && loadedFileReference.TryGetTarget(out Rive.File file);
        }


        /// <summary>
        /// Empties the embedded asset reference value in the Rive file. This is used if the asset at this index had embedded bytes. 
        /// </summary>
        internal void ClearEmbeddedAssetReference()
        {
            if (loadedFileReference == null)
            {
                DebugLogger.Instance.LogWarning($"{WarningCodes.FILE_NOT_LOADED}: Tried to clear embedded asset reference in file, but the file has not been loaded yet.");
                return;
            }

            if (loadedFileReference.TryGetTarget(out Rive.File file))
            {
                NativeFileInterface.clearAssignedAssetReferenceValue(file.NativeFile, m_Index);

            }
            else
            {
                loadedFileReference = null;
                DebugLogger.Instance.LogWarning($"{WarningCodes.FILE_RELEASED}: Tried to clear embedded asset reference in file, but the file has already been released.");
            }

        }


    }


}
