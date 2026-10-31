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
    /// A panel sending the frame's gamepads to its widget, with the pads supplied by the
    /// test instead of the Input System. gamepad_inputs_test.riv moves focus on a right
    /// shoulder press.
    /// </summary>
    [TestFixture(ThreadingMode.MainThread)]
    [TestFixture(ThreadingMode.BackgroundThread)]
    public class RivePanelGamepadTests
    {
        private const int DeviceId = 3;

        private readonly ThreadingMode m_threadingMode;

        private TestAssetLoadingManager m_assetLoader;
        private MockLogger m_mockLogger;
        private Canvas m_canvas;
        private GameObject m_camera;
        private GameObject m_eventSystemObject;
        private RivePanel m_panel;
        private RiveWidget m_widget;
        private Asset m_asset;
        private float m_rightShoulder;

        public RivePanelGamepadTests(ThreadingMode threadingMode)
        {
            m_threadingMode = threadingMode;
        }

        private FocusState WidgetFocus => ((IRiveFocusWidget)m_widget).FocusState;

        private CanvasFocusInputProvider FocusProvider => m_panel.GetComponent<CanvasFocusInputProvider>();

        [UnitySetUp]
        public IEnumerator Setup()
        {
            m_mockLogger = new MockLogger();
            DebugLogger.Instance = m_mockLogger;
            m_assetLoader = new TestAssetLoadingManager();
            m_rightShoulder = 0f;
            GamepadInputModule.ReadForTests = frame =>
            {
                GamepadFrame.Pad pad = frame.Add(DeviceId);
                Array.Clear(pad.Buttons, 0, pad.Buttons.Length);
                Array.Clear(pad.Axes, 0, pad.Axes.Length);
                pad.Buttons[(int)RiveGamepadButton.RightShoulder] = m_rightShoulder;
            };

            m_camera = new GameObject("GamepadCamera", typeof(Camera));

            var canvasObj = new GameObject("GamepadCanvas");
            m_canvas = canvasObj.AddComponent<Canvas>();
            m_canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<GraphicRaycaster>();

            m_eventSystemObject = new GameObject("EventSystem");
            m_eventSystemObject.AddComponent<EventSystem>();
            m_eventSystemObject.AddComponent<TestInputModule>();

            m_panel = RivePanelTestUtils.CreatePanel("GamepadPanel");
            m_panel.ThreadingMode = m_threadingMode;
            m_panel.transform.SetParent(m_canvas.transform, false);
            m_panel.SetDimensions(new Vector2(640f, 360f));
            m_panel.gameObject.AddComponent<RiveCanvasRenderer>();

            m_widget = RivePanelTestUtils.CreateWidget<RiveWidget>("GamepadWidget");
            m_widget.Fit = Fit.Fill;
            m_panel.AddToHierarchy(m_widget);
            RivePanelTestUtils.MakeWidgetFillPanel(m_widget);

            yield return m_assetLoader.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_gamepad_inputs_test,
                a => m_asset = a,
                () => Assert.Fail("Failed to load gamepad_inputs_test.riv"));
            m_widget.Load(m_asset);
            yield return RivePanelTestUtils.WaitForLoaded(m_widget);
            yield return null;
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            GamepadInputModule.ReadForTests = null;
            UnityEngine.Object.Destroy(m_eventSystemObject);
            UnityEngine.Object.Destroy(m_canvas.gameObject);
            UnityEngine.Object.Destroy(m_camera);
            m_assetLoader?.UnloadAllAssets();
        }

        [UnityTest]
        public IEnumerator Always_SendsWithoutTheSelection()
        {
            FocusState before = WidgetFocus;

            yield return PressAndRelease();

            yield return WaitUntil(() => FocusMoved(before), "the right shoulder to move focus");
        }

        [UnityTest]
        public IEnumerator WhenSelected_WaitsForTheSelection()
        {
            m_panel.GamepadInput = GamepadInputMode.WhenSelected;
            FocusState before = WidgetFocus;

            yield return PressAndRelease();
            yield return Frames(5);
            Assert.IsFalse(FocusMoved(before), "an unselected panel shouldn't hear the pad");

            EventSystem.current.SetSelectedGameObject(FocusProvider.gameObject);
            yield return null;
            before = WidgetFocus;
            yield return PressAndRelease();

            yield return WaitUntil(() => FocusMoved(before), "the right shoulder to move focus once selected");
        }

        [UnityTest]
        public IEnumerator Disabled_SendsNothing()
        {
            m_panel.GamepadInput = GamepadInputMode.Disabled;
            FocusState before = WidgetFocus;

            yield return PressAndRelease();
            yield return Frames(5);

            Assert.IsFalse(FocusMoved(before));
        }

        [UnityTest]
        public IEnumerator ScriptApi_KnowsWhatThePanelSent()
        {
            m_rightShoulder = 1f;
            yield return Frames(2);

            // A handle with its own tracker would announce the pad again, which puts
            // the held right shoulder back at rest.
            GamepadTracker scripts = m_threadingMode == ThreadingMode.BackgroundThread
                ? m_widget.StateMachineHandle.Gamepads
                : m_widget.StateMachine.Gamepads;
            Assert.IsNull(scripts.SetButton(DeviceId, RiveGamepadButton.RightShoulder, 1f));
        }

        private IEnumerator PressAndRelease()
        {
            m_rightShoulder = 1f;
            yield return Frames(2);
            m_rightShoulder = 0f;
            yield return Frames(2);
        }

        private bool FocusMoved(FocusState before)
        {
            FocusState now = WidgetFocus;
            if (now.HasFocus != before.HasFocus)
            {
                return true;
            }
            // The file nests its stops, so a move can keep the top left corner.
            return now.HasFocus && now.HasBounds && before.HasBounds &&
                   (Mathf.Abs(now.Bounds.minX - before.Bounds.minX) > 0.01f ||
                    Mathf.Abs(now.Bounds.minY - before.Bounds.minY) > 0.01f ||
                    Mathf.Abs(now.Bounds.maxX - before.Bounds.maxX) > 0.01f ||
                    Mathf.Abs(now.Bounds.maxY - before.Bounds.maxY) > 0.01f);
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
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
    }
}
