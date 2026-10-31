using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Rive.Tests.Utils;
using Rive.Utils;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rive.Tests
{
    /// <summary>
    /// focus.riv (trays/testing/focus), shared with the other runtimes' focus tests: one
    /// artboard per keyboard focus scenario. The same scenarios as rive_native's
    /// focus_widget_test, through the state machine and handle APIs.
    /// </summary>
    public class FocusScenarioTests
    {
        private TestAssetLoadingManager m_assetLoader;
        private MockLogger m_mockLogger;
        private Asset m_asset;
        private File m_file;
        private StateMachine m_stateMachine;
        private readonly List<IDisposable> m_toDispose = new List<IDisposable>();

        [UnitySetUp]
        public IEnumerator Setup()
        {
            m_mockLogger = new MockLogger();
            DebugLogger.Instance = m_mockLogger;
            m_assetLoader = new TestAssetLoadingManager();
            yield return m_assetLoader.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_focus,
                asset => m_asset = asset,
                () => Assert.Fail("Failed to load focus.riv"));
            m_file = File.Load(m_asset);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (IDisposable disposable in m_toDispose)
            {
                disposable.Dispose();
            }
            m_toDispose.Clear();
            m_file?.Dispose();
            m_assetLoader?.UnloadAllAssets();
        }

        [Test]
        public void Next_WalksTheRowLeftToRight_ThenLetsGo()
        {
            Load("Buttons");

            Assert.IsTrue(m_stateMachine.FocusNext());
            AABB previous = Bounds();
            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(m_stateMachine.FocusNext(), $"step {i + 2}");
                Assert.Greater(Bounds().minX, previous.minX, $"step {i + 2}");
                previous = Bounds();
            }

            // Off the end, Rive lets go so the host can move on.
            Assert.IsFalse(m_stateMachine.FocusNext());
            Assert.IsFalse(m_stateMachine.HasFocus);
        }

        [Test]
        public void Previous_WalksTheRowRightToLeft_ThenLetsGo()
        {
            Load("Buttons");

            Assert.IsTrue(m_stateMachine.FocusPrevious());
            AABB previous = Bounds();
            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(m_stateMachine.FocusPrevious(), $"step {i + 2}");
                Assert.Less(Bounds().minX, previous.minX, $"step {i + 2}");
                previous = Bounds();
            }

            Assert.IsFalse(m_stateMachine.FocusPrevious());
            Assert.IsFalse(m_stateMachine.HasFocus);
        }

        // A group with a FocusData is a stop in its own right, visited before its children.
        [TestCase("EdgeParentScope", 6)]
        [TestCase("NoTraverse", 4)]
        [TestCase("Nested", 7)]
        [TestCase("Grid", 9)]
        [TestCase("ScrollList", 12)]
        public void Next_VisitsEveryStop(string artboard, int stops)
        {
            Load(artboard);

            for (int i = 0; i < stops; i++)
            {
                Assert.IsTrue(m_stateMachine.FocusNext(), $"stop {i + 1}");
                Assert.IsTrue(m_stateMachine.HasFocus, $"stop {i + 1}");
            }
            Assert.IsFalse(m_stateMachine.FocusNext());
            Assert.IsFalse(m_stateMachine.HasFocus);
        }

        [TestCase("EdgeClosedLoop")]
        [TestCase("EdgeStop")]
        public void Next_StaysInsideAScopeThatDoesNotLetGo(string artboard)
        {
            Load(artboard);

            Assert.IsTrue(m_stateMachine.FocusNext()); // Before
            Assert.IsTrue(m_stateMachine.FocusNext()); // 1
            for (int i = 0; i < 6; i++)
            {
                m_stateMachine.FocusNext();
                Assert.IsTrue(m_stateMachine.HasFocus, $"step {i + 3}");
            }
        }

        [Test]
        public void Arrows_MoveByPosition_AndKeepFocusAtAnEdge()
        {
            Load("Grid");
            Assert.IsTrue(m_stateMachine.FocusNext());
            AABB one = Bounds();

            Assert.IsTrue(m_stateMachine.FocusRight());
            Assert.Greater(Bounds().minX, one.minX);
            Assert.AreEqual(one.minY, Bounds().minY, 0.01f);

            Assert.IsTrue(m_stateMachine.FocusDown());
            Assert.Greater(Bounds().minY, one.minY);

            Assert.IsTrue(m_stateMachine.FocusLeft());
            Assert.IsTrue(m_stateMachine.FocusUp());
            AssertSameBounds(one, Bounds());

            // Nothing above: the move fails but focus stays, so the host decides whether to leave.
            Assert.IsFalse(m_stateMachine.FocusUp());
            Assert.IsTrue(m_stateMachine.HasFocus);
            AssertSameBounds(one, Bounds());
        }

        [Test]
        public void Arrows_DoNothingBeforeANodeIsFocused()
        {
            Load("Grid");

            Assert.IsFalse(m_stateMachine.FocusRight());
            Assert.IsFalse(m_stateMachine.HasFocus);
        }

        [Test]
        public void ClearFocus_RunsTheBlur()
        {
            Load("Buttons");
            for (int i = 0; i < 4; i++)
            {
                Assert.IsTrue(m_stateMachine.FocusNext());
            }
            m_stateMachine.Advance(0f);
            ViewModelInstanceBooleanProperty lastFocused =
                m_stateMachine.ViewModelInstance.GetProperty<ViewModelInstanceBooleanProperty>("p4/focused");
            Assert.IsNotNull(lastFocused, "p4/focused should exist");
            Assert.IsTrue(lastFocused.Value);

            m_stateMachine.ClearFocus();
            m_stateMachine.Advance(0f);

            Assert.IsFalse(m_stateMachine.HasFocus);
            Assert.IsFalse(lastFocused.Value);
        }

        [UnityTest]
        public IEnumerator Handle_WalksTheRow_WithoutWaiting()
        {
            Future<FileHandle> load = FileHandle.LoadAsync(m_asset);
            yield return WaitFor(load);
            FileHandle file = Track(load.Result);
            ArtboardHandle artboard = Track(file.InstantiateArtboard("Buttons"));
            StateMachineHandle stateMachine = Track(artboard.InstantiateStateMachine());
            stateMachine.BindViewModelInstanceAsync(null);
            stateMachine.Advance(0f);

            Future<bool> hasNodes = stateMachine.HasFocusNodesAsync();
            yield return WaitFor(hasNodes);
            Assert.IsTrue(hasNodes.Result);

            for (int i = 0; i < 4; i++)
            {
                Future<bool> moved = stateMachine.FocusNextAsync();
                yield return WaitFor(moved);
                Assert.IsTrue(moved.Result, $"step {i + 1}");
            }

            Future<bool> offTheEnd = stateMachine.FocusNextAsync();
            Future<bool> hasFocus = stateMachine.HasFocusAsync();
            yield return WaitFor(hasFocus);
            Assert.IsFalse(offTheEnd.Result);
            Assert.IsFalse(hasFocus.Result);

            Future<bool> back = stateMachine.FocusPreviousAsync();
            yield return WaitFor(back);
            Assert.IsTrue(back.Result);

            stateMachine.ClearFocus();
            hasFocus = stateMachine.HasFocusAsync();
            yield return WaitFor(hasFocus);
            Assert.IsFalse(hasFocus.Result);
        }

        [Test]
        public void Typing_ReachesAFocusedTextInput()
        {
            Load("TextInput");
            Assert.IsFalse(m_stateMachine.ExpectsKeyboardInput);
            Assert.IsFalse(m_stateMachine.TextInput("lost"), "nothing has focus to take it");

            Assert.IsTrue(m_stateMachine.FocusNext());
            Assert.IsTrue(m_stateMachine.ExpectsKeyboardInput);
            Assert.IsTrue(m_stateMachine.TextInput("hix"));
            Assert.IsTrue(m_stateMachine.KeyInput(RiveKey.Backspace, RiveKeyModifiers.None, pressed: true));
            m_stateMachine.KeyInput(RiveKey.Backspace, RiveKeyModifiers.None, pressed: false);
            m_stateMachine.Advance(0f);

            Assert.AreEqual("hi", TextValue());
        }

        [Test]
        public void SelectedText_ReadsWhatSelectAllPicked()
        {
            Load("TextInput");
            Assert.IsTrue(m_stateMachine.FocusNext());
            m_stateMachine.TextInput("hi");
            Assert.AreEqual("", m_stateMachine.SelectedText);

            PressAndRelease(RiveKey.A, ShortcutModifier);

            Assert.AreEqual("hi", m_stateMachine.SelectedText);
        }

        [UnityTest]
        public IEnumerator Handle_TypesIntoAFocusedTextInput()
        {
            Future<FileHandle> load = FileHandle.LoadAsync(m_asset);
            yield return WaitFor(load);
            FileHandle file = Track(load.Result);
            ArtboardHandle artboard = Track(file.InstantiateArtboard("TextInput"));
            StateMachineHandle stateMachine = Track(artboard.InstantiateStateMachine());
            stateMachine.BindViewModelInstanceAsync(null);
            stateMachine.Advance(0f);

            Future<bool> moved = stateMachine.FocusNextAsync();
            Future<bool> expects = stateMachine.ExpectsKeyboardInputAsync();
            Future<bool> typed = stateMachine.TextInputAsync("hi");
            stateMachine.KeyInputAsync(RiveKey.A, ShortcutModifier, pressed: true);
            stateMachine.KeyInputAsync(RiveKey.A, ShortcutModifier, pressed: false);
            Future<string> selected = stateMachine.SelectedTextAsync();
            yield return WaitFor(selected);

            Assert.IsTrue(moved.Result);
            Assert.IsTrue(expects.Result);
            Assert.IsTrue(typed.Result);
            Assert.AreEqual("hi", selected.Result);
        }

        [Test]
        public void DisposedStateMachine_IgnoresKeysAndText()
        {
            Load("TextInput");
            m_stateMachine.Dispose();

            Assert.IsFalse(m_stateMachine.KeyInput(RiveKey.A, RiveKeyModifiers.None, pressed: true));
            Assert.IsFalse(m_stateMachine.TextInput("hi"));
            Assert.IsFalse(m_stateMachine.ExpectsKeyboardInput);
            Assert.AreEqual("", m_stateMachine.SelectedText);
        }

        // Cmd, or Ctrl on Windows, like core's text fields.
        private static RiveKeyModifiers ShortcutModifier =>
            SystemInfo.operatingSystemFamily == OperatingSystemFamily.Windows ? RiveKeyModifiers.Ctrl : RiveKeyModifiers.Meta;

        private void PressAndRelease(RiveKey key, RiveKeyModifiers modifiers)
        {
            m_stateMachine.KeyInput(key, modifiers, pressed: true);
            m_stateMachine.KeyInput(key, modifiers, pressed: false);
        }

        private string TextValue()
        {
            ViewModelInstanceStringProperty text =
                m_stateMachine.ViewModelInstance.GetProperty<ViewModelInstanceStringProperty>("text");
            Assert.IsNotNull(text, "text should exist");
            return text.Value;
        }

        private void Load(string artboardName)
        {
            Artboard artboard = m_file.Artboard(artboardName);
            Assert.IsNotNull(artboard, $"{artboardName} artboard should exist");
            m_stateMachine = artboard.StateMachine();
            Assert.IsNotNull(m_stateMachine);
            ViewModel viewModel = artboard.DefaultViewModel;
            if (viewModel != null)
            {
                m_stateMachine.BindViewModelInstance(viewModel.CreateDefaultInstance());
            }
            // Traversal skips focus targets that are still hidden, so settle visibility first.
            m_stateMachine.Advance(0f);
            Assert.IsTrue(m_stateMachine.HasFocusNodes);
            Assert.IsFalse(m_stateMachine.HasFocus);
        }

        private AABB Bounds()
        {
            Assert.IsTrue(m_stateMachine.TryGetPrimaryFocusBounds(out AABB bounds), "the focused node should have bounds");
            return bounds;
        }

        private static void AssertSameBounds(AABB expected, AABB actual)
        {
            Assert.AreEqual(expected.minX, actual.minX, 0.01f);
            Assert.AreEqual(expected.minY, actual.minY, 0.01f);
            Assert.AreEqual(expected.maxX, actual.maxX, 0.01f);
            Assert.AreEqual(expected.maxY, actual.maxY, 0.01f);
        }

        private T Track<T>(T disposable) where T : IDisposable
        {
            m_toDispose.Add(disposable);
            return disposable;
        }

        private static IEnumerator WaitFor<T>(Future<T> future)
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!future.IsDone)
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, "Rive didn't answer within 10 seconds.");
                yield return null;
            }
            Assert.AreEqual(FutureStatus.Succeeded, future.Status, future.Exception?.ToString());
        }
    }
}
