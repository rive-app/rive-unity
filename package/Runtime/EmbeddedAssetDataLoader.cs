using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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


            IntPtr listPtr = IntPtr.Zero;
            try
            {
                listPtr = loadEmbeddedAssetList(riveFileBytes, (nuint)riveFileBytes.Length);
                nuint assetCount = getEmbeddedAssetCount(listPtr);

                for (nuint i = 0; i < assetCount; i++)
                {
                    string name = Marshal.PtrToStringAnsi(getEmbeddedAssetName(listPtr, i));
                    ushort type = getEmbeddedAssetType(listPtr, i);
                    var assetType = Enum.IsDefined(typeof(EmbeddedAssetType), type)
                            ? (EmbeddedAssetType)type
                            : EmbeddedAssetType.Unknown;
                    uint id = getEmbeddedAssetId(listPtr, i);
                    uint embeddedBytes = (uint)getEmbeddedAssetSize(listPtr, i);
                    var embeddedAsset = new EmbeddedAssetData(assetType, id, name, embeddedBytes);

                    yield return embeddedAsset;
                }
            }
            finally
            {
                if (listPtr != IntPtr.Zero)
                {
                    deleteEmbeddedAssetList(listPtr);
                }
            }
        }


        #region Native Methods
        [DllImport(NativeLibrary.name)]
        internal static extern IntPtr loadEmbeddedAssetList(byte[] bytes, nuint byteCount);

        [DllImport(NativeLibrary.name)]
        internal static extern void deleteEmbeddedAssetList(IntPtr list);

        [DllImport(NativeLibrary.name)]
        internal static extern nuint getEmbeddedAssetCount(IntPtr list);

        [DllImport(NativeLibrary.name)]
        internal static extern IntPtr getEmbeddedAssetName(IntPtr riveFile, nuint index);

        [DllImport(NativeLibrary.name)]
        internal static extern ushort getEmbeddedAssetType(IntPtr riveFile, nuint index);

        [DllImport(NativeLibrary.name)]
        internal static extern uint getEmbeddedAssetId(IntPtr riveFile, nuint index);

        [DllImport(NativeLibrary.name)]
        internal static extern nuint getEmbeddedAssetSize(IntPtr riveFile, nuint index);
        #endregion
    }
}