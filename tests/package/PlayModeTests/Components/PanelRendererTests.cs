using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Rive.Components;
using Rive.Tests.Utils;
using Rive.Utils;

namespace Rive.Tests
{
    public class PanelRendererTests
    {
        private MockRivePanel m_panel;
        private MockPanelRenderer m_renderer;
        private MockLogger m_mockLogger;
        private GameObject m_gameObject;

        [SetUp]
        public void Setup()
        {
            m_mockLogger = new MockLogger();
            DebugLogger.Instance = m_mockLogger;

            m_gameObject = new GameObject("TestRenderer");
            m_panel = new MockRivePanel();
            m_renderer = m_gameObject.AddComponent<MockPanelRenderer>();
            m_renderer.RivePanel = m_panel;

            // We do this to simulate the OnEnable event
            m_renderer.gameObject.SetActive(false);

            m_renderer.gameObject.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            m_panel.Dispose();
            UnityEngine.Object.DestroyImmediate(m_gameObject);
        }

        [Test]
        public void OnEnable_WithPanel_RegistersEvents()
        {
            m_renderer.gameObject.SetActive(false);
            Assert.AreEqual(0, m_renderer.UpdateVisualTargetCallCount);

            m_panel.IsRendering = true;
            m_renderer.gameObject.SetActive(true);

            Assert.AreEqual(1, m_renderer.UpdateVisualTargetCallCount,
                "UpdateVisualTarget should be called when enabled if panel is rendering");
        }

        [Test]
        public void OnDisable_UnregistersEvents()
        {
            m_panel.IsRendering = true;
            int initialCount = m_renderer.UpdateVisualTargetCallCount;

            m_renderer.gameObject.SetActive(false);

            // These events should not trigger updates after disable
            m_panel.TriggerRenderingStateChanged();
            m_panel.TriggerRenderTargetUpdated();

            Assert.AreEqual(initialCount, m_renderer.UpdateVisualTargetCallCount,
                "UpdateVisualTarget should not be called after disable");
        }

        [Test]
        public void RenderTargetUpdated_TriggersVisualUpdate_IfInitialPanelProvided()
        {
            m_panel.IsRendering = true;
            int initialCount = m_renderer.UpdateVisualTargetCallCount;

            m_panel.TriggerRenderTargetUpdated();

            Assert.AreEqual(initialCount + 1, m_renderer.UpdateVisualTargetCallCount,
                "UpdateVisualTarget should be called when render target is updated");
        }

        [Test]
        public void RenderingStateChanged_TriggersVisualUpdate_IfInitialPanelProvided()
        {
            int initialCount = m_renderer.UpdateVisualTargetCallCount;

            m_panel.IsRendering = true;
            m_panel.TriggerRenderingStateChanged();
            Assert.AreEqual(initialCount + 1, m_renderer.UpdateVisualTargetCallCount,
                "UpdateVisualTarget should be called when rendering starts");

            m_panel.IsRendering = false;
            m_panel.TriggerRenderingStateChanged();
            Assert.AreEqual(initialCount + 2, m_renderer.UpdateVisualTargetCallCount,
                "UpdateVisualTarget should be called when rendering stops");
        }

        [Test]
        public void PointerInputMode_ChangingValue_TriggersCallback()
        {
            int callbackCount = m_renderer.PointerInputModeChangedCount;

            m_renderer.PointerInputMode = PointerInputMode.DisablePointerInput;
            Assert.AreEqual(callbackCount + 1, m_renderer.PointerInputModeChangedCount,
                "HandlePointerInputModeChanged should be called when mode changes");

            // Setting same value should not trigger callback
            m_renderer.PointerInputMode = PointerInputMode.DisablePointerInput;
            Assert.AreEqual(callbackCount + 1, m_renderer.PointerInputModeChangedCount,
                "HandlePointerInputModeChanged should not be called when setting same value");
        }
    }

