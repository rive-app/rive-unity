using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Rive.Producer;
using Rive.Tests.Utils;
using Rive.Utils;
using UnityEngine;
using UnityEngine.TestTools;
using Rive.Host;

namespace Rive.Tests
{
    public class FutureTests
    {
        private MockLogger m_mockLogger;
        private ManualResetEventSlim m_gate;

        [SetUp]
        public void SetUp()
        {
            m_mockLogger = new MockLogger();
            DebugLogger.Instance = m_mockLogger;
            CommandTransport.EnsureStarted();
        }

        [TearDown]
        public void TearDown()
        {
            m_gate?.Set();
            m_gate = null;
            CommandTransport.EnsureStarted();
        }

        private void HoldServer()
        {
            Assert.IsTrue(CommandTransport.IsThreaded, "Needs a server thread.");
            var started = new ManualResetEventSlim(false);
            var gate = new ManualResetEventSlim(false);
            m_gate = gate;
            ServerGate.Hold(started, gate);
            Assert.IsTrue(started.Wait(2000), "The server should park on the hold.");
        }

        private static IEnumerator Frames(Func<bool> until)
        {
            for (int frame = 0; frame < 120 && !until(); frame++)
            {
                yield return null;
            }
        }

        private static async Task<int> AwaitOnce(Future<int> operation)
        {
            return await operation;
        }

        [UnityTest]
        public IEnumerator Await_GivesTheResult()
        {
            Future<int> operation = TestServer.EchoAsync(42);
            Task<int> awaited = AwaitOnce(operation);

            yield return Frames(() => awaited.IsCompleted);

            Assert.IsTrue(awaited.IsCompleted);
            Assert.AreEqual(42, awaited.Result);
        }

        [UnityTest]
        public IEnumerator Yield_WaitsUntilDone()
        {
            Future<int> operation = TestServer.EchoAsync(7);

            yield return operation;

            Assert.IsTrue(operation.IsDone);
            Assert.AreEqual(7, operation.Result);
        }

        [NeedsRiveThread]
        [Test]
        public void Result_BeforeDone_Throws()
        {
            HoldServer();
            Future<int> operation = TestServer.EchoAsync(1);

            Assert.IsFalse(operation.IsDone);
            Assert.Throws<InvalidOperationException>(() => { int _ = operation.Result; });
        }

        [UnityTest]
        public IEnumerator Completed_AfterDone_RunsStraightAway()
        {
            Future<int> operation = TestServer.EchoAsync(3);
            yield return operation;

            int seen = -1;
            operation.Completed += done => seen = done.Result;

            Assert.AreEqual(3, seen);
        }

        [UnityTest]
        public IEnumerator Completed_RunsOnTheMainThread()
        {
            int mainThread = Thread.CurrentThread.ManagedThreadId;
            int ranOn = -1;
            Future<int> operation = TestServer.EchoAsync(1);
            operation.Completed += _ => ranOn = Thread.CurrentThread.ManagedThreadId;

            yield return Frames(() => ranOn != -1);

            Assert.AreEqual(mainThread, ranOn);
        }

        [UnityTest]
        public IEnumerator Failure_ComesBackAsFailed()
        {
            Future<int> operation = TestServer.EchoAsync<int>(
                () => throw new InvalidOperationException("operation boom"));
            yield return operation;

            Assert.AreEqual(FutureStatus.Failed, operation.Status);
            Assert.AreEqual("operation boom", operation.Exception.Message);
            Assert.Throws<InvalidOperationException>(() => { int _ = operation.Result; });
        }

        [UnityTest]
        public IEnumerator AsTask_WorksWithWhenAll()
        {
            Task<int> first = TestServer.EchoAsync(1).AsTask();
            Task<int> second = TestServer.EchoAsync(2).AsTask();
            Task<int[]> both = Task.WhenAll(first, second);

            yield return Frames(() => both.IsCompleted);

            CollectionAssert.AreEqual(new[] { 1, 2 }, both.Result);
        }

        [Test]
        public void WaitForCompletion_GivesTheResult()
        {
            Future<int> operation = TestServer.EchoAsync(9);

            Assert.AreEqual(9, operation.WaitForCompletion());
        }

