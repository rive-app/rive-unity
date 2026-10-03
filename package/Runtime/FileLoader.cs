using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Rive.Producer;
using Rive.Utils;

namespace Rive
{
    /// <summary>
    /// A utility class for loading Rive files with referenced or embedded assets.
    /// </summary>
    internal class FileLoader
    {

        public static class LogCodes
        {
            public const string ERROR_EMPTY_RIVE_FILE_BYTES = "RIVE_FILE_BYTES_NULL_OR_EMPTY";
            public const string ERROR_RIVE_FILE_LOAD_FAILED = "RIVE_FILE_LOAD_FAILED";

        }
        private readonly ConcurrentDictionary<long, (WeakReference<File> FileRef, int RefCount)> m_activeFiles = new();

        // Async loads still on their way, by cache id, so a second one for the
        // same id shares the first.
        private readonly Dictionary<long, Future<File>> m_loading = new Dictionary<long, Future<File>>();




        public FileLoader()
        {
            FileNative.WhenHostStops(ForgetLoadedFiles);
        }

        // A stopped host took every native file with it, so the next load of
        // a cached asset has to import it again.
        private void ForgetLoadedFiles()
        {
            m_activeFiles.Clear();
            m_loading.Clear();
        }


        /// <summary>
        /// Load a Rive file with embedded asset info available in the Unity project.
        /// </summary>
        /// <param name="riveFileByteContents"></param>
        /// <param name="cacheId"></param>
        /// <param name="embeddedAssets"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        internal File LoadWithKnownAssets(
        byte[] riveFileByteContents,
        long? cacheId,
        IEnumerable<EmbeddedAssetData> embeddedAssets)
        {
            if (!ValidateInput(riveFileByteContents))
            {
                return null;
            }

            if (cacheId.HasValue)
            {
                var cacheResult = GetFileFromCache(cacheId.Value);
                if (cacheResult != null)
                {
                    IncrementRefCount(cacheId.Value);
                    return cacheResult;
                }

                // Share an async load of the same file that's still on its way.
                Future<File> loading;
                bool inFlight;
                lock (m_loading)
                {
                    inFlight = m_loading.TryGetValue(cacheId.Value, out loading);
                }
                if (inFlight)
                {
                    loading.WaitInternal();
                    File landed = loading.Status == FutureStatus.Succeeded ? loading.Result : null;
                    if (landed != null)
                    {
                        IncrementRefCount(cacheId.Value);
                        return landed;
                    }
                }
            }

            var fallbackAssetLoader = new FallbackFileAssetLoader();

            var file = LoadFileAndAssets(() =>
            {
                var assetMap = fallbackAssetLoader.GenerateAssetMapBytesFromEmbeddedAssets(embeddedAssets);
                return LoadNativeFileWithAssetMap(riveFileByteContents, assetMap, fallbackAssetLoader, cacheId);
            }, fallbackAssetLoader);

            if (file != null && cacheId.HasValue)
            {
                m_activeFiles[cacheId.Value] = (new WeakReference<File>(file), 1);
            }

            return file;
        }

