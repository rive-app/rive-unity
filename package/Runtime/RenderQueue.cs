using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using System.Numerics;
using Rive.Producer;
using Rive.Host;
using Rive.Utils;

namespace Rive
{
    public class RiveCommandBuffer : CommandBuffer
    {
        private readonly Renderer m_renderer;

        public RiveCommandBuffer(Renderer renderer)
        {
            m_renderer = renderer;
        }
    }

    internal class CoroutineRunner : MonoBehaviour { }

    public class Renderer : IRenderer
    {
        protected RenderQueue m_renderQueue;
        // Copies, so a disposed queue's handle and id resolve to nothing
        // rather than to whatever comes next.
        private readonly NativeRenderQueueHandle m_nativeRenderQueue;
        private readonly uint m_renderId;

        // The draw list lives here, not in native, so a frame costs one call.
        private readonly List<DrawOp> m_ops = new List<DrawOp>();
        // One entry per op, null when the op has no artboard. The native
        // pointer is read from here at record time, so a disposed artboard is
        // dropped instead of leaving a dangling pointer in the list.
        private readonly List<Artboard> m_opArtboards = new List<Artboard>();
        private DrawOp[] m_scratch = new DrawOp[64];
        private readonly RecordStaging m_staging = new RecordStaging();
        private readonly RecordBatch m_synchronousRecord = new RecordBatch();
        private bool m_recordsAsynchronously;
        private bool m_artboardDirtCheckEnabled;
        private bool m_forceRenderNext = true;

        // Queues with a renderer added to a command buffer. Unity can replay
        // that buffer every frame without calling back in, so the late step
        // records any that nothing else has since the last one. Weak, so a
        // queue nobody holds still finalizes. Main thread only.
        private static readonly List<WeakReference<RenderQueue>> s_replayedQueues =
            new List<WeakReference<RenderQueue>>();
        // Counts late steps, so a record can tell whether it's this frame's.
        private static int s_lateStep;
        private int m_recordedAtStep = -1;
        private int m_replayRecordsForTests;

        private sealed class RecordBatch
        {
            internal DrawOp[] Ops;
            internal int Count;
            internal uint Generation;
            internal bool DirtCheckEnabled;
            internal bool ForceRenderNext;
            internal uint Frame;
        }

        internal Renderer(RenderQueue queue)
        {
            m_renderQueue = queue;
            m_nativeRenderQueue = queue.NativeHandle;
            m_renderId = queue.RenderId;
        }

        internal RenderQueue RenderQueue => m_renderQueue;

        internal bool RecordsAsynchronously =>
            m_recordsAsynchronously;

        internal void SetRecordsAsynchronously(bool enabled)
        {
            m_recordsAsynchronously = enabled;
        }

        /// The late step this last recorded in. Tests only.
        internal int RecordedAtStep => m_recordedAtStep;

        /// Tests. Everything recorded so far has reached the server.
        internal void SettleRecordsForTests() => m_staging.SettleForTests();

        internal static int LateStep => s_lateStep;

        /// Records the late step made because nothing else did. Tests only.
        internal int ReplayRecordsForTests => m_replayRecordsForTests;

        internal void InvalidateTarget()
        {
            m_renderQueue.AdvanceGeneration();
        }

        /// <summary>
        /// Clear the commands in the render queue.
        /// </summary>
        public void Clear()
        {
            m_ops.Clear();
            m_opArtboards.Clear();
            m_forceRenderNext = true;
        }

        /// <summary>
        /// Enables/disables checking whether any artboards referenced by this render queue have dirt (didChange)
        /// to decide if a render should occur.
        /// This is a render-queue level setting: if any artboard in the queue has dirt, the entire queue can redraw.
        /// </summary>
        internal void SetArtboardDirtCheckEnabled(bool enabled)
        {
            if (m_artboardDirtCheckEnabled != enabled)
            {
                m_artboardDirtCheckEnabled = enabled;
                m_forceRenderNext = true;
            }
        }

