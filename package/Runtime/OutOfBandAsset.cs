using UnityEngine;
using System;
using System.Collections.Generic;

namespace Rive
{
    /// <summary>
    /// Represents an out-of-band Rive asset.
    ///
    /// Out-of-band assets are assets that are referenced in a Rive asset, but are not
    /// part of the Rive asset itself. For example, images and fonts can be marked as
    /// referenced and linked separately.
    /// </summary>
    public abstract class OutOfBandAsset : ScriptableObject
    {
        [HideInInspector]
        public byte[] bytes;

        [NonSerialized]
        private int m_refCount;

        [NonSerialized]
        private IntPtr m_nativeAsset = IntPtr.Zero;

        protected abstract IntPtr LoadNative(byte[] bytes);
        protected abstract void UnloadNative(IntPtr nativePtr);

        public void Load(EmbeddedAsset embeddedDetails, List<byte> assetMap)
        {
            if (m_refCount == 0)
            {
                m_nativeAsset = LoadNative(bytes);
            }
            m_refCount++;

            // Write it into the asset map if the native asset was succesfully loaded.
            if (m_nativeAsset != IntPtr.Zero)
            {
                var bytes = BitConverter.GetBytes(embeddedDetails.id);
                for (int j = 0; j < bytes.Length; j++)
                {
                    assetMap.Add(bytes[j]);
                }
                bytes = BitConverter.GetBytes((ushort)embeddedDetails.type);
                for (int j = 0; j < bytes.Length; j++)
                {
                    assetMap.Add(bytes[j]);
                }
                bytes = BitConverter.GetBytes((nint)m_nativeAsset);
                for (int j = 0; j < bytes.Length; j++)
                {
                    assetMap.Add(bytes[j]);
                }
            }
        }

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
    }
}
