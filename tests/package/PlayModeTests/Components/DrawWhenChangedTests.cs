using System.Collections;
using NUnit.Framework;
using Rive.Components;
using Rive.Tests.Utils;
using Rive.Utils;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Rive.Tests
{
    /// <summary>
    /// Paints a marker over the panel's texture and checks whether Rive draws
    /// over it. Every replay into a texture clears it first, so the marker
    /// only survives if nothing was drawn.
    /// </summary>
    public class DrawWhenChangedTests
    {
        // Not used by image_db_test.
        private static readonly UnityEngine.Color MarkerColor = new UnityEngine.Color(1f, 0f, 1f, 1f);

        private MockLogger m_mockLogger;
        private TestAssetLoadingManager m_testAssetLoadingManager;
        private File m_file;
        private RivePanel m_panel;
        private RiveWidget m_widget;
        private GameObject m_cameraObject;
        private Texture2D m_readback;

        [SetUp]
        public void SetUp()
        {
            m_mockLogger = new MockLogger();
            DebugLogger.Instance = m_mockLogger;
            m_testAssetLoadingManager = new TestAssetLoadingManager();

            // Nothing draws without a camera rendering.
            m_cameraObject = new GameObject("Camera");
            m_cameraObject.AddComponent<Camera>().tag = "MainCamera";

            m_panel = RivePanelTestUtils.CreatePanel("DrawWhenChangedPanel");
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
            if (m_readback != null)
            {
                Object.Destroy(m_readback);
            }
            m_file?.Dispose();
            m_file = null;
            m_testAssetLoadingManager.UnloadAllAssets();
        }

        // image_db_test has nothing animating once it has settled.
        private IEnumerator LoadStaticFile()
        {
            Asset asset = null;
            yield return m_testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_image_db_test,
                loaded => asset = loaded,
                () => Assert.Fail($"Failed to load {TestAssetReferences.riv_image_db_test}"));

            m_file = File.Load(asset);
            m_widget.Load(m_file);
            yield return RivePanelTestUtils.WaitForLoaded(m_widget);
        }

        private static IEnumerator RenderedFrames(int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                yield return null;
                yield return new WaitForEndOfFrame();
            }
        }

        private void PaintMarker()
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = m_panel.RenderTexture;
            GL.Clear(false, true, MarkerColor);
            RenderTexture.active = previous;
        }

        private float MarkerCoverage()
        {
            RenderTexture texture = m_panel.RenderTexture;
            if (m_readback == null)
            {
                m_readback = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            }
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = texture;
            m_readback.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            m_readback.Apply();
            RenderTexture.active = previous;

            Color32[] pixels = m_readback.GetPixels32();
            int marked = 0;
            foreach (Color32 pixel in pixels)
            {
                if (pixel.r > 240 && pixel.g < 15 && pixel.b > 240 && pixel.a > 240)
                {
                    marked++;
                }
            }
            return (float)marked / pixels.Length;
        }

        private IEnumerator SettleAndMark(ThreadingMode mode, DrawOptimizationOptions optimization)
        {
            m_panel.ThreadingMode = mode;
            m_panel.DrawOptimization = optimization;
            yield return LoadStaticFile();
            yield return RenderedFrames(10);

            Assert.IsNotNull(m_panel.RenderTexture, "The panel never rendered.");
            Assert.Less(MarkerCoverage(), 0.01f, "The panel should have drawn over the texture.");
            PaintMarker();
        }

        [UnityTest]
        public IEnumerator StaticArtboard_LeavesTheTextureAlone(
            [Values(ThreadingMode.MainThread, ThreadingMode.BackgroundThread)] ThreadingMode mode)
        {
            yield return SettleAndMark(mode, DrawOptimizationOptions.DrawWhenChanged);

            yield return RenderedFrames(20);

            Assert.AreEqual(1f, MarkerCoverage());
        }

        [UnityTest]
        public IEnumerator StaticArtboard_WithAlwaysDraw_RedrawsTheTexture(
            [Values(ThreadingMode.MainThread, ThreadingMode.BackgroundThread)] ThreadingMode mode)
        {
            yield return SettleAndMark(mode, DrawOptimizationOptions.AlwaysDraw);

            yield return RenderedFrames(3);

            Assert.Less(MarkerCoverage(), 0.01f);
        }

        [UnityTest]
        public IEnumerator FitChange_RedrawsTheTexture(
            [Values(ThreadingMode.MainThread, ThreadingMode.BackgroundThread)] ThreadingMode mode)
        {
            yield return SettleAndMark(mode, DrawOptimizationOptions.DrawWhenChanged);
            yield return RenderedFrames(3);
            Assert.AreEqual(1f, MarkerCoverage(), "Nothing should draw before the change.");

            m_widget.Fit = m_widget.Fit == Fit.Fill ? Fit.Contain : Fit.Fill;

            for (int i = 0; i < 10 && MarkerCoverage() > 0.01f; i++)
            {
                yield return RenderedFrames(1);
            }

            Assert.Less(MarkerCoverage(), 0.01f);
        }
    }
}
