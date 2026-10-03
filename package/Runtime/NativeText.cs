using System;
using System.Runtime.InteropServices;

namespace Rive
{
    /// <summary>
    /// How strings cross between C# and native. Rive's strings are UTF-8, so
    /// both directions use UTF-8 on every platform. The default marshalling
    /// is the system code page on Windows, which garbles anything past ASCII.
    /// </summary>
    internal static class NativeText
    {
        /// For string parameters: <c>[MarshalAs(NativeText.Encoding)] string name</c>.
        internal const UnmanagedType Encoding = UnmanagedType.LPUTF8Str;

        /// A native null-terminated string. Null for a null pointer.
        internal static string FromNative(IntPtr text)
        {
            return text == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(text);
        }

        /// A native string of a known byte length. Null for a null pointer.
        internal static string FromNative(IntPtr text, int byteLength)
        {
            return text == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(text, byteLength);
        }
    }
}
