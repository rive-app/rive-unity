using System;
using System.Collections.Generic;
using Rive.Utils;


namespace Rive
{
    /// <summary>
    /// Utility class for loading embedded asset data from a Rive file's byte data.
    /// </summary>
    internal class EmbeddedAssetDataLoader
    {
        public const string ERROR_CODE_RIVE_FILE_BYTES_NULL_OR_EMPTY = "RIVE_FILE_BYTES_NULL_OR_EMPTY";


        public EmbeddedAssetDataLoader()
        {
        }


        /// <summary>
        /// Load the embedded assets from a Rive file's byte data.
        /// </summary>
        /// <param name="riveFileBytes"> The bytes of the Rive file. </param>
        /// <returns> An enumerable of embedded assets. </returns>
        public IEnumerable<EmbeddedAssetData> LoadEmbeddedAssetDataFromRiveFileBytes(byte[] riveFileBytes)
        {
            if (riveFileBytes == null || riveFileBytes.Length == 0)
            {
                DebugLogger.Instance.LogError(ERROR_CODE_RIVE_FILE_BYTES_NULL_OR_EMPTY + " - The Rive file bytes are null or empty.");
                yield break;
            }

            NativeUsageGuard.ThrowIfNativeUnavailable();

            FileContents.AssetInfo[] assets = FileNative.ListAssets(riveFileBytes);
            if (assets == null)
            {
                yield break;
            }
            foreach (FileContents.AssetInfo asset in assets)
            {
                var assetType = Enum.IsDefined(typeof(EmbeddedAssetType), asset.Type)
                        ? (EmbeddedAssetType)asset.Type
                        : EmbeddedAssetType.Unknown;
                if (assetType == EmbeddedAssetType.Manifest)
                {
                    continue;
                }
                yield return new EmbeddedAssetData(assetType, asset.Id, asset.Name, asset.EmbeddedBytes);
            }
        }
    }
}