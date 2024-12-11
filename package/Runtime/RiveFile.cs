using UnityEngine;
using System;
using System.Runtime.InteropServices;
using Rive.Utils;

namespace Rive
{

    /// <summary>
    /// Represents a Rive File containing Artboards, StateMachines, and Animations.
    /// </summary>
    /// <remarks>
    /// There are higher level behaviours that you can use directly in the Unity editor. Use this class if you need direct control of the lifecycle of the Rive File.
    /// </remarks>
    public class File : IDisposable
    {
        internal static class LogCodes
        {

            public const string ERROR_NO_ARTBOARD_FOUND = "1001";

            public const string ERROR_ASSET_REFERENCE_UPDATE_FAILED = "1002";

            public const string ERROR_INVALID_FILE_ACCESS = "1003";


        }

        private IntPtr m_nativeFile;
        private readonly int? m_assetKey;
        private bool m_isDisposed = false;

        internal IntPtr NativeFile
        {
            get { return m_nativeFile; }
        }

        /// <summary>
        /// Returns true if the file has been disposed.
        /// </summary>
        public bool IsDisposed { get => m_isDisposed; }

        internal bool IsNativeFileValid()
        {
            if (m_isDisposed)
            {
                DebugLogger.Instance.LogError($"{LogCodes.ERROR_INVALID_FILE_ACCESS}: - Attempting to access a Rive file that is no longer referenced or was not properly loaded.");
                return false;
            }
            return true;
        }



        private static FileLoader s_fileLoader = new FileLoader();


        /// <summary>
        /// Use this to load custom assets from the Rive file. If you return an OutOfBandAsset, it will be used to load the asset. If you return null, then nothing will be loaded.
        /// </summary>
        /// <param name="assetData"> The EmbeddedAssetReference data. </param>
        /// <returns> Return true if you intend on handling and loading in an asset yourself, or false if you do not want to handle asset loading for that given asset yourself, and attempt to have the runtime try to load the asset. </returns>
        public delegate bool CustomAssetLoaderCallback(EmbeddedAssetReference assetReference);

        private IFallbackFileAssetLoader m_fallbackFileAssetLoader;

        internal IFallbackFileAssetLoader FallbackFileAssetLoader
        {
            get { return m_fallbackFileAssetLoader; }
        }

        internal int? AssetKey
        {
            get { return m_assetKey; }
        }

        /// <summary>
        /// Dispose of the Rive File.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }


        protected virtual void Dispose(bool disposing)
        {
            if (!m_isDisposed)
            {
                // If the file was loaded with a cache key, we need to decrement the ref count and/or unload the file if it's no longer in use.

                if (m_assetKey.HasValue)
                {
                    if (!disposing)
                    {
                        // Called from the finalizer; remove the file from the cache to account for the user not manually disposing of it.
                        s_fileLoader.ReleaseFile(this);
                    }
                    else
                    {
                        // Manual disposal (called from Dispose())
                        bool isStillReferenced = s_fileLoader.DecrementRefCount(m_assetKey.Value);

                        if (isStillReferenced)
                        {
                            // If the file is still being referenced, don't mark it as disposed yet
                            return;
                        }

                    }
                }
                else
                {
                    // For non-cached files, unref directly
                    s_fileLoader.ReleaseFile(this);

                }
                m_nativeFile = IntPtr.Zero;
                m_isDisposed = true;
            }
        }



        /// <summary>
        /// Load a .riv File from a byte array and cache it using the provided ID.
        /// </summary>
        /// <param name="name">The name of the file.</param>
        /// <param name="riveFileByteContents">The raw bytes of the Rive file.</param>
        /// <param name="cacheId">The unique identifier of the file. This is used to cache and reuse the file if it's loaded multiple times with the same ID.</param>
        /// <returns>A File instance representing the loaded Rive file, either from cache or newly created.</returns>
        /// <remarks>
        /// This method will return a cached version if the file with the provided ID was already loaded, otherwise it loads and caches a new instance in memory.
        /// </remarks>
        public static File Load(byte[] riveFileByteContents, int cacheId)
        {
            return s_fileLoader.LoadWithKnownAssets(riveFileByteContents, cacheId, null);
        }



        /// <summary>
        /// Load a .riv File from a byte array using a custom asset loader.
        /// </summary>
        /// <param name="riveFileByteContents">The raw bytes of the Rive file, e.g. fetched from a remote server.</param>
        /// <param name="customAssetLoaderCallback">This callback will be called for every asset the runtime detects from the .riv file on load, and will be responsible for either handling the load of an asset at runtime or passing on the responsibility and giving the runtime a chance to load it otherwise. </param>
        /// <returns>A new File instance representing the loaded Rive file.</returns>
        /// <remarks>
        /// This method always creates and returns a new File instance, bypassing any caching mechanism. 
        /// Use this when you need custom asset loading behavior, but be aware that you'll need to manage 
        /// caching/reusing the instance yourself if required.
        /// </remarks>
        public static File Load(byte[] riveFileByteContents, CustomAssetLoaderCallback customAssetLoaderCallback)
        {
            return s_fileLoader.LoadFileWithCallback(riveFileByteContents, customAssetLoaderCallback);
        }


