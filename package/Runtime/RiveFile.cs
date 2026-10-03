using UnityEngine;
using System;
using System.Runtime.InteropServices;
using Rive.Producer;
using Rive.Utils;
using System.Collections.Generic;
using System.Threading.Tasks;
using Rive.Host;

namespace Rive
{

    /// <summary>
    /// Represents a Rive File containing Artboards, StateMachines, and Animations.
    /// </summary>
    /// <remarks>
    /// There are higher level behaviours that you can use directly in the Unity editor. Use this class if you need direct control of the lifecycle of the Rive File.
    /// </remarks>
    public class File : ILoadedFile
    {
        internal static class LogCodes
        {

            public const string ERROR_NO_ARTBOARD_FOUND = "1001";

            public const string ERROR_ASSET_REFERENCE_UPDATE_FAILED = "1002";

            public const string ERROR_INVALID_FILE_ACCESS = "1003";


        }

        private NativeFileHandle m_nativeFile;
        private readonly FileContents m_contents;
        private readonly long? m_assetKey;
        private readonly NativeLifetime m_lifetime;
        private bool m_isDisposed = false;

        private ViewModel[] m_viewModels;

        private ViewModelEnumData[] m_viewModelEnums;

        private string[] m_globalViewModelNames;



        internal NativeFileHandle NativeFile
        {
            get { return m_nativeFile; }
        }

        /// Deletes the native file once the File and every artboard made from
        /// it let go.
        internal NativeLifetime Lifetime => m_lifetime;

        /// Everything name shaped in the file, read when it loaded.
        internal FileContents Contents => m_contents;

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

        /// The loader behind File.Load. FileHandle and the widget load through
        /// it without waiting, and share its cache.
        internal static FileLoader Loader => s_fileLoader;


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

        internal long? AssetKey
        {
            get { return m_assetKey; }
        }

        /// <summary>
        /// The view models in the file.
        /// </summary>
        public IReadOnlyList<ViewModel> ViewModels
        {
            get
            {
                if (m_viewModels == null)
                {
                    LoadViewModels();
                }
                return m_viewModels;
            }
        }

        /// <summary>
        /// The enums in the file.
        /// </summary>
        public IReadOnlyList<ViewModelEnumData> ViewModelEnums
        {
            get
            {

                if (m_viewModelEnums == null)
                {
                    m_viewModelEnums = GetViewModelEnums();
                }
                return m_viewModelEnums;
            }
        }

