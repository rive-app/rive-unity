using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Rive.Components;
using Rive.Tests.Utils;
using Rive.Utils;

namespace Rive.Tests
{
    public class AtlasRenderTargetStrategyTests
    {
        private AtlasRenderTargetStrategy m_strategy;
        private RivePanel m_panel;
        private MockLogger m_mockLogger;

        [SetUp]
        public void Setup()
        {
            m_mockLogger = new MockLogger();
            DebugLogger.Instance = m_mockLogger;

            m_panel = RivePanelTestUtils.CreatePanel();
            var strategyGO = new GameObject("AtlasRenderTargetStrategy");
            m_strategy = strategyGO.AddComponent<AtlasRenderTargetStrategy>();

            // When updating the strategy, the panel inherits the previous strategy's rendering state (e.g. rendering is stopped)
            // So we need to ensure the panel is not rendering before updating the strategy so that it can be properly registered in the tests
            if (m_panel.IsRendering)
            {
                m_panel.StopRendering();
            }
            m_panel.RenderTargetStrategy = m_strategy;
        }

        [TearDown]
        public void TearDown()
        {
            if (m_panel != null && m_panel.gameObject != null)
            {
                DestroyObject(m_panel.gameObject);
            }
            if (m_strategy != null && m_strategy.gameObject != null)
            {
                DestroyObject(m_strategy.gameObject);
            }
        }

        private void DestroyObject(Object obj)
        {
            if (obj != null)
            {
                Object.Destroy(obj);
            }
        }

        [Test]
        public void Configure_BeforeInitialization_Succeeds()
        {

            Vector2Int startingSize = new Vector2Int(512, 512);
            Vector2Int maxSize = new Vector2Int(1024, 1024);
            int maxResPerPanel = 256;
            int padding = 2;

            m_strategy.Configure(startingSize, maxSize, maxResPerPanel, padding);

            Assert.AreEqual(startingSize, m_strategy.StartingSize);
            Assert.AreEqual(maxSize, m_strategy.MaxAtlasSize);
            Assert.AreEqual(maxResPerPanel, m_strategy.MaxResolutionPerObject);
            Assert.AreEqual(padding, m_strategy.Padding);
        }

        [Test]
        public void Configure_AfterInitialization_Fails()
        {
            // Initialize the pool by registering a panel
            m_strategy.RegisterPanel(m_panel);

            Vector2Int startingSize = new Vector2Int(1024, 1024);
            Vector2Int maxSize = new Vector2Int(2048, 2048);
            int maxResPerPanel = 512;
            int padding = 2;

            m_strategy.Configure(startingSize, maxSize, maxResPerPanel, padding);
            Assert.IsTrue(m_mockLogger.LoggedWarnings.Count > 0);
        }

        [Test]
        public void Configure_ValidParameters_Succeeds()
        {
            Vector2Int startingSize = new Vector2Int(512, 512);
            Vector2Int maxSize = new Vector2Int(1024, 1024);
            int maxResPerPanel = 256;
            int padding = 2;

            m_strategy.Configure(startingSize, maxSize, maxResPerPanel, padding);

            Assert.AreEqual(startingSize, m_strategy.StartingSize);
            Assert.AreEqual(maxSize, m_strategy.MaxAtlasSize);
            Assert.AreEqual(maxResPerPanel, m_strategy.MaxResolutionPerObject);
            Assert.AreEqual(padding, m_strategy.Padding);
        }

        [Test]
        public void Configure_InvalidParameters_LogsWarning()
        {
            m_strategy.Configure(Vector2Int.zero, new Vector2Int(1024, 1024));
            Assert.IsTrue(m_mockLogger.LoggedWarnings.Count > 0);

            m_mockLogger.Clear();
            m_strategy.Configure(new Vector2Int(512, 512), Vector2Int.zero);
            Assert.IsTrue(m_mockLogger.LoggedWarnings.Count > 0);
        }

        [UnityTest]
        public IEnumerator RegisterPanel_WithinAtlasLimits_Succeeds()
        {
            m_strategy.Configure(new Vector2Int(512, 512), new Vector2Int(1024, 1024));
            m_panel.SetDimensions(new Vector2(100, 100));

            Assert.IsTrue(m_strategy.RegisterPanel(m_panel));
            yield return null;

            Assert.IsTrue(m_strategy.IsPanelRegistered(m_panel));
            Assert.IsNotNull(m_strategy.GetRenderTexture(m_panel));
        }

