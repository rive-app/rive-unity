#if !RIVE_USING_URP && !RIVE_USING_HDRP

using System.Collections;
using System.Collections.Generic;
using Rive.Utils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;


namespace Rive.Components.BuiltIn
{
    public class BuiltInRenderPipelineHandler : MonoBehaviour, IRenderPipelineHandler
    {

        private Dictionary<IRenderer, CommandBuffer> m_activeRenderCommandBuffers = new Dictionary<IRenderer, CommandBuffer>();


        [Tooltip("The cameras that will render the Rive content. If not provided, the main camera will be used.")]
        [SerializeField] private Camera m_renderCamera;

        private CommandBufferPool m_commandBufferPool;

        [SerializeField]
        private CameraEvent m_cameraEvent = CameraEvent.AfterEverything;

        private Coroutine m_setNewRenderCameraCoroutine;

        private List<IRenderer> m_rendererCleanupList = new List<IRenderer>();

        private bool m_isDestroyed = false;

        private Camera[] m_camerasInScene;


        /// <summary>
        /// The default capacity of the render pass pool.
        /// </summary>
        public int CommandBufferPoolDefaultCapacity { get; set; } = 5;

        /// <summary>
        /// The maximum size of the render pass pool.
        /// </summary>
        public int CommandBufferPoolMaxSize { get; set; } = 10000;


        /// <summary>
        /// The camera that will render the Rive content.
        /// </summary>
        public Camera RenderCamera
        {
            get
            {
                return m_renderCamera;
            }
            set => SetRenderCamera(value);
        }

        /// <summary>
        /// The current instance of the BuiltInRenderPipelineHandler in the scene.
        /// </summary>
        public static BuiltInRenderPipelineHandler Instance
        {
            get
            {
                return RenderPipelineHelper.CurrentHandler as BuiltInRenderPipelineHandler;
            }
        }


        protected virtual void OnEnable()
        {
            SceneManager.activeSceneChanged += ChangedActiveScene;

        }

        protected virtual void OnDisable()
        {
            SceneManager.activeSceneChanged -= ChangedActiveScene;
        }

        private void ChangedActiveScene(Scene arg0, Scene arg1)
        {
            // If the scene changes, it's likely the main camera was destroyed, we need to wait until there's one
            // This is also called on initial scene load when entering play mode
            if (m_setNewRenderCameraCoroutine == null)
            {
                m_setNewRenderCameraCoroutine = StartCoroutine(SetNewRenderCamera());
            }


        }

        private IEnumerator SetNewRenderCamera()
        {

            // Wait until the scene is loaded
            while (!SceneManager.GetActiveScene().isLoaded)
            {
                yield return null;
            }

            if (m_renderCamera == null)
            {
                // Wait until there's a main camera in the scene
                Camera camera = null;
                while (camera == null)
                {
                    camera = GetRenderCameraInScene();

                    if (camera == null)
                    {
                        yield return null;
                    }

                }

                RenderCamera = camera;
            }


            m_setNewRenderCameraCoroutine = null;

        }

        public virtual RenderTexture AllocateRenderTexture(int width, int height)
        {
            RenderTextureDescriptor descriptor = TextureHelper.Descriptor(width, height);
            RenderTexture renderTexture = new RenderTexture(descriptor);
            return renderTexture;
        }



        /// <summary>
        /// Looks for the main camera in the scene to render the Rive content.
        /// </summary>
        /// <returns></returns>
        private Camera GetRenderCameraInScene()
        {
            return CameraHelper.GetRenderCameraInScene();
        }


        private void SetRenderCamera(Camera camera)
        {
            if (ReferenceEquals(m_renderCamera, camera))
            {
                return;

            }

            // If we have a previous camera, remove the command buffers from it
            if (m_renderCamera != null)
            {

                foreach (var kvp in m_activeRenderCommandBuffers)
                {
                    SafeRemoveCommandBuffer(m_renderCamera, kvp.Value);
                }
            }

            m_renderCamera = camera;

            if (m_renderCamera != null)
            {

                // Add existing command buffers to the new camera
                foreach (var kvp in m_activeRenderCommandBuffers)
                {
                    AddCommandBufferToCamera(m_renderCamera, kvp.Value);
                }

            }

        }

