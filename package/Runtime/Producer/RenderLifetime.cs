using System;
using System.Collections.Concurrent;
using UnityEngine;
using UnityEngine.Rendering;
using Rive.Host;

namespace Rive.Producer
{
    /// <summary>
    /// Retires things render events point at. Native keeps each one alive by
    /// its render id until the retire event runs, which is after every event
    /// already queued, so a command buffer still in flight never outlives it.
    /// </summary>
    internal static class RenderLifetime
    {
        private struct Retirement
        {
            internal IntPtr Callback;
            internal uint RenderId;
        }

        // Retires from off the main thread, like finalizers, wait here for it.
        private static readonly ConcurrentQueue<Retirement> s_pending =
            new ConcurrentQueue<Retirement>();
        private static CommandBuffer s_commandBuffer;

        /// What a render event carries. The id fits in a pointer on 32 bit.
        internal static IntPtr EventData(uint renderId)
        {
            return new IntPtr(unchecked((int)renderId));
        }

        /// Any thread. Off the main thread it goes out with the next Flush.
        internal static void Retire(IntPtr callback, uint renderId)
        {
            if (renderId == 0 || callback == IntPtr.Zero)
            {
                return;
            }
            s_pending.Enqueue(new Retirement { Callback = callback, RenderId = renderId });
            if (CommandTransport.IsMainThread)
            {
                Flush();
            }
        }

        internal static int PendingCount => s_pending.Count;

        /// Main thread. Issues whatever is waiting.
        internal static void Flush()
        {
            if (s_pending.IsEmpty || !CommandTransport.IsMainThread)
            {
                return;
            }
            if (s_commandBuffer == null)
            {
                s_commandBuffer = new CommandBuffer { name = "RiveRetire" };
            }
            s_commandBuffer.Clear();
            while (s_pending.TryDequeue(out Retirement retirement))
            {
                s_commandBuffer.IssuePluginEventAndData(
                    retirement.Callback, 0, EventData(retirement.RenderId));
            }
            Graphics.ExecuteCommandBuffer(s_commandBuffer);
        }
    }
}