        /// <summary>
        /// The names of the file's global view models, in file order. These are the keys accepted by
        /// <see cref="StateMachine.BindViewModelInstance(ViewModelInstance, IReadOnlyDictionary{string, ViewModelInstance})"/>
        /// and <see cref="StateMachine.GetGlobalViewModelInstance"/>.
        /// </summary>
        public IReadOnlyList<string> GlobalViewModelNames
        {
            get
            {
                if (m_globalViewModelNames == null)
                {
                    m_globalViewModelNames = LoadGlobalViewModelNames();
                }
                return m_globalViewModelNames;
            }
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
                m_nativeFile = default;
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
            return s_fileLoader.LoadWithKnownAssets(asset.bytes, ObjectHelper.GetInstanceId(asset), null);
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
            return s_fileLoader.LoadWithKnownAssets(asset.Bytes, ObjectHelper.GetInstanceId(asset), asset.EmbeddedAssets);
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
        /// Load a .riv File from a Unity Rive.Asset.
        /// </summary>
        /// <param name="asset"> The Rive.Asset (.riv) to load. </param>
        /// <param name="customAssetLoaderCallback"> This callback will be called for every asset the runtime detects from the .riv file on load, and will be responsible for either handling the load of an asset at runtime or passing on the responsibility and giving the runtime a chance to load it otherwise. </param>
        /// <param name="fallbackToAssignedAssets"> If true, the runtime will attempt to load the assets assigned to the Rive.Asset in the Unity inspector if an asset is not handled by the custom loader. </param>
        /// <returns> A File instance representing the loaded Rive file. </returns>
        /// <remarks>
        /// Always creates and returns a new File instance, even if the same asset had been loaded before. Use this when you need custom asset loading behavior, but be aware that you'll need to manage caching/reusing the instance yourself if required.
        /// </remarks>
        static public File Load(Asset asset, CustomAssetLoaderCallback customAssetLoaderCallback, bool fallbackToAssignedAssets)
        {
            IEnumerable<EmbeddedAssetData> fallbackAssetData = fallbackToAssignedAssets ? asset.EmbeddedAssets : null;
            return s_fileLoader.LoadFileWithCallback(asset.Bytes, customAssetLoaderCallback, fallbackAssetData);
        }


        /// <summary>
        /// Update the embedded asset reference data in the Rive file. The out-of-band asset must be loaded before calling this method.
        /// </summary>
        /// <param name="assetId"></param>
        /// <param name="asset"></param>
        internal void UpdateEmbeddedAssetReference(uint assetId, OutOfBandAsset asset)
        {
            if (asset == null)
            {
                DebugLogger.Instance.LogError($"{LogCodes.ERROR_ASSET_REFERENCE_UPDATE_FAILED}: - The provided out-of-band asset is null. Make sure to load the out-of-band asset before updating the reference.");
                return;
            }

            if (!asset.NativeHandle.IsValid)
            {
                DebugLogger.Instance.LogError($"{LogCodes.ERROR_ASSET_REFERENCE_UPDATE_FAILED}: - The provided asset is not loaded in memory. Make sure to load the out-of-band asset before updating the reference.");
                return;
            }

            bool wasUpdated = FileNative.UpdateAsset(NativeFile, assetId, asset.NativeHandle);

            if (!wasUpdated)
            {
                DebugLogger.Instance.LogError($"{LogCodes.ERROR_ASSET_REFERENCE_UPDATE_FAILED}: - Asset reference with id {assetId} not found in the file. Could the Rive file you loaded be different from the one you are trying to update?");
            }


        }



        internal File(NativeFileHandle nativeFile, FileContents contents, long? assetKey, IFallbackFileAssetLoader fallbackFileAssetLoader)
        {
            m_nativeFile = nativeFile;
            m_lifetime = new NativeLifetime(() => FileNative.Delete(nativeFile));
            m_contents = contents;
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
                return (uint)m_contents.Artboards.Length;
            }
        }

        /// <summary>
        /// Returns the name of the artboard at the given index.
        /// </summary>
        /// <param name="index"></param>
        public string ArtboardName(uint index)
        {
            if (!IsNativeFileValid()) return null;
            return index < m_contents.ArtboardNames.Length ? m_contents.ArtboardNames[index] : null;
        }

        private static void ReportMissingArtboardAt(uint index)
        {
            DebugLogger.Instance.LogError($"{LogCodes.ERROR_NO_ARTBOARD_FOUND}: - No Artboard found at index {index}. Could the Rive file you loaded be different from the one you are trying to access? Or is the index possibly out of bounds?");
        }

        /// With no name it's the first artboard that was missing.
        internal void ReportMissingArtboard(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                ReportMissingArtboardAt(0);
                return;
            }
            DebugLogger.Instance.LogError($"{LogCodes.ERROR_NO_ARTBOARD_FOUND}: - No Artboard named \"{name}\". It's possible the name is misspelled or the file does not contain the named artboard.");
        }

        /// <summary>
        /// Instance the artboard at the given index.
        /// </summary>
        /// <param name="index">The index of the artboard to get.</param>
        /// <returns> An instance of the artboard at the given index or null if the index is out of bounds.</returns>
        public Artboard Artboard(uint index)
        {
            if (!IsNativeFileValid()) return null;
            NativeArtboardHandle ptr = index < m_contents.Artboards.Length
                ? ArtboardNative.Instantiate(NativeFile, m_contents.Artboards[index].Name)
                : default;
            if (!ptr.IsValid)
            {
                ReportMissingArtboardAt(index);
                return null;
            }
            return new Artboard(ptr, this, m_contents.Artboards[index]);
        }


