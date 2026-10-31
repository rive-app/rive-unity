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
    /// <summary>
    /// End-to-end focus tests for <c>focus_grid_test.riv</c> using the canvas path. Tests select, move, and click
    /// through uGUI the way a gamepad or pointer would.
    /// </summary>
    public class RiveWidgetFocusGridTests
    {
        private const float ArtboardWidth = 500f;
        private const float ArtboardHeight = 400f;

        private TestAssetLoadingManager m_testAssetLoadingManager;
        private MockLogger m_mockLogger;
        private Canvas m_canvas;
        private RivePanel m_panel;
        private RiveCanvasRenderer m_renderer;
        private RiveWidget m_widget;
        private GameObject m_eventSystemObject;
        private TestInputModule m_inputModule;
        private Selectable m_leftButton;
        private Selectable m_rightButton;

        private PanelFocusController FocusController => m_panel.FocusController;

        private CanvasFocusInputProvider FocusProvider =>
            m_panel != null ? m_panel.GetComponent<CanvasFocusInputProvider>() : null;

        [SetUp]
        public void Setup()
        {
            m_mockLogger = new MockLogger();
            DebugLogger.Instance = m_mockLogger;
            m_testAssetLoadingManager = new TestAssetLoadingManager();

            var canvasObj = new GameObject("FocusGridCanvas");
            m_canvas = canvasObj.AddComponent<Canvas>();
            m_canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<GraphicRaycaster>();

            m_eventSystemObject = new GameObject("EventSystem");
            m_eventSystemObject.AddComponent<EventSystem>();
            m_inputModule = m_eventSystemObject.AddComponent<TestInputModule>();

            m_panel = RivePanelTestUtils.CreatePanel("FocusGridPanel");
            m_panel.transform.SetParent(m_canvas.transform, false);
            m_panel.SetDimensions(new Vector2(ArtboardWidth, ArtboardHeight));
            m_renderer = m_panel.gameObject.AddComponent<RiveCanvasRenderer>();

            m_widget = RivePanelTestUtils.CreateWidget<RiveWidget>("FocusGridWidget");
            m_widget.Fit = Fit.Fill;
            m_panel.AddToHierarchy(m_widget);
            RivePanelTestUtils.MakeWidgetFillPanel(m_widget);

            m_leftButton = CreateUiButton("LeftButton", new Vector2(-400f, 0f));
            m_rightButton = CreateUiButton("RightButton", new Vector2(400f, 0f));
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

            m_testAssetLoadingManager?.UnloadAllAssets();
        }

        [UnityTest]
        public IEnumerator LoadedGrid_CanBeSelectedByEventSystem()
        {
            yield return LoadGrid();

            Assert.IsTrue(FocusController.HasFocusableContent);
            Assert.IsNotNull(FocusProvider);
            Assert.IsTrue(FocusProvider.interactable);
            Assert.IsTrue(((IRiveFocusWidget)m_widget).IsEligibleForFocus);
            Assert.AreSame(FocusProvider, FocusController.FocusProvider);
        }

        [UnityTest]
        public IEnumerator MovingRightFromLeftButton_FocusesTopLeftThenWalksTheGrid()
        {
            yield return LoadGrid();
            LinkPanelToNeighborButtons();

            yield return MoveFromUiButton(m_leftButton, MoveDirection.Right);

            Assert.AreEqual(FocusProvider.gameObject, EventSystem.current.currentSelectedGameObject);
            Assert.AreEqual(PanelFocusSessionState.RiveFocused, FocusController.SessionState);
            Assert.AreSame(m_widget, FocusController.ActiveWidget);
            AssertFocused("topLeft");

            yield return MoveOnRivePanel(MoveDirection.Right);
            AssertFocused("topRight");
            Assert.AreEqual(FocusProvider.gameObject, EventSystem.current.currentSelectedGameObject);

            yield return MoveOnRivePanel(MoveDirection.Down);
            AssertFocused("bottomRight");

            yield return MoveOnRivePanel(MoveDirection.Left);
            AssertFocused("bottomLeft");

            yield return MoveOnRivePanel(MoveDirection.Up);
            AssertFocused("topLeft");
            Assert.AreEqual(PanelFocusSessionState.RiveFocused, FocusController.SessionState);
        }

        [UnityTest]
        public IEnumerator MovingRightFromTopRight_SelectsTheButtonOnTheRight()
        {
            yield return LoadGrid();
            LinkPanelToNeighborButtons();

            yield return MoveFromUiButton(m_leftButton, MoveDirection.Right);
            AssertFocused("topLeft");

            yield return MoveOnRivePanel(MoveDirection.Right);
            AssertFocused("topRight");

            yield return MoveOnRivePanel(MoveDirection.Right);

            Assert.AreEqual(m_rightButton.gameObject, EventSystem.current.currentSelectedGameObject);
            Assert.IsFalse(m_widget.StateMachine.HasFocus);
            Assert.AreEqual(PanelFocusSessionState.Unfocused, FocusController.SessionState);
            Assert.IsFalse(IsFocused("topRight"));
        }

        [UnityTest]
        public IEnumerator Click_FocusesButton_AndDecoyDoesNotStealFocus()
        {
            yield return LoadGrid();
            Canvas.ForceUpdateCanvases();

            ClickAtArtboardPoint(110f, 90f);
            yield return null;

            Assert.AreEqual(FocusProvider.gameObject, EventSystem.current.currentSelectedGameObject);
            Assert.AreEqual(PanelFocusSessionState.RiveFocused, FocusController.SessionState);
            AssertFocused("topLeft");

            m_widget.StateMachine.ClearFocus();
            yield return null;
            Assert.AreEqual(PanelFocusSessionState.Released, FocusController.SessionState);

            ClickAtArtboardPoint(250f, 200f);
            yield return null;

            Assert.IsFalse(m_widget.StateMachine.HasFocus);
            Assert.IsFalse(IsFocused("topLeft"));
            Assert.IsFalse(IsFocused("topRight"));
            Assert.IsFalse(IsFocused("bottomLeft"));
            Assert.IsFalse(IsFocused("bottomRight"));

            ClickAtArtboardPoint(390f, 90f);
            yield return null;

            Assert.AreEqual(PanelFocusSessionState.RiveFocused, FocusController.SessionState);
            AssertFocused("topRight");
        }

        [UnityTest]
        public IEnumerator AfterRiveClearsFocus_ArrowLeaves_FocusNextKeepsTheNextArrowInside()
        {
            yield return LoadGrid();
            LinkPanelToNeighborButtons();

            yield return MoveFromUiButton(m_leftButton, MoveDirection.Right);
            Assert.AreEqual(PanelFocusSessionState.RiveFocused, FocusController.SessionState);
            AssertFocused("topLeft");

            m_widget.StateMachine.ClearFocus();
            yield return null;
            Assert.AreEqual(PanelFocusSessionState.Released, FocusController.SessionState);
            Assert.AreEqual(FocusProvider.gameObject, EventSystem.current.currentSelectedGameObject);

            yield return MoveOnRivePanel(MoveDirection.Right);
            Assert.AreEqual(m_rightButton.gameObject, EventSystem.current.currentSelectedGameObject);
            Assert.AreEqual(PanelFocusSessionState.Unfocused, FocusController.SessionState);
            Assert.IsFalse(m_widget.StateMachine.HasFocus);

            yield return MoveFromUiButton(m_leftButton, MoveDirection.Right);
            AssertFocused("topLeft");

            m_widget.StateMachine.ClearFocus();
            yield return null;
            Assert.AreEqual(PanelFocusSessionState.Released, FocusController.SessionState);

            Assert.IsTrue(m_widget.StateMachine.FocusNext());
            Assert.AreEqual(PanelFocusSessionState.RiveFocused, FocusController.SessionState);
            Assert.AreSame(m_widget, FocusController.ActiveWidget);
            yield return null;
            AssertFocused("topLeft");

            yield return MoveOnRivePanel(MoveDirection.Right);
            AssertFocused("topRight");
            Assert.AreEqual(FocusProvider.gameObject, EventSystem.current.currentSelectedGameObject);
        }

        [UnityTest]
        public IEnumerator DisablingTopRight_DownStillMoves_RightSkipsThenLeaves()
        {
            yield return LoadGrid();
            LinkPanelToNeighborButtons();

            SetCanFocus("topRight", false);
            yield return null;

            yield return MoveFromUiButton(m_leftButton, MoveDirection.Right);
            AssertFocused("topLeft");

            yield return MoveOnRivePanel(MoveDirection.Down);
            AssertFocused("bottomLeft");
            Assert.IsFalse(IsFocused("topRight"));
            Assert.AreEqual(FocusProvider.gameObject, EventSystem.current.currentSelectedGameObject);

            m_widget.StateMachine.ClearFocus();
            yield return null;
            yield return MoveFromUiButton(m_leftButton, MoveDirection.Right);
            AssertFocused("topLeft");

            // BottomRight is still to the right of TopLeft, so this stays inside
            // the panel and skips the disabled TopRight button.
            yield return MoveOnRivePanel(MoveDirection.Right);
            AssertFocused("bottomRight");
            Assert.IsFalse(IsFocused("topRight"));
            Assert.AreEqual(FocusProvider.gameObject, EventSystem.current.currentSelectedGameObject);

            yield return MoveOnRivePanel(MoveDirection.Right);
            Assert.AreEqual(m_rightButton.gameObject, EventSystem.current.currentSelectedGameObject);
            Assert.IsFalse(IsFocused("topRight"));
            Assert.IsFalse(m_widget.StateMachine.HasFocus);
        }

        [UnityTest]
        public IEnumerator Deselect_ClearsRiveFocus()
        {
            yield return LoadGrid();
            LinkPanelToNeighborButtons();

            yield return MoveFromUiButton(m_leftButton, MoveDirection.Right);
            AssertFocused("topLeft");

            EventSystem.current.SetSelectedGameObject(null);
            yield return null;

            Assert.IsFalse(m_widget.StateMachine.HasFocus);
            Assert.AreEqual(PanelFocusSessionState.Unfocused, FocusController.SessionState);
            Assert.IsFalse(IsFocused("topLeft"));
        }

        private IEnumerator LoadGrid()
        {
            Asset riveAsset = null;
            yield return m_testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_focus_grid_test,
                (asset) => riveAsset = asset,
                () => Assert.Fail("Failed to load focus_grid_test.riv"));

            m_widget.Load(riveAsset, "FocusGrid", "State Machine 1");
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;

            Assert.AreEqual(WidgetStatus.Loaded, m_widget.Status);
            Assert.IsNotNull(m_widget.StateMachine);
            Assert.IsTrue(m_widget.StateMachine.HasFocusNodes);
            Assert.IsNotNull(m_widget.StateMachine.ViewModelInstance);
            Assert.IsNotNull(FocusProvider);
        }

        /// <summary>
        /// Makes the left/right UI buttons and the Rive panel point at each other
        /// so gamepad/keyboard navigation can enter and leave the panel.
        /// </summary>
        private void LinkPanelToNeighborButtons()
        {
            CanvasFocusInputProvider provider = FocusProvider;
            m_renderer.Navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnLeft = m_leftButton,
                selectOnRight = m_rightButton
            };
            m_leftButton.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnRight = provider
            };
            m_rightButton.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnLeft = provider
            };
        }

        private IEnumerator MoveFromUiButton(Selectable button, MoveDirection direction)
        {
            EventSystem.current.SetSelectedGameObject(button.gameObject);
            yield return null;
            SendUiMove(button.gameObject, direction);
            yield return null;
        }

        private IEnumerator MoveOnRivePanel(MoveDirection direction)
        {
            SendUiMove(FocusProvider.gameObject, direction);
            yield return null;
        }

        private static void SendUiMove(GameObject target, MoveDirection direction)
        {
            var axis = new AxisEventData(EventSystem.current)
            {
                moveDir = direction,
                moveVector = DirectionToVector(direction)
            };
            ExecuteEvents.Execute(target, axis, ExecuteEvents.moveHandler);
        }

        private static Vector2 DirectionToVector(MoveDirection direction)
        {
            switch (direction)
            {
                case MoveDirection.Left:
                    return Vector2.left;
                case MoveDirection.Right:
                    return Vector2.right;
                case MoveDirection.Up:
                    return Vector2.up;
                case MoveDirection.Down:
                    return Vector2.down;
                default:
                    return Vector2.zero;
            }
        }

        private void ClickAtArtboardPoint(float artboardX, float artboardY)
        {
            Vector2 screenPoint = ArtboardPointToScreen(artboardX, artboardY);
            m_inputModule.PointerDownAt(screenPoint);
            m_inputModule.PointerUpAt(screenPoint);
        }

        private Vector2 ArtboardPointToScreen(float artboardX, float artboardY)
        {
            Vector2 normalized = new Vector2(artboardX / ArtboardWidth, 1f - (artboardY / ArtboardHeight));
            RectTransform rectTransform = m_renderer.RectTransform;
            Rect rect = rectTransform.rect;
            Vector3 world = rectTransform.TransformPoint(new Vector3(
                rect.xMin + normalized.x * rect.width,
                rect.yMin + normalized.y * rect.height,
                0f));
            return RectTransformUtility.WorldToScreenPoint(null, world);
        }

        private void AssertFocused(string expectedButton)
        {
            Assert.IsTrue(IsFocused(expectedButton), $"{expectedButton}/isFocused should be true");
        }

        private bool IsFocused(string button)
        {
            ViewModelInstanceBooleanProperty property =
                m_widget.StateMachine.ViewModelInstance.GetProperty<ViewModelInstanceBooleanProperty>($"{button}/isFocused");
            Assert.IsNotNull(property, $"{button}/isFocused should exist");
            return property.Value;
        }

        private void SetCanFocus(string button, bool canFocus)
        {
            ViewModelInstanceBooleanProperty property =
                m_widget.StateMachine.ViewModelInstance.GetProperty<ViewModelInstanceBooleanProperty>($"{button}/canFocus");
            Assert.IsNotNull(property, $"{button}/canFocus should exist");
            property.Value = canFocus;
        }

        private Selectable CreateUiButton(string name, Vector2 anchoredPosition)
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
