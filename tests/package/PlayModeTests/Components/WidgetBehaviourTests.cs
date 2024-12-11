using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Rive.Components;
using Rive.Tests.Utils;
using Rive.Utils;

namespace Rive.Tests
{
    public class WidgetBehaviourTests
    {
        private GameObject m_panelObject;
        private RivePanel m_panel;
        private MockRenderTargetStrategy m_mockStrategy;
        private MockLogger m_mockLogger;

        [SetUp]
        public void Setup()
        {
            m_mockLogger = new MockLogger();
            DebugLogger.Instance = m_mockLogger;

            m_panel = RivePanelTestUtils.CreatePanel();
            m_panelObject = m_panel.gameObject;

            m_mockStrategy = m_panelObject.AddComponent<MockRenderTargetStrategy>();
            m_panel.RenderTargetStrategy = m_mockStrategy;

        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(m_panelObject);
        }

        [Test]
        public void OnEnable_AutomaticallyFindsParentPanel()
        {
            var widget = RivePanelTestUtils.CreateWidget<MockRiveWidgetBase>();
            widget.transform.SetParent(m_panel.transform);

            Assert.AreEqual(m_panel, widget.RivePanel);
        }

        [Test]
        public void OnDisable_UnregistersFromPanel()
        {
            var widget = RivePanelTestUtils.CreateWidget<MockRiveWidgetBase>();
            m_panel.AddToHierarchy(widget);

            Assert.IsTrue(m_panel.ContainsWidget(widget));
            widget.gameObject.SetActive(false);

            Assert.IsFalse(m_panel.ContainsWidget(widget));
        }

        [UnityTest]
        public IEnumerator TransformParentChanged_UpdatesPanelReference()
        {
            var widget = RivePanelTestUtils.CreateWidget<MockRiveWidgetBase>();
            var panel2 = RivePanelTestUtils.CreatePanel("Panel2");

            m_panel.AddToHierarchy(widget);
            Assert.AreEqual(m_panel, widget.RivePanel);

            widget.transform.SetParent(panel2.transform);
            yield return null;

            Assert.AreEqual(panel2, widget.RivePanel);
            Assert.IsFalse(m_panel.ContainsWidget(widget));
            Assert.IsTrue(panel2.ContainsWidget(widget));

            UnityEngine.Object.DestroyImmediate(panel2.gameObject);
        }

        [Test]
        public void RectTransformChange_TriggersRedraw_WhenWidgetLoaded()
        {
            var widget = RivePanelTestUtils.CreateWidget<MockRiveWidgetBase>();

            // For performance, the WidgetBase does not tick if the widget isn't loaded
            Assert.AreNotEqual(widget.Status, WidgetStatus.Loaded);

            widget.SetStatus(WidgetStatus.Loaded);

            m_panel.AddToHierarchy(widget);

            bool shouldRedraw = false;
            // size changes should trigger redraw
            widget.RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 100);
            widget.RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 100);
            shouldRedraw = widget.Tick(0.016f);
            Assert.IsTrue(shouldRedraw);

            // Reset
            shouldRedraw = false;

            // rotation changes should trigger redraw
            widget.RectTransform.rotation = Quaternion.Euler(0, 0, 45);
            shouldRedraw = widget.Tick(0.016f);
            Assert.IsTrue(shouldRedraw);

            // Reset
            shouldRedraw = false;

            // scale changes should trigger redraw
            widget.RectTransform.localScale = new Vector3(2, 2, 1);
            shouldRedraw = widget.Tick(0.016f);
            Assert.IsTrue(shouldRedraw);

            // Reset
            shouldRedraw = false;

            // position changes should trigger redraw
            widget.RectTransform.anchoredPosition = new Vector2(50, 50);
            shouldRedraw = widget.Tick(0.016f);
            Assert.IsTrue(shouldRedraw);


            // Reset
            shouldRedraw = false;

            // Tick without changes should not trigger redraw
            shouldRedraw = widget.Tick(0.016f);

