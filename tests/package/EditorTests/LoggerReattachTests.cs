using System.Collections;
using NUnit.Framework;
using Rive.Tests.Utils;
using Rive.Utils;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rive.Tests
{
    /// <summary>
    /// A domain reload leaves the plugin loaded and the managed delegate
    /// gone, which used to crash the editor on the next native log. The
    /// reload itself would take this test with it, so it drives the same
    /// drop and re-register directly.
    /// </summary>
    public class LoggerReattachTests
    {
        private const string ImportFailure = "failed to import file";

        private MockLogger m_logger;
        private IDebugLogger m_previous;

        [SetUp]
        public void SetUp()
        {
            m_previous = DebugLogger.Instance;
            m_logger = new MockLogger();
            DebugLogger.Instance = m_logger;
        }

        [TearDown]
        public void TearDown()
        {
            NativeLibrary.RegisterUnityLogForTests();
            DebugLogger.Instance = m_previous;
        }

        [UnityTest]
        public IEnumerator LogsWhileDetached_ArriveAfterReattaching()
        {
            NativeLibrary.DropUnityLogForTests();

            // This is the call that crashed. Native has no delegate now,
            // so it has to hold the message rather than call anything.
            File file = File.Load(new byte[] { 1, 2, 3, 4 }, cacheId: 0);
            Assert.IsNull(file, "Garbage bytes should not load.");

            yield return null;

            Assert.IsFalse(
                m_logger.AnyLogTypeContains(ImportFailure),
                "Native reached managed code with no delegate registered.");

            NativeLibrary.RegisterUnityLogForTests();

            float deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline &&
                   !m_logger.AnyLogTypeContains(ImportFailure))
            {
                yield return null;
            }

            Assert.IsTrue(
                m_logger.AnyLogTypeContains(ImportFailure),
                "The message logged while detached never arrived.");
        }
    }
}
