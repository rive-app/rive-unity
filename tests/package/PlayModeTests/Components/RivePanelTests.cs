using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Rive.Components;
using System.Linq;
using System.Collections.Generic;
using System;
using Rive.Utils;
using Rive.Tests.Utils;

namespace Rive.Tests
{
    public class RivePanelTests
    {
        private GameObject m_panelObject;
        private RivePanel m_panel;
        private MockRenderTargetStrategy m_mockStrategy;
        private MockInputProvider m_mockInputProvider;
        MockLogger mockLogger;

        private MockRiveWidget CreateMockRiveWidget(string name = "TestWidget")
        {
            var widgetObj = new GameObject(name);
            return widgetObj.AddComponent<MockRiveWidget>();
        }


        [SetUp]
        public void Setup()
        {
            mockLogger = new MockLogger();
            DebugLogger.Instance = mockLogger;

            m_panel = RivePanelTestUtils.CreatePanel("TestPanel");
            m_panelObject = m_panel.gameObject;

            m_mockStrategy = m_panelObject.AddComponent<MockRenderTargetStrategy>();
            m_mockInputProvider = new MockInputProvider();
            m_panel.RenderTargetStrategy = m_mockStrategy;
            m_panel.RegisterInputProvider(m_mockInputProvider);

        }

        [TearDown]
        public void TearDown()
        {
            if (m_panelObject != null)
            {
                DestroyObj(m_panelObject);
            }
        }

        private void DestroyObj(UnityEngine.Object obj)
        {
            if (obj != null)
            {
                UnityEngine.Object.Destroy(obj);

            }
        }

        [UnityTest]
        public IEnumerator RivePanel_UsesDefautRenderTargetStrategy_IfNoneSet()
        {
            DestroyObj(m_mockStrategy);
            DestroyObj(m_panelObject);

            m_panel = RivePanelTestUtils.CreatePanel("TestPanel");
            m_panelObject = m_panel.gameObject;


            yield return null;

            Assert.IsNotNull(m_panel.RenderTargetStrategy, "Panel should have a default RenderTargetStrategy");

            Assert.IsInstanceOf<SimpleRenderTargetStrategy>(m_panel.RenderTargetStrategy, $"Default RenderTargetStrategy should be of type {nameof(SimpleRenderTargetStrategy)}");
        }

        [UnityTest]
        public IEnumerator MultitouchMode_Disabled_CollapsesIds_ToZero()
        {
            // Arrange panel with default input provider
            var panel = RivePanelTestUtils.CreatePanel("MTPanel");
            var mockWidget = RivePanelTestUtils.CreateWidget<MockRiveWidget>();
            panel.AddToHierarchy(mockWidget);
            RivePanelTestUtils.MakeWidgetFillPanel(mockWidget);

            // Explicitly set Disabled to ensure legacy behavior
            panel.MultiTouch = RivePanel.MultiTouchSupport.Disabled;

            // Simulate two different pointer ids via local provider
            var provider = new MockInputProvider();
            panel.RegisterInputProvider(provider);
            yield return null;

            Vector2 center = new Vector2(0.5f, 0.5f);
            provider.SimulatePointerDown(center, 123);
            provider.SimulatePointerDown(center, 456);
            yield return null;

            // We expect both calls to reach the widget but with id collapsed to 0
            Assert.AreEqual(2, mockWidget.PointerDownCalledCount);
            Assert.AreEqual(2, mockWidget.PointerDownIds.Count);
            Assert.IsTrue(mockWidget.PointerDownIds.TrueForAll(id => id == 0));

            panel.UnregisterInputProvider(provider);
        }

        [UnityTest]
        public IEnumerator MultitouchMode_Enabled_PreservesDistinctIds()
        {
            var panel = RivePanelTestUtils.CreatePanel("MTPanelEnabled");
            var mockWidget = RivePanelTestUtils.CreateWidget<MockRiveWidget>();
            panel.AddToHierarchy(mockWidget);
            RivePanelTestUtils.MakeWidgetFillPanel(mockWidget);

            panel.MultiTouch = RivePanel.MultiTouchSupport.Enabled;

            var provider = new MockInputProvider();
            panel.RegisterInputProvider(provider);
            yield return null;

            Vector2 center = new Vector2(0.5f, 0.5f);
            provider.SimulatePointerDown(center, 123);
            provider.SimulatePointerDown(center, 456);
            yield return null;

            Assert.AreEqual(2, mockWidget.PointerDownCalledCount);
            CollectionAssert.AreEquivalent(new[] { 123, 456 }, mockWidget.PointerDownIds);

            panel.UnregisterInputProvider(provider);
        }


