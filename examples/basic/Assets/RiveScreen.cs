using System.Collections;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using UnityEditor;

namespace Rive
{
    internal class CameraTextureHelper
    {
        private Camera m_camera;
        private RenderTexture m_renderTexture;
        private int m_pixelWidth;
        private int m_pixelHeight;
        private RenderQueue m_renderQueue;

        public RenderTexture renderTexture
        {
            get { return m_renderTexture; }
        }

        public Camera camera
        {
            get { return m_camera; }
        }

        internal CameraTextureHelper(Camera camera, RenderQueue queue)
        {
            m_camera = camera;
            m_renderQueue = queue;
            update();
        }

        ~CameraTextureHelper()
        {
            cleanup();
        }

        void cleanup()
        {
            if (m_renderTexture != null)
            {
                m_renderTexture.Release();
            }
        }

        public bool update()
        {
            if (m_pixelWidth == m_camera.pixelWidth && m_pixelHeight == m_camera.pixelHeight)
            {
                return false;
            }
            cleanup();

            m_pixelWidth = m_camera.pixelWidth;
            m_pixelHeight = m_camera.pixelHeight;
            m_renderTexture = new RenderTexture(
                m_camera.pixelWidth,
                m_camera.pixelHeight,
                0,
                RenderTextureFormat.ARGB32
            );
            m_renderTexture.enableRandomWrite = true;
            m_renderTexture.Create();
            m_renderQueue.updateTexture(m_renderTexture);
            return true;
        }
    }

    [RequireComponent(typeof(Camera))]
    // Draw a Rive artboard to the screen. Must be bound to a camera.
    public class RiveScreen : MonoBehaviour
    {
        public Rive.Asset asset;
        public CameraEvent cameraEvent = CameraEvent.BeforeImageEffects;
        public Fit fit = Fit.contain;
        public Alignment alignment = Alignment.center;

        private RenderQueue m_renderQueue;
        private CommandBuffer m_commandBuffer;

        private Rive.File m_file;
        private Artboard m_artboard;
        private StateMachine m_stateMachine;
        private CameraTextureHelper m_helper;

        public Material material;

        private static bool flipY()
        {
            switch (UnityEngine.SystemInfo.graphicsDeviceType)
            {
                case UnityEngine.Rendering.GraphicsDeviceType.Metal:
                case UnityEngine.Rendering.GraphicsDeviceType.Direct3D11:
                    return true;
                default:
                    return false;
            }
        }

        void OnGUI()
        {
            if (m_helper != null && Event.current.type.Equals(EventType.Repaint))
            {
                var texture = m_helper.renderTexture;

                var width = m_helper.camera.scaledPixelWidth;
                var height = m_helper.camera.scaledPixelHeight;

                GUI.DrawTexture(
                    flipY() ? new Rect(0, height, width, -height) : new Rect(0, 0, width, height),
                    texture,
                    ScaleMode.StretchToFill,
                    true
                );
            }
        }

        private void Start()
        {
            if (asset != null)
            {
                m_file = Rive.File.load(asset);
                m_artboard = m_file.artboard(0);
                m_stateMachine = m_artboard?.stateMachine();
            }

            Camera camera = gameObject.GetComponent<Camera>();
            Assert.IsNotNull(camera, "TestRive must be attached to a camera.");
            m_renderQueue = new RenderQueue();
            m_commandBuffer = m_renderQueue.toCommandBuffer();
            camera.AddCommandBuffer(cameraEvent, m_commandBuffer);
            if (!RenderQueue.supportsDrawingToScreen())
            {
                m_helper = new CameraTextureHelper(camera, m_renderQueue);
            }
            drawRive(m_renderQueue);
        }

        void drawRive(RenderQueue queue)
        {
            if (m_artboard != null)
            {
                queue.align(fit, alignment, m_artboard);
                queue.draw(m_artboard);
            }
        }

        private Vector2 m_lastMousePosition;
        bool m_wasMouseDown = false;

        private void Update()
        {
            m_helper?.update();
            Camera camera = gameObject.GetComponent<Camera>();
            if (camera != null)
            {
                Vector3 mousePos = camera.ScreenToViewportPoint(Input.mousePosition);
                Vector2 mouseRiveScreenPos = new Vector2(
                    mousePos.x * camera.pixelWidth,
                    (1 - mousePos.y) * camera.pixelHeight
                );
                if (m_artboard != null && m_lastMousePosition != mouseRiveScreenPos)
                {
                    Vector2 local = m_artboard.localCoordinate(
                        mouseRiveScreenPos,
                        new Rect(0, 0, camera.pixelWidth, camera.pixelHeight),
                        fit,
                        alignment
                    );
                    m_stateMachine?.pointerMove(local);
                    m_lastMousePosition = mouseRiveScreenPos;
                }
                if (Input.GetMouseButtonDown(0))
                {
                    Vector2 local = m_artboard.localCoordinate(
                        mouseRiveScreenPos,
                        new Rect(0, 0, camera.pixelWidth, camera.pixelHeight),
                        fit,
                        alignment
                    );
                    m_stateMachine?.pointerDown(local);
                    m_wasMouseDown = true;
                }
                else if (m_wasMouseDown)
                {
                    m_wasMouseDown = false;
                    Vector2 local = m_artboard.localCoordinate(
                        mouseRiveScreenPos,
                        new Rect(0, 0, camera.pixelWidth, camera.pixelHeight),
                        fit,
                        alignment
                    );
                    m_stateMachine?.pointerUp(local);
                }
            }
            m_stateMachine?.advance(Time.deltaTime);
        }

        private void OnDisable()
        {
            Camera camera = gameObject.GetComponent<Camera>();
            if (m_commandBuffer != null && camera != null)
            {
                camera.RemoveCommandBuffer(cameraEvent, m_commandBuffer);
            }
        }
    }
}
