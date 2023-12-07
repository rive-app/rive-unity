using UnityEngine;
using System;
using System.Runtime.InteropServices;

namespace Rive
{
    public class FontAsset : OutOfBandAsset
    {
        protected override IntPtr loadNative(byte[] data)
        {
            return loadRiveFont(data, (nuint)data.Length);
        }

        protected override void unloadNative(IntPtr nativePtr)
        {
            unrefRiveFont(nativePtr);
        }

        #region Native Methods
        [DllImport(NativeLibrary.name)]
        private static extern IntPtr loadRiveFont(byte[] bytes, nuint byteCount);

        [DllImport(NativeLibrary.name)]
        private static extern void unrefRiveFont(IntPtr font);
        #endregion
    }
}