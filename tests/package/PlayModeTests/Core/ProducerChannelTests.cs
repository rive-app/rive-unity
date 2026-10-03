using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using Rive.Producer;
using Rive.Tests.Utils;
using Rive.Utils;
using UnityEngine.TestTools;
using Rive.Host;

namespace Rive.Tests
{
    public class ProducerChannelTests
    {
        private sealed class Request
        {
            public int Value;
        }

        private sealed class Result
        {
            public int Value;
            public int ThreadId;
        }

        private MockLogger m_mockLogger;
        private ManualResetEventSlim m_gate;
        private readonly List<int> m_landed = new List<int>();
        private readonly List<Request> m_reset = new List<Request>();
        private int m_sent;

        [SetUp]
        public void SetUp()
        {
            m_mockLogger = new MockLogger();
            DebugLogger.Instance = m_mockLogger;
            CommandTransport.EnsureStarted();
            m_landed.Clear();
            m_reset.Clear();
            m_sent = 0;
        }

        [TearDown]
        public void TearDown()
        {
            m_gate?.Set();
            m_gate = null;
            CommandTransport.EnsureStarted();
            CommandTransport.Barrier();
        }

        // Each request goes out as an echo carrying its value.
        private void SendEcho(Request request, ulong requestId)
        {
            m_sent++;
            byte[] payload = BitConverter.GetBytes(request.Value);
            HostNative.riveHostEcho(requestId, payload, (uint)payload.Length);
        }

        private static void ReadEcho(Request request, Result result, HostMessageBatch batch, HostMessage message)
        {
            result.Value = BitConverter.ToInt32(batch.Bytes, message.PayloadOffset);
            result.ThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        private ProducerChannel<Request, Result> MakeChannel(
            ChannelMode mode,
            Action<Request, Result, HostMessageBatch, HostMessage> parse = null,
            Action<Request, Result> onLanded = null)
        {
            return new ProducerChannel<Request, Result>(
                mode,
                SendEcho,
                parse ?? ReadEcho,
                onLanded ?? ((request, result) => m_landed.Add(result.Value)),
                (request, result) =>
                {
                    m_reset.Add(request);
                    request.Value = 0;
                    result.Value = 0;
                });
        }

        private static void Send(ProducerChannel<Request, Result> channel, int value, object key = null)
        {
            Request request = channel.Begin(key);
            request.Value = value;
            channel.Send(request);
        }

        // Parks the server until the gate opens.
        private void HoldServer()
        {
            Assert.IsTrue(CommandTransport.IsThreaded, "Needs a server thread.");
            var started = new ManualResetEventSlim(false);
            var gate = new ManualResetEventSlim(false);
            m_gate = gate;
            ServerGate.Hold(started, gate);
            Assert.IsTrue(started.Wait(2000), "The server should park on the hold.");
        }

        private void ReleaseServer()
        {
            m_gate?.Set();
        }

        [NeedsRiveThread]
        [Test]
        public void Poll_DoesNotWaitWhileTheServerIsHeld()
        {
            var channel = MakeChannel(ChannelMode.Fifo);
            HoldServer();
            try
            {
                Send(channel, 1);
                var watch = System.Diagnostics.Stopwatch.StartNew();
                channel.Poll();
                channel.Poll();
                watch.Stop();

                Assert.IsEmpty(m_landed, "Nothing should land while the server is held.");
                Assert.Less(watch.ElapsedMilliseconds, 100, "Poll shouldn't wait.");
                Assert.AreEqual(1, channel.InFlightCount);
            }
            finally
            {
                ReleaseServer();
            }

            channel.JoinAll();
            CollectionAssert.AreEqual(new[] { 1 }, m_landed);
        }

        [UnityTest]
        public IEnumerator Poll_LandsResultsOnLaterFrames()
        {
            var channel = MakeChannel(ChannelMode.Fifo);
            Send(channel, 5);

            for (int frame = 0; frame < 60 && m_landed.Count == 0; frame++)
            {
                yield return null;
                channel.Poll();
            }

            CollectionAssert.AreEqual(new[] { 5 }, m_landed);
            Assert.AreEqual(0, channel.InFlightCount);
        }

        [NeedsRiveThread]
        [Test]
        public void Fifo_LandsInSendOrder()
        {
            var channel = MakeChannel(ChannelMode.Fifo);
            HoldServer();
            for (int i = 0; i < 20; i++)
            {
                Send(channel, i);
            }
            ReleaseServer();
            channel.JoinAll();

            var expected = new List<int>();
            for (int i = 0; i < 20; i++)
            {
                expected.Add(i);
            }
            CollectionAssert.AreEqual(expected, m_landed);
        }

        [Test]
        public void Fifo_LandsOnTheMainThread()
        {
            int mainThread = Thread.CurrentThread.ManagedThreadId;
            var threads = new List<int>();
            var channel = MakeChannel(
                ChannelMode.Fifo,
                onLanded: (request, result) => threads.Add(result.ThreadId));

            Send(channel, 1);
            channel.JoinAll();

            CollectionAssert.AreEqual(new[] { mainThread }, threads);
        }

        [NeedsRiveThread]
        [Test]
        public void LatestValue_BeginHandsBackTheHeldRequest()
        {
            var channel = MakeChannel(ChannelMode.LatestValue);
            var key = new object();
            HoldServer();

            Send(channel, 1, key);
            Request second = channel.Begin(key);
            second.Value = 2;
            channel.Send(second);

            Request again = channel.Begin(key);
            Assert.AreSame(second, again, "The held request should come back.");
            Assert.AreEqual(2, again.Value, "It should still hold what was sent.");
            again.Value = 3;
            channel.Send(again);

            Assert.AreEqual(1, m_sent, "Only the first should have gone out.");
            Assert.AreEqual(2, channel.InFlightCount);

            ReleaseServer();
            channel.JoinAll();
            CollectionAssert.AreEqual(new[] { 1, 3 }, m_landed);
            Assert.AreEqual(2, m_sent);
        }

        [NeedsRiveThread]
        [Test]
        public void LatestValue_KeepsOtherKeys()
        {
            var channel = MakeChannel(ChannelMode.LatestValue);
            HoldServer();
            Send(channel, 1, "a");
            Send(channel, 2, "b");
            Send(channel, 3, "a");
            Send(channel, 4, "a");
            ReleaseServer();
            channel.JoinAll();

            CollectionAssert.AreEqual(new[] { 1, 2, 4 }, m_landed);
        }

        [NeedsRiveThread]
        [Test]
        public void LatestValue_DeliversTheSentRequestAndTheNewest()
        {
            var channel = MakeChannel(ChannelMode.LatestValue);
            var key = new object();
            HoldServer();

            Send(channel, 1, key);
            Send(channel, 2, key);
            Send(channel, 3, key);
            ReleaseServer();
            channel.JoinAll();

            CollectionAssert.AreEqual(new[] { 1, 3 }, m_landed);
        }

        [NeedsRiveThread]
        [Test]
        public void JoinAll_LandsEverything()
        {
            var channel = MakeChannel(ChannelMode.Fifo);
            HoldServer();
            for (int i = 0; i < 5; i++)
            {
                Send(channel, i);
            }
            Assert.IsEmpty(m_landed);
            ReleaseServer();

            channel.JoinAll();

            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, m_landed);
            Assert.AreEqual(0, channel.InFlightCount);
        }

