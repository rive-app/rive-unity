#if RIVE_USING_EXPERIMENTAL
using System.Collections;
using NUnit.Framework;
using Rive.Components;
using Rive.Tests.Utils;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rive.Tests
{
    public class LayerMaskTests
    {
        private const int Size = 400;
        // The fixture's background, which the mask leaves showing outside it.
        private static readonly Color32 Background = new Color32(32, 32, 32, 255);

        private TestAssetLoadingManager m_assets;
        private Asset m_fixture;
        private GameObject m_camera;
        private RivePanel m_panel;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            m_assets = new TestAssetLoadingManager();
            yield return m_assets.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_layerMask,
                a => m_fixture = a,
                () => Assert.Fail("Failed to load the layer mask fixture"));
            // Nothing renders without a camera.
            m_camera = new GameObject("LayerMaskCamera", typeof(Camera));
        }

        [TearDown]
        public void TearDown()
        {
            if (m_panel != null)
            {
                Object.Destroy(m_panel.gameObject);
            }
            Object.Destroy(m_camera);
            m_assets.UnloadAllAssets();
        }

        // A mask is the file's only canvas feature here, so its frames carry no
        // ore commands. The composite still needs ore, or the layer draws empty.
        [UnityTest]
        public IEnumerator LayerMask_ShowsTheMaskedContent([Values] ThreadingMode mode)
        {
            m_panel = RivePanelTestUtils.CreatePanel("LayerMaskPanel");
            m_panel.ThreadingMode = mode;
            m_panel.SetDimensions(new Vector2(Size, Size));
            RiveWidget widget = RivePanelTestUtils.CreateWidget<RiveWidget>();
            widget.transform.SetParent(m_panel.WidgetContainer, false);
            RivePanelTestUtils.MakeWidgetFillPanel(widget);
            widget.Load(m_fixture);
            yield return RivePanelTestUtils.WaitForLoaded(widget);
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            // Along the middle row, so the backend's vertical flip doesn't matter.
            Color32[] row = ReadMiddleRow(m_panel.RenderTexture);
            Color32 inner = row[150];
            Color32 faded = row[80];
            Color32 outside = row[10];

            Assert.Greater(inner.r, 120, $"The middle of the mask should show the red half, got {inner}.");
            Assert.Greater(inner.r, faded.r, $"The mask should fade towards its edge, got {inner} then {faded}.");
            Assert.Greater(faded.r, Background.r, $"Partway out the mask should still show some red, got {faded}.");
            Assert.AreEqual(Background, outside, "Outside the mask only the background should show.");
        }

        private static Color32[] ReadMiddleRow(RenderTexture texture)
        {
            RenderTexture previous = RenderTexture.active;
            var readback = new Texture2D(texture.width, 1, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = texture;
                readback.ReadPixels(new Rect(0, texture.height / 2, texture.width, 1), 0, 0);
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
