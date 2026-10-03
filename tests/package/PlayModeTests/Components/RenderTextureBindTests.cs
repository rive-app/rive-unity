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
    /// Binding a texture Rive drew into as Unity's render target. On WebGL that
    /// can wipe what Rive drew unless the texture was set up as a target first,
    /// which TextureHelper.PrepareForNativeDrawing does for textures Rive creates.
    /// </summary>
    public class RenderTextureBindTests
    {
        private const float DrawTimeoutSeconds = 10f;

        // Logged by Unity's GL plugin interface on WebGL. Tracked separately.
        private const string KnownWebGLPluginError =
            "OPENGL NATIVE PLUG-IN ERROR: GL_INVALID_ENUM: enum argument out of range";

        private TestAssetLoadingManager m_assets;
        private Camera m_camera;
        private readonly List<string> m_unexpectedErrors = new List<string>();

        [SetUp]
        public void SetUp()
        {
            if (!NativeUsageGuard.IsNativeAvailable)
            {
                Assert.Ignore("Needs the native plugin.");
            }
            m_assets = new TestAssetLoadingManager();
            m_camera = new GameObject("Camera").AddComponent<Camera>();
            m_camera.tag = "MainCamera";
            m_unexpectedErrors.Clear();
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                Application.logMessageReceived += CollectErrors;
            }
        }

        [TearDown]
        public void TearDown()
        {
            TextureHelper.PrepareDisabledForTests = false;
            Application.logMessageReceived -= CollectErrors;
            m_assets?.UnloadAllAssets();
            if (m_camera != null)
            {
                Object.Destroy(m_camera.gameObject);
            }
            Assert.IsEmpty(m_unexpectedErrors, "Unexpected errors were logged.");
        }

        /// Lets the known plugin error through on WebGL; anything else still
        /// fails the test in TearDown. Has to run in the test body, since Unity's
        /// log check covers only the test method and makes a new scope for it.
        private static void AllowKnownWebGLPluginError()
        {
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                LogAssert.ignoreFailingMessages = true;
            }
        }

        private void CollectErrors(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Log || type == LogType.Warning)
            {
                return;
            }
            if (message.Contains(KnownWebGLPluginError))
            {
                return;
            }
            m_unexpectedErrors.Add($"{type}: {message}");
        }

        /// Runs with the preparation on and off. Off shows whether the platform
        /// wipes a panel's texture on the first bind; on must never lose content.
        [UnityTest]
        public IEnumerator PanelTexture_KeepsRiveContent_WhenBoundAsRenderTarget(
            [Values(true, false)] bool prepare)
        {
            AllowKnownWebGLPluginError();
            TextureHelper.PrepareDisabledForTests = !prepare;

            RivePanel panel = null;
            yield return m_assets.LoadAssetCoroutine<GameObject>(
                TestPrefabReferences.RivePanelWithSingleWidget,
                prefab =>
                {
                    panel = Object.Instantiate(prefab).GetComponent<RivePanel>();
                    // A redraw would put the content back and hide the wipe.
                    panel.DrawOptimization = DrawOptimizationOptions.DrawWhenChanged;
                    panel.SetDimensions(new Vector2(256, 256));
                },
                () => Assert.Fail("Failed to load the panel prefab."));

            Asset riveAsset = null;
            yield return m_assets.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_asset_databinding_test,
                asset => riveAsset = asset,
                () => Assert.Fail("Failed to load the Rive asset."));

            File file = File.Load(riveAsset);
            try
            {
                var widget = panel.GetComponentInChildren<RiveWidget>();
                widget.Load(file);
                widget.BindingMode = RiveWidget.DataBindingMode.AutoBindDefault;
                yield return RivePanelTestUtils.WaitForLoaded(widget);

                yield return WaitForSampledContent(panel.RenderTexture, "The panel never drew.");

                bool contentWhenBound = HasContent(ReadByBinding(panel.RenderTexture));
                yield return Frames(2);
                bool contentAfter = HasContent(ReadBySampling(panel.RenderTexture));

                Debug.Log($"Panel texture on {SystemInfo.graphicsDeviceType}, prepare = {prepare}: " +
                          $"content when bound = {contentWhenBound}, after = {contentAfter}.");

                if (prepare)
                {
                    Assert.IsTrue(contentWhenBound,
                        "Reading the panel's texture as the render target saw none of Rive's content.");
                    Assert.IsTrue(contentAfter,
                        "The panel's texture lost Rive's content after being bound as the render target.");
                }
            }
            finally
            {
                Object.Destroy(panel.gameObject);
                file.Dispose();
            }
        }

        /// A texture from the same allocator panels use, drawn with a bare
        /// RenderQueue and never prepared. Logs what binding does to it.
        [UnityTest]
        public IEnumerator HandlerTexture_ReportsWhetherBindingKeepsRiveContent()
        {
            AllowKnownWebGLPluginError();
            RivePanel panel = null;
            yield return m_assets.LoadAssetCoroutine<GameObject>(
                TestPrefabReferences.RivePanelWithSingleWidget,
                prefab => panel = Object.Instantiate(prefab).GetComponent<RivePanel>(),
                () => Assert.Fail("Failed to load the panel prefab."));
            // The panel only lends its pipeline handler.
            yield return null;
            var strategy = panel.RenderTargetStrategy as RenderTargetStrategy;
            Assert.IsNotNull(strategy, "The panel has no built-in render target strategy.");
            IRenderPipelineHandler handler = strategy.RenderPipelineHandler;
            Assert.IsNotNull(handler, "The panel has no render pipeline handler.");

            Asset riveAsset = null;
            yield return m_assets.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_asset_databinding_test,
                asset => riveAsset = asset,
                () => Assert.Fail("Failed to load the Rive asset."));

            File file = File.Load(riveAsset);
            RenderTexture texture = handler.AllocateRenderTexture(256, 256);
            Assert.IsNotNull(texture, "The handler didn't allocate a texture.");
            if (!texture.IsCreated())
            {
                texture.Create();
            }
            var queue = new RenderQueue(texture);
            try
            {
                Artboard artboard = file.Artboard(0);
                // An advance lays the artboard out, or it draws nothing.
                artboard.StateMachine()?.Advance(0f);
                Renderer renderer = queue.Renderer();

                // Draw every frame until it shows, then stop, so nothing redraws
                // after the bind.
                float deadline = Time.realtimeSinceStartup + DrawTimeoutSeconds;
                do
                {
                    Assert.Less(Time.realtimeSinceStartup, deadline,
                        "Rive never drew into the handler's texture.");
                    renderer.Clear();
                    renderer.Align(Fit.Contain, Alignment.Center, artboard);
                    renderer.Draw(artboard);
                    renderer.Submit();
                    yield return null;
                    yield return new WaitForEndOfFrame();
                }
                while (!HasContent(ReadBySampling(texture)));

                bool contentWhenBound = HasContent(ReadByBinding(texture));
                yield return Frames(2);
                bool contentAfter = HasContent(ReadBySampling(texture));

                Debug.Log($"Handler texture ({texture.GetType().Name}) on {SystemInfo.graphicsDeviceType}: " +
                          $"content when bound = {contentWhenBound}, after = {contentAfter}.");
            }
            finally
            {
                queue.Dispose();
                handler.ReleaseRenderTexture(texture);
                Object.Destroy(panel.gameObject);
                file.Dispose();
            }
        }

        private static IEnumerator WaitForSampledContent(RenderTexture texture, string failure)
        {
            float deadline = Time.realtimeSinceStartup + DrawTimeoutSeconds;
            while (!HasContent(ReadBySampling(texture)))
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, failure);
                yield return null;
                yield return new WaitForEndOfFrame();
            }
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
                yield return new WaitForEndOfFrame();
            }
        }

        private static bool HasContent(Color32[] pixels)
        {
            foreach (Color32 pixel in pixels)
            {
                if (pixel.a > 2)
                {
                    return true;
                }
            }
            return false;
        }

        /// Makes the texture Unity's render target and reads it.
        private static Color32[] ReadByBinding(RenderTexture texture)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = texture;
            Color32[] pixels = ReadActive(texture.width, texture.height);
            RenderTexture.active = previous;
            return pixels;
        }

        /// Samples the texture into a temporary one and reads that, which never
        /// binds the texture itself.
        private static Color32[] ReadBySampling(RenderTexture texture)
        {
            RenderTexture copy = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(texture, copy);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = copy;
            Color32[] pixels = ReadActive(texture.width, texture.height);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(copy);
            return pixels;
        }

        private static Color32[] ReadActive(int width, int height)
        {
            var read = new Texture2D(width, height, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            read.Apply(false);
            Color32[] pixels = read.GetPixels32();
            Object.Destroy(read);
            return pixels;
        }
    }
}