        [Test]
        public void JoinAll_FromOnLandedThrows()
        {
            ProducerChannel<Request, Result> channel = null;
            Exception thrown = null;
            channel = MakeChannel(
                ChannelMode.Fifo,
                onLanded: (request, result) =>
                {
                    try
                    {
                        channel.JoinAll();
                    }
                    catch (Exception e)
                    {
                        thrown = e;
                    }
                });

            Send(channel, 1);
            Send(channel, 2);
            channel.JoinAll();

            Assert.IsInstanceOf<InvalidOperationException>(thrown);
            StringAssert.Contains("onLanded", thrown.Message);
        }

        [NeedsRiveThread]
        [Test]
        public void Drop_SkipsDeliveryAndRecycles()
        {
            var channel = MakeChannel(ChannelMode.Fifo);
            HoldServer();
            Send(channel, 1);
            Request dropped = channel.Begin();
            dropped.Value = 2;
            channel.Send(dropped);
            Send(channel, 3);

            channel.Drop(request => request.Value == 2);
            CollectionAssert.Contains(m_reset, dropped, "A dropped request should be reset.");

            ReleaseServer();
            channel.JoinAll();

            CollectionAssert.AreEqual(new[] { 1, 3 }, m_landed);

            var reused = new List<Request>();
            for (int i = 0; i < 3; i++)
            {
                reused.Add(channel.Begin());
            }
            CollectionAssert.Contains(reused, dropped, "The dropped request should be reused.");
        }

