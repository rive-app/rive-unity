using UnityEngine;
using System;
using System.Runtime.InteropServices;

namespace Rive
{
    public class ImageAsset : OutOfBandAsset
    {
        protected override IntPtr loadNative(byte[] data)
        {
            var inp = loadRiveImage(data, (nuint)data.Length);
            return inp;
        }

        protected override void unloadNative(IntPtr nativePtr)
        {
            unrefRiveImage(nativePtr);
        }

        #region Native Methods
        [DllImport(NativeLibrary.name)]
        private static extern IntPtr loadRiveImage(byte[] bytes, nuint byteCount);

        [DllImport(NativeLibrary.name)]
        private static extern void unrefRiveImage(IntPtr image);
        #endregion
    }
}