        /// <summary>
        /// Draw the given artboard to the render queue.
        /// </summary>
        public void Draw(Artboard artboard)
        {
            if (artboard == null)
            {
                throw new ArgumentException("A non null artboard must be provided.");
            }
            Add(DrawOp.DrawArtboard(default), artboard);
        }

        /// <summary>
        /// Clip the render queue to a rect of the given size, starting at the origin.
        /// </summary>
        public void ClipRect(float width, float height)
        {
            Add(DrawOp.ClipRect(width, height), null);
        }

        /// <summary>
        /// Save the current render queue state.
        /// </summary>
        public void Save()
        {
            Add(DrawOp.Save(), null);
        }

        /// <summary>
        /// Restore the last saved render queue state.
        /// </summary>
        public void Restore()
        {
            Add(DrawOp.Restore(), null);
        }

        /// <summary>
        /// Transform the render queue by the given translation.
        /// </summary>
        public void Translate(System.Numerics.Vector2 translation)
        {
            Add(DrawOp.Translate(translation.X, translation.Y), null);
        }

        /// <summary>
        /// Transform the render queue by the given translation.
        /// </summary>
        public void Translate(float x, float y)
        {
            Add(DrawOp.Translate(x, y), null);
        }

        /// <summary>
        /// Transform the render queue by the given matrix.
        /// </summary>
        public void Transform(System.Numerics.Matrix3x2 matrix)
        {
            Add(DrawOp.Transform(matrix), null);
        }

        /// <summary>
        /// Align the artboard to the given fit and alignment.
        /// </summary>
        public void Align(Fit fit, Alignment alignment, Artboard artboard, float scaleFactor = 1.0f)
        {
            if (artboard == null)
            {
                throw new ArgumentException("A non null artboard must be provided.");
            }
            Add(DrawOp.Align(fit, alignment, default, scaleFactor), artboard);
        }

        /// <summary>
        /// Align the artboard to the given fit and alignment, within the given frame.
        /// </summary>
        public void Align(Fit fit, Alignment alignment, Artboard artboard, AABB frame, float scaleFactor = 1.0f)
        {
            if (artboard == null)
            {
                throw new ArgumentException("A non null artboard must be provided.");
            }
            Add(
                DrawOp.AlignWithFrame(fit, alignment, default, frame, scaleFactor),
                artboard);
        }

        public void Submit()
        {
            IssueSubmit();
        }

        private void IssueSubmit()
        {
            var commandBuffer = new RiveCommandBuffer(this);
            if (m_renderQueue.Texture != null)
            {
                commandBuffer.SetRenderTarget(m_renderQueue.Texture);
            }

            m_renderQueue.UpdateDelayedRenderTexture();
            RecordForGpuCanvas();

            commandBuffer.IssuePluginEventAndData(
                getRenderCommandBufferCallback(),
                0,
                RenderLifetime.EventData(m_renderId, RenderLifetime.FrameBound())
            );
            Graphics.ExecuteCommandBuffer(commandBuffer);
        }

        public CommandBuffer ToCommandBuffer()
        {
            var commandBuffer = new RiveCommandBuffer(this);
            AddToCommandBuffer(commandBuffer);
            return commandBuffer;
        }

        public void AddToCommandBuffer(CommandBuffer commandBuffer)
        {
            if (
                UnityEngine.SystemInfo.graphicsDeviceType
                == UnityEngine.Rendering.GraphicsDeviceType.Metal
            )
            {
                // Unity seems to have the wrong texture bound when querying the
                // exposed CurrentRenderPassDescriptor's colorAttachment. This
                // forces the Metal backend to catch up.
                commandBuffer.DrawMesh(
                    GetResetMesh(),
                    new UnityEngine.Matrix4x4(),
                    GetResetMaterial()
                );
            }

            m_renderQueue.UpdateDelayedRenderTexture();
            RecordForGpuCanvas();
            TrackReplayed();

            commandBuffer.IssuePluginEventAndData(
                getRenderCommandBufferCallback(),
                0,
                RenderLifetime.EventData(m_renderId, RenderLifetime.FrameBound())
            );
            commandBuffer.IssuePluginEvent(getInvalidateState(), 0);
        }

#if UNITY_2023_1_OR_NEWER && RIVE_USING_URP

