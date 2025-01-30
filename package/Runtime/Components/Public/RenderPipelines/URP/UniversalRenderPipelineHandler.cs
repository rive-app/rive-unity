#if RIVE_USING_URP

using System.Collections.Generic;
using Rive.Utils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Rive.Components.URP
{
    /// <summary>
    /// This class is responsible for handling the rendering of Rive objects in the Universal Render Pipeline.
    /// </summary>
    public class UniversalRenderPipelineHandler : MonoBehaviour, IRenderPipelineHandler
    {

        [SerializeField] private RenderPassEvent m_renderPassEvent = RenderPassEvent.AfterRenderingTransparents;

        [Tooltip("The camera that will render the Rive content. If not provided, the main camera will be used.")]
        [SerializeField] private Camera m_renderCamera;

        RenderPassPool m_renderPassPool;

        private Dictionary<IRenderer, RenderPass> m_activeRenderPasses = new Dictionary<IRenderer, RenderPass>();

        private ScriptableRenderer m_scriptableRenderer;

        private bool m_isDestroyed = false;

        private List<IRenderer> m_cleanupList = new List<IRenderer>();

        private Dictionary<RenderTexture, RTHandle> m_rtHandleCache = new Dictionary<RenderTexture, RTHandle>();

        private const string PROFILER_MARKER_NAME = "Rive URP Render Pass";

        private const string DEFAULT_RENDER_TEXTURE_NAME = "URP Rive Render Texture";

        /// <summary>
        /// The default capacity of the render pass pool.
        /// </summary>
        public int RenderPassPoolDefaultCapacity { get; set; } = 5;

        /// <summary>
        /// The maximum size of the render pass pool.
        /// </summary>
        public int RenderPassPoolMaxSize { get; set; } = 10000;

        public bool IsInitialized { get; private set; }

        public RenderPassEvent RenderPassEvent
        {
            get => m_renderPassEvent;
            set => m_renderPassEvent = value;
        }

        public Camera RenderCamera
        {
            get
            {

                return m_renderCamera;
            }
            set => SetRenderCamera(value);
        }

        /// <summary>
        /// The current instance of the UniversalRenderPipelineHandler in the scene.
        /// </summary>
        public static UniversalRenderPipelineHandler Instance
        {
            get
            {
                return RenderPipelineHelper.CurrentHandler as UniversalRenderPipelineHandler;
            }
        }

        private RTHandleSystem m_RTHandleSystem;

        private RTHandleSystem RTHandleSystem
        {
            get
            {
                if (m_isDestroyed)
                {
                    return null;
                }
                if (m_RTHandleSystem == null)
                {
                    m_RTHandleSystem = new RTHandleSystem();
                }

                return m_RTHandleSystem;
            }
        }


        private void InitRenderPassPoolIfNeeded()
        {
            if (m_renderPassPool == null)
            {
                m_renderPassPool = new RenderPassPool(RenderPassPoolDefaultCapacity, RenderPassPoolMaxSize);
            }
        }

        private void Awake()
        {
            RTHandleSystem.Initialize(Screen.width, Screen.height);
            IsInitialized = true;
        }


        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        }

        private void SetRenderCamera(Camera camera)
        {
            m_renderCamera = camera;
        }

        private void OnBeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (RenderCamera == null)
            {
                RenderCamera = CameraHelper.GetRenderCameraInScene();
            }

            bool isValidCamera = ReferenceEquals(camera, RenderCamera);

#if UNITY_EDITOR
            isValidCamera = isValidCamera || camera.cameraType == CameraType.SceneView;
#endif

            // We only want one camera to render the Rive graphics
            if (RenderCamera == null || !isValidCamera)
            {
                return;
            }

            foreach (var renderHandle in m_activeRenderPasses.Values)
            {
                ProcessHandle(renderHandle, camera);
            }

        }

        private void ProcessHandle(RenderPass renderPass, Camera camera)
        {
            if (renderPass == null)
            {
                return;
            }

            if (m_scriptableRenderer == null)
            {
                m_scriptableRenderer = camera.GetUniversalAdditionalCameraData().scriptableRenderer;
            }

            m_scriptableRenderer?.EnqueuePass(renderPass);

        }

        private RenderPass GetRenderHandleForRenderer(IRenderer renderer)
        {
            if (renderer == null)
            {
                return null;
            }

            if (m_activeRenderPasses.ContainsKey(renderer))
            {
                return m_activeRenderPasses[renderer];
            }

            return null;
        }

        public void Register(IRenderer renderer)
        {
            InitRenderPassPoolIfNeeded();

            if (renderer == null)
            {
                DebugLogger.Instance.LogWarning("Trying to register a null renderer.");
                return;
            }

            var existingHandle = GetRenderHandleForRenderer(renderer);

            if (existingHandle != null)
            {
                DebugLogger.Instance.LogWarning("Trying to register the same renderer twice.");
                return;
            }

            RenderPass riveRenderHandlePass = m_renderPassPool.Get(renderer, m_renderPassEvent, PROFILER_MARKER_NAME);
            m_activeRenderPasses.Add(renderer, riveRenderHandlePass);

        }

        public void Unregister(IRenderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            if (m_activeRenderPasses.TryGetValue(renderer, out var renderPass))
            {
                m_activeRenderPasses.Remove(renderer);
                m_renderPassPool.Release(renderPass);
            }
        }

        public RenderTexture AllocateRenderTexture(int width, int height)
        {
            if (RTHandleSystem == null)
            {
                return null;
            }
            var descriptor = TextureHelper.Descriptor(width, height);
            RTHandle handle = RTHandleSystem.Alloc(width, height, colorFormat: descriptor.graphicsFormat, enableRandomWrite: descriptor.enableRandomWrite, name: DEFAULT_RENDER_TEXTURE_NAME);

            return handle;
        }

        public void ReleaseRenderTexture(RenderTexture renderTexture)
        {
            if (RTHandleSystem == null)
            {
                return;
            }

            if (renderTexture == null)
            {
                return;
            }

            if (m_rtHandleCache.Remove(renderTexture, out var rtHandle))
            {
                rtHandle.Release();
            }

        }

        public RenderTexture ResizeRenderTexture(RenderTexture renderTexture, int width, int height)
        {
            if (RTHandleSystem == null)
            {
                DebugLogger.Instance.LogWarning("Cannot resize render texture. RTHandleSystem is null.");

                return null;
            }

            if (renderTexture == null)
            {
                DebugLogger.Instance.LogWarning("Cannot resize a null render texture.");
                return null;
            }

            if (m_rtHandleCache.TryGetValue(renderTexture, out var rtHandle))
            {
                rtHandle.Release();
                m_rtHandleCache.Remove(renderTexture);
            }

            return AllocateRenderTexture(width, height);
        }



        public bool IsRendererRegistered(IRenderer renderer)
        {
            return m_activeRenderPasses.ContainsKey(renderer);
        }


        private void Cleanup()
        {

            if (RTHandleSystem != null)
            {
                RTHandleSystem.Dispose();
                m_RTHandleSystem = null;
            }

            // Unregister all renderers
            m_cleanupList.Clear();
            m_cleanupList.AddRange(m_activeRenderPasses.Keys);

            for (int i = 0; i < m_cleanupList.Count; i++)
            {
                Unregister(m_cleanupList[i]);
            }

            m_scriptableRenderer = null;
            m_renderCamera = null;
        }

        private void OnDestroy()
        {
            Cleanup();
            m_isDestroyed = true;
        }


    }
}
#endif