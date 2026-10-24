using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Rive.Components;
using Rive.Host;
using Rive.Tests.Utils;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rive.Tests
{
    /// <summary>
    /// Panels that keep resizing and getting replaced while they draw every
    /// frame. Textures and Rive's buffers get freed and reused while the GPU
    /// may still be on earlier frames, so this should catch anything released
    /// too early.
    /// </summary>
    public class PanelChurnTests
    {
        private const int Panels = 6;
        private const int Frames = 600;
        private const int ResizeEvery = 5;
        private const int ReplaceEvery = 30;
        // The last replacement lands on the last frame, so one panel is new
        // when the check starts.
        private const int ShowWithin = 10;
        // Only to report when a late panel does show.
        private const int WatchFor = 300;
        private static readonly int[] s_sizes = { 256, 384, 512 };

        private TestAssetLoadingManager m_assets;
        private Asset m_hud;
        private readonly List<GameObject> m_objects = new List<GameObject>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            m_assets = new TestAssetLoadingManager();
            yield return m_assets.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_sophiaHud,
                a => m_hud = a,
                () => Assert.Fail("Failed to load the HUD asset"));
        }

        [TearDown]
        public void TearDown()
        {
            HostNative.riveServerTimingEnable(false);
            foreach (GameObject go in m_objects)
            {
                if (go != null)
                {
                    Object.Destroy(go);
                }
            }
            m_objects.Clear();
            m_assets.UnloadAllAssets();
        }

        [UnityTest]
        public IEnumerator ResizedAndReplacedPanels_KeepDrawing([Values] ThreadingMode mode)
        {
            // Nothing renders without a camera.
            m_objects.Add(new GameObject("PanelChurnCamera", typeof(Camera)));

            var panels = new RivePanel[Panels];
            var widgets = new RiveWidget[Panels];
            for (int i = 0; i < Panels; i++)
            {
                (panels[i], widgets[i]) = AddPanel(mode, i, s_sizes[i % s_sizes.Length]);
            }
            foreach (RiveWidget widget in widgets)
            {
                yield return RivePanelTestUtils.WaitForLoaded(widget);
            }

            for (int frame = 1; frame <= Frames; frame++)
            {
                if (frame % ReplaceEvery == 0)
                {
                    int slot = frame / ReplaceEvery % Panels;
                    Object.Destroy(panels[slot].gameObject);
                    (panels[slot], widgets[slot]) = AddPanel(mode, slot, s_sizes[frame % s_sizes.Length]);
                }
                else if (frame % ResizeEvery == 0)
                {
                    int slot = frame / ResizeEvery % Panels;
                    int size = s_sizes[frame / ResizeEvery % s_sizes.Length];
                    panels[slot].SetDimensions(new Vector2(size, size));
                }
                yield return null;
            }

            foreach (RiveWidget widget in widgets)
            {
                yield return RivePanelTestUtils.WaitForLoaded(widget);
            }

            // Keeps watching past ShowWithin, so a failure says whether a
            // panel was late or never showed.
            // The counters only count while timing is on.
            HostNative.riveServerTimingEnable(true);
            HostNative.riveCanvasPacingTake(new uint[7], 7);
            var shownAfter = new int[Panels];
            for (int i = 0; i < Panels; i++)
            {
                shownAfter[i] = -1;
            }
            for (int frame = 1; frame <= WatchFor; frame++)
            {
                yield return null;
                bool allShown = true;
                for (int i = 0; i < Panels; i++)
                {
                    if (shownAfter[i] < 0 && panels[i].RenderTexture != null &&
                        VisiblePixels(panels[i].RenderTexture) > 0)
                    {
                        shownAfter[i] = frame;
                    }
                    allShown &= shownAfter[i] >= 0;
                }
                if (allShown && frame >= ShowWithin)
                {
                    break;
                }
            }
            var counts = new uint[7];
            HostNative.riveCanvasPacingTake(counts, (uint)counts.Length);
            HostNative.riveServerTimingEnable(false);
            string report = $"frames each panel took to show: {string.Join(", ", shownAfter)} (-1 is never in {WatchFor}); " +
                $"records made {counts[0]}, behind {counts[2]}, superseded {counts[6]}; " +
                $"replays {counts[3]}, nothing new {counts[4]}, caught up {counts[5]}";

            for (int i = 0; i < Panels; i++)
            {
                Assert.IsNotNull(panels[i].RenderTexture, $"Panel {i} should have a texture.");
                Assert.IsTrue(shownAfter[i] >= 0 && shownAfter[i] <= ShowWithin,
                    $"Panel {i} should be drawing within {ShowWithin} frames. {report}");
            }
        }

        private (RivePanel, RiveWidget) AddPanel(ThreadingMode mode, int slot, int size)
        {
            RivePanel panel = RivePanelTestUtils.CreatePanel($"ChurnPanel{slot}");
            m_objects.Add(panel.gameObject);
            panel.ThreadingMode = mode;
            panel.DrawOptimization = DrawOptimizationOptions.AlwaysDraw;
            panel.SetDimensions(new Vector2(size, size));
            RiveWidget widget = RivePanelTestUtils.CreateWidget<RiveWidget>();
            widget.transform.SetParent(panel.WidgetContainer, false);
            RivePanelTestUtils.MakeWidgetFillPanel(widget);
            widget.Load(m_hud);
            return (panel, widget);
        }

        private static int VisiblePixels(RenderTexture texture)
        {
            RenderTexture previous = RenderTexture.active;
            var readback = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = texture;
                readback.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                readback.Apply();
                int count = 0;
                foreach (Color32 pixel in readback.GetPixels32())
                {
                    if (pixel.a != 0)
                    {
                        count++;
                    }
                }
                return count;
            }
            finally
            {
                RenderTexture.active = previous;
                Object.Destroy(readback);
            }
        }
    }
}