        public void AddToCommandBuffer(UnsafeCommandBuffer commandBuffer)
        {
            if (
                UnityEngine.SystemInfo.graphicsDeviceType
                == UnityEngine.Rendering.GraphicsDeviceType.Metal
            )
            {
                commandBuffer.DrawMesh(
                    GetResetMesh(),
                    new UnityEngine.Matrix4x4(),
                    GetResetMaterial()
                );
            }

            m_renderQueue.UpdateDelayedRenderTexture();
            RecordForGpuCanvas();
            TrackReplayed();

            commandBuffer.IssuePluginEventAndData(
                getRenderCommandBufferCallback(),
                0,
                RenderLifetime.EventData(m_renderId, RenderLifetime.FrameBound())
            );
            commandBuffer.IssuePluginEvent(getInvalidateState(), 0);
        }

#endif

        /// <summary>
        /// Hands this frame's draw list to the server, which records it into
        /// the deferred session so the render thread only has to replay it.
        ///
        /// Has to run once per frame the queue is going to be drawn. Submit and
        /// AddToCommandBuffer cover the callers that re-issue every frame; a caller
        /// that registers a command buffer once and lets it replay, like the
        /// built-in render pipeline handler, calls this itself.
        /// </summary>
        private void Add(DrawOp op, Artboard artboard)
        {
            m_ops.Add(op);
            m_opArtboards.Add(artboard);
        }

        internal void RecordForGpuCanvas()
        {
            m_recordedAtStep = s_lateStep;
            CanvasNative.SetFrameBound(m_renderId, RenderLifetime.FrameBound());
            int count = m_ops.Count;
            using var noWait = CommandTransport.NoWaitIf(
                m_recordsAsynchronously, "async record");
            if (!m_recordsAsynchronously)
            {
                if (count > m_scratch.Length)
                {
                    m_scratch = new DrawOp[Mathf.NextPowerOfTwo(count)];
                }
                CopyDrawOps(m_scratch, count);
                m_synchronousRecord.Ops = m_scratch;
                m_synchronousRecord.Count = count;
                m_synchronousRecord.Generation = m_renderQueue.Generation;
                m_synchronousRecord.DirtCheckEnabled = m_artboardDirtCheckEnabled;
                m_synchronousRecord.ForceRenderNext = m_forceRenderNext;
                m_synchronousRecord.Frame = (uint)Time.frameCount;
                RecordSynchronous();
                m_forceRenderNext = false;
                return;
            }

            if (PacingCounters.Enabled)
            {
                PacingCounters.RecordsWanted++;
            }
            RecordStaging.Record record = m_staging.Begin();
            if (record.Ops == null || record.Ops.Length < count)
            {
                record.Ops = new DrawOp[Mathf.NextPowerOfTwo(Mathf.Max(1, count))];
            }
            CopyDrawOps(record.Ops, count);
            record.Count = count;
            record.Queue = m_nativeRenderQueue;
            record.Generation = m_renderQueue.Generation;
            record.DirtCheckEnabled = m_artboardDirtCheckEnabled;
            // A record still waiting may carry a force the server hasn't seen.
            record.ForceRenderNext |= m_forceRenderNext;
            record.Frame = (uint)Time.frameCount;
            m_forceRenderNext = false;
            m_staging.Send(record);
        }

        private void CopyDrawOps(DrawOp[] destination, int count)
        {
            m_ops.CopyTo(destination);

            // Read the handles now rather than when the op was added. The
            // list is replayed every camera render and a widget can be
            // unloaded in between; a handle for an artboard that has gone
            // resolves to nothing natively, so nothing here has to guess.
            for (int i = 0; i < count; i++)
            {
                Artboard artboard = m_opArtboards[i];
                destination[i].Artboard =
                    artboard != null ? artboard.NativeArtboard : default;
            }
        }

