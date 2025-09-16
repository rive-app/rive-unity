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
        public IEnumerator Strategy_SupportsMultiplePanels_InScene()
        {

            var panel2 = RivePanelTestUtils.CreatePanel();
            SimpleRenderTargetStrategy strategy2 = panel2.RenderTargetStrategy as SimpleRenderTargetStrategy;

            Assert.IsNotNull(strategy2, "Panel 2 should have a SimpleRenderTargetStrategy component");

            // Check that the render target strategies are different
            Assert.IsTrue(!ReferenceEquals(panel2.RenderTargetStrategy, m_strategy), "Panel 2 should not have the same strategy as Panel 1");
            yield return null;



            Assert.IsFalse(ReferenceEquals(m_panel.RenderTexture, panel2.RenderTexture), "Panel 1 should have a different renderer and texture than Panel 2");

            // We deactive the panels to force them to unregister. If everything is working correctly, the renderers should still be using the correct textures when reactivated


            //Deactivate panel 2
            panel2.StopRendering();

            // Deactivate panel 1
            m_panel.StopRendering();

            yield return null;

            // Reactivate panel 1
            m_panel.StartRendering();

            yield return null;

            panel2.StartRendering();

            // Now the renderers should still be using the correct textures

            Assert.IsFalse(ReferenceEquals(m_panel.RenderTexture, panel2.RenderTexture), "Panel 1 should have a different texture than Panel 2");

            // Reactivate panel 2

            panel2.gameObject.SetActive(false);
            yield return null;
            m_panel.gameObject.SetActive(false);

            yield return null;

            panel2.gameObject.SetActive(true);
            yield return null;
            m_panel.gameObject.SetActive(true);

            Assert.IsFalse(ReferenceEquals(m_panel.RenderTexture, panel2.RenderTexture), "Panel 1 should have a different texture than Panel 2");

            DestroyObject(panel2.gameObject);



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


        [Test]
        public void UsesExternalPixelSizeProvider_ForRenderTextureSize()
        {
            m_strategy.UnregisterPanel(m_panel);
            m_strategy.DrawTiming = DrawTimingOption.DrawImmediate;

            // Provider says 1170x2532
            m_strategy.ExternalPixelSizeProvider = (p) => new Vector2Int(1170, 2532);
            m_strategy.ExternalDrawScaleProvider = null;

            Assert.IsTrue(m_strategy.RegisterPanel(m_panel));
            m_strategy.DrawPanel(m_panel);

            var rt = m_strategy.GetRenderTexture(m_panel);
            Assert.IsNotNull(rt);
            Assert.AreEqual(1170, rt.width);
            Assert.AreEqual(2532, rt.height);
        }

        [UnityTest]
        public IEnumerator CanvasScaler_AttachProviders_SetsExpectedRTSize()
        {
            // Arrange Canvas with constant scaleFactor to avoid dependency on screen resolution
            var canvasGO = new GameObject("Canvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 3.25f;

            // Put panel under Canvas and add Canvas renderer (which attaches providers)
            m_panel.transform.SetParent(canvasGO.transform, false);
            var canvasRenderer = m_panel.gameObject.AddComponent<RiveCanvasRenderer>();
            canvasRenderer.MatchCanvasResolution = true;

            // Ensure Simple strategy is active and draws immediately
            m_strategy.UnregisterPanel(m_panel);
            m_strategy.DrawTiming = DrawTimingOption.DrawImmediate;
            m_panel.RenderTargetStrategy = m_strategy;

            // Set panel logical size (UI units)
            m_panel.SetDimensions(new Vector2(360, 780));

            Assert.IsTrue(m_strategy.RegisterPanel(m_panel));
            m_strategy.DrawPanel(m_panel);
            yield return null; // allow one frame for changes to propagate

            // Assert RT uses UI size × canvas scale (≈ 1170×2535)
            var rt = m_strategy.GetRenderTexture(m_panel);
            Assert.IsNotNull(rt);
            Assert.AreEqual(Mathf.CeilToInt(360 * 3.25f), rt.width);
            Assert.AreEqual(Mathf.CeilToInt(780 * 3.25f), rt.height);

            Object.Destroy(canvasGO);
        }

        [UnityTest]
        public IEnumerator MatchCanvasResolution_Toggle_Causes_Redraw_Via_SetDirty()
        {
            var canvasGO = new GameObject("Canvas_ToggleTest");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 3.25f;

            // Put panel under Canvas; attach canvas renderer so providers are attached
            m_panel.transform.SetParent(canvasGO.transform, false);
            var canvasRenderer = m_panel.gameObject.AddComponent<RiveCanvasRenderer>();
            Assert.IsFalse(canvasRenderer.MatchCanvasResolution);
            canvasRenderer.MatchCanvasResolution = true; // start using canvas pixels

            // Ensure the panel uses Simple strategy (already set in Setup) and is rendering
            m_panel.SetDimensions(new Vector2(360, 780));
            m_panel.StartRendering();

            // Wait a frame so the initial RT is created
            yield return null;
            yield return new WaitForEndOfFrame();

            // Baseline: RT is scaled by canvas scale (≈ 1170×2535)
            var rt = m_strategy.GetRenderTexture(m_panel);
            Assert.IsNotNull(rt);
            Assert.AreEqual(Mathf.CeilToInt(360 * 3.25f), rt.width);
            Assert.AreEqual(Mathf.CeilToInt(780 * 3.25f), rt.height);

            // Toggle OFF (RiveCanvasRenderer stops providing canvas pixels)
            canvasRenderer.MatchCanvasResolution = false;

            // Same frame: RT size has not changed yet (redraw happens next Update/Tick)
            var sameFrameRT = m_strategy.GetRenderTexture(m_panel);
            Assert.AreEqual(rt.width, sameFrameRT.width);
            Assert.AreEqual(rt.height, sameFrameRT.height);

            // Next frame: SetDirty flag causes RedrawIfNeeded() in Tick() and RT reverts to logical rect
            yield return null;
            yield return new WaitForEndOfFrame();

            var revertedRT = m_strategy.GetRenderTexture(m_panel);
            Assert.IsNotNull(revertedRT);
            Assert.AreEqual(360, revertedRT.width);
            Assert.AreEqual(780, revertedRT.height);

            // Toggle ON again (providers reattached) → SetDirty → next frame scales back up
            canvasRenderer.MatchCanvasResolution = true;

            yield return null;
            yield return new WaitForEndOfFrame();

            var scaledRT = m_strategy.GetRenderTexture(m_panel);
            Assert.IsNotNull(scaledRT);
            Assert.AreEqual(Mathf.CeilToInt(360 * 3.25f), scaledRT.width);
            Assert.AreEqual(Mathf.CeilToInt(780 * 3.25f), scaledRT.height);

            Object.Destroy(canvasGO);
        }
    }
}