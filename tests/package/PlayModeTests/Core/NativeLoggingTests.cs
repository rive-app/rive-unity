using System.Collections;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using Rive.Utils;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rive.Tests
{
    /// <summary>
    /// Native queues its logs and the main thread hands them to Unity, so
    /// the producer and render threads never call managed code.
    /// </summary>
    public class NativeLoggingTests
    {
        /// Records which thread each message came in on. A plain message
        /// log would still pass if native went back to calling the
        /// callback straight off the producer.
        private sealed class ThreadRecordingLogger : IDebugLogger
        {
            private readonly object m_lock = new object();
            private readonly List<(string Message, int ThreadId)> m_entries =
                new List<(string, int)>();

            public void Log(string message) => Record(message);
            public void LogWarning(string message) => Record(message);
            public void LogError(string message) => Record(message);
            public void LogException(System.Exception exception) =>
                Record(exception.Message);

            private void Record(string message)
            {
                lock (m_lock)
                {
                    m_entries.Add((message, Thread.CurrentThread.ManagedThreadId));
                }
            }

            internal bool TryFind(string contains, out int threadId)
            {
                lock (m_lock)
                {
                    foreach ((string Message, int ThreadId) entry in m_entries)
                    {
                        if (entry.Message != null && entry.Message.Contains(contains))
                        {
                            threadId = entry.ThreadId;
                            return true;
                        }
                    }
                }
                threadId = 0;
                return false;
            }
        }

        private const string ImportFailure = "failed to import file";

        private ThreadRecordingLogger m_logger;
        private IDebugLogger m_previous;

        [SetUp]
        public void SetUp()
        {
            m_previous = DebugLogger.Instance;
            m_logger = new ThreadRecordingLogger();
            DebugLogger.Instance = m_logger;
        }

        [TearDown]
        public void TearDown()
        {
            DebugLogger.Instance = m_previous;
        }

        [UnityTest]
        public IEnumerator NativeMessages_ReachUnity_OnTheMainThread()
        {
            int mainThread = Thread.CurrentThread.ManagedThreadId;

            // Garbage bytes make the importer log, natively, and the import
            // itself runs on the producer.
            File file = File.Load(new byte[] { 1, 2, 3, 4 }, cacheId: 0);
            Assert.IsNull(file, "Garbage bytes should not load.");

            int threadId = 0;
            float deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline &&
                   !m_logger.TryFind(ImportFailure, out threadId))
            {
                yield return null;
            }

            Assert.IsTrue(
                m_logger.TryFind(ImportFailure, out threadId),
                "Nothing native logged reached Unity. The drain is the only "
                    + "way out now, so this means messages are stuck.");

            Assert.AreEqual(
                mainThread,
                threadId,
                "Native called back on another thread. Logs have to be "
                    + "queued and drained, the producer must not run "
                    + "managed code.");
        }
    }
}
