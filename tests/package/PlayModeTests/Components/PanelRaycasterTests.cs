using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Rive.Components;

namespace Rive.Tests
{
    public class PanelRaycasterTests
    {
        private RivePanel m_panel;
        private GameObject m_panelObject;
        private List<IRiveWidget> m_raycastResults;

        [SetUp]
        public void Setup()
        {
            m_panel = RivePanelTestUtils.CreatePanel("TestPanel");
            m_panelObject = m_panel.gameObject;
            m_raycastResults = new List<IRiveWidget>();

            m_panel.SetDimensions(new Vector2(1000, 1000));
        }

        [TearDown]
        public void TearDown()
        {
            if (m_panelObject != null)
            {
                Object.Destroy(m_panelObject);
            }
        }

        [Test]
        public void RaycastAll_EmptyPanel_ReturnsNoResults()
        {
            PanelRaycaster.RaycastAll(m_panel, new Vector2(0.5f, 0.5f), m_raycastResults);
            Assert.AreEqual(0, m_raycastResults.Count);
        }

        [Test]
        public void RaycastAll_DisabledWidget_IsIgnored()
        {
            var widget = CreateMockRiveWidget();
            m_panel.AddToHierarchy(widget);
            RivePanelTestUtils.MakeWidgetFillPanel(widget);

            widget.enabled = false;

            PanelRaycaster.RaycastAll(m_panel, new Vector2(0.5f, 0.5f), m_raycastResults);
            Assert.AreEqual(0, m_raycastResults.Count);
        }

        [Test]
        public void RaycastAll_HitTestBehaviorNone_IsIgnored()
        {
            var widget = CreateMockRiveWidget();
            m_panel.AddToHierarchy(widget);
            RivePanelTestUtils.MakeWidgetFillPanel(widget);

            widget.HitTestBehavior = HitTestBehavior.None;

            PanelRaycaster.RaycastAll(m_panel, new Vector2(0.5f, 0.5f), m_raycastResults);
            Assert.AreEqual(0, m_raycastResults.Count);
        }

        [Test]
        public void RaycastAll_OpaqueWidget_BlocksLowerWidgets()
        {
            var backWidget = CreateMockRiveWidget("BackWidget");
            var frontWidget = CreateMockRiveWidget("FrontWidget");

            m_panel.AddToHierarchy(backWidget);
            m_panel.AddToHierarchy(frontWidget);

            RivePanelTestUtils.MakeWidgetFillPanel(backWidget);
            RivePanelTestUtils.MakeWidgetFillPanel(frontWidget);

            backWidget.HitTestBehavior = HitTestBehavior.Opaque;
            frontWidget.HitTestBehavior = HitTestBehavior.Opaque;

            PanelRaycaster.RaycastAll(m_panel, new Vector2(0.5f, 0.5f), m_raycastResults);

            Assert.AreEqual(1, m_raycastResults.Count);
            Assert.AreEqual(frontWidget, m_raycastResults[0]);
        }

        [Test]
        public void RaycastAll_TransparentWidget_AllowsLowerWidgets()
        {
#pragma warning disable CS0618 // Transparent hit testing is deprecated but kept for backward compatibility
            var backWidget = CreateMockRiveWidget("BackWidget");
            var frontWidget = CreateMockRiveWidget("FrontWidget");

            m_panel.AddToHierarchy(backWidget);
            m_panel.AddToHierarchy(frontWidget);

            RivePanelTestUtils.MakeWidgetFillPanel(backWidget);
            RivePanelTestUtils.MakeWidgetFillPanel(frontWidget);

            backWidget.HitTestBehavior = HitTestBehavior.Opaque;
            frontWidget.HitTestBehavior = HitTestBehavior.Transparent;

            PanelRaycaster.RaycastAll(m_panel, new Vector2(0.5f, 0.5f), m_raycastResults);

            Assert.AreEqual(2, m_raycastResults.Count);
            Assert.AreEqual(frontWidget, m_raycastResults[0]);
            Assert.AreEqual(backWidget, m_raycastResults[1]);
#pragma warning restore CS0618
        }

        [Test]
        public void RaycastAll_TranslucentWidget_OnlyBlocksOnHit()
        {
            var backWidget = CreateMockRiveWidget("BackWidget");
            var frontWidget = CreateMockRiveWidget("FrontWidget");

            m_panel.AddToHierarchy(backWidget);
            m_panel.AddToHierarchy(frontWidget);

            RivePanelTestUtils.MakeWidgetFillPanel(backWidget);
            RivePanelTestUtils.MakeWidgetFillPanel(frontWidget);

            backWidget.HitTestBehavior = HitTestBehavior.Opaque;
            frontWidget.HitTestBehavior = HitTestBehavior.Translucent;

            // Test when front widget returns false for hit test
            ((MockRiveWidget)frontWidget).ReturnTrueOnHitTest = false;
            PanelRaycaster.RaycastAll(m_panel, new Vector2(0.5f, 0.5f), m_raycastResults);

            Assert.AreEqual(1, m_raycastResults.Count);
            Assert.AreEqual(backWidget, m_raycastResults[0]);

            // Test when front widget returns true for hit test
            m_raycastResults.Clear();
            ((MockRiveWidget)frontWidget).ReturnTrueOnHitTest = true;
            PanelRaycaster.RaycastAll(m_panel, new Vector2(0.5f, 0.5f), m_raycastResults);

            Assert.AreEqual(1, m_raycastResults.Count);
            Assert.AreEqual(frontWidget, m_raycastResults[0]);
        }

