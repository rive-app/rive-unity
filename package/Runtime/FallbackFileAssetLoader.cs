using System;
using System.Collections.Generic;
using Rive.Utils;


namespace Rive
{

    internal interface IFallbackFileAssetLoader
    {
        void AddLoader(IFileAssetLoader loader);
        IntPtr NativeUnityAssetLoaderCallback(uint assetId, ushort assetType, string assetName, uint inBandByteSize);
        void LoadOutOfBandAssets(File riveFile);
        void UnloadInternallyLoadedAssets();
    }



    /// <summary>
    /// The FallbackFileAssetLoader is the brain of the asset loading system. It is responsible for loading embedded assets from a Rive file and keeping track of which assets it loaded. It also accepts a list of IFileAssetLoader instances to load the assets. If an asset is not loaded by any of the loaders, then it will handle the loading itself.
    /// </summary>
    internal class FallbackFileAssetLoader : IFileAssetLoader, IFallbackFileAssetLoader
    {
        public static class LogCodes
        {
            public const string ERROR_UNSUPPORTED_ASSET_TYPE = "UNSUPPORTED_ASSET_TYPE";
            public const string ERROR_ASSET_DATA_SET_FAILED = "FAILED_TO_SET_ASSET_DATA";

            public const string ERROR_ASSET_TYPE_MISMATCH = "ASSET_TYPE_MISMATCH";

            public const string ERROR_ASSET_LOADING_FAILED = "ASSET_LOADING_ERROR";

            public const string ERROR_NULL_LOADER = "NULL_LOADER_PROVIDED";

        }
        private List<IFileAssetLoader> loaders = new List<IFileAssetLoader>();

        public List<IFileAssetLoader> Loaders => loaders;

        /// <summary>
        /// A map of the out of band assets that this loader was responsible for loading/decoding. The key is the ID of the embedded asset. This is used to unload the assets when the Rive file is unloaded.
        /// </summary>
        Dictionary<uint, OutOfBandAsset> m_loadedOobAssets = new();


        /// <summary>
        /// A map of referenced out of band assets to load if the the user doesn't handle the loading themselves with the callback approach. We mostly need to use this for situations where the user is loading from a Rive Asset which has referenced images assigned in the Unity inspector.
        /// In this case, some users might want the assigned values to be used if they don't handle the loading themselves, so this lets us handle that.
        /// </summary>
        Dictionary<uint, OutOfBandAsset> m_fallbackReferenceAssetMap;


        public void AddLoader(IFileAssetLoader loader)
        {
            if (loader == null)
            {
                DebugLogger.Instance.LogError($"{LogCodes.ERROR_NULL_LOADER}: Can't add null to the list of loaders.");
                return;
            }
            loaders.Add(loader);
        }


        /// <summary>
        /// Stores a mapping of asset id to asset reference. This is used to load the asset data when the Rive file is loaded.
        /// </summary>
        private Dictionary<uint, EmbeddedAssetReference> assetReferenceMap = new Dictionary<uint, EmbeddedAssetReference>();


        public FallbackFileAssetLoader()
        {
        }


        public FallbackFileAssetLoader(IEnumerable<EmbeddedAssetData> fallbackReferenceAssetData)
        {

            if (fallbackReferenceAssetData == null)
            {
                return;
            }

            m_fallbackReferenceAssetMap = new Dictionary<uint, OutOfBandAsset>();

            foreach (var assetData in fallbackReferenceAssetData)
            {
                if (assetData.OutOfBandAsset != null)
                {
                    m_fallbackReferenceAssetMap[assetData.Id] = assetData.OutOfBandAsset;
                }
            }
        }



        /// <summary>
        /// Generate the asset map bytes from the preloaded cache. This is used to load the assets when the Rive file is loaded with the asset data known ahead of time or without a callback.
        /// </summary>
        /// <returns> The asset map bytes. </returns>
        public byte[] GenerateAssetMapBytesFromEmbeddedAssets(IEnumerable<EmbeddedAssetData> embeddedAssetData)
        {
            List<byte> assetMap = new List<byte>();

            if (embeddedAssetData == null)
            {
                return assetMap.ToArray();
            }

            foreach (var assetData in embeddedAssetData)
            {
                if (assetData.OutOfBandAsset != null)
                {
                    // Load the asset if it isn't in the cache
                    if (!m_loadedOobAssets.ContainsKey(assetData.Id))
                    {
                        assetData.OutOfBandAsset.Load();
                        // Add the asset to the cache so we can unload it when the Rive file is unloaded
                        this.AddToOobAssetCache(assetData.Id, assetData.OutOfBandAsset);
                    }

                    assetData.OutOfBandAsset.LoadIntoByteAssetMap(assetData.Id, assetData.AssetType, assetMap);
                }
            }

            return assetMap.ToArray();
        }


