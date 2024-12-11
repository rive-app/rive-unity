using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Rive.Components;
using Rive.Tests.Utils;
using Rive.Utils;

namespace Rive.Tests
{
    public class ProceduralRiveWidgetTests
    {
        private GameObject m_widgetObject;
        private ProceduralRiveWidget m_widget;
        private RivePanel m_panel;
        private MockLogger m_mockLogger;
        private MockProceduralDrawing m_mockDrawing;

        [SetUp]
        public void Setup()
        {
            m_mockLogger = new MockLogger();
            DebugLogger.Instance = m_mockLogger;

            m_panel = RivePanelTestUtils.CreatePanel();
            m_widget = RivePanelTestUtils.CreateWidget<ProceduralRiveWidget>();
            m_widgetObject = m_widget.gameObject;
            m_mockDrawing = m_widgetObject.AddComponent<MockProceduralDrawing>();

            m_widget.transform.SetParent(m_panel.WidgetContainer, false);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(m_widgetObject);
            Object.DestroyImmediate(m_panel.gameObject);
        }

        [Test]
        public void Load_ValidDrawing_SetsCorrectStatus()
        {
            Assert.AreEqual(WidgetStatus.Uninitialized, m_widget.Status);
            Assert.IsNotNull(m_mockDrawing);

            m_widget.Load(m_mockDrawing);

            Assert.IsTrue(m_mockLogger.LoggedWarnings.Count == 0 || m_mockLogger.LoggedErrors.Count == 0);

            Assert.AreEqual(WidgetStatus.Loaded, m_widget.Status);
        }

        [Test]
        public void Load_NullDrawing_LogsWarning()
        {
            m_widget.Load(null);
            Assert.IsTrue(m_mockLogger.LoggedErrors.Count > 0);
            Assert.AreNotEqual(WidgetStatus.Loaded, m_widget.Status);
        }

        [UnityTest]
        public IEnumerator Tick_ValidDrawing_CallsAdvance()
        {
            m_panel.UpdateMode = RivePanel.PanelUpdateMode.Manual;
            m_widget.Load(m_mockDrawing);
            yield return null;

            float deltaTime = 0.016f;
            m_widget.Tick(deltaTime);

            Assert.AreEqual(deltaTime, m_mockDrawing.LastDeltaTime);
            Assert.AreEqual(1, m_mockDrawing.AdvanceCallCount);
        }

        [Test]
        public void HitTest_DelegatesTo_ProceduralDrawing()
        {
            m_widget.Load(m_mockDrawing);
            Vector2 testPoint = new Vector2(0.5f, 0.5f);

            m_widget.HitTest(testPoint);

            Assert.AreEqual(testPoint, m_mockDrawing.LastHitTestPoint);
            Assert.AreEqual(1, m_mockDrawing.HitTestCallCount);
        }

        [Test]
        public void PointerEvents_DelegateTo_ProceduralDrawing()
        {
            m_widget.Load(m_mockDrawing);
            Vector2 testPoint = new Vector2(0.5f, 0.5f);

            m_widget.OnPointerDown(testPoint);
            m_widget.OnPointerMove(testPoint);
            m_widget.OnPointerUp(testPoint);

            Assert.AreEqual(1, m_mockDrawing.PointerDownCallCount);
            Assert.AreEqual(1, m_mockDrawing.PointerMoveCallCount);
            Assert.AreEqual(1, m_mockDrawing.PointerUpCallCount);
        }

        [Test]
        public void RenderObject_CreatedCorrectly()
        {
            m_widget.Load(m_mockDrawing);
            Assert.IsNotNull(m_widget.RenderObject);
            Assert.IsTrue(m_widget.RenderObject is ProceduralRenderObject);
        }

        [UnityTest]
        public IEnumerator Tick_RedrawEvent_OnlyTriggeredWhenAdvanceReturnsTrue()
        {
            m_panel.UpdateMode = RivePanel.PanelUpdateMode.Manual;
            m_widget.Load(m_mockDrawing);
            yield return null;

            bool shouldRedraw = m_widget.Tick(0.016f);

            // Mock drawing returns true by default, should trigger redraw
            Assert.IsTrue(m_mockDrawing.AdvanceReturnValue);
            Assert.IsTrue(shouldRedraw);

            shouldRedraw = m_widget.Tick(0.016f);
            Assert.IsTrue(shouldRedraw, "Redraw should be triggered again when Advance returns true");

            // Override advance to return false
            m_mockDrawing.AdvanceReturnValue = false;
            shouldRedraw = m_widget.Tick(0.016f);
            Assert.IsFalse(shouldRedraw, "Redraw should not be triggered when Advance returns false and nothing else has changed");

            // Set back to true
            m_mockDrawing.AdvanceReturnValue = true;
            shouldRedraw = m_widget.Tick(0.016f);
            Assert.IsTrue(shouldRedraw, "Redraw should be triggered again when Advance returns true again");
        }

    }

    public class MockProceduralDrawing : ProceduralDrawing
    {
        public float LastDeltaTime { get; private set; }
        public int AdvanceCallCount { get; private set; }

        public bool AdvanceReturnValue = true;
        public int DrawCallCount { get; private set; }
        public Vector2 LastHitTestPoint { get; private set; }
        public int HitTestCallCount { get; private set; }
        public int PointerDownCallCount { get; private set; }
        public int PointerMoveCallCount { get; private set; }
        public int PointerUpCallCount { get; private set; }

        public override void Draw(IRenderer renderer, AABB frame, RenderContext renderContext)
        {
            DrawCallCount++;
        }

        public override bool Advance(float deltaTime)
        {
            LastDeltaTime = deltaTime;
            AdvanceCallCount++;
            return AdvanceReturnValue;
        }

        public override bool HitTest(Vector2 point, Rect rect)
        {
            LastHitTestPoint = point;
            HitTestCallCount++;
            return true;
        }

        public override bool HandlePointerDown(Vector2 point, Rect rect)
        {
            PointerDownCallCount++;
            return true;
        }

        public override bool HandlePointerMove(Vector2 point, Rect rect)
        {
            PointerMoveCallCount++;
            return true;
        }

        public override bool HandlePointerUp(Vector2 point, Rect rect)
        {
            PointerUpCallCount++;
            return true;
        }
    }
}