    public class MockRivePanel : IRivePanel
    {
        private bool m_isRendering;
        private bool m_enabled = true;
        private readonly RectTransform m_widgetContainer;
        private readonly List<IRiveWidget> m_widgets = new List<IRiveWidget>();
        private IPanelInputProvider m_inputProvider;
        private RenderTexture m_renderTexture;
        private Vector2 m_scaleInRenderTexture = Vector2.one;
        private Vector2 m_offsetInRenderTexture = Vector2.zero;

        public MockRivePanel()
        {
            // Create a GameObject with RectTransform for the widget container
            var containerObj = new GameObject("MockPanelContainer", typeof(RectTransform));
            m_widgetContainer = containerObj.GetComponent<RectTransform>();
        }

        public event Action<IRiveWidget> OnWidgetAdded;
        public event Action<IRiveWidget> OnWidgetRemoved;
        public event Action OnRenderingStateChanged;
        public event Action OnRenderTargetStrategyChanged;
        public event Action OnRenderTargetUpdated;

        public RectTransform WidgetContainer => m_widgetContainer;
        public IReadOnlyList<IRiveWidget> Widgets => m_widgets;

        public bool IsRendering
        {
            get => m_isRendering;
            set => m_isRendering = value;
        }

        public bool Enabled
        {
            get => m_enabled;
            set => m_enabled = value;
        }

        public IPanelInputProvider InputProvider
        {
            get => m_inputProvider;
            set => m_inputProvider = value;
        }

        public RenderTexture RenderTexture
        {
            get => m_renderTexture;
            set => m_renderTexture = value;
        }

        public Vector2 ScaleInRenderTexture
        {
            get => m_scaleInRenderTexture;
            set => m_scaleInRenderTexture = value;
        }

        public Vector2 OffsetInRenderTexture
        {
            get => m_offsetInRenderTexture;
            set => m_offsetInRenderTexture = value;
        }

        public bool StartRendering()
        {
            if (m_isRendering) return false;
            m_isRendering = true;
            TriggerRenderingStateChanged();
            return true;
        }

        public bool StopRendering()
        {
            if (!m_isRendering) return false;
            m_isRendering = false;
            TriggerRenderingStateChanged();
            return true;
        }

        public void TriggerRenderingStateChanged()
        {
            OnRenderingStateChanged?.Invoke();
        }

        public void TriggerRenderTargetUpdated()
        {
            OnRenderTargetUpdated?.Invoke();
        }

        public void TriggerRenderTargetStrategyChanged()
        {
            OnRenderTargetStrategyChanged?.Invoke();
        }

        public void AddWidget(IRiveWidget widget)
        {
            m_widgets.Add(widget);
            OnWidgetAdded?.Invoke(widget);
        }

        public void RemoveWidget(IRiveWidget widget)
        {
            if (m_widgets.Remove(widget))
            {
                OnWidgetRemoved?.Invoke(widget);
            }
        }

        public void Dispose()
        {
            if (m_widgetContainer != null)
            {
                UnityEngine.Object.DestroyImmediate(m_widgetContainer.gameObject);
            }
            if (m_renderTexture != null)
            {
                m_renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(m_renderTexture);
            }
        }

        List<IPanelInputProvider> InputProviders { get; } = new List<IPanelInputProvider>();

        public void RegisterInputProvider(IPanelInputProvider inputProvider)
        {
            InputProviders.Add(inputProvider);
        }

        public void UnregisterInputProvider(IPanelInputProvider inputProvider)
        {
            InputProviders.Remove(inputProvider);
        }
    }

    public class MockPanelRenderer : PanelRenderer
    {
        public int UpdateVisualTargetCallCount { get; private set; }
        public int PointerInputModeChangedCount { get; private set; }

        private IRivePanel m_panel;
        public override IRivePanel RivePanel
        {
            get => m_panel;
            internal set => m_panel = value;
        }

        protected override void UpdateVisualTarget()
        {
            UpdateVisualTargetCallCount++;
        }

        protected override void HandlePointerInputModeChanged()
        {
            base.HandlePointerInputModeChanged();
            PointerInputModeChangedCount++;
        }
    }
}