        // Play mode only, where the late step runs. The editor's previews
        // record on every repaint anyway.
        private void TrackReplayed()
        {
            if (ReferenceEquals(m_renderQueue.ReplayedRenderer, this) ||
                !CommandTransport.IsMainThread || !Application.isPlaying)
            {
                return;
            }
            // The queue keeps it, since the buffer may be all that's left of
            // it, as with queue.Renderer().AddToCommandBuffer(buffer).
            if (m_renderQueue.KeepReplayed(this))
            {
                s_replayedQueues.Add(new WeakReference<RenderQueue>(m_renderQueue));
            }
            RiveFrameLoop.Ensure();
        }

        /// The frame loop's late step, after the components have recorded.
        /// Records each replayed renderer nothing has recorded since the last
        /// one. URP and HDRP record while rendering, after this step, which
        /// counts for the next one, so they're never recorded twice.
        internal static void RecordReplayed()
        {
            for (int i = s_replayedQueues.Count - 1; i >= 0; i--)
            {
                if (!s_replayedQueues[i].TryGetTarget(out RenderQueue queue) ||
                    queue.IsDisposed)
                {
                    s_replayedQueues[i] = s_replayedQueues[s_replayedQueues.Count - 1];
                    s_replayedQueues.RemoveAt(s_replayedQueues.Count - 1);
                    continue;
                }
                Renderer renderer = queue.ReplayedRenderer;
                if (renderer == null || renderer.m_recordedAtStep == s_lateStep)
                {
                    continue;
                }
                renderer.m_replayRecordsForTests++;
                try
                {
                    renderer.RecordForGpuCanvas();
                }
                catch (Exception e)
                {
                    DebugLogger.Instance.LogException(e);
                }
            }
            s_lateStep++;
        }

        // MainThread panels record and wait every frame, so the send is made
        // once and reads the batch. Native copies the ops as it goes out.
        private Action<ulong> m_sendSynchronousRecord;

        private void RecordSynchronous()
        {
            m_sendSynchronousRecord ??= id =>
            {
                RecordBatch batch = m_synchronousRecord;
                CanvasNative.RecordDrawList(
                    id,
                    m_nativeRenderQueue,
                    batch.Ops,
                    (uint)batch.Count,
                    batch.Generation,
                    batch.DirtCheckEnabled,
                    batch.ForceRenderNext,
                    batch.Frame);
            };
            RequestTicket ticket = CommandTransport.Send(m_sendSynchronousRecord);
            CommandTransport.Join(ref ticket);
        }

        private static Material m_resetMaterial;
        private static Mesh m_resetMesh;

        private static Material GetResetMaterial()
        {
            if (m_resetMaterial == null)
            {
                m_resetMaterial = new Material(UnityEngine.Shader.Find("UI/Default"));
            }
            return m_resetMaterial;
        }

        private static Mesh GetResetMesh()
        {
            if (m_resetMesh == null)
            {
                m_resetMesh = new Mesh();
            }
            return m_resetMesh;
        }

        #region Native Methods
        [DllImport(NativeLibrary.name)]
        internal static extern IntPtr getRenderCommandBufferCallback();

        [DllImport(NativeLibrary.name)]
        internal static extern IntPtr getInvalidateState();

        #endregion
    }


    public class RenderQueue : IDisposable
    {
        private bool m_disposed = false;
        private uint m_generation = 1;

        // When using Vulkan we need to pass the RenderTexture.colorBuffer's
        // native pointer. This colorBuffer's native pointer sometimes returns 0
        // (null) even after calling RenderTexture.Create. In these rare cases
        // we hold onto it to try to re-acquire it prior to the next submission.
        private bool m_delayed = false;

        public RenderTexture Texture { get; private set; }

        internal uint Generation => m_generation;

