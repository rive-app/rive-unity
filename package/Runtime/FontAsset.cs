using System;
using System.Runtime.InteropServices;

namespace Rive
{
    /// <summary>
    /// Represents an out-of-band Rive font asset.
    /// </summary>
    public class FontAsset : OutOfBandAsset
    {
        protected override IntPtr LoadNative(byte[] data)
        {
            return loadRiveFont(data, (nuint)data.Length);
        }

        protected override void UnloadNative(IntPtr nativePtr)
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
