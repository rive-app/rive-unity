#if RIVE_USING_EXPERIMENTAL
using Rive.Host;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Rive.Producer;
using Rive.Utils;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rive
{
    /// <summary>
    /// An image property a RenderTextureImageSource drives: a plain property
    /// or a property handle.
    /// </summary>
    internal interface IRenderImageTarget
    {
        /// Main thread. True once its instance is gone, so it's dropped.
        bool IsGone { get; }

        /// Main thread, as its batch is sent. The instance and the path to the
        /// property. False when there's no instance to reach it from.
        bool TryResolve(out NativeViewModelInstanceHandle instance, out string path);

        /// Main thread. Empties the property.
        void ClearRenderImage();
    }

    /// <summary>
    /// The main thread queues handle based build and destroy commands. The
    /// server binds an image for each build straight away, and the render
    /// thread fills it from the batch's event. Nothing here waits.
    ///
    /// A destroy or a retire carries a ticket. Native ends the ticket's lease
    /// once the render thread has let go of everything made from the source,
    /// or from one texture for a retire, and the waiter is told then.
    /// </summary>
    internal sealed class RenderImageCommandQueue
    {
        // 0 is reserved as "no handle". Handles are unique within a session.
        private static uint s_nextHandle = 1;
        // 0 means nobody's waiting on the destroy.
        private static uint s_nextTicket = 1;
        private static bool s_loggedUnsupportedBackend;
        private static RenderImageCommandQueue s_instance;

        internal static RenderImageCommandQueue Instance
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = new RenderImageCommandQueue();
                }
                return s_instance;
            }
        }

        private sealed class ImageCommand
        {
            internal uint Handle;
            internal IntPtr Texture;
            internal uint Width;
            internal uint Height;
            internal bool IsSRGB;
            internal IRenderImageTarget[] Targets;
            internal int TargetCount;
            // Filled from Targets as the batch is sent.
            internal NativeViewModelInstanceHandle[] Instances;
            internal string[] Paths;
            internal int PropertyCount;
            internal bool Destroy;
            internal uint Ticket;
        }

        private struct Release
        {
            internal uint Ticket;
            // False when the queue shuts down first.
            internal Action<bool> Done;
        }

        private struct Retire
        {
            internal IntPtr Texture;
            internal uint Ticket;
        }

        private sealed class Batch
        {
            internal ImageCommand[] Commands;
            internal int Count;
            internal Retire[] Retires;
            internal int RetireCount;
            internal ulong LastSerial;
        }

        private sealed class BatchResult
        {
            internal uint Generation;
        }

        // Only the newest update for each image waits on the main thread, and
        // one batch is out at a time, so a busy server can't pile up a job
        // per video frame.
        private readonly Dictionary<uint, ImageCommand> m_pending =
            new Dictionary<uint, ImageCommand>();
        private readonly Stack<ImageCommand> m_freeCommands = new Stack<ImageCommand>();
        private readonly ProducerChannel<Batch, BatchResult> m_batches;
        private ulong m_lastEnqueuedSerial;
        private ulong m_lastSubmittedSerial;
        private uint m_lastGeneration;
        private bool m_haveGeneration;
        private readonly List<Release> m_releases = new List<Release>();
        private readonly List<Retire> m_retires = new List<Retire>();
        private int m_lastDrainFrame = -1;
        private CommandBuffer m_commandBuffer;
        private bool m_shuttingDown;
        private object m_lastBatchCommands;

        private RenderImageCommandQueue()
        {
            m_batches = new ProducerChannel<Batch, BatchResult>(
                ChannelMode.Fifo,
                SendBatch,
                ReadBatch,
                OnBatchLanded,
                ResetBatch);
        }

        internal static uint NextHandle()
        {
            return s_nextHandle++;
        }

        // Editor domain reloads keep static state, so start handle numbering fresh.
        internal static void ResetHandles()
        {
            s_nextHandle = 1;
        }

        internal static void EnqueueDestroyIfActive(uint handle)
        {
            s_instance?.EnqueueDestroy(handle);
        }

        /// Destroys the handle and completes once nothing Rive holds uses its
        /// texture any more.
        internal static Future ReleaseAsync(uint handle)
        {
            var state = new FutureState<bool>();
            if (s_instance == null)
            {
                // Nothing was ever sent, so nothing wraps it.
                state.Succeed(true);
                return new Future(state);
            }
            uint ticket = NextTicket();
            s_instance.EnqueueDestroy(handle, ticket);
            s_instance.m_releases.Add(new Release
            {
                Ticket = ticket,
                Done = released =>
                {
                    if (released)
                    {
                        state.Succeed(true);
                    }
                    else
                    {
                        state.Cancel();
                    }
                },
            });
            return new Future(state);
        }

        /// Stops anything new wrapping texture and calls released once nothing
        /// Rive made from it is left. For our own intermediates, which can only
        /// be freed then.
        internal static void RetireTexture(IntPtr texture, Action released)
        {
            if (s_instance == null || texture == IntPtr.Zero)
            {
                // Never handed to native.
                released();
                return;
            }
            uint ticket = NextTicket();
            s_instance.m_retires.Add(new Retire { Texture = texture, Ticket = ticket });
            s_instance.m_releases.Add(new Release { Ticket = ticket, Done = _ => released() });
        }

        private static uint NextTicket()
        {
            uint ticket = s_nextTicket++;
            if (s_nextTicket == 0)
            {
                s_nextTicket = 1;
            }
            return ticket;
        }

        // Sends what was queued this frame and issues the render event for
        // whatever the server has taken. No-op if never created.
        internal static void FlushIfActive()
        {
            s_instance?.Flush();
        }

        private ImageCommand GetPendingCommand(uint handle)
        {
            if (!m_pending.TryGetValue(handle, out ImageCommand command))
            {
                command = m_freeCommands.Count > 0
                    ? m_freeCommands.Pop()
                    : new ImageCommand();
                m_pending.Add(handle, command);
            }
            return command;
        }

        public void EnqueueBuild(
            uint handle, IntPtr nativeTexture, int width, int height, bool isSRGB,
            IReadOnlyList<IRenderImageTarget> properties)
        {
            ImageCommand command = GetPendingCommand(handle);
            int count = 0;
            if (properties != null)
            {
                if (command.Targets == null || command.Targets.Length < properties.Count)
                {
                    int capacity = Math.Max(properties.Count,
                        (command.Targets?.Length ?? 0) * 2);
                    command.Targets = new IRenderImageTarget[capacity];
                    command.Instances = new NativeViewModelInstanceHandle[capacity];
                    command.Paths = new string[capacity];
                }
                for (int i = 0; i < properties.Count; i++)
                {
                    if (properties[i] != null)
                    {
                        command.Targets[count++] = properties[i];
                    }
                }
            }
            command.TargetCount = count;
            command.Handle = handle;
            command.Texture = nativeTexture;
            command.Width = (uint)width;
            command.Height = (uint)height;
            command.IsSRGB = isSRGB;
            command.PropertyCount = 0;
            command.Destroy = false;
            command.Ticket = 0;
            m_lastEnqueuedSerial++;
        }

        // A clear is a build with no texture: the render thread nulls the bound
        // properties and drops the image.
        public void EnqueueClear(uint handle, IReadOnlyList<IRenderImageTarget> properties)
        {
            EnqueueBuild(handle, IntPtr.Zero, 0, 0, false, properties);
        }

        public void EnqueueDestroy(uint handle, uint ticket = 0)
        {
            ImageCommand command = GetPendingCommand(handle);
            command.Handle = handle;
            command.PropertyCount = 0;
            // A waiting ticket survives a plain destroy landing on top of it.
            if (!command.Destroy || ticket != 0)
            {
                command.Ticket = ticket;
            }
            command.Destroy = true;
            m_lastEnqueuedSerial++;
        }

        // Lands a finished batch, which issues its render event, then sends
        // what's pending if nothing is out. Never waits. Runs in Update and
        // again in LateUpdate, so a batch the server took this frame is
        // built on this frame's render thread pass.
        public void Flush()
        {
            m_batches.Poll();
            PollReleases();
            if (m_batches.HasInFlight || (m_pending.Count == 0 && m_retires.Count == 0))
            {
                return;
            }

            Batch batch = m_batches.Begin();
            if (batch.Commands == null || batch.Commands.Length < m_pending.Count)
            {
                int capacity = Math.Max(m_pending.Count, (batch.Commands?.Length ?? 0) * 2);
                batch.Commands = new ImageCommand[capacity];
            }
            batch.Count = m_pending.Count;
            batch.LastSerial = m_lastEnqueuedSerial;
            m_pending.Values.CopyTo(batch.Commands, 0);
            m_pending.Clear();
            if (batch.Retires == null || batch.Retires.Length < m_retires.Count)
            {
                batch.Retires = new Retire[Math.Max(m_retires.Count, 4)];
            }
            batch.RetireCount = m_retires.Count;
            m_retires.CopyTo(batch.Retires, 0);
            m_retires.Clear();
            m_lastBatchCommands = batch.Commands;
            m_batches.Send(batch);

            // An inline host runs it as the main thread drains.
            m_batches.Poll();
        }

        // The event for this batch carries the same serial, and native only
        // fills from events that came after the batch's blits. See
        // riveRenderImageBatch for the layout.
        private static void SendBatch(Batch batch, ulong requestId)
        {
            PayloadWriter writer = s_batchWriter;
            writer.Clear();
            writer.U32((uint)batch.LastSerial);
            writer.U32((uint)batch.Count);
            for (int i = 0; i < batch.Count; i++)
            {
                ImageCommand command = batch.Commands[i];
                writer.U32(command.Destroy ? 1u : 0u);
                writer.U32(command.Handle);
                writer.U32(command.Ticket);
                writer.U64((ulong)command.Texture.ToInt64());
                writer.U32(command.Width);
                writer.U32(command.Height);
                writer.U32(command.IsSRGB ? 1u : 0u);
                int count = 0;
                if (!command.Destroy)
                {
                    for (int t = 0; t < command.TargetCount; t++)
                    {
                        if (command.Targets[t].TryResolve(out NativeViewModelInstanceHandle instance, out string path))
                        {
                            command.Instances[count] = instance;
                            command.Paths[count] = path;
                            count++;
                        }
                    }
                }
                command.PropertyCount = count;
                writer.U32((uint)count);
                for (int t = 0; t < count; t++)
                {
                    writer.U64(command.Instances[t].Value);
                    writer.String(command.Paths[t]);
                }
            }
            // Anything that used these went out in an earlier batch, since a
            // newer build for the same image replaces the pending one.
            writer.U32((uint)batch.RetireCount);
            for (int i = 0; i < batch.RetireCount; i++)
            {
                writer.U64((ulong)batch.Retires[i].Texture.ToInt64());
                writer.U32(batch.Retires[i].Ticket);
            }
            riveRenderImageBatch(requestId, writer.Bytes, (uint)writer.Size);
        }

        private static void ReadBatch(Batch batch, BatchResult result, HostMessageBatch replies, HostMessage message)
        {
            result.Generation = new PayloadReader(replies, message).U32();
        }

        private void OnBatchLanded(Batch batch, BatchResult result)
        {
            if (!m_shuttingDown)
            {
                m_lastGeneration = result.Generation;
                m_haveGeneration = true;
                IssueRenderEvent(result.Generation, batch.LastSerial);
            }
            m_lastSubmittedSerial = batch.LastSerial;
            // Native copied the property targets, so the commands can be reused.
            for (int i = 0; i < batch.Count; i++)
            {
                ImageCommand landed = batch.Commands[i];
                if (landed.Targets != null)
                {
                    Array.Clear(landed.Targets, 0, landed.TargetCount);
                }
                landed.TargetCount = 0;
                m_freeCommands.Push(landed);
                batch.Commands[i] = null;
            }
            batch.Count = 0;
            batch.RetireCount = 0;
        }

        // A batch that didn't land was dropped with a stopped host. Its
        // commands go back unless a newer one for that image has come since.
        private void ResetBatch(Batch batch, BatchResult result)
        {
            for (int i = 0; i < batch.Count; i++)
            {
                ImageCommand command = batch.Commands[i];
                if (!m_shuttingDown && !m_pending.ContainsKey(command.Handle))
                {
                    m_pending.Add(command.Handle, command);
                }
                else
                {
                    m_freeCommands.Push(command);
                }
                batch.Commands[i] = null;
            }
            batch.Count = 0;
            for (int i = 0; i < batch.RetireCount && !m_shuttingDown; i++)
            {
                m_retires.Add(batch.Retires[i]);
            }
            batch.RetireCount = 0;
            batch.LastSerial = 0;
            result.Generation = 0;
        }

        private void PollReleases()
        {
            if (m_releases.Count == 0)
            {
                return;
            }
            for (int i = m_releases.Count - 1; i >= 0; i--)
            {
                if (RiveRenderImageReleased(m_releases[i].Ticket))
                {
                    Action<bool> done = m_releases[i].Done;
                    m_releases.RemoveAt(i);
                    done(true);
                }
            }
            // The render thread only lets go when something drains its
            // disposes, and with no panel rendering nothing else would.
            if (m_releases.Count > 0 && m_haveGeneration && !m_shuttingDown &&
                m_lastDrainFrame != Time.frameCount)
            {
                m_lastDrainFrame = Time.frameCount;
                IssueRenderEvent(m_lastGeneration, m_lastSubmittedSerial);
            }
        }

        private void IssueRenderEvent(uint generation, ulong serial)
        {
            if (m_commandBuffer == null)
            {
                m_commandBuffer = new CommandBuffer { name = "Rive.RenderImageCommands" };
            }
            else
            {
                m_commandBuffer.Clear();
            }
            m_commandBuffer.IssuePluginEventAndData(
                getProcessRenderImageCommandsCallback(), (int)generation,
                RenderLifetime.EventData((uint)serial));
            Graphics.ExecuteCommandBuffer(m_commandBuffer);
        }

        // Full teardown for explicit lifecycle points (manager Clear / runtime
        // init), main thread only. Resets the global native queue, which frees
        // its images and moves the generation on so events in flight are
        // dropped, and drops the singleton so the next session starts clean.
        internal static void Shutdown()
        {
            if (s_instance != null)
            {
                s_instance.ReleaseManaged();
                s_instance = null;
            }
            ClearRenderImageCommandQueue();
        }

        private void ReleaseManaged()
        {
            // Let what's out reach native, the reset below undoes it.
            m_shuttingDown = true;
            using (CommandTransport.AllowWait("render image shutdown"))
            {
                m_batches.JoinAll();
            }
            if (m_commandBuffer != null)
            {
                m_commandBuffer.Release();
                m_commandBuffer = null;
            }
            m_pending.Clear();
            m_freeCommands.Clear();
            // The reset below drops the leases, so nothing would end them.
            var releases = m_releases.ToArray();
            m_releases.Clear();
            m_retires.Clear();
            foreach (Release release in releases)
            {
                release.Done(false);
            }
        }

        internal static void LogUnsupportedBackendOnce()
        {
            if (s_loggedUnsupportedBackend)
            {
                return;
            }
            GraphicsDeviceType backend = SystemInfo.graphicsDeviceType;
            bool supported = TextureHelper.SupportsRenderTextureImageSource();
            if (!supported)
            {
                s_loggedUnsupportedBackend = true;
                DebugLogger.Instance.LogError(
                    "RenderTextureImageSource: binding a RenderTexture as a Rive image is not " +
                    $"supported on the current graphics backend ({backend}). " +
                    "Supported backends: Metal, Direct3D11, Direct3D12, Vulkan. The bound image " +
                    "property will stay empty on this backend.");
            }
        }

        /// Tests only. The pending command for a handle, and the array the
        /// next batch would use.
        internal object PendingCommandForTests(uint handle) =>
            m_pending.TryGetValue(handle, out ImageCommand command) ? command : null;

        internal static object InstancesForTests(object command) =>
            (command as ImageCommand)?.Instances;

        internal object LastBatchCommandsForTests => m_lastBatchCommands;

        private static readonly PayloadWriter s_batchWriter = new PayloadWriter();

        // These write server owned state, the view model image properties
        // most of all, so they go through the server like the rest of the
        // bindings.

        [DllImport(NativeLibrary.name)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool riveRenderImageBatch(ulong requestId, byte[] batch, uint size);

        [DllImport(NativeLibrary.name)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool riveClearRenderImages(ulong requestId);

        private static void ClearRenderImageCommandQueue()
        {
            RequestTicket ticket = CommandTransport.Send(id => riveClearRenderImages(id));
            CommandTransport.Join(ref ticket);
        }

        // Any thread, it only reads a flag.
        [DllImport(NativeLibrary.name)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool riveRenderImageReleased(uint ticket);

        private static bool RiveRenderImageReleased(uint ticket)
        {
            return riveRenderImageReleased(ticket);
        }

        [DllImport(NativeLibrary.name)]
        private static extern IntPtr getProcessRenderImageCommandsCallback();
    }
}
#endif // RIVE_USING_EXPERIMENTAL
