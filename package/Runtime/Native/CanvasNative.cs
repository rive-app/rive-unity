using System;
using System.Runtime.InteropServices;
using Rive.Host;

namespace Rive
{
    /// <summary>
    /// The host calls behind render queues and the GPU canvas.
    /// </summary>
    internal static class CanvasNative
    {
        [DllImport(NativeLibrary.name)]
        private static extern ulong makeRenderQueue(
            IntPtr renderTexture,
            uint width,
            uint height,
            [MarshalAs(UnmanagedType.U1)] bool clear,
            out uint renderId);

        [DllImport(NativeLibrary.name)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool riveRecordDrawList(
            ulong requestId,
            ulong renderQueue,
            DrawOp[] ops,
            uint count,
            uint generation,
            [MarshalAs(UnmanagedType.U1)] bool dirtCheckEnabled,
            [MarshalAs(UnmanagedType.U1)] bool forceRenderNext,
            uint frame);

        [DllImport(NativeLibrary.name)]
        private static extern void unrefRenderQueue(ulong renderQueue);

        [DllImport(NativeLibrary.name)]
        private static extern void riveRenderQueueFrameBound(uint renderId, uint bound);

        /// For render events whose data carries no bound, like a buffer added to a camera once.
        internal static void SetFrameBound(uint renderId, uint bound)
        {
            riveRenderQueueFrameBound(renderId, bound);
        }

        [DllImport(NativeLibrary.name)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool riveGetRenderQueueLiveness(
            ulong requestId,
            ulong renderQueue,
            uint renderId);

        [DllImport(NativeLibrary.name)]
        private static extern void renderQueueUpdateRenderTexture(
            ulong renderQueue,
            IntPtr texture,
            uint width,
            uint height);

        [DllImport(NativeLibrary.name)]
        private static extern void setRenderQueueGeneration(ulong renderQueue, uint generation);

        [DllImport(NativeLibrary.name)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool riveTakeCanvasFlushFrame(ulong requestId);

        [DllImport(NativeLibrary.name)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool riveGetCanvasResidency(ulong requestId);

        /// Doesn't wait. The server picks the queue up in order with
        /// everything sent after it.
        internal static NativeRenderQueueHandle MakeRenderQueue(
            IntPtr texture, uint width, uint height, bool clear, out uint renderId)
        {
            ulong handle = 0;
            uint id = 0;
            CommandTransport.SendNoReply(
                () => handle = makeRenderQueue(texture, width, height, clear, out id));
            renderId = id;
            return new NativeRenderQueueHandle(handle);
        }

        /// For a channel that hands out its own request id. The ops are
        /// copied, so the array can be reused straight away.
        internal static void RecordDrawList(
            ulong requestId,
            NativeRenderQueueHandle renderQueue,
            DrawOp[] ops,
            uint count,
            uint generation,
            bool dirtCheckEnabled,
            bool forceRenderNext,
            uint frame)
        {
            riveRecordDrawList(
                requestId, renderQueue.Value, ops, count, generation, dirtCheckEnabled, forceRenderNext, frame);
        }

        internal static void UnrefRenderQueue(NativeRenderQueueHandle renderQueue)
        {
            CommandTransport.SendNoReply(() => unrefRenderQueue(renderQueue.Value));
        }

        /// Tests. Whether server calls and render events can still reach it.
        internal static void GetRenderQueueLiveness(
            NativeRenderQueueHandle renderQueue,
            uint renderId,
            out bool handleLive,
            out bool renderLive)
        {
            bool handle = false;
            bool render = false;
            RequestTicket ticket = CommandTransport.Send(
                id => riveGetRenderQueueLiveness(id, renderQueue.Value, renderId),
                (batch, message) =>
                {
                    var reader = new PayloadReader(batch, message);
                    handle = reader.Bool();
                    render = reader.Bool();
                });
            CommandTransport.Join(ref ticket);
            handleLive = handle;
            renderLive = render;
        }

        internal static void UpdateRenderTexture(
            NativeRenderQueueHandle renderQueue, IntPtr texture, uint width, uint height)
        {
            CommandTransport.SendNoReply(
                () => renderQueueUpdateRenderTexture(renderQueue.Value, texture, width, height));
        }

        internal static void SetRenderQueueGeneration(NativeRenderQueueHandle renderQueue, uint generation)
        {
            CommandTransport.SendNoReply(() => setRenderQueueGeneration(renderQueue.Value, generation));
        }

        /// Doesn't wait. landed runs in the drain that delivers the reply.
        internal static void TakeCanvasFlushFrame(Action landed)
        {
            CommandTransport.Send(
                id => riveTakeCanvasFlushFrame(id),
                (batch, message) => landed(),
                keep: false);
        }

        /// Waits for the server to read it.
        internal static GpuCanvasResidency ReadCanvasResidency()
        {
            GpuCanvasResidency residency = default;
            RequestTicket ticket = CommandTransport.Send(
                id => riveGetCanvasResidency(id),
                (batch, message) =>
                {
                    if (message.PayloadSize < Marshal.SizeOf<GpuCanvasResidency>())
                    {
                        return;
                    }
                    GCHandle pinned = GCHandle.Alloc(batch.Bytes, GCHandleType.Pinned);
                    try
                    {
                        residency = Marshal.PtrToStructure<GpuCanvasResidency>(
                            pinned.AddrOfPinnedObject() + message.PayloadOffset);
                    }
                    finally
                    {
                        pinned.Free();
                    }
                });
            CommandTransport.Join(ref ticket);
            return residency;
        }
    }
}
