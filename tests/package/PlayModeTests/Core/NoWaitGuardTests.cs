using NUnit.Framework;
using Rive.Tests.Utils;
using Rive.Utils;
using Rive.Host;

namespace Rive.Tests
{
    public class NoWaitGuardTests
    {
        private MockLogger m_mockLogger;

        [SetUp]
        public void SetUp()
        {
            m_mockLogger = new MockLogger();
            DebugLogger.Instance = m_mockLogger;
            CommandTransport.EnsureStarted();
        }

        private static RequestTicket SendEcho()
        {
            return CommandTransport.Send(
                id => HostNative.riveHostEcho(id, System.Array.Empty<byte>(), 0));
        }

        [Test]
        public void Join_InsideNoWait_LogsTheReason()
        {
            RequestTicket ticket = SendEcho();
            using (CommandTransport.NoWait("test frame"))
            {
                CommandTransport.Join(ref ticket);
            }

            Assert.AreEqual(1, m_mockLogger.LoggedErrors.Count);
            StringAssert.Contains("Join", m_mockLogger.LoggedErrors[0]);
            StringAssert.Contains("test frame", m_mockLogger.LoggedErrors[0]);
        }

        [Test]
        public void FutureWait_InsideNoWait_LogsTheReason()
        {
            Future<int> echo = TestServer.EchoAsync(1);
            using (CommandTransport.NoWait("test frame"))
            {
                echo.WaitInternal();
            }

            Assert.AreEqual(1, m_mockLogger.LoggedErrors.Count);
            StringAssert.Contains("WaitForCompletion", m_mockLogger.LoggedErrors[0]);
        }

        [Test]
        public void Send_InsideNoWait_LogsNothing()
        {
            RequestTicket ticket;
            using (CommandTransport.NoWait("test frame"))
            {
                ticket = SendEcho();
                CommandTransport.Write(() => { });
            }
            CommandTransport.Join(ref ticket);

            Assert.IsEmpty(m_mockLogger.LoggedErrors);
        }

        [Test]
        public void AllowWait_InsideNoWait_LogsNothing()
        {
            RequestTicket ticket = SendEcho();
            using (CommandTransport.NoWait("test frame"))
            using (CommandTransport.AllowWait("test join"))
            {
                CommandTransport.Join(ref ticket);
            }

            Assert.IsEmpty(m_mockLogger.LoggedErrors);
        }

        [Test]
        public void Scopes_RestoreWhenDisposed()
        {
            RequestTicket ticket = SendEcho();
            using (CommandTransport.NoWait("outer"))
            {
                using (CommandTransport.AllowWait("join"))
                {
                    Assert.IsFalse(CommandTransport.InNoWaitScope);
                }
                Assert.IsTrue(CommandTransport.InNoWaitScope);

                CommandTransport.Join(ref ticket);
            }
            Assert.IsFalse(CommandTransport.InNoWaitScope);

            Assert.AreEqual(1, m_mockLogger.LoggedErrors.Count);
            StringAssert.Contains("outer", m_mockLogger.LoggedErrors[0]);
        }

        [Test]
        public void Barrier_InsideNoWait_LogsNothing()
        {
            using (CommandTransport.NoWait("test frame"))
            {
                CommandTransport.Barrier();
            }

            Assert.IsEmpty(m_mockLogger.LoggedErrors);
        }
    }
}
