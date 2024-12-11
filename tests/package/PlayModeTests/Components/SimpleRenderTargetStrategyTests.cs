using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Rive.Components;
using Rive.Tests.Utils;
using Rive.Utils;

namespace Rive.Tests
{
    public class SimpleRenderTargetStrategyTests
    {
        private SimpleRenderTargetStrategy m_strategy;
        private RivePanel m_panel;
        private MockLogger m_mockLogger;

        [SetUp]
        public void Setup()
        {
            m_mockLogger = new MockLogger();
            DebugLogger.Instance = m_mockLogger;

            m_panel = RivePanelTestUtils.CreatePanel();


            // Try getting the SimpleRenderTargetStrategy from the panel
            m_strategy = m_panel.gameObject.GetComponent<SimpleRenderTargetStrategy>();

            if (m_strategy == null)
            {
                m_strategy = m_panel.gameObject.AddComponent<SimpleRenderTargetStrategy>();
            }

            m_panel.RenderTargetStrategy = m_strategy;
        }

        [TearDown]
        public void TearDown()
        {
            DestroyObject(m_panel.gameObject);
        }

        private void DestroyObject(Object obj)
        {
            if (obj != null)
            {
                Object.Destroy(obj);
            }
        }

        [Test]
        public void RegisterPanel_WithValidPanel_Succeeds()
        {
            m_strategy.UnregisterPanel(m_panel);
            Assert.IsFalse(m_strategy.IsPanelRegistered(m_panel), "Panel should not be registered initially");


            Assert.IsTrue(m_strategy.RegisterPanel(m_panel), "Registering panel should succeed");
            Assert.IsTrue(m_strategy.IsPanelRegistered(m_panel), "Panel should be registered after registration");
            Assert.AreEqual(0, m_mockLogger.LoggedWarnings.Count);
        }

        [Test]
        public void RegisterPanel_WithNullPanel_Fails()
        {
            Assert.IsFalse(m_strategy.RegisterPanel(null));
            Assert.IsTrue(m_mockLogger.LoggedWarnings.Count > 0);
        }


        [Test]
        public void RegisterPanel_AlreadyRegistered_Fails()
        {
            m_strategy.UnregisterPanel(m_panel);

            Assert.IsTrue(m_strategy.RegisterPanel(m_panel));
            Assert.IsFalse(m_strategy.RegisterPanel(m_panel));
            Assert.IsTrue(m_mockLogger.LoggedWarnings.Count > 0);
        }

        [Test]
        public void UnregisterPanel_RegisteredPanel_Succeeds()
        {
            m_strategy.RegisterPanel(m_panel);
            Assert.IsTrue(m_strategy.UnregisterPanel(m_panel));
            Assert.IsFalse(m_strategy.IsPanelRegistered(m_panel));
        }

        [Test]
        public void UnregisterPanel_NotRegisteredPanel_Fails()
        {
            m_strategy.UnregisterPanel(m_panel);

            Assert.IsFalse(m_strategy.UnregisterPanel(m_panel));
        }

        [Test]
        public void GetRenderTexture_UnregisteredPanel_ReturnsNull()
        {
            m_strategy.UnregisterPanel(m_panel);
            Assert.IsFalse(m_strategy.IsPanelRegistered(m_panel), "Panel should not be registered");

            var texture = m_strategy.GetRenderTexture(m_panel);
            Assert.IsNull(texture, "GetRenderTexture should return null for unregistered panel");
        }

        [UnityTest]
        public IEnumerator DrawTiming_BatchedMode_DrawsOncePerFrame()
        {
            m_strategy.DrawTiming = DrawTimingOption.DrawBatched;
            m_strategy.RegisterPanel(m_panel);

            yield return null;

            var initialTexture = m_strategy.GetRenderTexture(m_panel);
            Assert.IsNotNull(initialTexture);

            // Request a draw multiple times in same frame
            m_strategy.DrawPanel(m_panel);

            m_strategy.DrawPanel(m_panel);


            // Should not draw immediately in batched mode
            Assert.AreEqual(initialTexture, m_strategy.GetRenderTexture(m_panel));

            // Wait for LateUpdate
            yield return new WaitForEndOfFrame();

            // Now it should have drawn
            Assert.IsNotNull(m_strategy.GetRenderTexture(m_panel));
        }

        [Test]
        public void DrawTiming_ImmediateMode_DrawsInstantly()
        {
            m_strategy.UnregisterPanel(m_panel);

            m_strategy.DrawTiming = DrawTimingOption.DrawImmediate;
            m_strategy.RegisterPanel(m_panel);

            // Get initial texture
            var initialTexture = m_strategy.GetRenderTexture(m_panel);
            Assert.IsNotNull(initialTexture);

            // Request a draw
            m_strategy.DrawPanel(m_panel);

            // Should draw immediately in immediate mode
            Assert.IsNotNull(m_strategy.GetRenderTexture(m_panel));
        }