        private EmbeddedAssetReference GenerateAssetReference(uint assetId, ushort assetType, string assetName, uint inBandByteSize, OutOfBandAsset outOfBandAsset, uint indexInRiveFile)
        {

            EmbeddedAssetType type = (EmbeddedAssetType)assetType;

            EmbeddedAssetReference.InitializationData initializationData = new EmbeddedAssetReference.InitializationData(
                assetType: type,
                id: assetId,
                name: assetName,
                inBandBytesSize: inBandByteSize,
                indexInRiveFile: indexInRiveFile,
                outOfBandAsset: outOfBandAsset
            );

            switch (type)
            {
                case EmbeddedAssetType.Font:
                    return new FontEmbeddedAssetReference(initializationData);
                case EmbeddedAssetType.Image:
                    return new ImageEmbeddedAssetReference(initializationData);
                case EmbeddedAssetType.Audio:
                    return new AudioEmbeddedAssetReference(initializationData);
                default:
                    DebugLogger.Instance.LogError($"{LogCodes.ERROR_UNSUPPORTED_ASSET_TYPE}: Can't generate asset reference due to unsupported asset type: {type}");
                    return null;
            }
        }

        /// <summary>
        /// The native callback that is called when the Rive runtime needs to load an embedded asset. This is used to get information about the embedded assets.
        /// </summary>
        /// <param name="assetId"></param>
        /// <param name="assetType"></param>
        /// <param name="assetName"></param>
        /// <param name="inBandByteSize"></param>
        /// <returns> A pointer to the native asset to use, otherwise a null pointer. </returns>
        public IntPtr NativeUnityAssetLoaderCallback(uint assetId, ushort assetType, string assetName, uint inBandByteSize)
        {
            // We use the native callback to get information about the embedded/referenced assets.
            // Even though we have this information when an asset is imported into the Unity Editor, this is needed because we don't have the details about embedded assets when they're loaded remotely
            // This gives us that information so we can load and set the assets at runtime right after the Rive file is loaded.

            // Don't call any native code from the plugin that uses a lock in this callback, because it will cause a deadlock
            // For example, we can't call decodeFont here because it uses a lock. This is why we use the assetMapArray approach for assets that are known ahead of time, as those don't require a callback and are loaded before the Rive file is loaded.

            var assetIndexInRiveFile = assetReferenceMap.Count;

            var preloadedOutofBandAsset = this.GetLoadedOobAsset(assetId);


            var assetReference = GenerateAssetReference(assetId, assetType, assetName, inBandByteSize, preloadedOutofBandAsset, (uint)assetIndexInRiveFile);


            if (assetReference == null)
            {
                return System.IntPtr.Zero;

            }

            // Add the asset reference to the map so we can load the asset data when the Rive file is loaded if needed.
            assetReferenceMap[assetId] = assetReference;



            // If no asset was preloaded, then return a null native asset. This tells the native runtime to handle the asset loading.
            if (preloadedOutofBandAsset == null || assetReference == null)
            {
                return System.IntPtr.Zero;

            }



            // If the asset was preloaded, then return the native asset to the runtime.
            return assetReference.OutOfBandAsset == null ? System.IntPtr.Zero : assetReference.OutOfBandAsset.NativeAsset;




        }




        /// <summary>
        /// Load the contents of the asset reference. If the asset is not loaded by any of the loaders, then it will handle the loading itself.
        /// </summary>
        /// <param name="assetReference"></param>
        /// <returns> Returns true if the asset was loaded by this loader. Otherwise, returns false. </returns>
        public bool LoadContents(EmbeddedAssetReference assetReference)
        {
            // Try to load using other loaders first
            if (TryLoadWithOtherLoaders(assetReference))
            {
                // If the user chose to handle it, but no oob asset was loaded/assigned, then we clear the asset reference in the rive file
                // If we don't do this, for example, the embedded image will be visible in the rive file, but it shouldn't be because the user technically chose to handle it
                if (assetReference.OutOfBandAsset == null && assetReference.EmbeddededBytesSize > 0)
                {
                    assetReference.ClearEmbeddedAssetReference();
                }
                return true;
            }

            // We check if the asset was already preloaded during the assetLoad callback
            if (GetLoadedOobAsset(assetReference.Id) != null)
            {
                return false; // Asset already loaded, so we don't bother loading it again
            }



            // Load the out of band asset if available
            return LoadOutOfBandAsset(assetReference);
        }

