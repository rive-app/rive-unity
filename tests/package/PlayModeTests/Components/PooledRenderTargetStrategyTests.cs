using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Rive.Components;
using Rive.Tests.Utils;
using Rive.Utils;

namespace Rive.Tests
{
    public class PooledRenderTargetStrategyTests
    {
        private PooledRenderTargetStrategy m_strategy;
        private RivePanel m_panel;
        private MockLogger m_mockLogger;

        [SetUp]
        public void Setup()
        {
            m_mockLogger = new MockLogger();
            DebugLogger.Instance = m_mockLogger;

            m_panel = RivePanelTestUtils.CreatePanel();
            var m_strategyGO = new GameObject("PooledRenderTargetStrategy");
            m_strategy = m_strategyGO.AddComponent<PooledRenderTargetStrategy>();

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
        public void RegisterPanel_WithValidPanel_Succeeds()
        {
            m_strategy.UnregisterPanel(m_panel);
            Assert.IsFalse(m_strategy.IsPanelRegistered(m_panel));

            Assert.IsTrue(m_strategy.RegisterPanel(m_panel));
            Assert.IsTrue(m_strategy.IsPanelRegistered(m_panel));
            Assert.IsNotNull(m_strategy.GetRenderTexture(m_panel));
        }

        [Test]
        public void RegisterPanel_WithNullPanel_Fails()
        {
            Assert.IsFalse(m_strategy.RegisterPanel(null));
        }

        [Test]
        public void RegisterPanel_AlreadyRegistered_Fails()
        {
            m_strategy.UnregisterPanel(m_panel);
            Assert.IsTrue(m_strategy.RegisterPanel(m_panel));
            Assert.IsFalse(m_strategy.RegisterPanel(m_panel));
        }

        [Test]
        public void RegisterPanel_ExceedsMaxPoolSize_FailsInFixedMode()
        {
            m_strategy.PoolOverflow = PooledRenderTargetStrategy.PoolOverflowBehavior.Fixed;
            int maxSize = m_strategy.MaxPoolSize;

            // Create panels up to max size
            var panels = new RivePanel[maxSize + 1];
            for (int i = 0; i < maxSize + 1; i++)
            {
                panels[i] = RivePanelTestUtils.CreatePanel($"Panel{i}");
            }

            // Register panels
            for (int i = 0; i < maxSize; i++)
            {
                Assert.IsTrue(m_strategy.RegisterPanel(panels[i]),
                    $"Panel {i} should register successfully within pool limit");
            }

            // This should fail as we're at max capacity
            Assert.IsFalse(m_strategy.RegisterPanel(panels[maxSize]),
                "Registration should fail when pool is full in Fixed mode");

            // Cleanup
            foreach (var panel in panels)
            {
                DestroyObject(panel.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator PanelScaling_CalculatesCorrectTransforms()
        {
            m_panel.SetDimensions(new Vector2(200, 100));
            m_strategy.RegisterPanel(m_panel);

            yield return null;

            Vector2 scale = m_strategy.GetPanelScale(m_panel);
            Vector2 offset = m_strategy.GetPanelOffset(m_panel);

            Assert.IsTrue(scale.x > 0 && scale.y > 0, "Scale should be positive");
            Assert.IsTrue(offset.x >= 0 && offset.y >= 0, "Offset should be non-negative");

            // Verify aspect ratio is maintained
            float panelAspect = 200f / 100f;
            float scaleAspect = scale.x / scale.y;
            Assert.AreEqual(panelAspect, scaleAspect, 0.01f, "Aspect ratio should be maintained");
        }

        [UnityTest]
        public IEnumerator DrawTiming_BatchedMode_DrawsOncePerFrame()
        {
            m_strategy.DrawTiming = DrawTimingOption.DrawBatched;
            m_strategy.RegisterPanel(m_panel);

            yield return null;

            var initialTexture = m_strategy.GetRenderTexture(m_panel);
            Assert.IsNotNull(initialTexture);

            // Request multiple draws in same frame
            m_strategy.DrawPanel(m_panel);
            m_strategy.DrawPanel(m_panel);

            yield return new WaitForEndOfFrame();

            Assert.IsNotNull(m_strategy.GetRenderTexture(m_panel));
        }

        [UnityTest]
        public IEnumerator MultiplePanels_SharePooledResources()
        {
            int initialPoolSize = m_strategy.InitialPoolSize;
            var panels = new RivePanel[initialPoolSize];

            // Create and register multiple panels
            for (int i = 0; i < initialPoolSize; i++)
            {
                panels[i] = RivePanelTestUtils.CreatePanel($"Panel{i}");
                Assert.IsTrue(m_strategy.RegisterPanel(panels[i]));
            }

            yield return null;

            // Verify each panel has a valid render texture
            foreach (var panel in panels)
            {
                Assert.IsNotNull(m_strategy.GetRenderTexture(panel));
            }

            // Cleanup
            foreach (var panel in panels)
            {
                DestroyObject(panel.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator UnregisterPanel_ReleasesPooledResources()
        {
            m_strategy.RegisterPanel(m_panel);
            var texture = m_strategy.GetRenderTexture(m_panel);
            Assert.IsNotNull(texture);

            m_strategy.UnregisterPanel(m_panel);
            yield return null;

            Assert.IsFalse(m_strategy.IsPanelRegistered(m_panel));
            Assert.IsNull(m_strategy.GetRenderTexture(m_panel));
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
        public IEnumerator PanelResize_UpdatesScaleAndOffset()
        {
            m_strategy.Configure(new Vector2Int(192, 108), 2, 5, PooledRenderTargetStrategy.PoolOverflowBehavior.Flexible);
            m_strategy.RegisterPanel(m_panel);
            m_panel.SetDimensions(new Vector2(192, 108));

            yield return null;

            Vector2 initialScale = m_strategy.GetPanelScale(m_panel);
            Vector2 initialOffset = m_strategy.GetPanelOffset(m_panel);

            Assert.AreEqual(Vector2.one, initialScale, "Initial scale should be 1");
            Assert.AreEqual(Vector2.zero, initialOffset, "Initial offset should be 0");

            m_panel.SetDimensions(new Vector2(200, 200));
            m_strategy.DrawPanel(m_panel);

            yield return new WaitForEndOfFrame();

            Vector2 newScale = m_strategy.GetPanelScale(m_panel);
            Vector2 newOffset = m_strategy.GetPanelOffset(m_panel);

            Assert.AreNotEqual(initialScale, newScale, "Scale should update with panel size");
            Assert.AreNotEqual(initialOffset, newOffset, "Offset should update with panel size");
        }

        [Test]
        public void Configure_BeforeInitialization_Succeeds()
        {
            Vector2Int newSize = new Vector2Int(512, 512);
            int newInitialSize = 3;
            int newMaxSize = 10;
            var newOverflow = PooledRenderTargetStrategy.PoolOverflowBehavior.Flexible;

            Assert.IsTrue(m_strategy.Configure(newSize, newInitialSize, newMaxSize, newOverflow));

            Assert.AreEqual(newSize, m_strategy.PooledTextureSize);
            Assert.AreEqual(newInitialSize, m_strategy.InitialPoolSize);
            Assert.AreEqual(newMaxSize, m_strategy.MaxPoolSize);
            Assert.AreEqual(newOverflow, m_strategy.PoolOverflow);
        }

        [Test]
        public void Configure_AfterInitialization_Fails()
        {
            // Initialize the pool by registering a panel
            m_strategy.RegisterPanel(m_panel);

            Vector2Int newSize = new Vector2Int(512, 512);
            int newInitialSize = 3;
            int newMaxSize = 10;
            var newOverflow = PooledRenderTargetStrategy.PoolOverflowBehavior.Flexible;

            Assert.IsFalse(m_strategy.Configure(newSize, newInitialSize, newMaxSize, newOverflow));
            Assert.IsTrue(m_mockLogger.LoggedWarnings.Count > 0);
        }

        [Test]
        public void Configure_AffectsPooledTextures()
        {
            // Configure smaller texture size
            Vector2Int smallSize = new Vector2Int(256, 256);
            m_strategy.Configure(smallSize, 2, 5, PooledRenderTargetStrategy.PoolOverflowBehavior.Fixed);

            m_strategy.RegisterPanel(m_panel);
            var texture = m_strategy.GetRenderTexture(m_panel);

            Assert.AreEqual(smallSize.x, texture.width);
            Assert.AreEqual(smallSize.y, texture.height);
        }

        [Test]
        public void Configure_AffectsPoolSize()
        {
            // Configure small pool
            int smallPoolSize = 2;
            m_strategy.Configure(
                new Vector2Int(512, 512),
                smallPoolSize,
                smallPoolSize,
                PooledRenderTargetStrategy.PoolOverflowBehavior.Fixed
            );

            var panels = new RivePanel[smallPoolSize + 1];

            // Should be able to register up to pool size
            for (int i = 0; i < smallPoolSize; i++)
            {
                panels[i] = RivePanelTestUtils.CreatePanel($"Panel{i}");
                Assert.IsTrue(m_strategy.RegisterPanel(panels[i]));
            }

            // Additional panel should fail to register
            panels[smallPoolSize] = RivePanelTestUtils.CreatePanel("ExtraPanel");
            Assert.IsFalse(m_strategy.RegisterPanel(panels[smallPoolSize]));

            // Cleanup
            foreach (var panel in panels)
            {
                if (panel != null)
                {
                    DestroyObject(panel.gameObject);
                }
            }
        }

        [UnityTest]
        public IEnumerator Cleanup_DisposesAllResources()
        {
            // Register multiple panels
            var panels = new RivePanel[3];
            for (int i = 0; i < panels.Length; i++)
            {
                panels[i] = RivePanelTestUtils.CreatePanel($"Panel{i}");
                m_strategy.RegisterPanel(panels[i]);
            }

            // Store references to textures so we can verify cleanup
            var textures = new RenderTexture[panels.Length];
            for (int i = 0; i < panels.Length; i++)
            {
                textures[i] = m_strategy.GetRenderTexture(panels[i]);
                Assert.IsNotNull(textures[i]);
            }

            // Destroy strategy
            DestroyObject(m_strategy);
            yield return null;

            // Verify cleanup
            foreach (var panel in panels)
            {
                Assert.IsFalse(m_strategy.IsPanelRegistered(panel));
            }

            foreach (var texture in textures)
            {
                Assert.IsTrue(texture == null || !texture.IsCreated());
            }

            foreach (var panel in panels)
            {
                DestroyObject(panel.gameObject);
            }
        }
    }
}