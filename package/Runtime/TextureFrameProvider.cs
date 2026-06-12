#if RIVE_USING_EXPERIMENTAL
using System;
using UnityEngine;

namespace Rive
{
    /// <summary>
    /// The wrap-ready texture for one frame: the handle Rive adopts plus what it
    /// needs to read it correctly.
    /// </summary>
    internal readonly struct NativeTextureFrame
    {
        public readonly IntPtr Handle;
        public readonly int Width;
        public readonly int Height;

        // Lets D3D11/D3D12 pick the right view format so the texture samples in
        // the color space we mean.
        public readonly bool IsSRGB;

        public NativeTextureFrame(IntPtr handle, int width, int height, bool isSRGB)
        {
            Handle = handle;
            Width = width;
            Height = height;
            IsSRGB = isSRGB;
        }

        public bool IsValid => Handle != IntPtr.Zero;

        public static NativeTextureFrame Invalid => default;
    }

    /// <summary>
    /// Produces a native-ready <see cref="NativeTextureFrame"/> from a Unity
    /// RenderTexture each frame, optionally flipping or color-correcting.
    /// </summary>
    internal interface ITextureFrameProvider : IDisposable
    {
        /// <summary>
        /// The user's original texture.
        /// </summary>
        RenderTexture Source { get; }

        /// <summary>
        /// Whether the source is still usable (not destroyed or released).
        /// </summary>
        bool IsSourceAlive { get; }

        /// <summary>
        /// Returns the texture to wrap this frame (running any flip/color work), or
        /// an invalid frame if the source isn't ready yet (e.g. handle is 0, or an unsupported backend).
        /// </summary>
        NativeTextureFrame Acquire();
    }

    /// <summary>
    /// Picks the right <see cref="ITextureFrameProvider"/> for a source + mode.
    /// </summary>
    internal static class TextureFrameProvider
    {
        /// <summary>
        /// Returns true when any transform is needed, i.e. we need an intermediate.
        /// </summary>
        /// <param name="mode">The processing mode to use.</param>
        /// <param name="backendNeedsFlip">Whether the backend needs to flip the texture.</param>
        /// <param name="projectNeedsColorFix">Whether the project needs to color-correct the texture.</param>
        /// <param name="flip">Whether to flip the texture.</param>
        /// <param name="color">Whether to color-correct the texture.</param>
        /// <returns>True when any transform is needed, i.e. we need an intermediate.</returns>
        internal static bool ResolveTransforms(
            RenderTextureImageSource.TextureProcessingMode mode,
            bool backendNeedsFlip,
            bool projectNeedsColorFix,
            out bool flip,
            out bool color)
        {
            // The mode says which transforms the caller wants; each still only runs
            // when it's actually needed (the backend stores top-down / Linear project).
            bool allowFlip =
                mode == RenderTextureImageSource.TextureProcessingMode.Auto ||
                mode == RenderTextureImageSource.TextureProcessingMode.Orientation;
            bool allowColor =
                mode == RenderTextureImageSource.TextureProcessingMode.Auto ||
                mode == RenderTextureImageSource.TextureProcessingMode.Color;

            flip = allowFlip && backendNeedsFlip;
            color = allowColor && projectNeedsColorFix;
            return flip || color;
        }

        internal static ITextureFrameProvider Create(
            RenderTexture source,
            RenderTextureImageSource.TextureProcessingMode mode)
        {
            bool needsIntermediate = ResolveTransforms(
                mode,
                SystemInfo.graphicsUVStartsAtTop,
                TextureHelper.ProjectNeedsColorSpaceFix,
                out bool flip,
                out bool color);

            return needsIntermediate
                ? (ITextureFrameProvider)new ProcessedTextureFrameProvider(source, flip, color)
                : new DirectTextureFrameProvider(source);
        }
    }

    /// <summary>
    /// Binds the user's texture straight through, no intermediate or blit. Used
    /// when there's nothing to flip or color-correct.
    /// </summary>
    internal sealed class DirectTextureFrameProvider : ITextureFrameProvider
    {
        private readonly RenderTexture m_source;

        public DirectTextureFrameProvider(RenderTexture source)
        {
            m_source = source;
        }

        public RenderTexture Source => m_source;

        // Destroyed (fake-null) or released (GPU backing gone). Either way,
        // the user is done with it and there's nothing to sample.
        public bool IsSourceAlive => m_source != null && m_source.IsCreated();

        public NativeTextureFrame Acquire()
        {
            if (!IsSourceAlive)
            {
                return NativeTextureFrame.Invalid;
            }
            IntPtr handle = m_source.GetNativeTexturePtr();
            if (handle == IntPtr.Zero)
            {
                return NativeTextureFrame.Invalid;
            }
            return new NativeTextureFrame(handle, m_source.width, m_source.height, m_source.sRGB);
        }

