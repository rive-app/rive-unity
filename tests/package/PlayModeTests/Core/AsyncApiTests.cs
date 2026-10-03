using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Rive.Components;
using Rive.Producer;
using Rive.Tests.Utils;
using Rive.Utils;
using UnityEngine;
using UnityEngine.TestTools;
using Rive.Host;

namespace Rive.Tests
{
    /// <summary>
    /// The async API. What it should give back, and where the continuation runs.
    /// </summary>
    public class AsyncApiTests
    {
        private TestAssetLoadingManager m_assetLoader;
        private MockLogger m_mockLogger;
        private readonly List<File> m_files = new List<File>();
        private readonly List<GameObject> m_objects = new List<GameObject>();
        private int m_mainThreadId;

        [SetUp]
        public void SetUp()
        {
            m_mockLogger = new MockLogger();
            DebugLogger.Instance = m_mockLogger;
            m_assetLoader = new TestAssetLoadingManager();
            m_mainThreadId = Thread.CurrentThread.ManagedThreadId;
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

        /// An operation only finishes at a main-thread drain, so a test has
        /// to let frames run rather than block on it.
        private static IEnumerator Await(System.Func<bool> isDone, System.Func<FutureStatus> status)
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!isDone())
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    Assert.Fail("The operation didn't finish within 10 seconds.");
                }
                yield return null;
            }

            Assert.AreEqual(FutureStatus.Succeeded, status(), "The operation should succeed.");
        }

        private static IEnumerator Await<T>(Future<T> operation)
        {
            return Await(() => operation.IsDone, () => operation.Status);
        }

        private static IEnumerator Await(Future operation)
        {
            return Await(() => operation.IsDone, () => operation.Status);
        }

        [UnityTest]
        public IEnumerator FileLoadAsync_GivesAUsableFile()
        {
            Asset asset = null;
            yield return LoadAsset(TestAssetReferences.riv_asset_databinding_test,
                                   a => asset = a);

            Future<File> load = File.Loader.LoadAsync(asset, null);
            yield return Await(load);

            File file = TrackFile(load.Result);
            Assert.IsNotNull(file, "LoadAsync should give back a file.");
            Assert.Greater(file.ArtboardCount, 0u, "The file should be usable.");
        }

        [NeedsRiveThread]
        [UnityTest]
        public IEnumerator FileLoadAsync_DoesNotBlockTheCaller()
        {
            Asset asset = null;
            yield return LoadAsset(TestAssetReferences.riv_asset_databinding_test,
                                   a => asset = a);

            int framesBeforeItFinished = 0;
            Future<File> load = File.Loader.LoadAsync(asset.Bytes, 424242, null);

            while (!load.IsDone)
            {
                framesBeforeItFinished++;
                if (framesBeforeItFinished > 600)
                {
                    Assert.Fail("Load didn't finish.");
                }
                yield return null;
            }

            TrackFile(load.Result);
            Assert.Greater(framesBeforeItFinished, 0,
                           "The caller should get its frame back before the import is done.");
        }

        [UnityTest]
        public IEnumerator FileLoadAsync_TakesAssetsFromTheMap()
        {
            Asset asset = null;
            yield return LoadAsset(TestAssetReferences.riv_asset_databinding_test,
                                   a => asset = a);

            var image = OutOfBandAsset.Create<ImageOutOfBandAsset>(new byte[] { 1, 2, 3, 4 });
            var assets = new Dictionary<uint, OutOfBandAsset> { { 7u, image } };

            Future<File> load = File.Loader.LoadAsync(asset.Bytes, 515151, assets);
            yield return Await(load);

            File file = TrackFile(load.Result);
            Assert.IsNotNull(file, "Loading with an asset map should give back a file.");
            Assert.AreEqual(1, image.RefCount(),
                            "The asset the map named should have been taken up.");
        }

        /// <summary>
        /// Nothing should be left awaiting a producer that has gone. Unity has
        /// no destroy-scoped cancellation before 2022.2 and the package
        /// targets 2021.3, so the producer ends these itself.
        /// </summary>
        [NeedsRiveThread]
        [UnityTest]
        public IEnumerator OutstandingAsyncWork_IsCancelledWhenTheProducerGoes()
        {
            if (!CommandTransport.IsThreaded)
            {
                Assert.Ignore("Work runs on the calling thread, so it finishes before it can be cancelled.");
            }

            Asset asset = null;
            yield return LoadAsset(TestAssetReferences.riv_asset_databinding_test,
                                   a => asset = a);

            File file = TrackFile(File.Load(asset));
            StateMachine stateMachine = file.Artboard(0u).StateMachine();

            // Park the producer so the call can't answer on its own.
            var started = new ManualResetEventSlim(false);
            var gate = new ManualResetEventSlim(false);
            ServerGate.Hold(started, gate);
            Assert.IsTrue(started.Wait(2000), "The producer should have picked the job up.");

            Future<bool> hitTest = TestServer.EchoAsync(true);
            Assert.IsFalse(hitTest.IsDone, "It can't have answered yet.");

            CommandTransport.CancelOutstandingAsyncForTests();
            gate.Set();

            float deadline = Time.realtimeSinceStartup + 5f;
            while (!hitTest.IsDone && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.AreEqual(FutureStatus.Canceled, hitTest.Status,
                            "The operation should end rather than wait on a producer that has gone.");
        }

        [UnityTest]
        public IEnumerator AsyncResults_ArriveOnTheMainThread()
        {
            Asset asset = null;
            yield return LoadAsset(TestAssetReferences.riv_asset_databinding_test,
                                   a => asset = a);

            File file = TrackFile(File.Load(asset));
            Artboard artboard = file.Artboard(0u);
            StateMachine stateMachine = artboard.StateMachine();

            int continuationThreadId = -1;
            Future<bool> hitTest = TestServer.EchoAsync(true);
            hitTest.Completed += _ => continuationThreadId = Thread.CurrentThread.ManagedThreadId;

            yield return Await(hitTest);

            Assert.AreEqual(m_mainThreadId, continuationThreadId,
                            "Continuations should run where Unity's API is usable.");
        }

        [UnityTest]
        public IEnumerator PropertySetValueAsync_IsVisibleToAGet()
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

            float target = number.Value + 5f;
            Future set = number.SetValueAsync(target);
            yield return Await(set);

            Future<float> get = number.GetValueAsync();
            yield return Await(get);

            Assert.AreEqual(target, get.Result, 0.0001f,
                            "An async set should be visible to an async get.");
        }

        [UnityTest]
        public IEnumerator TickAsync_FinishesOnceThePassHasAdvanced()
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

            Future tick = panel.TickAsync(1f / 60f);
            Assert.IsFalse(tick.IsDone, "Nothing has advanced yet.");

            yield return Await(tick);

            Assert.AreEqual(WidgetStatus.Loaded, widget.Status,
                            "The widget should be fine after an async tick.");
            Assert.IsFalse(m_mockLogger.AnyLogTypeContains("error"),
                           "An async tick shouldn't log errors.");
        }

        [UnityTest]
        public IEnumerator WidgetLoadAsync_Loads()
        {
            Asset asset = null;
            yield return LoadAsset(TestAssetReferences.riv_asset_databinding_test,
                                   a => asset = a);

            RivePanel panel = RivePanelTestUtils.CreatePanel();
            Track(panel.gameObject);

            RiveWidget widget = RivePanelTestUtils.CreateWidget<RiveWidget>();
            widget.transform.SetParent(panel.WidgetContainer, false);
            RivePanelTestUtils.MakeWidgetFillPanel(widget);

            Future load = widget.LoadAsync(asset);
            yield return Await(load);

            Assert.AreEqual(WidgetStatus.Loaded, widget.Status,
                            "LoadAsync should leave the widget loaded.");
            Assert.IsNotNull(widget.StateMachine, "The widget should have a state machine.");
        }

        // Async, so frames don't wait on a held producer for the panel's own tick.
        // A prefab's widget starts loading its preset asset from Start. Loading a
        // file into it straight after must win, even though the asset's import
        // lands later.
        [NeedsRiveThread]
        [UnityTest]
        public IEnumerator WidgetLoad_FileWhileAnAssetImports_KeepsTheFile()
        {
            Asset car = null;
            Asset form = null;
            yield return LoadAsset(TestAssetReferences.riv_cleanTheCar, a => car = a);
            yield return LoadAsset(TestAssetReferences.riv_asset_databinding_test, a => form = a);
            File formFile = File.Load(form);
            RiveWidget widget = CreateWidgetInPanel();
            yield return null;

            widget.Load(car, "Car", "Motion");
            widget.Load(formFile);
            yield return Frames(30);

            Assert.AreEqual(WidgetStatus.Loaded, widget.Status);
            Assert.AreSame(formFile, widget.LoadedFile, "The asset's import landed after the file load and replaced it.");
            formFile.Dispose();
        }

        // The same race when the asset's import fails. The server answers in
        // order, so the failure lands while the newer load is still going, and
        // we shouldn't report an Error for a load nobody is waiting on.
        [NeedsRiveThread]
        [UnityTest]
        public IEnumerator WidgetLoad_FileWhileAnAssetImportFails_KeepsTheFile()
        {
            LogAssert.ignoreFailingMessages = true;
            Asset form = null;
            yield return LoadAsset(TestAssetReferences.riv_asset_databinding_test, a => form = a);
            File formFile = File.Load(form);
            Asset broken = Asset.Create(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, new EmbeddedAssetData[0]);
            RiveWidget widget = CreateWidgetInPanel();
            yield return null;
            bool sawError = false;
            widget.OnWidgetStatusChanged += () => sawError |= widget.Status == WidgetStatus.Error;

            widget.Load(broken);
            widget.Load(formFile);
            yield return Frames(30);

            Assert.IsFalse(sawError, "The superseded import's failure was reported as the widget's.");
            Assert.AreEqual(WidgetStatus.Loaded, widget.Status);
            Assert.AreSame(formFile, widget.LoadedFile);
            formFile.Dispose();
            UnityEngine.Object.Destroy(broken);
        }

        public enum StaleEnding
        {
            Fails = 0,
            ErrorResult = 1,
            Succeeds = 2,
        }

        // A load replaced by a newer one is cancelled however its preparation
        // ends, and whether that lands before or after the newer one.
        [NeedsRiveThread]
        [UnityTest]
        public IEnumerator HelperLoad_Superseded_IsCancelledHoweverItEnds(
            [Values] StaleEnding ending, [Values] bool afterNewerLands)
        {
            Asset asset = null;
            yield return LoadAsset(TestAssetReferences.riv_asset_databinding_test, a => asset = a);
            File file = File.Load(asset);
            var helper = new Rive.Components.Utilities.ArtboardLoadHelper();
            var binding = new Rive.Components.Utilities.ArtboardLoadHelper.DataBindingLoadInfo(
                RiveWidget.DataBindingMode.Manual, null);
            var stale = new FutureState<Rive.Components.Utilities.ArtboardLoadHelper.Prepared>();

            var first = helper.LoadAsync(
                file, new Future<Rive.Components.Utilities.ArtboardLoadHelper.Prepared>(stale),
                Fit.Contain, Alignment.Center, 1f, binding);
            var second = helper.LoadAsync(file, Fit.Contain, Alignment.Center, null, null, 1f, binding);
            if (!afterNewerLands)
            {
                Finish(stale, ending);
            }
            yield return Await(second);
            if (afterNewerLands)
            {
                Finish(stale, ending);
            }

            Assert.AreEqual(FutureStatus.Canceled, first.Status, "The replaced load should be cancelled.");
            Assert.IsTrue(second.Result.Success, "The newer load should still attach.");
            Assert.IsTrue(helper.IsLoaded);
            helper.Dispose();
            file.Dispose();
        }

        private static void Finish(
            FutureState<Rive.Components.Utilities.ArtboardLoadHelper.Prepared> stale, StaleEnding ending)
        {
            switch (ending)
            {
                case StaleEnding.Fails:
                    stale.Fail(new System.InvalidOperationException("Preparation failed."));
                    break;
                case StaleEnding.ErrorResult:
                    stale.Succeed(new Rive.Components.Utilities.ArtboardLoadHelper.Prepared
                    {
                        Error = Rive.Components.Utilities.ArtboardLoadHelper.ArtboardNotFound("Missing"),
                    });
                    break;
                default:
                    stale.Succeed(new Rive.Components.Utilities.ArtboardLoadHelper.Prepared());
                    break;
            }
        }

        private RiveWidget CreateWidgetInPanel()
        {
            RivePanel panel = RivePanelTestUtils.CreatePanel();
            Track(panel.gameObject);
            panel.ThreadingMode = ThreadingMode.BackgroundThread;
            RiveWidget widget = RivePanelTestUtils.CreateWidget<RiveWidget>();
            widget.transform.SetParent(panel.WidgetContainer, false);
            RivePanelTestUtils.MakeWidgetFillPanel(widget);
            return widget;
        }

        // Holds the producer until the returned gate is set.
        private static ManualResetEventSlim HoldProducer()
        {
            var started = new ManualResetEventSlim(false);
            var gate = new ManualResetEventSlim(false);
            ServerGate.Hold(started, gate);
            Assert.IsTrue(started.Wait(2000), "The producer should have picked the job up.");
            return gate;
        }

        [NeedsRiveThread]
        [UnityTest]
        public IEnumerator WidgetLoadAsync_NeverWaitsOnTheProducer()
        {
            Asset asset = null;
            yield return LoadAsset(TestAssetReferences.riv_asset_databinding_test,
                                   a => asset = a);
            RiveWidget widget = CreateWidgetInPanel();

            ManualResetEventSlim gate = HoldProducer();
            Future load;
            float longestFrame = 0f;
            try
            {
                load = widget.LoadAsync(asset);
                // Long enough for the import to land and hand the rest to the
                // held producer.
                float last = Time.realtimeSinceStartup;
                for (int frame = 0; frame < 20; frame++)
                {
                    yield return null;
                    float now = Time.realtimeSinceStartup;
                    longestFrame = Mathf.Max(longestFrame, now - last);
                    last = now;
                }
                Assert.AreEqual(WidgetStatus.Loading, widget.Status);
            }
            finally
            {
                gate.Set();
            }

            Assert.Less(longestFrame, 1f, "A frame waited on the held producer.");
            yield return Await(load);
            Assert.AreEqual(WidgetStatus.Loaded, widget.Status);
        }

        [NeedsRiveThread]
        [UnityTest]
        public IEnumerator WidgetLoadAsync_AgainWhileTheFirstIsBuilding_CancelsTheFirst()
        {
            Asset asset = null;
            yield return LoadAsset(TestAssetReferences.riv_asset_databinding_test,
                                   a => asset = a);
            RiveWidget widget = CreateWidgetInPanel();

            ManualResetEventSlim gate = HoldProducer();
            Future first;
            Future second;
            try
            {
                first = widget.LoadAsync(asset);
                for (int frame = 0; frame < 20; frame++)
                {
                    yield return null;
                }
                second = widget.LoadAsync(asset);
            }
            finally
            {
                gate.Set();
            }

            yield return Await(second);
            Assert.AreEqual(FutureStatus.Canceled, first.Status);
            Assert.AreEqual(WidgetStatus.Loaded, widget.Status);
            Assert.IsNotNull(widget.StateMachineHandle);
        }

        [UnityTest]
        public IEnumerator WidgetLoadAsync_WaitForCompletion_Loads()
        {
            Asset asset = null;
            yield return LoadAsset(TestAssetReferences.riv_asset_databinding_test,
                                   a => asset = a);
            RiveWidget widget = CreateWidgetInPanel();

            widget.LoadAsync(asset).WaitForCompletion();

            Assert.AreEqual(WidgetStatus.Loaded, widget.Status);
            Assert.IsNotNull(widget.StateMachineHandle);
        }

        // Runs body while noting every main thread wait that blocks on the
        // producer, and fails with the first one's stack.
        private static IEnumerator ExpectNoMainThreadWaits(string what, IEnumerator body)
        {
            var waits = new List<string>();
            CommandTransport.MainThreadWaitsForTests = waits;
            try
            {
                while (body.MoveNext())
                {
                    yield return body.Current;
                }
            }
            finally
            {
                CommandTransport.MainThreadWaitsForTests = null;
            }
            Assert.IsEmpty(waits, $"{what} waited on the producer {waits.Count} time(s). First:\n"
                                  + (waits.Count > 0 ? waits[0] : ""));
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        // The whole async load on the main thread, and the frames after it,
        // never wait. Catches any sync call creeping back into the load.
        [NeedsRiveThread]
        [UnityTest]
        public IEnumerator WidgetLoadAsync_MainThreadNeverWaits()
        {
            Asset asset = null;
            yield return LoadAsset(TestAssetReferences.riv_asset_databinding_test,
                                   a => asset = a);
            RiveWidget widget = CreateWidgetInPanel();
            yield return Frames(2);

            yield return ExpectNoMainThreadWaits("A first async load", LoadAndSettle(widget, asset));
            Assert.AreEqual(WidgetStatus.Loaded, widget.Status);

            // Same asset again takes the reuse branch.
            yield return ExpectNoMainThreadWaits("Loading the same asset again", LoadAndSettle(widget, asset));
            Assert.AreEqual(WidgetStatus.Loaded, widget.Status);
        }

        private IEnumerator LoadAndSettle(RiveWidget widget, Asset asset)
        {
            Future load = widget.LoadAsync(asset);
            yield return Await(load);
            // First advance and draw.
            yield return Frames(3);
        }

        private IEnumerator LoadedWidgetInAsyncPanel(System.Action<RiveWidget> onReady)
        {
            Asset asset = null;
            yield return LoadAsset(TestAssetReferences.riv_asset_databinding_test,
                                   a => asset = a);
            RiveWidget widget = CreateWidgetInPanel();
            yield return Await(widget.LoadAsync(asset));
            yield return Frames(3);
            onReady(widget);
        }

        [NeedsRiveThread]
        [UnityTest]
        public IEnumerator PanelDisable_WithAnAdvanceOut_DoesNotWait()
        {
            RiveWidget widget = null;
            yield return LoadedWidgetInAsyncPanel(w => widget = w);
            RivePanel panel = widget.RivePanel;
            Assert.IsNotNull(panel);

            ManualResetEventSlim gate = HoldProducer();
            Future tick;
            try
            {
                tick = panel.TickAsync(1f / 60f);
                yield return null;
                Assert.IsTrue(panel.HasAdvanceInFlight, "The advance should be out behind the held producer.");

                // Disabling also asks for a canvas flush, which takes a few frames.
                yield return ExpectNoMainThreadWaits("Disabling the panel", DisableAndRun(panel.gameObject));
                Assert.IsTrue(tick.IsDone, "A dropped advance should still finish its waiters.");
            }
            finally
            {
                gate.Set();
            }

            yield return Frames(3);
            Assert.IsFalse(m_mockLogger.AnyLogTypeContains("error"));
        }

        [NeedsRiveThread]
        [UnityTest]
        public IEnumerator WidgetDisable_WithAnAdvanceOut_DoesNotWait()
        {
            RiveWidget widget = null;
            yield return LoadedWidgetInAsyncPanel(w => widget = w);
            RivePanel panel = widget.RivePanel;
            Assert.IsNotNull(panel);

            ManualResetEventSlim gate = HoldProducer();
            try
            {
                yield return null;
                Assert.IsTrue(panel.HasAdvanceInFlight, "The advance should be out behind the held producer.");
                yield return ExpectNoMainThreadWaits("Disabling the widget", DisableAndRun(widget.gameObject));
            }
            finally
            {
                gate.Set();
            }

            // The advance it was in lands with the widget gone from the panel.
            yield return Frames(3);
            Assert.IsFalse(panel.ContainsWidget(widget));
            Assert.IsFalse(m_mockLogger.AnyLogTypeContains("error"));
        }

        private static IEnumerator DisableAndRun(GameObject go)
        {
            go.SetActive(false);
            yield return Frames(6);
        }

        // No panel. A write with nothing after it still goes out by the end of
        // the frame, since the core frame loop flushes.
        [NeedsRiveThread]
        [UnityTest]
        public IEnumerator HeldWrite_WithNothingAfterIt_GoesOutByTheEndOfTheFrame()
        {
            Asset asset = null;
            yield return LoadAsset(TestAssetReferences.riv_asset_databinding_test,
                                   a => asset = a);
            File file = TrackFile(File.Load(asset));
            Artboard artboard = file.Artboard(0);
            Size original = artboard.Size;
            yield return null;

            var doubled = new Size(original.Width * 2, original.Height * 2);
            artboard.Size = doubled;
            Assert.Greater(CommandTransport.PendingWriteCount, 0, "The write should be held until something sends it.");

            yield return null;
            Assert.AreEqual(0, CommandTransport.PendingWriteCount, "The frame loop should have sent it.");
            Assert.AreEqual(doubled, artboard.Size);
        }

        // What each advance of a widget's core saw. The read goes out as the
        // advance does, just ahead of it, so it sees what the advance sees.
        private sealed class AdvanceLog
        {
            internal volatile NumberPropertyHandle Property;
            private readonly List<KeyValuePair<WidgetCore, Future<float>>> m_seen =
                new List<KeyValuePair<WidgetCore, Future<float>>>();

            internal void Record(WidgetCore core)
            {
                NumberPropertyHandle property = Property;
                if (property == null)
                {
                    return;
                }
                Future<float> value = property.GetValueAsync();
                lock (m_seen)
                {
                    m_seen.Add(new KeyValuePair<WidgetCore, Future<float>>(core, value));
                }
            }

            internal float? FirstFor(WidgetCore core)
            {
                lock (m_seen)
                {
                    for (int i = 0; i < m_seen.Count; i++)
                    {
                        if (ReferenceEquals(m_seen[i].Key, core))
                        {
                            Future<float> value = m_seen[i].Value;
                            return value.IsDone && value.Status == FutureStatus.Succeeded ? value.Result : (float?)null;
                        }
                    }
                }
                return null;
            }
        }

        // Values set in OnWidgetStatusChanged, and by code that runs when the
        // load's Future finishes, both reach the first advance, so the first
        // drawn frame has them.
        [NeedsRiveThread]
        [UnityTest]
        public IEnumerator WidgetLoadAsync_ValuesSetOnLoad_ReachTheFirstAdvance()
        {
            Asset asset = null;
            yield return LoadAsset(TestAssetReferences.riv_asset_databinding_test,
                                   a => asset = a);
            RiveWidget widget = CreateWidgetInPanel();
            yield return Frames(2);

            const float setOnStatus = 1234.5f;
            const float setAfterLoad = 6789.25f;
            var log = new AdvanceLog();
            widget.OnWidgetStatusChanged += () =>
            {
                if (widget.Status != WidgetStatus.Loaded)
                {
                    return;
                }
                NumberPropertyHandle property = widget.StateMachineHandle.GetViewModelInstance().GetNumberProperty("age");
                property.SetValue(setOnStatus);
                log.Property = property;
            };

            WidgetCore.AdvancingForTests = log.Record;
            try
            {
                Future load = widget.LoadAsync(asset);
                // Runs where an await's continuation would.
                load.Completed += _ => log.Property.SetValue(setAfterLoad);
                yield return Await(load);
                // The panel advances on its next tick, which a busy run can
                // push back a few frames.
                for (int i = 0; i < 60 && !log.FirstFor(widget.Core).HasValue; i++)
                {
                    yield return null;
                }
            }
            finally
            {
                WidgetCore.AdvancingForTests = null;
            }

            float? first = log.FirstFor(widget.Core);
            Assert.IsTrue(first.HasValue, "The widget should have advanced.");
            Assert.AreEqual(setAfterLoad, first.Value,
                "The first advance should see what was set once the load finished.");
        }

        // The bound instance is looked up and written in the load callback,
        // while the handle is still pending, with no wait, and the first
        // advance sees it.
        [NeedsRiveThread]
        [UnityTest]
        public IEnumerator FirstFrameSetup_OnAPendingHandle_ReachesTheFirstAdvance_WithoutWaiting()
        {
            Asset asset = null;
            yield return LoadAsset(TestAssetReferences.riv_asset_databinding_test,
                                   a => asset = a);
            RiveWidget widget = CreateWidgetInPanel();
            yield return Frames(2);

            const float setOnLoad = 4321f;
            var log = new AdvanceLog();
            var statusInCallback = new List<HandleStatus>();
            widget.OnWidgetStatusChanged += () =>
            {
                if (widget.Status != WidgetStatus.Loaded)
                {
                    return;
                }
                ViewModelInstanceHandle instance = widget.StateMachineHandle.GetViewModelInstance();
                statusInCallback.Add(instance.Status);
                NumberPropertyHandle property = instance.GetNumberProperty("age");
                property.SetValue(setOnLoad);
                log.Property = property;
            };

            var waits = new List<string>();
            WidgetCore.AdvancingForTests = log.Record;
            CommandTransport.MainThreadWaitsForTests = waits;
            try
            {
                Future load = widget.LoadAsync(asset);
                yield return Await(load);
                yield return Frames(3);
            }
            finally
            {
                WidgetCore.AdvancingForTests = null;
                CommandTransport.MainThreadWaitsForTests = null;
            }

            CollectionAssert.AreEqual(new[] { HandleStatus.Pending }, statusInCallback, "The lookup can't have run by then.");
            CollectionAssert.IsEmpty(waits, "Nothing should wait on the producer.");
            float? first = log.FirstFor(widget.Core);
            Assert.IsTrue(first.HasValue, "The widget should have advanced.");
            Assert.AreEqual(setOnLoad, first.Value, "The first advance should see the value set on load.");
        }

        // With nothing bound, the lookup fails. Writes queued on it are
        // skipped, and the one error goes to OnError.
        [UnityTest]
        public IEnumerator FirstFrameSetup_WhenTheLookupFails_SkipsTheWrites_AndReportsOnce()
        {
            Asset asset = null;
            yield return LoadAsset(TestAssetReferences.riv_asset_databinding_test,
                                   a => asset = a);
            RiveWidget widget = CreateWidgetInPanel();
            widget.BindingMode = RiveWidget.DataBindingMode.Manual;
            yield return Frames(2);

            var errors = new List<RiveException>();
            widget.OnError += errors.Add;
            ViewModelInstanceHandle instance = null;
            widget.OnWidgetStatusChanged += () =>
            {
                if (widget.Status != WidgetStatus.Loaded)
                {
                    return;
                }
                instance = widget.StateMachineHandle.GetViewModelInstance();
                instance.GetNumberProperty("age").SetValue(1f);
                instance.GetStringProperty("name").SetValue("skipped");
                instance.GetViewModelInstanceProperty("favDrink").GetStringProperty("name").SetValue("skipped too");
            };

            Future load = widget.LoadAsync(asset);
            yield return Await(load);
            Future<ViewModelInstanceHandle> resolve = instance.ResolveAsync();
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!resolve.IsDone)
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, "The lookup didn't finish within 10 seconds.");
                yield return null;
            }
            yield return Frames(3);

            Assert.AreEqual(HandleStatus.Invalid, instance.Status);
            Assert.AreEqual(1, errors.Count, "Only the lookup's error should be reported.");
            Assert.AreEqual(RiveErrorCode.ViewModelInstanceNotFound, errors[0].Code);
            Assert.AreSame(errors[0], instance.Error);
            Assert.AreSame(errors[0], instance.GetNumberProperty("age").Error, "A property on it takes the same error.");
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
