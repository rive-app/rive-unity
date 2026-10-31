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
    /// Gamepad values through the state machine and handle APIs, and the batches they send.
    /// gamepad_inputs_test.riv is core's: a right shoulder press moves focus.
    /// </summary>
    public class GamepadInputTests
    {
        private const int DeviceId = 7;

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
                TestAssetReferences.riv_gamepad_inputs_test,
                asset => m_asset = asset,
                () => Assert.Fail("Failed to load gamepad_inputs_test.riv"));
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
        public void RightShoulder_MovesFocus()
        {
            Load();
            FocusState before = m_stateMachine.FocusState;

            PressAndRelease(RiveGamepadButton.RightShoulder);

            AssertFocusMoved(before, m_stateMachine.FocusState);
        }

        [Test]
        public void ButtonsWithoutAListener_LeaveFocusAlone()
        {
            Load();
            FocusState before = m_stateMachine.FocusState;

            PressAndRelease(RiveGamepadButton.North);

            AssertSameFocus(before, m_stateMachine.FocusState);
        }

        [Test]
        public void DisposedStateMachine_IgnoresGamepads()
        {
            Load();
            m_stateMachine.Dispose();

            Assert.DoesNotThrow(() => m_stateMachine.SetGamepadButton(DeviceId, RiveGamepadButton.South, 1f));
            Assert.DoesNotThrow(() => m_stateMachine.SetGamepadAxis(DeviceId, RiveGamepadAxis.LeftX, 1f));
            Assert.DoesNotThrow(() => m_stateMachine.DisconnectGamepad(DeviceId));
        }

        [UnityTest]
        public IEnumerator Handle_RightShoulder_MovesFocus()
        {
            Future<FileHandle> load = FileHandle.LoadAsync(m_asset);
            yield return WaitFor(load);
            FileHandle file = Track(load.Result);
            ArtboardHandle artboard = Track(file.InstantiateArtboard());
            StateMachineHandle stateMachine = Track(artboard.InstantiateStateMachine());
            stateMachine.BindViewModelInstanceAsync(null);
            stateMachine.Advance(0f);
            stateMachine.Advance(0.016f);
            Future<FocusState> before = ReadFocus(stateMachine);
            yield return WaitFor(before);

            stateMachine.SetGamepadButton(DeviceId, RiveGamepadButton.RightShoulder, 1f);
            stateMachine.Advance(0.016f);
            stateMachine.SetGamepadButton(DeviceId, RiveGamepadButton.RightShoulder, 0f);
            stateMachine.Advance(0.016f);
            Future<FocusState> after = ReadFocus(stateMachine);
            yield return WaitFor(after);

            AssertFocusMoved(before.Result, after.Result);
        }

        // The handle API has no bounds, so this asks the way the panel does.
        private static Future<FocusState> ReadFocus(StateMachineHandle stateMachine)
        {
            return StateMachineNative.FocusAsync(stateMachine.Native.Value, StateMachineNative.FocusOp.Query, result => result.State);
        }

        [Test]
        public void Tracker_AnnouncesAPadOnce_ThenSendsOnlyWhatChanged()
        {
            var tracker = new GamepadTracker();

            byte[] first = tracker.SetButton(DeviceId, RiveGamepadButton.South, 1f);
            // Version, connected with 17 buttons and 6 axes at rest, then one change.
            Assert.AreEqual(4 + (1 + 4 + 4 + 23 * 4) + (1 + 4 + 1 + 6), first.Length);
            Assert.AreEqual(0, first[4], "connected");
            Assert.AreEqual(1, first[4 + 101], "update");

            Assert.IsNull(tracker.SetButton(DeviceId, RiveGamepadButton.South, 1f), "nothing changed");

            byte[] axis = tracker.SetAxis(DeviceId, RiveGamepadAxis.LeftX, 0.5f);
            Assert.AreEqual(4 + 1 + 4 + 1 + 6, axis.Length);
            Assert.AreEqual(1, axis[4 + 5], "one change");
            Assert.AreEqual(1, axis[4 + 6], "an axis");
            Assert.AreEqual((byte)RiveGamepadAxis.LeftX, axis[4 + 7]);
            Assert.AreEqual(0.5f, BitConverter.ToSingle(axis, 4 + 8));
        }

        [Test]
        public void Tracker_PanelReleaseAndDisconnect_LeaveScriptPadsAlone()
        {
            var tracker = new GamepadTracker();
            var buttons = new float[GamepadTracker.ButtonCount];
            var axes = new float[GamepadTracker.AxisCount];
            buttons[(int)RiveGamepadButton.South] = 1f;
            Assert.IsNotNull(tracker.SetFromPanel(1, buttons, axes));
            Assert.IsNotNull(tracker.SetButton(2, RiveGamepadButton.South, 1f));

            byte[] release = tracker.ReleaseFromPanel();
            Assert.IsNotNull(release);
            Assert.AreEqual(4 + 1 + 4 + 1 + 6, release.Length, "only the panel's pad releases");
            Assert.IsNull(tracker.ReleaseFromPanel(), "already at rest");

            byte[] gone = tracker.DisconnectFromPanelBut(new HashSet<int>());
            Assert.AreEqual(4 + 1 + 4, gone.Length, "only the panel's pad disconnects");
            Assert.IsNull(tracker.SetButton(2, RiveGamepadButton.South, 1f), "the script's pad stayed");
        }

        [Test]
        public void Tracker_ScriptTakesOverAPanelPad()
        {
            var tracker = new GamepadTracker();
            var buttons = new float[GamepadTracker.ButtonCount];
            var axes = new float[GamepadTracker.AxisCount];
            buttons[(int)RiveGamepadButton.South] = 1f;
            Assert.IsNotNull(tracker.SetFromPanel(DeviceId, buttons, axes));
            Assert.IsNotNull(tracker.ReleaseFromPanel());

            byte[] script = tracker.SetButton(DeviceId, RiveGamepadButton.East, 1f);
            Assert.AreEqual(4 + 1 + 4 + 1 + 6, script.Length, "the pad was already announced");

            Assert.IsNull(tracker.ReleaseFromPanel(), "the panel shouldn't release the script's East");
            Assert.IsNull(tracker.DisconnectFromPanelBut(new HashSet<int>()), "the panel shouldn't disconnect the script's pad");
            Assert.IsNull(tracker.SetButton(DeviceId, RiveGamepadButton.East, 1f), "East is still held");
        }

        private void Load()
        {
            Artboard artboard = m_file.Artboard(0);
            Assert.IsNotNull(artboard);
            m_stateMachine = artboard.StateMachine();
            Assert.IsNotNull(m_stateMachine);
            ViewModel viewModel = artboard.DefaultViewModel;
            if (viewModel != null)
            {
                m_stateMachine.BindViewModelInstance(viewModel.CreateDefaultInstance());
            }
            m_stateMachine.Advance(0f);
            m_stateMachine.Advance(0.016f);
        }

        private void PressAndRelease(RiveGamepadButton button)
        {
            m_stateMachine.SetGamepadButton(DeviceId, button, 1f);
            m_stateMachine.Advance(0.016f);
            m_stateMachine.SetGamepadButton(DeviceId, button, 0f);
            m_stateMachine.Advance(0.016f);
        }

        private static void AssertFocusMoved(FocusState before, FocusState after)
        {
            Assert.IsTrue(after.HasFocus, "something should have focus");
            if (before.HasFocus)
            {
                Assert.IsFalse(SameBounds(before, after), "focus should have moved");
            }
        }

        private static void AssertSameFocus(FocusState before, FocusState after)
        {
            Assert.AreEqual(before.HasFocus, after.HasFocus);
            if (before.HasFocus)
            {
                Assert.IsTrue(SameBounds(before, after));
            }
        }

        private static bool SameBounds(FocusState a, FocusState b)
        {
            return Mathf.Abs(a.Bounds.minX - b.Bounds.minX) < 0.01f &&
                   Mathf.Abs(a.Bounds.minY - b.Bounds.minY) < 0.01f &&
                   Mathf.Abs(a.Bounds.maxX - b.Bounds.maxX) < 0.01f &&
                   Mathf.Abs(a.Bounds.maxY - b.Bounds.maxY) < 0.01f;
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