        internal void AdvanceGeneration()
        {
            m_generation++;
            CanvasNative.SetRenderQueueGeneration(m_nativeRenderQueue, m_generation);
        }

        /// For server calls. Stale once disposed.
        internal NativeRenderQueueHandle NativeHandle => m_nativeRenderQueue;

        internal bool IsDisposed => m_disposed;

        // The renderer most recently added to a command buffer, held for as
        // long as the queue is. Each record replaces the frame the queue
        // shows, so an older one would only be wasted work. See
        // Renderer.RecordReplayed.
        private Renderer m_replayedRenderer;
        private bool m_inReplayList;

        internal Renderer ReplayedRenderer => m_replayedRenderer;

        /// True the first time, when the queue needs adding to the list.
        internal bool KeepReplayed(Renderer renderer)
        {
            m_replayedRenderer = renderer;
            bool first = !m_inReplayList;
            m_inReplayList = true;
            return first;
        }

        /// What render events carry. 0 once disposed.
        internal uint RenderId => m_renderId;


        private void Initialize(RenderTexture texture, bool clear)
        {
            m_updateDelayedCallback = UpdateDelayedRenderTexture;

            Texture = texture;
            ValidateRenderTexture(texture, true);
            NativeUsageGuard.ThrowIfNativeUnavailable();
            if (texture != null)
            {
                texture.Create();
            }
            IntPtr pointer = texture == null ? IntPtr.Zero : GetNativeTexturePointer(texture);
            uint width = (uint)(texture?.width ?? 0);
            uint height = (uint)(texture?.height ?? 0);
            NativeLoadFailureReason? failure = null;
            uint renderId = 0;
            try
            {
                m_nativeRenderQueue = CanvasNative.MakeRenderQueue(pointer, width, height, clear, out renderId);
            }
            catch (DllNotFoundException)
            {
                failure = NativeLoadFailureReason.LibraryNotFound;
            }
            catch (EntryPointNotFoundException)
            {
                failure = NativeLoadFailureReason.EntryPointMissing;
            }
            m_renderId = renderId;
            if (failure.HasValue)
            {
                NativeUsageGuard.MarkNativeLoadFailed(failure.Value);
            }
            // Anything a finalizer retired off the main thread goes out now.
            RenderLifetime.Flush();
        }

        public RenderQueue(RenderTexture texture = null, bool clear = true)
        {
            Initialize(texture, clear);
        }

        internal RenderQueue(RenderTexture texture, bool clear, MonoBehaviour coroutineHelper)
        {
            CoroutineHelper = coroutineHelper;
            Initialize(texture, clear);
        }

        public Renderer Renderer()
        {
            if (!m_nativeRenderQueue.IsValid)
            {
                return null;
            }
            return new Renderer(this);
        }

