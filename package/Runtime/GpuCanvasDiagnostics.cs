using System;
using System.Runtime.InteropServices;
using Rive.Producer;
using UnityEngine;
using Rive.Host;

namespace Rive
{
    /// <summary>
    /// GPU resources the GPU canvas replayer is currently holding. Mirrors
    /// rive::unity::CanvasResidency, so only append to it.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct GpuCanvasResidency
    {
        public ulong ImageBytes;
        public ulong BufferBytes;
        public ulong OreTextureBytes;
        public ulong OreBufferBytes;
        public uint Images;
        public uint Buffers;
        public uint Paths;
        public uint Paints;
        public uint Shaders;
        public uint OreTextures;
        public uint OreBuffers;
        public uint OreOther;
        /// <summary>Times the destroy flush ran, and times it had work.</summary>
        public uint FlushRequests;
        public uint FlushReplays;
        /// <summary>Frames taken but not replayed yet, across every queue.</summary>
        public uint PendingFrames;
        /// <summary>Queued frames carrying Ore work.</summary>
        public uint PendingOreFrames;
        /// <summary>Distinct GPU canvases queued frames keep alive.</summary>
        public uint CanvasesHeld;
        /// <summary>Files loaded right now.</summary>
        public uint LiveFiles;

        /// <summary>
        /// Live objects across every table. Usually what you want to
        /// assert on, it hits zero when nothing is loaded.
        /// </summary>
        public uint TotalObjects =>
            Images + Buffers + Paths + Paints + Shaders
            + OreTextures + OreBuffers + OreOther;

        /// <summary>
        /// A floor on real GPU usage. Driver padding and backend scratch aren't
        /// visible here, so compare two readings rather than trusting one.
        /// </summary>
        public ulong TotalBytes =>
            ImageBytes + BufferBytes + OreTextureBytes + OreBufferBytes;

        public override string ToString() =>
            $"objects={TotalObjects} (images={Images} buffers={Buffers} " +
            $"paths={Paths} paints={Paints} shaders={Shaders} " +
            $"ore={OreTextures + OreBuffers + OreOther}) bytes={TotalBytes} " +
            $"flushes={FlushRequests}/{FlushReplays} pending={PendingFrames} " +
            $"pendingOre={PendingOreFrames} canvasesHeld={CanvasesHeld} " +
            $"files={LiveFiles}";
    }

    /// <summary>
    /// Lets tests check what the GPU canvas replayer is holding.
    ///
    /// Unity's own memory counters can't see this. Rive creates its textures
    /// through its own graphics device, so "Texture Memory" and "Gfx Used
    /// Memory" don't move when a Rive file loads or unloads.
    ///
    /// Reading walks the replayer's tables, so call it per assertion, not per
    /// frame.
    /// </summary>
    internal static class GpuCanvasDiagnostics
    {
        private static bool s_available = true;

        /// Turns residency tracking on. Off by default, it costs the
        /// render thread a lock per replay. Only the tests ask, from an
        /// editor assembly that ships with them.
        ///
        /// Has to happen before anything renders, the first replay fixes
        /// it for the session.
        internal static void Enable()
        {
            if (!NativeUsageGuard.IsNativeAvailable)
            {
                return;
            }
            try
            {
                riveSetCanvasDiagnosticsEnabled(true);
            }
            catch (EntryPointNotFoundException) { }
            catch (DllNotFoundException) { }
        }

        /// <summary>
        /// False when the loaded plugin has no residency export, or when
        /// diagnostics are off for this session, so zeros mean "can't tell"
        /// rather than "nothing held". Callers skip rather than assert.
        /// </summary>
        internal static bool TryRead(out GpuCanvasResidency residency)
        {
            residency = default;
            if (!s_available || !NativeUsageGuard.IsNativeAvailable)
            {
                return false;
            }
            try
            {
                if (!riveCanvasDiagnosticsEnabled())
                {
                    return false;
                }
                RiveGetCanvasResidency(out residency);
                return true;
            }
            catch (EntryPointNotFoundException)
            {
                s_available = false;
            }
            catch (DllNotFoundException)
            {
                s_available = false;
            }
            return false;
        }

        [DllImport(NativeLibrary.name)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool riveCanvasDiagnosticsEnabled();

        [DllImport(NativeLibrary.name)]
        private static extern void riveSetCanvasDiagnosticsEnabled(
            [MarshalAs(UnmanagedType.U1)] bool enabled
        );

        private static void RiveGetCanvasResidency(out GpuCanvasResidency residency)
        {
            residency = CanvasNative.ReadCanvasResidency();
        }
    }
}