        [UnityTest]
        public IEnumerator RegisterPanel_ExceedsMaxResolution_ScalesDown()
        {
            m_strategy.Configure(
                new Vector2Int(512, 512),
                new Vector2Int(1024, 1024),
                maxResolutionPerPanel: 256
            );

            m_panel.SetDimensions(new Vector2(512, 512));
            Assert.IsTrue(m_strategy.RegisterPanel(m_panel));

            yield return null;

            Vector2 scale = m_strategy.GetPanelScale(m_panel);
            Assert.Less(scale.x, 1f, "Panel should be scaled down");
            Assert.Less(scale.y, 1f, "Panel should be scaled down");
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
        public IEnumerator AtlasGrowth_AddsMultiplePanels()
        {
            m_strategy.Configure(
                new Vector2Int(256, 256),
                new Vector2Int(1024, 1024)
            );

            // Create multiple panels that won't fit in initial size
            var panels = new RivePanel[4];
            for (int i = 0; i < panels.Length; i++)
            {
                panels[i] = RivePanelTestUtils.CreatePanel($"Panel{i}");
                panels[i].SetDimensions(new Vector2(200, 200));
                Assert.IsTrue(m_strategy.RegisterPanel(panels[i]));
            }

            yield return new WaitForEndOfFrame();

            // Verify atlas grew to accommodate panels
            var texture = m_strategy.GetRenderTexture(panels[0]);
            Assert.Greater(texture.width, 256);
            Assert.Greater(texture.height, 256);

            foreach (var panel in panels)
            {
                DestroyObject(panel.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator DrawTiming_BatchedMode_HandlesMultipleUpdates()
        {
            m_strategy.DrawTiming = DrawTimingOption.DrawBatched;
            m_strategy.Configure(new Vector2Int(512, 512), new Vector2Int(1024, 1024));

            m_panel.SetDimensions(new Vector2(100, 100));
            m_strategy.RegisterPanel(m_panel);

            yield return null;

            // Change size multiple times in same frame
            m_panel.SetDimensions(new Vector2(200, 200));
            m_strategy.DrawPanel(m_panel);
            m_panel.SetDimensions(new Vector2(300, 300));
            m_strategy.DrawPanel(m_panel);

            var initialScale = m_strategy.GetPanelScale(m_panel);

            yield return new WaitForEndOfFrame();

            var finalScale = m_strategy.GetPanelScale(m_panel);
            Assert.AreNotEqual(initialScale, finalScale, "Scale should update after batched changes");
        }

        [UnityTest]
        public IEnumerator UnregisterPanel_RemovesFromAtlas()
        {
            m_strategy.Configure(new Vector2Int(512, 512), new Vector2Int(1024, 1024));
            m_panel.SetDimensions(new Vector2(100, 100));

            m_strategy.RegisterPanel(m_panel);
            Assert.IsTrue(m_strategy.IsPanelRegistered(m_panel));
            yield return null;

            m_strategy.UnregisterPanel(m_panel);
            yield return null;
            Assert.IsFalse(m_strategy.IsPanelRegistered(m_panel));
            Assert.IsNull(m_strategy.GetRenderTexture(m_panel));
        }



        [UnityTest]
        public IEnumerator GetPanelTransform_ReturnsCorrectValues()
        {
            m_strategy.Configure(new Vector2Int(512, 512), new Vector2Int(1024, 1024));
            m_panel.SetDimensions(new Vector2(100, 100));

            m_strategy.RegisterPanel(m_panel);

            yield return null;

            Vector2 offset = m_strategy.GetPanelOffset(m_panel);
            Vector2 scale = m_strategy.GetPanelScale(m_panel);

            Assert.IsTrue(offset.x >= 0 && offset.x <= 1, "Offset X should be normalized");
            Assert.IsTrue(offset.y >= 0 && offset.y <= 1, "Offset Y should be normalized");
            Assert.IsTrue(scale.x > 0 && scale.y > 0, "Scale should be positive");
        }

        [UnityTest]
        public IEnumerator Cleanup_DisposesResources()
        {
            m_strategy.Configure(new Vector2Int(512, 512), new Vector2Int(1024, 1024));

            // Register multiple panels
            var panels = new RivePanel[3];
            for (int i = 0; i < panels.Length; i++)
            {
                panels[i] = RivePanelTestUtils.CreatePanel($"Panel{i}");
                panels[i].SetDimensions(new Vector2(100, 100));
                m_strategy.RegisterPanel(panels[i]);
            }

            yield return null;

            var texture = m_strategy.GetRenderTexture(panels[0]);
            Assert.IsNotNull(texture);

            DestroyObject(m_strategy.gameObject);
            yield return null;

            Assert.IsTrue(texture == null || !texture.IsCreated());

            foreach (var panel in panels)
            {
                DestroyObject(panel.gameObject);
            }
        }

        [Test]
        public void CustomPackingProvider_IsUsedWhenProvided()
        {

            MockPackingStrategy mockPackingStrategy = new MockPackingStrategy();
            m_strategy.PackingStrategy = mockPackingStrategy;

            m_strategy.Configure(new Vector2Int(512, 512), new Vector2Int(1024, 1024));
            m_panel.SetDimensions(new Vector2(100, 100));
            m_strategy.RegisterPanel(m_panel);

            // Verify mock packer was used
            Assert.IsTrue(mockPackingStrategy.WasInitializeCalled, "Custom packing strategy should be initialized");
            Assert.IsTrue(mockPackingStrategy.WasInsertCalled, "Custom packing strategy should be used for insertion");

        }

        [Test]
        public void CustomPackingProvider_FallsBackToDefaultWhenNull()
        {
            m_strategy.PackingStrategy = null;
            m_strategy.Configure(new Vector2Int(512, 512), new Vector2Int(1024, 1024));
            m_panel.SetDimensions(new Vector2(100, 100));
            m_strategy.RegisterPanel(m_panel);

            Assert.IsNotNull(m_strategy.PackingStrategy);
            Assert.IsTrue(m_strategy.PackingStrategy is ShelfPackingStrategy,
                "Should fall back to ShelfPackingStrategy when no custom provider is set");
        }

        [UnityTest]
        public IEnumerator CustomPackingProvider_HandlesPackingFailure()
        {

            MockPackingStrategy mockPackingStrategy = new MockPackingStrategy();
            mockPackingStrategy.ShouldFailPacking = true;
            m_strategy.PackingStrategy = mockPackingStrategy;

            // Configure and try to register multiple panels
            m_strategy.Configure(new Vector2Int(512, 512), new Vector2Int(1024, 1024));

            var panels = new RivePanel[3];
            var registrationFailCount = 0;
            for (int i = 0; i < panels.Length; i++)
            {
                panels[i] = RivePanelTestUtils.CreatePanel($"Panel{i}");
                panels[i].SetDimensions(new Vector2(1000, 1000));
                bool wasRegistered = m_strategy.RegisterPanel(panels[i]);


                if (!wasRegistered)
                {
                    registrationFailCount++;
                }
            }

            Assert.IsTrue(mockPackingStrategy.WasInitializeCalled, "Custom packing strategy should be initialized");
            Assert.IsTrue(mockPackingStrategy.WasInsertCalled, "Custom packing strategy should be used for insertion");

            Assert.AreEqual(panels.Length, registrationFailCount, "All panels should fail to register");

            yield return null;


            foreach (var panel in panels)
            {
                DestroyObject(panel.gameObject);
            }
        }

        [Test]
        public void DoesPanelNeedClipping_NoWidgets_ReturnsFalse()
        {
            var panel = RivePanelTestUtils.CreatePanel();
            Assert.IsFalse(AtlasRenderTargetStrategy.DoesPanelNeedClipping(panel));
            DestroyObject(panel.gameObject);
        }

        [Test]
        public void DoesPanelNeedClipping_WidgetInsidePanel_ReturnsFalse()
        {
            var panel = RivePanelTestUtils.CreatePanel();
            panel.SetDimensions(new Vector2(100, 100));

            var widget = RivePanelTestUtils.CreateWidget<MockRiveWidget>();
            panel.AddToHierarchy(widget);

            widget.RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 50);
            widget.RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 50);
            widget.RectTransform.anchoredPosition = new Vector2(25, 25); // Centered in panel

            Assert.IsFalse(AtlasRenderTargetStrategy.DoesPanelNeedClipping(panel));
            DestroyObject(panel.gameObject);
        }

        [Test]
        public void DoesPanelNeedClipping_WidgetPartiallyOutside_ReturnsTrue()
        {
            var panel = RivePanelTestUtils.CreatePanel();
            panel.SetDimensions(new Vector2(100, 100));

            var widget = RivePanelTestUtils.CreateWidget<MockRiveWidget>();
            panel.AddToHierarchy(widget);

            widget.RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 50);
            widget.RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 50);
            widget.RectTransform.anchoredPosition = new Vector2(80, 80); // Partially outside

            Assert.IsTrue(AtlasRenderTargetStrategy.DoesPanelNeedClipping(panel));
            DestroyObject(panel.gameObject);
        }

