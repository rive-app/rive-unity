using System;
using System.Runtime.InteropServices;

namespace Rive.Host
{
    /// <summary>
    /// Thread check counts from native. Mirrors ThreadViolations in
    /// thread_check.hpp.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    internal struct ThreadViolations
    {
        public uint MainCalls;
        public uint MainViolations;
        public uint ServerCalls;
        public uint ServerViolations;
        public uint RenderCalls;
        public uint RenderViolations;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string FirstMainOffender;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string FirstServerOffender;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string FirstRenderOffender;
    }

    internal static class HostNative
    {
        [DllImport(NativeLibrary.name)]
        internal static extern void riveHostStart();

        [DllImport(NativeLibrary.name)]
        internal static extern void riveHostStop();

        [DllImport(NativeLibrary.name)]
        [return: MarshalAs(UnmanagedType.U1)]
        internal static extern bool riveHostIsRunning();

        [DllImport(NativeLibrary.name)]
        [return: MarshalAs(UnmanagedType.U1)]
        internal static extern bool riveHostIsThreaded();

        [DllImport(NativeLibrary.name)]
        internal static extern void riveHostYield();

        [DllImport(NativeLibrary.name)]
        [return: MarshalAs(UnmanagedType.U1)]
        internal static extern bool riveHostServing();

        /// Copies Rive's counts into the profiler counters. Editor and
        /// development players only.
        [DllImport(NativeLibrary.name)]
        internal static extern void riveProfilerFrame();

        /// Tests only. Starts counting live objects.
        [DllImport(NativeLibrary.name)]
        internal static extern void riveTrackLive();

        /// Tests only. See LiveCounts.
        [DllImport(NativeLibrary.name)]
        internal static extern void riveLiveCounts([Out] uint[] counts, uint count);

        [DllImport(NativeLibrary.name)]
        internal static extern IntPtr riveHostDrain(out uint size);

        [DllImport(NativeLibrary.name)]
        [return: MarshalAs(UnmanagedType.U1)]
        internal static extern bool riveHostEcho(
            ulong requestId,
            byte[] data,
            uint size
        );

        [DllImport(NativeLibrary.name)]
        [return: MarshalAs(UnmanagedType.U1)]
        internal static extern bool riveHostHold(ulong requestId);

        [DllImport(NativeLibrary.name)]
        internal static extern void riveHostRelease();

        [DllImport(NativeLibrary.name)]
        internal static extern void riveGetThreadViolations(
            out ThreadViolations violations
        );

        [DllImport(NativeLibrary.name)]
        internal static extern void riveResetThreadViolations();
    }
}
