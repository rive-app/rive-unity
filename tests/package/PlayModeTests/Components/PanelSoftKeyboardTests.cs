using NUnit.Framework;
using Rive.Components;
using Rive.Tests.Utils;
using Rive.Utils;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Rive.Tests
{
    /// <summary>
    /// The on-screen keyboard following a focused Rive text field, with a fake keyboard standing in for Unity's.
    /// </summary>
    public class PanelSoftKeyboardTests
    {
        private RivePanel m_panel;
        private FakeFocusProvider m_provider;
        private GameObject m_eventSystemObject;
        private MockFocusWidget m_field;
        private FakeSoftKeyboard m_keyboard;

        private PanelFocusController FocusController => m_panel.FocusController;

        private PanelSoftKeyboard SoftKeyboard => FocusController.Keyboard.SoftKeyboard;

        [SetUp]
        public void Setup()
        {
            DebugLogger.Instance = new MockLogger();
            m_eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));

            m_panel = RivePanelTestUtils.CreatePanel("SoftKeyboardPanel");
            m_panel.SetDimensions(new Vector2(400, 200));
            var canvasObj = new GameObject("SoftKeyboardCanvas");
            canvasObj.AddComponent<Canvas>();
            m_panel.transform.SetParent(canvasObj.transform, false);
            m_provider = new FakeFocusProvider();
            FocusController.FocusProvider = m_provider;
            m_keyboard = new FakeSoftKeyboard();
            SoftKeyboard.Keyboard = m_keyboard;

            m_field = RivePanelTestUtils.CreateWidget<MockFocusWidget>("Field");
            m_field.MockEligible = true;
            m_field.SeedSucceeds = true;
            m_panel.AddToHierarchy(m_field);
            RivePanelTestUtils.PositionWidget(m_field, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(80, 80), new Vector2(50, 100));
            m_panel.Tick(0f);
            Orchestrator.Instance.RunUpdatePass();

            m_provider.HasHostFocus = true;
            FocusController.NotifyHostFocusGained(FocusEntryReason.Navigation, FocusNavigationDirection.Right);
            Assert.AreSame(m_field, FocusController.ActiveWidget);
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
        public void OpensForAField_AndClosesWhenFocusLeavesIt()
        {
            Frame();
            Assert.AreEqual(0, m_keyboard.Opens, "a plain button doesn't open it");

            m_field.ExpectsText = true;
            Frame();
            Assert.AreEqual(1, m_keyboard.Opens);
            Assert.AreEqual(PanelSoftKeyboard.Placeholder, m_keyboard.Text);

            m_field.ExpectsText = false;
            Frame();
            Assert.AreEqual(1, m_keyboard.Closes);
            Assert.IsFalse(SoftKeyboard.IsOpen);
        }

        [Test]
        public void Typing_GoesToRiveAsText()
        {
            OpenForField();

            m_keyboard.Text = " hi";
            Frame();
            m_keyboard.Text = " hi!";
            Frame();

            CollectionAssert.AreEqual(new[] { "hi", "!" }, m_field.Texts);
            Assert.IsEmpty(m_field.Keys);
        }

        [Test]
        public void ReplacingAWord_DeletesBackToWhereTheyAgree()
        {
            OpenForField();
            m_keyboard.Text = " helo";
            Frame();

            m_keyboard.Text = " hello";
            Frame();

            CollectionAssert.AreEqual(new[] { "helo", "lo" }, m_field.Texts);
            Assert.AreEqual(2, m_field.Keys.Count);
            Assert.AreEqual((RiveKey.Backspace, RiveKeyModifiers.None, true, false), m_field.Keys[0]);
            Assert.AreEqual((RiveKey.Backspace, RiveKeyModifiers.None, false, false), m_field.Keys[1]);
        }

        [Test]
        public void DeletingThePlaceholder_IsABackspace_AndPutsItBack()
        {
            OpenForField();

            m_keyboard.Text = "";
            Frame();

            Assert.AreEqual(RiveKey.Backspace, m_field.Keys[0].key);
            Assert.AreEqual(PanelSoftKeyboard.Placeholder, m_keyboard.Text);

            // A frame later the keyboard shows the placeholder, and the user deletes it again.
            Frame();
            m_keyboard.Text = "";
            Frame();
            Assert.AreEqual(4, m_field.Keys.Count, "every Backspace counts, even with nothing typed");
        }

        [Test]
        public void AKeyboardSlowToTakeTheReset_IsntReadTwice()
        {
            OpenForField();
            m_keyboard.Text = "";
            m_keyboard.LagsResets = true;
            Frame();
            Frame();

            Assert.AreEqual(2, m_field.Keys.Count, "one Backspace");

            m_keyboard.TakeReset();
            m_keyboard.Text = " a";
            Frame();
            CollectionAssert.AreEqual(new[] { "a" }, m_field.Texts);
        }

        [Test]
        public void Done_SendsEnter_AndATapOpensItAgain()
        {
            OpenForField();

            m_keyboard.Status = TouchScreenKeyboard.Status.Done;
            Frame();
            Assert.AreEqual(RiveKey.Enter, m_field.Keys[0].key);
            Assert.IsFalse(SoftKeyboard.IsOpen);

            Frame();
            Assert.AreEqual(1, m_keyboard.Opens, "the field kept focus, but the user closed it");

            SoftKeyboard.NotifyTap();
            Frame();
            Assert.AreEqual(2, m_keyboard.Opens);
        }

        [Test]
        public void Cancelling_KeepsWhatWasTyped()
        {
            OpenForField();
            m_keyboard.Text = " hi";
            Frame();

            // Android puts back the text the keyboard opened with.
            m_keyboard.Text = PanelSoftKeyboard.Placeholder;
            m_keyboard.Status = TouchScreenKeyboard.Status.Canceled;
            Frame();

            CollectionAssert.AreEqual(new[] { "hi" }, m_field.Texts);
            Assert.IsEmpty(m_field.Keys, "no Backspace, and no Enter either");
            Assert.IsFalse(SoftKeyboard.IsOpen);
        }

        [Test]
        public void LosingUnitysSelection_ClosesIt()
        {
            OpenForField();

            m_provider.HasHostFocus = false;
            FocusController.NotifyHostFocusLost();
            Assert.AreEqual(1, m_keyboard.Closes);

            Frame();
            Assert.AreEqual(1, m_keyboard.Opens, "a field Unity doesn't have selected doesn't open it");
        }

        // What a frame does: Rive's update syncs the keyboard, then the selected panel reads it.
        private void Frame()
        {
            SoftKeyboard.Sync(m_provider.HasHostFocus);
            SoftKeyboard.ReadTyped();
        }

        private void OpenForField()
        {
            m_field.ExpectsText = true;
            Frame();
            Assert.IsTrue(SoftKeyboard.IsOpen);
        }

        private sealed class FakeSoftKeyboard : ISoftKeyboard
        {
            private string m_text = "";
            private string m_pendingReset;

            public int Opens { get; private set; }
            public int Closes { get; private set; }
            public bool LagsResets { get; set; }

            public bool IsSupported => true;

            public TouchScreenKeyboard.Status? Status { get; set; }

            public void Open(string text)
            {
                Opens++;
                m_text = text;
                Status = TouchScreenKeyboard.Status.Visible;
            }

            public void Close()
            {
                Closes++;
                Status = null;
            }

            public string Text
            {
                get => m_text;
                set
                {
                    if (LagsResets)
                    {
                        m_pendingReset = value;
                    }
                    else
                    {
                        m_text = value;
                    }
                }
            }

            public void TakeReset()
            {
                m_text = m_pendingReset;
                LagsResets = false;
            }
        }
    }
}
