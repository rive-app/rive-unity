using System.Collections;
using System.Threading;
using NUnit.Framework;
using Rive.Producer;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Rive.Host;
using Rive.Tests.Utils;

namespace Rive.Tests
{
    public class RenderQueueLifetimeTests
    {
        private const int Size = 64;
        private RenderTexture m_texture;

        [SetUp]
        public void SetUp()
        {
            if (!NativeUsageGuard.IsNativeAvailable)
            {
                Assert.Ignore("Needs the native plugin.");
            }
            CommandTransport.EnsureStarted();
            // Random writes only where the platform needs them; WebGL rejects them.
            m_texture = new RenderTexture(TextureHelper.Descriptor(Size, Size));
            m_texture.Create();
        }

        [TearDown]
        public void TearDown()
        {
            if (m_texture != null)
            {
                m_texture.Release();
                Object.Destroy(m_texture);
                m_texture = null;
            }
        }

        private static void Liveness(NativeRenderQueueHandle handle, uint renderId,
                                     out bool handleLive, out bool renderLive)
        {
            RenderQueue.RiveGetRenderQueueLiveness(handle, renderId, out handleLive, out renderLive);
        }

        private static IEnumerator RenderedFrames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return new WaitForEndOfFrame();
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Dispose_WithARenderEventStillQueued_RetiresTheQueue()
        {
            var queue = new RenderQueue(m_texture);
            NativeRenderQueueHandle handle = queue.NativeHandle;
            uint renderId = queue.RenderId;
            Assert.IsTrue(handle.IsValid);
            Assert.AreNotEqual(0u, renderId);

            Renderer renderer = queue.Renderer();
            renderer.ClipRect(Size, Size);
            renderer.Submit();
            queue.Dispose();

            Liveness(handle, renderId, out bool handleLive, out _);
            Assert.IsFalse(handleLive, "The handle should be released once the producer has it.");

            yield return RenderedFrames(3);

            Liveness(handle, renderId, out handleLive, out bool renderLive);
            Assert.IsFalse(handleLive);
            Assert.IsFalse(renderLive, "The retire event should have removed it.");
            Assert.AreEqual(0, RenderLifetime.PendingCount);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator PersistentBuffer_ReplayedAfterDispose_DoesNothing()
        {
            var queue = new RenderQueue(m_texture);
            NativeRenderQueueHandle handle = queue.NativeHandle;
            uint renderId = queue.RenderId;
            Renderer renderer = queue.Renderer();
            renderer.ClipRect(Size, Size);

            // Built once and replayed, the way a camera buffer is.
            var buffer = new CommandBuffer { name = "RiveLifetimeReplay" };
            buffer.SetRenderTarget(m_texture);
            renderer.AddToCommandBuffer(buffer);
            Graphics.ExecuteCommandBuffer(buffer);
            yield return RenderedFrames(1);

            queue.Dispose();
            for (int i = 0; i < 5; i++)
            {
                Graphics.ExecuteCommandBuffer(buffer);
                yield return RenderedFrames(1);
            }

            Liveness(handle, renderId, out bool handleLive, out bool renderLive);
            Assert.IsFalse(handleLive);
            Assert.IsFalse(renderLive);
            buffer.Release();
            LogAssert.NoUnexpectedReceived();
        }

#if !RIVE_USING_URP && !RIVE_USING_HDRP
        [UnityTest]
        public IEnumerator CameraBuffer_ReplayedAfterDispose_DoesNothing()
        {
            var cameraObject = new GameObject("RiveLifetimeCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.targetTexture = m_texture;

            var queue = new RenderQueue(m_texture);
            NativeRenderQueueHandle handle = queue.NativeHandle;
            uint renderId = queue.RenderId;
            Renderer renderer = queue.Renderer();
            renderer.ClipRect(Size, Size);

            var buffer = new CommandBuffer { name = "RiveLifetimeCamera" };
            renderer.AddToCommandBuffer(buffer);
            camera.AddCommandBuffer(CameraEvent.AfterEverything, buffer);
            yield return RenderedFrames(2);

            queue.Dispose();
            yield return RenderedFrames(5);

            Liveness(handle, renderId, out bool handleLive, out bool renderLive);
            Assert.IsFalse(handleLive);
            Assert.IsFalse(renderLive);

            camera.RemoveCommandBuffer(CameraEvent.AfterEverything, buffer);
            buffer.Release();
            Object.Destroy(cameraObject);
            LogAssert.NoUnexpectedReceived();
        }
#endif

        [UnityTest]
        public IEnumerator Record_AfterDispose_DoesNothing()
        {
            var queue = new RenderQueue(m_texture);
            NativeRenderQueueHandle handle = queue.NativeHandle;
            uint renderId = queue.RenderId;
            Renderer renderer = queue.Renderer();
            renderer.ClipRect(Size, Size);
            queue.Dispose();

            renderer.SetRecordsAsynchronously(false);
            renderer.Submit();
            renderer.SetRecordsAsynchronously(true);
            renderer.Submit();
            CommandTransport.Barrier();
            yield return RenderedFrames(2);

            Liveness(handle, renderId, out bool handleLive, out bool renderLive);
            Assert.IsFalse(handleLive);
            Assert.IsFalse(renderLive);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator RenderIds_AreNotReused()
        {
            var first = new RenderQueue(m_texture);
            uint firstId = first.RenderId;
            first.Dispose();
            yield return RenderedFrames(2);

            var second = new RenderQueue(m_texture);
            Assert.AreNotEqual(firstId, second.RenderId);
            second.Dispose();
            yield return RenderedFrames(1);
        }

        // MainThread panels record and wait every frame, so it should avoid allocating.
        [UnityPlatform(RuntimePlatform.OSXEditor, RuntimePlatform.WindowsEditor, RuntimePlatform.LinuxEditor)]
        [Test]
        public void SynchronousRecord_AllocatesNothing()
        {
            var queue = new RenderQueue(m_texture);
            Renderer renderer = queue.Renderer();
            renderer.ClipRect(Size, Size);
            TestDelegate record = renderer.RecordForGpuCanvas;
            for (int i = 0; i < 8; i++)
            {
                record();
            }

            Assert.That(record, UnityEngine.TestTools.Constraints.ConstraintExtensions.AllocatingGCMemory(
                UnityEngine.TestTools.Constraints.Is.Not));
            queue.Dispose();
        }

        // A buffer built once and replayed, like a camera's, never calls back
        // into the renderer, so the frame loop records for it.
        [UnityTest]
        public IEnumerator PersistentBuffer_IsRecordedEachFrame()
        {
            var queue = new RenderQueue(m_texture);
            Renderer renderer = queue.Renderer();
            renderer.ClipRect(Size, Size);
            var buffer = new CommandBuffer { name = "RiveReplayRecord" };
            renderer.AddToCommandBuffer(buffer);

            for (int i = 0; i < 3; i++)
            {
                yield return null;
                Assert.AreEqual(Renderer.LateStep - 1, renderer.RecordedAtStep,
                    "The last late step should have recorded it.");
            }

            buffer.Release();
            queue.Dispose();
        }

        // queue.Renderer().AddToCommandBuffer(buffer) leaves nothing holding
        // the renderer, but the buffer still replays it.
        [UnityTest]
        public IEnumerator PersistentBuffer_WithNothingHoldingItsRenderer_KeepsRecording()
        {
            var queue = new RenderQueue(m_texture);
            var buffer = new CommandBuffer { name = "RiveReplayUnheld" };
            System.WeakReference<Renderer> weak = AddWithoutKeeping(queue, buffer);
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();

            for (int i = 0; i < 3; i++)
            {
                yield return null;
            }

            Assert.IsTrue(weak.TryGetTarget(out Renderer renderer), "The queue should keep its renderer.");
            Assert.AreEqual(Renderer.LateStep - 1, renderer.RecordedAtStep,
                "The last late step should have recorded it.");
            buffer.Release();
            queue.Dispose();
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static System.WeakReference<Renderer> AddWithoutKeeping(RenderQueue queue, CommandBuffer buffer)
        {
            Renderer renderer = queue.Renderer();
            renderer.ClipRect(Size, Size);
            renderer.AddToCommandBuffer(buffer);
            return new System.WeakReference<Renderer>(renderer);
        }

        // Each record replaces the frame the queue shows, so only the renderer
        // added last is kept and recorded, however many there were.
        [UnityTest]
        public IEnumerator PersistentBuffers_OnOneQueue_OnlyTheLatestIsRecorded()
        {
            var queue = new RenderQueue(m_texture);
            var buffers = new CommandBuffer[3];
            var renderers = new Renderer[3];
            for (int i = 0; i < buffers.Length; i++)
            {
                buffers[i] = new CommandBuffer { name = "RiveReplayMany" };
                renderers[i] = queue.Renderer();
                renderers[i].ClipRect(Size, Size);
                renderers[i].AddToCommandBuffer(buffers[i]);
            }
            int olderRecordedAt = renderers[0].RecordedAtStep;

            for (int i = 0; i < 3; i++)
            {
                yield return null;
            }

            Assert.AreSame(renderers[2], queue.ReplayedRenderer, "The queue should keep only the latest.");
            Assert.AreEqual(olderRecordedAt, renderers[0].RecordedAtStep, "An older renderer shouldn't be recorded.");
            Assert.AreEqual(Renderer.LateStep - 1, renderers[2].RecordedAtStep,
                "The last late step should have recorded the latest.");
            foreach (CommandBuffer buffer in buffers)
            {
                buffer.Release();
            }
            queue.Dispose();
        }

        [UnityTest]
        public IEnumerator PersistentBuffer_ShowsDrawsAddedAfterItWasBuilt()
        {
            var loader = new TestAssetLoadingManager();
            Asset asset = null;
            yield return loader.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_roboDude,
                a => asset = a,
                () => Assert.Fail("Failed to load the test asset."));
            File file = File.Load(asset);
            Artboard artboard = file.Artboard(0u);
            artboard.StateMachine()?.Advance(0f);

            var queue = new RenderQueue(m_texture);
            Renderer renderer = queue.Renderer();
            var buffer = new CommandBuffer { name = "RiveReplayDraws" };
            buffer.SetRenderTarget(m_texture);
            // Built while the draw list is still empty.
            renderer.AddToCommandBuffer(buffer);
            renderer.Align(Fit.Contain, Alignment.Center, artboard);
            renderer.Draw(artboard);

            for (int i = 0; i < 5; i++)
            {
                Graphics.ExecuteCommandBuffer(buffer);
                yield return RenderedFrames(1);
            }

            Assert.Greater(VisiblePixels(m_texture), 0, "The replayed buffer should show the artboard.");
            buffer.Release();
            queue.Dispose();
            file.Dispose();
            loader.UnloadAllAssets();
        }

        // The same, recording on the server like a BackgroundThread panel. A
        // buffer built once carries the frame it was built in, which the
        // records that follow are newer than, so its replays can't wait on it.
        // Built-in's camera buffers are made this way.
        [UnityTest]
        public IEnumerator PersistentBuffer_RecordingAsynchronously_ShowsItsFirstFrameRightAway()
        {
            var loader = new TestAssetLoadingManager();
            Asset asset = null;
            yield return loader.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_roboDude,
                a => asset = a,
                () => Assert.Fail("Failed to load the test asset."));
            File file = File.Load(asset);
            Artboard artboard = file.Artboard(0u);
            artboard.StateMachine()?.Advance(0f);

            var queue = new RenderQueue(m_texture);
            Renderer renderer = queue.Renderer();
            renderer.SetRecordsAsynchronously(true);
            var buffer = new CommandBuffer { name = "RiveReplayAsync" };
            buffer.SetRenderTarget(m_texture);
            renderer.AddToCommandBuffer(buffer);
            renderer.Align(Fit.Contain, Alignment.Center, artboard);
            renderer.Draw(artboard);

            // After each frame's record, as a camera runs it.
            int shownAfter = -1;
            for (int i = 1; i <= 3 && shownAfter < 0; i++)
            {
                yield return new WaitForEndOfFrame();
                renderer.SettleRecordsForTests();
                Graphics.ExecuteCommandBuffer(buffer);
                if (VisiblePixels(m_texture) > 0)
                {
                    shownAfter = i;
                }
                yield return null;
            }

            buffer.Release();
            queue.Dispose();
            file.Dispose();
            loader.UnloadAllAssets();
            Assert.Greater(shownAfter, 0, "The replayed buffer should show the artboard within 3 frames.");
        }

        // Unity frees a texture in render thread order, so the replay has to
        // see texture changes in that order too. Here the server is late with
        // a texture that was already replaced and freed, as with Built-in's in
        // place resize. The replay shouldn't draw into the freed one.
        [UnityTest]
        public IEnumerator Texture_ReplacedAndFreedBeforeTheServerCatchesUp_IsNeverDrawnInto()
        {
            if (!CommandTransport.IsThreaded)
            {
                Assert.Ignore("Needs a server thread.");
            }
            var loader = new TestAssetLoadingManager();
            Asset asset = null;
            yield return loader.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_roboDude,
                a => asset = a,
                () => Assert.Fail("Failed to load the test asset."));
            File file = File.Load(asset);
            Artboard artboard = file.Artboard(0u);
            artboard.StateMachine()?.Advance(0f);

            var queue = new RenderQueue(m_texture);
            Renderer renderer = queue.Renderer();
            // So no record waits on the held server.
            renderer.SetRecordsAsynchronously(true);
            var buffer = new CommandBuffer { name = "RiveReplayReplacedTexture" };
            renderer.AddToCommandBuffer(buffer);
            renderer.Align(Fit.Contain, Alignment.Center, artboard);
            renderer.Draw(artboard);
            Graphics.ExecuteCommandBuffer(buffer);
            yield return RenderedFrames(1);

            var gate = new ManualResetEventSlim(false);
            var parkedAgain = new ManualResetEventSlim(false);
            var doomed = new RenderTexture(TextureHelper.Descriptor(Size, Size));
            try
            {
                ServerGate.Hold(new ManualResetEventSlim(false), gate);
                doomed.Create();
                queue.UpdateTexture(doomed);
                // Parks the server again right after it takes doomed.
                CommandTransport.Send(
                    id => HostNative.riveHostHold(id),
                    (batch, message) => parkedAgain.Set(),
                    keep: false);
                doomed.Release();
                Object.Destroy(doomed);
                queue.UpdateTexture(m_texture);
                // Long enough for Unity to free it on the render thread.
                yield return RenderedFrames(4);

                gate.Set();
                var timer = System.Diagnostics.Stopwatch.StartNew();
                while (!parkedAgain.IsSet && timer.ElapsedMilliseconds < 2000)
                {
                    CommandTransport.Drain();
                    Thread.Yield();
                }
                Assert.IsTrue(parkedAgain.IsSet, "The server should park after taking doomed.");

                // The server has only caught up to doomed.
                Graphics.ExecuteCommandBuffer(buffer);
                yield return RenderedFrames(1);
            }
            finally
            {
                gate.Set();
                HostNative.riveHostRelease();
            }
            yield return TestServer.EchoAsync(true);

            for (int i = 0; i < 3; i++)
            {
                Graphics.ExecuteCommandBuffer(buffer);
                yield return RenderedFrames(1);
            }

            Assert.Greater(VisiblePixels(m_texture), 0, "The queue should keep drawing into its live texture.");
            buffer.Release();
            queue.Dispose();
            file.Dispose();
            loader.UnloadAllAssets();
        }

        // A frame recorded for the old size, still waiting when a bigger texture
        // is handed over with the server held. The texture reaches replays
        // ahead of the server, so the generation has to come with it, or that
        // frame draws into the new texture laid out for the old one.
        [UnityTest]
        public IEnumerator Texture_ResizedWhileTheServerIsHeld_GetsNoFrameFromTheOldSize()
        {
            if (!CommandTransport.IsThreaded)
            {
                Assert.Ignore("Needs a server thread.");
            }
            var loader = new TestAssetLoadingManager();
            Asset asset = null;
            yield return loader.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_roboDude,
                a => asset = a,
                () => Assert.Fail("Failed to load the test asset."));
            File file = File.Load(asset);
            Artboard artboard = file.Artboard(0u);
            artboard.StateMachine()?.Advance(0f);

            var queue = new RenderQueue(m_texture);
            Renderer renderer = queue.Renderer();
            // So no record waits on the held server.
            renderer.SetRecordsAsynchronously(true);
            var buffer = new CommandBuffer { name = "RiveReplayResizedTexture" };
            renderer.AddToCommandBuffer(buffer);
            renderer.Align(Fit.Contain, Alignment.Center, artboard);
            renderer.Draw(artboard);
            Graphics.ExecuteCommandBuffer(buffer);
            yield return RenderedFrames(1);

            var bigger = new RenderTexture(TextureHelper.Descriptor(Size * 2, Size * 2));
            bigger.Create();
            var clear = new CommandBuffer { name = "ClearBigger" };
            clear.SetRenderTarget(bigger);
            clear.ClearRenderTarget(true, true, UnityEngine.Color.clear);
            Graphics.ExecuteCommandBuffer(clear);
            clear.Release();

            var gate = new ManualResetEventSlim(false);
            int visible;
            try
            {
                // A frame for the old size that nothing has replayed yet.
                yield return null;
                renderer.SettleRecordsForTests();
                ServerGate.Hold(new ManualResetEventSlim(false), gate);
                queue.UpdateTexture(bigger);
                // The frame after, so the waiting frame is within the bound.
                yield return null;
                Graphics.ExecuteCommandBuffer(buffer);
                yield return RenderedFrames(1);
                visible = VisiblePixels(bigger);
            }
            finally
            {
                gate.Set();
            }
            yield return TestServer.EchoAsync(true);

            buffer.Release();
            queue.Dispose();
            bigger.Release();
            Object.Destroy(bigger);
            file.Dispose();
            loader.UnloadAllAssets();
            Assert.AreEqual(0, visible, "A frame laid out for the old size drew into the new texture.");
        }