        [Test]
        public void WaitForCompletion_OnATick_RunsItNow()
        {
            var panelObject = RivePanelTestUtils.CreatePanel("OperationTickPanel").gameObject;
            try
            {
                var panel = panelObject.GetComponent<Rive.Components.RivePanel>();
                panel.UpdateMode = Rive.Components.RivePanel.PanelUpdateMode.Manual;
                Future tick = panel.TickAsync(0.016f);
                Assert.IsFalse(tick.IsDone);

                tick.WaitForCompletion();

                Assert.IsTrue(tick.IsDone);
                Assert.IsFalse(panel.HasTimeWaiting, "The queued time should have gone out.");
                Assert.IsFalse(panel.HasAdvanceInFlight, "And landed.");
            }
            finally
            {
                UnityEngine.Object.Destroy(panelObject);
            }
        }

        [UnityTest]
        public IEnumerator SecondAwait_Throws()
        {
            Future<int> operation = TestServer.EchoAsync(5);
            yield return operation;

            Assert.AreEqual(5, operation.GetAwaiter().GetResult());
            Assert.Throws<InvalidOperationException>(() => operation.GetAwaiter().GetResult());
            Assert.Throws<InvalidOperationException>(() => operation.AsTask());
            Assert.AreEqual(5, operation.Result, "Reading Result isn't a use.");
        }

        [NeedsRiveThread]
        [UnityTest]
        public IEnumerator HostGoing_CancelsWhatsOut()
        {
            HoldServer();
            Future<int> operation = TestServer.EchoAsync(1);
            bool canceled = false;
            operation.Completed += done => canceled = done.Status == FutureStatus.Canceled;

            CommandTransport.CancelOutstandingAsyncForTests();
            m_gate.Set();
            yield return Frames(() => operation.IsDone);

            Assert.AreEqual(FutureStatus.Canceled, operation.Status);
            Assert.IsTrue(canceled);
            Assert.Throws<OperationCanceledException>(() => { int _ = operation.Result; });
        }

        [NeedsRiveThread]
        [UnityTest]
        public IEnumerator SecondAwaitWhilePending_ThrowsInTheSecondMethod()
        {
            HoldServer();
            Future<int> future = TestServer.EchoAsync(42);
            Task<int> first = AwaitOnce(future);
            Task<int> second = AwaitOnce(future);

            yield return Frames(() => second.IsCompleted);

            Assert.IsTrue(second.IsFaulted);
            Assert.IsInstanceOf<InvalidOperationException>(second.Exception.InnerException);
            StringAssert.Contains("AsTask()", second.Exception.InnerException.Message);
            Assert.IsFalse(first.IsCompleted, "The first await is still waiting.");

            m_gate.Set();
            yield return Frames(() => first.IsCompleted);

            Assert.AreEqual(42, first.Result);
        }

        [NeedsManagedThreads]
        [NeedsRiveThread]
        [UnityTest]
        public IEnumerator BackgroundAwait_DoesNotContinueOnTheMainThread()
        {
            int mainThread = Thread.CurrentThread.ManagedThreadId;
            HoldServer();
            Future<int> future = TestServer.EchoAsync(1);
            int continuedOn = -1;
            var awaiting = new ManualResetEventSlim(false);
            Task background = Task.Run(async () =>
            {
                Task<int> pending = AwaitOnce(future);
                awaiting.Set();
                await pending;
                continuedOn = Thread.CurrentThread.ManagedThreadId;
            });
            Assert.IsTrue(awaiting.Wait(2000), "The background await should have started.");

            m_gate.Set();
            yield return Frames(() => background.IsCompleted);

            Assert.IsTrue(background.IsCompleted);
            Assert.AreNotEqual(-1, continuedOn);
            Assert.AreNotEqual(mainThread, continuedOn);
        }

        [NeedsRiveThread]
        [Test]
        public void AwaitAsTask_ContinuesInline()
        {
            HoldServer();
            Future<int> future = TestServer.EchoAsync(3);
            Task<int> task = future.AsTask();
            bool continued = false;
            async Task AwaitTask()
            {
                await task;
                continued = true;
            }
            Task awaiting = AwaitTask();

            m_gate.Set();
            CommandTransport.Barrier();
            CommandTransport.DrainMainThread();

            Assert.IsTrue(future.IsDone);
            Assert.IsTrue(continued, "It should continue inside the delivery, not later.");
            Assert.IsTrue(awaiting.IsCompleted);
        }

        [Test]
        public void Default_IsDone()
        {
            Future<int> operation = default;

            Assert.IsTrue(operation.IsDone);
            Assert.AreEqual(FutureStatus.Succeeded, operation.Status);
            Assert.AreEqual(0, operation.Result);
        }
    }
}
