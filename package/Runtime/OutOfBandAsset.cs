using UnityEngine;
using System;
using System.Collections.Generic;

namespace Rive
{
    /// <summary>
    /// Interface for out-of-band assets.
    /// </summary>
    public interface IOutOfBandAsset
    {
        void Load();
        void Unload();
    }

    /// <summary>
    /// Represents an out-of-band Rive asset.
    ///
    /// Out-of-band assets are assets that are referenced in a Rive asset, but are not
    /// part of the Rive asset itself. For example, images and fonts can be marked as
    /// referenced and linked separately.
    /// </summary>
    public abstract class OutOfBandAsset : ScriptableObject, IOutOfBandAsset
    {
        [HideInInspector]
        [SerializeField]
        private byte[] bytes;

        [NonSerialized]
        private int m_refCount;

        [NonSerialized]
        private IntPtr m_nativeAsset = IntPtr.Zero;

        protected abstract IntPtr LoadNative(byte[] bytes);
        protected abstract void UnloadNative(IntPtr nativePtr);

        internal IntPtr NativeAsset { get { return m_nativeAsset; } }

        /// <summary>
        /// The raw bytes of the out-of-band asset.
        /// </summary>
        public byte[] Bytes { get { return bytes; } }

        /// <summary>
        /// Create an out-of-band asset instance from the given bytes.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="bytes"></param>
        /// <returns> The created out-of-band asset instance. </returns>
        public static T Create<T>(byte[] bytes) where T : OutOfBandAsset
        {
            var asset = ScriptableObject.CreateInstance<T>();
            asset.Init(bytes);
            return asset;
        }

        private void Init(byte[] bytes)
        {
            this.bytes = bytes;
        }

        internal void LoadIntoByteAssetMap(uint embeddedAssetId, EmbeddedAssetType embeddedAssetType, List<byte> assetMap)
        {
            // Write it into the asset map if the native asset was succesfully loaded.
            if (NativeAsset != IntPtr.Zero)
            {
                var bytes = BitConverter.GetBytes(embeddedAssetId);
                for (int j = 0; j < bytes.Length; j++)
                {
                    assetMap.Add(bytes[j]);
                }
                bytes = BitConverter.GetBytes((ushort)embeddedAssetType);
                for (int j = 0; j < bytes.Length; j++)
                {
                    assetMap.Add(bytes[j]);
                }
#if UNITY_WEBGL && !UNITY_EDITOR
                // nint is incorrectly reported as 64 bit on wasm which would
                // break Rive's native code.
                bytes = BitConverter.GetBytes((int)NativeAsset);
#else
                bytes = BitConverter.GetBytes((nint)NativeAsset);
#endif
                for (int j = 0; j < bytes.Length; j++)
                {
                    assetMap.Add(bytes[j]);
                }
            }
        }


        /// <summary>
        /// Load the out-of-band asset. Call this before using the asset.
        /// </summary>
        public void Load()
        {
            if (m_refCount == 0)
            {
                m_nativeAsset = LoadNative(bytes);
            }
            m_refCount++;
        }

        /// <summary>
        /// Unload the out-of-band asset. This allows the engine to clean it up when it is not used by any more animations.
        /// </summary>
        public void Unload()
        {
            m_refCount--;
            if (m_refCount == 0)
            {
                IntPtr nativeAsset = m_nativeAsset;
                m_nativeAsset = IntPtr.Zero;
                UnloadNative(nativeAsset);
            }
        }

        /// <summary>
        /// Check the reference count of the out-of-band asset.
        /// </summary>
        /// <returns></returns>
        internal int RefCount()
        {
            return m_refCount;
        }
    }
}
