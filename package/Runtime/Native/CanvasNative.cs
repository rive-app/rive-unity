using System;
using System.Runtime.InteropServices;
using Rive.Host;
using Rive.Producer;
using UnityEngine;
using UnityEngine.Rendering;

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
            uint frame,
            [MarshalAs(UnmanagedType.U1)] bool replaceable);

        [DllImport(NativeLibrary.name)]
        private static extern void unrefRenderQueue(ulong renderQueue);

        [DllImport(NativeLibrary.name)]
        private static extern void riveRenderQueueFrameBound(
            uint renderId,
            uint bound,
            [MarshalAs(UnmanagedType.U1)] bool ignoreEventBound);

        /// For render events whose data carries no bound, or an old one, like
        /// a buffer added to a camera once. ignoreEventBound says the record is
        /// for a buffer built earlier.
        internal static void SetFrameBound(uint renderId, uint bound, bool ignoreEventBound)
        {
            riveRenderQueueFrameBound(renderId, bound, ignoreEventBound);
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
        /// copied, so the array can be reused straight away. A replaceable
        /// record is skipped if a newer replaceable one for the same queue is
        /// sent before Rive starts it. Its reply still comes.
        internal static void RecordDrawList(
            ulong requestId,
            NativeRenderQueueHandle renderQueue,
            DrawOp[] ops,
            uint count,
            uint generation,
            bool dirtCheckEnabled,
            bool forceRenderNext,
            uint frame,
            bool replaceable)
        {
            riveRecordDrawList(
                requestId, renderQueue.Value, ops, count, generation, dirtCheckEnabled, forceRenderNext, frame, replaceable);
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

        [DllImport(NativeLibrary.name)]
        private static extern uint renderQueueHandOverTexture(
            uint renderId,
            IntPtr texture,
            uint width,
            uint height,
            uint generation,
            HandOverKind kind);

        // Matches HandOverKind in render_queue.cpp. Each one also carries
        // the generation replays switch to.
        private enum HandOverKind : uint
        {
            // A new texture to draw into, from UpdateTexture.
            Texture = 0,
            // The texture was destroyed or released, so skip replays until a new one.
            Gone = 1,
            // Same texture, new layout, from InvalidateTarget.
            Generation = 2,
        }

        [DllImport(NativeLibrary.name)]
        private static extern IntPtr getHandOverTexturesCallback();

        private static CommandBuffer s_handOverBuffer;

        /// Main thread. Records only need the size, so that goes through the
        /// server. Replays get the texture in render thread order, the order
        /// Unity frees textures in, so they never draw into a freed one.
        internal static void UpdateRenderTexture(
            NativeRenderQueueHandle renderQueue, uint renderId, IntPtr texture, uint width, uint height,
            uint generation)
        {
            CommandTransport.SendNoReply(
                () => renderQueueUpdateRenderTexture(renderQueue.Value, texture, width, height));
            HandOver(renderId, texture, width, height, generation, HandOverKind.Texture);
        }

        /// Main thread. The queue's texture was destroyed or released with
        /// nothing in its place, so replays skip it.
        internal static void TextureGone(uint renderId, uint generation)
        {
            HandOver(renderId, IntPtr.Zero, 0, 0, generation, HandOverKind.Gone);
        }

        /// Main thread. A new generation with the same texture, so replays
        /// drop frames recorded for the old one.
        internal static void HandOverGeneration(uint renderId, uint generation)
        {
            HandOver(renderId, IntPtr.Zero, 0, 0, generation, HandOverKind.Generation);
        }

        private static void HandOver(
            uint renderId, IntPtr texture, uint width, uint height, uint generation, HandOverKind kind)
        {
            if (renderId == 0)
            {
                return;
            }
            uint serial = renderQueueHandOverTexture(renderId, texture, width, height, generation, kind);
            if (s_handOverBuffer == null)
            {
                s_handOverBuffer = new CommandBuffer { name = "Rive.HandOverTexture" };
            }
            else
            {
                s_handOverBuffer.Clear();
            }
            s_handOverBuffer.IssuePluginEventAndData(
                getHandOverTexturesCallback(), 0, RenderLifetime.EventData(serial));
            Graphics.ExecuteCommandBuffer(s_handOverBuffer);
        }

        [DllImport(NativeLibrary.name)]
        private static extern void renderQueueSetInkbleed(
            ulong renderQueue,
            [MarshalAs(UnmanagedType.U1)] bool enabled);

        internal static void SetRenderQueueInkbleed(NativeRenderQueueHandle renderQueue, bool enabled)
        {
            CommandTransport.SendNoReply(() => renderQueueSetInkbleed(renderQueue.Value, enabled));
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
