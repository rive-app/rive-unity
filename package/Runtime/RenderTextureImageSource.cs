#if RIVE_USING_EXPERIMENTAL
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Rive.Utils;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rive
{
    /// <summary>
    /// Wraps a Unity RenderTexture as a Rive RenderImage for binding to a
    /// view-model image property (VideoPlayer output, camera output, custom
    /// GPU content).
    /// </summary>
    /// <remarks>
    /// Supported: Metal, D3D11, D3D12, Vulkan. OpenGL safe-fails.
    ///
    /// Requirements:
    ///   - The source must be a stable user-allocated RenderTexture asset.
    ///     Transient RenderGraph resources alias their backing memory and
    ///     will produce stale/garbage samples or crashes.
    ///   - Single-sample, non-array, 2D textures only. MSAA / array / cube /
    ///     3D sources are rejected with a log.
    ///   - Rive composites through an 8-bit internal RT, so HDR source
    ///     values above 1.0 are clamped at the Rive layer (downstream
    ///     tone-mapping sees the clamped values).
    ///
    /// For live sources (video, camera) use <see cref="RefreshMode.PerFrame"/>
    /// (the default) and the image keeps itself up to date. For content that
    /// does not change, use <see cref="RefreshMode.Manual"/> and call
    /// <see cref="Refresh"/> when it does.
    ///
    /// Call <see cref="Dispose"/> when you're done to stop updates and free
    /// resources.
    /// </remarks>
    public sealed class RenderTextureImageSource : IDisposable
    {
        /// <summary>
        /// How much we process a source texture before Rive samples it. Some backends
        /// store a Unity RenderTexture upside-down, and in Linear projects its colors are encoded
        /// differently than Rive expects.These are opt-in flags; each still only kicks in when
        /// it's actually needed.
        /// </summary>
        public enum TextureProcessingMode
        {
            /// <summary>Determine the processing mode based on the backend and project color space. Handles both orientation and color (each only where needed). Default.</summary>
            Auto = 0,
            /// <summary>Flip the texture so it's not upside-down on backends that store texels top-down; leave color alone.</summary>
            Orientation = 1,
            /// <summary>Re-encode the texture to gamma so colors composite correctly in Linear projects; leave orientation alone.</summary>
            Color = 2,
            /// <summary>Bind the texture as-is. No intermediate texture.</summary>
            None = 3,
        }

        /// <summary>
        /// Controls how the image keeps up with changes to the source texture.
        /// </summary>
        public enum RefreshMode
        {
            /// <summary>
            /// Keep up to date automatically every frame. Use for live sources
            /// such as video or camera output whose contents change each frame.
            /// </summary>
            PerFrame,
            /// <summary>
            /// Update only when you call <see cref="Refresh"/>. Use for content
            /// that does not change, or that you update yourself (a snapshot, a
            /// baked texture).
            /// </summary>
            Manual,
        }

        private readonly RefreshMode m_refreshMode;
        // Hands back a wrap-ready texture each frame.
        private readonly ITextureFrameProvider m_textureProvider;
        // Identifies this image within the command queue. The image lifetime lives
        // entirely on the native side, keyed by this handle.
        private readonly uint m_handle;

        private bool m_disposed;
        private bool m_refreshRequested;
        private bool m_pendingFollowUpRebuild;
        private bool m_needsInitialBuild = true;
        // Set when a new property binds, so the next tick re-pushes the image to it.
        private bool m_rebindRequested;
        // Tracks whether we've already reacted to the source dying, so a dead Manual
        // source clears its properties once instead of every frame.
        private bool m_handledSourceLoss;

        internal ITextureFrameProvider FrameProvider => m_textureProvider;

        internal uint Handle => m_handle;

        /// <summary>
        /// Whether the image can currently produce frames. False on unsupported
        /// backends or once the source texture is destroyed/released. The actual
        /// wrap happens on the render thread
        /// </summary>
        public bool IsValid => !m_disposed && m_textureProvider != null
            && m_textureProvider.IsSourceAlive
            && TextureHelper.SupportsRenderTextureImageSource();

        /// <summary>The source RenderTexture this image draws from.</summary>
        public RenderTexture Source => m_textureProvider?.Source;

        internal bool IsDisposed => m_disposed;

        /// <param name="source">The RenderTexture to use as the image source. Required.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="source"/> is null.
        /// </exception>
        public RenderTextureImageSource(RenderTexture source)
            : this(source, TextureProcessingMode.Auto, RefreshMode.PerFrame)
        {
        }

        /// <param name="source">The RenderTexture to use as the image source. Required.</param>
        /// <param name="processing">Which processing mode to apply before Rive samples the source.</param>
        /// <param name="refreshMode">How the image keeps up with source changes.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="source"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="source"/> is MSAA, an array, a cube, or 3D.
        /// </exception>
        public RenderTextureImageSource(
            RenderTexture source,
            TextureProcessingMode processing = TextureProcessingMode.Auto,
            RefreshMode refreshMode = RefreshMode.PerFrame)
        {
            if (source == null)
            {
                throw new ArgumentNullException(
                    nameof(source),
                    "RenderTextureImageSource requires a non-null source RenderTexture.");
            }

            if (!IsSupportedSource(source, out string reason))
            {
                throw new ArgumentException(
                    "RenderTextureImageSource requires a single-sample, non-array, 2D " +
                    $"RenderTexture. {reason}",
                    nameof(source));
            }
            m_textureProvider = TextureFrameProvider.Create(source, processing);
            m_refreshMode = refreshMode;
            m_handle = RenderImageCommandQueue.NextHandle();
            RenderImageCommandQueue.LogUnsupportedBackendOnce();
        }

        // Mirrors the native shape check (sampleCount/array/type) so an unusable
        // source is caught at construction instead of silently on the render thread.
        private static bool IsSupportedSource(RenderTexture source, out string reason)
        {
            if (source.dimension != TextureDimension.Tex2D)
            {
                reason = $"Source dimension is {source.dimension}; cube, array, and 3D are not supported.";
                return false;
            }
            if (source.antiAliasing > 1 || source.descriptor.msaaSamples > 1)
            {
                reason = $"Source is MSAA (antiAliasing={source.antiAliasing}).";
                return false;
            }
            if (source.volumeDepth != 1)
            {
                reason = $"Source has volumeDepth={source.volumeDepth}; expected 1.";
                return false;
            }
            reason = null;
            return true;
        }

        /// <summary>
        /// Updates the image from the current contents of the source texture.
        /// Call this in <see cref="RefreshMode.Manual"/> when the source changes;
        /// any bound properties update automatically. Has no effect after
        /// <see cref="Dispose"/>.
        /// </summary>
        public void Refresh()
        {
            if (m_disposed)
            {
                return;
            }
            // Queue the work for the next manager Tick rather than rebuilding +
            // pushing here so it fits within the Orchestrator's timing sequence.
            m_refreshRequested = true;
        }

        // Forces the next tick to re-push the image so a freshly-bound property
        // picks it up (matters for Manual sources that don't rebuild every frame).
        internal void NotifyPropertyBound()
        {
            m_rebindRequested = true;
        }

        // Called by the manager once per frame for every registered image, right
        // before panels tick. Decides whether a rebuild is wanted, then queues a
        // build (or clear) command on the queue for the render thread to process.
        internal void Tick(IReadOnlyList<ViewModelInstanceImageProperty> properties, RenderImageCommandQueue queue)
        {
            if (m_disposed)
            {
                return;
            }

            // In Manual mode nothing else triggers a rebuild, so cheaply poll for
            // the source dying (no blit) and force a clear when it does.
            bool sourceJustLost = false;
            if (m_refreshMode == RefreshMode.Manual)
            {
                bool sourceAlive = m_textureProvider.IsSourceAlive;
                sourceJustLost = !sourceAlive && !m_handledSourceLoss;
                m_handledSourceLoss = !sourceAlive;
            }

            bool rebuild = m_refreshMode == RefreshMode.PerFrame
                || m_refreshRequested
                || m_pendingFollowUpRebuild
                || m_needsInitialBuild
                || m_rebindRequested
                || sourceJustLost;

            if (!rebuild)
            {
                return;
            }

            // The first build, a manual refresh, and a new binding each need one
            // more rebuild next frame before the change is visible; native binding
            // propagation isn't immediate otherwise. PerFrame rebuilds anyway.
            // TODO: look into why the follow-up rebuild is needed.
            bool discreteTrigger = m_needsInitialBuild || m_refreshRequested || m_rebindRequested;
            m_needsInitialBuild = false;
            m_refreshRequested = false;
            m_rebindRequested = false;
            m_pendingFollowUpRebuild = discreteTrigger;

            NativeTextureFrame frame = m_textureProvider.Acquire();
            if (frame.IsValid)
            {
                queue.EnqueueBuild(
                    m_handle, frame.Handle, frame.Width, frame.Height, frame.IsSRGB, properties);
            }
            else if (!m_textureProvider.IsSourceAlive)
            {
                // Destroyed/released source: clear the bound properties and drop the image.
                queue.EnqueueClear(m_handle, properties);
            }
            // Otherwise the source just isn't ready yet (handle still 0); retry next
            // frame without clearing.
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }
            m_disposed = true;

            // Unregister drops the binding, which queues the native image's destroy.
            // The image's lifetime lives on the native queue, so there's nothing to
            // free here
            RenderTextureImageManager.Instance.Unregister(this);
            // Frees the source's GPU resources.
            m_textureProvider?.Dispose();
        }
    }

    /// <summary>
    ///The main thread queues handle-based build/destroy commands and triggers one drain per frame; the
    /// native queue owns the actual RenderImage lifetimes.
    /// </summary>
    internal sealed class RenderImageCommandQueue
    {
        // 0 is reserved as "no handle". Handles are unique within a session.
        private static uint s_nextHandle = 1;
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

        private RenderImageCommandQueue() { }

        // Whether anything has been queued since the last flush, so an idle frame
        // doesn't issue a command buffer. Only touched on the main thread.
        private bool m_pending;
        // Reused buffer for marshalling a build's property pointers.
        private IntPtr[] m_propertyScratch = new IntPtr[8];
        private CommandBuffer m_commandBuffer;

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

        // Drains anything queued this frame, including destroys enqueued between
        // frames after the last binding went away. No-op if never created.
        internal static void FlushIfActive()
        {
            s_instance?.Flush();
        }

        public void EnqueueBuild(
            uint handle, IntPtr nativeTexture, int width, int height, bool isSRGB,
            IReadOnlyList<ViewModelInstanceImageProperty> properties)
        {
            int count = 0;
            if (properties != null)
            {
                EnsureScratch(properties.Count);
                for (int i = 0; i < properties.Count; i++)
                {
                    ViewModelInstanceImageProperty property = properties[i];
                    if (property != null)
                    {
                        m_propertyScratch[count++] = property.InstancePropertyPtr;
                    }
                }
            }

            enqueueBuildRenderImageCommand(
                handle, nativeTexture, (uint)width, (uint)height, isSRGB, m_propertyScratch, count);
            m_pending = true;
        }

        // A clear is a build with no texture: the render thread nulls the bound
        // properties and drops the image.
        public void EnqueueClear(uint handle, IReadOnlyList<ViewModelInstanceImageProperty> properties)
        {
            EnqueueBuild(handle, IntPtr.Zero, 0, 0, false, properties);
        }

        public void EnqueueDestroy(uint handle)
        {
            enqueueDestroyRenderImageCommand(handle);
            m_pending = true;
        }

        // Issues the single per-frame drain event. We stamp the event with the current
        // generation so a teardown that bumps the generation drops this drain rather
        // than letting it run against a new session's activity.
        public void Flush()
        {
            if (!m_pending)
            {
                return;
            }
            m_pending = false;

            if (m_commandBuffer == null)
            {
                m_commandBuffer = new CommandBuffer { name = "Rive.RenderImageCommands" };
            }
            else
            {
                m_commandBuffer.Clear();
            }
            int generation = (int)getRenderImageGeneration();
            m_commandBuffer.IssuePluginEventAndData(
                getProcessRenderImageCommandsCallback(), generation, IntPtr.Zero);
            Graphics.ExecuteCommandBuffer(m_commandBuffer);
        }

        // Full teardown for explicit lifecycle points (manager Clear / runtime
        // init), main thread only. Resets the global native queue (frees its
        // images + bumps the generation so in-flight drains are dropped), releases
        // the managed CommandBuffer, and drops the singleton so the next session
        // rebuilds clean. 
        internal static void Shutdown()
        {
            clearRenderImageCommandQueue();
            if (s_instance != null)
            {
                s_instance.ReleaseManaged();
                s_instance = null;
            }
        }

        private void ReleaseManaged()
        {
            if (m_commandBuffer != null)
            {
                m_commandBuffer.Release();
                m_commandBuffer = null;
            }
            m_pending = false;
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

        private void EnsureScratch(int count)
        {
            if (m_propertyScratch.Length >= count)
            {
                return;
            }
            int size = m_propertyScratch.Length;
            while (size < count)
            {
                size *= 2;
            }
            m_propertyScratch = new IntPtr[size];
        }

        [DllImport(NativeLibrary.name)]
        private static extern uint getRenderImageGeneration();

        [DllImport(NativeLibrary.name)]
        private static extern void clearRenderImageCommandQueue();

        [DllImport(NativeLibrary.name)]
        private static extern void enqueueBuildRenderImageCommand(
            uint handle, IntPtr nativeTexture, uint width, uint height,
            [MarshalAs(UnmanagedType.I1)] bool isSRGB, IntPtr[] propertyPtrs, int propertyCount);

        [DllImport(NativeLibrary.name)]
        private static extern void enqueueDestroyRenderImageCommand(uint handle);

        [DllImport(NativeLibrary.name)]
        private static extern IntPtr getProcessRenderImageCommandsCallback();
    }

    /// <summary>
    /// Owns the <see cref="RenderTextureImageSource"/> to view-model-property bindings
    /// and ticks every bound image once per frame from the Orchestrator.
    /// </summary>
    /// <remarks>
    /// An image is registered (and kept alive by a strong reference) only while it
    /// is bound to at least one property, so a bound image keeps updating even if
    /// the caller doesn't hold a reference. When its last property is removed
    /// (cleared, rebound elsewhere, or its owning view model disposed) the strong
    /// reference is dropped and the native image is queued for destruction. .
    /// </remarks>
    internal sealed class RenderTextureImageManager
    {
        private static RenderTextureImageManager s_instance;

        internal static RenderTextureImageManager Instance
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = new RenderTextureImageManager();
                }
                return s_instance;
            }
        }

        private RenderTextureImageManager() { }

        // Clears static state that can outlive a reload: editor domain reload may
        // be disabled and Unity-as-a-Library reuses the process, so this runs in
        // player too. Shutdown first so the queue singleton's CommandBuffer is
        // released before we drop our reference to it. Note this does NOT refire on
        // subsequent UaaL runEmbedded calls, so repeated reloads lean on Clear()
        // (Orchestrator.OnDestroy) and the native device-shutdown reset.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            RenderImageCommandQueue.Shutdown();
            s_instance = null;
            RenderImageCommandQueue.ResetHandles();
        }

        private sealed class Binding
        {
            // While an image has bound properties the manager
            // keeps it alive with a strong reference so it keeps ticking without the caller holding it.
            public readonly RenderTextureImageSource Image;
            public readonly List<ViewModelInstanceImageProperty> Properties =
                new List<ViewModelInstanceImageProperty>();

            public Binding(RenderTextureImageSource image)
            {
                Image = image;
            }
        }

        private readonly List<Binding> m_bindings = new List<Binding>();
        // Intermediates whose source died/realloc'd. So we defer releasing them until the next frame,
        // after any build referencing them has drained on the render thread.
        private readonly List<RenderTexture> m_deferredReleases = new List<RenderTexture>();

        /// <summary>
        /// True when at least one render-texture image is currently bound. Lets
        /// callers skip the manager entirely (no singleton instantiation, no
        /// iteration) on the common path where nothing uses render textures.
        /// </summary>
        internal static bool HasAnyBindings =>
            s_instance != null && s_instance.m_bindings.Count > 0;

        internal int BindingCount => m_bindings.Count;

        // Removes an image's binding entry outright (e.g. on explicit Dispose),
        // regardless of how many properties it had.
        internal void Unregister(RenderTextureImageSource image)
        {
            if (image == null)
            {
                return;
            }
            for (int i = m_bindings.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(m_bindings[i].Image, image))
                {
                    RemoveBindingAt(i);
                }
            }
        }

        /// <summary>
        /// Binds a property to an image, registering the image if needed. A property
        /// is only ever driven by one image, so it is detached from any others. The
        /// image is pushed to the property on the next tick.
        /// </summary>
        internal bool BindPropertyToImage(RenderTextureImageSource image, ViewModelInstanceImageProperty property)
        {
            if (image == null || property == null)
            {
                return false;
            }
            // A property can only be driven by one image at a time; detach it from
            // any other image, dropping that image's entry if it ends up unbound.
            for (int i = m_bindings.Count - 1; i >= 0; i--)
            {
                Binding other = m_bindings[i];
                if (!ReferenceEquals(other.Image, image) &&
                    other.Properties.Remove(property) &&
                    other.Properties.Count == 0)
                {
                    RemoveBindingAt(i);
                }
            }

            int idx = FindBindingIndex(image);
            Binding binding = idx >= 0 ? m_bindings[idx] : null;
            if (binding == null)
            {
                binding = new Binding(image);
                m_bindings.Add(binding);
            }
            if (!binding.Properties.Contains(property))
            {
                binding.Properties.Add(property);
            }

            // The render thread pushes the image; force a rebuild so the freshly
            // bound property receives it on the next tick.
            image.NotifyPropertyBound();
            return true;
        }

        /// <summary>
        /// Detaches a property from whatever image was driving it, dropping that
        /// image's entry if it becomes unbound.
        /// </summary>
        internal void Unbind(ViewModelInstanceImageProperty property)
        {
            if (property == null)
            {
                return;
            }
            for (int i = m_bindings.Count - 1; i >= 0; i--)
            {
                Binding binding = m_bindings[i];
                if (binding.Properties.Remove(property) && binding.Properties.Count == 0)
                {
                    RemoveBindingAt(i);
                }
            }
        }

        internal void Tick()
        {
            // Release last tick's freed intermediate textures.
            FlushDeferredReleases();

   
            if (m_bindings.Count > 0)
            {
                RenderImageCommandQueue queue = RenderImageCommandQueue.Instance;
                for (int i = m_bindings.Count - 1; i >= 0; i--)
                {
                    Binding binding = m_bindings[i];
                    PruneDisposedProperties(binding);
                    binding.Image.Tick(binding.Properties, queue);

                    if (binding.Properties.Count == 0)
                    {
                        // Last consumer pruned (e.g. owning view model disposed); drop
                        // the strong reference (and the native image) so the GC can
                        // reclaim the image source.
                        RemoveBindingAt(i);
                    }
                }
            }

            // Drain everything queued this frame on the render thread, in order.
            // Also drains destroys enqueued between frames after the last binding
            // went away.
            RenderImageCommandQueue.FlushIfActive();
        }

        /// <summary>
        /// Disposes every bound image and clears all bindings for deterministic native cleanup on Teardown.
        /// </summary>
        internal void Clear()
        {
            // Remove each entry before disposing so the Dispose -> Unregister
            // callback is a no-op and doesn't mutate the list mid-iteration.
            for (int i = m_bindings.Count - 1; i >= 0; i--)
            {
                RenderTextureImageSource image = m_bindings[i].Image;
                m_bindings.RemoveAt(i);
                image?.Dispose();
            }

            // Resets the global native queue 
            RenderImageCommandQueue.Shutdown();

            FlushDeferredReleases();
        }

        // Hands an intermediate over to be released a tick later, once any build
        // referencing it has drained on the render thread.
        internal void DeferRelease(RenderTexture intermediate)
        {
            if (intermediate != null)
            {
                m_deferredReleases.Add(intermediate);
            }
        }

        private void FlushDeferredReleases()
        {
            if (m_deferredReleases.Count == 0)
            {
                return;
            }
            for (int i = 0; i < m_deferredReleases.Count; i++)
            {
                RenderTexture rt = m_deferredReleases[i];
                if (rt != null)
                {
                    rt.Release();
                    UnityEngine.Object.Destroy(rt);
                }
            }
            m_deferredReleases.Clear();
        }

        // Drops a binding and queues the native image's destruction.
        private void RemoveBindingAt(int index)
        {
            Binding binding = m_bindings[index];
            m_bindings.RemoveAt(index);
            RenderImageCommandQueue.EnqueueDestroyIfActive(binding.Image.Handle);
        }

        private void PruneDisposedProperties(Binding binding)
        {
            List<ViewModelInstanceImageProperty> props = binding.Properties;
            for (int i = props.Count - 1; i >= 0; i--)
            {
                ViewModelInstanceImageProperty property = props[i];
                if (property == null ||
                    (property.RootInstance != null && property.RootInstance.IsDisposed))
                {
                    props.RemoveAt(i);
                }
            }
        }

        private int FindBindingIndex(RenderTextureImageSource image)
        {
            for (int i = 0; i < m_bindings.Count; i++)
            {
                if (ReferenceEquals(m_bindings[i].Image, image))
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
#endif // RIVE_USING_EXPERIMENTAL
