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
    public class RenderQueue
    {
        // public static RenderQueue forTexture(RenderTexture texture)
        // {
        //     // User knows what they want, they'll do something with the texture.
        //     return new TextureRenderQueue(texture);
        // }

        // public static RenderQueue forCamera(Camera camera)
        // {
        //     if (
        //         UnityEngine.SystemInfo.graphicsDeviceType
        //         == UnityEngine.Rendering.GraphicsDeviceType.Metal
        //     )
        //     {
        //         // On Metal we support rendering directly to the framebuffer.
        //         return new CameraRenderQueue(camera);
        //     }
        //     else
        //     {
        //         // We need to render to a texture and then blit it for the
        //         // camera.
        //         return new CameraTextureRenderQueue(camera);
        //     }
        // }

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
            var commandBuffer = new CommandBuffer();
            commandBuffer.IssuePluginEventAndData(getSubmitQueueCallback(), 0, m_nativeRenderQueue);
            Graphics.ExecuteCommandBuffer(commandBuffer);
            // camera.AddCommandBuffer(CameraEvent.AfterEverything, commandBuffer);
        }

        public CommandBuffer toCommandBuffer()
        {
            var commandBuffer = new CommandBuffer();
            addToCommandBuffer(commandBuffer);
            return commandBuffer;
        }

        public void addToCommandBuffer(CommandBuffer commandBuffer)
        {
            if (UnityEngine.SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Metal)
            {
                // Unity seems to have the wrong texture bound when querying the
                // exposed CurrentRenderPassDescriptor's colorAttachment. This
                // forces the Metal backend to catch up.
                commandBuffer.DrawMesh(getResetMesh(), new UnityEngine.Matrix4x4(), getResetMaterial());
            }
            commandBuffer.IssuePluginEventAndData(getSubmitQueueCallback(), 0, m_nativeRenderQueue);
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
        internal static extern IntPtr unrefRenderQueue(IntPtr renderQueue);

        [DllImport(NativeLibrary.name)]
        internal static extern IntPtr getSubmitQueueCallback();

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

    // class TextureRenderQueue : RenderQueue
    // {
    //     internal TextureRenderQueue(RenderTexture texture)
    //     {
    //         texture.Create();
    //         m_nativeRenderQueue = makeRenderQueue(texture.GetNativeTexturePtr());
    //     }
    // }

    // class CameraRenderQueue : RenderQueue
    // {
    //     internal CameraRenderQueue(Camera camera)
    //     {
    //         m_nativeRenderQueue = makeRenderQueue(IntPtr.Zero);
    //     }
    // }

    // class CameraTextureRenderQueue : RenderQueue
    // {
    //     private Camera m_camera;
    //     private RenderTexture m_renderTexture;
    //     private int m_pixelWidth;
    //     private int m_pixelHeight;
    //     private CommandBuffer m_commandBuffer;

    //     internal CameraTextureRenderQueue(Camera camera)
    //     {
    //         m_camera = camera;
    //         updateTexture();
    //     }

    //     ~CameraTextureRenderQueue()
    //     {
    //         cleanup();
    //     }

    //     void cleanup()
    //     {
    //         if (m_renderTexture != null)
    //         {
    //             m_renderTexture.Release();
    //         }
    //         if (m_commandBuffer != null)
    //         {
    //             m_camera.RemoveCommandBuffer(CameraEvent.AfterEverything, m_commandBuffer);
    //             m_commandBuffer = null;
    //         }
    //         unrefRenderQueue(m_nativeRenderQueue);
    //     }

    //     void updateTexturdoublede()
    //     {
    //         if (m_pixelWidth == m_camera.pixelWidth && m_pixelHeight == m_camera.pixelHeight)
    //         {
    //             return;
    //         }
    //         cleanup();

    //         m_renderTexture = new RenderTexture(
    //             m_camera.pixelWidth,
    //             m_camera.pixelHeight,
    //             0,
    //             RenderTextureFormat.ARGB32
    //         );
    //         m_renderTexture.Create();
    //         m_nativeRenderQueue = makeRenderQueue(m_renderTexture.GetNativeTexturePtr());

    //         m_commandBuffer = toCommandBuffer();
    //         m_camera.AddCommandBuffer(CameraEvent.AfterEverything, m_commandBuffer);
    //     }
    // }
}
