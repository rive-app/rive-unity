using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using Rive.Producer;
using Rive.Tests.Utils;
using Rive.Utils;
using UnityEngine;
using UnityEngine.TestTools;
using Rive.Host;

namespace Rive.Tests
{
    /// <summary>
    /// The resolution contract is that a handle returned before Rive has looked it
    /// up is Pending, becomes Valid or Invalid once the lookup has run, keeps
    /// what it found, and a failed lookup is reported once while work queued
    /// on it is skipped.
    /// </summary>
    public class HandleResolutionTests
    {
        private TestAssetLoadingManager m_assetLoader;
        private MockLogger m_mockLogger;
        private readonly List<IDisposable> m_toDispose = new List<IDisposable>();

        [SetUp]
        public void SetUp()
        {
            m_mockLogger = new MockLogger();
            DebugLogger.Instance = m_mockLogger;
            m_assetLoader = new TestAssetLoadingManager();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = m_toDispose.Count - 1; i >= 0; i--)
            {
                m_toDispose[i]?.Dispose();
            }
            m_toDispose.Clear();
            m_assetLoader.UnloadAllAssets();
        }

        private T Track<T>(T disposable) where T : IDisposable
        {
            m_toDispose.Add(disposable);
            return disposable;
        }

        private IEnumerator LoadFile(string path, Action<FileHandle> onLoaded)
        {
            Asset asset = null;
            yield return m_assetLoader.LoadAssetCoroutine<Asset>(
                path, a => asset = a, () => Assert.Fail($"Failed to load {path}"));
            Future<FileHandle> load = FileHandle.LoadAsync(asset);
            yield return WaitUntil(() => load.IsDone, "The file load");
            Assert.AreEqual(FutureStatus.Succeeded, load.Status);
            onLoaded(Track(load.Result));
        }

        // A state machine off the first artboard with its default instance bound.
        private IEnumerator LoadBound(string path, Action<FileHandle, StateMachineHandle, ViewModelInstanceHandle> onLoaded)
        {
            FileHandle file = null;
            yield return LoadFile(path, f => file = f);
            StateMachineHandle stateMachine = Track(Track(file.InstantiateArtboard()).InstantiateStateMachine());
            stateMachine.BindViewModelInstanceAsync(null);
            onLoaded(file, stateMachine, Track(stateMachine.GetViewModelInstance()));
        }

