using System;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;
using Rive.Producer;
using Rive.Host;

namespace Rive
{
    /// <summary>
    /// Replays whatever a panel's next frame would have carried: destroys, and
    /// any canvas content a script recorded.
    ///
    /// Unloading a file queues its resources for destruction, but they only
    /// free when a later frame replays them. If the last panel was just
    /// destroyed there is no later frame, so the memory would sit there until
    /// the graphics device shuts down. This has the server take that work
    /// and the render thread replay it anyway.
    /// </summary>
    internal static class GpuCanvasResources
    {
        private static CommandBuffer s_commandBuffer;
        // A take's reply can land off the main thread.
        private static volatile bool s_available = true;
        // A few frames rather than one, because teardown is spread out: a
        // panel unregisters, its renderer is released at end of frame, and only
        // then do the artboards drop and queue their destroys. Flushing once
        // would keep missing whichever step came last.
        private const int FlushFrames = 4;
        private static int s_flushFramesLeft;

        // Takes land in a drain. The replay goes out on the first frame
        // after one lands, so the last take always gets one.
        private static int s_takesLanded;
        private static int s_takesReplayed;

        /// <summary>
        /// Call after releasing something that held GPU canvas resources. The
        /// flush itself happens over the next few frames.
        /// </summary>
        internal static void RequestFlush()
        {
            s_flushFramesLeft = FlushFrames;
        }

        /// <summary>
        /// Runs any requested flush. Called once a frame; does nothing when
        /// nothing was released.
        /// </summary>
        internal static void FlushIfRequested()
        {
            // Retires from finalizers, ahead of the flush that collects them.
            RenderLifetime.Flush();

            if (!s_available || !NativeUsageGuard.IsNativeAvailable)
            {
                return;
            }

            bool flushFrame = s_flushFramesLeft > 0;
            if (flushFrame)
            {
                s_flushFramesLeft--;
                RiveTakeCanvasFlushFrame();
            }

            int landed = Volatile.Read(ref s_takesLanded);
            if (flushFrame || landed != s_takesReplayed)
            {
                s_takesReplayed = landed;
                IssueReplay();
            }
        }

        private static void IssueReplay()
        {
            try
            {
                if (s_commandBuffer == null)
                {
                    s_commandBuffer = new CommandBuffer { name = "RiveFlushPendingCanvasWork" };
                }
                s_commandBuffer.Clear();
                s_commandBuffer.IssuePluginEvent(getFlushPendingCanvasWork(), 0);
                Graphics.ExecuteCommandBuffer(s_commandBuffer);
            }
            catch (DllNotFoundException)
            {
                s_available = false;
            }
            catch (EntryPointNotFoundException)
            {
                s_available = false;
            }
        }

        /// Doesn't wait. The session belongs to the recording thread, so the
        /// server takes and the event only replays.
        private static void RiveTakeCanvasFlushFrame()
        {
            try
            {
                CanvasNative.TakeCanvasFlushFrame(s_takeLanded);
            }
            catch (DllNotFoundException)
            {
                s_available = false;
            }
            catch (EntryPointNotFoundException)
            {
                s_available = false;
            }
        }

        private static readonly Action s_takeLanded = () => Interlocked.Increment(ref s_takesLanded);

        [DllImport(NativeLibrary.name)]
        private static extern IntPtr getFlushPendingCanvasWork();
    }
}
