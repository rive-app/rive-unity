using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
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
    /// ThreadingMode, and the pass that advances every panel.
    /// </summary>
    public class ExecutionModeTests
    {
        private MockLogger m_mockLogger;
        private readonly List<GameObject> m_objects = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            m_mockLogger = new MockLogger();
            DebugLogger.Instance = m_mockLogger;
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
        }

        private RivePanel CreatePanel(string name = "TestPanel")
        {
            RivePanel panel = RivePanelTestUtils.CreatePanel(name);
            m_objects.Add(panel.gameObject);
            panel.RenderTargetStrategy = panel.gameObject.AddComponent<MockRenderTargetStrategy>();
            return panel;
        }

        private AdvanceRecorderWidget AddRecorder(RivePanel panel)
        {
            var widget = RivePanelTestUtils.CreateWidget<AdvanceRecorderWidget>();
            widget.transform.SetParent(panel.WidgetContainer, false);
            RivePanelTestUtils.MakeWidgetFillPanel(widget);
            return widget;
        }

        private Renderer FirstRenderer(RenderTargetStrategy strategy)
        {
            MethodInfo method = strategy.GetType().GetMethod(
                "GetRenderers",
                BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (Renderer renderer in (IEnumerable<Renderer>)method.Invoke(strategy, null))
            {
                return renderer;
            }
            return null;
        }

        private RivePanel CreatePanel(RenderTargetStrategy strategy, string name)
        {
            RivePanel panel = CreatePanel(name);
            panel.StopRendering();
            panel.RenderTargetStrategy = strategy;
            panel.StartRendering();
            return panel;
        }

        [UnityTest]
        public IEnumerator ArtboardDraw_DoesNotWaitOnTheProducer()
        {
            var assets = new TestAssetLoadingManager();
            Asset asset = null;
            yield return assets.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_sophiaHud,
                loaded => asset = loaded,
                () => Assert.Fail("Failed to load the test asset"));

            RivePanel panel = CreatePanel();
            RiveWidget widget = RivePanelTestUtils.CreateWidget<RiveWidget>();
            widget.transform.SetParent(panel.WidgetContainer, false);
            widget.Load(asset);
            yield return null;
            Assert.AreEqual(WidgetStatus.Loaded, widget.Status);

            // What an async panel's draw does, clip check included.
            var renderObject = (ArtboardRenderObject)widget.RenderObject;
            using (CommandTransport.NoWait("test draw"))
            {
                renderObject.DrawContent(
                    new MockRenderer(),
                    new AABB(0, 0, 50, 50),
                    new RenderContext(RenderContext.ClippingModeSetting.CheckClipping));
            }

            Assert.IsFalse(m_mockLogger.LoggedErrorsContains("no-wait scope"),
                "Drawing an artboard shouldn't wait on the producer.");
            assets.UnloadAllAssets();
        }

        [Test]
        public void ThreadingMode_DefaultsToMainThread()
        {
            RivePanel panel = CreatePanel();

            Assert.AreEqual(ThreadingMode.MainThread, panel.ThreadingMode,
                            "A panel that has never been touched should tick as it always has.");
            Assert.AreEqual(0, (int)ThreadingMode.MainThread,
                            "MainThread has to be zero so existing scenes deserialize into it.");
        }

        [UnityTest]
        public IEnumerator ManualTick_WaitsForThePass()
        {
            RivePanel panel = CreatePanel();
            panel.UpdateMode = RivePanel.PanelUpdateMode.Manual;
            AdvanceRecorderWidget widget = AddRecorder(panel);

            yield return null;
            widget.Reset();

            panel.Tick(0.25f);

            Assert.AreEqual(0, widget.AdvanceCount,
                            "Tick should queue the time, not advance at the call site.");
            Assert.IsTrue(panel.HasQueuedTick, "The time should be waiting for the pass.");

            Orchestrator.Instance.RunTickPass();

            Assert.AreEqual(1, widget.AdvanceCount, "The pass should advance once.");
            Assert.AreEqual(0.25f, widget.TakeAdvances()[0], 0.0001f,
                            "The advance should use the time that was queued.");
            Assert.IsFalse(panel.HasQueuedTick, "The pass should empty the queue.");
        }

        [UnityTest]
        public IEnumerator ManualTicks_BeforeThePass_AddUp()
        {
            RivePanel panel = CreatePanel();
            panel.UpdateMode = RivePanel.PanelUpdateMode.Manual;
            AdvanceRecorderWidget widget = AddRecorder(panel);

            yield return null;
            widget.Reset();

            panel.Tick(0.1f);
            panel.Tick(0.2f);
            panel.Tick(0.3f);

            Orchestrator.Instance.RunTickPass();

            Assert.AreEqual(1, widget.AdvanceCount, "Three ticks should still be one advance.");
            Assert.AreEqual(0.6f, widget.TakeAdvances()[0], 0.0001f, "The times should add up.");
        }

        [UnityTest]
        public IEnumerator ManualTick_AfterThePass_LandsInTheNextOne()
        {
            RivePanel panel = CreatePanel();
            panel.UpdateMode = RivePanel.PanelUpdateMode.Manual;
            AdvanceRecorderWidget widget = AddRecorder(panel);

            yield return null;
            widget.Reset();

            Orchestrator.Instance.RunTickPass();
            Assert.AreEqual(0, widget.AdvanceCount, "Nothing was queued, so nothing should advance.");

            panel.Tick(0.5f);
            Assert.AreEqual(0, widget.AdvanceCount, "Still waiting for a pass.");

            Orchestrator.Instance.RunTickPass();
            Assert.AreEqual(1, widget.AdvanceCount, "The next pass should take it.");
            Assert.AreEqual(0.5f, widget.TakeAdvances()[0], 0.0001f);
        }

        [UnityTest]
        public IEnumerator ManualTick_OnADisabledPanel_IsIgnored()
        {
            RivePanel panel = CreatePanel();
            panel.UpdateMode = RivePanel.PanelUpdateMode.Manual;
            AdvanceRecorderWidget widget = AddRecorder(panel);

            yield return null;
            widget.Reset();

            panel.enabled = false;
            panel.Tick(0.5f);

            Assert.IsFalse(panel.HasQueuedTick, "A disabled panel shouldn't queue anything.");

            panel.enabled = true;
            yield return null;

            Assert.AreEqual(0, widget.AdvanceCount,
                            "Time asked for while disabled shouldn't turn up later.");
        }

        [UnityTest]
        public IEnumerator ManualTick_FromAnEarlyUpdate_LandsInTheSameFrame()
        {
            RivePanel panel = CreatePanel();
            panel.UpdateMode = RivePanel.PanelUpdateMode.Manual;
            AdvanceRecorderWidget widget = AddRecorder(panel);

            yield return null;
            widget.Reset();

            var caller = new GameObject("EarlyTicker");
            m_objects.Add(caller);
            var ticker = caller.AddComponent<EarlyTicker>();
            ticker.Panel = panel;
            ticker.DeltaTime = 0.125f;

            yield return null;

            Assert.AreEqual(1, widget.AdvanceCount, "The tick should have landed.");
            Assert.AreEqual(0.125f, widget.TakeAdvances()[0], 0.0001f);
            Assert.AreEqual(ticker.TickedOnFrame, widget.LastTickFrame,
                            "The advance should happen in the frame Tick was called in, "
                                + "so it lands before that frame's render.");
        }

        [UnityTest]
        public IEnumerator SynchronousPanel_HasNothingInFlightAfterThePass()
        {
            RivePanel panel = CreatePanel();
            AdvanceRecorderWidget widget = AddRecorder(panel);

            yield return null;

            Orchestrator.Instance.RunTickPass();

            Assert.IsFalse(panel.HasAdvanceInFlight,
                           "A Synchronous panel should be done by the time the pass returns.");
            Assert.Greater(widget.AdvanceCount, 0, "It should have advanced.");
        }

        [NeedsManagedThreads]
        [NeedsRiveThread]
        [UnityTest]
        public IEnumerator EveryPanelIsQueuedBeforeAnyIsWaitedFor()
        {
            if (!CommandTransport.IsThreaded)
            {
                Assert.Ignore("Nothing is queued when work runs on the calling thread.");
            }

            RivePanel first = CreatePanel("First");
            AddRecorder(first);
            RivePanel second = CreatePanel("Second");
            AddRecorder(second);

            yield return null;

            // Park the producer so both panels' advances have to sit in the
            // queue together. One pass would only ever hold one, because the
            // second panel isn't reached until the first has been waited for.
            // No yielding from here until the gate is open, or Unity's own
            // pass runs into it first.
            var started = new ManualResetEventSlim(false);
            var gate = new ManualResetEventSlim(false);
            ServerGate.Hold(started, gate);
            Assert.IsTrue(started.Wait(2000), "The producer should have picked the job up.");

            CommandTransport.ResetPendingPeakForTests();

            var release = new Thread(() =>
            {
                Thread.Sleep(200);
                gate.Set();
            });
            release.Start();

            Orchestrator.Instance.RunTickPass();
            release.Join();

            Assert.GreaterOrEqual(CommandTransport.PendingPeakForTests, 2,
                                  "Both panels' advances should be queued before either is "
                                      + "waited for.");
        }

        [UnityTest]
        public IEnumerator AsynchronousPanel_AdvancesOverFrames()
        {
            RivePanel panel = CreatePanel();
            panel.ThreadingMode = ThreadingMode.BackgroundThread;
            AdvanceRecorderWidget widget = AddRecorder(panel);

            yield return null;
            widget.Reset();

            yield return null;
            yield return null;
            yield return null;

            Assert.Greater(widget.AdvanceCount, 0, "An Asynchronous panel should still advance.");
            Assert.IsFalse(m_mockLogger.AnyLogTypeContains("error"),
                           "Advancing asynchronously shouldn't log errors.");
        }

        [NeedsRiveThread]
        [UnityTest]
        public IEnumerator AsynchronousPanel_KeepsTheTimeItCouldNotSendYet()
        {
            if (!CommandTransport.IsThreaded)
            {
                Assert.Ignore("Work runs on the calling thread, so nothing is ever in flight.");
            }

            RivePanel panel = CreatePanel();
            panel.UpdateMode = RivePanel.PanelUpdateMode.Manual;
            panel.ThreadingMode = ThreadingMode.BackgroundThread;
            AdvanceRecorderWidget widget = AddRecorder(panel);

            yield return null;
            widget.Reset();

            // No yielding until the gate is open, or Unity's own pass runs
            // into the parked producer.
            var started = new ManualResetEventSlim(false);
            var gate = new ManualResetEventSlim(false);
            ServerGate.Hold(started, gate);
            Assert.IsTrue(started.Wait(2000), "The producer should have picked the job up.");

            panel.Tick(0.1f);
            Orchestrator.Instance.RunTickPass();

            panel.Tick(0.2f);
            Orchestrator.Instance.RunTickPass();

            panel.Tick(0.3f);
            Orchestrator.Instance.RunTickPass();

            gate.Set();
            panel.JoinAdvance();

            // Three passes, one advance out, and the rest of the time merged
            // into the next one rather than dropped.
            Orchestrator.Instance.RunTickPass();
            panel.JoinAdvance();

            List<float> advances = widget.TakeAdvances();
            float total = 0f;
            for (int i = 0; i < advances.Count; i++)
            {
                total += advances[i];
            }

            Assert.LessOrEqual(advances.Count, 2,
                               "Later ticks should merge into the advance in flight rather than "
                                   + "queue behind it.");
            Assert.AreEqual(0.6f, total, 0.0001f, "No time should be lost.");
        }

        [NeedsRiveThread]
        [UnityTest]
        public IEnumerator AsynchronousRecords_ReplaceWorkThatHasNotStarted()
        {
            if (!CommandTransport.IsThreaded)
            {
                Assert.Ignore("Nothing is queued when work runs on the calling thread.");
            }

            var queue = new RenderQueue();
            Renderer renderer = queue.Renderer();
            renderer.SetRecordsAsynchronously(true);

            var started = new ManualResetEventSlim(false);
            var gate = new ManualResetEventSlim(false);
            ServerGate.Hold(started, gate);
            Assert.IsTrue(started.Wait(2000), "The producer should have picked the job up.");

            try
            {
                long jobsBefore = CommandTransport.RequestsForTests;
                renderer.RecordForGpuCanvas();
                renderer.RecordForGpuCanvas();
                renderer.RecordForGpuCanvas();

                Assert.AreEqual(1, CommandTransport.RequestsForTests - jobsBefore,
                    "Only the newest waiting record should stay queued.");
            }
            finally
            {
                gate.Set();
                CommandTransport.Barrier();
                queue.Dispose();
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator RenderStrategies_RecordAsyncOnlyWhenEveryPanelIsAsync()
        {
            RivePanel simplePanel = CreatePanel("Simple");
            simplePanel.StopRendering();
            var simple = simplePanel.GetComponent<SimpleRenderTargetStrategy>() ??
                simplePanel.gameObject.AddComponent<SimpleRenderTargetStrategy>();
            simplePanel.RenderTargetStrategy = simple;
            simplePanel.ThreadingMode = ThreadingMode.BackgroundThread;
            simplePanel.StartRendering();
            simple.DrawPanel(simplePanel);
            simple.PrepareRenderFromOrchestrator();
            Assert.IsTrue(FirstRenderer(simple).RecordsAsynchronously);

            var pooledObject = new GameObject("Pooled");
            m_objects.Add(pooledObject);
            var pooled = pooledObject.AddComponent<PooledRenderTargetStrategy>();
            RivePanel pooledPanel = CreatePanel(pooled, "PooledPanel");
            pooledPanel.ThreadingMode = ThreadingMode.BackgroundThread;
            pooled.DrawPanel(pooledPanel);
            pooled.PrepareRenderFromOrchestrator();
            Assert.IsTrue(FirstRenderer(pooled).RecordsAsynchronously);

            var atlasObject = new GameObject("Atlas");
            m_objects.Add(atlasObject);
            var atlas = atlasObject.AddComponent<AtlasRenderTargetStrategy>();
            RivePanel asyncPanel = CreatePanel(atlas, "AtlasAsync");
            asyncPanel.ThreadingMode = ThreadingMode.BackgroundThread;
            asyncPanel.DrawOptimization = DrawOptimizationOptions.AlwaysDraw;
            atlas.DrawPanel(asyncPanel);
            atlas.PrepareRenderFromOrchestrator();
            Assert.IsTrue(FirstRenderer(atlas).RecordsAsynchronously);

            RivePanel syncPanel = CreatePanel(atlas, "AtlasSync");
            atlas.DrawPanel(syncPanel);
            atlas.PrepareRenderFromOrchestrator();
            Assert.IsFalse(FirstRenderer(atlas).RecordsAsynchronously,
                "A shared target should wait when one panel is synchronous.");

            syncPanel.ThreadingMode = ThreadingMode.BackgroundThread;
            atlas.PrepareRenderFromOrchestrator();
            Assert.IsTrue(FirstRenderer(atlas).RecordsAsynchronously);

            yield return null;
        }

        [UnityTest]
        public IEnumerator MixedModes_BothPanelsAdvance()
        {
            RivePanel sync = CreatePanel("Sync");
            AdvanceRecorderWidget syncWidget = AddRecorder(sync);

            RivePanel async = CreatePanel("Async");
            async.ThreadingMode = ThreadingMode.BackgroundThread;
            AdvanceRecorderWidget asyncWidget = AddRecorder(async);

            yield return null;
            syncWidget.Reset();
            asyncWidget.Reset();

            yield return null;
            yield return null;
            yield return null;

            Assert.Greater(syncWidget.AdvanceCount, 0, "The Synchronous panel should advance.");
            Assert.Greater(asyncWidget.AdvanceCount, 0, "The Asynchronous panel should advance.");
        }

        [UnityTest]
        public IEnumerator SwitchingToSynchronous_LandsTheAdvanceInFlight()
        {
            RivePanel panel = CreatePanel();
            panel.ThreadingMode = ThreadingMode.BackgroundThread;
            AddRecorder(panel);

            yield return null;

            panel.Tick(0.1f);
            Orchestrator.Instance.RunTickPass();

            panel.ThreadingMode = ThreadingMode.MainThread;

            Assert.IsFalse(panel.HasAdvanceInFlight,
                           "Changing mode should land whatever was already out.");
        }

        /// <summary>
        /// Records what the panel asked it to advance by. The advance runs on the producer, so nothing here touches Unity.
        /// </summary>
        public class AdvanceRecorderWidget : WidgetBehaviour
        {
            private readonly MockRenderObject m_renderObject = new MockRenderObject();
            private HitTestBehavior m_hitTestBehavior = HitTestBehavior.Opaque;

            public override IRenderObject RenderObject => m_renderObject;

            public override HitTestBehavior HitTestBehavior
            {
                get => m_hitTestBehavior;
                set => m_hitTestBehavior = value;
            }

            // Written on the producer and read on the main thread.
            private readonly object m_lock = new object();
            private readonly List<float> m_advances = new List<float>();

            public int AdvanceCount
            {
                get { lock (m_lock) { return m_advances.Count; } }
            }

            public List<float> TakeAdvances()
            {
                lock (m_lock) { return new List<float>(m_advances); }
            }

            /// The frame the panel last ran this widget's main-thread half in.
            public int LastTickFrame { get; private set; } = -1;

            public void Reset()
            {
                lock (m_lock) { m_advances.Clear(); }
                LastTickFrame = -1;
            }

            public override bool Tick(float deltaTime)
            {
                LastTickFrame = Time.frameCount;
                return base.Tick(deltaTime);
            }

            internal override void Advance(float deltaTime)
            {
                lock (m_lock) { m_advances.Add(deltaTime); }
            }
        }

        /// <summary>
        /// Ticks a panel from an Update that runs before the orchestrator's.
        /// </summary>
        [DefaultExecutionOrder(-20000)]
        public class EarlyTicker : MonoBehaviour
        {
            public RivePanel Panel;
            public float DeltaTime = 0.016f;

            /// -1 until it has ticked.
            public int TickedOnFrame { get; private set; } = -1;

            private void Update()
            {
                if (Panel == null || TickedOnFrame >= 0)
                {
                    return;
                }

                TickedOnFrame = Time.frameCount;
                Panel.Tick(DeltaTime);
            }
        }
    }
}
