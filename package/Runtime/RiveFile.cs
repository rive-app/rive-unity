using UnityEngine;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Rive
{
    /// <summary>
    /// Represents a Rive File containing Artboards, StateMachines, and Animations.
    /// </summary>
    /// <remarks>
    /// There are higher level behaviours that you can use directly in the Unity editor. Use this class if you need direct control of the lifecycle of the Rive File.
    /// </remarks>
    public class File
    {
        private readonly IntPtr m_nativeFile;
        private readonly int m_assetKey;

        // Only valid when the file was loaded from a Rive.Asset.
        private readonly Asset m_asset;

        internal IntPtr NativeFile
        {
            get { return m_nativeFile; }
        }

        private static readonly Dictionary<int, WeakReference<File>> m_activeFiles = new();

        public static File Load(string name, byte[] contents, int id, Asset asset = null)
        {
            File activeFile;
            if (m_activeFiles.TryGetValue(id, out WeakReference<File> fileReference))
            {
                // File was in our cache, see if the reference is still good.
                if (fileReference.TryGetTarget(out activeFile))
                {
                    return activeFile;
                }
                else
                {
                    // No longer referenced, remove it from the active list.
                    m_activeFiles.Remove(id);
                }
            }

            List<byte> assetMap = new();
            if (asset != null)
            {
                asset.LoadOOBAssets(assetMap);
            }

            var assetMapArray = assetMap.ToArray();
            var address = loadRiveFile(
                contents,
                (uint)contents.Length,
                assetMapArray,
                (uint)assetMapArray.Length
            );

            if (address == IntPtr.Zero)
            {
                asset.UnloadOOBAssets();
                Debug.Log("Failed to load TextAsset \"" + name + "\" as a Rive file.");
                return null;
            }
            else
            {
                activeFile = new File(address, id, asset);
                m_activeFiles.Add(id, new WeakReference<File>(activeFile));
                return activeFile;
            }
        }

        /// Load a .riv File from a Unity TextAsset. Note that the if the file
        /// was already in memory, we'll return a cached version of it.
        static public File Load(TextAsset asset)
        {
            return Load(asset.name, asset.bytes, asset.GetInstanceID());
        }

        /// Load a .riv File from a Unity Rive.Asset. Note that the if the file
        /// was already in memory, we'll return a cached version of it.
        static public File Load(Asset asset)
        {
            return Load(asset.name, asset.bytes, asset.GetInstanceID(), asset);
        }

        private File(IntPtr nativeFile, int assetKey, Asset asset = null)
        {
            m_nativeFile = nativeFile;
            m_assetKey = assetKey;
            m_asset = asset;
        }

        ~File()
        {
            if (m_asset != null)
            {
                m_asset.UnloadOOBAssets();
            }
            m_activeFiles.Remove(m_assetKey);
            unrefRiveFile(m_nativeFile);
        }

        /// Returns the number of artboards stored in the file.
        public uint ArtboardCount
        {
            get { return getArtboardCount(m_nativeFile); }
        }

        /// Returns the name of the artboard at the given index.
        public string ArtboardName(uint index)
        {
            return Marshal.PtrToStringAnsi(getArtboardName(m_nativeFile, index));
        }

        /// Instance an Artboard from the RiveFile.
        public Artboard Artboard(uint index)
        {
            IntPtr ptr = instanceArtboardAtIndex(m_nativeFile, index);
            if (ptr == IntPtr.Zero)
            {
                Debug.Log($"No Artboard at index {index}.");
                return null;
            }
            return new Artboard(ptr);
        }

        /// Instance an Artboard from the RiveFile.
        public Artboard Artboard(string name)
        {
            IntPtr ptr = instanceArtboardWithName(m_nativeFile, name);
            if (ptr == IntPtr.Zero)
            {
                Debug.Log($"No Artboard named \"{name}\".");
                return null;
            }
            return new Artboard(ptr);
        }

        #region Native Methods
        [DllImport(NativeLibrary.name)]
        private static extern IntPtr loadRiveFile(
            byte[] bytes,
            uint byteCount,
            byte[] assetMapBytes,
            uint assetMapByteCount
        );

        [DllImport(NativeLibrary.name)]
        private static extern void unrefRiveFile(IntPtr riveFile);

        [DllImport(NativeLibrary.name)]
        internal static extern uint getArtboardCount(IntPtr riveFile);

        [DllImport(NativeLibrary.name)]
        internal static extern IntPtr getArtboardName(IntPtr riveFile, uint index);

        [DllImport(NativeLibrary.name)]
        internal static extern IntPtr instanceArtboardAtIndex(IntPtr riveFile, uint index);

        [DllImport(NativeLibrary.name)]
        internal static extern IntPtr instanceArtboardWithName(IntPtr riveFile, string name);
        #endregion
    }
}