        /// <summary>
        /// Instance the artboard with the given name.
        /// </summary>
        /// <param name="name">The name of the artboard to get.</param>
        /// <returns> An instance of the artboard with the given name, or null if no artboard with the given name exists.</returns>
        public Artboard Artboard(string name)
        {
            if (!IsNativeFileValid()) return null;
            int index = m_contents.ArtboardIndex(name);
            NativeArtboardHandle ptr = index >= 0 ? ArtboardNative.Instantiate(NativeFile, name) : default;
            if (!ptr.IsValid)
            {
                ReportMissingArtboard(name);
                return null;
            }
            return new Artboard(ptr, this, m_contents.Artboards[index]);
        }


        /// <summary>
        /// Returns a bindable artboard with the given name.
        /// </summary>
        /// <param name="name">The name of the artboard.</param>
        /// <returns>A BindableArtboard instance, or null if not found.</returns>
        public BindableArtboard BindableArtboard(string name)
        {
            return BindableArtboard(name, null);
        }

        /// <summary>
        /// Returns a bindable artboard with the given name and bound ViewModel instance.
        /// </summary>
        /// <param name="name">The name of the artboard.</param>
        /// <param name="viewModelInstance">The ViewModel instance to bind to the artboard.</param>
        /// <returns>A BindableArtboard instance, or null if not found.</returns>
        public BindableArtboard BindableArtboard(string name, ViewModelInstance viewModelInstance)
        {
            if (name == null)
            {
                DebugLogger.Instance.LogError("Invalid name: " + name);
                return null;
            }

            NativeArtboardHandle handle = ArtboardNative.Instantiate(m_nativeFile, name);
            if (!handle.IsValid)
            {
                DebugLogger.Instance.Log($"No bindable artboard named \"{name}\".");
                return null;
            }

            return new BindableArtboard(handle, name, viewModelInstance);
        }


        // Data binding methods

        public int ViewModelCount
        {
            get
            {
                if (!IsNativeFileValid()) return 0;
                return m_contents.ViewModels.Length;
            }
        }

        /// <summary>
        /// Get a view model by index in the file.
        /// </summary>
        /// <param name="index"> The index of the view model to get. </param>
        /// <returns> The view model at the given index. </returns>
        public ViewModel GetViewModelAtIndex(int index)
        {
            if (index < 0)
            {
                DebugLogger.Instance.LogError("Invalid index: " + index);
                return null;
            }
            if (!IsNativeFileValid()) return null;


            if (index >= m_contents.ViewModels.Length)
            {
                DebugLogger.Instance.LogError("Index out of bounds: " + index);
                return null;
            }

            return new ViewModel(this, index);
        }

        /// <summary>
        /// Get a view model by name in the file.
        /// </summary>
        /// <param name="name"> The name of the view model to get. </param>
        /// <returns> The view model with the given name. </returns>
        public ViewModel GetViewModelByName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                DebugLogger.Instance.LogError("Invalid name: " + name);
                return null;
            }
            if (!IsNativeFileValid()) return null;
            int index = m_contents.ViewModelIndex(name);
            if (index < 0)
            {
                DebugLogger.Instance.LogError("Failed to get view model with name: " + name);
                return null;
            }
            return new ViewModel(this, index);
        }



        private void LoadViewModels()
        {
            if (m_viewModels != null)
            {
                return;
            }

            nuint count = (nuint)ViewModelCount;
            m_viewModels = new ViewModel[count];
            for (nuint i = 0; i < count; i++)
            {
                m_viewModels[i] = new ViewModel(this, (int)i);
            }
        }

        private string[] LoadGlobalViewModelNames()
        {
            if (!IsNativeFileValid())
            {
                return Array.Empty<string>();
            }

            return (string[])m_contents.GlobalViewModelNames.Clone();
        }


        private ViewModelEnumData[] GetViewModelEnums()
        {

            return (ViewModelEnumData[])m_contents.Enums.Clone();
        }

        /// Null for an index below 0.
        internal ViewModel ViewModelAt(int index)
        {
            return index < 0 ? null : new ViewModel(this, index);
        }

    }

    /// <summary>
    /// Provides a way to work with the native file interface methods.
    /// </summary>
    internal class NativeFileInterface
    {
        /// True while the native file is still loaded.
        internal static bool IsRiveFileValid(NativeFileHandle riveFile)
        {
            return FileNative.IsAlive(riveFile);
        }
    }
}
