using UnityEngine;
using System;
using System.Runtime.InteropServices;

namespace Rive
{
    /// <summary>
    /// Represents an out-of-band Rive image asset.
    /// </summary>
    public class ImageAsset : OutOfBandAsset
    {
        protected override IntPtr LoadNative(byte[] data)
        {
            var inp = loadRiveImage(data, (nuint)data.Length);
            return inp;
        }

        protected override void UnloadNative(IntPtr nativePtr)
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
