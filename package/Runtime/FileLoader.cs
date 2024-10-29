using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        private readonly ConcurrentDictionary<int, (WeakReference<File> FileRef, int RefCount)> m_activeFiles = new();




        public FileLoader()
        {
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
        int? cacheId,
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
            }

            var fallbackAssetLoader = new FallbackFileAssetLoader();
            var assetMap = fallbackAssetLoader.GenerateAssetMapBytesFromEmbeddedAssets(embeddedAssets);

            var file = LoadFileAndAssets(() =>
            {
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
            File.CustomAssetLoaderCallback customAssetLoaderCallback)
        {

            if (!ValidateInput(riveFileByteContents))
            {
                return null;
            }


            var fallbackAssetLoader = new FallbackFileAssetLoader();
            if (customAssetLoaderCallback != null)
            {
                fallbackAssetLoader.AddLoader(new CustomFileAssetLoader(customAssetLoaderCallback));
            }

            return LoadFileAndAssets(() =>
            {
                return LoadNativeFileWithCallback(riveFileByteContents, fallbackAssetLoader);
            }, fallbackAssetLoader);
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
        private File GetFileFromCache(int id)
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
            catch (Exception ex)
            {
                DebugLogger.Instance.LogException(ex);
                fallbackAssetLoader.UnloadInternallyLoadedAssets();
                return null;
            }
        }

        private File LoadNativeFileWithCallback(byte[] riveFileByteContents, FallbackFileAssetLoader fallbackAssetLoader)
        {
            NativeFileInterface.s_NativeFileAssetLoader = fallbackAssetLoader;
            IntPtr filePtr = NativeFileInterface.loadRiveFileWithUnityCallback(riveFileByteContents, (uint)riveFileByteContents.Length, NativeFileInterface.AssetLoaderCallback);

            if (filePtr == IntPtr.Zero)
            {
                return null;
            }

            // We don't pass the cacheId here because we expect that if the user is using a custom asset loader callback, they will want the callback to be called every time. In that case, the user is responsible for reusing the file if they want to.
            var file = new File(filePtr, null, fallbackAssetLoader);

            // Then we load the out-of-band assets after. We can't do this during the actual asset loader callback because of potential deadlocks when calling native rive code within the callback (which already has a lock).
            // This will call the asset loader callback for each asset and allow the user to load the asset in their own way. It's safe to load the assets here because we're not in the callback anymore.
            fallbackAssetLoader.LoadOutOfBandAssets(file);

            return file;
        }

        private File LoadNativeFileWithAssetMap(byte[] riveFileByteContents, byte[] assetMap, FallbackFileAssetLoader fallbackAssetLoader, int? cacheId)
        {
            // We call the native load function with the asset map. This will provide the native file with the necessary information to load the assets.
            IntPtr filePtr = NativeFileInterface.loadRiveFile(
                riveFileByteContents,
                (uint)riveFileByteContents.Length,
                assetMap,
                (uint)assetMap.Length
            );

            if (filePtr == IntPtr.Zero)
            {
                return null;
            }

            var file = new File(filePtr, cacheId, fallbackAssetLoader);

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
        internal void IncrementRefCount(int assetKey)
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
        internal bool DecrementRefCount(int assetKey)
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

                if (file.NativeFile == IntPtr.Zero)
                {
                    return;
                }

                NativeFileInterface.unrefRiveFile(file.NativeFile);
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