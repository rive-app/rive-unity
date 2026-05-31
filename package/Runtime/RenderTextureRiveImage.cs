using System;
using System.Runtime.InteropServices;
using UnityEngine;

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
    /// For dynamic sources call Refresh() each frame so the wrap stays in
    /// sync with new contents.
    /// </remarks>
    public sealed class RenderTextureRiveImage : IDisposable
    {
        private IntPtr m_nativePtr;

        public IntPtr NativePtr => m_nativePtr;
        public bool IsValid => m_nativePtr != IntPtr.Zero;

        public RenderTextureRiveImage(RenderTexture renderTexture)
        {
            Refresh(renderTexture);
        }

        public void Refresh(RenderTexture renderTexture)
        {
            if (m_nativePtr != IntPtr.Zero)
            {
                unrefRiveImage(m_nativePtr);
                m_nativePtr = IntPtr.Zero;
            }
            if (renderTexture == null)
            {
                return;
            }
            IntPtr handle = renderTexture.GetNativeTexturePtr();
            if (handle == IntPtr.Zero)
            {
                return;
            }
            // sRGB hint drives view-format selection on D3D11/D3D12 so
            // gamma-encoded sources sample correctly in linear projects.
            m_nativePtr = makeRenderImageFromNativeTexture(
                handle,
                (uint)renderTexture.width,
                (uint)renderTexture.height,
                renderTexture.sRGB);
        }

        public void Dispose()
        {
            if (m_nativePtr != IntPtr.Zero)
            {
                unrefRiveImage(m_nativePtr);
                m_nativePtr = IntPtr.Zero;
            }
        }

        [DllImport(NativeLibrary.name)]
        private static extern IntPtr makeRenderImageFromNativeTexture(
            IntPtr nativeTexture, uint width, uint height,
            [MarshalAs(UnmanagedType.I1)] bool isSRGB);

        [DllImport(NativeLibrary.name)]
        private static extern void unrefRiveImage(IntPtr image);
    }
}