        [Test]
        public void ChangingRenderTargetStrategy_HandlesRegistrationCorrectly()
        {
            m_panel.StopRendering();

            var newMockStrategy = m_panelObject.AddComponent<MockRenderTargetStrategy>();

            // Test when panel is not registered with original strategy
            Assert.IsFalse(m_mockStrategy.IsRegistered(m_panel), "Panel should not be registered initially");

            m_panel.RenderTargetStrategy = newMockStrategy;

            Assert.IsFalse(newMockStrategy.IsRegistered(m_panel),
                "Panel should not register with new strategy if it wasn't registered with previous one");

            // Test when panel is registered with original strategy
            m_panel.RenderTargetStrategy = m_mockStrategy;
            m_panel.StartRendering();

            Assert.IsTrue(m_mockStrategy.IsRegistered(m_panel),
                "Panel should be registered with original strategy after StartRendering");

            m_panel.RenderTargetStrategy = newMockStrategy;

            Assert.IsFalse(m_mockStrategy.IsRegistered(m_panel),
                "Panel should be unregistered from original strategy");
            Assert.IsTrue(newMockStrategy.IsRegistered(m_panel),
                "Panel should be registered with new strategy");
        }

        /// <summary>
        /// When a widget GameObject is added to the panel without using the AddToHierarchy helper method, it should still be registered with the panel. This is important for cases where the widgets are added in the editor.
        /// </summary>
        [UnityTest]
        public IEnumerator AddWidgetGameObjectToPanel_RegistersWidget()
        {

            Assert.AreEqual(0, m_panel.Widgets.Count);

            var widgetObj = CreateMockRiveWidget();
            widgetObj.transform.SetParent(m_panel.transform);

            yield return null;

            Assert.IsTrue(m_panel.Widgets.Contains(widgetObj));
            Assert.AreEqual(1, m_panel.Widgets.Count);
        }

        /// <summary>
        /// When a widget GameObject is added to the panel using the AddToHierarchy helper method, it should be registered with the panel.
        /// </summary>
        [UnityTest]
        public IEnumerator AddToHierarchy_RegistersWidget()
        {
            Assert.AreEqual(0, m_panel.Widgets.Count);

            var widget = CreateMockRiveWidget();
            m_panel.AddToHierarchy(widget);

            yield return null;

            Assert.IsTrue(widget.transform.IsChildOf(m_panel.WidgetContainer));
            Assert.IsTrue(m_panel.ContainsWidget(widget));
            Assert.AreEqual(1, m_panel.Widgets.Count);
        }

        /// <summary>
        /// When a widget GameObject is removed from the panel without using the RemoveFromHierarchy helper method, it should still be unregistered from the panel.
        /// </summary>
        /// <returns></returns>
        [UnityTest]
        public IEnumerator RemoveWidgetGameObjectFromPanel_UnregistersWidget()
        {
            var widgetObj = CreateMockRiveWidget();
            widgetObj.transform.SetParent(m_panel.transform);

            Assert.IsTrue(m_panel.ContainsWidget(widgetObj));
            Assert.AreEqual(1, m_panel.Widgets.Count);

            widgetObj.transform.SetParent(null);
            yield return null;

            Assert.IsFalse(m_panel.Widgets.Contains(widgetObj));
            Assert.AreEqual(0, m_panel.Widgets.Count);
        }

        /// <summary>
        /// When a widget GameObject is destroyed, it should be unregistered from the panel.
        /// </summary>
        [UnityTest]
        public IEnumerator DestroyExistingWidgetGameObjectFromPanel_UnregistersWidget()
        {
            var widgetObj = CreateMockRiveWidget();
            widgetObj.transform.SetParent(m_panel.transform);

            Assert.IsTrue(m_panel.ContainsWidget(widgetObj));
            Assert.AreEqual(1, m_panel.Widgets.Count);

            DestroyObj(widgetObj);
            yield return null;

            Assert.IsFalse(m_panel.Widgets.Contains(widgetObj));
            Assert.AreEqual(0, m_panel.Widgets.Count);
        }

        /// <summary>
        /// When a widget GameObject is removed from the panel using the RemoveFromHierarchy helper method, it should be unregistered from the panel.
        /// </summary>
        [Test]
        public void RemoveFromHierarchy_ExistingWidget_RemovesFromPanel()
        {
            var widgetObj = CreateMockRiveWidget();
            m_panel.AddToHierarchy(widgetObj);

            Assert.IsTrue(m_panel.ContainsWidget(widgetObj));

            m_panel.RemoveFromHierarchy(widgetObj);

            Assert.IsFalse(m_panel.ContainsWidget(widgetObj));

        }

