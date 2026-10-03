using System;
using System.Runtime.InteropServices;

namespace Rive.Host
{
    /// <summary>
    /// Starts and stops the native command host, and drains its messages.
    ///
    /// One host per domain. CommandTransport decides when it starts and
    /// stops. Main thread only.
    /// </summary>
    internal static class CommandHost
    {
        private static bool s_running;

        internal static bool IsRunning => s_running;

        /// <summary>
        /// False only on WebGL without threads, where the main thread runs
        /// the server when it drains.
        /// </summary>
        internal static bool IsThreaded => HostNative.riveHostIsThreaded();

        /// <summary>
        /// Starts the host the first time it's needed in this domain.
        /// </summary>
        internal static void EnsureStarted()
        {
            if (s_running)
            {
                return;
            }

            // Also stops one left over from an older domain.
            HostNative.riveHostStart();
            s_running = true;
        }

        /// <summary>
        /// Stops the host. Queued work still runs, and anything it posts is
        /// dropped.
        /// </summary>
        internal static void Stop()
        {
            if (!s_running)
            {
                return;
            }
            s_running = false;
            HostNative.riveHostStop();
        }

        /// <summary>
        /// Fills the batch with what the server posted since the last drain.
        /// </summary>
        internal static void Drain(HostMessageBatch batch)
        {
            if (!s_running)
            {
                batch.Messages.Clear();
                return;
            }

            IntPtr bytes = HostNative.riveHostDrain(out uint nativeSize);
            int size = (int)nativeSize;
            if (batch.Bytes.Length < size)
            {
                batch.Bytes = new byte[Math.Max(size, batch.Bytes.Length * 2)];
            }
            if (size != 0)
            {
                Marshal.Copy(bytes, batch.Bytes, 0, size);
            }
            batch.Parse(size);
        }
    }
}
