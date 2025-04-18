#if RIVE_USING_URP
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Rive.Components.URP;
using Rive.Tests.Utils;
using UnityEngine.Rendering.Universal;
using Rive.Utils;
using UnityEngine.SceneManagement;

namespace Rive.Tests
{
    public class UniversalRenderPipelineHandlerTests
    {
        private GameObject m_handlerObject;
        private UniversalRenderPipelineHandler m_handler;
        private Camera m_camera;
        private MockRenderer m_renderer;
        private MockLogger m_mockLogger;
        private UniversalAdditionalCameraData m_cameraData;

        [SetUp]
        public void Setup()
        {
            m_mockLogger = new MockLogger();
            DebugLogger.Instance = m_mockLogger;

            // Create camera with URP components
            var cameraObject = new GameObject("TestCamera");
            m_camera = cameraObject.AddComponent<Camera>();
            m_camera.tag = "MainCamera";
            m_cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();

            // Create handler
            m_handlerObject = new GameObject("URPHandler");
            m_handler = m_handlerObject.AddComponent<UniversalRenderPipelineHandler>();

            m_renderer = new MockRenderer();

        }

        [TearDown]
        public void TearDown()
        {
            if (m_camera != null)
            {
                DestroyObj(m_camera.gameObject);
            }

            if (m_handlerObject != null)
            {
                DestroyObj(m_handlerObject);
            }
        }

        private void DestroyObj(UnityEngine.Object obj)
        {
            if (obj != null)
            {
                UnityEngine.Object.Destroy(obj);
            }
        }

        [Test]
        public void Register_ValidRenderer_Succeeds()
        {
            m_handler.Register(m_renderer);
            Assert.IsTrue(m_handler.IsRendererRegistered(m_renderer));
            Assert.AreEqual(0, m_mockLogger.LoggedWarnings.Count);
        }

        [Test]
        public void Register_NullRenderer_LogsWarning()
        {
            m_handler.Register(null);
            Assert.IsTrue(m_mockLogger.LoggedWarnings.Count > 0);
        }

        [Test]
        public void Register_DuplicateRenderer_LogsWarning()
        {
            m_handler.Register(m_renderer);
            m_handler.Register(m_renderer);
            Assert.IsTrue(m_mockLogger.LoggedWarnings.Count > 0);
        }

        [Test]
        public void Unregister_RegisteredRenderer_Succeeds()
        {
            m_handler.Register(m_renderer);
            Assert.IsTrue(m_handler.IsRendererRegistered(m_renderer));

            m_handler.Unregister(m_renderer);
            Assert.IsFalse(m_handler.IsRendererRegistered(m_renderer));
        }

        [Test]
        public void Unregister_UnregisteredRenderer_DoesNothing()
        {
            m_handler.Unregister(m_renderer);
            Assert.IsFalse(m_handler.IsRendererRegistered(m_renderer));
        }

        [UnityTest]
        public IEnumerator RenderCamera_ChangingCamera_UpdatesRenderer()
        {
            // Create new camera
            var newCameraObject = new GameObject("NewCamera");
            var newCamera = newCameraObject.AddComponent<Camera>();
            var newCameraData = newCameraObject.AddComponent<UniversalAdditionalCameraData>();

            m_handler.Register(m_renderer);
            yield return null;

            Camera originalCamera = m_handler.RenderCamera;
            m_handler.RenderCamera = newCamera;
            yield return null;

            Assert.AreEqual(newCamera, m_handler.RenderCamera);
            Assert.AreNotEqual(originalCamera, m_handler.RenderCamera);

            DestroyObj(newCameraObject);
        }

        [Test]
        public void RenderPassEvent_CanBeModified()
        {
            RenderPassEvent newEvent = RenderPassEvent.BeforeRenderingTransparents;
            m_handler.RenderPassEvent = newEvent;
            Assert.AreEqual(newEvent, m_handler.RenderPassEvent);
        }

        [Test]
        public void AllocateRenderTexture_CreatesCorrectFormat()
        {
            int width = 256, height = 128;
            var texture = m_handler.AllocateRenderTexture(width, height);

            Assert.IsNotNull(texture);
            Assert.AreEqual(width, texture.width);
            Assert.AreEqual(height, texture.height);
            Assert.AreEqual(TextureHelper.Format, texture.graphicsFormat);

            DestroyObj(texture);
        }

        /// <summary>
        /// Tests that the handler uses any camera in the new scene if no camera is found, NOT just the main camera.
        /// </summary>
        /// <returns></returns>
        [UnityTest]
        public IEnumerator SceneChange_WaitsForAnyCamera_InNewScene()
        {
            yield return null;
            // Make handler persist across scene loads
            UnityEngine.Object.DontDestroyOnLoad(m_handlerObject);

            // Setup test scene tracking
            Scene originalScene = SceneManager.GetActiveScene();
            Scene newScene = default;

            newScene = SceneManager.CreateScene("TestScene_AnyCamera_URP");

            // Register renderer in original scene
            m_handler.Register(m_renderer);

            int commandBufferCount = 0;
            m_renderer.OnAddToCommandBuffer += (buffer, release) => commandBufferCount++;

            UnityEngine.Object.Destroy(m_camera.gameObject);
            yield return null;

            // Switch to new scene
            yield return SceneManager.SetActiveScene(newScene);

            yield return null;

            Assert.IsTrue(m_camera == null);

            // Create new camera in new scene
            var newCameraObject = new GameObject("NewCamera");
            var newCamera = newCameraObject.AddComponent<Camera>();

            Assert.AreNotEqual(newCamera, Camera.main);

            Assert.AreEqual(newCamera.commandBufferCount, 0);

            SceneManager.MoveGameObjectToScene(newCameraObject, newScene);

            // Wait for up to 0.3 seconds for camera detection
            float timeoutDuration = 0.3f;
            float elapsedTime = 0f;
            while (elapsedTime < timeoutDuration)
            {
                if (m_handler.RenderCamera != null)
                {
                    break;
                }
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            // Verify new camera was found
            Assert.AreEqual(newCamera, m_handler.RenderCamera);

            // Cleanup
            DestroyObj(newCameraObject);
            yield return SceneManager.SetActiveScene(originalScene);
            if (newScene.IsValid())
            {
                yield return SceneManager.UnloadSceneAsync(newScene);
            }
        }


        [Test]
        public void RenderPassPool_Configuration()
        {
            int defaultCapacity = 10;
            int maxSize = 20;

            m_handler.RenderPassPoolDefaultCapacity = defaultCapacity;
            m_handler.RenderPassPoolMaxSize = maxSize;

            Assert.AreEqual(defaultCapacity, m_handler.RenderPassPoolDefaultCapacity);
            Assert.AreEqual(maxSize, m_handler.RenderPassPoolMaxSize);

            // Register multiple renderers to test pool
            for (int i = 0; i < defaultCapacity + 1; i++)
            {
                var renderer = new MockRenderer();
                m_handler.Register(renderer);
            }

            // Pool should handle overflow gracefully
            Assert.AreEqual(0, m_mockLogger.LoggedErrors.Count);
        }
    }


}
#endif