            Assert.IsFalse(shouldRedraw);

        }

        [Test]
        public void RectTransformChange_DoesntTriggersRedraw_WhenWidgetNotLoaded()
        {
            var widget = RivePanelTestUtils.CreateWidget<MockRiveWidgetBase>();

            // For performance, the WidgetBase does not tick if the widget isn't loaded
            widget.SetStatus(WidgetStatus.Uninitialized);
            m_panel.AddToHierarchy(widget);



            bool shouldRedraw = false;


            widget.RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 100);
            widget.RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 100);


            // Force update since we're not in play mode
            shouldRedraw = widget.Tick(0.016f);


            Assert.IsFalse(shouldRedraw);
        }

        [Test]
        public void WidgetStatusChange_TriggersRedraw_WhenLoaded()
        {
            var widget = RivePanelTestUtils.CreateWidget<MockRiveWidgetBase>();
            m_panel.AddToHierarchy(widget);

            bool shouldRedraw = false;

            // Initially not loaded, should not trigger redraw
            widget.SetStatus(WidgetStatus.Uninitialized);
            shouldRedraw = widget.Tick(0.016f);
            Assert.IsFalse(shouldRedraw);


            // Setting to loaded should trigger redraw
            widget.SetStatus(WidgetStatus.Loaded);
            shouldRedraw = widget.Tick(0.016f);
            Assert.IsTrue(shouldRedraw);
        }

        [UnityTest]
        public IEnumerator SiblingIndexChanged_TriggersEvent()
        {
            var widget1 = RivePanelTestUtils.CreateWidget<MockRiveWidgetBase>();
            var widget2 = RivePanelTestUtils.CreateWidget<MockRiveWidgetBase>();

            // For performance, the WidgetBase does not tick if the widget isn't loaded
            widget1.SetStatus(WidgetStatus.Loaded);
            widget2.SetStatus(WidgetStatus.Loaded);

            m_panel.AddToHierarchy(widget1);
            m_panel.AddToHierarchy(widget2);

            m_panel.UpdateMode = RivePanel.PanelUpdateMode.Auto;

            Assert.AreEqual(widget1.transform.GetSiblingIndex(), 0);
            Assert.AreEqual(widget2.transform.GetSiblingIndex(), 1);


            bool siblingIndexChanged = false;
            widget1.OnSiblingIndexChanged += () => siblingIndexChanged = true;

            widget1.transform.SetSiblingIndex(1);

            yield return null;

            Assert.IsTrue(siblingIndexChanged);
        }

        [Test]
        public void HitTestBehavior_DefaultsToFalse()
        {
            var widget = RivePanelTestUtils.CreateWidget<MockRiveWidgetBase>();
            Assert.IsFalse(widget.HitTest(Vector2.zero));
        }

        [Test]
        public void PointerEvents_DefaultToFalse()
        {
            var widget = RivePanelTestUtils.CreateWidget<MockRiveWidgetBase>();
            Assert.IsFalse(widget.OnPointerDown(Vector2.zero));
            Assert.IsFalse(widget.OnPointerUp(Vector2.zero));
            Assert.IsFalse(widget.OnPointerMove(Vector2.zero));
        }

        [UnityTest]
        public IEnumerator Status_ChangeTriggers_StatusChangedEvent()
        {
            var widget = RivePanelTestUtils.CreateWidget<MockRiveWidgetBase>();
            m_panel.AddToHierarchy(widget);

            int statusChangeCount = 0;
            widget.OnWidgetStatusChanged.AddListener(() => statusChangeCount++);

            widget.SetStatus(WidgetStatus.Loading);
            yield return null;

            Assert.AreEqual(1, statusChangeCount);
            Assert.AreEqual(WidgetStatus.Loading, widget.Status);

            // Setting same status should not trigger event
            widget.SetStatus(WidgetStatus.Loading);
            yield return null;

            Assert.AreEqual(1, statusChangeCount);
        }

        [Test]
        public void RenderObject_UpdatesTransformOnEnable()
        {
            var widget = RivePanelTestUtils.CreateWidget<MockRiveWidgetBase>();
            m_panel.AddToHierarchy(widget);

            var initialTransform = widget.RenderObject.RenderTransform;

            widget.gameObject.SetActive(false);
            widget.RectTransform.sizeDelta = new Vector2(100, 100);
            widget.gameObject.SetActive(true);

            var newTransform = widget.RenderObject.RenderTransform;
            Assert.AreNotEqual(initialTransform, newTransform);
        }
    }

    public class MockRiveWidgetBase : WidgetBehaviour
    {
        private MockRenderObject m_renderObject = new MockRenderObject();
        private HitTestBehavior m_hitTestBehavior = HitTestBehavior.Opaque;

        public override IRenderObject RenderObject => m_renderObject;
        public override HitTestBehavior HitTestBehavior
        {
            get => m_hitTestBehavior;
            set => m_hitTestBehavior = value;
        }

        public void SetStatus(WidgetStatus status)
        {
            Status = status;
        }
    }
}