        [UnityTest]
        public IEnumerator DrawTiming_BatchedMode_HandlesMultipleSizeChanges_DrawsOnlyOncePerFrame()
        {
            m_strategy.DrawTiming = DrawTimingOption.DrawBatched;
            m_strategy.RegisterPanel(m_panel);

            // Initial setup
            yield return null;

            // Get initial texture at 100x100
            m_panel.SetDimensions(new Vector2(100, 100));
            var initialTexture = m_strategy.GetRenderTexture(m_panel);
            Assert.IsNotNull(initialTexture);
            Assert.AreEqual(100, initialTexture.width);
            Assert.AreEqual(100, initialTexture.height);

            // Change size multiple times in same frame
            // The texture should not update immediately
            m_panel.SetDimensions(new Vector2(200, 200));
            m_strategy.DrawPanel(m_panel);
            Assert.AreEqual(100, m_strategy.GetRenderTexture(m_panel).width);
            Assert.AreEqual(100, m_strategy.GetRenderTexture(m_panel).height);

            m_panel.SetDimensions(new Vector2(300, 300));
            m_strategy.DrawPanel(m_panel);
            Assert.AreEqual(100, m_strategy.GetRenderTexture(m_panel).width);
            Assert.AreEqual(100, m_strategy.GetRenderTexture(m_panel).height);

            m_panel.SetDimensions(new Vector2(400, 400));
            m_strategy.DrawPanel(m_panel);
            Assert.AreEqual(100, m_strategy.GetRenderTexture(m_panel).width);
            Assert.AreEqual(100, m_strategy.GetRenderTexture(m_panel).height);


            yield return new WaitForEndOfFrame();

            // Now it should have drawn with final dimensions
            var finalTexture = m_strategy.GetRenderTexture(m_panel);
            Assert.IsNotNull(finalTexture);
            Assert.AreEqual(400, finalTexture.width);
            Assert.AreEqual(400, finalTexture.height);
        }

        [UnityTest]
        public IEnumerator DrawTiming_ImmediateMode_HandlesMultipleSizeChanges_RedrawsEachTime()
        {
            m_strategy.DrawTiming = DrawTimingOption.DrawImmediate;
            m_strategy.RegisterPanel(m_panel);

            yield return null;

            // Initial size
            m_panel.SetDimensions(new Vector2(100, 100));
            var initialTexture = m_strategy.GetRenderTexture(m_panel);
            Assert.IsNotNull(initialTexture);
            Assert.AreEqual(100, initialTexture.width);

            // Change size and draw multiple times in same frame
            // The texture should update immediately each time
            m_panel.SetDimensions(new Vector2(200, 200));
            m_strategy.DrawPanel(m_panel);
            Assert.AreEqual(200, m_strategy.GetRenderTexture(m_panel).width);

            m_panel.SetDimensions(new Vector2(300, 300));
            m_strategy.DrawPanel(m_panel);
            Assert.AreEqual(300, m_strategy.GetRenderTexture(m_panel).width);

            m_panel.SetDimensions(new Vector2(400, 400));
            m_strategy.DrawPanel(m_panel);
            Assert.AreEqual(400, m_strategy.GetRenderTexture(m_panel).width);
        }

        [Test]
        public void GetPanelTransform_ReturnsExpectedValues()
        {
            m_strategy.RegisterPanel(m_panel);

            Vector2 offset = m_strategy.GetPanelOffset(m_panel);
            Vector2 scale = m_strategy.GetPanelScale(m_panel);

            // SimpleRenderTargetStrategy should always return 0 offset and 1 scale
            Assert.AreEqual(Vector2.zero, offset);
            Assert.AreEqual(Vector2.one, scale);
        }

        [UnityTest]
        public IEnumerator RenderTexture_UpdatesWithPanelSize()
        {
            yield return null;
            Assert.IsTrue(ReferenceEquals(m_strategy, m_panel.RenderTargetStrategy));
            // Set initial size
            m_panel.SetDimensions(new Vector2(100, 100));


            m_strategy.RegisterPanel(m_panel);

            yield return null;

            var initialTexture = m_strategy.GetRenderTexture(m_panel);

            Assert.IsNotNull(initialTexture, "Initial texture should not be null");
            Assert.AreEqual(100, initialTexture.width, "Initial texture width should match panel");
            Assert.AreEqual(100, initialTexture.height, "Initial texture height should match panel");

            // Change size
            m_panel.SetDimensions(new Vector2(200, 150));
            m_strategy.DrawPanel(m_panel);
            yield return new WaitForEndOfFrame();

            var newTexture = m_strategy.GetRenderTexture(m_panel);
            Assert.IsNotNull(newTexture, "New texture should not be null");
            Assert.AreEqual(200, newTexture.width);
            Assert.AreEqual(150, newTexture.height);
        }

        [UnityTest]
        public IEnumerator Cleanup_DisposesResources()
        {
            m_strategy.RegisterPanel(m_panel);
            var texture = m_strategy.GetRenderTexture(m_panel);
            Assert.IsNotNull(texture);

            DestroyObject(m_strategy);
            yield return null;

            // Strategy should be unregistered and texture destroyed
            Assert.IsTrue(texture == null || !texture.IsCreated());
        }
    }
}