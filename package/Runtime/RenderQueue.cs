using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using System.Numerics;

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

    public class Renderer
    {
        protected RenderQueue m_renderQueue;
        private IntPtr m_nativeRenderQueue = IntPtr.Zero;
        private uint m_index;

        internal Renderer(RenderQueue queue)
        {
            m_renderQueue = queue;
            m_nativeRenderQueue = queue.m_nativeRenderQueue;
            m_index = getNextCommandBufferIndex(m_nativeRenderQueue);
        }

        void Release()
        {
            releaseCommandBuffer(m_nativeRenderQueue, m_index);
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
            renderQueueDrawArtboard(m_nativeRenderQueue, m_index, artboard.NativeArtboard);
        }

        /// <summary>
        /// Draw the given path and paint to the render queue.
        /// </summary>
        public void Draw(Path path, Paint paint)
        {
            renderQueueDrawPath(m_nativeRenderQueue, m_index, path.NativePath, paint.NativePaint);
        }

        /// <summary>
        /// Clip the render queue to the given path.
        /// </summary>
        public void Clip(Path path)
        {
            renderQueueClipPath(m_nativeRenderQueue, m_index, path.NativePath);
        }

        /// <summary>
        /// Save the current render queue state.
        /// </summary>
        public void Save()
        {
            renderQueueSave(m_nativeRenderQueue, m_index);
        }

        /// <summary>
        /// Restore the last saved render queue state.
        /// </summary>
        public void Restore()
        {
            renderQueueRestore(m_nativeRenderQueue, m_index);
        }

        /// <summary>
        /// Transform the render queue by the given translation.
        /// </summary>
        public void Translate(System.Numerics.Vector2 translation)
        {
            renderQueueTransform(
                m_nativeRenderQueue,
                m_index,
                1.0f,
                0.0f,
                0.0f,
                1.0f,
                translation.X,
                translation.Y
            );
        }

        /// <summary>
        /// Transform the render queue by the given translation.
        /// </summary>
        public void Translate(float x, float y)
        {
            renderQueueTransform(m_nativeRenderQueue, m_index, 1.0f, 0.0f, 0.0f, 1.0f, x, y);
        }

        /// <summary>
        /// Transform the render queue by the given matrix.
        /// </summary>
        public void Transform(Matrix3x2 matrix)
        {
            renderQueueTransform(
                m_nativeRenderQueue,
                m_index,
                matrix.M11,
                matrix.M12,
                matrix.M21,
                matrix.M22,
                matrix.M31,
                matrix.M32
            );
        }

        /// <summary>
        /// Align the artboard to the given fit and alignment.
        /// </summary>
        public void Align(Fit fit, Alignment alignment, Artboard artboard)
        {
            if (artboard == null)
            {
                throw new ArgumentException("A non null artboard must be provided.");
            }
            renderQueueAlign(
                m_nativeRenderQueue,
                m_index,
                (byte)fit,
                alignment.X,
                alignment.Y,
                artboard.NativeArtboard
            );
        }

        public void Submit()
        {
            var commandBuffer = new RiveCommandBuffer(this);
            if (m_renderQueue.Texture != null)
            {
                commandBuffer.SetRenderTarget(m_renderQueue.Texture);
            }
            commandBuffer.IssuePluginEventAndData(
                getRenderCommandBufferCallback(),
                (int)m_index,
                m_nativeRenderQueue
            );
            Graphics.ExecuteCommandBuffer(commandBuffer);
        }

        public void SubmitAndRelease()
        {
            var commandBuffer = new RiveCommandBuffer(this);
            if (m_renderQueue.Texture != null)
            {
                commandBuffer.SetRenderTarget(m_renderQueue.Texture);
            }
            commandBuffer.IssuePluginEventAndData(
                getRenderAndReleaseCommandBufferCallback(),
                (int)m_index,
                m_nativeRenderQueue
            );
            Graphics.ExecuteCommandBuffer(commandBuffer);
        }

        public CommandBuffer ToCommandBuffer()
        {
            var commandBuffer = new RiveCommandBuffer(this);
            AddToCommandBuffer(commandBuffer);
            return commandBuffer;
        }

        public void AddToCommandBuffer(CommandBuffer commandBuffer, bool release = false)
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
            commandBuffer.IssuePluginEventAndData(
                release
                    ? getRenderAndReleaseCommandBufferCallback()
                    : getRenderCommandBufferCallback(),
                (int)m_index,
                m_nativeRenderQueue
            );
            commandBuffer.IssuePluginEvent(getInvalidateState(), 0);
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
        protected static extern uint getNextCommandBufferIndex(IntPtr renderQueue);

        [DllImport(NativeLibrary.name)]
        internal static extern IntPtr getRenderCommandBufferCallback();

        [DllImport(NativeLibrary.name)]
        internal static extern void releaseCommandBuffer(
            IntPtr renderQueue,
            uint commandBufferIndex
        );

        [DllImport(NativeLibrary.name)]
        internal static extern void renderQueueDrawArtboard(
            IntPtr renderQueue,
            uint commandBufferIndex,
            IntPtr artboard
        );

        [DllImport(NativeLibrary.name)]
        internal static extern void renderQueueDrawPath(
            IntPtr renderQueue,
            uint commandBufferIndex,
            IntPtr path,
            IntPtr Paint
        );

        [DllImport(NativeLibrary.name)]
        internal static extern void renderQueueClipPath(
            IntPtr renderQueue,
            uint commandBufferIndex,
            IntPtr path
        );

        [DllImport(NativeLibrary.name)]
        internal static extern void renderQueueSave(IntPtr renderQueue, uint commandBufferIndex);

        [DllImport(NativeLibrary.name)]
        internal static extern void renderQueueRestore(IntPtr renderQueue, uint commandBufferIndex);

        [DllImport(NativeLibrary.name)]
        internal static extern void renderQueueTransform(
            IntPtr renderQueue,
            uint commandBufferIndex,
            float xx,
            float xy,
            float yx,
            float yy,
            float tx,
            float ty
        );

        [DllImport(NativeLibrary.name)]
        internal static extern void renderQueueAlign(
            IntPtr renderQueue,
            uint commandBufferIndex,
            byte fit,
            float alignX,
            float alignY,
            IntPtr artboard
        );

        [DllImport(NativeLibrary.name)]
        internal static extern IntPtr getRenderAndReleaseCommandBufferCallback();

        [DllImport(NativeLibrary.name)]
        internal static extern IntPtr getInvalidateState();
        #endregion
    }

    public class RenderQueue
    {
        public RenderTexture Texture { get; private set; }

        public RenderQueue(RenderTexture texture = null, bool clear = true)
        {
            Texture = texture;
            ValidateRenderTexture(texture, true);
            if (texture != null)
            {
                texture.Create();
            }
            m_nativeRenderQueue = makeRenderQueue(
                texture == null ? IntPtr.Zero : texture.GetNativeTexturePtr(),
                (uint)(texture?.width ?? 0),
                (uint)(texture?.height ?? 0),
                clear
            );
        }

        public Renderer Renderer()
        {
            return new Renderer(this);
        }

        ~RenderQueue()
        {
            unrefRenderQueue(m_nativeRenderQueue);
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

        /// <summary>
        /// Update the render queue's target texture.
        /// </summary>
        public void UpdateTexture(RenderTexture texture)
        {
            ValidateRenderTexture(texture);
            renderQueueUpdateRenderTexture(
                m_nativeRenderQueue,
                texture.GetNativeTexturePtr(),
                (uint)texture.width,
                (uint)texture.height
            );
        }

        internal IntPtr m_nativeRenderQueue = IntPtr.Zero;

        #region Native Methods
        [DllImport(NativeLibrary.name)]
        protected static extern IntPtr makeRenderQueue(
            IntPtr renderTexture,
            uint width,
            uint height,
            bool clear
        );

        [DllImport(NativeLibrary.name)]
        internal static extern void unrefRenderQueue(IntPtr renderQueue);

        [DllImport(NativeLibrary.name)]
        internal static extern void renderQueueUpdateRenderTexture(
            IntPtr renderQueue,
            IntPtr texture,
            uint width,
            uint heigh
        );

        [DllImport(NativeLibrary.name)]
        public static extern bool supportsDrawingToScreen();
        #endregion
    }
}