        public void Dispose() { }
    }

    /// <summary>
    /// Blits the user's texture into an owned non-sRGB intermediate, flipping
    /// and/or gamma re-encoding along the way, so Rive samples gamma bytes raw and
    /// the single display-time decode is right for the whole panel.
    /// </summary>
    internal sealed class ProcessedTextureFrameProvider : ITextureFrameProvider
    {
        private readonly RenderTexture m_source;
        private readonly bool m_flip;
        private readonly bool m_gammaEncode;

        private RenderTexture m_intermediate;
        // We own m_intermediate and only swap it on a size change, so its handle is
        // stable between those. Cache it to skip a per-frame GetNativeTexturePtr
        // sync; 0 means "fetch it again".
        private IntPtr m_intermediateHandle;

        private static readonly int s_flipYId = Shader.PropertyToID("_FlipY");
        private static readonly int s_gammaEncodeId = Shader.PropertyToID("_GammaEncode");

        public ProcessedTextureFrameProvider(RenderTexture source, bool flip, bool gammaEncode)
        {
            m_source = source;
            m_flip = flip;
            m_gammaEncode = gammaEncode;
        }

        public RenderTexture Source => m_source;

        public bool IsSourceAlive => m_source != null && m_source.IsCreated();

        public NativeTextureFrame Acquire()
        {
            if (!IsSourceAlive)
            {
                // Source destroyed or released, so free our GPU resources so we don't
                // keep a stale intermediate alive with orphaned content.
                ReleaseIntermediate();
                return NativeTextureFrame.Invalid;
            }

            EnsureIntermediate();

            Material mat = TextureHelper.TexturePrepareMaterial;
            if (mat != null)
            {
                mat.SetFloat(s_flipYId, m_flip ? 1f : 0f);
                mat.SetFloat(s_gammaEncodeId, m_gammaEncode ? 1f : 0f);
                BlitAndRestoreActive(m_source, m_intermediate, mat);
            }
            else
            {
                // Shader went missing somehow; keep frame without any transformations.
                // instead of hard-failing.
                BlitAndRestoreActive(m_source, m_intermediate, null);
            }

            if (m_intermediateHandle == IntPtr.Zero)
            {
                m_intermediateHandle = m_intermediate.GetNativeTexturePtr();
            }
            if (m_intermediateHandle == IntPtr.Zero)
            {
                return NativeTextureFrame.Invalid;
            }

            // The bytes we just wrote are gamma in a non-sRGB texture, so we tell Rive
            // not to sRGB-decode them on sample.
            return new NativeTextureFrame(
                m_intermediateHandle, m_intermediate.width, m_intermediate.height, false);
        }

        private void EnsureIntermediate()
        {
            // Format is fixed (below), so only a size change forces a realloc.
            if (m_intermediate != null &&
                m_intermediate.width == m_source.width &&
                m_intermediate.height == m_source.height)
            {
                return;
            }
            if (m_intermediate != null)
            {
                ReleaseIntermediate();
            }

            RenderTextureDescriptor desc = m_source.descriptor;
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;
            // Non-sRGB so our gamma bytes get stored raw and Rive can read them
            // without the hardware decoding them back.
            desc.graphicsFormat = TextureHelper.Format;
            desc.sRGB = false;

            m_intermediate = new RenderTexture(desc);
            m_intermediate.Create();
            // Fresh texture so we invalidate the cached handle.
            m_intermediateHandle = IntPtr.Zero;
        }

        public void Dispose()
        {
            ReleaseIntermediate();
        }

        private static void BlitAndRestoreActive(Texture source, RenderTexture destination, Material material)
        {
            RenderTexture previousActive = RenderTexture.active;
            try
            {
                if (material != null)
                {
                    Graphics.Blit(source, destination, material);
                }
                else
                {
                    Graphics.Blit(source, destination);
                }
            }
            finally
            {
                RenderTexture.active = previousActive;
            }
        }

        private void ReleaseIntermediate()
        {
            if (m_intermediate != null)
            {
                if (RenderTexture.active == m_intermediate)
                {
                    RenderTexture.active = null;
                }
                // A build referencing this handle may still be draining on
                // the render thread, so we defer releasing it until the next frame.
                RenderTextureImageManager.Instance.DeferRelease(m_intermediate);
                m_intermediate = null;
            }
            m_intermediateHandle = IntPtr.Zero;
        }
    }
}
#endif // RIVE_USING_EXPERIMENTAL