        [UnityTest]
        public IEnumerator BringToFront_ValidWidget_UpdatesOrder()
        {
            var widget1 = CreateMockRiveWidget("Widget1");
            var widget2 = CreateMockRiveWidget("Widget2");
            m_panel.AddToHierarchy(widget1);
            m_panel.AddToHierarchy(widget2);

            Assert.AreEqual(widget1, m_panel.Widgets.First());
            Assert.AreEqual(widget2, m_panel.Widgets.Last());

            widget1.transform.SetAsLastSibling();

            yield return null;

            Assert.AreEqual(widget1, m_panel.Widgets.Last());
            Assert.AreEqual(widget2, m_panel.Widgets.First());
        }

        [UnityTest]
        public IEnumerator SendToBack_ValidWidget_UpdatesOrder()
        {
            var widget1 = CreateMockRiveWidget("Widget1");
            var widget2 = CreateMockRiveWidget("Widget2");
            m_panel.AddToHierarchy(widget1);
            m_panel.AddToHierarchy(widget2);

            Assert.AreEqual(widget1, m_panel.Widgets.First());
            Assert.AreEqual(widget2, m_panel.Widgets.Last());

            widget2.transform.SetAsFirstSibling();

            yield return null;

            Assert.AreEqual(widget1, m_panel.Widgets.Last());
            Assert.AreEqual(widget2, m_panel.Widgets.First());
        }

        [UnityTest]
        public IEnumerator WidgetOrdering_WithNestedHierarchy_MaintainsCorrectOrder()
        {
            var parentWidget1 = CreateMockRiveWidget("ParentWidget1");
            var childWidget1 = CreateMockRiveWidget("ChildWidget1");
            var parentWidget2 = CreateMockRiveWidget("ParentWidget2");
            var childWidget2 = CreateMockRiveWidget("ChildWidget2");

            childWidget1.transform.SetParent(parentWidget1.transform);
            childWidget2.transform.SetParent(parentWidget2.transform);

            m_panel.AddToHierarchy(parentWidget1);
            m_panel.AddToHierarchy(parentWidget2);

            parentWidget1.transform.SetSiblingIndex(0);

            yield return null;

            // Initial order should be: parentWidget1 -> childWidget1 -> parentWidget2 -> childWidget2
            var widgets = m_panel.Widgets;
            Assert.AreEqual(4, widgets.Count);
            Assert.AreEqual(parentWidget1, widgets[0]);
            Assert.AreEqual(childWidget1, widgets[1]);
            Assert.AreEqual(parentWidget2, widgets[2]);
            Assert.AreEqual(childWidget2, widgets[3]);


            parentWidget1.transform.SetSiblingIndex(m_panel.WidgetContainer.childCount - 1);
            yield return null;

            // Order should now be: parentWidget2 -> childWidget2 -> parentWidget1 -> childWidget1
            widgets = m_panel.Widgets;

            Assert.AreEqual(parentWidget2, widgets[0]);
            Assert.AreEqual(childWidget2, widgets[1]);
            Assert.AreEqual(parentWidget1, widgets[2]);
            Assert.AreEqual(childWidget1, widgets[3]);


            parentWidget1.transform.SetSiblingIndex(0);

            yield return null;


            // Order should return to: parentWidget1 -> childWidget1 -> parentWidget2 -> childWidget2
            widgets = m_panel.Widgets;
            Assert.AreEqual(parentWidget1, widgets[0]);
            Assert.AreEqual(childWidget1, widgets[1]);
            Assert.AreEqual(parentWidget2, widgets[2]);
            Assert.AreEqual(childWidget2, widgets[3]);

            // Test bringing child to front
            childWidget1.transform.SetParent(m_panel.WidgetContainer);
            childWidget1.transform.SetSiblingIndex(m_panel.WidgetContainer.childCount - 1);

            yield return null;

            // Order should be: parentWidget1 -> parentWidget2 -> childWidget2 -> childWidget1
            widgets = m_panel.Widgets;
            Assert.AreEqual(parentWidget1, widgets[0]);
            Assert.AreEqual(parentWidget2, widgets[1]);
            Assert.AreEqual(childWidget2, widgets[2]);
            Assert.AreEqual(childWidget1, widgets[3]);
        }

