using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

using LoadAction = UnityEngine.Rendering.RenderBufferLoadAction;
using StoreAction = UnityEngine.Rendering.RenderBufferStoreAction;

namespace Rive
{
    public class RiveTexture : MonoBehaviour
    {
        public TextAsset asset;
        public RenderTexture renderTexture;
        public Fit fit = Fit.contain;
        public Alignment alignment = Alignment.center;

        private RenderQueue m_renderQueue;
        private CommandBuffer m_commandBuffer;

        private Rive.File m_file;
        private Artboard m_artboard;
        private StateMachine m_stateMachine;

        private Camera m_camera;

        private void Start()
        {
            m_renderQueue = new RenderQueue(renderTexture);
            if (asset != null)
            {
                m_file = Rive.File.load(asset);
                m_artboard = m_file.artboard(0);
                m_stateMachine = m_artboard?.stateMachine();
            }

            if (m_artboard != null && renderTexture != null)
            {
                m_renderQueue.align(fit, alignment, m_artboard);
                m_renderQueue.draw(m_artboard);

                m_commandBuffer = new CommandBuffer();
                m_renderQueue.toCommandBuffer();
                // This a way to force Unity to close the current MTL command encoder
                // We need to activate a new encoder in the current command buffer for our Offscreen phase
                // if (UnityEngine.SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Metal)
                // {
                //     UnityEngine.RenderTexture surface = UnityEngine.RenderTexture.GetTemporary(1, 1);
                //     m_commandBuffer.SetRenderTarget(surface, LoadAction.DontCare, StoreAction.DontCare, LoadAction.DontCare, StoreAction.DontCare);
                //     m_commandBuffer.ClearRenderTarget(false, false, UnityEngine.Color.clear);
                //     UnityEngine.RenderTexture.ReleaseTemporary(surface);
                // }
                m_commandBuffer.SetRenderTarget(renderTexture);
                m_commandBuffer.ClearRenderTarget(true, true, UnityEngine.Color.clear, 0.0f);
                // m_commandBuffer.SetRenderTarget(new RenderTargetIdentifier[1] { renderTexture });
                // m_commandBuffer.ClearRenderTarget(true, true, UnityEngine.Color.red, 0.0f);
                m_renderQueue.addToCommandBuffer(m_commandBuffer);
                m_camera = Camera.main;
                if (m_camera != null)
                {
                    Camera.main.AddCommandBuffer(CameraEvent.AfterEverything, m_commandBuffer);
                }
                // Graphics.ExecuteCommandBuffer(m_commandBuffer);
            }
        }

        private Vector2 m_lastMousePosition;
        bool m_wasMouseDown = false;

        private void Update()
        {
            // Camera camera = gameObject.GetComponent<Camera>();
            // if (camera != null)
            // {
            //     Vector3 mousePos = camera.ScreenToViewportPoint(Input.mousePosition);
            //     Vector2 mouseRiveScreenPos = new Vector2(mousePos.x * camera.pixelWidth, (1 - mousePos.y) * camera.pixelHeight);
            //     if (m_artboard != null && m_lastMousePosition != mouseRiveScreenPos)
            //     {
            //         Vector2 local = m_artboard.localCoordinate(mouseRiveScreenPos, new Rect(0, 0, camera.pixelWidth, camera.pixelHeight), fit, alignment);
            //         m_stateMachine?.pointerMove(local);
            //         m_lastMousePosition = mouseRiveScreenPos;
            //     }
            //     if (Input.GetMouseButtonDown(0))
            //     {
            //         Vector2 local = m_artboard.localCoordinate(mouseRiveScreenPos, new Rect(0, 0, camera.pixelWidth, camera.pixelHeight), fit, alignment);
            //         m_stateMachine?.pointerDown(local);
            //         m_wasMouseDown = true;
            //     }
            //     else if (m_wasMouseDown)
            //     {
            //         m_wasMouseDown = false; Vector2 local = m_artboard.localCoordinate(mouseRiveScreenPos, new Rect(0, 0, camera.pixelWidth, camera.pixelHeight), fit, alignment);
            //         m_stateMachine?.pointerUp(local);
            //     }
            // }
            if (m_stateMachine != null)
            {
                m_stateMachine.advance(Time.deltaTime);
                // Graphics.ExecuteCommandBuffer(m_commandBuffer);
            }
        }

        private void OnDisable()
        {
            if (m_camera != null && m_commandBuffer != null)
            {
                m_camera.RemoveCommandBuffer(CameraEvent.AfterEverything, m_commandBuffer);
            }
            // Camera camera = gameObject.GetComponent<Camera>();
            // if (m_commandBuffer != null && camera != null)
            // {
            //     camera.RemoveCommandBuffer(cameraEvent, m_commandBuffer);
            // }
        }

        // private void OnRenderImage(RenderTexture src, RenderTexture dst)
        // {
        //     // Graphics.Blit(src, dst);
        //     // if (m_artboard != null)
        //     // {
        //     //     m_renderQueue.draw(m_artboard);
        //     // }
        //     // m_renderQueue.submit();
        //     // GL.InvalidateState();
        // }
    }
}