        /// <summary>
        /// Load a Rive File from a Unity TextAsset. Make sure the imported asset ends with the .bytes extension.
        /// </summary>
        /// <param name="asset">The TextAsset to load.</param>
        /// <param name="customAssetLoaderCallback">This callback will be called for every asset the runtime detects from the .riv file on load, and will be responsible for either handling the load of an asset at runtime or passing on the responsibility and giving the runtime a chance to load it otherwise. </param>
        /// <returns>A File instance representing the loaded Rive file.</returns>
        /// <remarks>
        /// Always creates and returns a new File instance, even if the same asset had been loaded before. Use this when you need custom asset loading behavior, but be aware that you'll need to manage caching/reusing the instance yourself if required.
        /// </remarks>     
        static public File Load(TextAsset asset, CustomAssetLoaderCallback customAssetLoaderCallback)
        {
            return s_fileLoader.LoadFileWithCallback(asset.bytes, customAssetLoaderCallback);
        }

        /// <summary>
        /// Load a Rive File from a Unity TextAsset. Make sure the imported asset ends with the .bytes extension.
        /// </summary>
        /// <param name="asset">The TextAsset to load.</param>
        /// <returns>A File instance representing the loaded Rive file.</returns>
        /// <remarks>
        /// Returns a cached version if the asset was already loaded, otherwise loads and caches a new instance in memory.
        /// </remarks>
        static public File Load(TextAsset asset)
        {
            // With text assets, we don't have the embedded asset data ahead of time, but we still want them to be cached if the same asset is loaded multiple times.
            return s_fileLoader.LoadWithKnownAssets(asset.bytes, asset.GetInstanceID(), null);
        }

        /// <summary>
        /// Load a .riv File from a Unity Rive.Asset.
        /// </summary>
        /// <param name="asset">The Rive.Asset (.riv) to load.</param>
        /// <returns>A File instance representing the loaded Rive file.</returns>
        /// <remarks>
        /// Returns a cached version if the asset was already loaded, otherwise loads and caches a new instance in memory.
        /// </remarks>
        static public File Load(Asset asset)
        {
            return s_fileLoader.LoadWithKnownAssets(asset.Bytes, asset.GetInstanceID(), asset.EmbeddedAssets);
        }

        /// <summary>
        /// Load a .riv File from a Unity Rive.Asset.
        /// </summary>
        /// <param name="asset">The Rive.Asset (.riv) to load.</param>
        /// <param name="customAssetLoaderCallback">This callback will be called for every asset the runtime detects from the .riv file on load, and will be responsible for either handling the load of an asset at runtime or passing on the responsibility and giving the runtime a chance to load it otherwise. </param>
        /// <returns>A File instance representing the loaded Rive file.</returns>
        /// <remarks>
        /// Always creates and returns a new File instance, even if the same asset had been loaded before. Use this when you need custom asset loading behavior, but be aware that you'll need to manage caching/reusing the instance yourself if required.
        /// </remarks>
        static public File Load(Asset asset, CustomAssetLoaderCallback customAssetLoaderCallback)
        {
            return s_fileLoader.LoadFileWithCallback(asset.Bytes, customAssetLoaderCallback);
        }


        /// <summary>
        /// Update the embedded asset reference data in the Rive file. The out-of-band asset must be loaded before calling this method.
        /// </summary>
        /// <param name="assetIndex"></param>
        /// <param name="asset"></param>
        internal void UpdateEmbeddedAssetReference(uint assetIndex, OutOfBandAsset asset)
        {
            if (asset == null)
            {
                DebugLogger.Instance.LogError($"{LogCodes.ERROR_ASSET_REFERENCE_UPDATE_FAILED}: - The provided out-of-band asset is null. Make sure to load the out-of-band asset before updating the reference.");
                return;
            }

            if (asset.NativeAsset == IntPtr.Zero)
            {
                DebugLogger.Instance.LogError($"{LogCodes.ERROR_ASSET_REFERENCE_UPDATE_FAILED}: - The provided asset is not loaded in memory. Make sure to load the out-of-band asset before updating the reference.");
                return;
            }

            bool wasUpdated = NativeFileInterface.updateEmbeddedAssetReferenceInFile(NativeFile, assetIndex, asset.NativeAsset);

            if (!wasUpdated)
            {
                DebugLogger.Instance.LogError($"{LogCodes.ERROR_ASSET_REFERENCE_UPDATE_FAILED}: - Asset reference with index {assetIndex} not found in the file. Could the Rive file you loaded be different from the one you are trying to update?");
            }


        }


        internal File(IntPtr nativeFile, int? assetKey, IFallbackFileAssetLoader fallbackFileAssetLoader)
        {
            m_nativeFile = nativeFile;
            m_assetKey = assetKey;
            m_fallbackFileAssetLoader = fallbackFileAssetLoader;
        }

