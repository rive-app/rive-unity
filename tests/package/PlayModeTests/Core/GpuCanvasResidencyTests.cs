using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Rive.Components;
using Rive.Tests.Utils;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rive.Tests
{
    /// <summary>
    /// Checks that destroying a panel releases the GPU resources its file was
    /// using.
    ///
    /// These go through the normal panel/widget flow: load an asset into a
    /// widget, let it render, destroy the GameObject.
    ///
    /// They assert on GpuCanvasDiagnostics rather than Unity's memory counters,
    /// which can't see Rive's GPU allocations at all.
    /// </summary>
    public class GpuCanvasResidencyTests
    {
        // Destroys don't reach the command stream until the next recorded
        // frame, and don't free until the replay after that. Plenty of room.
        private const int SettleFrames = 10;

        private TestAssetLoadingManager m_assetLoader;
        private Camera m_camera;
        private readonly List<GameObject> m_spawned = new List<GameObject>();
        private readonly List<OutOfBandAsset> m_outOfBandAssets =
            new List<OutOfBandAsset>();
        private readonly List<File> m_files = new List<File>();

        // Only this fixture uses these ids, so each is a fresh import whose
        // canvas content nobody has taken yet.
        private const int PrivateOreCacheId = 0x6F7265;
        private const int HeldFrameOreCacheId = 0x6F7266;
        private const int FreedCanvasOreCacheId = 0x6F7267;

        /// <summary>
        /// A panel, plus the native file handle behind it. We keep the handle
        /// so we can check the file really unloaded once the panel is gone.
        /// File.Load shares a ref-counted instance per asset, so anything else
        /// holding the same asset would keep it alive and make the residency
        /// numbers mean nothing.
        /// </summary>
        private class PanelUnderTest
        {
            public RivePanel Panel;
            public RiveWidget Widget;
            public File File;
            // Pooled and atlas strategies keep their renderer until the
            // strategy itself is destroyed, so tearing down the panel alone
            // wouldn't release anything.
            public GameObject StrategyObject;
        }

        [SetUp]
        public void Setup()
        {
            m_assetLoader = new TestAssetLoadingManager();
            m_camera = new GameObject("Camera").AddComponent<Camera>();
            m_camera.tag = "MainCamera";
            m_spawned.Add(m_camera.gameObject);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject spawned in m_spawned)
            {
                if (spawned != null)
                {
                    UnityEngine.Object.Destroy(spawned);
                }
            }
            m_spawned.Clear();

            foreach (OutOfBandAsset asset in m_outOfBandAssets)
            {
                asset.Unload();
            }
            m_outOfBandAssets.Clear();

            m_assetLoader?.UnloadAllAssets();

            // Destroy is deferred to end of frame, so without this the panels
            // are still alive and still holding their files when the next
            // test loads the same asset.
            yield return null;

            foreach (File file in m_files)
            {
                if (!file.IsDisposed)
                {
                    file.Dispose();
                }
            }
            m_files.Clear();
        }

        // Destroys only reach the command stream when a frame is recorded. If
        // the panel that went away was the last one, nothing is left to record
        // one, so releasing has to happen some other way.
        [UnityTest]
        public IEnumerator Residency_ReturnsToBaseline_WhenPanelDestroyedAndNothingElseRenders()
        {
            yield return RequireGpuCanvas();

            PanelUnderTest panel = null;
            yield return CreateRenderingPanel(
                TestAssetReferences.riv_stormtrooper_bird, p => panel = p);

            // Compared against what's held while the panel is alive, not
            // against a starting count. An earlier test's resources can still
            // be sitting in the tables, and loading the same file again swaps
            // one identical set for another, so a starting count tells us
            // nothing.
            GpuCanvasResidency loaded = Residency();
            Assert.Greater(
                loaded.TotalObjects, 0u,
                "Nothing was resident while the panel was alive, so this test " +
                "can't tell whether resources get released. Either GPU canvas " +
                $"isn't recording or the panel never rendered. loaded={loaded}");

            Release(panel);

            // No panels left, so nothing records or replays from here.
            yield return WaitFrames(SettleFrames);
            AssertFileUnloaded(panel);

            GpuCanvasResidency after = Residency();
            Assert.Less(
                after.TotalObjects, loaded.TotalObjects,
                "Destroying the panel released nothing, because nothing " +
                "rendered afterwards to carry the destroys. " +
                $"loaded={loaded} after={after}");
        }

        // Same teardown, but a second panel keeps rendering.
        [UnityTest]
        public IEnumerator Residency_ReturnsToBaseline_WhenAnotherPanelKeepsRendering()
        {
            yield return RequireGpuCanvas();

            PanelUnderTest survivor = null;
            yield return CreateRenderingPanel(TestAssetReferences.riv_roboDude, p => survivor = p);

            // Baseline is "survivor loaded", so we're measuring the second
            // panel's resources specifically.
            GpuCanvasResidency survivorOnly = Residency();

            PanelUnderTest temporary = null;
            yield return CreateRenderingPanel(TestAssetReferences.riv_sophiaHud, p => temporary = p);

            GpuCanvasResidency both = Residency();
            AssertSomethingLoaded(survivorOnly, both);

            Release(temporary);
            yield return WaitFrames(SettleFrames);
            AssertFileUnloaded(temporary);

            // At most, not exactly. The survivor is animating, so it creates
            // and drops its own resources as it goes. What matters is that the
            // destroyed panel's are gone, which a leak would leave far above
            // the survivor's level.
            GpuCanvasResidency after = Residency();
            Assert.LessOrEqual(
                after.TotalObjects, survivorOnly.TotalObjects,
                "The destroyed panel's resources weren't released even though " +
                $"another panel kept rendering. survivorOnly={survivorOnly} " +
                $"both={both} after={after}");
        }

        // Releasing on teardown must
        // not take resources a still-loaded panel is using.
        [UnityTest]
        public IEnumerator RemainingPanel_StillRenders_AfterAnotherPanelIsDestroyed()
        {
            yield return RequireGpuCanvas();

            PanelUnderTest survivor = null;
            yield return CreateRenderingPanel(
                TestAssetReferences.riv_cleanTheCar, p => survivor = p);

            PanelUnderTest temporary = null;
            yield return CreateRenderingPanel(
                TestAssetReferences.riv_gameHudScope, p => temporary = p);

            Assert.IsTrue(
                HasVisiblePixels(survivor.Panel.RenderTexture),
                "The surviving panel wasn't drawing to begin with, so this test " +
                "can't tell whether destroying the other one broke it.");

            Release(temporary);
            yield return WaitFrames(SettleFrames);

            Assert.IsTrue(
                HasVisiblePixels(survivor.Panel.RenderTexture),
                "The surviving panel stopped drawing after an unrelated panel " +
                "was destroyed. Its resources were released along with the " +
                "other panel's.");
        }

        // Pooled and atlas strategies hold their renderer until the strategy
        // itself is destroyed, so their teardown order differs from the simple
        // one. Releasing has to cope with both.
        [UnityTest]
        public IEnumerator Residency_ReturnsToBaseline_WhenPooledStrategyIsTornDown()
        {
            yield return RequireGpuCanvas();
            yield return AssertTeardownReleases<PooledRenderTargetStrategy>(
                TestAssetReferences.riv_ratingAnimationWithEvents);
        }

        [UnityTest]
        public IEnumerator Residency_ReturnsToBaseline_WhenAtlasStrategyIsTornDown()
        {
            yield return RequireGpuCanvas();
            yield return AssertTeardownReleases<AtlasRenderTargetStrategy>(
                TestAssetReferences.riv_layoutTest);
        }

        // Swapping files in a live widget releases the old one through the
        // normal path, since the panel keeps rendering. Repeated so that a
        // leak shows up as growth rather than needing us to know what each
        // file costs.
        [UnityTest]
        public IEnumerator Residency_DoesNotGrow_WhenWidgetSwapsFilesRepeatedly()
        {
            yield return RequireGpuCanvas();

            PanelUnderTest panel = null;
            yield return CreateRenderingPanel(
                TestAssetReferences.riv_roboDude, p => panel = p);

            Asset other = null;
            yield return m_assetLoader.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_sophiaHud,
                asset => other = asset,
                () => Assert.Fail("Failed to load the second asset"));

            Asset first = null;
            yield return m_assetLoader.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_roboDude,
                asset => first = asset,
                () => Assert.Fail("Failed to load the first asset"));

            uint afterFirstCycle = 0;
            for (int cycle = 0; cycle < 4; ++cycle)
            {
                panel.Widget.Load(other);
                yield return WaitFrames(3);
                panel.Widget.Load(first);
                yield return WaitFrames(3);

                uint objects = Residency().TotalObjects;
                if (cycle == 0)
                {
                    afterFirstCycle = objects;
                    continue;
                }

                Assert.AreEqual(
                    afterFirstCycle, objects,
                    $"Swapping files is accumulating resources. After cycle 1 " +
                    $"there were {afterFirstCycle} objects; after cycle " +
                    $"{cycle + 1} there are {objects}.");
            }
        }

        // Files are loaded on demand, typically not at the end of a frame.
        // When a second file is loaded while another is still rendering, its GPU resources
        // get created after the first panel has already drawn. The renderer keeps all
        // resources available across rendering steps so future draws can access them.
        [UnityTest]
        public IEnumerator SecondFile_Renders_WhenImportedWhileAnotherIsDrawing()
        {
            yield return RequireGpuCanvas();

            PanelUnderTest first = null;
            yield return CreateRenderingPanel(
                TestAssetReferences.riv_stormtrooper_bird, p => first = p);

            Assert.IsTrue(
                HasVisiblePixels(first.Panel.RenderTexture),
                "The first panel drew nothing, so this test can't tell what " +
                "importing a second file did to it.");

            GpuCanvasResidency afterFirst = Residency();

            // Imports now, several frames into the first panel's rendering.
            PanelUnderTest second = null;
            yield return CreateRenderingPanel(
                TestAssetReferences.riv_roboDude, p => second = p);

            GpuCanvasResidency afterSecond = Residency();
            Assert.Greater(
                afterSecond.TotalObjects, afterFirst.TotalObjects,
                $"The second file made nothing resident, so nothing here is " +
                $"being tested. afterFirst={afterFirst} afterSecond={afterSecond}");

            Assert.IsTrue(
                HasVisiblePixels(second.Panel.RenderTexture),
                "The second file drew nothing. Its resources were recorded " +
                "after the first panel's frame was taken, so a draw that " +
                "can't find them means they were never made resident.");

            Assert.IsTrue(
                HasVisiblePixels(first.Panel.RenderTexture),
                "The first panel stopped drawing once a second file imported.");
        }

        // A file whose content is drawn by scripts onto a GPU canvas. Worth its
        // own tests because the canvas is recorded once at import and never
        // again, so anything that drops it loses it for good. The panel keeps
        // drawing, just black where the canvas should be.
        [UnityTest]
        public IEnumerator GpuCanvasContent_Renders()
        {
            yield return RequireGpuCanvas();

            PanelUnderTest panel = null;
            yield return CreateRenderingPanel(TestAssetReferences.riv_ore, p => panel = p);

            Assert.IsTrue(
                HasVisibleColor(panel.Panel.RenderTexture),
                "The GPU canvas file drew nothing but black, which is what a " +
                "dropped canvas looks like.");
        }

        // Releasing resources can replay the session outside a panel's frame.
        // The canvas has to survive that: it is recorded once and never again,
        // so anything that drops it takes the content with it.
        [UnityTest]
        public IEnumerator GpuCanvasContent_SurvivesAFlush()
        {
            yield return RequireGpuCanvas();

            PanelUnderTest canvasPanel = null;
            yield return CreateRenderingPanel(
                TestAssetReferences.riv_ore, p => canvasPanel = p);

            Assert.IsTrue(
                HasVisibleColor(canvasPanel.Panel.RenderTexture),
                "The GPU canvas file wasn't drawing before the flush, so this " +
                "test can't tell whether the flush broke it.");

            // Destroying a panel is what asks for a flush.
            PanelUnderTest temporary = null;
            yield return CreateRenderingPanel(
                TestAssetReferences.riv_stormtrooper_bird, p => temporary = p);
            Release(temporary);
            yield return WaitFrames(SettleFrames);

            Assert.IsTrue(
                HasVisibleColor(canvasPanel.Panel.RenderTexture),
                "The GPU canvas went black after resources were released. " +
                "Replaying the session without a GPU scripting context drops " +
                "the recorded canvas, and it is never recorded again.");
        }

        // The first frame taken gets the canvas content. Usually that's the
        // panel's first record, but a flush can get there first and it replays
        // with no screen open. The content only records once, so if the flush
        // doesn't run it the canvas stays black. Built-in records before the
        // flush each LateUpdate; URP and HDRP record after, so they hit this.
        [UnityTest]
        public IEnumerator GpuCanvasContent_Renders_WhenAFlushTakesTheFrameFirst()
        {
            yield return RequireGpuCanvas();

            Asset riveAsset = null;
            yield return m_assetLoader.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_ore,
                asset => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {TestAssetReferences.riv_ore}"));

            RivePanel panel = RivePanelTestUtils.CreatePanel();
            m_spawned.Add(panel.gameObject);
            panel.SetDimensions(new Vector2(512, 512));

            // Widget still ticks and records, but nothing takes a frame, so the
            // flush below is first to take on every pipeline.
            panel.StopRendering();

            RiveWidget widget = RivePanelTestUtils.CreateWidget<RiveWidget>();
            widget.transform.SetParent(panel.WidgetContainer, false);
            RivePanelTestUtils.MakeWidgetFillPanel(widget);

            File file = File.Load(riveAsset.Bytes, PrivateOreCacheId);
            m_files.Add(file);
            widget.Load(file);

            for (int i = 0; i < 120 && widget.Status != WidgetStatus.Loaded; ++i)
            {
                yield return null;
            }
            Assert.AreEqual(
                WidgetStatus.Loaded, widget.Status, "Widget never loaded ore.riv");

            // Give the scripts a few ticks to record their canvas content.
            yield return WaitFrames(3);

            uint flushesBefore = Residency().FlushReplays;
            GpuCanvasResources.RequestFlush();
            yield return WaitFrames(SettleFrames);
            Assert.Greater(
                Residency().FlushReplays, flushesBefore,
                "The flush had nothing to replay, so it never took the frame and " +
                "this test isn't reproducing the case it exists for.");

            panel.StartRendering();
            yield return WaitFrames(SettleFrames);

            Assert.IsTrue(
                HasVisiblePixels(panel.RenderTexture),
                "The panel drew nothing at all, so this can't say anything " +
                "about the canvas.");

            // Everything outside ore.riv's canvases is grey, so saturated pixels
            // only come from canvas content. The reference is ~2% saturated;
            // 0.5% leaves room for backend and colour space differences.
            RenderTexture target = panel.RenderTexture;
            int saturated = CountPixels(target, IsSaturated);
            int minimum = target.width * target.height / 200;
            Assert.GreaterOrEqual(
                saturated, minimum,
                $"The GPU canvas drew black ({saturated} saturated pixels, " +
                $"expected at least {minimum}). The flush took the frame " +
                "carrying the canvas content and replayed it without running it.");
        }

        // Frames share the replayer's resources, so they have to replay in the
        // order they were taken, across every queue. Here one queue holds back
        // the frame that created the artboard's resources and canvas content
        // (like a panel whose camera skipped a frame) while a panel keeps
        // drawing the same artboard. If the panel's newer frames replay first,
        // every draw drops and the panel stays blank for good.
        [UnityTest]
        public IEnumerator GpuCanvasContent_Renders_WhenAnotherQueueHoldsTheOlderFrame()
        {
            yield return RequireGpuCanvas();

            Asset riveAsset = null;
            yield return m_assetLoader.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_ore,
                asset => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {TestAssetReferences.riv_ore}"));

            RivePanel panel = RivePanelTestUtils.CreatePanel();
            m_spawned.Add(panel.gameObject);
            panel.SetDimensions(new Vector2(512, 512));
            // Nothing records until the held frame below has taken the content.
            panel.StopRendering();

            RiveWidget widget = RivePanelTestUtils.CreateWidget<RiveWidget>();
            widget.transform.SetParent(panel.WidgetContainer, false);
            RivePanelTestUtils.MakeWidgetFillPanel(widget);

            // Stopping the panel and earlier teardowns both request flushes. Let
            // them run out first, or one could take the content before the held
            // frame does and replay it early.
            yield return WaitFrames(SettleFrames);

            File file = File.Load(riveAsset.Bytes, HeldFrameOreCacheId);
            m_files.Add(file);
            widget.Load(file);
            for (int i = 0; i < 120 && widget.Status != WidgetStatus.Loaded; ++i)
            {
                yield return null;
            }
            Assert.AreEqual(
                WidgetStatus.Loaded, widget.Status, "Widget never loaded ore.riv");

            // Give the scripts a few ticks to record their canvas content.
            yield return WaitFrames(3);

            var heldTexture = new RenderTexture(TextureHelper.Descriptor(512, 512));
            heldTexture.Create();
            var heldQueue = new RenderQueue(heldTexture);
            try
            {
                // Same artboard instance, so this frame takes everything pending
                // since the file loaded, canvas content included. Recorded but
                // never submitted, so it sits in its queue unreplayed.
                Renderer held = heldQueue.Renderer();
                held.Align(Fit.Contain, Alignment.Center, widget.Artboard);
                held.Draw(widget.Artboard);
                uint pendingBefore = Residency().PendingFrames;
                held.RecordForGpuCanvas();
                Assert.Greater(
                    Residency().PendingFrames, pendingBefore,
                    "The held queue didn't take a frame, so this isn't testing " +
                    "anything.");

                panel.StartRendering();
                yield return WaitFrames(SettleFrames);

                Assert.IsTrue(panel.IsRendering, "The panel never started rendering.");

                // The held frame has the file's resource creates too, so out of
                // order every panel draw drops. A blank panel is this bug.
                Assert.IsTrue(
                    HasVisiblePixels(panel.RenderTexture),
                    "The panel drew nothing. Its frames replayed ahead of the " +
                    "older held frame that created the artboard's resources, so " +
                    "every draw dropped.");

                RenderTexture target = panel.RenderTexture;
                int saturated = CountPixels(target, IsSaturated);
                int minimum = target.width * target.height / 200;
                Assert.GreaterOrEqual(
                    saturated, minimum,
                    $"The GPU canvas drew black ({saturated} saturated pixels, " +
                    $"expected at least {minimum}). The panel replayed its own " +
                    "frames ahead of the older held frame that carried the " +
                    "canvas content.");
            }
            finally
            {
                heldQueue.Dispose();
                heldTexture.Release();
                UnityEngine.Object.Destroy(heldTexture);
            }
        }

        // The replayer's Ore views only point at the canvases the script made,
        // so a canvas has to outlive every view of it. Otherwise a frame that
        // replays after the script is gone draws into freed memory, which
        // crashes on Vulkan (e.g. hitting play in the editor).
        [UnityTest]
        public IEnumerator GpuCanvases_StayAlive_WhileTheReplayerHasViewsOfThem()
        {
            yield return RequireGpuCanvas();

            Asset riveAsset = null;
            yield return m_assetLoader.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_ore,
                asset => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {TestAssetReferences.riv_ore}"));

            // Other fixtures leave their own canvases behind, so measure what
            // ore.riv adds rather than trusting a starting count.
            uint before = Residency().CanvasesHeld;

            RivePanel panel = RivePanelTestUtils.CreatePanel();
            m_spawned.Add(panel.gameObject);
            panel.SetDimensions(new Vector2(512, 512));

            RiveWidget widget = RivePanelTestUtils.CreateWidget<RiveWidget>();
            widget.transform.SetParent(panel.WidgetContainer, false);
            RivePanelTestUtils.MakeWidgetFillPanel(widget);

            File file = File.Load(riveAsset.Bytes, FreedCanvasOreCacheId);
            m_files.Add(file);
            widget.Load(file);
            for (int i = 0; i < 120 && widget.Status != WidgetStatus.Loaded; ++i)
            {
                yield return null;
            }
            Assert.AreEqual(
                WidgetStatus.Loaded, widget.Status, "Widget never loaded ore.riv");

            // The canvases are made once, on the frame the file imports. Let it
            // replay, so anything still holding them isn't just that frame.
            yield return WaitFrames(SettleFrames);
            Assert.IsTrue(
                HasVisibleColor(panel.RenderTexture),
                "ore.riv never drew its GPU canvases, so there's nothing to hold.");

            GpuCanvasResidency drawing = Residency();
            uint filesLoaded = drawing.LiveFiles;
            Assert.Greater(
                drawing.CanvasesHeld, before,
                "Nothing holds the script's GPU canvases while the replayer still " +
                "has Ore views of them, so a frame replaying after the script goes " +
                $"away draws into freed memory. before={before} drawing={drawing}");

            // Once the file unloads, the views are destroyed and the canvases
            // should go with them.
            Artboard artboard = widget.Artboard;
            m_spawned.Remove(panel.gameObject);
            UnityEngine.Object.Destroy(panel.gameObject);
            yield return null;
            artboard?.Dispose();
            file.Dispose();
            yield return WaitFrames(SettleFrames);

            GpuCanvasResidency settled = Residency();
            for (int i = 0; i < 30 && settled.LiveFiles >= filesLoaded; ++i)
            {
                yield return null;
                settled = Residency();
            }
            Assert.Less(
                settled.LiveFiles, filesLoaded,
                "ore.riv is still loaded, so this can't tell whether its canvases " +
                $"were released. settled={settled}");
            Assert.LessOrEqual(
                settled.CanvasesHeld, before,
                "The canvases are still held after the file unloaded and its Ore " +
                $"views were destroyed, so they leak. before={before} settled={settled}");
        }

        // Out of band images take a different route from a file's own resources:
        // they are decoded through their own entry point and released by their
        // own unload, so they need their own coverage.
        [UnityTest]
        public IEnumerator Residency_ReturnsToBaseline_WhenOutOfBandImageIsUnloaded()
        {
            yield return RequireGpuCanvas();

            // A panel keeps frames flowing, so creates and destroys get
            // replayed the ordinary way.
            PanelUnderTest panel = null;
            yield return CreateRenderingPanel(
                TestAssetReferences.riv_roboDude, p => panel = p);

            GpuCanvasResidency before = Residency();

            ImageOutOfBandAsset image = null;
            yield return LoadOutOfBandImage(a => image = a);
            image.Load();
            yield return WaitFrames(3);

            GpuCanvasResidency loaded = Residency();
            Assert.Greater(
                loaded.Images, before.Images,
                "Loading the image made nothing resident, so this test can't " +
                $"tell whether unloading releases it. before={before} loaded={loaded}");

            image.Unload();
            yield return WaitFrames(SettleFrames);

            GpuCanvasResidency after = Residency();
            Assert.AreEqual(
                before.Images, after.Images,
                "The image's texture is still resident after it was unloaded. " +
                $"before={before} loaded={loaded} after={after}");
        }

        // The same unload with no panel left, so nothing is going to record a
        // frame that would carry the destroy.
        [UnityTest]
        public IEnumerator Residency_ReturnsToBaseline_WhenOutOfBandImageUnloadsWithNoPanel()
        {
            yield return RequireGpuCanvas();

            PanelUnderTest panel = null;
            yield return CreateRenderingPanel(
                TestAssetReferences.riv_roboDude, p => panel = p);

            GpuCanvasResidency before = Residency();

            ImageOutOfBandAsset image = null;
            yield return LoadOutOfBandImage(a => image = a);
            image.Load();
            yield return WaitFrames(3);

            GpuCanvasResidency loaded = Residency();
            Assert.Greater(
                loaded.Images, before.Images,
                "Loading the image made nothing resident, so this test can't " +
                $"tell whether unloading releases it. before={before} loaded={loaded}");

            Release(panel);
            image.Unload();
            yield return WaitFrames(SettleFrames);

            GpuCanvasResidency after = Residency();
            Assert.LessOrEqual(
                after.Images, before.Images,
                "The image's texture is still resident after it was unloaded " +
                "with no panel left to render. " +
                $"before={before} loaded={loaded} after={after}");
        }

        // -- helpers --

        /// <summary>
        /// Loads a panel on the given strategy, tears both down, and checks the
        /// resources went with them.
        /// </summary>
        private IEnumerator AssertTeardownReleases<T>(string addressablePath)
            where T : RenderTargetStrategy
        {
            T strategy = CreateStrategy<T>();

            // Other fixtures leave residency behind, so "something is resident"
            // says nothing about our panel. Measure what it adds.
            GpuCanvasResidency before = Residency();

            PanelUnderTest panel = null;
            yield return CreateRenderingPanel(
                addressablePath, p => panel = p, strategy);

            GpuCanvasResidency loaded = Residency();
            Assert.Greater(
                loaded.TotalObjects, before.TotalObjects,
                $"The panel added nothing resident on a {typeof(T).Name}, so " +
                "this test can't tell whether teardown releases anything. It " +
                "most likely never rendered. " +
                $"before={before} loaded={loaded}");

            Assert.IsTrue(
                HasVisiblePixels(panel.Panel.RenderTexture),
                $"The panel drew nothing on a {typeof(T).Name}, so there is " +
                "nothing for teardown to release.");

            Release(panel);
            yield return WaitFrames(SettleFrames);
            AssertFileUnloaded(panel);

            GpuCanvasResidency after = Residency();
            Assert.Less(
                after.TotalObjects, loaded.TotalObjects,
                $"Tearing down a {typeof(T).Name} released nothing. " +
                $"loaded={loaded} after={after}");
        }

        private static GpuCanvasResidency Residency()
        {
            Assert.IsTrue(
                GpuCanvasDiagnostics.TryRead(out GpuCanvasResidency residency),
                "RiveGetCanvasResidency is missing from the loaded plugin. The " +
                "editor loads the plugin once per session, so restart it after " +
                "rebuilding.");
            return residency;
        }

        private static IEnumerator RequireGpuCanvas()
        {
            if (!GpuCanvasDiagnostics.TryRead(out _))
            {
                Assert.Ignore("This plugin has no residency export.");
            }
            yield break;
        }

        // If this fires, the test can't observe anything, so it's worth its own
        // message rather than looking like a leak.
        private static void AssertSomethingLoaded(
            GpuCanvasResidency before, GpuCanvasResidency after)
        {
            Assert.Greater(
                after.TotalObjects, before.TotalObjects,
                "Loading the panel made nothing resident, so this test can't " +
                "tell whether resources are released. Either GPU canvas isn't " +
                $"recording or the panel never rendered. before={before} after={after}");
        }

        /// <summary>
        /// Builds a panel with one widget, loads the file, and renders a few
        /// frames so its resources are live.
        /// </summary>
        private IEnumerator CreateRenderingPanel(
            string addressablePath,
            System.Action<PanelUnderTest> onReady,
            RenderTargetStrategy strategy = null)
        {
            Asset riveAsset = null;
            yield return m_assetLoader.LoadAssetCoroutine<Asset>(
                addressablePath,
                asset => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {addressablePath}"));

            RivePanel panel = RivePanelTestUtils.CreatePanel();
            m_spawned.Add(panel.gameObject);
            if (strategy != null)
            {
                // The panel starts on its own default strategy; it has to stop
                // rendering before it will register with a different one.
                if (panel.IsRendering)
                {
                    panel.StopRendering();
                }
                panel.RenderTargetStrategy = strategy;
            }
            panel.SetDimensions(new Vector2(512, 512));

            RiveWidget widget = RivePanelTestUtils.CreateWidget<RiveWidget>();
            widget.transform.SetParent(panel.WidgetContainer, false);
            RivePanelTestUtils.MakeWidgetFillPanel(widget);

            widget.Load(riveAsset);

            for (int i = 0; i < 120 && widget.Status != WidgetStatus.Loaded; ++i)
            {
                yield return null;
            }
            Assert.AreEqual(
                WidgetStatus.Loaded, widget.Status,
                $"Widget never loaded {addressablePath}");

            // Assigning a strategy leaves the panel unregistered, so it has to
            // be told to start again before anything draws.
            if (!panel.IsRendering)
            {
                panel.StartRendering();
            }

            // Panels are on Auto update, so the orchestrator ticks them and the
            // render pass runs at end of frame.
            yield return WaitFrames(3);

            Assert.IsNotNull(widget.File, "Widget reported no file after loading.");
            onReady(new PanelUnderTest
            {
                Panel = panel,
                Widget = widget,
                File = widget.File,
                StrategyObject = strategy != null ? strategy.gameObject : null,
            });
        }

        /// <summary>
        /// What a user does. The widget disposes the file it loaded from an
        /// asset in OnDestroy, which Unity runs at the end of the frame.
        /// </summary>
        private void Release(PanelUnderTest panel)
        {
            GameObject panelObject = panel.Panel.gameObject;
            m_spawned.Remove(panelObject);
            UnityEngine.Object.Destroy(panelObject);

            if (panel.StrategyObject != null)
            {
                m_spawned.Remove(panel.StrategyObject);
                UnityEngine.Object.Destroy(panel.StrategyObject);
            }
        }

        private IEnumerator LoadOutOfBandImage(
            System.Action<ImageOutOfBandAsset> onLoaded)
        {
            string path = TestAssetReferences.imageasset_dog1_asset_swapping;
            ImageOutOfBandAsset asset = null;
            yield return m_assetLoader.LoadAssetCoroutine<ImageOutOfBandAsset>(
                path,
                loaded => asset = loaded,
                () => Assert.Fail($"Failed to load asset at {path}"));

            Assert.IsNotNull(asset, $"Expected an image asset at {path}");
            m_outOfBandAssets.Add(asset);
            onLoaded(asset);
        }

        private T CreateStrategy<T>() where T : RenderTargetStrategy
        {
            var strategyObject = new GameObject(typeof(T).Name);
            m_spawned.Add(strategyObject);
            return strategyObject.AddComponent<T>();
        }

        /// <summary>
        /// Confirms destroying the panel actually unloaded the file, so a
        /// residency assertion after this is about the replayer and not about
        /// someone else still holding the asset.
        /// </summary>
        private static void AssertFileUnloaded(PanelUnderTest panel)
        {
            // IsDisposed rather than IsRiveFileValid. That one reads through a
            // pointer the runtime may already have freed, so it answers
            // differently run to run.
            Assert.IsTrue(
                panel.File.IsDisposed,
                "The file is still loaded after its panel was destroyed. Either " +
                "the widget didn't dispose it, or something else holds the same " +
                "asset. Either way the residency numbers below say nothing " +
                "about whether the replayer released anything.");
        }

        private static IEnumerator WaitFrames(int frames)
        {
            for (int i = 0; i < frames; ++i)
            {
                yield return null;
                yield return new WaitForEndOfFrame();
            }
        }

        /// <summary>
        /// True if any pixel has alpha. A blank panel means nothing drew, which
        /// is all these tests need to know.
        /// </summary>
        /// <summary>
        /// True if anything is drawn in a colour, not just drawn. A dropped GPU
        /// canvas still fills the panel with black, so alpha alone can't tell
        /// the difference.
        /// </summary>
        private static bool HasVisibleColor(RenderTexture renderTexture)
        {
            return AnyPixel(
                renderTexture,
                pixel => pixel.a != 0 &&
                    Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b)) > 32);
        }

        private static bool HasVisiblePixels(RenderTexture renderTexture)
        {
            return AnyPixel(renderTexture, pixel => pixel.a != 0);
        }

        private static bool IsSaturated(Color32 pixel)
        {
            int max = Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b));
            int min = Mathf.Min(pixel.r, Mathf.Min(pixel.g, pixel.b));
            return pixel.a > 128 && max > 80 && max - min > 60;
        }

        private static bool AnyPixel(
            RenderTexture renderTexture, System.Func<Color32, bool> predicate)
        {
            return CountPixels(renderTexture, predicate, stopAtFirst: true) > 0;
        }

        private static int CountPixels(
            RenderTexture renderTexture,
            System.Func<Color32, bool> predicate,
            bool stopAtFirst = false)
        {
            Assert.IsNotNull(renderTexture, "Panel has no render texture.");

            RenderTexture previous = RenderTexture.active;
            var readback = new Texture2D(
                renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = renderTexture;
                readback.ReadPixels(
                    new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
                readback.Apply();

                int count = 0;
                foreach (Color32 pixel in readback.GetPixels32())
                {
                    if (predicate(pixel))
                    {
                        count++;
                        if (stopAtFirst)
                        {
                            break;
                        }
                    }
                }
                return count;
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.Destroy(readback);
            }
        }
    }
}
