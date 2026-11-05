#if UNITY_EDITOR
using System.Collections;
using IO = System.IO;
using NUnit.Framework;
using Rive.Components;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rive.Tests
{
    public class CanvasResolutionVisualTests
    {
        [UnityTest]
        public IEnumerator RendererEnabledBeforePanel_CapturesCanvasResolution()
        {
            var camera = new GameObject("Repro Camera").AddComponent<Camera>();
            var canvas = new GameObject("Repro Canvas").AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.scaleFactor = 2.5625f;
            Texture2D image = null;
            RenderTexture display = null;
            var previousTarget = RenderTexture.active;
            try
            {
                var panelObject = new GameObject("Panel", typeof(RectTransform));
                panelObject.SetActive(false);
                panelObject.transform.SetParent(canvas.transform, false);
                var panel = panelObject.AddComponent<RivePanel>();
                panel.enabled = false;
                panel.SetDimensions(new Vector2(240, 240));
                var renderer = panelObject.AddComponent<RiveCanvasRenderer>();
                renderer.PointerInputMode = PointerInputMode.DisablePointerInput;
                renderer.MatchCanvasResolution = true;
                // Enable the renderer before the panel initializes its strategy.
                panelObject.SetActive(true);
                panel.enabled = true;
                var rect = new GameObject("Sample", typeof(RectTransform)).GetComponent<RectTransform>();
                rect.SetParent(panelObject.transform, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.sizeDelta = Vector2.zero;
                var widget = rect.gameObject.AddComponent<RiveWidget>();
                widget.Speed = 0;
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<Asset>("Assets/cute_robot.riv");
                Assert.IsNotNull(asset, "Run this reproduction from examples/basic.");
                widget.Load(asset);
                Assert.AreEqual(WidgetStatus.Loaded, widget.Status);
                Canvas.ForceUpdateCanvases();
                var strategy = (SimpleRenderTargetStrategy)panel.RenderTargetStrategy;
                strategy.DrawTiming = DrawTimingOption.DrawImmediate;
                yield return null;
                strategy.DrawPanel(panel);
                yield return new WaitForEndOfFrame();
                var source = panel.RenderTexture;
                Assert.IsNotNull(source);
                display = RenderTexture.GetTemporary(615, 615, 0, RenderTextureFormat.ARGB32);
                source.filterMode = FilterMode.Bilinear;
                // Match the displayed size, including the normal bilinear upscale.
                Graphics.Blit(source, display);
                RenderTexture.active = display;
                image = new Texture2D(615, 615, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, 615, 615), 0, 0);
                image.Apply();
                int visiblePixels = 0;
                foreach (var pixel in image.GetPixels32())
                    if (pixel.a > 128) visiblePixels++;
                Assert.Greater(visiblePixels, 10000, "Capture must contain the sample artwork.");
                var output = IO.Path.Combine(IO.Path.GetTempPath(), "rive-canvas-resolution");
                IO.Directory.CreateDirectory(output);
                var label = source.width == 615 ? "after" : "before";
                IO.File.WriteAllBytes(IO.Path.Combine(output, label + ".png"), image.EncodeToPNG());
                Debug.Log($"Render target: {source.width}x{source.height}. Capture: {output}");
                Assert.AreEqual(615, source.width);
                Assert.AreEqual(615, source.height);
            }
            finally
            {
                RenderTexture.active = previousTarget;
                if (display != null) RenderTexture.ReleaseTemporary(display);
                if (image != null) Object.DestroyImmediate(image);
                Object.DestroyImmediate(canvas.gameObject);
                Object.DestroyImmediate(camera.gameObject);
            }
        }
    }
}
#endif