        /// <summary>
        /// Load a Rive file with a custom asset loader callback. Use this method if you want to load a file that isn't available within the Unity project.
        /// </summary>
        /// <param name="riveFileByteContents"></param>
        /// <param name="customAssetLoaderCallback"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        internal File LoadFileWithCallback(
            byte[] riveFileByteContents,
            File.CustomAssetLoaderCallback customAssetLoaderCallback,
            IEnumerable<EmbeddedAssetData> fallbackAssets = null)

        {

            if (!ValidateInput(riveFileByteContents))
            {
                return null;
            }


            var fallbackAssetLoader = new FallbackFileAssetLoader(fallbackAssets);
            if (customAssetLoaderCallback != null)
            {
                fallbackAssetLoader.AddLoader(new CustomFileAssetLoader(customAssetLoaderCallback));
            }

            return LoadFileAndAssets(() =>
            {
                return LoadNativeFileWithCallback(riveFileByteContents, fallbackAssetLoader);
            }, fallbackAssetLoader);
        }

        /// Loads a Rive.Asset without waiting. With no overrides it shares the
        /// cache with File.Load(asset). With overrides it's a new file.
        internal Future<File> LoadAsync(Asset asset, IReadOnlyDictionary<uint, OutOfBandAsset> assets)
        {
            if (assets == null)
            {
                return LoadWithKnownAssetsAsync(
                    asset.Bytes,
                    ObjectHelper.GetInstanceId(asset),
                    new List<EmbeddedAssetData>(asset.EmbeddedAssets));
            }
            return LoadWithKnownAssetsAsync(asset.Bytes, null, BuildAssetData(asset.EmbeddedAssets, assets));
        }

        /// Loads bytes without waiting, cached under cacheId when there is one.
        internal Future<File> LoadAsync(
            byte[] riveFileByteContents,
            long? cacheId,
            IReadOnlyDictionary<uint, OutOfBandAsset> assets)
        {
            List<EmbeddedAssetData> assetData = assets != null ? BuildAssetData(null, assets) : null;
            return LoadWithKnownAssetsAsync(riveFileByteContents, cacheId, assetData);
        }

        /// LoadWithKnownAssets without waiting. The file lands on the main
        /// thread, or null if it couldn't be loaded. Loads for one cache id
        /// share a file, landed or not.
        internal Future<File> LoadWithKnownAssetsAsync(
            byte[] riveFileByteContents,
            long? cacheId,
            IEnumerable<EmbeddedAssetData> embeddedAssets)
        {
            if (!ValidateInput(riveFileByteContents))
            {
                return Future<File>.FromResult(null);
            }

            Future<File> loading = default;
            bool inFlight = false;
            if (cacheId.HasValue)
            {
                File cached = GetFileFromCache(cacheId.Value);
                if (cached != null)
                {
                    IncrementRefCount(cacheId.Value);
                    return Future<File>.FromResult(cached);
                }
                lock (m_loading)
                {
                    inFlight = m_loading.TryGetValue(cacheId.Value, out loading);
                }
            }
            if (inFlight)
            {
                long id = cacheId.Value;
                return new Future<File>(FutureState<File>.Then(loading, (landed, state) =>
                {
                    if (state.TryForwardFailure(landed))
                    {
                        return;
                    }
                    if (landed.Result != null)
                    {
                        IncrementRefCount(id);
                    }
                    state.Succeed(landed.Result);
                }));
            }

            var fallbackAssetLoader = new FallbackFileAssetLoader();
            Future<FileNative.Loaded> native;
            try
            {
                NativeUsageGuard.ThrowIfNativeUnavailable();
                byte[] assetMap = fallbackAssetLoader.GenerateAssetMapBytesFromEmbeddedAssets(embeddedAssets);
                native = FileNative.LoadAsync(riveFileByteContents, assetMap);
            }
            catch (Exception)
            {
                fallbackAssetLoader.UnloadInternallyLoadedAssets();
                throw;
            }

            var file = new Future<File>(FutureState<File>.Then(native, (landed, state) =>
            {
                if (cacheId.HasValue)
                {
                    lock (m_loading)
                    {
                        m_loading.Remove(cacheId.Value);
                    }
                }
                if (state.TryForwardFailure(landed))
                {
                    fallbackAssetLoader.UnloadInternallyLoadedAssets();
                    return;
                }
                FileNative.Loaded loaded = landed.Result;
                if (loaded.Contents == null)
                {
                    DebugLogger.Instance.LogError($"{LogCodes.ERROR_RIVE_FILE_LOAD_FAILED}: Failed to load Rive file. Make sure the file is valid and not corrupted.");
                    fallbackAssetLoader.UnloadInternallyLoadedAssets();
                    state.Succeed(null);
                    return;
                }
                var result = new File(loaded.Handle, loaded.Contents, cacheId, fallbackAssetLoader);
                if (cacheId.HasValue)
                {
                    m_activeFiles[cacheId.Value] = (new WeakReference<File>(result), 1);
                }
                state.Succeed(result);
            }));

            if (cacheId.HasValue)
            {
                lock (m_loading)
                {
                    m_loading[cacheId.Value] = file;
                }
            }
            return file;
        }

        /// <summary>
        /// Turns an id to asset map into the list the loader wants, on top of whatever a Rive.Asset already had assigned.
        /// </summary>
        private static List<EmbeddedAssetData> BuildAssetData(
            IEnumerable<EmbeddedAssetData> assigned,
            IReadOnlyDictionary<uint, OutOfBandAsset> assets)
        {
            var merged = new List<EmbeddedAssetData>();
            var taken = new HashSet<uint>();

            if (assigned != null)
            {
                foreach (EmbeddedAssetData entry in assigned)
                {
                    OutOfBandAsset replacement = null;
                    if (assets != null && assets.TryGetValue(entry.Id, out replacement))
                    {
                        taken.Add(entry.Id);
                    }

                    // A fresh entry either way, so the Rive.Asset's own
                    // serialized data isn't written to.
                    merged.Add(new EmbeddedAssetData(
                        entry.AssetType, entry.Id, entry.Name, entry.InBandBytesSize)
                    {
                        OutOfBandAsset = replacement ?? entry.OutOfBandAsset,
                    });
                }
            }

            if (assets == null)
            {
                return merged;
            }

            foreach (KeyValuePair<uint, OutOfBandAsset> pair in assets)
            {
                if (pair.Value == null || taken.Contains(pair.Key))
                {
                    continue;
                }
                merged.Add(new EmbeddedAssetData(pair.Value.AssetType, pair.Key, null, 0)
                {
                    OutOfBandAsset = pair.Value,
                });
            }

            return merged;
        }

        private bool ValidateInput(byte[] riveFileByteContents)
        {
            if (riveFileByteContents == null || riveFileByteContents.Length == 0)
            {
                DebugLogger.Instance.LogError($"{LogCodes.ERROR_EMPTY_RIVE_FILE_BYTES}: The provided Rive file bytes are null or empty. If you're loading from a remote file, make sure you're using the correct path and that the file exists.");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Get a file from the cache if it's already loaded.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private File GetFileFromCache(long id)
        {
            if (m_activeFiles.TryGetValue(id, out var fileInfo) && fileInfo.FileRef.TryGetTarget(out File activeFile))
            {
                return activeFile;
            }
            m_activeFiles.TryRemove(id, out _);
            return null;
        }


        private File LoadFileAndAssets(Func<File> loadFile, FallbackFileAssetLoader fallbackAssetLoader)
        {
            try
            {
                var file = loadFile();

                if (file == null)
                {
                    DebugLogger.Instance.LogError($"{LogCodes.ERROR_RIVE_FILE_LOAD_FAILED}: Failed to load Rive file. Make sure the file is valid and not corrupted.");
                    // We unload the out-of-band assets we loaded here because we don't want to keep them in memory if the file failed to load.
                    fallbackAssetLoader.UnloadInternallyLoadedAssets();
                    return null;
                }

                return file;
            }
            catch (DllNotFoundException)
            {
                fallbackAssetLoader.UnloadInternallyLoadedAssets();
                NativeUsageGuard.MarkNativeLoadFailed(NativeLoadFailureReason.LibraryNotFound);
                return null;
            }
            catch (EntryPointNotFoundException)
            {
                fallbackAssetLoader.UnloadInternallyLoadedAssets();
                NativeUsageGuard.MarkNativeLoadFailed(NativeLoadFailureReason.EntryPointMissing);
                return null;
            }
            catch (Exception)
            {
                fallbackAssetLoader.UnloadInternallyLoadedAssets();
                throw;
            }
        }

        private File LoadNativeFileWithCallback(byte[] riveFileByteContents, FallbackFileAssetLoader fallbackAssetLoader)
        {
            NativeUsageGuard.ThrowIfNativeUnavailable();

            FileNative.Loaded loaded = FileNative.Load(riveFileByteContents, Array.Empty<byte>());

            if (loaded.Contents == null)
            {
                return null;
            }

            // The import records the assets, so no C# runs while it's going.
            foreach (FileContents.AssetInfo asset in loaded.Contents.Assets)
            {
                fallbackAssetLoader.AddAssetReference(asset.Id, asset.Type, asset.Name, asset.EmbeddedBytes);
            }

            // We don't pass the cacheId here because we expect that if the user is using a custom asset loader callback, they will want the callback to be called every time. In that case, the user is responsible for reusing the file if they want to.
            var file = new File(loaded.Handle, loaded.Contents, null, fallbackAssetLoader);

            // Calls the user's loader for each asset, on this thread, with the file already loaded.
            fallbackAssetLoader.LoadOutOfBandAssets(file);



            return file;
        }

        private File LoadNativeFileWithAssetMap(byte[] riveFileByteContents, byte[] assetMap, FallbackFileAssetLoader fallbackAssetLoader, long? cacheId)
        {
            NativeUsageGuard.ThrowIfNativeUnavailable();

            // The asset map gives the import the assets C# already has.
            FileNative.Loaded loaded = FileNative.Load(riveFileByteContents, assetMap);

            if (loaded.Contents == null)
            {
                return null;
            }

            var file = new File(loaded.Handle, loaded.Contents, cacheId, fallbackAssetLoader);

            if (cacheId.HasValue)
            {
                m_activeFiles[cacheId.Value] = (new WeakReference<File>(file), 1);
            }

            return file;


        }

        /// <summary>
        /// Increment the reference count for a file.
        /// </summary>
        /// <param name="assetKey"></param>
        internal void IncrementRefCount(long assetKey)
        {
            if (m_activeFiles.TryGetValue(assetKey, out var fileInfo))
            {
                m_activeFiles[assetKey] = (fileInfo.FileRef, fileInfo.RefCount + 1);
            }
        }

        /// <summary>
        /// Decrement the reference count for a file. If the reference count reaches 0, the file is unloaded.
        /// </summary>
        /// <param name="assetKey"></param>
        /// <returns> Returns true if the file is still being referenced after decrementing. Otherwise, returns false.</returns>
        internal bool DecrementRefCount(long assetKey)
        {
            if (m_activeFiles.TryGetValue(assetKey, out var fileInfo))
            {
                int newRefCount = fileInfo.RefCount - 1;
                if (newRefCount > 0)
                {
                    m_activeFiles[assetKey] = (fileInfo.FileRef, newRefCount);
                    return true;
                }
                else
                {
                    if (fileInfo.FileRef.TryGetTarget(out var file))
                    {

                        ReleaseFile(file);
                        m_activeFiles.TryRemove(assetKey, out _);
                        return false;
                    }
                }
            }
            return false;
        }



        /// <summary>
        /// Release a file
        /// </summary>
        /// <param name="file"> The file to release.</param>
        internal void ReleaseFile(File file)
        {
            if (file != null)
            {


                if (file.AssetKey.HasValue)
                {
                    m_activeFiles.TryRemove(file.AssetKey.Value, out _);
                }

                if (!file.NativeFile.IsValid)
                {
                    return;
                }

                file.Lifetime.ReleaseOwner();
                if (file.FallbackFileAssetLoader != null)
                {
                    // Unload any out-of-band assets that the fallback loader was responsible for loading.
                    // This doesn't unload the assets the user loaded themselves because they might still be in use elsewhere.
                    file.FallbackFileAssetLoader.UnloadInternallyLoadedAssets();
                }

            }
        }



    }


}