        [UnityTest]
        public IEnumerator WidgetOrdering_WithNonRiveWidgetParents_MaintainsCorrectOrder()
        {
            var parentObj1 = new GameObject("ParentObj1", typeof(RectTransform));
            var parentObj2 = new GameObject("ParentObj2", typeof(RectTransform));

            var childWidget1 = CreateMockRiveWidget("ChildWidget1");
            var childWidget2 = CreateMockRiveWidget("ChildWidget2");

            parentObj1.transform.SetParent(m_panel.WidgetContainer);
            parentObj2.transform.SetParent(m_panel.WidgetContainer);
            childWidget1.transform.SetParent(parentObj1.transform);
            childWidget2.transform.SetParent(parentObj2.transform);

            parentObj1.transform.SetSiblingIndex(0);
            parentObj2.transform.SetSiblingIndex(1);

            yield return null;

            // Initial order should be: childWidget1 -> childWidget2
            var widgets = m_panel.Widgets;
            Assert.AreEqual(2, widgets.Count);
            Assert.AreEqual(childWidget1, widgets[0]);
            Assert.AreEqual(childWidget2, widgets[1]);

            // Move parentObj1 to front
            parentObj1.transform.SetSiblingIndex(m_panel.WidgetContainer.childCount - 1);
            yield return null;

            // Order should now be: childWidget2 -> childWidget1
            widgets = m_panel.Widgets;
            Assert.AreEqual(childWidget2, widgets[0]);
            Assert.AreEqual(childWidget1, widgets[1]);

            // Create a deeper hierarchy with mixed RiveWidget and non-RiveWidget parents
            var middleObj = new GameObject("MiddleObj", typeof(RectTransform));
            var childWidget3 = CreateMockRiveWidget("ChildWidget3");

            middleObj.transform.SetParent(parentObj2.transform);
            childWidget3.transform.SetParent(middleObj.transform);

            yield return null;

            // Order should now be: childWidget2 -> childWidget3 -> childWidget1
            widgets = m_panel.Widgets;
            Assert.AreEqual(3, widgets.Count);
            Assert.AreEqual(childWidget2, widgets[0]);
            Assert.AreEqual(childWidget3, widgets[1]);
            Assert.AreEqual(childWidget1, widgets[2]);

            DestroyObj(parentObj1);
            DestroyObj(parentObj2);
        }

        [Test]
        public void WidgetOrdering_NestedHierarchyRemoval_HandlesOrderCorrectly()
        {
            var parentWidget = CreateMockRiveWidget("ParentWidget");
            var childWidget1 = CreateMockRiveWidget("ChildWidget1");
            var childWidget2 = CreateMockRiveWidget("ChildWidget2");

            childWidget1.transform.SetParent(parentWidget.transform);
            childWidget2.transform.SetParent(parentWidget.transform);

            m_panel.AddToHierarchy(parentWidget);

            // Verify initial order
            var widgets = m_panel.Widgets.ToList();
            Assert.AreEqual(3, widgets.Count);
            Assert.AreEqual(parentWidget, widgets[0]);
            Assert.AreEqual(childWidget1, widgets[1]);
            Assert.AreEqual(childWidget2, widgets[2]);

            m_panel.RemoveFromHierarchy(parentWidget);

            // Verify all widgets are removed
            Assert.AreEqual(0, m_panel.Widgets.Count());
            Assert.IsFalse(m_panel.ContainsWidget(parentWidget));
            Assert.IsFalse(m_panel.ContainsWidget(childWidget1));
            Assert.IsFalse(m_panel.ContainsWidget(childWidget2));
        }

        // RENDERING TESTS
        [UnityTest]
        public IEnumerator StartRendering_EnabledPanel_RegistersWithStrategy()
        {
            m_panel.StopRendering();
            Assert.IsFalse(m_panel.IsRendering);
            yield return null;
            m_panel.StartRendering();
            Assert.IsTrue(m_panel.IsRendering);


        }

        [UnityTest]
        public IEnumerator StopRendering_RenderingPanel_UnregistersFromStrategy()
        {
            m_panel.StartRendering();
            Assert.IsTrue(m_panel.IsRendering);
            yield return null;

            bool result = m_panel.StopRendering();

            Assert.IsTrue(result);
            Assert.IsFalse(m_panel.IsRendering);
        }

        [UnityTest]
        public IEnumerator DisablingPanelGameObject_StopsRendering()
        {
            m_panel.StartRendering();
            Assert.IsTrue(m_panel.IsRendering);

            m_panelObject.SetActive(false);
            yield return null;


            Assert.IsFalse(m_panel.IsRendering);
        }

        [UnityTest]
        public IEnumerator ReEnablingPanelGameObject_StartsRendering()
        {
            m_panel.StartRendering();
            yield return null;
            Assert.IsTrue(m_panel.IsRendering);

            m_panelObject.SetActive(false);

            yield return null;

            Assert.IsFalse(m_panel.IsRendering);


            m_panelObject.SetActive(true);

            yield return null;

            Assert.IsTrue(m_panel.IsRendering);
        }


        [Test]
        public void SetDimensions_ValidDimensions_UpdatesRectTransform()
        {
            Vector2 newDimensions = new Vector2(100, 200);

            m_panel.SetDimensions(newDimensions);

            var rect = m_panel.WidgetContainer.rect;
            Assert.AreEqual(newDimensions.x, rect.width);
            Assert.AreEqual(newDimensions.y, rect.height);

            Assert.AreEqual(mockLogger.LoggedErrors.Count, 0);
            Assert.AreEqual(mockLogger.LoggedWarnings.Count, 0);
        }