        private bool TryLoadWithOtherLoaders(EmbeddedAssetReference assetReference)
        {
            for (int i = 0; i < loaders.Count; i++)
            {
                var loader = loaders[i];
                if (loader is IFileAssetLoader fileLoader && fileLoader.LoadContents(assetReference))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Load the out of band asset for the asset reference.
        /// </summary>
        /// <param name="assetReference"></param>
        /// <returns> Returns true if the asset was loaded successfully. Otherwise, returns false. </returns>
        public bool LoadOutOfBandAsset(EmbeddedAssetReference assetReference)
        {

            OutOfBandAsset outOfBandAssetToLoad = assetReference.OutOfBandAsset;

            // If the user didn't handle the loading, and a default asset is available, then we assign it to the asset reference
            if (outOfBandAssetToLoad == null && m_fallbackReferenceAssetMap != null && m_fallbackReferenceAssetMap.TryGetValue(assetReference.Id, out OutOfBandAsset fallbackAsset))
            {
                outOfBandAssetToLoad = fallbackAsset;
            }



            if (outOfBandAssetToLoad == null)
            {
                return false;
            }

            bool success = false;

            outOfBandAssetToLoad.Load();

            try
            {

                if (SetAssetReferenceData(assetReference, outOfBandAssetToLoad))
                {
                    AddToOobAssetCache(assetReference.Id, outOfBandAssetToLoad);
                    success = true;
                }
                else
                {
                    DebugLogger.Instance.LogError($"{LogCodes.ERROR_ASSET_DATA_SET_FAILED}: Failed to set asset reference for asset {assetReference.Id} of type {assetReference.AssetType}.");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Instance.LogError($"{LogCodes.ERROR_ASSET_LOADING_FAILED}: Failed to load asset {assetReference.Id} of type {assetReference.AssetType}. Error: {ex.Message}");

            }
            finally
            {
                if (!success)
                {
                    // Unload the asset if anything went wrong
                    outOfBandAssetToLoad.Unload();

                }
            }

            return success;
        }

        private bool SetAssetReferenceData(EmbeddedAssetReference assetReference, OutOfBandAsset outOfBandAsset)
        {
            switch (assetReference.AssetType)
            {
                case EmbeddedAssetType.Font:
                    if (assetReference is FontEmbeddedAssetReference fontRef && outOfBandAsset is FontOutOfBandAsset fontAsset)
                    {
                        fontRef.SetFont(fontAsset);
                        return true;
                    }
                    break;
                case EmbeddedAssetType.Image:
                    if (assetReference is ImageEmbeddedAssetReference imageRef && outOfBandAsset is ImageOutOfBandAsset imageAsset)
                    {
                        imageRef.SetImage(imageAsset);
                        return true;
                    }
                    break;
                case EmbeddedAssetType.Audio:
                    if (assetReference is AudioEmbeddedAssetReference audioRef && outOfBandAsset is AudioOutOfBandAsset audioAsset)
                    {
                        audioRef.SetAudio(audioAsset);
                        return true;
                    }
                    break;
                default:
                    DebugLogger.Instance.LogError($"{LogCodes.ERROR_UNSUPPORTED_ASSET_TYPE}: Unable to set asset reference due to unsupported asset type: {assetReference.AssetType}");
                    return false;
            }

            DebugLogger.Instance.LogError($"{LogCodes.ERROR_ASSET_TYPE_MISMATCH}: Failed to set asset reference for type {assetReference.AssetType}. Asset reference or out-of-band asset is of incorrect type.");
            return false;
        }



        /// <summary>
        /// Get the loaded out of band asset by it's owning asset ID.
        /// </summary>
        /// <param name="assetId"></param>
        /// <returns> The loaded out of band asset, otherwise null. </returns>
        public OutOfBandAsset GetLoadedOobAsset(uint assetId)
        {
            if (m_loadedOobAssets.TryGetValue(assetId, out OutOfBandAsset asset))
            {
                return asset;
            }
            return null;
        }

        /// <summary>
        /// Load the out of band assets into the Rive file. If a custom loader is provided, then it will be used to load the assets. If not, then the loader will handle the loading itself.
        /// </summary>
        /// <param name="riveFile"></param>
        public void LoadOutOfBandAssets(File riveFile)
        {
            foreach (var assetReferencePair in assetReferenceMap)
            {
                var assetReference = assetReferencePair.Value;
                assetReference.SetRiveFileReference(riveFile);
                this.LoadContents(assetReference);

            }
        }

        /// <summary>
        /// Unload all the out of band assets that were loaded by this loader.
        /// </summary>
        public void UnloadInternallyLoadedAssets()
        {
            foreach (var assetValuePair in m_loadedOobAssets)
            {
                OutOfBandAsset asset = assetValuePair.Value;
                asset.Unload();
            }
            m_loadedOobAssets.Clear();
        }


        /// <summary>
        /// Adds a loaded out of band asset to the cache along with the asset ID.
        /// </summary>
        /// <param name="assetId"></param>
        /// <param name="outOfBandAsset"></param>
        private void AddToOobAssetCache(uint assetId, OutOfBandAsset outOfBandAsset)
        {
            m_loadedOobAssets[assetId] = outOfBandAsset;
        }

        /// <summary>
        /// Get the asset reference by it's owning asset ID.
        /// </summary>
        /// <param name="assetId"></param>
        /// <returns> The asset reference, otherwise null. </returns>
        public EmbeddedAssetReference GetAssetReference(uint assetId)
        {
            if (assetReferenceMap.TryGetValue(assetId, out EmbeddedAssetReference assetReference))
            {
                return assetReference;
            }
            return null;
        }



    }
}