        [NeedsRiveThread]
        [Test]
        public void Drop_OfASentRequestSkipsDelivery()
        {
            var channel = MakeChannel(ChannelMode.Fifo);
            HoldServer();
            Send(channel, 1);

            channel.Drop(request => true);
            ReleaseServer();
            channel.JoinAll();
            CommandTransport.Barrier();
            channel.Poll();

            Assert.IsEmpty(m_landed);
            Assert.AreEqual(1, m_reset.Count, "It should be recycled once.");
        }

        [NeedsRiveThread]
        [Test]
        public void TryCancel_RecyclesAHeldRequest()
        {
            var channel = MakeChannel(ChannelMode.LatestValue);
            var key = new object();
            HoldServer();
            Send(channel, 1, key);
            Request cancelled = channel.Begin(key);
            cancelled.Value = 2;
            channel.Send(cancelled);

            Assert.IsTrue(channel.IsUnstarted(cancelled));
            Assert.IsTrue(channel.TryCancel(cancelled));
            CollectionAssert.Contains(m_reset, cancelled);

            ReleaseServer();
            channel.JoinAll();
            CollectionAssert.AreEqual(new[] { 1 }, m_landed);
            Assert.AreEqual(1, m_sent);
        }

        [Test]
        public void Discard_RecyclesAnUnsentRequest()
        {
            var channel = MakeChannel(ChannelMode.Fifo);
            Request discarded = channel.Begin();
            channel.Discard(discarded);

            CollectionAssert.Contains(m_reset, discarded);
            Assert.AreEqual(0, channel.InFlightCount);
            Assert.AreSame(discarded, channel.Begin(), "The request should be reused.");
        }

        [NeedsRiveThread]
        [Test]
        public void TryCancel_LeavesASentRequestToLand()
        {
            var channel = MakeChannel(ChannelMode.Fifo);
            HoldServer();

            Request request = channel.Begin();
            request.Value = 7;
            channel.Send(request);

            Assert.IsFalse(channel.TryCancel(request));
            ReleaseServer();
            channel.JoinAll();
            CollectionAssert.AreEqual(new[] { 7 }, m_landed);
        }

        [Test]
        public void OnLandedException_DoesNotStopLaterResults()
        {
            var channel = MakeChannel(
                ChannelMode.Fifo,
                onLanded: (request, result) =>
                {
                    if (result.Value == 1)
                    {
                        throw new InvalidOperationException("landed boom");
                    }
                    m_landed.Add(result.Value);
                });

            Send(channel, 1);
            Send(channel, 2);
            Send(channel, 3);
            channel.JoinAll();

            CollectionAssert.AreEqual(new[] { 2, 3 }, m_landed);
            Assert.IsTrue(m_mockLogger.LoggedExceptionsContains("landed boom"));
        }

        [Test]
        public void ParseException_DropsOnlyThatResult()
        {
            var channel = MakeChannel(
                ChannelMode.Fifo,
                (request, result, batch, message) =>
                {
                    if (request.Value == 1)
                    {
                        throw new InvalidOperationException("parse boom");
                    }
                    result.Value = request.Value;
                });

            Send(channel, 1);
            Send(channel, 2);
            channel.JoinAll();

            CollectionAssert.AreEqual(new[] { 2 }, m_landed);
            Assert.IsTrue(m_mockLogger.LoggedExceptionsContains("parse boom"));
        }

        [Test]
        public void Pooling_ReusesObjectsAfterWarmUp()
        {
            var results = new List<Result>();
            var channel = MakeChannel(
                ChannelMode.Fifo,
                onLanded: (request, result) => results.Add(result));

            Request warmRequest = channel.Begin();
            channel.Send(warmRequest);
            channel.JoinAll();
            Result warmResult = results[0];

            for (int i = 0; i < 5; i++)
            {
                Request request = channel.Begin();
                Assert.AreSame(warmRequest, request, "The request should be reused.");
                channel.Send(request);
                channel.JoinAll();
                Assert.AreSame(warmResult, results[results.Count - 1], "The result should be reused.");
            }
        }

        [NeedsRiveThread]
        [Test]
        public void StoppedHost_DropsWhatNeverLanded()
        {
            var channel = MakeChannel(ChannelMode.Fifo);
            HoldServer();
            Send(channel, 1);

            CommandTransport.Shutdown();
            ReleaseServer();

            channel.Poll();
            Assert.AreEqual(0, channel.InFlightCount, "The request should be dropped.");
            Assert.IsEmpty(m_landed);
            Assert.AreEqual(1, m_reset.Count, "The dropped request should be recycled.");

            CommandTransport.EnsureStarted();
            Send(channel, 2);
            channel.JoinAll();
            CollectionAssert.AreEqual(new[] { 2 }, m_landed);
        }
    }
}