        [Test]
        public void SetDimensions_InvalidDimensions_DoesNotUpdateRectTransform()
        {
            Vector2 originalDimensions = new Vector2(1920, 1080);
            m_panel.SetDimensions(originalDimensions);

            Assert.AreEqual(originalDimensions.x, m_panel.WidgetContainer.rect.width);

            // Negative dimensions should not be allowed.
            Vector2 newDimensions = new Vector2(-100, -200);

            var rect = m_panel.WidgetContainer.rect;

            m_panel.SetDimensions(newDimensions);

            Assert.AreEqual(rect.width, originalDimensions.x);
            Assert.AreEqual(rect.height, originalDimensions.y);

            // Zero dimensions should not be allowed.
            newDimensions = Vector2.zero;

            m_panel.SetDimensions(newDimensions);

            Assert.AreEqual(rect.width, originalDimensions.x);
            Assert.AreEqual(rect.height, originalDimensions.y);

            Assert.IsTrue(mockLogger.LoggedErrors.Count > 0 || mockLogger.LoggedWarnings.Count > 0);
        }


        // INPUTS

        [UnityTest]
        public IEnumerator InputProvider_PointerDown_TriggersWidgetEvents()
        {
            var widget = CreateMockRiveWidget("TestWidget");
            m_panel.AddToHierarchy(widget);

            m_panel.SetDimensions(new Vector2(1920, 1080));
            var mockWidget = widget;
            mockWidget.HitTestBehavior = HitTestBehavior.Opaque;

            // Position the the widget in the center of the panel and make it fill the panel.
            widget.RectTransform.anchorMin = Vector2.zero;
            widget.RectTransform.anchorMax = Vector2.one;
            widget.RectTransform.sizeDelta = Vector2.zero;
            widget.RectTransform.anchoredPosition = Vector2.zero;





            Vector2 testPoint = new Vector2(0.5f, 0.5f);

            Assert.AreEqual(mockWidget.PointerDownCalledCount, 0);
            Assert.AreEqual(mockWidget.PointerUpCalledCount, 0);
            Assert.AreEqual(mockWidget.PointerMoveCalledCount, 0);


            m_mockInputProvider.SimulatePointerDown(testPoint);
            m_mockInputProvider.SimulatePointerUp(testPoint);
            m_mockInputProvider.SimulatePointerMove(testPoint);

            int expectedCount = 1;

            Assert.AreEqual(mockWidget.PointerDownCalledCount, expectedCount);
            Assert.AreEqual(mockWidget.PointerUpCalledCount, expectedCount);
            Assert.AreEqual(mockWidget.PointerMoveCalledCount, expectedCount);



            // Position the widget to the top left corner of the panel. And make the widget size 400x400.
            widget.RectTransform.anchorMin = new Vector2(0, 1);
            widget.RectTransform.anchorMax = new Vector2(0, 1);
            widget.RectTransform.pivot = new Vector2(0, 1);
            widget.RectTransform.anchoredPosition = Vector2.zero;
            widget.RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 400);
            widget.RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 400);

            yield return null;

            // Now try to simulate pointer events at the center of the panel.
            testPoint = new Vector2(0.5f, 0.5f);

            m_mockInputProvider.SimulatePointerDown(testPoint);
            m_mockInputProvider.SimulatePointerUp(testPoint);
            m_mockInputProvider.SimulatePointerMove(testPoint);

            // The widget should still receive pointer events even if the test point is outside the widget's bounds to account for events that happen outside the widget like OnPointerExit
            Assert.AreEqual(mockWidget.PointerDownCalledCount, 2);
            Assert.AreEqual(mockWidget.PointerUpCalledCount, 2);
            Assert.AreEqual(mockWidget.PointerMoveCalledCount, 2);




            // Now let's make sure the test point is now inside the widget's bounds.
            // We don't make the test points exactly on the edge because Unity considers points that lie exactly on the right or top edges as being outside the rectangle.
            testPoint = new Vector2(0.05f, 0.95f);
            m_mockInputProvider.SimulatePointerDown(testPoint);
            m_mockInputProvider.SimulatePointerUp(testPoint);
            m_mockInputProvider.SimulatePointerMove(testPoint);

            yield return null;

            // The widget should still receive the pointer events.
            expectedCount = 3;

