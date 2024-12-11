#if RIVE_USING_HDRP
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using Rive.Utils;

namespace Rive.Components.HDRP
{
    /// <summary>
    /// This class is responsible for handling the rendering of Rive objects in the High Definition Render Pipeline.
    /// </summary>
    public class HighDefinitionRenderPipelineHandler : MonoBehaviour, IRenderPipelineHandler
    {
        [SerializeField] private CustomPassInjectionPoint m_injectionPoint = CustomPassInjectionPoint.AfterOpaqueDepthAndNormal;

        [Tooltip("The camera that will render the Rive content. If not provided, the main camera will be used.")]
        [SerializeField] private Camera m_renderCamera;

        private Dictionary<RenderTexture, RTHandle> m_rtHandleCache = new Dictionary<RenderTexture, RTHandle>();


        /// <summary>
        /// The camera that will render the Rive content.
        /// </summary>
        public Camera RenderCamera
        {
            get => m_renderCamera;
            set
            {
                m_renderCamera = value;
                if (m_customPassVolume != null)
                {
                    m_customPassVolume.targetCamera = m_renderCamera;
                }
            }
        }


        private List<IRenderer> m_activeRenderPasses = new List<IRenderer>();

        private RiveCustomPass m_riveCustomPass;

        private CustomPassVolume m_customPassVolume;

        private const string DEFAULT_RENDER_TEXTURE_NAME = "URP Rive Render Texture";

        private RTHandleSystem m_RTHandleSystem = new RTHandleSystem();

        private Coroutine m_setNewRenderCameraCoroutine;

        /// <summary>
        /// The rive renderers that are currently being rendered by the custom pass.
        /// </summary>
        public IEnumerable<IRenderer> ActiveRenderers => m_activeRenderPasses;




        protected virtual void Awake()
        {
            m_RTHandleSystem.Initialize(Screen.width, Screen.height);
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
            // If the scene changes and there's no main camera, we need to wait until there's one
            if (m_renderCamera == null && m_setNewRenderCameraCoroutine == null)
            {
                m_setNewRenderCameraCoroutine = StartCoroutine(SetNewRenderCamera());
            }
        }

        private IEnumerator SetNewRenderCamera()
        {
            // Wait until there's a main camera
            Camera camera = null;
            while (camera == null)
            {
                camera = Camera.main;

                if (camera == null)
                {
                    yield return null;
                }

            }

            RenderCamera = camera;

            m_setNewRenderCameraCoroutine = null;


        }


        protected virtual void Start()
        {
            if (m_renderCamera == null)
            {
                RenderCamera = Camera.main;
            }

            m_customPassVolume = gameObject.AddComponent<CustomPassVolume>();

            m_customPassVolume.injectionPoint = m_injectionPoint;

            // We only want one camera to render the Rive content.
            m_customPassVolume.targetCamera = RenderCamera;


            m_riveCustomPass = new RiveCustomPass(this);
            m_customPassVolume.customPasses.Add(m_riveCustomPass);
        }


        public void Register(IRenderer renderer)
        {
            if (renderer == null)
            {
                DebugLogger.Instance.LogWarning("Cannot register a null renderer.");
                return;
            }

            if (m_activeRenderPasses.Contains(renderer))
            {
                DebugLogger.Instance.LogWarning("Renderer is already registered.");
                return;
            }

            m_activeRenderPasses.Add(renderer);
        }

        public void Unregister(IRenderer renderer)
        {
            if (renderer == null)
            {
                DebugLogger.Instance.LogWarning("Cannot unregister a null renderer.");
                return;
            }

            if (!m_activeRenderPasses.Contains(renderer))
            {
                DebugLogger.Instance.LogWarning("Renderer is not registered.");
                return;
            }

            m_activeRenderPasses.Remove(renderer);
        }

        public UnityEngine.RenderTexture AllocateRenderTexture(int width, int height)
        {
            var descriptor = TextureHelper.Descriptor(width, height);
            return m_RTHandleSystem.Alloc(width, height, colorFormat: descriptor.graphicsFormat, enableRandomWrite: descriptor.enableRandomWrite, name: DEFAULT_RENDER_TEXTURE_NAME);
        }

        public void ReleaseRenderTexture(RenderTexture renderTexture)
        {
            if (m_RTHandleSystem == null)
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
            if (m_RTHandleSystem == null)
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
            return m_activeRenderPasses.Contains(renderer);
        }

        private void Cleanup()
        {

            // Remove the custom pass from the volume and destroy the volume
            if (m_customPassVolume != null)
            {
                if (m_riveCustomPass != null)
                {
                    m_customPassVolume.customPasses.Remove(m_riveCustomPass);
                    m_riveCustomPass = null;
                }
                Destroy(m_customPassVolume);
                m_customPassVolume = null;
            }
            m_RTHandleSystem?.Dispose();

            // Stop any running coroutines
            if (m_setNewRenderCameraCoroutine != null)
            {
                StopCoroutine(m_setNewRenderCameraCoroutine);
                m_setNewRenderCameraCoroutine = null;
            }
        }

        private void OnDestroy()
        {
            Cleanup();
        }


    }
}
#endif