        ~RenderQueue()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!m_disposed)
            {
                if (m_nativeRenderQueue.IsValid)
                {
                    // The server lets go first, then the retire event runs
                    // behind every render event already queued. The queue is
                    // freed by whichever lands last.
                    CanvasNative.UnrefRenderQueue(m_nativeRenderQueue);
                    RenderLifetime.Retire(getRetireRenderQueueCallback(), m_renderId);
                    m_nativeRenderQueue = default;
                    m_renderId = 0;

                    // This drops the queue's references to the artboards it
                    // drew, which is what finally releases their file and
                    // queues its GPU resources for destruction. Nothing may be
                    // left to render a frame that would carry them.
                    GpuCanvasResources.RequestFlush();
                }

                Texture = null;
                m_replayedRenderer = null;
                m_disposed = true;
            }
        }

        static void ValidateRenderTexture(RenderTexture texture, bool allowNull = false)
        {
            if (allowNull && texture == null)
            {
                return;
            }
            if (texture == null)
            {
                throw new ArgumentException("A non null RenderTexture must be provided.");
            }
            if (
                UnityEngine.SystemInfo.graphicsDeviceType
                    == UnityEngine.Rendering.GraphicsDeviceType.Direct3D11
                && !texture.enableRandomWrite
            )
            {
                throw new ArgumentException(
                    "RenderTexture must have enableRandomWrite set to true for D3D11."
                );
            }
        }


        static WaitForEndOfFrame s_waitForEndOfFrame = new WaitForEndOfFrame();
        private CallbackEventHandler m_updateDelayedCallback;

        private static MonoBehaviour s_coroutineHelper;

        /// <summary>
        /// Used to run coroutines for the render queue. Needs to be a dontdestroyonload object so that it can be used across scenes.
        /// </summary>
        internal MonoBehaviour CoroutineHelper { get { return s_coroutineHelper; } set { s_coroutineHelper = value; } }

        private delegate void CallbackEventHandler();
        private static IEnumerator CallCallback(CallbackEventHandler callback)
        {
            yield return s_waitForEndOfFrame;
            callback();
        }

        private static void EndOfFrame(CallbackEventHandler callback)
        {
            if (Application.isPlaying)
            {
                if (s_coroutineHelper == null)
                {
                    s_coroutineHelper = new GameObject("[Rive] RenderQueue Coroutine Helper").AddComponent<CoroutineRunner>();
                    UnityEngine.Object.DontDestroyOnLoad(s_coroutineHelper.gameObject);
                }
                s_coroutineHelper.StartCoroutine(CallCallback(callback));
            }
        }

        private IntPtr GetNativeTexturePointer(RenderTexture texture)
        {
            if (UnityEngine.SystemInfo.graphicsDeviceType
                    == UnityEngine.Rendering.GraphicsDeviceType.Vulkan)
            {
                IntPtr pointer = texture.colorBuffer.GetNativeRenderBufferPtr();
                if (pointer == IntPtr.Zero)
                {
                    texture.Create();
                    pointer = texture.colorBuffer.GetNativeRenderBufferPtr();
                    if (pointer == IntPtr.Zero)
                    {
                        m_delayed = true;
                        EndOfFrame(m_updateDelayedCallback);
                    }
                }
                return pointer;
            }
            return texture.GetNativeTexturePtr();
        }

        internal void UpdateDelayedRenderTexture()
        {
            if (!m_delayed || Texture == null)
            {
                return;
            }
            IntPtr pointer = GetNativeTexturePointer(Texture);
            if (pointer == IntPtr.Zero)
            {
                // Unity still hasn't made it, which graphics jobs make likelier.
                // GetNativeTexturePointer queued another try, and native already
                // has no texture, so there's nothing to send yet.
                return;
            }
            AdvanceGeneration();
            CanvasNative.UpdateRenderTexture(
                m_nativeRenderQueue,
                pointer,
                (uint)Texture.width,
                (uint)Texture.height
            );
            m_delayed = false;
        }

        /// <summary>
        /// Update the render queue's target texture.
        /// </summary>
        public void UpdateTexture(RenderTexture texture)
        {
            ValidateRenderTexture(texture);
            IntPtr pointer = GetNativeTexturePointer(texture);
            AdvanceGeneration();
            CanvasNative.UpdateRenderTexture(
                m_nativeRenderQueue,
                pointer,
                (uint)texture.width,
                (uint)texture.height
            );
            Texture = texture;
        }

        private NativeRenderQueueHandle m_nativeRenderQueue;
        private uint m_renderId;


        #region Native Methods
        [DllImport(NativeLibrary.name)]
        private static extern IntPtr getRetireRenderQueueCallback();

        /// Tests. Whether server calls and render events can still reach it.
        internal static void RiveGetRenderQueueLiveness(
            NativeRenderQueueHandle renderQueue,
            uint renderId,
            out bool handleLive,
            out bool renderLive)
        {
            CanvasNative.GetRenderQueueLiveness(renderQueue, renderId, out handleLive, out renderLive);
        }

        [DllImport(NativeLibrary.name)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool supportsDrawingToScreen();

        public static bool SupportsDrawingToScreen()
        {
            return supportsDrawingToScreen();
        }
        #endregion
    }
}
