using System.Collections.Generic;
using NUnit.Framework;
using Rive.Host;

namespace Rive.Tests
{
    public class CommandHostTests
    {
        private struct Reply
        {
            public RoutineTag Tag;
            public ulong RequestId;
            public byte[] Payload;
        }

        private readonly List<Reply> m_replies = new List<Reply>();

        [SetUp]
        public void SetUp()
        {
            CommandTransport.EnsureStarted();
            CommandTransport.Barrier();
            HostNative.riveResetThreadViolations();
            m_replies.Clear();
        }

        private RequestTicket SendEcho(byte[] payload)
        {
            return CommandTransport.Send(
                id => HostNative.riveHostEcho(id, payload, (uint)payload.Length),
                (batch, message) => m_replies.Add(new Reply
                {
                    Tag = (RoutineTag)message.Code,
                    RequestId = message.RequestId,
                    Payload = batch.CopyPayload(message),
                }));
        }

        private static byte[] PayloadFor(int index)
        {
            var payload = new byte[index % 21];
            for (int i = 0; i < payload.Length; i++)
            {
                payload[i] = (byte)(index * 31 + i);
            }
            return payload;
        }

        [Test]
        public void Echo_ReturnsPayload()
        {
            var payload = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            RequestTicket ticket = SendEcho(payload);

            Assert.IsTrue(CommandTransport.Join(ref ticket));

            Assert.AreEqual(1, m_replies.Count);
            Assert.AreEqual(RoutineTag.Echo, m_replies[0].Tag);
            CollectionAssert.AreEqual(payload, m_replies[0].Payload);
        }

        [Test]
        public void Echo_RepliesComeBackInOrder()
        {
            var sent = new List<ulong>();
            RequestTicket last = default;
            for (int i = 0; i < 200; i++)
            {
                last = SendEcho(PayloadFor(i));
                sent.Add(last.Id);
            }

            CommandTransport.Join(ref last);

            Assert.AreEqual(sent.Count, m_replies.Count);
            for (int i = 0; i < sent.Count; i++)
            {
                Assert.AreEqual(sent[i], m_replies[i].RequestId);
                CollectionAssert.AreEqual(PayloadFor(i), m_replies[i].Payload);
            }
        }

        [NeedsRiveThread]
        [Test]
        public void Hold_ParksServerUntilReleased()
        {
            if (!CommandHost.IsThreaded)
            {
                Assert.Ignore("Inline hosts can't hold the server.");
            }

            var started = new System.Threading.ManualResetEventSlim(false);
            var gate = new System.Threading.ManualResetEventSlim(false);
            ServerGate.Hold(started, gate);
            Assert.IsTrue(started.IsSet, "The hold should report once the server parks.");

            RequestTicket echo = SendEcho(new byte[] { 42 });
            System.Threading.Thread.Sleep(50);
            CommandTransport.Drain();
            Assert.IsEmpty(m_replies, "Nothing should run while held.");

            gate.Set();
            CommandTransport.Join(ref echo);
            Assert.AreEqual(1, m_replies.Count);
        }

        [Test]
        public void Stop_DropsQueuedReplies_AndStartGivesFreshHost()
        {
            SendEcho(new byte[] { 1 });
            CommandTransport.Shutdown();
            Assert.IsFalse(HostNative.riveHostIsRunning());

            CommandTransport.EnsureStarted();
            RequestTicket ticket = SendEcho(new byte[] { 2 });
            ulong id = ticket.Id;
            CommandTransport.Join(ref ticket);

            Assert.AreEqual(1, m_replies.Count, "Replies from before the stop are dropped.");
            Assert.AreEqual(id, m_replies[0].RequestId);
            CollectionAssert.AreEqual(new byte[] { 2 }, m_replies[0].Payload);
        }

        [Test]
        public void ThreadChecks_StayClean()
        {
            RequestTicket ticket = SendEcho(new byte[] { 3 });
            CommandTransport.Join(ref ticket);

            HostNative.riveGetThreadViolations(out var violations);

            Assert.Greater(violations.ServerCalls, 0u);
            Assert.AreEqual(
                0u,
                violations.MainViolations,
                violations.FirstMainOffender
            );
            Assert.AreEqual(
                0u,
                violations.ServerViolations,
                violations.FirstServerOffender
            );
        }
    }
}
