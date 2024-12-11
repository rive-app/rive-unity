#if !RIVE_USING_URP && !RIVE_USING_HDRP

using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Rive.Components;
using Rive.Components.BuiltIn;
using Rive.Tests.Utils;
using UnityEngine.Rendering;
using Rive.Utils;
using UnityEngine.SceneManagement;

namespace Rive.Tests
{
    public class BuiltInRenderPipelineHandlerTests
    {
        private GameObject m_handlerObject;
        private BuiltInRenderPipelineHandler m_handler;
        private Camera m_camera;
        private MockRenderer m_renderer;
        private MockLogger m_mockLogger;
        private List<CommandBuffer> m_recordedCommandBuffers;

        [SetUp]
        public void Setup()
        {
            m_mockLogger = new MockLogger();
            DebugLogger.Instance = m_mockLogger;

            // Create camera
            var cameraObject = new GameObject("TestCamera");
            m_camera = cameraObject.AddComponent<Camera>();
            m_camera.tag = "MainCamera";

            // Create handler
            m_handlerObject = new GameObject("RenderPipelineHandler");
            m_handler = m_handlerObject.AddComponent<BuiltInRenderPipelineHandler>();

            // Create mock renderer
            m_renderer = new MockRenderer();
            m_recordedCommandBuffers = new List<CommandBuffer>();

            // Subscribe to events so we can track command buffers
            m_renderer.OnAddToCommandBuffer += OnAddToCommandBuffer;
        }

        private void OnAddToCommandBuffer(CommandBuffer buffer, bool release)
        {
            m_recordedCommandBuffers.Add(buffer);
        }

        [TearDown]
        public void TearDown()
        {
            m_renderer.OnAddToCommandBuffer -= OnAddToCommandBuffer;

            m_recordedCommandBuffers.Clear();

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
        public void Register_ValidRenderer_AddsToCommandBuffer()
        {

            m_handler.Register(m_renderer);

            Assert.IsTrue(m_handler.IsRendererRegistered(m_renderer));
            Assert.AreEqual(1, m_recordedCommandBuffers.Count, "AddToCommandBuffer should be called exactly once");
            Assert.AreEqual(0, m_mockLogger.LoggedWarnings.Count);
        }

        [Test]
        public void Register_NullRenderer_LogsWarning()
        {
            m_handler.Register(null);

            Assert.AreEqual(0, m_recordedCommandBuffers.Count, "AddToCommandBuffer should not be called");
            Assert.IsTrue(m_mockLogger.LoggedWarnings.Count > 0);
        }

        [Test]
        public void Register_DuplicateRenderer_LogsWarning()
        {
            m_handler.Register(m_renderer);
            m_handler.Register(m_renderer);

            Assert.AreEqual(1, m_recordedCommandBuffers.Count, "AddToCommandBuffer should be called exactly once");
            Assert.IsTrue(m_mockLogger.LoggedWarnings.Count > 0);
        }

        [UnityTest]
        public IEnumerator RenderCamera_ChangingCamera_MovesCommandBuffers()
        {

            // Create new camera
            var newCameraObject = new GameObject("NewCamera");
            var newCamera = newCameraObject.AddComponent<Camera>();

            m_handler.Register(m_renderer);
            yield return null;

            int originalCameraBufferCount = m_camera.commandBufferCount;
            m_handler.RenderCamera = newCamera;
            yield return null;

            Assert.Greater(originalCameraBufferCount, 0, "Should have added command buffers initially");
            Assert.AreEqual(originalCameraBufferCount, newCamera.commandBufferCount,
                "Should maintain same number of command buffers after camera change");

            DestroyObj(newCameraObject);
        }

        [UnityTest]
        public IEnumerator SceneChange_WaitsForNewMainCamera()
        {
            // Make handler persist across scene loads
            UnityEngine.Object.DontDestroyOnLoad(m_handlerObject);

            // Setup test scene tracking
            Scene originalScene = SceneManager.GetActiveScene();
            Scene newScene = default;

            // Create a new scene
            newScene = SceneManager.CreateScene("TestScene");

            // Register renderer in original scene
            m_handler.Register(m_renderer);

            int commandBufferCount = 0;
            m_renderer.OnAddToCommandBuffer += (buffer, release) => commandBufferCount++;

            UnityEngine.Object.DestroyImmediate(m_camera.gameObject);
            yield return null;

            // Switch to new scene
            yield return SceneManager.SetActiveScene(newScene);

            yield return null;


            Camera camera = UnityEngine.Object.FindObjectOfType<Camera>();

            // Verify no camera yet
            Assert.IsNull(camera);
            Assert.IsNull(Camera.main);

            // Create new camera in new scene
            var newCameraObject = new GameObject("NewCamera");
            var newCamera = newCameraObject.AddComponent<Camera>();

            Assert.AreEqual(newCamera.commandBufferCount, 0);

            newCamera.tag = "MainCamera";
            SceneManager.MoveGameObjectToScene(newCameraObject, newScene);

            // Wait for camera detection
            yield return null;

            // Verify new camera was found and command buffers were added to it
            Assert.AreEqual(newCamera, m_handler.RenderCamera);
            Assert.AreEqual(newCamera.commandBufferCount, 1);

            // Cleanup
            yield return SceneManager.SetActiveScene(originalScene);
            if (newScene.IsValid())
            {
                yield return SceneManager.UnloadSceneAsync(newScene);
            }
        }

        [Test]
        public void CommandBufferPool_ReusesBuffers()
        {
            // Register and unregister multiple times
            for (int i = 0; i < 3; i++)
            {
                m_handler.Register(m_renderer);
                m_handler.Unregister(m_renderer);
            }
            // Filter m_recordedCommandBuffers by unique buffers
            HashSet<CommandBuffer> usedBuffers = new HashSet<CommandBuffer>(m_recordedCommandBuffers);

            // Verify that only one buffer was used since we unregistered and registered the renderer multiple times
            Assert.AreEqual(usedBuffers.Count, 1, "Command buffers should be reused from pool");
        }


        [UnityTest]
        public IEnumerator Cleanup_ClearsAllCommandBuffers()
        {
            int initialCameraBufferCount = m_camera.commandBufferCount;

            List<CommandBuffer> destroyedBuffers = new List<CommandBuffer>();
            m_renderer.OnAddToCommandBuffer += (buffer, release) =>
            {
                if (release) destroyedBuffers.Add(buffer);
            };

            m_handler.Register(m_renderer);

            Assert.Greater(m_camera.commandBufferCount, initialCameraBufferCount,
                "Command buffers should be added to camera");

            DestroyObj(m_handler);

            yield return null;


            Assert.AreEqual(initialCameraBufferCount, m_camera.commandBufferCount, "Command buffers should be removed from camera");
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
    }


}
#endif