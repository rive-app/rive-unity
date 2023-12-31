using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine.Rendering;

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
        private IntPtr m_nativeFile;
        private int m_assetKey;

        // Only valid when the file was loaded from a Rive.Asset.
        private Asset m_asset;

        internal IntPtr nativeFile
        {
            get { return m_nativeFile; }
        }

        static Dictionary<int, WeakReference<File>> _activeFiles =
            new Dictionary<int, WeakReference<File>>();

        public static File load(string name, byte[] contents, int id, Asset asset = null)
        {
            WeakReference<File> fileReference;
            File activeFile;
            if (_activeFiles.TryGetValue(id, out fileReference))
            {
                // File was in our cache, see if the reference is still good.
                if (fileReference.TryGetTarget(out activeFile))
                {
                    return activeFile;
                }
                else
                {
                    // No longer referenced, remove it from the active list.
                    _activeFiles.Remove(id);
                }
            }

            List<byte> assetMap = new List<byte>();
            if (asset != null)
            {
                asset.loadOOBAssets(assetMap);
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
                asset.unloadOOBAssets();
                Debug.Log("Failed to load TextAsset \"" + name + "\" as a Rive file.");
                return null;
            }
            else
            {
                activeFile = new File(address, id, asset);
                _activeFiles.Add(id, new WeakReference<File>(activeFile));
                return activeFile;
            }
        }

        /// Load a .riv File from a Unity TextAsset. Note that the if the file
        /// was already in memory, we'll return a cached version of it.
        static public File load(TextAsset asset)
        {
            return load(asset.name, asset.bytes, asset.GetInstanceID());
        }

        /// Load a .riv File from a Unity Rive.Asset. Note that the if the file
        /// was already in memory, we'll return a cached version of it.
        static public File load(Asset asset)
        {
            return load(asset.name, asset.bytes, asset.GetInstanceID(), asset);
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
                m_asset.unloadOOBAssets();
            }
            _activeFiles.Remove(m_assetKey);
            unrefRiveFile(m_nativeFile);
        }

        /// Returns the number of artboards stored in the file.
        public uint artboardCount
        {
            get { return getArtboardCount(m_nativeFile); }
        }

        /// Returns the name of the artboard at the given index.
        public string artboardName(uint index)
        {
            return Marshal.PtrToStringAnsi(getArtboardName(m_nativeFile, index));
        }

        /// Instance an Artboard from the RiveFile.
        public Artboard artboard(uint index)
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
        public Artboard artboard(string name)
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