        [Test]
        public void DoesPanelNeedClipping_RotatedWidgetOutside_ReturnsTrue()
        {
            var panel = RivePanelTestUtils.CreatePanel();
            panel.SetDimensions(new Vector2(100, 100));

            var widget = RivePanelTestUtils.CreateWidget<MockRiveWidget>();
            panel.AddToHierarchy(widget);

            widget.RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 50);
            widget.RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 50);
            widget.RectTransform.anchoredPosition = new Vector2(75, 75);
            widget.RectTransform.rotation = Quaternion.Euler(0, 0, 45); // Rotate 45 degrees

            // We want to check that it clips if any part of the widget is outside the panel

            Assert.IsTrue(AtlasRenderTargetStrategy.DoesPanelNeedClipping(panel));
            DestroyObject(panel.gameObject);
        }

        [Test]
        public void DoesPanelNeedClipping_ScaledWidgetOutside_ReturnsTrue()
        {
            var panel = RivePanelTestUtils.CreatePanel();
            panel.SetDimensions(new Vector2(100, 100));

            var widget = RivePanelTestUtils.CreateWidget<MockRiveWidget>();
            panel.AddToHierarchy(widget);
            widget.RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 50);
            widget.RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 50);
            widget.RectTransform.anchoredPosition = new Vector2(50, 50);
            widget.RectTransform.localScale = new Vector3(2, 2, 1); // Scale up to extend outside

            Assert.IsTrue(AtlasRenderTargetStrategy.DoesPanelNeedClipping(panel));
            DestroyObject(panel.gameObject);
        }

        [Test]
        public void DoesPanelNeedClipping_MultipleWidgets_OnlyOneOutside_ReturnsTrue()
        {
            var panel = RivePanelTestUtils.CreatePanel();
            panel.SetDimensions(new Vector2(100, 100));

            // Inside widget
            var widget1 = RivePanelTestUtils.CreateWidget<MockRiveWidget>();
            panel.AddToHierarchy(widget1);
            widget1.RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 50);
            widget1.RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 50);
            widget1.RectTransform.anchoredPosition = new Vector2(25, 25);

            // Outside widget
            var widget2 = RivePanelTestUtils.CreateWidget<MockRiveWidget>();
            widget2.RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 50);
            widget2.RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 50);
            widget2.RectTransform.anchoredPosition = new Vector2(80, 80);
            panel.AddToHierarchy(widget2);

            Assert.IsTrue(AtlasRenderTargetStrategy.DoesPanelNeedClipping(panel));
            DestroyObject(panel.gameObject);
        }

        [Test]
        public void DoesPanelNeedClipping_WidgetFillsPanel_ReturnsFalse()
        {
            var panel = RivePanelTestUtils.CreatePanel();
            panel.SetDimensions(new Vector2(100, 100));

            var widget = RivePanelTestUtils.CreateWidget<MockRiveWidget>();
            panel.AddToHierarchy(widget);
            RivePanelTestUtils.MakeWidgetFillPanel(widget);

            Assert.IsFalse(AtlasRenderTargetStrategy.DoesPanelNeedClipping(panel));
            DestroyObject(panel.gameObject);
        }

        [Test]
        public void DoesPanelNeedClipping_NullPanel_ReturnsFalse()
        {
            Assert.IsFalse(AtlasRenderTargetStrategy.DoesPanelNeedClipping(null));
        }


        private class MockPackingStrategy : RenderTargetAtlasPackingProvider.IPackingStrategy
        {
            public bool WasInitializeCalled { get; private set; }
            public bool WasInsertCalled { get; private set; }
            public bool ShouldFailPacking { get; set; }

            public void Initialize(int width, int height)
            {
                WasInitializeCalled = true;
            }

            public bool TryInsert(int width, int height, out RectInt rect)
            {
                WasInsertCalled = true;
                rect = ShouldFailPacking ?
                    new RectInt(0, 0, 0, 0) : // Failure
                    new RectInt(0, 0, width, height); // Success
                return !ShouldFailPacking;
            }
        }

    }
}