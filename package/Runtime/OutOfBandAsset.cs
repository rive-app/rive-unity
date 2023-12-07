using UnityEngine;
using System;
using System.Collections.Generic;

namespace Rive
{
    public abstract class OutOfBandAsset : ScriptableObject
    {
        [HideInInspector]
        public byte[] bytes;

        [System.NonSerialized]
        private int m_refCount;

        [System.NonSerialized]
        private IntPtr m_nativeAsset = IntPtr.Zero;

        protected abstract IntPtr loadNative(byte[] bytes);
        protected abstract void unloadNative(IntPtr nativePtr);

        public void load(EmbeddedAsset embeddedDetails, List<byte> assetMap)
        {
            if (m_refCount == 0)
            {
                m_nativeAsset = loadNative(bytes);
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

        public void unload()
        {
            m_refCount--;
            if (m_refCount == 0)
            {
                IntPtr nativeAsset = m_nativeAsset;
                m_nativeAsset = IntPtr.Zero;
                unloadNative(nativeAsset);
            }
        }


    }
}