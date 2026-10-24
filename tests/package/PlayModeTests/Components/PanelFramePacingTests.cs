using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using NUnit.Framework;
using Rive.Components;
using Rive.Host;
using Rive.Tests.Utils;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rive.Tests
{
    /// <summary>
    /// What reaches a panel's texture each frame, read back from it. The clock
    /// file's bar moves 60 px a second, so a frame that shows the last picture
    /// again leaves the bar where it was, and the one after jumps two steps.
    /// Rive's thread is made late on purpose by parking it for a while after
    /// the advance goes out, before the panel records.
    /// </summary>
    public class PanelFramePacingTests
    {
        // Slow enough that a frame is much longer than a stall, so being late
        // only ever means late for this frame's render event.
        private const int FrameRate = 20;
        private const int StallMs = 20;
        private const int StallEvery = 3;
        // How many frames Rive is held for when it falls behind.
        private const int BehindFrames = 6;
        private const int WarmupFrames = 10;
        private const int MeasuredFrames = 30;
        // The bar's speed in px per ms.
        private const double BarSpeed = 0.06;

        private TestAssetLoadingManager m_assets;
        private Asset m_clock;
        private readonly List<GameObject> m_objects = new List<GameObject>();
        private readonly List<Thread> m_releases = new List<Thread>();
        private Texture2D m_readback;
        private int m_vSyncCount;
        private int m_targetFrameRate;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            m_vSyncCount = QualitySettings.vSyncCount;
            m_targetFrameRate = Application.targetFrameRate;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = FrameRate;

            m_assets = new TestAssetLoadingManager();
            yield return m_assets.LoadAssetCoroutine<Asset>(TestAssetReferences.riv_pacingClock,
                a => m_clock = a, () => Assert.Fail("Failed to load the clock asset"));

            // Nothing renders without a camera.
            m_objects.Add(new GameObject("FramePacingCamera", typeof(Camera)));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Thread release in m_releases)
            {
                release.Join();
            }
            m_releases.Clear();
            QualitySettings.vSyncCount = m_vSyncCount;
            Application.targetFrameRate = m_targetFrameRate;
            foreach (GameObject go in m_objects)
            {
                if (go != null)
                {
                    Object.Destroy(go);
                }
            }
            m_objects.Clear();
            if (m_readback != null)
            {
                Object.Destroy(m_readback);
                m_readback = null;
            }
            m_assets?.UnloadAllAssets();
        }

        [UnityTest]
        public IEnumerator BackgroundPanel_WithRiveLateOnSomeFrames_ShowsEveryFrameOnce()
        {
            if (!CommandTransport.IsThreaded)
            {
                Assert.Ignore("Needs Rive's own thread.");
            }
            RivePanel panel = AddClockPanel(ThreadingMode.BackgroundThread, out RiveWidget clock);
            yield return RivePanelTestUtils.WaitForLoaded(clock);
            yield return Frames(WarmupFrames);

            var bars = new double[MeasuredFrames];
            var deltas = new double[MeasuredFrames];
            var stalled = new bool[MeasuredFrames];
            for (int f = 0; f < MeasuredFrames; f++)
            {
                // Here the advance has gone out and the panel hasn't recorded.
                stalled[f] = f % StallEvery == 1;
                if (stalled[f])
                {
                    StallRive(StallMs);
                }
                yield return new WaitForEndOfFrame();
                bars[f] = ReadBar(panel.RenderTexture);
                deltas[f] = Time.unscaledDeltaTime * 1000.0;
                yield return null;
            }

            int repeated = 0, jumped = 0, missing = 0;
            var log = new StringBuilder();
            for (int f = 1; f < MeasuredFrames; f++)
            {
                log.Append($"{f}{(stalled[f] ? "*" : "")}: {bars[f]:F1}; ");
                if (bars[f] < 0)
                {
                    missing++;
                    continue;
                }
                if (bars[f - 1] < 0)
                {
                    continue;
                }
                double moved = Wrap(bars[f] - bars[f - 1], panel.RenderTexture.width);
                // A frame may show this frame's step or the last one's, so
                // judge against both.
                double shortest = System.Math.Min(deltas[f], deltas[f - 1]) * BarSpeed;
                double longest = System.Math.Max(deltas[f], deltas[f - 1]) * BarSpeed;
                if (moved < shortest * 0.25)
                {
                    repeated++;
                }
                else if (moved > longest * 1.75)
                {
                    jumped++;
                }
            }
            Assert.AreEqual(0, missing, $"The clock should show every frame. Bars (* = Rive late): {log}");
            Assert.AreEqual(0, repeated,
                $"No frame should show the last picture again when Rive is late. Bars (* = Rive late): {log}");
            Assert.AreEqual(0, jumped,
                $"No frame should skip a step when Rive is late. Bars (* = Rive late): {log}");
        }

        [UnityTest]
        public IEnumerator BackgroundPanel_WithRiveBehindForFrames_CatchesUpOnTheNewest()
        {
            if (!CommandTransport.IsThreaded)
            {
                Assert.Ignore("Needs Rive's own thread.");
            }
            RivePanel panel = AddClockPanel(ThreadingMode.BackgroundThread, out RiveWidget clock);
            yield return RivePanelTestUtils.WaitForLoaded(clock);
            yield return Frames(WarmupFrames);

            int baseline = CommandTransport.PendingRequestsForTests;
            HostNative.riveServerTimingEnable(true);
            try
            {
                // Several frames' worth of records pile up behind the hold.
                StallRive(BehindFrames * 1000 / FrameRate);
                int peak = baseline;
                int heldFrames = 0;
                while (m_releases[m_releases.Count - 1].IsAlive)
                {
                    yield return null;
                    heldFrames++;
                    peak = System.Math.Max(peak, CommandTransport.PendingRequestsForTests);
                }

                // Then it should be back to one step a frame within a few.
                int recovered = -1;
                double last = -1;
                double lastDelta = 0;
                for (int f = 0; f < 10 && recovered < 0; f++)
                {
                    yield return new WaitForEndOfFrame();
                    double bar = ReadBar(panel.RenderTexture);
                    double delta = Time.unscaledDeltaTime * 1000.0;
                    if (bar >= 0 && last >= 0)
                    {
                        double moved = Wrap(bar - last, panel.RenderTexture.width);
                        double shortest = System.Math.Min(delta, lastDelta) * BarSpeed;
                        double longest = System.Math.Max(delta, lastDelta) * BarSpeed;
                        if (moved >= shortest * 0.25 && moved <= longest * 1.75)
                        {
                            recovered = f;
                        }
                    }
                    last = bar;
                    lastDelta = delta;
                    yield return null;
                }
                int settled = CommandTransport.PendingRequestsForTests;

                var counts = new uint[7];
                HostNative.riveCanvasPacingTake(counts, (uint)counts.Length);
                string numbers = $"held {heldFrames} frames, pending requests baseline {baseline}, peak {peak}, after {settled}; " +
                    $"records superseded {counts[6]}, made {counts[0]}; back to one step a frame after {recovered} frames";
                Debug.Log($"Rive behind for frames: {numbers}");
                Assert.GreaterOrEqual((int)counts[6], heldFrames - 2,
                    $"Records waiting behind the hold should give way to the newest. {numbers}");
                Assert.IsTrue(recovered >= 0 && recovered <= 3,
                    $"The panel should be back to one step a frame within 3 frames. {numbers}");
                Assert.LessOrEqual(settled, baseline + 2,
                    $"What piled up should have settled. {numbers}");
            }
            finally
            {
                HostNative.riveServerTimingEnable(false);
            }
        }

        [UnityTest]
        public IEnumerator BackgroundPanels_ChangedWhileRiveWaits_ShowTheirLatestState()
        {
            if (!CommandTransport.IsThreaded)
            {
                Assert.Ignore("Needs Rive's own thread.");
            }
            RivePanel resized = AddClockPanel(ThreadingMode.BackgroundThread, out RiveWidget first);
            RivePanel released = AddClockPanel(ThreadingMode.BackgroundThread, out RiveWidget second);
            yield return RivePanelTestUtils.WaitForLoaded(first);
            yield return RivePanelTestUtils.WaitForLoaded(second);
            yield return Frames(WarmupFrames);

            // Warnings and errors only. Plain logs aren't problems.
            var problems = new List<string>();
            Application.LogCallback collect = (message, stack, type) =>
            {
                if (type != LogType.Log)
                {
                    problems.Add($"{type}: {message}");
                }
            };
            Application.logMessageReceived += collect;
            try
            {
                // Records for every panel queue up behind the hold, with a
                // resize, a new panel and a destroyed one in between.
                StallRive(BehindFrames * 1000 / FrameRate);
                yield return null;
                resized.SetDimensions(new Vector2(256, 96));
                RivePanel created = AddClockPanel(ThreadingMode.BackgroundThread, out RiveWidget third);
                Object.Destroy(released.gameObject);
                while (m_releases[m_releases.Count - 1].IsAlive)
                {
                    yield return null;
                }
                // Everything queued so far has run, so this frame's event shows
                // the newest record from before the release, and nothing
                // recorded after it can stand in for it.
                CommandTransport.Barrier();
                yield return new WaitForEndOfFrame();
                Assert.AreEqual(96, resized.RenderTexture.height, "The resized panel should have its new size.");
                Assert.GreaterOrEqual(ReadBar(resized.RenderTexture), 0,
                    "The first frame after Rive catches up should show the resized panel's clock at its new size.");

                yield return RivePanelTestUtils.WaitForLoaded(third);
                yield return Frames(3);
                yield return new WaitForEndOfFrame();
                Assert.GreaterOrEqual(ReadBar(created.RenderTexture), 0, "The panel made while Rive waited should show the clock.");
                Assert.IsEmpty(problems, "Nothing should go wrong when Rive catches up: " + string.Join("; ", problems));
            }
            finally
            {
                Application.logMessageReceived -= collect;
            }
        }

        [UnityTest]
        public IEnumerator BackgroundRecords_WaitingOnRive_OnlyTheNewestDraws()
        {
            if (!CommandTransport.IsThreaded)
            {
                Assert.Ignore("Needs Rive's own thread.");
            }
            var texture = new RenderTexture(TextureHelper.Descriptor(512, 64));
            texture.Create();
            File file = File.Load(m_clock);
            var queue = new RenderQueue(texture);
            HostNative.riveServerTimingEnable(true);
            try
            {
                Artboard artboard = file.Artboard(0);
                StateMachine stateMachine = artboard.StateMachine();
                stateMachine.Advance(0f);
                Renderer renderer = queue.Renderer();
                yield return Prime(renderer, artboard, texture, Alignment.CenterRight);

                // Each draws the bar somewhere else: prime and A at the right,
                // B at the left, C in the middle.
                renderer.SetRecordsAsynchronously(true);
                HostNative.riveCanvasPacingTake(new uint[7], 7);
                StallRive(BehindFrames * 1000 / FrameRate);
                Record(renderer, artboard, Alignment.CenterRight);
                Record(renderer, artboard, Alignment.CenterLeft);
                Record(renderer, artboard, Alignment.Center);
                yield return UntilRiveLetGo(renderer);

                var counts = new uint[7];
                HostNative.riveCanvasPacingTake(counts, (uint)counts.Length);
                Assert.AreEqual(2u, counts[6], "A and B should give way to C.");

                // One render event, with nothing recorded after C to fix it up.
                yield return null;
                ReplayOnce(queue, texture);
                yield return new WaitForEndOfFrame();
                List<double> bars = ReadBars(texture);
                Assert.AreEqual(1, bars.Count, $"One bar should show. Got {Join(bars)}.");
                Assert.That(bars[0], Is.InRange(64.0, 192.0), $"C's bar, in the middle, should show. Got {Join(bars)}.");
            }
            finally
            {
                HostNative.riveServerTimingEnable(false);
                queue.Dispose();
                file.Dispose();
                texture.Release();
                Object.Destroy(texture);
            }
        }

        [UnityTest]
        public IEnumerator SupersededRecord_PassesItsForcedRedraw_ToTheNewest()
        {
            if (!CommandTransport.IsThreaded)
            {
                Assert.Ignore("Needs Rive's own thread.");
            }
            var texture = new RenderTexture(TextureHelper.Descriptor(512, 64));
            texture.Create();
            File file = File.Load(m_clock);
            var queue = new RenderQueue(texture);
            HostNative.riveServerTimingEnable(true);
            try
            {
                Artboard artboard = file.Artboard(0);
                StateMachine stateMachine = artboard.StateMachine();
                stateMachine.Advance(0f);
                Renderer renderer = queue.Renderer();
                renderer.SetArtboardDirtCheckEnabled(true);
                yield return Prime(renderer, artboard, texture, Alignment.Center);

                // The artboard isn't moving, so once a record has seen it
                // settle, a record that isn't forced draws nothing. The first
                // draw counts as a change, and how many submits the prime
                // took depends on the backend, so record until it's quiet.
                renderer.SetRecordsAsynchronously(true);
                var counts = new uint[7];
                HostNative.riveCanvasPacingTake(counts, (uint)counts.Length);
                uint published = 0;
                for (int i = 0; i < 5 && counts[1] == 0; i++)
                {
                    renderer.RecordForGpuCanvas();
                    renderer.SettleRecordsForTests();
                    HostNative.riveCanvasPacingTake(counts, (uint)counts.Length);
                    published += counts[0];
                }
                Assert.AreEqual(1u, counts[1],
                    $"An unforced record of an unchanged artboard should be skipped. Made {published} before it went quiet.");

                // X is forced, since Clear forces the next record. Y adds a
                // second bar and isn't forced, so only X's force can draw it.
                StallRive(BehindFrames * 1000 / FrameRate);
                renderer.Clear();
                renderer.Align(Fit.None, Alignment.CenterLeft, artboard);
                renderer.Draw(artboard);
                renderer.RecordForGpuCanvas();
                renderer.Align(Fit.None, Alignment.CenterRight, artboard);
                renderer.Draw(artboard);
                renderer.RecordForGpuCanvas();
                yield return UntilRiveLetGo(renderer);

                HostNative.riveCanvasPacingTake(counts, (uint)counts.Length);
                Assert.AreEqual(1u, counts[6], "X should give way to Y.");
                Assert.AreEqual(1u, counts[0], "Y should be recorded with X's force, not skipped as unchanged.");

                yield return null;
                ReplayOnce(queue, texture);
                yield return new WaitForEndOfFrame();
                List<double> bars = ReadBars(texture);
                Assert.AreEqual(2, bars.Count, $"Y's two bars should show. Got {Join(bars)}.");
            }
            finally
            {
                HostNative.riveServerTimingEnable(false);
                queue.Dispose();
                file.Dispose();
                texture.Release();
                Object.Destroy(texture);
            }
        }

        [UnityTest]
        public IEnumerator MainThreadPanel_ShowsAnInputChange_InTheFrameItWasMade()
        {
            RivePanel panel = AddClockPanel(ThreadingMode.MainThread, out RiveWidget clock);
            yield return RivePanelTestUtils.WaitForLoaded(clock);
            yield return Frames(WarmupFrames);

            SMIBool flag = clock.LoadedStateMachine.GetBool("flag");
            Assert.IsNotNull(flag, "The clock file should have a flag input.");
            yield return new WaitForEndOfFrame();
            Assert.IsFalse(ReadFlag(panel.RenderTexture), "The flag should start off.");

            // Set after this frame rendered, so the next frame's advance is the
            // first to see it, and that same frame should show it.
            flag.Value = true;
            yield return null;
            yield return new WaitForEndOfFrame();
            Assert.IsTrue(ReadFlag(panel.RenderTexture),
                "A main thread panel should show a change in the frame that advanced with it, not a frame later.");
        }

        [UnityTest]
        public IEnumerator Renderer_SubmittedOnce_ShowsWithoutAnotherSubmit()
        {
            var texture = new RenderTexture(TextureHelper.Descriptor(256, 64));
            texture.Create();
            File file = File.Load(m_clock);
            var queue = new RenderQueue(texture);
            try
            {
                Artboard artboard = file.Artboard(0);
                StateMachine stateMachine = artboard.StateMachine();
                // An advance lays the artboard out, or it draws nothing.
                stateMachine.Advance(0f);
                Renderer renderer = queue.Renderer();

                // A new texture may not be ready for the first few, so draw
                // until it shows.
                double before = -1;
                int submits = 0;
                while (before < 0 && submits < 10)
                {
                    Draw(renderer, artboard);
                    submits++;
                    yield return new WaitForEndOfFrame();
                    before = ReadBar(texture);
                    yield return null;
                }
                Assert.GreaterOrEqual(before, 0, "The clock never showed in the texture.");
                Debug.Log($"The clock first showed after {submits} submits.");

                // Half way round, drawn once.
                stateMachine.Advance(2f);
                Draw(renderer, artboard);
                bool moved = false;
                double after = before;
                for (int i = 0; i < 5 && !moved; i++)
                {
                    yield return new WaitForEndOfFrame();
                    after = ReadBar(texture);
                    moved = after >= 0 && System.Math.Abs(Wrap(after - before, texture.width)) > 60;
                    yield return null;
                }
                Assert.IsTrue(moved,
                    $"A single Submit should reach the texture without another one after it. The bar went from {before:F1} to {after:F1}.");
            }
            finally
            {
                queue.Dispose();
                file.Dispose();
                texture.Release();
                Object.Destroy(texture);
            }
        }

        private RivePanel AddClockPanel(ThreadingMode mode, out RiveWidget clock)
        {
            RivePanel panel = RivePanelTestUtils.CreatePanel($"FramePacing{mode}");
            m_objects.Add(panel.gameObject);
            panel.ThreadingMode = mode;
            panel.DrawOptimization = DrawOptimizationOptions.AlwaysDraw;
            panel.SetDimensions(new Vector2(256, 64));
            clock = RivePanelTestUtils.CreateWidget<RiveWidget>();
            clock.transform.SetParent(panel.WidgetContainer, false);
            RivePanelTestUtils.MakeWidgetFillPanel(clock);
            clock.Fit = Fit.Fill;
            clock.Load(m_clock);
            return panel;
        }

        private void StallRive(int milliseconds)
        {
            m_releases.Add(ServerGate.HoldFor(milliseconds));
        }

        // Draws with one-off submits until the texture shows it, since a new
        // texture may not be ready for the first few.
        private IEnumerator Prime(Renderer renderer, Artboard artboard, RenderTexture texture, Alignment alignment)
        {
            for (int i = 0; i < 10; i++)
            {
                renderer.Clear();
                renderer.Align(Fit.None, alignment, artboard);
                renderer.Draw(artboard);
                renderer.Submit();
                yield return new WaitForEndOfFrame();
                if (ReadBars(texture).Count > 0)
                {
                    yield return null;
                    yield break;
                }
                yield return null;
            }
            Assert.Fail("The clock never showed in the texture.");
        }

        private static void Record(Renderer renderer, Artboard artboard, Alignment alignment)
        {
            renderer.Clear();
            renderer.Align(Fit.None, alignment, artboard);
            renderer.Draw(artboard);
            renderer.RecordForGpuCanvas();
        }

        private IEnumerator UntilRiveLetGo(Renderer renderer)
        {
            while (m_releases[m_releases.Count - 1].IsAlive)
            {
                yield return null;
            }
            renderer.SettleRecordsForTests();
        }

        // A render event on its own, with no record before it.
        private static void ReplayOnce(RenderQueue queue, RenderTexture texture)
        {
            var commandBuffer = new UnityEngine.Rendering.CommandBuffer { name = "Rive.ReplayOnce" };
            commandBuffer.SetRenderTarget(texture);
            commandBuffer.IssuePluginEventAndData(
                Renderer.getRenderCommandBufferCallback(),
                0,
                Rive.Producer.RenderLifetime.EventData(queue.RenderId, Rive.Producer.RenderLifetime.FrameBound()));
            Graphics.ExecuteCommandBuffer(commandBuffer);
            commandBuffer.Release();
        }

        // Every bar's center in the bar's row, in pixels.
        private List<double> ReadBars(RenderTexture texture)
        {
            Color32[] pixels = Read(texture);
            int width = texture.width;
            int height = texture.height;
            var bars = new List<double>();
            for (int pass = 0; pass < 2 && bars.Count == 0; pass++)
            {
                int y = pass == 0 ? height / 4 : height - 1 - height / 4;
                int start = -1;
                bool tooWide = false;
                for (int x = 0; x <= width; x++)
                {
                    bool bright = x < width && pixels[y * width + x].r > 128;
                    if (bright && start < 0)
                    {
                        start = x;
                    }
                    else if (!bright && start >= 0)
                    {
                        int length = x - start;
                        if (length > 12)
                        {
                            tooWide = true;
                        }
                        else
                        {
                            bars.Add(start + (length - 1) / 2.0);
                        }
                        start = -1;
                    }
                }
                if (tooWide)
                {
                    bars.Clear();
                }
            }
            return bars;
        }

        private static string Join(List<double> values)
        {
            return values.Count == 0 ? "none" : string.Join(", ", values.ConvertAll(v => v.ToString("F1")));
        }

        private static void Draw(Renderer renderer, Artboard artboard)
        {
            renderer.Clear();
            renderer.Align(Fit.Fill, Alignment.Center, artboard);
            renderer.Draw(artboard);
            renderer.Submit();
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        private Color32[] Read(RenderTexture texture)
        {
            if (m_readback == null || m_readback.width != texture.width || m_readback.height != texture.height)
            {
                if (m_readback != null)
                {
                    Object.Destroy(m_readback);
                }
                m_readback = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            }
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = texture;
            m_readback.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            m_readback.Apply();
            RenderTexture.active = previous;
            return m_readback.GetPixels32();
        }

        // The bar's center in pixels, or -1 when it isn't there. The bar and the
        // flag are in opposite halves, and which is which depends on the
        // backend's row order, so look at both.
        private double ReadBar(RenderTexture texture)
        {
            Color32[] pixels = Read(texture);
            int width = texture.width;
            int height = texture.height;
            for (int pass = 0; pass < 2; pass++)
            {
                int y = pass == 0 ? height / 4 : height - 1 - height / 4;
                int bright = 0;
                double sum = 0;
                for (int x = 0; x < width; x++)
                {
                    if (pixels[y * width + x].r > 128)
                    {
                        bright++;
                        sum += x;
                    }
                }
                if (bright > 0 && bright <= 12)
                {
                    return sum / bright;
                }
            }
            return -1;
        }

        private bool ReadFlag(RenderTexture texture)
        {
            Color32[] pixels = Read(texture);
            int width = texture.width;
            int height = texture.height;
            for (int pass = 0; pass < 2; pass++)
            {
                int y = pass == 0 ? height / 4 : height - 1 - height / 4;
                int bright = 0;
                for (int x = 0; x < width; x++)
                {
                    if (pixels[y * width + x].r > 128)
                    {
                        bright++;
                    }
                }
                if (bright > width / 2)
                {
                    return true;
                }
            }
            return false;
        }

        // The bar wraps around, so a move is taken the short way.
        private static double Wrap(double value, int width)
        {
            while (value < -width / 2.0)
            {
                value += width;
            }
            while (value > width / 2.0)
            {
                value -= width;
            }
            return value;
        }
    }
}
