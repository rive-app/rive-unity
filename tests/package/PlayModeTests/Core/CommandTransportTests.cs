using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using Rive.Host;
using Rive.Producer;
using Rive.Tests.Utils;
using Rive.Utils;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Rive.Tests
{
    /// <summary>
    /// Covers the transport on its own, with echo routines standing in for
    /// real work.
    /// </summary>
    public class CommandTransportTests
    {
        private MockLogger m_mockLogger;
        private readonly List<int> m_replies = new List<int>();

        [SetUp]
        public void SetUp()
        {
            m_mockLogger = new MockLogger();
            DebugLogger.Instance = m_mockLogger;
            CommandTransport.EnsureStarted();
            CommandTransport.Barrier();
            m_replies.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            CommandTransport.EnsureStarted();
        }

        // Sends an echo whose reply adds tag to m_replies. Keep it only to wait
        // on the ticket, or it stays pending for the tests after this one.
        private RequestTicket SendEcho(int tag, Action<HostMessageBatch, HostMessage> onReply = null, bool keep = false)
        {
            return CommandTransport.Send(
                id => HostNative.riveHostEcho(id, Array.Empty<byte>(), 0),
                (batch, message) =>
                {
                    m_replies.Add(tag);
                    onReply?.Invoke(batch, message);
                },
                keep);
        }

        [NeedsRiveThread]
        private sealed class EchoRequest
        {
        }

        private sealed class EchoResult
        {
        }

        private static void SendEchoRequest(EchoRequest request, ulong requestId)
        {
            HostNative.riveHostEcho(requestId, Array.Empty<byte>(), 0);
        }

        private static void Nothing(EchoRequest request, EchoResult result)
        {
        }

        // The tick, recording and captures all go through a channel every
        // frame, so sending and landing one must not allocate.
        [UnityPlatform(RuntimePlatform.OSXEditor, RuntimePlatform.WindowsEditor, RuntimePlatform.LinuxEditor)]
        [Test]
        public void ChannelSend_AndLanding_AllocateNothing()
        {
            var channel = new ProducerChannel<EchoRequest, EchoResult>(
                ChannelMode.Fifo, SendEchoRequest, null, Nothing, Nothing);
            TestDelegate cycle = () =>
            {
                channel.Send(channel.Begin());
                channel.JoinAll();
            };
            // Fills the pools and grows the tables first.
            for (int i = 0; i < 8; i++)
            {
                cycle();
            }

            Assert.That(cycle, Is.Not.AllocatingGCMemory());
        }

        // Translucent hit testing converts and hit tests on every raycast, a
        // synchronous panel advances after each pointer event, and scripts
        // advance every frame. Each waits, and none may allocate once warmed up.
        [UnityPlatform(RuntimePlatform.OSXEditor, RuntimePlatform.WindowsEditor, RuntimePlatform.LinuxEditor)]
        [UnityTest]
        public IEnumerator SynchronousPointerAndAdvanceCalls_AllocateNothing()
        {
            var loader = new TestAssetLoadingManager();
            Asset asset = null;
            yield return loader.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_cleanTheCar,
                a => asset = a,
                () => Assert.Fail("Failed to load the test asset."));
            File file = File.Load(asset);
            Artboard artboard = file.Artboard(0u);
            StateMachine stateMachine = artboard.StateMachine();
            var core = new Rive.Components.WidgetCore(artboard, stateMachine);
            var screen = new Rect(0, 0, 1920, 1080);
            TestDelegate raycast = () =>
            {
                Vector2 point = artboard.LocalCoordinate(new Vector2(900, 450), screen, Fit.Contain, Alignment.Center);
                stateMachine.HitTest(point);
                core.AdvanceAndWait(1f / 60f, 1f, false);
                stateMachine.Advance(1f / 60f);
            };
            for (int i = 0; i < 8; i++)
            {
                raycast();
            }

            Assert.That(raycast, Is.Not.AllocatingGCMemory());
            file.Dispose();
            loader.UnloadAllAssets();
        }

        [Test]
        public void Start_UsesAThreadInPlayMode()
        {
            Assert.IsTrue(CommandTransport.IsThreaded,
                          "Play mode should get a real server thread.");
        }

        [Test]
        public void Join_ReturnsOnceTheReplyLanded()
        {
            RequestTicket ticket = SendEcho(42, keep: true);

            Assert.IsTrue(CommandTransport.Join(ref ticket), "Join should report success.");
            CollectionAssert.AreEqual(new[] { 42 }, m_replies, "The reply should be in on return.");
            Assert.IsFalse(ticket.IsPending, "Join spends the ticket.");
        }

        [Test]
        public void Drain_SurvivesAThrowingReplyHandler()
        {
            SendEcho(1, (batch, message) => throw new InvalidOperationException("boom"));
            RequestTicket after = SendEcho(2, keep: true);
            CommandTransport.Join(ref after);

            Assert.IsTrue(m_mockLogger.LoggedExceptionsContains("boom"),
                          "The exception should be logged.");
            CollectionAssert.AreEqual(new[] { 1, 2 }, m_replies,
                                      "Later replies should still land.");
        }

        [NeedsRiveThread]
        [Test]
        public void Shutdown_ThenStart_Works()
        {
            CommandTransport.Shutdown();
            Assert.IsFalse(CommandHost.IsRunning, "Shutdown should stop the host.");

            CommandTransport.EnsureStarted();
            Assert.IsTrue(CommandTransport.IsThreaded, "Should restart cleanly.");

            RequestTicket ticket = SendEcho(5, keep: true);
            CommandTransport.Join(ref ticket);
            CollectionAssert.AreEqual(new[] { 5 }, m_replies, "The host should work after a restart.");
        }

        // A drain on another thread copies out of the host's buffer, so
        // shutdown has to wait for it rather than free the host under it.
        [NeedsRiveThread]
        [NeedsManagedThreads]
        [Test]
        public void Shutdown_WaitsForADrainOnAnotherThread()
        {
            var inDrain = new ManualResetEventSlim(false);
            var shutDown = new ManualResetEventSlim(false);
            bool runningThroughDrain = false;
            CommandTransport.BeforeDrainForTests = () =>
            {
                if (CommandTransport.IsMainThread)
                {
                    return;
                }
                inDrain.Set();
                // Returns early only if shutdown didn't wait.
                shutDown.Wait(500);
                runningThroughDrain = CommandHost.IsRunning;
            };
            try
            {
                var drainer = new Thread(CommandTransport.Drain);
                drainer.Start();
                Assert.IsTrue(inDrain.Wait(2000), "The other thread should have started draining.");
                CommandTransport.Shutdown();
                shutDown.Set();
                drainer.Join();
            }
            finally
            {
                CommandTransport.BeforeDrainForTests = null;
            }

            Assert.IsTrue(runningThroughDrain, "Shutdown stopped the host while another thread was draining.");
        }

        [NeedsRiveThread]
        [UnityTest]
        public IEnumerator ThreadChecks_NoViolationsDoingRealWork()
        {
            var loader = new TestAssetLoadingManager();
            Asset asset = null;
            yield return loader.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_asset_databinding_test,
                a => asset = a,
                () => Assert.Fail("Failed to load the test asset."));

            CommandTransport.Barrier();
            HostNative.riveResetThreadViolations();

            File file = File.Load(asset);
            Assert.IsNotNull(file, "Load should work through the host.");
            Artboard artboard = file.Artboard(0u);
            Assert.IsNotNull(artboard);
            StateMachine stateMachine = artboard.StateMachine();
            Assert.IsNotNull(stateMachine);
            stateMachine.Advance(1f / 60f);
            stateMachine.HitTest(new Vector2(0.5f, 0.5f));
            file.Dispose();
            CommandTransport.Barrier();

            HostNative.riveGetThreadViolations(out ThreadViolations v);
            loader.UnloadAllAssets();

            Assert.Greater(v.ServerCalls, 0u, "Routines should have run.");
            Assert.AreEqual(0u, v.MainViolations,
                            $"A host call came off the main thread. First: {v.FirstMainOffender}");
            Assert.AreEqual(0u, v.ServerViolations,
                            $"A routine ran off the server. First: {v.FirstServerOffender}");
        }

        [Test]
        public void Shutdown_RunsWhateverWasStillQueued()
        {
            HostNative.riveResetThreadViolations();

            // More than the server can clear before shutdown.
            for (int i = 0; i < 500; ++i)
            {
                SendEcho(i);
            }

            CommandTransport.Shutdown();

            HostNative.riveGetThreadViolations(out ThreadViolations v);
            Assert.GreaterOrEqual(v.ServerCalls, 500u,
                                  "Shutdown should finish queued work, not drop it. " +
                                  "Dropped releases leak native objects.");
        }

        [Test]
        public void Write_IsHeldUntilTheNextSend()
        {
            long sentBefore = CommandTransport.RequestsForTests;
            CommandTransport.Write(() => SendEcho(1));
            Assert.AreEqual(1, CommandTransport.PendingWriteCount, "The write should be held.");
            Assert.AreEqual(sentBefore, CommandTransport.RequestsForTests, "A held write shouldn't have gone out.");

            RequestTicket ticket = SendEcho(2, keep: true);
            CommandTransport.Join(ref ticket);

            CollectionAssert.AreEqual(new[] { 1, 2 }, m_replies,
                                      "The next send should go out behind every write made before it.");
            Assert.AreEqual(0, CommandTransport.PendingWriteCount);
        }

        [Test]
        public void Write_KeepsOrderWithOtherWork()
        {
            for (int i = 0; i < 100; ++i)
            {
                int captured = i;
                if (i % 3 == 0)
                {
                    SendEcho(captured);
                }
                else
                {
                    CommandTransport.Write(() => SendEcho(captured));
                }
            }
            CommandTransport.Barrier();

            Assert.AreEqual(100, m_replies.Count, "Every write and send should have landed.");
            for (int i = 0; i < m_replies.Count; ++i)
            {
                Assert.AreEqual(i, m_replies[i], "Writes should keep their place among other work.");
            }
        }

        [Test]
        public void FlushWrites_SendsHeldWrites()
        {
            CommandTransport.Write(() => SendEcho(2));
            CommandTransport.FlushWrites();
            Assert.AreEqual(0, CommandTransport.PendingWriteCount, "Flush should empty the batch.");

            CommandTransport.Barrier();
            CollectionAssert.AreEqual(new[] { 2 }, m_replies);
        }

        [Test]
        public void Post_GoesAfterHeldWrites()
        {
            CommandTransport.Write(() => SendEcho(1));
            CommandTransport.Post(() => SendEcho(2));
            CommandTransport.DrainMainThread();
            CommandTransport.Barrier();

            CollectionAssert.AreEqual(new[] { 1, 2 }, m_replies,
                                      "A release shouldn't overtake a write to the thing it frees.");
        }

        [NeedsManagedThreads]
        [NeedsRiveThread]
        [UnityTest]
        public IEnumerator Join_WorksFromABackgroundThread()
        {
            bool ok = false;
            int landedOn = -1;

            var caller = new Thread(() =>
            {
                RequestTicket ticket = CommandTransport.Send(
                    id => HostNative.riveHostEcho(id, Array.Empty<byte>(), 0),
                    (batch, message) => landedOn = Thread.CurrentThread.ManagedThreadId);
                ok = CommandTransport.Join(ref ticket);
            });
            caller.Start();
            while (caller.IsAlive)
            {
                yield return null;
            }

            Assert.IsTrue(ok, "A background caller should get its reply.");
            Assert.AreEqual(caller.ManagedThreadId, landedOn,
                            "A background request's reply lands on the thread that waits for it.");
        }

        [NeedsManagedThreads]
        [NeedsRiveThread]
        [UnityTest]
        public IEnumerator Write_FromABackgroundThreadIsSeenByTheNextWait()
        {
            var thread = new Thread(() => CommandTransport.Write(() => SendEcho(12)));
            thread.Start();
            while (thread.IsAlive)
            {
                yield return null;
            }

            RequestTicket ticket = SendEcho(13, keep: true);
            CommandTransport.Join(ref ticket);
            CollectionAssert.AreEqual(new[] { 12, 13 }, m_replies,
                                      "Writes from any thread go out ahead of the next send.");
        }

        [NeedsManagedThreads]
        [NeedsRiveThread]
        [UnityTest]
        public IEnumerator MainThreadReplies_StayOnTheMainThread_WhileAnotherThreadDrains()
        {
            int mainThread = Thread.CurrentThread.ManagedThreadId;
            var mainLandedOn = new List<int>();
            for (int i = 0; i < 20; i++)
            {
                SendEcho(i, (batch, message) => mainLandedOn.Add(Thread.CurrentThread.ManagedThreadId));
            }

            int backgroundJoins = 0;
            var caller = new Thread(() =>
            {
                for (int i = 0; i < 200; i++)
                {
                    RequestTicket ticket = CommandTransport.Send(
                        id => HostNative.riveHostEcho(id, Array.Empty<byte>(), 0));
                    if (CommandTransport.Join(ref ticket))
                    {
                        backgroundJoins++;
                    }
                }
            });
            caller.Start();
            while (caller.IsAlive)
            {
                yield return null;
            }
            CommandTransport.Barrier();

            Assert.AreEqual(200, backgroundJoins, "Every background wait should get its reply.");
            Assert.AreEqual(20, mainLandedOn.Count, "Every main thread reply should land.");
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19 },
                                      m_replies, "Main thread replies keep their order.");
            foreach (int thread in mainLandedOn)
            {
                Assert.AreEqual(mainThread, thread, "A main thread request's reply lands on the main thread.");
            }
        }

        [Test]
        public void Shutdown_RunsHeldWrites()
        {
            HostNative.riveResetThreadViolations();
            CommandTransport.Write(() => SendEcho(7));
            CommandTransport.Shutdown();

            HostNative.riveGetThreadViolations(out ThreadViolations v);
            Assert.GreaterOrEqual(v.ServerCalls, 1u, "Shutdown shouldn't drop held writes.");
            Assert.AreEqual(0, CommandTransport.PendingWriteCount);
        }
    }
}
