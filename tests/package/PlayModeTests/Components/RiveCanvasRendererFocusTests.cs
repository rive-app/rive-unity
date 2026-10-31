using System.Collections;
using NUnit.Framework;
using Rive.Components;
using Rive.Tests.Utils;
using Rive.Utils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rive.Tests
{
    public class RiveCanvasRendererFocusTests
    {
        private Canvas m_canvas;
        private RivePanel m_panel;
        private RiveCanvasRenderer m_renderer;
        private GameObject m_eventSystemObject;
        private MockLogger m_mockLogger;
        private PanelFocusController FocusController => m_panel.FocusController;

        [SetUp]
        public void Setup()
        {
            m_mockLogger = new MockLogger();
            DebugLogger.Instance = m_mockLogger;

            var canvasObj = new GameObject("Canvas");
            m_canvas = canvasObj.AddComponent<Canvas>();
            canvasObj.AddComponent<GraphicRaycaster>();
            m_canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            m_eventSystemObject = new GameObject("EventSystem");
            m_eventSystemObject.AddComponent<EventSystem>();
            m_eventSystemObject.AddComponent<StandaloneInputModule>();

            m_panel = RivePanelTestUtils.CreatePanel("FocusCanvasPanel");
            m_panel.transform.SetParent(m_canvas.transform, false);
            m_panel.SetDimensions(new Vector2(200, 200));
            m_renderer = m_panel.gameObject.AddComponent<RiveCanvasRenderer>();
        }

        [TearDown]
        public void TearDown()
        {
            if (m_eventSystemObject != null)
            {
                Object.Destroy(m_eventSystemObject);
            }

            if (m_canvas != null)
            {
                Object.Destroy(m_canvas.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator Navigation_IsPreservedAndConfigurable()
        {
            yield return null;
            var provider = m_panel.GetComponent<CanvasFocusInputProvider>();
            Assert.IsNotNull(provider);

            var rightButton = CreateNeighborButton("Right", new Vector2(300, 0));
            var navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnRight = rightButton
            };
            m_renderer.Navigation = navigation;

            Assert.AreEqual(Navigation.Mode.Explicit, provider.navigation.mode);
            Assert.AreEqual(rightButton, provider.navigation.selectOnRight);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ConsumedRiveMove_DoesNotSelectNeighbor()
        {
            var widget = CreateEligibleWidget();
            widget.MoveResults[FocusNavigationDirection.Right] = true;
            var rightButton = CreateNeighborButton("Right", new Vector2(300, 0));
            m_renderer.Navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnRight = rightButton
            };

            yield return null;
            var provider = m_panel.GetComponent<CanvasFocusInputProvider>();
            m_panel.Tick(0f);
            EventSystem.current.SetSelectedGameObject(provider.gameObject);
            FocusController.NotifyHostFocusGained(FocusEntryReason.Navigation, FocusNavigationDirection.Next);
            yield return null;

            var axis = new AxisEventData(EventSystem.current)
            {
                moveDir = MoveDirection.Right,
                moveVector = Vector2.right
            };
            ExecuteEvents.Execute(provider.gameObject, axis, ExecuteEvents.moveHandler);

            Assert.AreEqual(provider.gameObject, EventSystem.current.currentSelectedGameObject);
            Assert.AreEqual(FocusNavigationDirection.Right, widget.LastMove);
        }

        [UnityTest]
        public IEnumerator ExhaustedMove_SelectsConfiguredNeighbor()
        {
            var widget = CreateEligibleWidget();
            widget.MoveResults[FocusNavigationDirection.Right] = false;
            var rightButton = CreateNeighborButton("Right", new Vector2(300, 0));
            m_renderer.Navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnRight = rightButton
            };

            yield return null;
            var provider = m_panel.GetComponent<CanvasFocusInputProvider>();
            m_panel.Tick(0f);
            EventSystem.current.SetSelectedGameObject(provider.gameObject);
            FocusController.NotifyHostFocusGained(FocusEntryReason.Navigation, FocusNavigationDirection.Next);
            yield return null;

            var axis = new AxisEventData(EventSystem.current)
            {
                moveDir = MoveDirection.Right,
                moveVector = Vector2.right
            };
            ExecuteEvents.Execute(provider.gameObject, axis, ExecuteEvents.moveHandler);

            Assert.AreEqual(rightButton.gameObject, EventSystem.current.currentSelectedGameObject);
            Assert.IsFalse(widget.HasFocus);
            Assert.AreEqual(PanelFocusSessionState.Unfocused, FocusController.SessionState);
        }

        [UnityTest]
        public IEnumerator NoNeighbor_LeavesRiveElementFocused()
        {
            var widget = CreateEligibleWidget();
            widget.MoveResults[FocusNavigationDirection.Right] = false;
            m_renderer.Navigation = new Navigation { mode = Navigation.Mode.Explicit };

            yield return null;
            var provider = m_panel.GetComponent<CanvasFocusInputProvider>();
            m_panel.Tick(0f);
            EventSystem.current.SetSelectedGameObject(provider.gameObject);
            FocusController.NotifyHostFocusGained(FocusEntryReason.Navigation, FocusNavigationDirection.Next);
            yield return null;

            var axis = new AxisEventData(EventSystem.current)
            {
                moveDir = MoveDirection.Right,
                moveVector = Vector2.right
            };
            ExecuteEvents.Execute(provider.gameObject, axis, ExecuteEvents.moveHandler);

            Assert.AreEqual(provider.gameObject, EventSystem.current.currentSelectedGameObject);
            Assert.AreSame(widget, FocusController.ActiveWidget);
            Assert.IsTrue(widget.HasFocus);
        }

        [UnityTest]
        public IEnumerator PointerSelect_EntersPendingWithoutSeeding()
        {
            var widget = CreateEligibleWidget();
            yield return null;
            var provider = m_panel.GetComponent<CanvasFocusInputProvider>();
            m_panel.Tick(0f);

            var pointer = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute(provider.gameObject, pointer, ExecuteEvents.selectHandler);

            Assert.AreEqual(PanelFocusSessionState.EntryPending, FocusController.SessionState);
            Assert.AreEqual(0, widget.SeedCount);
        }

        [UnityTest]
        public IEnumerator Deselect_ClearsRiveFocus()
        {
            var widget = CreateEligibleWidget();
            yield return null;
            var provider = m_panel.GetComponent<CanvasFocusInputProvider>();
            m_panel.Tick(0f);
            EventSystem.current.SetSelectedGameObject(provider.gameObject);
            FocusController.NotifyHostFocusGained(FocusEntryReason.Navigation, FocusNavigationDirection.Next);
            Assert.IsTrue(widget.HasFocus);

            EventSystem.current.SetSelectedGameObject(null);
            yield return null;

            Assert.IsFalse(widget.HasFocus);
            Assert.AreEqual(PanelFocusSessionState.Unfocused, FocusController.SessionState);
        }

        [UnityTest]
        public IEnumerator MoveLeftFromRightNeighbor_EntersRightHandWidget()
        {
            m_panel.SetDimensions(new Vector2(400, 200));
            var left = CreatePositionedWidget("Left", new Vector2(-100, 0), new Vector2(200, 180));
            var right = CreatePositionedWidget("Right", new Vector2(102, 0), new Vector2(204, 180));
            yield return null;
            Canvas.ForceUpdateCanvases();

            var provider = m_panel.GetComponent<CanvasFocusInputProvider>();
            var rightButton = CreateNeighborButton("RightButton", new Vector2(280, 0));
            m_renderer.Navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnRight = rightButton
            };
            rightButton.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnLeft = provider
            };

            m_panel.Tick(0f);
            EventSystem.current.SetSelectedGameObject(rightButton.gameObject);
            yield return null;

            var axis = new AxisEventData(EventSystem.current)
            {
                moveDir = MoveDirection.Left,
                moveVector = Vector2.left
            };
            ExecuteEvents.Execute(rightButton.gameObject, axis, ExecuteEvents.moveHandler);

            Assert.AreEqual(provider.gameObject, EventSystem.current.currentSelectedGameObject);
            Assert.AreSame(
                right,
                FocusController.ActiveWidget,
                "Left from the uGUI button on the right should enter the right-hand widget, not the first widget in hierarchy.");
            Assert.AreEqual(0, left.SeedCount);
        }

        [UnityTest]
        public IEnumerator DisableFocus_DoesNotDisablePointerInput()
        {
            var widget = CreateEligibleWidget();
            yield return null;
            var provider = m_panel.GetComponent<CanvasFocusInputProvider>();
            m_panel.Tick(0f);
            Assert.IsTrue(provider.interactable);

            m_renderer.FocusBehavior = FocusBehavior.DisableFocus;
            Assert.IsFalse(provider.interactable);
            Assert.AreEqual(PointerInputMode.EnablePointerInput, m_renderer.PointerInputMode);
            Assert.IsTrue(m_renderer.DisplayImage.raycastTarget);
        }

        private MockFocusWidget CreateEligibleWidget()
        {
            var widget = RivePanelTestUtils.CreateWidget<MockFocusWidget>();
            widget.MockEligible = true;
            widget.SeedSucceeds = true;
            m_panel.AddToHierarchy(widget);
            RivePanelTestUtils.MakeWidgetFillPanel(widget);
            return widget;
        }

        private MockFocusWidget CreatePositionedWidget(string name, Vector2 anchoredPosition, Vector2 size)
        {
            var widget = RivePanelTestUtils.CreateWidget<MockFocusWidget>(name);
            widget.MockEligible = true;
            widget.SeedSucceeds = true;
            m_panel.AddToHierarchy(widget);
            RivePanelTestUtils.PositionWidget(widget, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), size, anchoredPosition);
            return widget;
        }

        private Selectable CreateNeighborButton(string name, Vector2 anchoredPosition)
        {
            var buttonObj = new GameObject(name);
            buttonObj.transform.SetParent(m_canvas.transform, false);
            var rect = buttonObj.AddComponent<RectTransform>();
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(80, 40);
            buttonObj.AddComponent<Image>();
            return buttonObj.AddComponent<Button>();
        }
    }
}
