using System;
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
    /// focus.riv's Grid between two uGUI buttons, driven by EventSystem navigation the way
    /// a gamepad or the arrow keys would. Runs on a panel that waits for Rive and on one that doesn't.
    /// </summary>
    [TestFixture(ThreadingMode.MainThread)]
    [TestFixture(ThreadingMode.BackgroundThread)]
    public class RivePanelFocusNavigationTests
    {
        private readonly ThreadingMode m_threadingMode;

        private TestAssetLoadingManager m_assetLoader;
        private MockLogger m_mockLogger;
        private Canvas m_canvas;
        private GameObject m_camera;
        private GameObject m_eventSystemObject;
        private TestInputModule m_inputModule;
        private RivePanel m_panel;
        private RiveCanvasRenderer m_renderer;
        private RiveWidget m_widget;
        private Selectable m_leftButton;
        private Selectable m_rightButton;

        public RivePanelFocusNavigationTests(ThreadingMode threadingMode)
        {
            m_threadingMode = threadingMode;
        }

        private PanelFocusController FocusController => m_panel.FocusController;

        private CanvasFocusInputProvider FocusProvider => m_panel.GetComponent<CanvasFocusInputProvider>();

        private FocusState WidgetFocus => ((IRiveFocusWidget)m_widget).FocusState;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            m_mockLogger = new MockLogger();
            DebugLogger.Instance = m_mockLogger;
            m_assetLoader = new TestAssetLoadingManager();

            m_camera = new GameObject("FocusCamera", typeof(Camera));

            var canvasObj = new GameObject("FocusNavigationCanvas");
            m_canvas = canvasObj.AddComponent<Canvas>();
            m_canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<GraphicRaycaster>();

            m_eventSystemObject = new GameObject("EventSystem");
            m_eventSystemObject.AddComponent<EventSystem>();
            m_inputModule = m_eventSystemObject.AddComponent<TestInputModule>();

            m_panel = RivePanelTestUtils.CreatePanel("FocusNavigationPanel");
            m_panel.ThreadingMode = m_threadingMode;
            m_panel.transform.SetParent(m_canvas.transform, false);
            m_panel.SetDimensions(new Vector2(640f, 360f));
            m_renderer = m_panel.gameObject.AddComponent<RiveCanvasRenderer>();

            m_widget = RivePanelTestUtils.CreateWidget<RiveWidget>("FocusNavigationWidget");
            m_widget.Fit = Fit.Fill;
            m_panel.AddToHierarchy(m_widget);
            RivePanelTestUtils.MakeWidgetFillPanel(m_widget);

            m_leftButton = CreateUiButton("LeftButton", new Vector2(-450f, 0f));
            m_rightButton = CreateUiButton("RightButton", new Vector2(450f, 0f));

            yield return m_assetLoader.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_focus,
                a => m_asset = a,
                () => Assert.Fail("Failed to load focus.riv"));
        }

        private Asset m_asset;

        private IEnumerator Load(string artboard)
        {
            m_widget.Load(m_asset, artboard, null);
            yield return RivePanelTestUtils.WaitForLoaded(m_widget);
            yield return WaitUntil(() => FocusController.HasFocusableContent, $"{artboard}'s focus nodes to show up");
            Canvas.ForceUpdateCanvases();
            LinkPanelToNeighborButtons();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.Destroy(m_eventSystemObject);
            UnityEngine.Object.Destroy(m_canvas.gameObject);
            UnityEngine.Object.Destroy(m_camera);
            m_assetLoader?.UnloadAllAssets();
        }

        [UnityTest]
        public IEnumerator Arrows_EnterFromAButton_MoveByPosition_AndLeaveAtTheEdge()
        {
            yield return Load("Grid");
            yield return Select(m_leftButton);
            Move(m_leftButton.gameObject, MoveDirection.Right);
            yield return WaitUntil(() => WidgetFocus.HasFocus, "Rive to take focus");
            Assert.AreEqual(FocusProvider.gameObject, EventSystem.current.currentSelectedGameObject);
            Assert.AreEqual(PanelFocusSessionState.RiveFocused, FocusController.SessionState);
            AABB one = FocusedBounds();

            Move(FocusProvider.gameObject, MoveDirection.Right);
            yield return WaitUntil(() => FocusedBounds().minX > one.minX, "focus to move right");
            Assert.AreEqual(one.minY, FocusedBounds().minY, 0.01f);

            Move(FocusProvider.gameObject, MoveDirection.Left);
            yield return WaitUntil(() => Mathf.Approximately(FocusedBounds().minX, one.minX), "focus to move back");

            // Nothing left of the first column, so Unity's navigation takes the move.
            Move(FocusProvider.gameObject, MoveDirection.Left);
            yield return WaitUntil(
                () => EventSystem.current.currentSelectedGameObject == m_leftButton.gameObject,
                "Unity to select the button on the left");
            yield return WaitUntil(() => !WidgetFocus.HasFocus, "Rive to let go");
            Assert.AreEqual(PanelFocusSessionState.Unfocused, FocusController.SessionState);
        }

        [UnityTest]
        public IEnumerator Deselecting_ThePanel_ClearsRiveFocus()
        {
            yield return Load("Grid");
            yield return Select(m_leftButton);
            Move(m_leftButton.gameObject, MoveDirection.Right);
            yield return WaitUntil(() => WidgetFocus.HasFocus, "Rive to take focus");

            EventSystem.current.SetSelectedGameObject(null);
            Assert.IsFalse(WidgetFocus.HasFocus, "clearing doesn't wait, so it shows straight away");
            Assert.AreEqual(PanelFocusSessionState.Unfocused, FocusController.SessionState);

            // And Rive agrees once the next advance lands.
            yield return null;
            yield return null;
            Assert.IsFalse(WidgetFocus.HasFocus);
        }

        [UnityTest]
        public IEnumerator DeselectingBeforeTheEntryLands_DropsTheFocusItTook()
        {
            yield return Load("Grid");
            yield return Select(m_leftButton);
            Move(m_leftButton.gameObject, MoveDirection.Right);
            // Same frame, so on a panel that doesn't wait the entry is still out.
            EventSystem.current.SetSelectedGameObject(null);

            for (int i = 0; i < 10; i++)
            {
                yield return null;
                Assert.IsFalse(WidgetFocus.HasFocus, $"frame {i + 1}");
                Assert.AreEqual(PanelFocusSessionState.Unfocused, FocusController.SessionState, $"frame {i + 1}");
            }
        }

        [UnityTest]
        public IEnumerator EnterAndSpace_ReachTheFocusedButton_AndTabMovesOn()
        {
            yield return Load("Buttons");
            yield return EnterFromTheLeft();
            AABB first = FocusedBounds();

            Press(KeyCode.Return);
            yield return WaitForString("lastPressed", "1");

            Press(KeyCode.Tab);
            yield return WaitUntil(() => FocusedBounds().minX > first.minX, "Tab to move focus on");

            Press(KeyCode.Space);
            yield return WaitForString("lastPressed", "2");
        }

        [UnityTest]
        public IEnumerator Typing_ReachesAFocusedTextInput()
        {
            yield return Load("TextInput");
            yield return EnterFromTheLeft();
            yield return WaitUntil(() => WidgetFocus.ExpectsKeyboardInput, "the text field to take focus");

            Type('h', KeyCode.H);
            Type('i', KeyCode.I);
            yield return WaitForString("text", "hi");
        }

        [UnityTest]
        public IEnumerator CutAndPaste_GoThroughTheClipboard()
        {
            string clipboard = null;
            FocusController.Keyboard.ReadClipboard = () => clipboard;
            FocusController.Keyboard.WriteClipboard = text => clipboard = text;
            yield return Load("TextInput");
            yield return EnterFromTheLeft();
            yield return WaitUntil(() => WidgetFocus.ExpectsKeyboardInput, "the text field to take focus");
            Type('h', KeyCode.H);
            Type('i', KeyCode.I);
            yield return WaitForString("text", "hi");

            // Core selects all itself, the host only does the clipboard.
            Press(KeyCode.A, ShortcutModifier);
            Press(KeyCode.X, ShortcutModifier);
            yield return WaitForString("text", "");
            yield return WaitUntil(() => clipboard == "hi", "the cut text on the clipboard");

            Press(KeyCode.V, ShortcutModifier);
            Press(KeyCode.V, ShortcutModifier);
            yield return WaitForString("text", "hihi");
        }

        [UnityTest]
        public IEnumerator ATabTheSceneClaims_KeepsFocusPut()
        {
            yield return Load("ClaimsTab");
            yield return EnterFromTheLeft();
            AABB trap = FocusedBounds();

            Press(KeyCode.Tab);
            yield return WaitForString("lastPressed", "tab");
            Assert.AreEqual(trap.minX, FocusedBounds().minX, 0.01f);
            Assert.AreEqual(trap.minY, FocusedBounds().minY, 0.01f);
            Assert.AreEqual(FocusProvider.gameObject, EventSystem.current.currentSelectedGameObject);
        }

        private IEnumerator EnterFromTheLeft()
        {
            yield return Select(m_leftButton);
            Move(m_leftButton.gameObject, MoveDirection.Right);
            yield return WaitUntil(() => WidgetFocus.HasFocus, "Rive to take focus");
        }

        private void Press(KeyCode keyCode, EventModifiers modifiers = EventModifiers.None)
        {
            SendKeys(new Event { type = EventType.KeyDown, keyCode = keyCode, modifiers = modifiers });
            SendKeys(new Event { type = EventType.KeyUp, keyCode = keyCode, modifiers = modifiers });
        }

        // Cmd, or Ctrl on Windows, like core's text fields.
        private static EventModifiers ShortcutModifier =>
            SystemInfo.operatingSystemFamily == OperatingSystemFamily.Windows ? EventModifiers.Control : EventModifiers.Command;

        // IMGUI sends the key, then the character as a key down with no key code.
        private void Type(char character, KeyCode keyCode)
        {
            SendKeys(
                new Event { type = EventType.KeyDown, keyCode = keyCode },
                new Event { type = EventType.KeyDown, keyCode = KeyCode.None, character = character });
            SendKeys(new Event { type = EventType.KeyUp, keyCode = keyCode });
        }

        private void SendKeys(params Event[] events)
        {
            FocusController.Keyboard.Process(new System.Collections.Generic.List<Event>(events), new BaseEventData(EventSystem.current));
        }

        private IEnumerator WaitForString(string path, string expected)
        {
            string value = null;
            float deadline = Time.realtimeSinceStartup + 5f;
            while (value != expected)
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, $"Timed out waiting for {path} to be '{expected}', it was '{value}'.");
                if (m_threadingMode == ThreadingMode.MainThread)
                {
                    value = m_widget.StateMachine.ViewModelInstance.GetProperty<ViewModelInstanceStringProperty>(path).Value;
                }
                else
                {
                    Future<string> read = m_widget.StateMachineHandle.GetViewModelInstance().GetStringProperty(path).GetValueAsync();
                    while (!read.IsDone)
                    {
                        yield return null;
                    }
                    value = read.Result;
                }
                if (value != expected)
                {
                    yield return null;
                }
            }
        }

        private AABB FocusedBounds()
        {
            Assert.IsTrue(WidgetFocus.HasBounds, "the focused node should have bounds");
            return WidgetFocus.Bounds;
        }

        private void LinkPanelToNeighborButtons()
        {
            m_renderer.Navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnLeft = m_leftButton,
                selectOnRight = m_rightButton
            };
            m_leftButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = FocusProvider };
            m_rightButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = FocusProvider };
        }

        private static IEnumerator Select(Selectable selectable)
        {
            EventSystem.current.SetSelectedGameObject(selectable.gameObject);
            yield return null;
        }

        private static void Move(GameObject target, MoveDirection direction)
        {
            var axis = new AxisEventData(EventSystem.current) { moveDir = direction };
            ExecuteEvents.Execute(target, axis, ExecuteEvents.moveHandler);
        }

        private static IEnumerator WaitUntil(Func<bool> condition, string what)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!condition())
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, $"Timed out waiting for {what}.");
                yield return null;
            }
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
