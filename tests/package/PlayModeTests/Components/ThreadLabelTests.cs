using System.Collections;
using System.Threading;
using NUnit.Framework;
using Rive.Components;
using Rive.Host;
using Rive.Tests.Utils;
using Rive.Utils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Rive.Tests
{
    /// <summary>
    /// Runs a panel through loading, drawing, data binding and a resize, then
    /// checks nothing ran off the thread its method is labelled for. The
    /// checks only count, so these tests are where a wrong label shows up.
    /// </summary>
    public class ThreadLabelTests
    {
        private const string ArtboardName = "Artboard";
        private const string StateMachineName = "State Machine 1";

        private TestAssetLoadingManager m_assetLoader;
        private File m_file;
        private RivePanel m_panel;
        private RiveWidget m_widget;
        private GameObject m_cameraObject;

        [SetUp]
        public void SetUp()
        {
            DebugLogger.Instance = new MockLogger();
            m_assetLoader = new TestAssetLoadingManager();

            // Nothing draws without a camera rendering.
            m_cameraObject = new GameObject("Camera");
            m_cameraObject.AddComponent<Camera>().tag = "MainCamera";

            m_panel = RivePanelTestUtils.CreatePanel("ThreadLabelPanel");
            m_panel.SetDimensions(new Vector2(256, 256));
            m_widget = RivePanelTestUtils.CreateWidget<RiveWidget>();
            m_widget.transform.SetParent(m_panel.WidgetContainer, false);
            RivePanelTestUtils.MakeWidgetFillPanel(m_widget);
        }

        [TearDown]
        public void TearDown()
        {
            if (m_widget != null)
            {
                Object.Destroy(m_widget.gameObject);
            }
            if (m_panel != null)
            {
                Object.Destroy(m_panel.gameObject);
            }
            if (m_cameraObject != null)
            {
                Object.Destroy(m_cameraObject);
            }
            m_file?.Dispose();
            m_file = null;
            m_assetLoader.UnloadAllAssets();
        }

        private IEnumerator LoadDataBindingFile(ThreadingMode mode)
        {
            m_panel.ThreadingMode = mode;
            Asset asset = null;
            yield return m_assetLoader.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_asset_databinding_test,
                loaded => asset = loaded,
                () => Assert.Fail($"Failed to load {TestAssetReferences.riv_asset_databinding_test}"));
            m_file = File.Load(asset);
            m_widget.Load(m_file, ArtboardName, StateMachineName);
            yield return RivePanelTestUtils.WaitForLoaded(m_widget);
        }

        private NumberPropertyHandle AgeProperty()
        {
            var instance = m_widget.StateMachineHandle.GetViewModelInstance();
            Assert.IsNotNull(instance, "The widget should have a bound instance.");
            return instance.GetNumberProperty("age");
        }

        private static IEnumerator RenderedFrames(int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                yield return null;
                yield return new WaitForEndOfFrame();
            }
        }

        private static void ResetCounts()
        {
            HostNative.riveResetThreadViolations();
            ThreadCheck.Reset();
        }

        private static void AssertStayedOnTheirThreads()
        {
            HostNative.riveGetThreadViolations(out ThreadViolations v);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Assert.AreEqual(0, ThreadCheck.MainViolations,
                $"Calls expected the main thread. First was '{ThreadCheck.FirstMainOffender}'.");
            Assert.Greater(ThreadCheck.MainCalls, 0, "No main thread checks ran, so this proved nothing.");
#endif
            Assert.AreEqual(0u, v.MainViolations,
                $"Native calls expected the main thread. First was '{v.FirstMainOffender}'.");
            Assert.AreEqual(0u, v.ServerViolations,
                $"Native calls expected the server thread. First was '{v.FirstServerOffender}'.");
            // So a pass shows whether the render check ran on this runner.
            bool checksRender = HasOneRenderThread();
            Debug.Log($"[ThreadLabelTests] {SystemInfo.graphicsDeviceType}, " +
                $"{SystemInfo.renderingThreadingMode}, render check ran: {checksRender}, " +
                $"render calls: {v.RenderCalls}");
            if (checksRender)
            {
                Assert.AreEqual(0u, v.RenderViolations,
                    $"Native calls expected the render thread. First was '{v.FirstRenderOffender}'.");
            }
            Assert.Greater(v.ServerCalls, 0u, "No server checks ran, so this proved nothing.");
        }

        // Graphics jobs can run render events on worker threads, where a check
        // against one render thread doesn't hold.
        private static bool HasOneRenderThread()
        {
            switch (SystemInfo.renderingThreadingMode)
            {
                case RenderingThreadingMode.Direct:
                case RenderingThreadingMode.SingleThreaded:
                case RenderingThreadingMode.MultiThreaded:
                    return true;
                default:
                    return false;
            }
        }

        [UnityTest]
        public IEnumerator Panel_StaysOnItsThreads(
            [Values(ThreadingMode.MainThread, ThreadingMode.BackgroundThread)] ThreadingMode mode)
        {
            yield return LoadDataBindingFile(mode);
            yield return RenderedFrames(3);
            ResetCounts();

            // Main thread panels bind through the view model instance, and
            // background ones through handles.
            if (mode == ThreadingMode.BackgroundThread)
            {
                var age = AgeProperty();
                age.Subscribe(_ => { });
                age.SetValue(41f);
                yield return RenderedFrames(3);
                Future<float> read = age.GetValueAsync();
                yield return RenderedFrames(5);
                Assert.IsTrue(read.IsDone, "The read should have landed.");
            }
            else
            {
                var age = m_widget.StateMachine.ViewModelInstance.GetNumberProperty("age");
                age.OnValueChanged += _ => { };
                age.Value = 41f;
                yield return RenderedFrames(3);
                age.Value = 42f;
                yield return RenderedFrames(5);
            }

            // A new size hands a new texture to the render thread.
            m_panel.SetDimensions(new Vector2(300, 200));
            yield return RenderedFrames(5);

            AssertStayedOnTheirThreads();
        }

        // The managed checks only exist in the editor and development builds.
        [NeedsManagedThreads]
        [UnityTest]
        public IEnumerator CallsFromABackgroundThread_AreCountedAndWarnOncePerApi()
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            Assert.Ignore("Release builds compile the managed thread checks out.");
#endif
            yield return LoadDataBindingFile(ThreadingMode.BackgroundThread);
            yield return RenderedFrames(3);
            var age = AgeProperty();
            var core = m_widget.Core;
            Assert.IsNotNull(core, "The widget should have a core once loaded.");
            var logger = (MockLogger)DebugLogger.Instance;
            ResetCounts();
            logger.Clear();

            // Handles and widgets are main thread only, so this is the misuse
            // the checks exist for. One API from each runtime assembly, twice.
            // Joined, so nothing on the main thread runs alongside it.
            var caller = new Thread(() =>
            {
                for (int i = 0; i < 2; i++)
                {
                    age.Subscribe(_ => { });
                    core.TryTakeEvent(out _);
                }
            });
            caller.Start();
            caller.Join();

            Assert.GreaterOrEqual(ThreadCheck.MainViolations, 4, "Each off-thread call should have been counted.");
#if UNITY_EDITOR
            var warnings = logger.LoggedWarnings.FindAll(w => w.Contains("called off the main thread"));
            Assert.AreEqual(2, warnings.Count, "Each API should warn once. Got: " + string.Join(" | ", warnings));
            Assert.IsTrue(warnings.Exists(w => w.StartsWith("ViewModelPropertyHandle.Subscribe ")),
                "The handle warning should name the method the script called.");
            Assert.IsTrue(warnings.Exists(w => w.StartsWith("WidgetCore.TryTakeEvent ")),
                "The component warning should name the component method, not the checker.");
#endif
            yield return RenderedFrames(3);
        }
    }
}
