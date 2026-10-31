using System.Collections.Generic;
using NUnit.Framework;
using Rive.Components;
using Rive.Tests.Utils;
using Rive.Utils;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Rive.Tests
{
    /// <summary>
    /// Keys reaching the focused widget, and arrows and Tab moving focus only when Rive leaves
    /// them, as on the web runtime. Run on a panel that waits for Rive and one that doesn't.
    /// </summary>
    [TestFixture(ThreadingMode.MainThread)]
    [TestFixture(ThreadingMode.BackgroundThread)]
    public class PanelKeyboardInputTests
    {
        private readonly ThreadingMode m_threadingMode;
        private RivePanel m_panel;
        private FakeFocusProvider m_provider;
        private GameObject m_eventSystemObject;
        private MockFocusWidget m_left;
        private MockFocusWidget m_right;
        private string m_clipboard;

        public PanelKeyboardInputTests(ThreadingMode threadingMode)
        {
            m_threadingMode = threadingMode;
        }

        private PanelFocusController FocusController => m_panel.FocusController;

        private bool Waits => m_threadingMode == ThreadingMode.MainThread;

        [SetUp]
        public void Setup()
        {
            DebugLogger.Instance = new MockLogger();
            m_eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));

            m_panel = RivePanelTestUtils.CreatePanel("KeyboardPanel");
            m_panel.ThreadingMode = m_threadingMode;
            m_panel.SetDimensions(new Vector2(400, 200));
            var canvasObj = new GameObject("KeyboardCanvas");
            canvasObj.AddComponent<Canvas>();
            m_panel.transform.SetParent(canvasObj.transform, false);
            m_provider = new FakeFocusProvider();
            FocusController.FocusProvider = m_provider;

            m_left = CreateWidget("Left", new Vector2(50, 100));
            m_right = CreateWidget("Right", new Vector2(250, 100));
            m_panel.Tick(0f);
            Orchestrator.Instance.RunUpdatePass();

            m_provider.HasHostFocus = true;
            FocusController.NotifyHostFocusGained(FocusEntryReason.Navigation, FocusNavigationDirection.Right);
            Assert.AreSame(m_left, FocusController.ActiveWidget);
            m_left.DeferAnswers = !Waits;
            m_right.DeferAnswers = !Waits;

            m_clipboard = null;
            FocusController.Keyboard.ReadClipboard = () => m_clipboard;
            FocusController.Keyboard.WriteClipboard = text => m_clipboard = text;
        }

        [TearDown]
        public void TearDown()
        {
            Canvas canvas = m_panel.GetComponentInParent<Canvas>();
            Object.Destroy(m_panel.gameObject);
            Object.Destroy(canvas.gameObject);
            Object.Destroy(m_eventSystemObject);
        }

        [Test]
        public void Keys_GoToTheFocusedWidget_DownAndUp()
        {
            Send(KeyDown(KeyCode.Return), KeyUp(KeyCode.Return));

            Assert.AreEqual(2, m_left.Keys.Count);
            Assert.AreEqual((RiveKey.Enter, RiveKeyModifiers.None, true, false), m_left.Keys[0]);
            Assert.AreEqual((RiveKey.Enter, RiveKeyModifiers.None, false, false), m_left.Keys[1]);
            Assert.IsEmpty(m_right.Keys);
        }

        [Test]
        public void HeldKey_IsSentAsARepeat()
        {
            Send(KeyDown(KeyCode.Space));
            Send(KeyDown(KeyCode.Space));

            Assert.IsFalse(m_left.Keys[0].repeat);
            Assert.IsTrue(m_left.Keys[1].repeat);
        }

        [Test]
        public void UnclaimedArrow_MovesFocus_AndKeepsUnitysMove()
        {
            m_left.MoveResults[FocusNavigationDirection.Right] = false;

            BaseEventData eventData = Send(KeyDown(KeyCode.RightArrow));
            Answer();

            Assert.IsTrue(eventData.used, "the frame's move is Rive's");
            Assert.AreEqual(RiveKey.Right, m_left.Keys[0].key);
            Assert.AreEqual(FocusNavigationDirection.Right, m_left.LastMove);
            Assert.AreSame(m_right, FocusController.ActiveWidget);
        }

        [Test]
        public void ClaimedArrow_DoesNotMoveFocus()
        {
            m_left.ClaimedKeys.Add(RiveKey.Right);

            Send(KeyDown(KeyCode.RightArrow));
            Answer();

            Assert.IsNull(m_left.LastMove);
            Assert.AreSame(m_left, FocusController.ActiveWidget);
        }

        [Test]
        public void Tab_MovesNext_AndShiftTab_MovesPrevious()
        {
            Send(KeyDown(KeyCode.Tab));
            Answer();
            Assert.AreEqual(FocusNavigationDirection.Next, m_left.LastMove);

            Send(KeyUp(KeyCode.Tab), KeyDown(KeyCode.Tab, EventModifiers.Shift));
            Answer();
            Assert.AreEqual(FocusNavigationDirection.Previous, m_left.LastMove);
        }

        [Test]
        public void ClaimedTab_KeepsFocusPut()
        {
            m_left.ClaimedKeys.Add(RiveKey.Tab);

            Send(KeyDown(KeyCode.Tab));
            Answer();

            Assert.IsNull(m_left.LastMove);
            Assert.AreSame(m_left, FocusController.ActiveWidget);
        }

        [Test]
        public void ArrowWithAShortcutModifier_IsOnlyAKey()
        {
            BaseEventData eventData = Send(KeyDown(KeyCode.RightArrow, EventModifiers.Control));
            Answer();

            Assert.IsFalse(eventData.used);
            Assert.AreEqual(RiveKeyModifiers.Ctrl, m_left.Keys[0].modifiers);
            Assert.IsNull(m_left.LastMove);
        }

        [Test]
        public void TypedCharacters_GoAsTextOnlyWhenTheNodeTakesText()
        {
            Send(Character('h'), Character('i'));
            Assert.IsEmpty(m_left.Texts, "a plain button doesn't take text");

            m_left.ExpectsText = true;
            Send(Character('h'), Character('i'), Character('\b'));
            CollectionAssert.AreEqual(new[] { "hi" }, m_left.Texts);
        }

        [Test]
        public void TypingIntoText_KeepsUnitysMove_SoWasdDoesntNavigate()
        {
            BaseEventData plain = Send(KeyDown(KeyCode.D), Character('d'));
            Assert.IsFalse(plain.used, "WASD can still navigate a plain button");
            Assert.IsFalse(FocusController.Keyboard.OwnsMove(MoveDirection.Right));
            Send(KeyUp(KeyCode.D));

            m_left.ExpectsText = true;
            BaseEventData typing = Send(KeyDown(KeyCode.D), Character('d'));
            Assert.IsTrue(typing.used);
            Assert.IsTrue(FocusController.Keyboard.OwnsMove(MoveDirection.Right), "nor while d is held");

            Send(KeyUp(KeyCode.D));
            Assert.IsFalse(FocusController.Keyboard.OwnsMove(MoveDirection.Right), "a gamepad can still leave");
        }

        [Test]
        public void TextTypedBeforeAKey_ReachesRiveFirst()
        {
            m_left.ExpectsText = true;
            Send(KeyDown(KeyCode.A), Character('a'), KeyDown(KeyCode.B), Character('b'), KeyDown(KeyCode.Backspace));

            CollectionAssert.AreEqual(new[] { "a", "b" }, m_left.Texts);
            Assert.AreEqual(RiveKey.Backspace, m_left.Keys[2].key);
        }

        [Test]
        public void Copy_PutsTheSelectionOnTheClipboard()
        {
            m_left.Selection = "hello";
            Send(KeyDown(KeyCode.C, ShortcutModifier), Character('c', ShortcutModifier));
            Answer();

            Assert.AreEqual("hello", m_clipboard);
            Assert.AreEqual(KeySelection.Copy, m_left.KeySelections[0]);
            Assert.IsEmpty(m_left.Texts, "the shortcut's character isn't typed");
        }

        [Test]
        public void Copy_WithNothingSelected_LeavesTheClipboard()
        {
            m_clipboard = "before";
            Send(KeyDown(KeyCode.C, ShortcutModifier));
            Answer();

            Assert.AreEqual("before", m_clipboard);
        }

        [Test]
        public void Cut_CopiesAndDeletes_OnlyInAField()
        {
            m_left.Selection = "plain";
            Send(KeyDown(KeyCode.X, ShortcutModifier));
            Answer();
            Assert.AreEqual(KeySelection.Copy, m_left.KeySelections[0], "nothing to delete outside a field");
            Assert.AreEqual("plain", m_clipboard);

            m_left.ExpectsText = true;
            m_left.Selection = "typed";
            Send(KeyUp(KeyCode.X), KeyDown(KeyCode.X, ShortcutModifier));
            Answer();
            Assert.AreEqual(KeySelection.Cut, m_left.KeySelections[2]);
            Assert.AreEqual("typed", m_clipboard);
        }

        [Test]
        public void Paste_TypesTheClipboard_InOrder()
        {
            m_clipboard = "one\r\ntwo";
            Send(Character('x'), KeyDown(KeyCode.V, ShortcutModifier));
            Assert.IsEmpty(m_left.Texts, "a plain button doesn't take text");

            m_left.ExpectsText = true;
            Send(KeyDown(KeyCode.A), Character('a'), KeyDown(KeyCode.V, ShortcutModifier), Character('v', ShortcutModifier));
            CollectionAssert.AreEqual(new[] { "a", "one\ntwo" }, m_left.Texts);
        }

        [Test]
        public void AltGrCharacters_StillType()
        {
            m_left.ExpectsText = true;
            Send(Character('@', EventModifiers.Control | EventModifiers.Alt));

            CollectionAssert.AreEqual(new[] { "@" }, m_left.Texts);
        }

        [Test]
        public void HeldArrow_OwnsUnitysRepeatedMove()
        {
            Send(KeyDown(KeyCode.DownArrow));
            Answer();

            Assert.IsTrue(FocusController.Keyboard.OwnsMove(MoveDirection.Down));
            Assert.IsFalse(FocusController.Keyboard.OwnsMove(MoveDirection.Up));

            Send(KeyUp(KeyCode.DownArrow));
            Assert.IsFalse(FocusController.Keyboard.OwnsMove(MoveDirection.Down));
        }

        [Test]
        public void Submit_SendsEnter_UnlessTheKeyboardAlreadyDidThisFrame()
        {
            FocusController.Keyboard.OnSubmit();
            Assert.AreEqual(2, m_left.Keys.Count);
            Assert.AreEqual(RiveKey.Enter, m_left.Keys[0].key);

            m_left.Keys.Clear();
            Send(KeyDown(KeyCode.Return));
            FocusController.Keyboard.OnSubmit();
            Assert.AreEqual(1, m_left.Keys.Count, "the keyboard's Enter went through as a key");
        }

        [Test]
        public void Cancel_SendsEscape()
        {
            FocusController.Keyboard.OnCancel();

            Assert.AreEqual(RiveKey.Escape, m_left.Keys[0].key);
            Assert.IsTrue(m_left.Keys[0].pressed);
            Assert.IsFalse(m_left.Keys[1].pressed);
        }

        [Test]
        public void Keys_AreDropped_WhileNothingInRiveHasFocus()
        {
            m_provider.HasHostFocus = false;
            FocusController.NotifyHostFocusLost();
            m_left.AnswerPending();
            m_left.Keys.Clear();

            Send(KeyDown(KeyCode.Return), Character('x'));

            Assert.IsEmpty(m_left.Keys);
            Assert.IsEmpty(m_left.Texts);
        }

        private MockFocusWidget CreateWidget(string name, Vector2 position)
        {
            var widget = RivePanelTestUtils.CreateWidget<MockFocusWidget>(name);
            widget.MockEligible = true;
            widget.SeedSucceeds = true;
            m_panel.AddToHierarchy(widget);
            RivePanelTestUtils.PositionWidget(widget, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(80, 80), position);
            return widget;
        }

        private BaseEventData Send(params Event[] events)
        {
            var eventData = new BaseEventData(EventSystem.current);
            FocusController.Keyboard.Process(new List<Event>(events), eventData);
            return eventData;
        }

        // Lands what a panel that doesn't wait still has out, in order.
        private void Answer()
        {
            for (int i = 0; i < 4; i++)
            {
                m_left.AnswerPending();
                m_right.AnswerPending();
            }
        }

        private static Event KeyDown(KeyCode keyCode, EventModifiers modifiers = EventModifiers.None)
        {
            return new Event { type = EventType.KeyDown, keyCode = keyCode, modifiers = modifiers };
        }

        private static Event KeyUp(KeyCode keyCode)
        {
            return new Event { type = EventType.KeyUp, keyCode = keyCode };
        }

        // How IMGUI reports a typed character: a key down with no key code.
        private static Event Character(char character, EventModifiers modifiers = EventModifiers.None)
        {
            return new Event { type = EventType.KeyDown, keyCode = KeyCode.None, character = character, modifiers = modifiers };
        }

        // Cmd, or Ctrl on Windows, like core's text fields.
        private static EventModifiers ShortcutModifier =>
            SystemInfo.operatingSystemFamily == OperatingSystemFamily.Windows ? EventModifiers.Control : EventModifiers.Command;
    }
}
