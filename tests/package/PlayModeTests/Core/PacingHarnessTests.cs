using System.Collections;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Rive.Components;
using Rive.Tests.Utils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace Rive.Tests
{
    /// <summary>
    /// Measures what a viewer sees, read back from the panels every frame. A
    /// clock file's bar moves a pixel a frame, so stale and skipped frames show
    /// as a bar that didn't move or jumped. Video stand-ins encode the frame in
    /// pure channel values, so their lag and first image show in frames. A bool
    /// is pulsed for 1, 2 and 3 frames to see which pulses reach the screen.
    /// Run it by hand in a player build. It logs numbers and checks nothing.
    /// </summary>
    [Explicit("A measurement. Run it by hand in a player build.")]
    public class PacingHarnessTests
    {
        private const int WarmupFrames = 60;
        private const int MeasuredFrames = 600;
        private const int VideoSize = 64;
        private const int PulseEvery = 40;
        // Frames after a pulse that still count as it showing.
        private const int PulseWindow = 12;
        // Codes 1 to 7. 0 is black, which means no image yet.
        private const int CodePeriod = 7;

        private TestAssetLoadingManager m_assets;
        private Asset m_clock;
        private Asset m_video;
        private Asset m_canvas;
        private readonly List<GameObject> m_objects = new List<GameObject>();
        private readonly List<RenderTexture> m_textures = new List<RenderTexture>();
        private readonly List<RenderTextureImageSource> m_sources = new List<RenderTextureImageSource>();
        private readonly List<File> m_files = new List<File>();
        private int m_vSyncCount;
        private int m_targetFrameRate;
        private readonly List<TraceEvent> m_trace = new List<TraceEvent>();

        private struct TraceEvent
        {
            public int Frame;
            public ImagePipelineTrace.Step Step;
            public uint Handle;
            public object Value;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            if (!SystemInfo.supportsAsyncGPUReadback)
            {
                Assert.Ignore("Needs async GPU readback.");
            }
            // 60fps like the reported setup. A frame rate cap rather than vsync,
            // since the editor ignores vsync.
            m_vSyncCount = QualitySettings.vSyncCount;
            m_targetFrameRate = Application.targetFrameRate;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;

            m_assets = new TestAssetLoadingManager();
            yield return m_assets.LoadAssetCoroutine<Asset>(TestAssetReferences.riv_pacingClock,
                a => m_clock = a, () => Assert.Fail("Failed to load the clock asset"));
            yield return m_assets.LoadAssetCoroutine<Asset>(TestAssetReferences.riv_image_db_test,
                a => m_video = a, () => Assert.Fail("Failed to load the image asset"));
            yield return m_assets.LoadAssetCoroutine<Asset>(TestAssetReferences.riv_canvasContent,
                a => m_canvas = a, () => Assert.Fail("Failed to load the canvas asset"));
        }

        [TearDown]
        public void TearDown()
        {
            ImagePipelineTrace.ForTests = null;
            m_trace.Clear();
            RenderImageCommandQueue.riveRenderImageCountForTests(false);
            QualitySettings.vSyncCount = m_vSyncCount;
            Application.targetFrameRate = m_targetFrameRate;
            foreach (RenderTextureImageSource source in m_sources)
            {
                source.Dispose();
            }
            m_sources.Clear();
            foreach (GameObject go in m_objects)
            {
                if (go != null)
                {
                    Object.Destroy(go);
                }
            }
            m_objects.Clear();
            foreach (File file in m_files)
            {
                file.Dispose();
            }
            m_files.Clear();
            foreach (RenderTexture texture in m_textures)
            {
                texture.Release();
                Object.Destroy(texture);
            }
            m_textures.Clear();
            m_assets?.UnloadAllAssets();
        }

        [UnityTest]
        public IEnumerator Pacing([Values] ThreadingMode mode, [Values(0, 4)] int videos, [Values(0, 1, 2, 4)] int canvases)
        {
            m_objects.Add(new GameObject("PacingCamera", typeof(Camera)));

            RivePanel clockPanel = AddPanel("Clock", mode, new Vector2(256, 64), out RiveWidget clock);
            clock.Fit = Fit.Fill;
            clock.Load(m_clock);

            var videoPanels = new List<RivePanel>();
            var videoWidgets = new List<RiveWidget>();
            for (int i = 0; i < videos; i++)
            {
                RivePanel panel = AddPanel($"Video{i}", mode, new Vector2(128, 128), out RiveWidget widget);
                widget.Fit = Fit.Contain;
                File file = File.Load(m_video);
                m_files.Add(file);
                widget.Load(file);
                widget.BindingMode = RiveWidget.DataBindingMode.AutoBindDefault;
                videoPanels.Add(panel);
                videoWidgets.Add(widget);

                var texture = new RenderTexture(VideoSize, VideoSize, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                texture.Create();
                m_textures.Add(texture);
            }
            // Each one is its own panel with a script that draws into a canvas
            // every frame.
            var canvasWidgets = new List<RiveWidget>();
            for (int i = 0; i < canvases; i++)
            {
                AddPanel($"Canvas{i}", mode, new Vector2(128, 128), out RiveWidget canvasWidget);
                canvasWidget.Fit = Fit.Fill;
                canvasWidget.Load(m_canvas);
                canvasWidgets.Add(canvasWidget);
            }

            yield return RivePanelTestUtils.WaitForLoaded(clock);
            foreach (RiveWidget widget in videoWidgets)
            {
                yield return RivePanelTestUtils.WaitForLoaded(widget);
            }
            foreach (RiveWidget widget in canvasWidgets)
            {
                yield return RivePanelTestUtils.WaitForLoaded(widget);
            }
            for (int i = 0; i < WarmupFrames; i++)
            {
                FillVideos(Time.frameCount);
                yield return null;
            }

            // Every step of the image path from a few frames before the bind,
            // so the timelines show what was already in flight.
            m_trace.Clear();
            // Stops 40 frames after the bind so it doesn't add work to the
            // frames being measured.
            int traceUntil = int.MaxValue;
            ImagePipelineTrace.ForTests = (step, handle, value) =>
            {
                if (Time.frameCount <= traceUntil)
                {
                    m_trace.Add(new TraceEvent { Frame = Time.frameCount, Step = step, Handle = handle, Value = value });
                }
            };
            for (int i = 0; i < 3; i++)
            {
                FillVideos(Time.frameCount);
                yield return null;
            }

            // What the file shows before anything is bound, so the first image
            // can be told apart from it.
            var baseline = new int[videos];
            for (int i = 0; i < videos; i++)
            {
                int index = i;
                AsyncGPUReadback.Request(videoPanels[i].RenderTexture, 0, TextureFormat.RGBA32,
                    r => baseline[index] = ReadCode(r));
            }
            AsyncGPUReadback.WaitAllRequests();

            // Bound with content already in the texture, like a video that's
            // playing when its image source is made.
            int bindFrame = Time.frameCount;
            traceUntil = bindFrame + 40;
            RenderImageCommandQueue.riveRenderImageCountForTests(videos > 0);
            string imageCounts = null;
            for (int i = 0; i < videos; i++)
            {
                var source = new RenderTextureImageSource(
                    m_textures[i],
                    RenderTextureImageSource.TextureProcessingMode.None,
                    RenderTextureImageSource.RefreshMode.PerFrame);
                m_sources.Add(source);
                BindImage(videoWidgets[i], mode, source);
            }

            SMIBool flag = clock.LoadedStateMachine.GetBool("flag");
            var samples = new Sample[MeasuredFrames];
            var pulses = new List<(int frame, int length)>();
            int pulseEnd = -1;
            int firstFrame = Time.frameCount;
            var frameTimes = new FrameTiming[1];

            for (int f = 0; f < MeasuredFrames; f++)
            {
                int frame = Time.frameCount;
                FillVideos(frame);

                if (f > 0 && f % PulseEvery == 0)
                {
                    int length = 1 + (f / PulseEvery) % 3;
                    flag.Value = true;
                    pulseEnd = f + length;
                    pulses.Add((f, length));
                }
                else if (f == pulseEnd)
                {
                    flag.Value = false;
                }

                yield return new WaitForEndOfFrame();

                var sample = new Sample { Frame = frame, DeltaMs = Time.unscaledDeltaTime * 1000.0, Video = new int[videos] };
                samples[f] = sample;
                Sample captured = sample;
                AsyncGPUReadback.Request(clockPanel.RenderTexture, 0, TextureFormat.RGBA32,
                    r => ReadClock(r, captured));
                for (int i = 0; i < videos; i++)
                {
                    int index = i;
                    AsyncGPUReadback.Request(videoPanels[i].RenderTexture, 0, TextureFormat.RGBA32,
                        r => captured.Video[index] = ReadCode(r));
                }

                if (videos > 0 && frame == traceUntil)
                {
                    RenderImageCommandQueue.riveRenderImageTakeCounts(
                        out uint bornUnfilled, out uint bornFilled, out uint wrapsCached, out uint wrapsNotCached);
                    RenderImageCommandQueue.riveRenderImageCountForTests(false);
                    imageCounts = $"native images in the first 40 frames: made unfilled {bornUnfilled}, made filled {bornFilled}, " +
                        $"wraps cached {wrapsCached}, wraps not cached because a newer build landed first {wrapsNotCached}\n";
                }

                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, frameTimes) > 0)
                {
                    sample.RenderThreadMs = frameTimes[0].cpuRenderThreadFrameTime;
                }
                yield return null;
            }
            AsyncGPUReadback.WaitAllRequests();
            flag.Value = false;
            ImagePipelineTrace.ForTests = null;

            Debug.Log(Report(mode, videos, canvases, samples, pulses, bindFrame - firstFrame, baseline)
                + Timelines(bindFrame, samples, baseline, videoWidgets, videoPanels) + imageCounts);
        }

        private RivePanel AddPanel(string name, ThreadingMode mode, Vector2 size, out RiveWidget widget)
        {
            RivePanel panel = RivePanelTestUtils.CreatePanel($"Pacing{name}");
            m_objects.Add(panel.gameObject);
            panel.ThreadingMode = mode;
            panel.SetDimensions(size);
            widget = RivePanelTestUtils.CreateWidget<RiveWidget>();
            widget.transform.SetParent(panel.WidgetContainer, false);
            RivePanelTestUtils.MakeWidgetFillPanel(widget);
            return panel;
        }

        private static void BindImage(RiveWidget widget, ThreadingMode mode, RenderTextureImageSource source)
        {
            if (mode == ThreadingMode.BackgroundThread)
            {
                widget.StateMachineHandle.GetViewModelInstance().GetImageProperty("image")
                    .SetFromRenderTextureImageSource(source);
            }
            else
            {
                widget.StateMachine.ViewModelInstance.GetProperty<ViewModelInstanceImageProperty>("image")
                    .SetFromRenderTextureImageSource(source);
            }
        }

        // Pure 0 or 255 channels survive any color space conversion.
        private static int CodeFor(int frame)
        {
            return 1 + frame % CodePeriod;
        }

        private void FillVideos(int frame)
        {
            int code = CodeFor(frame);
            var color = new UnityEngine.Color((code & 1) != 0 ? 1f : 0f, (code & 2) != 0 ? 1f : 0f, (code & 4) != 0 ? 1f : 0f, 1f);
            RenderTexture previous = RenderTexture.active;
            foreach (RenderTexture texture in m_textures)
            {
                RenderTexture.active = texture;
                GL.Clear(false, true, color);
            }
            RenderTexture.active = previous;
        }

        private sealed class Sample
        {
            public int Frame;
            public double DeltaMs;
            public double RenderThreadMs;
            public bool Read;
            // Bar center in pixels, or -1 when it wasn't found.
            public double Bar = -1;
            public bool Flag;
            public int[] Video;
        }

        private static void ReadClock(AsyncGPUReadbackRequest request, Sample sample)
        {
            if (request.hasError)
            {
                return;
            }
            var data = request.GetData<Color32>();
            int width = request.width;
            int height = request.height;
            // The bar is in one half and the flag in the other. Which is which
            // depends on the backend's row order, so look at both.
            for (int pass = 0; pass < 2; pass++)
            {
                int y = pass == 0 ? height / 4 : height - 1 - height / 4;
                int bright = 0;
                double sum = 0;
                for (int x = 0; x < width; x++)
                {
                    if (data[y * width + x].r > 128)
                    {
                        bright++;
                        sum += x;
                    }
                }
                if (bright > 0 && bright <= 12)
                {
                    sample.Bar = sum / bright;
                }
                else if (bright > width / 2)
                {
                    sample.Flag = true;
                }
            }
            sample.Read = true;
        }

        private static int ReadCode(AsyncGPUReadbackRequest request)
        {
            if (request.hasError)
            {
                return -1;
            }
            var data = request.GetData<Color32>();
            Color32 c = data[(request.height / 2) * request.width + request.width / 2];
            return (c.r > 128 ? 1 : 0) | (c.g > 128 ? 2 : 0) | (c.b > 128 ? 4 : 0);
        }

        private static string Report(ThreadingMode mode, int videos, int canvases, Sample[] samples,
            List<(int frame, int length)> pulses, int bindOffset, int[] baseline)
        {
            var report = new StringBuilder();
            report.AppendLine($"Rive pacing. {mode}, videos {videos}, canvases {canvases}, {samples.Length} frames.");
            report.AppendLine($"Unity {Application.unityVersion}, {SystemInfo.graphicsDeviceType}, {SystemInfo.graphicsDeviceName}, " +
                $"threading {SystemInfo.renderingThreadingMode}, {(Application.isEditor ? "editor" : "player")}, " +
                $"target {Application.targetFrameRate} fps.");

            // Pacing. The bar moves 60 px a second. A frame only counts as stale
            // or skipped when it should have moved the bar at least half a pixel.
            int stale = 0, skipped = 0, judged = 0, read = 0;
            double freezeMs = 0, longestFreezeMs = 0;
            var lag = new List<double>();
            double clock = 0;
            Sample previous = null;
            foreach (Sample s in samples)
            {
                clock += s.DeltaMs * 0.06;
                if (!s.Read || s.Bar < 0)
                {
                    continue;
                }
                read++;
                lag.Add(Wrap(clock - s.Bar) / 0.06);
                if (previous != null)
                {
                    double moved = Wrap(s.Bar - previous.Bar);
                    double expected = s.DeltaMs * 0.06;
                    if (moved < 0.25)
                    {
                        freezeMs += s.DeltaMs;
                        longestFreezeMs = System.Math.Max(longestFreezeMs, freezeMs);
                    }
                    else
                    {
                        freezeMs = 0;
                    }
                    if (expected >= 0.5)
                    {
                        judged++;
                        if (moved < 0.25)
                        {
                            stale++;
                        }
                        else if (moved > expected + 1.0)
                        {
                            skipped++;
                        }
                    }
                }
                previous = s;
            }
            lag.Sort();
            report.AppendLine($"clock: read {read}, stale {Percent(stale, judged)}, skipped {Percent(skipped, judged)}, " +
                $"longest freeze {longestFreezeMs:F0} ms, lag spread p5-p95 {Spread(lag):F1} ms");

            // Frame time and render thread.
            var frameMs = new List<double>();
            var renderMs = new List<double>();
            foreach (Sample s in samples)
            {
                frameMs.Add(s.DeltaMs);
                if (s.RenderThreadMs > 0)
                {
                    renderMs.Add(s.RenderThreadMs);
                }
            }
            frameMs.Sort();
            renderMs.Sort();
            report.AppendLine($"frame ms: median {Pct(frameMs, 0.5):F2}, p95 {Pct(frameMs, 0.95):F2}, max {Pct(frameMs, 1):F2}; " +
                $"render thread ms: {(renderMs.Count > 0 ? $"median {Pct(renderMs, 0.5):F2}, p95 {Pct(renderMs, 0.95):F2}" : "n/a (needs Frame Timing Stats)")}");

            // Videos. First image counts from the bind, lag is mod 7 frames.
            for (int v = 0; samples.Length > 0 && v < samples[0].Video.Length; v++)
            {
                int first = -1;
                var lags = new int[CodePeriod];
                int blank = 0, counted = 0;
                for (int i = 0; i < samples.Length; i++)
                {
                    int code = samples[i].Video[v];
                    int behindHere = ((CodeFor(samples[i].Frame) - code) % CodePeriod + CodePeriod) % CodePeriod;
                    // Before the first image, the file's own image or a code
                    // that doesn't fit the pattern doesn't count.
                    if (first < 0 && (code == baseline[v] || behindHere > 3))
                    {
                        code = 0;
                    }
                    if (code <= 0)
                    {
                        if (first >= 0)
                        {
                            blank++;
                        }
                        continue;
                    }
                    if (first < 0)
                    {
                        first = i - bindOffset;
                    }
                    int behind = ((CodeFor(samples[i].Frame) - code) % CodePeriod + CodePeriod) % CodePeriod;
                    lags[behind]++;
                    counted++;
                }
                var line = new StringBuilder($"video {v}: first image {(first >= 0 ? $"{first} frames after bind" : "never")}, blank after {blank}, lag (mod 7):");
                for (int k = 0; k < CodePeriod; k++)
                {
                    if (lags[k] > 0)
                    {
                        line.Append($" {k}f {Percent(lags[k], counted)}");
                    }
                }
                report.AppendLine(line.ToString());
            }

            // Bool pulses.
            var seen = new int[4];
            var sent = new int[4];
            foreach ((int frame, int length) in pulses)
            {
                sent[length]++;
                for (int i = frame; i < Mathf.Min(samples.Length, frame + length + PulseWindow); i++)
                {
                    if (samples[i] != null && samples[i].Flag)
                    {
                        seen[length]++;
                        break;
                    }
                }
            }
            report.AppendLine($"bool pulses seen: 1 frame {seen[1]}/{sent[1]}, 2 frames {seen[2]}/{sent[2]}, 3 frames {seen[3]}/{sent[3]}");
            return report.ToString();
        }

        // One line per video, from a few frames before the bind to the first
        // frame its image showed, with each step stamped by frames after the
        // bind. Steps without an image handle (flush blocked, fill issued) are
        // shared by every video.
        private string Timelines(int bindFrame, Sample[] samples, int[] baseline,
            List<RiveWidget> widgets, List<RivePanel> panels)
        {
            var text = new StringBuilder();
            for (int v = 0; v < widgets.Count; v++)
            {
                uint handle = m_sources[v].Handle;
                int firstPixel = -1;
                foreach (Sample s in samples)
                {
                    int code = s.Video[v];
                    int behind = ((CodeFor(s.Frame) - code) % CodePeriod + CodePeriod) % CodePeriod;
                    if (s.Frame >= bindFrame && code > 0 && code != baseline[v] && behind <= 3)
                    {
                        firstPixel = s.Frame;
                        break;
                    }
                }
                int end = firstPixel >= 0 ? firstPixel : bindFrame + 30;
                var line = new StringBuilder($"video {v} timeline (frames after bind):");
                foreach (TraceEvent e in m_trace)
                {
                    if (e.Frame > end)
                    {
                        break;
                    }
                    string label = Label(e, handle, widgets[v], panels[v]);
                    if (label != null)
                    {
                        line.Append($" {e.Frame - bindFrame:+0;-0}:{label}");
                    }
                }
                line.Append(firstPixel >= 0 ? $" {firstPixel - bindFrame:+0;-0}:PIXEL" : " no pixel within 30 frames");
                text.AppendLine(line.ToString());
            }
            return text.ToString();
        }

        private static string Label(TraceEvent e, uint handle, RiveWidget widget, RivePanel panel)
        {
            switch (e.Step)
            {
                case ImagePipelineTrace.Step.BuildQueued:
                    return e.Handle == handle ? ((e.Value is bool first && first) ? "queued(discrete)" : "queued") : null;
                case ImagePipelineTrace.Step.SourceNotReady:
                    return e.Handle == handle ? "notready" : null;
                case ImagePipelineTrace.Step.FlushBlocked:
                    return $"blocked({e.Value})";
                case ImagePipelineTrace.Step.BatchSent:
                    return e.Handle == handle ? "sent" : null;
                case ImagePipelineTrace.Step.BatchLanded:
                    return e.Handle == handle ? "landed" : null;
                case ImagePipelineTrace.Step.FillIssued:
                    return "fill";
                case ImagePipelineTrace.Step.AdvanceSent:
                    return ReferenceEquals(e.Value, widget) ? "adv" : null;
                case ImagePipelineTrace.Step.AdvanceLanded:
                    return ReferenceEquals(e.Value, widget) ? "advLanded" : null;
                case ImagePipelineTrace.Step.PanelDrawn:
                    return ReferenceEquals(e.Value, panel) ? "drawn" : null;
                case ImagePipelineTrace.Step.RedrawRequested:
                    return ReferenceEquals(e.Value, panel) ? "redraw" : null;
                default:
                    return null;
            }
        }

        // Into -128..128 on the 256 px loop.
        private static double Wrap(double value)
        {
            value %= 256.0;
            if (value > 128.0) value -= 256.0;
            if (value < -128.0) value += 256.0;
            return value;
        }

        private static string Percent(int count, int total)
        {
            return total > 0 ? $"{100.0 * count / total:F1}%" : "n/a";
        }

        private static double Pct(List<double> sorted, double p)
        {
            if (sorted.Count == 0)
            {
                return 0;
            }
            int index = Mathf.Clamp((int)(p * (sorted.Count - 1)), 0, sorted.Count - 1);
            return sorted[index];
        }

        private static double Spread(List<double> sorted)
        {
            return Pct(sorted, 0.95) - Pct(sorted, 0.05);
        }
    }
}