        // A texture destroyed while its queue still draws, with nothing handed
        // over in its place. The next record sees it's gone, so the replay
        // skips it rather than drawing into freed memory, on every backend.
        [UnityTest]
        public IEnumerator Texture_DestroyedWhileItsQueueStillDraws_IsSkipped()
        {
            var loader = new TestAssetLoadingManager();
            Asset asset = null;
            yield return loader.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_roboDude,
                a => asset = a,
                () => Assert.Fail("Failed to load the test asset."));
            File file = File.Load(asset);
            Artboard artboard = file.Artboard(0u);
            artboard.StateMachine()?.Advance(0f);

            var queue = new RenderQueue(m_texture);
            Renderer renderer = queue.Renderer();
            var buffer = new CommandBuffer { name = "RiveReplayDestroyedTexture" };
            renderer.AddToCommandBuffer(buffer);
            renderer.Align(Fit.Contain, Alignment.Center, artboard);
            renderer.Draw(artboard);
            Graphics.ExecuteCommandBuffer(buffer);
            yield return RenderedFrames(1);

            // Handed over, so the next replay wraps it fresh.
            var doomed = new RenderTexture(TextureHelper.Descriptor(Size, Size));
            doomed.Create();
            queue.UpdateTexture(doomed);
            doomed.Release();
            Object.Destroy(doomed);
            // Frees it on the render thread, and records the queue.
            yield return RenderedFrames(4);

            Graphics.ExecuteCommandBuffer(buffer);
            yield return RenderedFrames(1);

            queue.UpdateTexture(m_texture);
            for (int i = 0; i < 3; i++)
            {
                Graphics.ExecuteCommandBuffer(buffer);
                yield return RenderedFrames(1);
            }

            Assert.Greater(VisiblePixels(m_texture), 0, "The queue should draw again once it has a live texture.");
            buffer.Release();
            queue.Dispose();
            file.Dispose();
            loader.UnloadAllAssets();
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