            Assert.AreEqual(mockWidget.PointerDownCalledCount, expectedCount);
            Assert.AreEqual(mockWidget.PointerUpCalledCount, expectedCount);
            Assert.AreEqual(mockWidget.PointerMoveCalledCount, expectedCount);


        }

        [UnityTest]
        public IEnumerator WidgetHitTestBehavior_AffectsPointerEvents()
        {

            m_panel.SetDimensions(new Vector2(1920, 1080));

            // Create two overlapping widgets
            var frontWidget = CreateMockRiveWidget("FrontWidget");
            var backWidget = CreateMockRiveWidget("BackWidget");

            m_panel.AddToHierarchy(backWidget);
            m_panel.AddToHierarchy(frontWidget);

            backWidget.transform.SetSiblingIndex(0);

            // Position both widgets at the same location
            Vector2 position = new Vector2(0.5f, 0.5f);
            foreach (var widget in new[] { frontWidget, backWidget })
            {
                widget.RectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                widget.RectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                widget.RectTransform.pivot = new Vector2(0.5f, 0.5f);
                widget.RectTransform.anchoredPosition = Vector2.zero;
                widget.RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 200);
                widget.RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 200);
            }

            yield return null;

            // Test 1: Front widget Opaque blocks back widget
            frontWidget.HitTestBehavior = HitTestBehavior.Opaque;
            backWidget.HitTestBehavior = HitTestBehavior.Opaque;

            m_mockInputProvider.SimulatePointerDown(position);
            m_mockInputProvider.SimulatePointerUp(position);
            m_mockInputProvider.SimulatePointerMove(position);

            Assert.AreEqual(1, frontWidget.PointerDownCalledCount, "Front widget should receive events when Opaque");
            Assert.AreEqual(0, backWidget.PointerDownCalledCount, "Back widget should be blocked when front is Opaque");

#pragma warning disable CS0618 // Transparent hit testing is deprecated but kept for backward compatibility
            // Test 2: Front widget Transparent allows back widget to receive events
            frontWidget.HitTestBehavior = HitTestBehavior.Transparent;
            backWidget.HitTestBehavior = HitTestBehavior.Opaque;

            m_mockInputProvider.SimulatePointerDown(position);
            m_mockInputProvider.SimulatePointerUp(position);
            m_mockInputProvider.SimulatePointerMove(position);

            Assert.AreEqual(2, frontWidget.PointerDownCalledCount, "Front widget should still receive events when Transparent");