        private void InitCommandBufferPoolIfNeeded()
        {
            if (m_commandBufferPool == null)
            {
                m_commandBufferPool = new CommandBufferPool(CommandBufferPoolDefaultCapacity, CommandBufferPoolMaxSize);
            }
        }

        public virtual void Register(IRenderer renderer)
        {
            if (m_isDestroyed)
            {
                return;
            }

            InitCommandBufferPoolIfNeeded();

            if (renderer == null)
            {
                DebugLogger.Instance.LogWarning("Trying to register a null renderer.");
                return;
            }

            if (m_activeRenderCommandBuffers.ContainsKey(renderer))
            {
                DebugLogger.Instance.LogWarning("Trying to register the same renderer twice.");
                return;
            }

            CommandBuffer commandBuffer = m_commandBufferPool.Get("RiveRenderPass");



            renderer.AddToCommandBuffer(commandBuffer);

            m_activeRenderCommandBuffers.Add(renderer, commandBuffer);

            Camera cameraToUse = RenderCamera;

            if (cameraToUse == null)
            {
                cameraToUse = GetRenderCameraInScene();

                if (cameraToUse != null)
                {
                    // If we found a camera in the scene, set it as the render camera
                    // We do this immediately, instead of waiting for the coroutine to finish because we want the visuals to show up on the initial frame
                    RenderCamera = cameraToUse;
                }
                else
                {
                    // Schedule the camera to be set when available in the scene
                    if (m_setNewRenderCameraCoroutine == null)
                    {
                        m_setNewRenderCameraCoroutine = StartCoroutine(SetNewRenderCamera());
                    }
                }
            }
            else
            {
                // If we already have a camera, add the new command buffer to it
                AddCommandBufferToCamera(cameraToUse, commandBuffer);
            }

        }

        public virtual void Unregister(IRenderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            if (m_activeRenderCommandBuffers.TryGetValue(renderer, out var commandBuffer))
            {


                SafeRemoveCommandBuffer(m_renderCamera, commandBuffer);

                m_activeRenderCommandBuffers.Remove(renderer);
                m_commandBufferPool.Release(commandBuffer);
            }
        }

        private void SafeRemoveCommandBuffer(Camera camera, CommandBuffer commandBuffer)
        {
            if (camera != null && commandBuffer != null)
            {
                camera.RemoveCommandBuffer(m_cameraEvent, commandBuffer);
            }
        }

        private void AddCommandBufferToCamera(Camera camera, CommandBuffer commandBuffer)
        {
            if (camera != null && commandBuffer != null)
            {
                camera.AddCommandBuffer(m_cameraEvent, commandBuffer);
            }
        }

        private void Cleanup()
        {
            m_isDestroyed = true;
            if (m_setNewRenderCameraCoroutine != null)
            {
                StopCoroutine(m_setNewRenderCameraCoroutine);
                m_setNewRenderCameraCoroutine = null;
            }

            m_rendererCleanupList.Clear();
            m_rendererCleanupList.AddRange(m_activeRenderCommandBuffers.Keys);

            for (int i = 0; i < m_rendererCleanupList.Count; i++)
            {
                Unregister(m_rendererCleanupList[i]);
            }

        }

        protected virtual void OnDestroy()
        {
            Cleanup();
        }

        public bool IsRendererRegistered(IRenderer renderer)
        {
            return m_activeRenderCommandBuffers.ContainsKey(renderer);
        }

        public void ReleaseRenderTexture(RenderTexture renderTexture)
        {
            if (renderTexture != null)
            {
                renderTexture.Release();
            }
        }

        public RenderTexture ResizeRenderTexture(RenderTexture renderTexture, int width, int height)
        {
            if (renderTexture == null)
            {
                DebugLogger.Instance.LogError("Cannot resize a null render texture.");
                return null;
            }

            renderTexture.Release();
            renderTexture.width = width;
            renderTexture.height = height;
            renderTexture.Create();


            return renderTexture;
        }
    }
}
#endif