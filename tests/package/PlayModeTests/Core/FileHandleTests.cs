using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    /// FileHandle: loading without waiting, the names cached at load, and
    /// sharing the loaded file with File.
    /// </summary>
    public class FileHandleTests
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
            for (int i = 0; i < m_toDispose.Count; i++)
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

        private IEnumerator LoadAsset(Action<Asset> onLoaded)
        {
            return m_assetLoader.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_global_variables_test,
                onLoaded,
                () => Assert.Fail("Failed to load the test asset"));
        }

        private static IEnumerator WaitFor<T>(Future<T> operation)
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!operation.IsDone)
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, "The load didn't finish within 10 seconds.");
                yield return null;
            }
        }

        private IEnumerator LoadHandle(Asset asset, Action<FileHandle> onLoaded)
        {
            Future<FileHandle> load = FileHandle.LoadAsync(asset);
            yield return WaitFor(load);
            Assert.AreEqual(FutureStatus.Succeeded, load.Status, "The load should succeed.");
            onLoaded(Track(load.Result));
        }

        [UnityTest]
        public IEnumerator LoadAsync_NamesMatchTheLoadedFile()
        {
            Asset asset = null;
            yield return LoadAsset(a => asset = a);
            FileHandle handle = null;
            yield return LoadHandle(asset, h => handle = h);
            File file = Track(File.Load(asset));

            var artboardNames = new List<string>();
            for (uint i = 0; i < file.ArtboardCount; i++)
            {
                artboardNames.Add(file.ArtboardName(i));
            }
            CollectionAssert.AreEqual(artboardNames, handle.GetArtboardNamesAsync().Result);

            for (int i = 0; i < artboardNames.Count; i++)
            {
                using (Artboard artboard = file.Artboard(artboardNames[i]))
                {
                    var stateMachineNames = new List<string>();
                    for (uint j = 0; j < artboard.StateMachineCount; j++)
                    {
                        stateMachineNames.Add(artboard.StateMachineName(j));
                    }
                    // Kept for ArtboardHandle, not exposed on the file.
                    CollectionAssert.AreEqual(stateMachineNames, handle.Contents.Artboards[i].StateMachineNames,
                        $"State machines of '{artboardNames[i]}'");
                }
            }

            var viewModelNames = new List<string>();
            for (int i = 0; i < file.ViewModelCount; i++)
            {
                ViewModel viewModel = file.GetViewModelAtIndex(i);
                viewModelNames.Add(viewModel.Name);
                CollectionAssert.AreEqual(viewModel.InstanceNames, handle.Contents.ViewModels[i].InstanceNames,
                    $"Instances of '{viewModel.Name}'");
            }
            CollectionAssert.AreEqual(viewModelNames, handle.GetViewModelNamesAsync().Result);
            CollectionAssert.AreEqual(file.GlobalViewModelNames, handle.GetGlobalViewModelNamesAsync().Result);
            Assert.IsNotEmpty(handle.GetGlobalViewModelNamesAsync().Result, "The test file has global view models.");
        }

        [NeedsRiveThread]
        [UnityTest]
        public IEnumerator LoadAsync_DoesNotWaitOnTheMainThread()
        {
            Asset asset = null;
            yield return LoadAsset(a => asset = a);

            var waits = new List<string>();
            CommandTransport.MainThreadWaitsForTests = waits;
            Future<FileHandle> load;
            try
            {
                load = FileHandle.LoadAsync(asset, new Dictionary<uint, OutOfBandAsset>());
                yield return WaitFor(load);
            }
            finally
            {
                CommandTransport.MainThreadWaitsForTests = null;
            }
            Track(load.Result);
            CollectionAssert.IsEmpty(waits, "Loading a handle shouldn't wait on the producer.");
        }

        [NeedsRiveThread]
        [UnityTest]
        public IEnumerator Instantiating_ReturnsAtOnce_AndWorksInOrder()
        {
            Asset asset = null;
            yield return LoadAsset(a => asset = a);
            FileHandle file = null;
            yield return LoadHandle(asset, h => file = h);

            var waits = new List<string>();
            CommandTransport.MainThreadWaitsForTests = waits;
            ArtboardHandle artboard;
            StateMachineHandle stateMachine;
            Future<Size> size;
            try
            {
                artboard = Track(file.InstantiateArtboard());
                stateMachine = Track(artboard.InstantiateStateMachine());
                stateMachine.Advance(1f / 60f);
                artboard.SetSize(new Size(123f, 45f));
                size = artboard.GetSizeAsync();
            }
            finally
            {
                CommandTransport.MainThreadWaitsForTests = null;
            }
            CollectionAssert.IsEmpty(waits, "Nothing on a handle should wait.");
            Assert.AreEqual(file.GetArtboardNamesAsync().Result[0], artboard.GetNameAsync().Result);

            yield return WaitFor(size);
            Assert.AreEqual(new Size(123f, 45f), size.Result, "Work queued on the handles should run in order.");
        }

        [UnityTest]
        public IEnumerator DefaultStateMachine_MatchesTheSyncDefault()
        {
            Asset asset = null;
            yield return LoadAsset(a => asset = a);
            FileHandle file = null;
            yield return LoadHandle(asset, h => file = h);
            File syncFile = Track(File.Load(asset));

            ArtboardHandle artboard = Track(file.InstantiateArtboard());
            StateMachineHandle stateMachine = Track(artboard.InstantiateStateMachine());

            using (Artboard syncArtboard = syncFile.Artboard(0))
            using (StateMachine syncStateMachine = syncArtboard.StateMachine())
            {
                Assert.AreEqual(syncStateMachine.Name, stateMachine.GetNameAsync().Result);
            }
        }

        [UnityTest]
        public IEnumerator UnknownNames_ReturnNullWithAWarning()
        {
            Asset asset = null;
            yield return LoadAsset(a => asset = a);
            FileHandle file = null;
            yield return LoadHandle(asset, h => file = h);

            Assert.IsNull(file.InstantiateArtboard("No such artboard"));
            Assert.IsTrue(m_mockLogger.LoggedWarningsContains("no artboard named 'No such artboard'"));

            ArtboardHandle artboard = Track(file.InstantiateArtboard());
            Assert.IsNull(artboard.InstantiateStateMachine("No such state machine"));
            Assert.IsTrue(m_mockLogger.LoggedWarningsContains("no state machine named 'No such state machine'"));
        }

        [UnityTest]
        public IEnumerator LoadAsync_UnreadableBytes_FailsWithLoadFailed()
        {
            Future<FileHandle> load = FileHandle.LoadAsync(new byte[] { 0, 1, 2, 3 });
            yield return WaitFor(load);

            Assert.AreEqual(FutureStatus.Failed, load.Status);
            var error = load.Exception as RiveException;
            Assert.IsNotNull(error, "The failure should be a RiveException.");
            Assert.AreEqual(RiveErrorCode.LoadFailed, error.Code);
        }

        [UnityTest]
        public IEnumerator SameAssetAsFileAndHandle_SharesTheLoadedFile()
        {
            Asset asset = null;
            yield return LoadAsset(a => asset = a);
            File file = File.Load(asset);
            FileHandle handle = null;
            yield return LoadHandle(asset, h => handle = h);

            Assert.AreSame(file, handle.File, "A handle from the same Rive Asset should share the loaded file.");

            file.Dispose();
            Assert.IsFalse(handle.IsDisposed);
            Assert.IsTrue(NativeFileInterface.IsRiveFileValid(handle.NativeFile),
                "Disposing the File shouldn't free the file the handle still holds.");

            handle.Dispose();
            Assert.IsTrue(handle.IsDisposed);
        }

        [UnityTest]
        public IEnumerator LoadAsync_FromBytes_MatchesTheAsset()
        {
            Asset asset = null;
            yield return LoadAsset(a => asset = a);
            FileHandle fromAsset = null;
            yield return LoadHandle(asset, h => fromAsset = h);

            Future<FileHandle> load = FileHandle.LoadAsync(asset.Bytes);
            yield return WaitFor(load);
            FileHandle fromBytes = Track(load.Result);

            CollectionAssert.AreEqual(fromAsset.GetArtboardNamesAsync().Result, fromBytes.GetArtboardNamesAsync().Result);
            CollectionAssert.AreEqual(fromAsset.GetViewModelNamesAsync().Result, fromBytes.GetViewModelNamesAsync().Result);
        }

        [UnityTest]
        public IEnumerator Dispose_ReleasesTheNativeFile()
        {
            Asset asset = null;
            yield return LoadAsset(a => asset = a);
            Future<FileHandle> load = FileHandle.LoadAsync(asset);
            yield return WaitFor(load);
            FileHandle handle = load.Result;
            Assert.IsTrue(NativeFileInterface.IsRiveFileValid(handle.NativeFile));

            handle.Dispose();
            yield return null;

            Assert.IsFalse(NativeFileInterface.IsRiveFileValid(handle.NativeFile),
                "With nothing else holding it, disposing the handle frees the file.");
        }

        [UnityTest]
        public IEnumerator Dispose_ReleasesTheOutOfBandAssetsTheLoadLoaded()
        {
            Asset asset = null;
            yield return m_assetLoader.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_stormtrooper_bird, a => asset = a, () => Assert.Fail("Failed to load the asset"));
            OutOfBandAsset image = asset.EmbeddedAssets.Select(e => e.OutOfBandAsset).FirstOrDefault(a => a != null);
            Assert.IsNotNull(image, "The file references an out-of-band image.");

            Future<FileHandle> load = FileHandle.LoadAsync(asset);
            yield return WaitFor(load);
            Assert.Greater(image.RefCount(), 0, "Loading the file loads the image.");

            load.Result.Dispose();
            yield return null;

            Assert.AreEqual(0, image.RefCount(), "Disposing the file lets the image go.");
        }

        [UnityTest]
        public IEnumerator CffFont_LoadsAndAdvances()
        {
            Asset asset = null;
            yield return m_assetLoader.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_cff_font_test, a => asset = a, () => Assert.Fail("Failed to load the CFF asset"));
            FileHandle file = null;
            yield return LoadHandle(asset, h => file = h);

            StateMachineHandle stateMachine = Track(Track(file.InstantiateArtboard()).InstantiateStateMachine());
            stateMachine.Advance(0f);
            Future<Size> size = stateMachine.Artboard.GetSizeAsync();
            yield return WaitFor(size);

            Assert.AreEqual(FutureStatus.Succeeded, size.Status);
        }

        [UnityTest]
        public IEnumerator DisposedFile_LogsErrors()
        {
            Asset asset = null;
            yield return LoadAsset(a => asset = a);
            Future<FileHandle> load = FileHandle.LoadAsync(asset);
            yield return WaitFor(load);
            FileHandle file = load.Result;
            ViewModelHandle viewModel = file.GetViewModel(file.GetViewModelNamesAsync().Result[0]);
            file.Dispose();

            Assert.IsNull(file.InstantiateArtboard());
            Assert.IsNull(file.GetBindableArtboard(file.GetArtboardNamesAsync().Result[0]));
            Assert.IsNull(viewModel.Instantiate(), "A view model from a disposed file can't make instances.");
            Assert.AreEqual(3, m_mockLogger.LoggedErrors.FindAll(e => e.Contains("the file has been disposed")).Count);
        }

        [UnityTest]
        public IEnumerator LoadAsync_WithAnAssetMap_IsItsOwnFile()
        {
            Asset asset = null;
            yield return LoadAsset(a => asset = a);
            File file = Track(File.Load(asset));

            Future<FileHandle> load = FileHandle.LoadAsync(asset, new Dictionary<uint, OutOfBandAsset>());
            yield return WaitFor(load);
            FileHandle handle = Track(load.Result);

            Assert.AreNotSame(file, handle.File, "Overriding assets loads a separate file.");
            CollectionAssert.IsNotEmpty(handle.GetArtboardNamesAsync().Result);
        }
    }
}