        private static IEnumerator WaitUntil(Func<bool> condition, string what)
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!condition())
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, $"{what} didn't happen within 10 seconds.");
                yield return null;
            }
        }

        private static IEnumerator Resolve<T>(Future<T> resolve)
        {
            yield return WaitUntil(() => resolve.IsDone, "The lookup");
        }

        private static IEnumerator ReadString(StringPropertyHandle property, Action<string> onRead)
        {
            Future<string> read = property.GetValueAsync();
            yield return WaitUntil(() => read.IsDone, "The read");
            Assert.AreEqual(FutureStatus.Succeeded, read.Status, read.Exception?.Message);
            onRead(read.Result);
        }

        // Holds the producer until the returned gate is set.
        private static ManualResetEventSlim HoldProducer()
        {
            var started = new ManualResetEventSlim(false);
            var gate = new ManualResetEventSlim(false);
            ServerGate.Hold(started, gate);
            Assert.IsTrue(started.Wait(2000), "The producer should have picked the job up.");
            return gate;
        }

        [UnityTest]
        public IEnumerator ListItem_Resolves_AndKeepsItsInstance_WhenTheListChanges()
        {
            FileHandle file = null;
            ViewModelInstanceHandle root = null;
            yield return LoadBound(TestAssetReferences.riv_db_list_test, (f, s, r) => { file = f; root = r; });
            ListPropertyHandle items = root.GetListProperty("items");
            items.Clear();
            foreach (string text in new[] { "first", "second" })
            {
                ViewModelInstanceHandle item = Track(file.GetViewModel("TodoItem").InstantiateBlank());
                item.GetStringProperty("text").SetValue(text);
                items.Add(item);
            }

            ViewModelInstanceHandle atZero = Track(items.GetInstanceAt(0));
            Assert.AreEqual(HandleStatus.Pending, atZero.Status);
            // Queued after the lookup, so it doesn't change what the handle found.
            items.RemoveAt(0);
            ViewModelInstanceHandle atZeroNow = Track(items.GetInstanceAt(0));
            Future<ViewModelInstanceHandle> resolve = atZero.ResolveAsync();
            yield return Resolve(resolve);

            Assert.AreEqual(FutureStatus.Succeeded, resolve.Status, resolve.Exception?.Message);
            Assert.AreSame(atZero, resolve.Result);
            Assert.AreEqual(HandleStatus.Valid, atZero.Status);
            Assert.IsNull(atZero.Error);
            string found = null;
            string now = null;
            yield return ReadString(atZero.GetStringProperty("text"), v => found = v);
            yield return ReadString(atZeroNow.GetStringProperty("text"), v => now = v);
            Assert.AreEqual("first", found, "It keeps the instance that was there when it ran.");
            Assert.AreEqual("second", now, "A lookup after the removal finds the next one.");
        }

        [UnityTest]
        public IEnumerator ListItem_PastTheEnd_IsInvalid_WritesAreSkipped_AndItsReportedOnce()
        {
            ViewModelInstanceHandle root = null;
            yield return LoadBound(TestAssetReferences.riv_db_list_test, (f, s, r) => root = r);
            ListPropertyHandle items = root.GetListProperty("items");
            items.Clear();

            ViewModelInstanceHandle missing = Track(items.GetInstanceAt(3));
            missing.GetStringProperty("text").SetValue("skipped");
            missing.GetStringProperty("text").SetValue("skipped again");
            ViewModelInstanceHandle nested = Track(missing.GetViewModelInstanceProperty("anything"));
            Future<ViewModelInstanceHandle> resolve = missing.ResolveAsync();
            Future<ViewModelInstanceHandle> resolveNested = nested.ResolveAsync();
            yield return Resolve(resolve);
            yield return Resolve(resolveNested);

            Assert.AreEqual(HandleStatus.Invalid, missing.Status);
            Assert.AreEqual(RiveErrorCode.IndexOutOfRange, missing.Error.Code);
            Assert.AreSame(missing.Error, resolve.Exception);
            Assert.AreSame(missing.Error, nested.Error, "Built on a failed lookup, it takes that error.");
            Assert.AreSame(missing.Error, missing.GetStringProperty("text").Error);
            Assert.AreEqual(1, m_mockLogger.LoggedWarnings.FindAll(w => w.Contains("out of range")).Count,
                "The failed lookup is reported once, not again for what was queued on it.");
            Assert.AreEqual(0, m_mockLogger.LoggedExceptions.Count);
        }

        [UnityTest]
        public IEnumerator DefaultInstance_Resolves_AndAnUnboundStateMachineHasNone()
        {
            FileHandle file = null;
            yield return LoadFile(TestAssetReferences.riv_asset_databinding_test, f => file = f);
            ViewModelInstanceHandle made = Track(file.GetViewModel("PersonViewModel").Instantiate());
            StateMachineHandle unbound = Track(Track(file.InstantiateArtboard()).InstantiateStateMachine());
            ViewModelInstanceHandle none = Track(unbound.GetViewModelInstance());
            Assert.AreEqual(HandleStatus.Pending, made.Status);
            Assert.AreEqual(HandleStatus.Pending, none.Status);

            Future<ViewModelInstanceHandle> resolveMade = made.ResolveAsync();
            Future<ViewModelInstanceHandle> resolveNone = none.ResolveAsync();
            yield return Resolve(resolveMade);
            yield return Resolve(resolveNone);

            Assert.AreEqual(FutureStatus.Succeeded, resolveMade.Status);
            Assert.AreEqual(HandleStatus.Valid, made.Status);
            Assert.AreEqual(FutureStatus.Failed, resolveNone.Status);
            Assert.AreEqual(HandleStatus.Invalid, none.Status);
            Assert.AreEqual(RiveErrorCode.ViewModelInstanceNotFound, none.Error.Code);
            Assert.IsTrue(m_mockLogger.LoggedWarningsContains("has no view model instance bound"));
        }

        [NeedsRiveThread]
        [UnityTest]
        public IEnumerator ResolveAsync_GivesEachCallerItsOwnFuture()
        {
            FileHandle file = null;
            yield return LoadFile(TestAssetReferences.riv_asset_databinding_test, f => file = f);

            ViewModelInstanceHandle instance;
            Future<ViewModelInstanceHandle> first;
            Future<ViewModelInstanceHandle> second;
            ManualResetEventSlim gate = HoldProducer();
            try
            {
                instance = Track(file.GetViewModel("PersonViewModel").Instantiate());
                first = instance.ResolveAsync();
                second = instance.ResolveAsync();
                Assert.IsFalse(first.IsDone);
                Assert.IsFalse(second.IsDone);
            }
            finally
            {
                gate.Set();
            }

            // Each can be awaited once on its own.
            ViewModelInstanceHandle viaFirst = null;
            ViewModelInstanceHandle viaSecond = null;
            yield return AwaitInTask(first, h => viaFirst = h);
            yield return AwaitInTask(second, h => viaSecond = h);
            Assert.AreSame(instance, viaFirst);
            Assert.AreSame(instance, viaSecond);

            // Once it's valid, a new one is done straight away.
            Future<ViewModelInstanceHandle> later = instance.ResolveAsync();
            Assert.IsTrue(later.IsDone);
            Assert.AreSame(instance, later.Result);
        }

        private static IEnumerator AwaitInTask(Future<ViewModelInstanceHandle> future, Action<ViewModelInstanceHandle> onResult)
        {
            System.Threading.Tasks.Task<ViewModelInstanceHandle> task = Await(future);
            yield return WaitUntil(() => task.IsCompleted, "The await");
            Assert.IsTrue(task.Status == System.Threading.Tasks.TaskStatus.RanToCompletion, task.Exception?.Message);
            onResult(task.Result);
        }

        private static async System.Threading.Tasks.Task<ViewModelInstanceHandle> Await(Future<ViewModelInstanceHandle> future)
        {
            return await future;
        }

        [NeedsRiveThread]
        [UnityTest]
        public IEnumerator DisposingWhilePending_FailsWaiters_AndReleasesTheInstance()
        {
            FileHandle file = null;
            yield return LoadFile(TestAssetReferences.riv_asset_databinding_test, f => file = f);

            ViewModelInstanceHandle instance;
            Future<ViewModelInstanceHandle> resolve;
            ManualResetEventSlim gate = HoldProducer();
            try
            {
                instance = file.GetViewModel("PersonViewModel").Instantiate();
                resolve = instance.ResolveAsync();
                instance.Dispose();

                Assert.AreEqual(HandleStatus.Disposed, instance.Status);
                Assert.IsTrue(resolve.IsDone, "Disposing finishes the waiters straight away.");
                Assert.AreEqual(RiveErrorCode.ResourceDisposed, ((RiveException)resolve.Exception).Code);
            }
            finally
            {
                gate.Set();
            }
            yield return WaitUntil(() => CommandTransport.PendingRequestsForTests == 0, "The queue draining");
            yield return null;

            Assert.AreEqual(HandleStatus.Disposed, instance.Status, "The lookup landing later doesn't change it.");
            Assert.AreEqual(0, ViewModelInstanceNative.RefCountForTests(instance.Native), "The instance made for it is let go.");
            Future<ViewModelInstanceHandle> after = instance.ResolveAsync();
            Assert.AreEqual(FutureStatus.Failed, after.Status);
            Assert.AreEqual(RiveErrorCode.ResourceDisposed, ((RiveException)after.Exception).Code);
        }

        [UnityTest]
        public IEnumerator PropertyResolveAsync_ChecksThePath_WhenNothingElseHas()
        {
            ViewModelInstanceHandle person = null;
            yield return LoadBound(TestAssetReferences.riv_asset_databinding_test, (f, s, r) => person = r);
            NumberPropertyHandle age = person.GetNumberProperty("age");
            NumberPropertyHandle missing = person.GetNumberProperty("missing");
            Assert.AreEqual(HandleStatus.Pending, age.Status);

            Future<NumberPropertyHandle> resolveAge = age.ResolveAsync();
            Future<NumberPropertyHandle> resolveMissing = missing.ResolveAsync();
            yield return Resolve(resolveAge);
            yield return Resolve(resolveMissing);

            Assert.AreEqual(FutureStatus.Succeeded, resolveAge.Status);
            Assert.AreSame(age, resolveAge.Result);
            Assert.AreEqual(HandleStatus.Valid, age.Status);
            Assert.AreEqual(HandleStatus.Invalid, missing.Status);
            Assert.AreEqual(RiveErrorCode.PropertyNotFound, missing.Error.Code);
            Assert.AreEqual(1, m_mockLogger.LoggedWarnings.FindAll(w => w.Contains("'missing'")).Count);

            // Disposing the instance disposes its properties.
            ViewModelInstanceHandle loose = Track(person.GetViewModelInstanceProperty("favDrink"));
            StringPropertyHandle name = loose.GetStringProperty("name");
            loose.Dispose();
            Assert.AreEqual(HandleStatus.Disposed, name.Status);
        }
    }
}