#pragma warning restore CS0618
            Assert.AreEqual(1, backWidget.PointerDownCalledCount, "Back widget should receive events when front is Transparent");

            // Test 3: Front widget None receives no events
            frontWidget.HitTestBehavior = HitTestBehavior.None;
            backWidget.HitTestBehavior = HitTestBehavior.Opaque;

            m_mockInputProvider.SimulatePointerDown(position);
            m_mockInputProvider.SimulatePointerUp(position);
            m_mockInputProvider.SimulatePointerMove(position);

            Assert.AreEqual(2, frontWidget.PointerDownCalledCount, "Front widget should not receive events when None");
            Assert.AreEqual(2, backWidget.PointerDownCalledCount, "Back widget should receive events when front is None");

            // Test 4: Front widget Translucent only blocks if it has a hit
            frontWidget.ReturnTrueOnHitTest = false;

            frontWidget.HitTestBehavior = HitTestBehavior.Translucent;
            backWidget.HitTestBehavior = HitTestBehavior.Opaque;

            m_mockInputProvider.SimulatePointerDown(position);
            m_mockInputProvider.SimulatePointerUp(position);
            m_mockInputProvider.SimulatePointerMove(position);

            Assert.AreEqual(2, frontWidget.PointerDownCalledCount, "Front widget should not receive events when Translucent (no hit areas)");
            Assert.AreEqual(3, backWidget.PointerDownCalledCount, "Back widget should receive events when front is Translucent with no hit areas");

            // Test 5: Front widget Translucent blocks back widget when it has a hit
            frontWidget.ReturnTrueOnHitTest = true;

            m_mockInputProvider.SimulatePointerDown(position);
            m_mockInputProvider.SimulatePointerUp(position);
            m_mockInputProvider.SimulatePointerMove(position);

            Assert.AreEqual(3, frontWidget.PointerDownCalledCount, "Front widget should receive events when Translucent with hit");
            Assert.AreEqual(3, backWidget.PointerDownCalledCount, "Back widget should be blocked when front is Translucent with hit");

        }

        // EVENTS

        [Test]
        public void PanelEvents_WidgetAddedAndRemoved_InvokeCallbacks()
        {
            int widgetAddedCount = 0;
            int widgetRemovedCount = 0;
            IRiveWidget lastAddedWidget = null;
            IRiveWidget lastRemovedWidget = null;

            m_panel.OnWidgetAdded += (widget) =>
            {
                widgetAddedCount++;
                lastAddedWidget = widget;
            };
            m_panel.OnWidgetRemoved += (widget) =>
            {
                widgetRemovedCount++;
                lastRemovedWidget = widget;
            };

            var widget = CreateMockRiveWidget();
            m_panel.AddToHierarchy(widget);

            Assert.AreEqual(1, widgetAddedCount, "OnWidgetAdded should be called once");
            Assert.AreEqual(widget, lastAddedWidget, "OnWidgetAdded should pass the correct widget");
            Assert.AreEqual(0, widgetRemovedCount, "OnWidgetRemoved should not be called yet");

            m_panel.RemoveFromHierarchy(widget);

            Assert.AreEqual(1, widgetAddedCount, "OnWidgetAdded count should not change");
            Assert.AreEqual(1, widgetRemovedCount, "OnWidgetRemoved should be called once");
            Assert.AreEqual(widget, lastRemovedWidget, "OnWidgetRemoved should pass the correct widget");
        }

        [Test]
        public void PanelEvents_RenderingStateChanged_InvokesCallback()
        {
            m_panel.StopRendering();

            int renderingStateChangedCount = 0;
            m_panel.OnRenderingStateChanged += () => renderingStateChangedCount++;

            Assert.IsFalse(m_panel.IsRendering, "Panel should not be rendering by default");

            m_panel.StartRendering();
            Assert.AreEqual(1, renderingStateChangedCount, "OnRenderingStateChanged should be called when starting rendering");

            bool wasStopped = m_panel.StopRendering();
            Assert.IsTrue(wasStopped, "StopRendering should return true when panel is rendering");
            Assert.AreEqual(2, renderingStateChangedCount, "OnRenderingStateChanged should be called when stopping rendering");
        }

        [UnityTest]
        public IEnumerator UpdateMode_Auto_TicksWidgetsEveryFrame()
        {
            var widget = CreateMockRiveWidget();
            m_panel.AddToHierarchy(widget);
            m_panel.UpdateMode = RivePanel.PanelUpdateMode.Auto;

            // Initial frame
            yield return null;
            int initialTickCount = widget.TickCount;

            yield return new WaitForSeconds(0.1f); // Wait for multiple frames

            Assert.Greater(widget.TickCount, initialTickCount,
                "Widget should be ticked multiple times in Auto mode");
        }

        [UnityTest]
        public IEnumerator UpdateMode_Manual_OnlyTicksWhenRequested()
        {
            var widget = CreateMockRiveWidget();
            m_panel.AddToHierarchy(widget);
            m_panel.UpdateMode = RivePanel.PanelUpdateMode.Manual;

            // Initial frame
            yield return null;
            int initialTickCount = widget.TickCount;

            // Wait a few frames
            yield return new WaitForSeconds(0.1f);

            Assert.AreEqual(initialTickCount, widget.TickCount,
                "Widget should not be ticked automatically in Manual mode");

            // Manually tick the panel
            float deltaTime = 0.016f; // Simulate 60fps
            m_panel.Tick(deltaTime);

            Assert.AreEqual(initialTickCount + 1, widget.TickCount,
                "Widget should be ticked once after manual tick");
        }

    }

    public class MockRiveWidget : WidgetBehaviour
    {
        private HitTestBehavior m_hitTestBehavior = HitTestBehavior.Opaque;
        private IRenderObject m_renderObject;
        public int PointerDownCalledCount { get; private set; }
        public int PointerUpCalledCount { get; private set; }
        public int PointerMoveCalledCount { get; private set; }
        public List<int> PointerDownIds { get; } = new List<int>();
        public List<int> PointerUpIds { get; } = new List<int>();
        public List<int> PointerMoveIds { get; } = new List<int>();

        public override IRenderObject RenderObject => m_renderObject;

        public override HitTestBehavior HitTestBehavior { get => m_hitTestBehavior; set => m_hitTestBehavior = value; }

        public bool ReturnTrueOnHitTest { get; set; } = false;

        public int TickCount { get; private set; }

        public override bool Tick(float deltaTime)
        {
            TickCount++;
            return base.Tick(deltaTime);

        }
        protected override void OnEnable()
        {
            m_renderObject = new MockRenderObject();

            base.OnEnable();

        }

        public override bool OnPointerDown(Vector2 normalizedPoint, int pointerId)
        {
            PointerDownCalledCount++;
            PointerDownIds.Add(pointerId);

            return true;
        }

        public override bool OnPointerUp(Vector2 normalizedPoint, int pointerId)
        {
            PointerUpCalledCount++;
            PointerUpIds.Add(pointerId);

            return true;
        }

        public override bool OnPointerMove(Vector2 normalizedPoint, int pointerId)
        {
            PointerMoveCalledCount++;
            PointerMoveIds.Add(pointerId);

            return true;
        }

        override public bool HitTest(Vector2 normalizedPoint)
        {
            return ReturnTrueOnHitTest;
        }


    }

    public class MockRenderObject : IRenderObject
    {
        event Action OnDrawContentCalled;
        public RenderTransform RenderTransform { get; set; }

        public void DrawContent(IRenderer renderer, AABB frame, RenderContext renderContext)
        {
            OnDrawContentCalled?.Invoke();
        }



    }

    public class MockRenderTargetStrategy : RenderTargetStrategy
    {
        private bool m_isRegistered;
        private DrawTimingOption m_drawTiming;

        private RenderTargetSpaceOccupancy TargetSpaceOccupancy
        {
            get
            {


                return RenderTargetSpaceOccupancy.Exclusive;
            }
        }

        public override DrawTimingOption DrawTiming { get => m_drawTiming; set => m_drawTiming = value; }

        public override bool RegisterPanel(IRivePanel panel)
        {
            m_isRegistered = true;
            return true;
        }

        public override bool UnregisterPanel(IRivePanel panel)
        {
            m_isRegistered = false;
            return true;
        }

        public bool IsRegistered(IRivePanel panel) => m_isRegistered;

        public override void DrawPanel(IRivePanel panel) { }
        public override RenderTexture GetRenderTexture(IRivePanel panel) => null;
        public override Vector2 GetPanelOffset(IRivePanel panel) => Vector2.zero;
        public override Vector2 GetPanelScale(IRivePanel panel) => Vector2.one;
        public override bool IsPanelRegistered(IRivePanel panel) => m_isRegistered;

        protected override IEnumerable<Renderer> GetRenderers()
        {
            return Enumerable.Empty<Renderer>();
        }
    }

    public class MockInputProvider : IPanelInputProvider
    {
        public event System.Action<PanelPointerEvent> PointerPressed;
        public event System.Action<PanelPointerEvent> PointerReleased;
        public event System.Action<PanelPointerEvent> PointerMoved;
        public event Action<PanelPointerEvent> PointerExited;
        public event Action<PanelPointerEvent> PointerEntered;

        private static PanelPointerEvent MakeEvent(Vector2 point) => new PanelPointerEvent(point, 0);
        private static PanelPointerEvent MakeEvent(Vector2 point, int pointerId) => new PanelPointerEvent(point, pointerId);

        public void SimulatePointerDown(Vector2 point) => PointerPressed?.Invoke(MakeEvent(point));
        public void SimulatePointerUp(Vector2 point) => PointerReleased?.Invoke(MakeEvent(point));
        public void SimulatePointerMove(Vector2 point) => PointerMoved?.Invoke(MakeEvent(point));
        public void SimulatePointerExit(Vector2 point) => PointerExited?.Invoke(MakeEvent(point));
        public void SimulatePointerEnter(Vector2 point) => PointerEntered?.Invoke(MakeEvent(point));

        public void SimulatePointerDown(Vector2 point, int pointerId) => PointerPressed?.Invoke(MakeEvent(point, pointerId));
        public void SimulatePointerUp(Vector2 point, int pointerId) => PointerReleased?.Invoke(MakeEvent(point, pointerId));
        public void SimulatePointerMove(Vector2 point, int pointerId) => PointerMoved?.Invoke(MakeEvent(point, pointerId));
    }




    public static class RivePanelTestUtils
    {
        /// <summary>
        /// Creates a GameObject with a RiveWidget component
        /// </summary>
        public static T CreateWidget<T>(string name = "TestWidget") where T : WidgetBehaviour
        {
            var widgetObj = new GameObject(name);
            return widgetObj.AddComponent<T>();
        }

        /// <summary>
        /// Creates a GameObject with a RivePanel component and configurable dependencies
        /// </summary>
        public static RivePanel CreatePanel(
            string name = "TestPanel",
            RenderTargetStrategy renderStrategy = null,
            IPanelInputProvider inputProvider = null)
        {
            var panelObj = new GameObject(name);
            var panel = panelObj.AddComponent<RivePanel>();

            if (renderStrategy != null)
            {
                panel.RenderTargetStrategy = renderStrategy;
            }

            if (inputProvider != null)
            {
                panel.RegisterInputProvider(inputProvider);
            }

            return panel;
        }

        /// <summary>
        /// Configures a widget to fill its parent panel
        /// </summary>
        public static void MakeWidgetFillPanel(WidgetBehaviour widget)
        {
            var rectTransform = widget.RectTransform;
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.sizeDelta = Vector2.zero;
            rectTransform.anchoredPosition = Vector2.zero;
        }

        /// <summary>
        /// Sets up a widget at a specific position and size within a panel
        /// </summary>
        public static void PositionWidget(
            WidgetBehaviour widget,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 size,
            Vector2 position)
        {
            var rectTransform = widget.RectTransform;
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = pivot;
            rectTransform.anchoredPosition = position;
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
        }
    }

}