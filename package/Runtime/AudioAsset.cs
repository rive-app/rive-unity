using System;
using System.Runtime.InteropServices;

namespace Rive
{
    /// <summary>
    /// Represents an out-of-band Rive font asset.
    /// </summary>
    public class AudioAsset : OutOfBandAsset
    {
        protected override IntPtr LoadNative(byte[] data)
        {
            return loadAudioSource(data, (nuint)data.Length);
        }

        protected override void UnloadNative(IntPtr nativePtr)
        {
            unrefAudioSource(nativePtr);
        }

        #region Native Methods
        [DllImport(NativeLibrary.name)]
        private static extern IntPtr loadAudioSource(byte[] bytes, nuint byteCount);

        [DllImport(NativeLibrary.name)]
        private static extern void unrefAudioSource(IntPtr audioSource);
        #endregion
    }
}
