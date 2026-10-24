using NUnit.Framework;
using Rive.Host;
using Rive.Producer;
using Rive.Tests.Utils;
using Rive.Utils;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rive.Tests
{
    /// <summary>
    /// Drawing without a RenderTexture is deprecated, so a queue made through
    /// the public constructor warns once when it draws with no target.
    /// </summary>
    public class RenderQueueTargetTests
    {
        private const int Size = 64;
        private IDebugLogger m_previousLogger;
        private MockLogger m_logger;
        private RenderTexture m_texture;

        [SetUp]
        public void SetUp()
        {
            if (!NativeUsageGuard.IsNativeAvailable)
            {
                Assert.Ignore("Needs the native plugin.");
            }
            CommandTransport.EnsureStarted();
            m_previousLogger = DebugLogger.Instance;
            m_logger = new MockLogger();
            DebugLogger.Instance = m_logger;
            RenderQueue.ResetWarningForTests();
            m_texture = new RenderTexture(TextureHelper.Descriptor(Size, Size));
            m_texture.Create();
        }

        [TearDown]
        public void TearDown()
        {
            DebugLogger.Instance = m_previousLogger;
            RenderQueue.ResetWarningForTests();
            if (m_texture != null)
            {
                m_texture.Release();
                Object.Destroy(m_texture);
                m_texture = null;
            }
        }

        [Test]
        public void DrawingWithoutATarget_WarnsOnce()
        {
            using var first = new RenderQueue();
            using var second = new RenderQueue(null, false);
            AddToCommandBuffer(first);
            AddToCommandBuffer(second);

            Assert.AreEqual(1, m_logger.LoggedWarnings.FindAll(w => w.Contains("without a RenderTexture")).Count);
        }

        [Test]
        public void DrawingWithATarget_DoesNotWarn()
        {
            using var given = new RenderQueue(m_texture);
            using var updated = new RenderQueue();
            updated.UpdateTexture(m_texture);
            AddToCommandBuffer(given);
            AddToCommandBuffer(updated);

            Assert.IsFalse(m_logger.LoggedWarningsContains("without a RenderTexture"));
        }

        [Test]
        public void OurOwnQueuesWithoutATarget_DoNotWarn()
        {
            using var queue = new RenderQueue(null, true, null);
            AddToCommandBuffer(queue);

            Assert.IsFalse(m_logger.LoggedWarningsContains("without a RenderTexture"));
        }

        private static void AddToCommandBuffer(RenderQueue queue)
        {
            // Never executed, so nothing draws.
            using var commandBuffer = new CommandBuffer();
            queue.Renderer().AddToCommandBuffer(commandBuffer);
        }
    }
}
