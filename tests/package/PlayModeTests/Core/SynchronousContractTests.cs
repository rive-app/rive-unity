using System.Collections;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using Rive.Components;
using Rive.Tests.Utils;
using Rive.Utils;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rive.Tests
{
    /// <summary>
    /// Checks the sync API is done by the time it returns. Asset loading is
    /// covered by OutOfBandAssetTests and friends.
    /// </summary>
    public class SynchronousContractTests
    {
        private TestAssetLoadingManager m_assetLoader;
        private MockLogger m_mockLogger;
        private readonly List<File> m_files = new List<File>();
        private readonly List<GameObject> m_objects = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            m_mockLogger = new MockLogger();
            DebugLogger.Instance = m_mockLogger;
            m_assetLoader = new TestAssetLoadingManager();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in m_objects)
            {
                if (go != null)
                {
                    Object.Destroy(go);
                }
            }
            m_objects.Clear();

            foreach (var file in m_files)
            {
                if (file != null && !file.IsDisposed)
                {
                    file.Dispose();
                }
            }
            m_files.Clear();

            m_assetLoader.UnloadAllAssets();
        }

        private File TrackFile(File file)
        {
            m_files.Add(file);
            return file;
        }

        private GameObject Track(GameObject go)
        {
            m_objects.Add(go);
            return go;
        }

        private IEnumerator LoadAsset(string path, System.Action<Asset> onLoaded)
        {
            return m_assetLoader.LoadAssetCoroutine<Asset>(
                path,
                onLoaded,
                () => Assert.Fail($"Failed to load asset at {path}"));
        }

        [UnityTest]
        public IEnumerator FileLoad_ProducesUsableObjectsBeforeReturning()
        {
            Asset asset = null;
            yield return LoadAsset(TestAssetReferences.riv_asset_databinding_test,
                                   a => asset = a);

            // No yields past this point, that's what's being checked.
            File file = TrackFile(File.Load(asset));
            Assert.IsNotNull(file, "Load should return a file.");
            Assert.Greater(file.ArtboardCount, 0u, "Artboard count should be readable now.");

            Artboard artboard = file.Artboard(0u);
            Assert.IsNotNull(artboard, "Artboard should exist when Artboard() returns.");
            Assert.Greater(artboard.Width, 0f, "Artboard size should be readable now.");

            StateMachine stateMachine = artboard.StateMachine();
            Assert.IsNotNull(stateMachine, "StateMachine should exist when StateMachine() returns.");
        }

        [UnityTest]
        public IEnumerator FileLoad_ExposesMetadataImmediately()
        {
            Asset asset = null;
            yield return LoadAsset(TestAssetReferences.riv_asset_databinding_test,
                                   a => asset = a);

            File file = TrackFile(File.Load(asset));

            Assert.IsNotNull(file.ArtboardName(0), "Artboard name should be readable now.");
            Assert.IsNotNull(file.ViewModels, "View models should be readable now.");
            Assert.Greater(file.ViewModelCount, 0, "This asset has view models.");
        }

        [UnityTest]
        public IEnumerator PropertySet_IsVisibleToAnImmediateGet()
        {
            Asset asset = null;
            yield return LoadAsset(TestAssetReferences.riv_asset_databinding_test,
                                   a => asset = a);

            File file = TrackFile(File.Load(asset));
            Artboard artboard = file.Artboard(0u);
            StateMachine stateMachine = artboard.StateMachine();

            ViewModelInstance instance = stateMachine.ViewModelInstance;
            if (instance == null)
            {
                ViewModel viewModel = file.ViewModels.Count > 0 ? file.ViewModels[0] : null;
                Assert.IsNotNull(viewModel, "Expected a view model.");
                instance = viewModel.CreateDefaultInstance() ?? viewModel.CreateInstance();
                Assert.IsNotNull(instance, "Expected a view model instance.");
                stateMachine.BindViewModelInstance(instance);
            }

            ViewModelInstanceNumberProperty number = FirstNumberProperty(file, instance);
            Assert.IsNotNull(number, "Expected a number property.");

            float target = number.Value + 3f;
            number.Value = target;

            Assert.AreEqual(target, number.Value, 0.0001f,
                            "A set should be visible to a get in the same call.");
        }

        [UnityTest]
        public IEnumerator ManualTick_LeavesEverythingUsable()
        {
            Asset asset = null;
            yield return LoadAsset(TestAssetReferences.riv_asset_databinding_test,
                                   a => asset = a);

            RivePanel panel = RivePanelTestUtils.CreatePanel();
            Track(panel.gameObject);
            panel.UpdateMode = RivePanel.PanelUpdateMode.Manual;

            RiveWidget widget = RivePanelTestUtils.CreateWidget<RiveWidget>();
            widget.transform.SetParent(panel.WidgetContainer, false);
            RivePanelTestUtils.MakeWidgetFillPanel(widget);

            File file = TrackFile(File.Load(asset));
            widget.Load(file);
            yield return RivePanelTestUtils.WaitForLoaded(widget);

            StateMachine stateMachine = widget.StateMachine;
            Assert.IsNotNull(stateMachine, "Loaded widget should have a state machine.");

            // Settle before measuring.
            panel.Tick(1f / 60f);
            yield return null;

            panel.Tick(1f / 60f);
            yield return null;

            Assert.AreEqual(WidgetStatus.Loaded, widget.Status,
                            "Tick shouldn't leave the widget mid-flight.");
            Assert.IsFalse(stateMachine.IsDisposed, "State machine should survive the tick.");
            Assert.AreSame(stateMachine, widget.StateMachine,
                           "Tick shouldn't swap the state machine out.");
            Assert.IsFalse(m_mockLogger.AnyLogTypeContains("error"),
                           "Tick shouldn't log errors.");
        }

        /// <summary>
        /// Tick queues rather than advancing at the call site. Called from a
        /// test coroutine it lands in the next frame's pass, and one yield
        /// gets past that pass but not past the render.
        /// </summary>
        [UnityTest]
        public IEnumerator ManualTick_LandsBeforeTheFrameRenders()
        {
            Asset asset = null;
            yield return LoadAsset(TestAssetReferences.riv_asset_databinding_test,
                                   a => asset = a);

            RivePanel panel = RivePanelTestUtils.CreatePanel();
            Track(panel.gameObject);
            panel.UpdateMode = RivePanel.PanelUpdateMode.Manual;

            RiveWidget widget = RivePanelTestUtils.CreateWidget<RiveWidget>();
            widget.transform.SetParent(panel.WidgetContainer, false);
            RivePanelTestUtils.MakeWidgetFillPanel(widget);

            File file = TrackFile(File.Load(asset));
            widget.Load(file);
            yield return RivePanelTestUtils.WaitForLoaded(widget);
            yield return null;

            ViewModelInstance instance = widget.StateMachine.ViewModelInstance;
            Assert.IsNotNull(instance, "Expected a bound view model instance.");

            ViewModelInstanceNumberProperty number = FirstNumberProperty(file, instance);
            Assert.IsNotNull(number, "Expected a number property.");

            int callbacks = 0;
            number.OnValueChanged += _ => callbacks++;

            number.Value = number.Value + 1f;
            panel.Tick(1f / 60f);

            Assert.AreEqual(0, callbacks, "Nothing should have run at the call site.");

            // Resumes after every Update and before LateUpdate, so the pass
            // and its callback flush have both happened and nothing has
            // rendered yet.
            yield return null;

            Assert.AreEqual(1, callbacks, "A queued tick should land before the render.");
        }

        [UnityTest]
        public IEnumerator PointerDownAndUpInOneFrame_BothLand()
        {
            Asset asset = null;
            yield return LoadAsset(TestAssetReferences.riv_asset_databinding_test,
                                   a => asset = a);

            RivePanel panel = RivePanelTestUtils.CreatePanel();
            Track(panel.gameObject);
            panel.SetDimensions(new Vector2(500, 500));

            RiveWidget widget = RivePanelTestUtils.CreateWidget<RiveWidget>();
            widget.transform.SetParent(panel.WidgetContainer, false);
            RivePanelTestUtils.MakeWidgetFillPanel(widget);

            File file = TrackFile(File.Load(asset));
            widget.Load(file);
            yield return RivePanelTestUtils.WaitForLoaded(widget);
            yield return null;

            var centre = new Vector2(0.5f, 0.5f);

            // Both in one frame, no yield between them.
            widget.OnPointerDown(centre, 0);
            widget.OnPointerUp(centre, 0);

            Assert.AreEqual(WidgetStatus.Loaded, widget.Status,
                            "Down then up in one frame shouldn't disturb the widget.");
            Assert.IsFalse(widget.StateMachine.IsDisposed,
                           "State machine should survive down then up.");
            Assert.IsFalse(m_mockLogger.AnyLogTypeContains("error"),
                           "Down then up shouldn't log errors.");

            // Still answers, so nothing was left half done.
            widget.HitTest(centre);
        }

        [UnityTest]
        public IEnumerator HitTest_AnswersSynchronously()
        {
            Asset asset = null;
            yield return LoadAsset(TestAssetReferences.riv_asset_databinding_test,
                                   a => asset = a);

            File file = TrackFile(File.Load(asset));
            Artboard artboard = file.Artboard(0u);
            StateMachine stateMachine = artboard.StateMachine();
            stateMachine.Advance(0f);

            // Nothing changes in between, so both should match.
            bool first = stateMachine.HitTest(new Vector2(0.5f, 0.5f));
            bool second = stateMachine.HitTest(new Vector2(0.5f, 0.5f));
            Assert.AreEqual(first, second, "HitTest should answer from current state.");
        }

        [UnityTest]
        public IEnumerator PropertyCallback_RunsOnMainThread()
        {
            Asset asset = null;
            yield return LoadAsset(TestAssetReferences.riv_asset_databinding_test,
                                   a => asset = a);

            RivePanel panel = RivePanelTestUtils.CreatePanel();
            Track(panel.gameObject);

            RiveWidget widget = RivePanelTestUtils.CreateWidget<RiveWidget>();
            widget.transform.SetParent(panel.WidgetContainer, false);
            RivePanelTestUtils.MakeWidgetFillPanel(widget);

            File file = TrackFile(File.Load(asset));
            widget.Load(file);
            yield return RivePanelTestUtils.WaitForLoaded(widget);

            ViewModelInstance instance = widget.StateMachine.ViewModelInstance;
            Assert.IsNotNull(instance, "Expected a bound view model instance.");

            ViewModelInstanceNumberProperty number = FirstNumberProperty(file, instance);
            Assert.IsNotNull(number, "Expected a number property.");

            int mainThreadId = Thread.CurrentThread.ManagedThreadId;
            int callbackThreadId = -1;
            int callbacks = 0;

            number.OnValueChanged += _ =>
            {
                callbackThreadId = Thread.CurrentThread.ManagedThreadId;
                callbacks++;
            };

            number.Value = number.Value + 1f;

            yield return null;
            yield return null;

            Assert.AreEqual(1, callbacks, "One set should give one callback.");
            Assert.AreEqual(mainThreadId, callbackThreadId,
                            "Callbacks should arrive on the main thread.");
        }

        [NeedsManagedThreads]
        [NeedsRiveThread]
        [UnityTest]
        public IEnumerator FileLoad_WorksFromABackgroundThread()
        {
            Asset asset = null;
            yield return LoadAsset(TestAssetReferences.riv_asset_databinding_test,
                                   a => asset = a);

            byte[] bytes = asset.Bytes;
            Assert.IsNotNull(bytes, "Expected raw bytes on the asset.");

            File fromWorker = null;
            System.Exception failure = null;

            var thread = new Thread(() =>
            {
                try
                {
                    // Distinct cache id, so this is a real import.
                    fromWorker = File.Load(bytes, 987654);
                }
                catch (System.Exception e)
                {
                    failure = e;
                }
            });
            thread.Start();

            while (thread.IsAlive)
            {
                yield return null;
            }

            Assert.IsNull(failure, $"Loading off the main thread threw: {failure}");
            Assert.IsNotNull(fromWorker, "Load should work off the main thread.");

            TrackFile(fromWorker);
            Assert.Greater(fromWorker.ArtboardCount, 0u, "The file should be usable.");
        }

        [NeedsManagedThreads]
        [NeedsRiveThread]
        [UnityTest]
        public IEnumerator NativeUsageGuard_ReadableOffMainThread()
        {
#if UNITY_EDITOR
            System.Exception caught = null;
            bool value = false;

            var thread = new Thread(() =>
            {
                try
                {
                    value = NativeUsageGuard.IsNativeAvailable;
                }
                catch (System.Exception e)
                {
                    caught = e;
                }
            });
            thread.Start();
            while (thread.IsAlive)
            {
                yield return null;
            }

            Assert.IsNull(caught, $"Guard threw off the main thread: {caught}");
            Assert.IsTrue(value, "Native should read as available here.");
#else
            yield return null;
            Assert.Pass("Players don't read SessionState.");
#endif
        }

        // The instance doesn't expose its view model, so try each number
        // property until one resolves.
        private static ViewModelInstanceNumberProperty FirstNumberProperty(
            File file, ViewModelInstance instance)
        {
            foreach (ViewModel viewModel in file.ViewModels)
            {
                foreach (ViewModelPropertyData property in viewModel.Properties)
                {
                    if (property.Type != ViewModelDataType.Number)
                    {
                        continue;
                    }
                    var found =
                        instance.GetProperty<ViewModelInstanceNumberProperty>(property.Name);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }
            return null;
        }
    }
}
