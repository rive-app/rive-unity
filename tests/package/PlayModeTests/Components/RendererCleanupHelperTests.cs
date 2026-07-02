using System;
using System.Collections;
using NUnit.Framework;
using Rive.Components;
using Rive.Components.Utilities;
using Rive.Tests.Utils;
using Rive.Utils;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rive.Tests
{
    public class RendererCleanupHelperTests
    {
        private MockLogger m_mockLogger;

        [SetUp]
        public void SetUp()
        {
            m_mockLogger = new MockLogger();
            DebugLogger.Instance = m_mockLogger;
        }

        [TearDown]
        public void TearDown()
        {
            var handlerMb = RenderPipelineHelper.CurrentHandler as MonoBehaviour;
            if (handlerMb != null)
            {
                UnityEngine.Object.DestroyImmediate(handlerMb.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator ReleaseRenderer_DisposesRenderQueue_AtEndOfFrame()
        {
            if (!NativeUsageGuard.IsNativeAvailable)
            {
                Assert.Ignore("Native plugin is not available on this platform; skipping renderer disposal test.");
                yield break;
            }

            Rive.Renderer renderer = RendererUtils.CreateRenderer();
            Assert.IsNotNull(
                renderer,
                "A renderer should be created when the native plugin is available.");

            Rive.RenderQueue renderQueue = renderer.RenderQueue;
            Assert.IsNotNull(renderQueue, "The renderer should expose a render queue.");
            Assert.AreNotEqual(
                IntPtr.Zero, renderQueue.m_nativeRenderQueue,
                "The native render queue should be allocated on creation.");

            RendererUtils.ReleaseRenderer(renderer);

            // Release is deferred to the end of the frame, so the queue must still be alive now.
            Assert.AreNotEqual(
                IntPtr.Zero, renderQueue.m_nativeRenderQueue,
                "RenderQueue should not be disposed immediately; release is deferred to end of frame.");

            yield return new WaitForEndOfFrame();
            yield return null;

            Assert.AreEqual(
                IntPtr.Zero, renderQueue.m_nativeRenderQueue,
                "RenderQueue should be disposed after the deferred end-of-frame release.");
        }

        [UnityTest]
        public IEnumerator Instance_RecoversAfterPreviousInstanceDestroyed()
        {
            var first = RendererCleanupHelper.Instance;
            Assert.IsNotNull(
                first,
                "The cleanup helper should be created lazily on first access.");

            var handlerMb = RenderPipelineHelper.CurrentHandler as MonoBehaviour;
            Assert.IsNotNull(
                handlerMb,
                "RenderPipelineHelper.CurrentHandler should be a MonoBehaviour.");

            // Simulate a Unity-as-a-Library unload by destroying the handler GameObject (which also
            // destroys the helper attached to it). We deliberately don't reset any statics, since
            // RuntimeInitializeOnLoadMethod doesn't re-fire on subsequent runEmbedded calls.
            UnityEngine.Object.DestroyImmediate(handlerMb.gameObject);

            yield return null;

            var second = RendererCleanupHelper.Instance;
            Assert.IsNotNull(
                second,
                "RendererCleanupHelper.Instance returned null after the previous instance was destroyed; the getter must be lifecycle-aware so it can recreate the helper.");
            Assert.AreNotSame(
                first, second,
                "RendererCleanupHelper.Instance returned the previously-destroyed instance instead of creating a fresh one.");
        }
    }
}
