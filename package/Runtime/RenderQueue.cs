using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using LoadAction = UnityEngine.Rendering.RenderBufferLoadAction;
using StoreAction = UnityEngine.Rendering.RenderBufferStoreAction;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

using System.Collections.Generic;

namespace Rive
{
    public class RiveCommandBuffer : CommandBuffer
    {
        private RenderQueue m_renderQueue;

        public RiveCommandBuffer(RenderQueue renderQueue)
        {
            m_renderQueue = renderQueue;
        }
    }

    public class RenderQueue
    {
        public RenderQueue(RenderTexture texture = null)
        {
            if (texture != null)
            {
                texture.Create();
            }
            m_nativeRenderQueue = makeRenderQueue(
                texture == null ? IntPtr.Zero : texture.GetNativeTexturePtr()
            );
        }

        ~RenderQueue()
        {
            unrefRenderQueue(m_nativeRenderQueue);
        }

        public void draw(Artboard artboard)
        {
            renderQueueDrawArtboard(m_nativeRenderQueue, artboard.nativeArtboard);
        }

        public void draw(Path path, Paint paint)
        {
            renderQueueDrawPath(m_nativeRenderQueue, path.nativePath, paint.nativePaint);
        }

        public void clip(Path path)
        {
            renderQueueClipPath(m_nativeRenderQueue, path.nativePath);
        }

        public void save()
        {
            renderQueueSave(m_nativeRenderQueue);
        }

        public void restore()
        {
            renderQueueRestore(m_nativeRenderQueue);
        }

        public void transform(Mat2D matrix)
        {
            renderQueueTransform(m_nativeRenderQueue, matrix.xx, matrix.xy, matrix.yx, matrix.yy, matrix.tx, matrix.ty);
        }

        public void align(Fit fit, Alignment alignment, Artboard artboard)
        {
            renderQueueAlign(
                m_nativeRenderQueue,
                (byte)fit,
                alignment.x,
                alignment.y,
                artboard.nativeArtboard
            );
        }

        public void submit()
        {
            var commandBuffer = new RiveCommandBuffer(this);
            commandBuffer.IssuePluginEventAndData(getSubmitQueueCallback(), 0, m_nativeRenderQueue);
            Graphics.ExecuteCommandBuffer(commandBuffer);
        }

        public void submitAndClear()
        {
            var commandBuffer = new RiveCommandBuffer(this);
            commandBuffer.IssuePluginEventAndData(getSubmitAndClearQueueCallback(), 0, m_nativeRenderQueue);
            Graphics.ExecuteCommandBuffer(commandBuffer);
        }

        public CommandBuffer toCommandBuffer()
        {
            var commandBuffer = new RiveCommandBuffer(this);
            addToCommandBuffer(commandBuffer);
            return commandBuffer;
        }

        public void addToCommandBuffer(CommandBuffer commandBuffer)
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
                    getResetMesh(),
                    new UnityEngine.Matrix4x4(),
                    getResetMaterial()
                );
            }
            commandBuffer.IssuePluginEventAndData(getSubmitQueueCallback(), 0, m_nativeRenderQueue);
            commandBuffer.IssuePluginEvent(getInvalidateState(), 0);
        }

        private static UnityEngine.Material m_resetMaterial;
        private static UnityEngine.Mesh m_resetMesh;

        private static UnityEngine.Material getResetMaterial()
        {
            if (m_resetMaterial == null)
            {
                m_resetMaterial = new UnityEngine.Material(UnityEngine.Shader.Find("Unlit/Color"));
            }
            return m_resetMaterial;
        }

        private static UnityEngine.Mesh getResetMesh()
        {
            if (m_resetMesh == null)
            {
                m_resetMesh = new UnityEngine.Mesh();
            }
            return m_resetMesh;
        }

        public void updateTexture(RenderTexture texture)
        {
            renderQueueUpdateRenderTexture(m_nativeRenderQueue, texture.GetNativeTexturePtr());
        }

        protected IntPtr m_nativeRenderQueue = IntPtr.Zero;

        #region Native Methods
        [DllImport(NativeLibrary.name)]
        protected static extern IntPtr makeRenderQueue(IntPtr renderTexture);

        [DllImport(NativeLibrary.name)]
        internal static extern IntPtr getSubmitQueueCallback();

        [DllImport(NativeLibrary.name)]
        internal static extern void unrefRenderQueue(IntPtr renderQueue);

        [DllImport(NativeLibrary.name)]
        internal static extern IntPtr getInvalidateState();

        [DllImport(NativeLibrary.name)]
        internal static extern IntPtr getSubmitAndClearQueueCallback();

        [DllImport(NativeLibrary.name)]
        internal static extern void renderQueueUpdateRenderTexture(
            IntPtr renderQueue,
            IntPtr texture
        );

        [DllImport(NativeLibrary.name)]
        internal static extern void renderQueueDrawArtboard(IntPtr renderQueue, IntPtr artboard);

        [DllImport(NativeLibrary.name)]
        internal static extern void renderQueueDrawPath(IntPtr renderQueue, IntPtr path, IntPtr Paint);

        [DllImport(NativeLibrary.name)]
        internal static extern void renderQueueClipPath(IntPtr renderQueue, IntPtr path);

        [DllImport(NativeLibrary.name)]
        internal static extern void renderQueueSave(IntPtr renderQueue);

        [DllImport(NativeLibrary.name)]
        internal static extern void renderQueueRestore(IntPtr renderQueue);

        [DllImport(NativeLibrary.name)]
        internal static extern void renderQueueTransform(IntPtr renderQueue, float xx, float xy, float yx, float yy, float tx, float ty);

        [DllImport(NativeLibrary.name)]
        public static extern bool supportsDrawingToScreen();

        [DllImport(NativeLibrary.name)]
        internal static extern void renderQueueAlign(
            IntPtr renderQueue,
            byte fit,
            float alignX,
            float alignY,
            IntPtr artboard
        );
        #endregion
    }
}
