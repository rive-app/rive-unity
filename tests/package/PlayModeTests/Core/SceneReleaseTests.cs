using System.Collections;
using NUnit.Framework;
using Rive.Components;
using Rive.Host;
using Rive.Tests.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Rive.Tests
{
    /// <summary>
    /// Switching away from a scene with Rive in it has to give back what it
    /// made. Each round loads panels into a fresh scene and unloads it, and the
    /// counts afterwards can't be above where they were after the first round.
    /// Once per threading mode, since the two keep their state differently.
    /// </summary>
    public class SceneReleaseTests
    {
        private const int Rounds = 5;
        private const int RenderFrames = 10;
        private const int SettleFrames = 10;

        // riveLiveCounts' order.
        private static readonly string[] s_countNames =
        {
            "files", "artboards", "state machines", "view model instances", "images", "fonts",
            "audio sources", "render queues", "pending canvas frames",
        };

        private TestAssetLoadingManager m_assets;
        private Asset m_databinding;
        private Asset m_hud;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            HostNative.riveTrackLive();
            m_assets = new TestAssetLoadingManager();
            yield return m_assets.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_asset_databinding_test,
                a => m_databinding = a,
                () => Assert.Fail("Failed to load the data binding asset"));
            yield return m_assets.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_sophiaHud,
                a => m_hud = a,
                () => Assert.Fail("Failed to load the HUD asset"));
        }

        [TearDown]
        public void TearDown()
        {
            m_assets.UnloadAllAssets();
        }

        [UnityTest]
        public IEnumerator RepeatedSceneSwitches_GiveBackWhatTheyMade([Values] ThreadingMode mode)
        {
            // The first round may leave one time caches, so the baseline is
            // taken after it.
            uint[] loaded = null;
            yield return Round(0, mode, counts => loaded = counts);
            uint[] baseline = Counts();
            Assert.Greater(loaded[1], baseline[1], "Counting should see the scene's artboards.");

            for (int round = 1; round <= Rounds; round++)
            {
                yield return Round(round, mode, counts => loaded = counts);
                uint[] after = Counts();
                for (int i = 0; i < after.Length; i++)
                {
                    Assert.LessOrEqual(after[i], baseline[i],
                        $"After round {round}, {s_countNames[i]} went from {baseline[i]} to {after[i]} " +
                        $"(loaded: {loaded[i]}), so something outlived its scene.");
                }
            }
            Assert.AreEqual(0, CommandTransport.PendingRequestsForTests, "Nothing should be left waiting.");
        }

        // The native counters reach Unity's profiler, where the Rive profiler
        // module and ProfilerRecorder read them.
        [UnityPlatform(RuntimePlatform.OSXEditor, RuntimePlatform.WindowsEditor, RuntimePlatform.LinuxEditor)]
        [UnityTest]
        public IEnumerator ProfilerCounters_ShowWhatIsLoaded()
        {
            using var artboards = Unity.Profiling.ProfilerRecorder.StartNew(
                Unity.Profiling.ProfilerCategory.Scripts, "Rive Artboards");
            long seen = 0;
            yield return Round(0, ThreadingMode.MainThread, _ =>
            {
                seen = artboards.LastValue;
            });

            Assert.IsTrue(artboards.Valid, "The Rive Artboards counter should exist.");
            Assert.Greater(seen, 0, "The counter should count the scene's artboards.");
        }

        // The pipeline adds a panel's renderer to a command buffer every frame
        // and records it while rendering, so the frame loop's replay pass
        // should leave it alone.
        [UnityTest]
        public IEnumerator PipelineRecordedPanels_AreNotRecordedAgainByTheFrameLoop()
        {
            // The pipeline's pass only runs for a camera.
            var camera = new GameObject("RiveReplayCamera").AddComponent<Camera>();
            RiveWidget widget = AddPanel(ThreadingMode.MainThread, m_hud);
            yield return RivePanelTestUtils.WaitForLoaded(widget);
            for (int i = 0; i < RenderFrames; i++)
            {
                yield return null;
            }
            // Only this panel's renderer. Others left alive by earlier tests
            // may have nothing else recording them.
            RivePanel panel = widget.GetComponentInParent<RivePanel>();
            Renderer renderer = System.Linq.Enumerable.Single(
                ((RenderTargetStrategy)panel.RenderTargetStrategy).RenderersForTests);
            Assert.AreSame(renderer, renderer.RenderQueue.ReplayedRenderer,
                "The pipeline should have added the panel's renderer to a command buffer.");

            int before = renderer.ReplayRecordsForTests;
            for (int i = 0; i < RenderFrames; i++)
            {
                yield return null;
            }
            int extra = renderer.ReplayRecordsForTests - before;
            Object.Destroy(widget.GetComponentInParent<RivePanel>().gameObject);
            Object.Destroy(camera.gameObject);
            yield return null;

            Assert.AreEqual(0, extra, "The frame loop recorded a panel the pipeline had already recorded.");
        }

        // Two panels in a scene of their own: a data bound file and one without.
        private IEnumerator Round(int round, ThreadingMode mode, System.Action<uint[]> whileLoaded)
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = SceneManager.CreateScene($"RiveRelease{mode}{round}");
            SceneManager.SetActiveScene(scene);

            RiveWidget bound = AddPanel(mode, m_databinding);
            RiveWidget plain = AddPanel(mode, m_hud);
            SceneManager.SetActiveScene(previous);

            yield return RivePanelTestUtils.WaitForLoaded(bound);
            yield return RivePanelTestUtils.WaitForLoaded(plain);
            for (int i = 0; i < RenderFrames; i++)
            {
                yield return null;
            }
            CommandTransport.Barrier();
            whileLoaded(Counts());

            yield return SceneManager.UnloadSceneAsync(scene);
            // A scene switch usually collects (UnloadUnusedAssets does), and
            // view model instances the widgets made go with their finalizers.
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();
            for (int i = 0; i < SettleFrames; i++)
            {
                yield return null;
            }
            CommandTransport.Barrier();
        }

        private static RiveWidget AddPanel(ThreadingMode mode, Asset asset)
        {
            RivePanel panel = RivePanelTestUtils.CreatePanel($"Panel{mode}");
            panel.ThreadingMode = mode;
            panel.SetDimensions(new Vector2(256, 256));
            RiveWidget widget = RivePanelTestUtils.CreateWidget<RiveWidget>();
            widget.transform.SetParent(panel.WidgetContainer, false);
            RivePanelTestUtils.MakeWidgetFillPanel(widget);
            widget.Load(asset);
            return widget;
        }

        private static uint[] Counts()
        {
            var counts = new uint[s_countNames.Length];
            HostNative.riveLiveCounts(counts, (uint)counts.Length);
            return counts;
        }
    }
}
