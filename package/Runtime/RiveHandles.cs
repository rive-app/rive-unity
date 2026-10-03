using System;
using System.Runtime.InteropServices;

namespace Rive
{
    // What C# holds instead of a native pointer. See rive_handles.hpp.
    //
    // One type per kind, so passing an artboard handle to a file call won't
    // compile. Each is a single ulong, so it crosses the boundary as an
    // integer with no marshalling.
    //
    // Not a pointer. Nothing to dereference, nothing to free through Marshal.
    //
    // Released by the same free/unref binding as the pointer it replaced,
    // from the owner's Dispose. Freeing works exactly as it did. What changes
    // is after: a released handle resolves to null, and releasing twice does
    // nothing.

    /// <summary>
    /// An artboard instanced from a file. Released on Dispose.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct NativeArtboardHandle
    {
        private readonly ulong m_value;

        internal NativeArtboardHandle(ulong value)
        {
            m_value = value;
        }

        internal bool IsValid => m_value != 0;

        internal ulong Value => m_value;
    }

    /// <summary>
    /// A loaded .riv file. Released on Dispose.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct NativeFileHandle
    {
        private readonly ulong m_value;

        internal NativeFileHandle(ulong value)
        {
            m_value = value;
        }

        internal bool IsValid => m_value != 0;

        internal ulong Value => m_value;
    }

    /// <summary>
    /// A state machine instanced from an artboard. Released on Dispose.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct NativeStateMachineHandle
    {
        private readonly ulong m_value;

        internal NativeStateMachineHandle(ulong value)
        {
            m_value = value;
        }

        internal bool IsValid => m_value != 0;

        internal ulong Value => m_value;
    }

    /// <summary>
    /// A state machine input, by its index in the state machine. Owned by the
    /// state machine it came from, so it stops resolving when that is
    /// disposed.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct NativeSMIInputHandle
    {
        private readonly ulong m_value;

        internal NativeSMIInputHandle(uint index)
        {
            m_value = (ulong)index + 1;
        }

        internal bool IsValid => m_value != 0;

        internal uint Index => (uint)(m_value - 1);
    }

    /// <summary>
    /// A view model instance. The same instance always comes back as the same
    /// handle, and every one handed out is released once.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct NativeViewModelInstanceHandle : IEquatable<NativeViewModelInstanceHandle>
    {
        private readonly ulong m_value;

        internal NativeViewModelInstanceHandle(ulong value)
        {
            m_value = value;
        }

        internal bool IsValid => m_value != 0;

        internal ulong Value => m_value;

        public bool Equals(NativeViewModelInstanceHandle other) => m_value == other.m_value;

        public override bool Equals(object obj) => obj is NativeViewModelInstanceHandle other && Equals(other);

        public override int GetHashCode() => m_value.GetHashCode();
    }

    /// <summary>
    /// An artboard that can be assigned to a view model property. Released
    /// on Dispose.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct NativeBindableArtboardHandle
    {
        private readonly ulong m_value;

        internal bool IsValid => m_value != 0;
    }

    /// <summary>
    /// A decoded image, font or audio source. Which native table it names
    /// depends on the asset's type. Released on Unload.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct NativeAssetHandle : IEquatable<NativeAssetHandle>
    {
        private readonly ulong m_value;

        internal NativeAssetHandle(ulong value)
        {
            m_value = value;
        }

        internal bool IsValid => m_value != 0;

        internal ulong Value => m_value;

        public bool Equals(NativeAssetHandle other) => m_value == other.m_value;

        public override bool Equals(object obj) => obj is NativeAssetHandle other && Equals(other);

        public override int GetHashCode() => m_value.GetHashCode();
    }

    /// <summary>
    /// A render queue, for server calls. Render events use its render id
    /// instead. Released on Dispose.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct NativeRenderQueueHandle
    {
        private readonly ulong m_value;

        internal NativeRenderQueueHandle(ulong value)
        {
            m_value = value;
        }

        internal bool IsValid => m_value != 0;

        internal ulong Value => m_value;
    }
}
