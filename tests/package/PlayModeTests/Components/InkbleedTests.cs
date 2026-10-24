#if RIVE_USING_EXPERIMENTAL
using System.Collections;
using NUnit.Framework;
using Rive.Components;
using Rive.Host;
using Rive.Tests.Utils;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rive.Tests
{
    public class InkbleedTests
    {
        private TestAssetLoadingManager m_assets;
        private Asset m_fixture;
        private GameObject m_camera;
        private readonly System.Collections.Generic.List<RivePanel> m_panels = new System.Collections.Generic.List<RivePanel>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            m_assets = new TestAssetLoadingManager();
            yield return m_assets.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_inkbleedFixture,
                a => m_fixture = a,
                () => Assert.Fail("Failed to load the inkbleed fixture"));
            // Nothing renders without a camera.
            m_camera = new GameObject("InkbleedCamera", typeof(Camera));
        }

        [TearDown]
        public void TearDown()
        {
            HostNative.riveServerTimingEnable(false);
            foreach (RivePanel panel in m_panels)
            {
                if (panel != null)
                {
                    Object.Destroy(panel.gameObject);
                }
            }
            m_panels.Clear();
            Object.Destroy(m_camera);
            m_assets.UnloadAllAssets();
        }

        [Test]
        public void EffectiveAntialiasingMode_FollowsTheGraphicsApi()
        {
            RivePanel panel = AddPanel(ThreadingMode.MainThread, load: false);
            Assert.AreEqual(AntialiasingMode.Default, panel.EffectiveAntialiasingMode, "Panels should start on Default.");
            panel.AntialiasingMode = AntialiasingMode.Inkbleed;
            Assert.AreEqual(
                TextureHelper.SupportsInkbleed() ? AntialiasingMode.Inkbleed : AntialiasingMode.Default,
                panel.EffectiveAntialiasingMode);
        }

        // A panel that only draws when it changes still has to show the
        // change, and switching back gives back the same picture.
        [UnityTest]
        public IEnumerator Inkbleed_RedrawsAPanelThatDidNotChange([Values] ThreadingMode mode)
        {
            IgnoreIfUnsupported();
            RivePanel panel = AddPanel(mode);
            yield return WaitForLoaded(panel);
            yield return Frames(10);
            Color32[] before = Read(panel.RenderTexture);

            // The counters only count while timing is on.
            HostNative.riveServerTimingEnable(true);
            HostNative.riveCanvasPacingTake(new uint[7], 7);
            panel.AntialiasingMode = AntialiasingMode.Inkbleed;
            yield return Frames(10);
            Color32[] inkbleed = Read(panel.RenderTexture);
            var counts = new uint[7];
            HostNative.riveCanvasPacingTake(counts, (uint)counts.Length);
            HostNative.riveServerTimingEnable(false);
            string report = $"visible pixels before {Visible(before)}, with inkbleed {Visible(inkbleed)}; " +
                $"records after turning it on: made {counts[0]}, unchanged {counts[1]}, behind {counts[2]}; " +
                $"replays {counts[3]}, nothing new {counts[4]}";

            panel.AntialiasingMode = AntialiasingMode.Default;
            yield return Frames(10);
            Color32[] after = Read(panel.RenderTexture);

            Assert.Greater(Visible(before), 0, $"The panel should draw something. {report}");
            Assert.Greater(Differing(before, inkbleed), 0, $"Turning inkbleed on should redraw the panel. {report}");
            Assert.AreEqual(0, Differing(before, after), "Turning it off should give back the same picture.");
        }

        // Only the panel that asks for inkbleed draws with it.
        [UnityTest]
        public IEnumerator Inkbleed_OnlyChangesThePanelThatAsksForIt([Values] ThreadingMode mode)
        {
            IgnoreIfUnsupported();
            RivePanel inkbleed = AddPanel(mode);
            RivePanel normal = AddPanel(mode);
            yield return WaitForLoaded(inkbleed);
            yield return WaitForLoaded(normal);
            yield return Frames(10);
            Color32[] inkbleedBefore = Read(inkbleed.RenderTexture);
            Color32[] normalBefore = Read(normal.RenderTexture);

            inkbleed.AntialiasingMode = AntialiasingMode.Inkbleed;
            yield return Frames(10);

            Assert.Greater(Differing(inkbleedBefore, Read(inkbleed.RenderTexture)), 0,
                "The panel set to inkbleed should draw with it.");
            Assert.AreEqual(0, Differing(normalBefore, Read(normal.RenderTexture)),
                "The other panel should draw as before.");
        }

        private static void IgnoreIfUnsupported()
        {
            if (!TextureHelper.SupportsInkbleed())
            {
                Assert.Ignore($"{SystemInfo.graphicsDeviceType} doesn't support inkbleed.");
            }
        }

        private RivePanel AddPanel(ThreadingMode mode, bool load = true)
        {
            RivePanel panel = RivePanelTestUtils.CreatePanel($"InkbleedPanel{m_panels.Count}");
            m_panels.Add(panel);
            panel.ThreadingMode = mode;
            panel.DrawOptimization = DrawOptimizationOptions.DrawWhenChanged;
            panel.SetDimensions(new Vector2(512, 512));
            if (load)
            {
                RiveWidget widget = RivePanelTestUtils.CreateWidget<RiveWidget>();
                widget.transform.SetParent(panel.WidgetContainer, false);
                RivePanelTestUtils.MakeWidgetFillPanel(widget);
                // Frozen, so only inkbleed changes the picture.
                widget.Speed = 0f;
                widget.Load(m_fixture);
            }
            return panel;
        }

        private static IEnumerator WaitForLoaded(RivePanel panel)
        {
            yield return RivePanelTestUtils.WaitForLoaded(panel.GetComponentInChildren<RiveWidget>());
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        private static int Visible(Color32[] pixels)
        {
            int count = 0;
            foreach (Color32 pixel in pixels)
            {
                if (pixel.a != 0)
                {
                    count++;
                }
            }
            return count;
        }

        private static int Differing(Color32[] a, Color32[] b)
        {
            int count = 0;
            for (int i = 0; i < a.Length; i++)
            {
                if (!a[i].Equals(b[i]))
                {
                    count++;
                }
            }
            return count;
        }

        private static Color32[] Read(RenderTexture texture)
        {
            RenderTexture previous = RenderTexture.active;
            var readback = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = texture;
                readback.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                readback.Apply();
                return readback.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                Object.Destroy(readback);
            }
        }
    }
}
#endif