        [Test]
        public void TryGetNormalizedLocalPointInWidget_PointInsideWidget_ReturnsTrue()
        {
            var widget = CreateMockRiveWidget();
            m_panel.AddToHierarchy(widget);
            RivePanelTestUtils.MakeWidgetFillPanel(widget);

            Vector2 normalizedWidgetPoint;
            bool result = PanelRaycaster.TryGetNormalizedPointInWidget(
                m_panel,
                new Vector2(0.5f, 0.5f),
                widget,
                out normalizedWidgetPoint
            );

            Assert.IsTrue(result);
            Assert.AreEqual(new Vector2(0.5f, 0.5f), normalizedWidgetPoint);
        }


        [Test]
        public void TryGetNormalizedLocalPointInWidget_PointOutsideWidget_ReturnsFalse()
        {
            var widget = CreateMockRiveWidget();
            m_panel.AddToHierarchy(widget);

            // Position widget in top-left quarter of panel
            RivePanelTestUtils.PositionWidget(
                widget,
                new Vector2(0, 0.5f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(500, 500),
                Vector2.zero
            );

            Vector2 normalizedWidgetPoint;
            bool result = PanelRaycaster.TryGetNormalizedPointInWidget(
                m_panel,
                new Vector2(0.75f, 0.25f), // Point in bottom-right quarter
                widget,
                out normalizedWidgetPoint
            );

            Assert.IsFalse(result);
        }

        [Test]
        public void TryGetNormalizedLocalPointInWidget_CenterAlignedWidget_ReturnsCorrectPoints()
        {
            var widget = CreateMockRiveWidget();
            m_panel.AddToHierarchy(widget);

            // Center-aligned widget taking up middle 50% of panel
            RivePanelTestUtils.PositionWidget(
                widget,
                new Vector2(0.25f, 0.25f), // anchorMin
                new Vector2(0.75f, 0.75f), // anchorMax
                new Vector2(0.5f, 0.5f),   // pivot
                new Vector2(500, 500),     // size
                Vector2.zero              // position
            );

            Vector2 normalizedWidgetPoint;

            // Test center point
            bool result = PanelRaycaster.TryGetNormalizedPointInWidget(
                m_panel,
                new Vector2(0.5f, 0.5f),
                widget,
                out normalizedWidgetPoint
            );

            Assert.IsTrue(result);
            Assert.AreEqual(new Vector2(0.5f, 0.5f), normalizedWidgetPoint);

            // Test top-right corner of widget
            result = PanelRaycaster.TryGetNormalizedPointInWidget(
                m_panel,
                new Vector2(0.74f, 0.74f),
                widget,
                out normalizedWidgetPoint
            );

            Assert.IsTrue(result);
            Assert.That(normalizedWidgetPoint.x, Is.EqualTo(0.98f).Within(0.0001f));
            Assert.That(normalizedWidgetPoint.y, Is.EqualTo(0.98f).Within(0.0001f));

            // Test bottom-left corner of widget
            result = PanelRaycaster.TryGetNormalizedPointInWidget(
                m_panel,
                new Vector2(0.25f, 0.25f),
                widget,
                out normalizedWidgetPoint
            );

            Assert.IsTrue(result);
            Assert.AreEqual(new Vector2(0f, 0f), normalizedWidgetPoint);
        }

        [Test]
        public void TryGetNormalizedLocalPointInWidget_CornerAnchoredWidget_ReturnsCorrectPoints()
        {
            var widget = CreateMockRiveWidget();
            m_panel.AddToHierarchy(widget);

            // Widget anchored to top-right corner
            RivePanelTestUtils.PositionWidget(
                widget,
                new Vector2(1, 1),     // anchorMin
                new Vector2(1, 1),     // anchorMax
                new Vector2(1, 1),     // pivot
                new Vector2(200, 200), // size
                Vector2.zero          // position
            );

            Vector2 normalizedWidgetPoint;

            // Test center of widget
            bool result = PanelRaycaster.TryGetNormalizedPointInWidget(
                m_panel,
                new Vector2(0.9f, 0.9f),
                widget,
                out normalizedWidgetPoint
            );

            Assert.IsTrue(result);
            Assert.That(normalizedWidgetPoint.x, Is.EqualTo(0.50f).Within(0.0001f));
            Assert.That(normalizedWidgetPoint.y, Is.EqualTo(0.50f).Within(0.0001f));

            // Test top-right corner (anchor point)
            result = PanelRaycaster.TryGetNormalizedPointInWidget(
                m_panel,
                new Vector2(0.99f, 0.99f),
                widget,
                out normalizedWidgetPoint
            );

            Assert.IsTrue(result);
            Assert.That(normalizedWidgetPoint.x, Is.EqualTo(0.95f).Within(0.0001f));
            Assert.That(normalizedWidgetPoint.y, Is.EqualTo(0.95f).Within(0.0001f));
        }

        [Test]
        public void TryGetNormalizedLocalPointInWidget_RotatedWidget_ReturnsCorrectPoints()
        {
            var widget = CreateMockRiveWidget();
            m_panel.AddToHierarchy(widget);

            // Center widget and rotate it 45 degrees
            RivePanelTestUtils.PositionWidget(
                widget,
                new Vector2(0.25f, 0.25f), // anchorMin
                new Vector2(0.75f, 0.75f), // anchorMax
                new Vector2(0.5f, 0.5f),   // pivot
                new Vector2(500, 500),     // size
                Vector2.zero              // position
            );

            widget.RectTransform.rotation = Quaternion.Euler(0, 0, 45);

            Vector2 normalizedWidgetPoint;

            // Test center point (should still work with rotation)
            bool result = PanelRaycaster.TryGetNormalizedPointInWidget(
                m_panel,
                new Vector2(0.5f, 0.5f),
                widget,
                out normalizedWidgetPoint
            );

            Assert.IsTrue(result);
            Assert.AreEqual(new Vector2(0.5f, 0.5f), normalizedWidgetPoint, "Center point should remain at (0.5, 0.5) after rotation");
        }

        [Test]
        public void TryGetNormalizedLocalPointInWidget_ScaledWidget_ReturnsCorrectPoints()
        {
            var widget = CreateMockRiveWidget();
            m_panel.AddToHierarchy(widget);

            // Center widget and apply scale
            RivePanelTestUtils.PositionWidget(
                widget,
                new Vector2(0.25f, 0.25f), // anchorMin
                new Vector2(0.75f, 0.75f), // anchorMax
                new Vector2(0.5f, 0.5f),   // pivot
                new Vector2(500, 500),     // size
                Vector2.zero              // position
            );

            widget.RectTransform.localScale = new Vector3(0.5f, 0.5f, 1f);

            Vector2 normalizedWidgetPoint;

            // Test center point
            bool result = PanelRaycaster.TryGetNormalizedPointInWidget(
                m_panel,
                new Vector2(0.5f, 0.5f),
                widget,
                out normalizedWidgetPoint
            );

            Assert.IsTrue(result);
            Assert.AreEqual(new Vector2(0.5f, 0.5f), normalizedWidgetPoint, "Center point should remain at (0.5, 0.5) after scaling");
        }

        [Test]
        public void TryGetNormalizedLocalPointInWidget_NestedWidget_ReturnsCorrectPoints()
        {
            var parentWidget = CreateMockRiveWidget("ParentWidget");
            var childWidget = CreateMockRiveWidget("ChildWidget");
            m_panel.AddToHierarchy(parentWidget);

            // Setup parent widget in center of panel
            RivePanelTestUtils.PositionWidget(
                parentWidget,
                new Vector2(0.25f, 0.25f), // anchorMin
                new Vector2(0.75f, 0.75f), // anchorMax
                new Vector2(0.5f, 0.5f),   // pivot
                new Vector2(500, 500),     // size
                Vector2.zero              // position
            );

            // Setup child widget in corner of parent
            childWidget.transform.SetParent(parentWidget.transform);
            RivePanelTestUtils.PositionWidget(
                childWidget,
                new Vector2(0, 0),       // anchorMin
                new Vector2(0, 0),       // anchorMax
                new Vector2(0, 0),       // pivot
                new Vector2(100, 100),   // size
                Vector2.zero            // position
            );

            Vector2 normalizedWidgetPoint;

            // Test point in child widget
            bool result = PanelRaycaster.TryGetNormalizedPointInWidget(
                m_panel,
                new Vector2(0.25f, 0.25f), // Bottom-left corner of parent widget
                childWidget,
                out normalizedWidgetPoint
            );

            Assert.IsTrue(result);
            Assert.AreEqual(new Vector2(0f, 0f), normalizedWidgetPoint, "Point should be at (0, 0) in child widget space");
        }

        private MockRiveWidget CreateMockRiveWidget(string name = "TestWidget")
        {
            return RivePanelTestUtils.CreateWidget<MockRiveWidget>(name);
        }
    }
}