        ~File()
        {
            // If the native file was not manually released then we need to dispose of it here when the managed object is garbage collected.
            Dispose(false);


        }

        ///<summary>
        /// Returns the number of artboards stored in the file.
        /// </summary>        
        public uint ArtboardCount
        {
            get
            {
                if (!IsNativeFileValid()) return 0;
                return NativeFileInterface.getArtboardCount(NativeFile);
            }
        }

        /// <summary>
        /// Returns the name of the artboard at the given index.
        /// </summary>
        /// <param name="index"></param>
        public string ArtboardName(uint index)
        {
            if (!IsNativeFileValid()) return null;
            return Marshal.PtrToStringAnsi(NativeFileInterface.getArtboardName(NativeFile, index));
        }

        /// <summary>
        /// Instance the artboard at the given index.
        /// </summary>
        /// <param name="index">The index of the artboard to get.</param>
        /// <returns> An instance of the artboard at the given index or null if the index is out of bounds.</returns>
        public Artboard Artboard(uint index)
        {
            if (!IsNativeFileValid()) return null;
            IntPtr ptr = NativeFileInterface.instanceArtboardAtIndex(NativeFile, index);
            if (ptr == IntPtr.Zero)
            {
                DebugLogger.Instance.LogError($"{LogCodes.ERROR_NO_ARTBOARD_FOUND}: - No Artboard found at index {index}. Could the Rive file you loaded be different from the one you are trying to access? Or is the index possibly out of bounds?");
                return null;
            }
            return new Artboard(ptr);
        }


        /// <summary>
        /// Instance the artboard with the given name.
        /// </summary>
        /// <param name="name">The name of the artboard to get.</param>
        /// <returns> An instance of the artboard with the given name, or null if no artboard with the given name exists.</returns>
        public Artboard Artboard(string name)
        {
            if (!IsNativeFileValid()) return null;
            IntPtr ptr = NativeFileInterface.instanceArtboardWithName(NativeFile, name);
            if (ptr == IntPtr.Zero)
            {
                DebugLogger.Instance.LogError($"{LogCodes.ERROR_NO_ARTBOARD_FOUND}: - No Artboard named \"{name}\". It's possible the name is misspelled or the file does not contain the named artboard.");
                return null;
            }
            return new Artboard(ptr);
        }


    }

    /// <summary>
    /// Provides a way to work with the native file interface methods.
    /// </summary>
    internal class NativeFileInterface
    {
        internal static IFallbackFileAssetLoader s_NativeFileAssetLoader;

        internal delegate IntPtr NativeUnityAssetLoaderCallback(uint assetId, ushort assetType, string assetName, uint inBandByteSize);

        #region Native Methods

        [DllImport(NativeLibrary.name)]
        internal static extern IntPtr loadRiveFile(byte[] bytes, uint byteCount, byte[] assetMapBytes, uint assetMapByteCount);

        [DllImport(NativeLibrary.name)]
        internal static extern void unrefRiveFile(IntPtr riveFile);

        [DllImport(NativeLibrary.name)]
        internal static extern bool isRiveFileValid(IntPtr riveFile);
        [DllImport(NativeLibrary.name)]
        internal static extern uint getArtboardCount(IntPtr riveFile);

        [DllImport(NativeLibrary.name)]
        internal static extern IntPtr getArtboardName(IntPtr riveFile, uint index);

        [DllImport(NativeLibrary.name)]
        internal static extern IntPtr instanceArtboardAtIndex(IntPtr riveFile, uint index);

        [DllImport(NativeLibrary.name)]
        internal static extern IntPtr instanceArtboardWithName(IntPtr riveFile, string name);

        [DllImport(NativeLibrary.name)]
        internal static extern bool updateEmbeddedAssetReferenceInFile(IntPtr riveFile, uint assetIndex, IntPtr decodedAsset);

        [DllImport(NativeLibrary.name)]
        internal static extern IntPtr loadRiveFileWithUnityCallback(byte[] riveFileBytes, uint byteCount, NativeUnityAssetLoaderCallback callback);

        /// <summary>
        /// The callback that is called by the native code to load the assets. It should return the native asset pointer to the native code. Don't call any Rive methods from this callback as it may lead to a crash due to deadlocks.
        /// </summary>
        /// <param name="assetId"> The unique ID of the asset. </param>
        /// <param name="assetType"> The type of the asset. </param>
        /// <param name="assetName"> The name of the asset. </param>
        /// <param name="inBandByteSize"> The size of the asset embedded in the file. </param>
        /// <returns> The native asset pointer to set in the Rive file. </returns>
        [AOT.MonoPInvokeCallback(typeof(NativeUnityAssetLoaderCallback))]
        internal static IntPtr AssetLoaderCallback(uint assetId, ushort assetType, string assetName, uint inBandByteSize)
        {


            if (s_NativeFileAssetLoader != null)
            {
                return s_NativeFileAssetLoader.NativeUnityAssetLoaderCallback(assetId, assetType, assetName, inBandByteSize);
            }

            return IntPtr.Zero;
        }
        #endregion

    }
}
