using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Rive.Tests.Utils;
using Rive.Utils;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rive.Tests
{
    /// <summary>
    /// ArtboardHandle and StateMachineHandle, mirroring <see cref="ArtboardTests"/> for what the
    /// handles have.
    /// </summary>
    public class ArtboardHandleTests
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

        private IEnumerator Load(string path, Action<Asset, FileHandle> onLoaded)
        {
            Asset asset = null;
            yield return m_assetLoader.LoadAssetCoroutine<Asset>(path, a => asset = a, () => Assert.Fail($"Failed to load {path}"));
            Future<FileHandle> load = FileHandle.LoadAsync(asset);
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!load.IsDone)
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, "The load didn't finish within 10 seconds.");
                yield return null;
            }
            onLoaded(asset, Track(load.Result));
        }

        [UnityTest]
        public IEnumerator InstantiateStateMachine_ByName_GivesThatStateMachine()
        {
            FileHandle file = null;
            yield return Load(TestAssetReferences.riv_asset_databinding_test, (a, f) => file = f);
            ArtboardHandle artboard = Track(file.InstantiateArtboard());

            foreach (string name in artboard.GetStateMachineNamesAsync().Result)
            {
                StateMachineHandle stateMachine = Track(artboard.InstantiateStateMachine(name));
                Assert.AreEqual(name, stateMachine.GetNameAsync().Result);
                Assert.AreSame(artboard, stateMachine.Artboard);
            }
        }

        [UnityTest]
        public IEnumerator DefaultViewModel_MatchesThePlainArtboard_ForEveryArtboard()
        {
            string[] paths =
            {
                TestAssetReferences.riv_asset_databinding_test,
                TestAssetReferences.riv_global_debug_no_main_viewmodel,
                TestAssetReferences.riv_sophiaHud,
            };
            foreach (string path in paths)
            {
                Asset asset = null;
                FileHandle file = null;
                yield return Load(path, (a, f) => { asset = a; file = f; });
                using (File plain = File.Load(asset))
                {
                    IReadOnlyList<string> artboardNames = file.GetArtboardNamesAsync().Result;
                    for (int i = 0; i < artboardNames.Count; i++)
                    {
                        using (Artboard plainArtboard = plain.Artboard((uint)i))
                        {
                            ArtboardHandle artboard = Track(file.InstantiateArtboard(artboardNames[i]));
                            ViewModelHandle viewModel = artboard.GetDefaultViewModel();
                            Assert.AreEqual(plainArtboard.HasDefaultViewModel, viewModel != null,
                                $"'{artboardNames[i]}' in {path}");
                            if (viewModel != null)
                            {
                                Assert.AreEqual(plainArtboard.DefaultViewModel.Name, viewModel.GetNameAsync().Result);
                            }
                        }
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator Size_MatchesThePlainArtboard_AndCanBeSet()
        {
            Asset asset = null;
            FileHandle file = null;
            yield return Load(TestAssetReferences.riv_asset_databinding_test, (a, f) => { asset = a; file = f; });
            Size expected;
            using (File plain = File.Load(asset))
            using (Artboard plainArtboard = plain.Artboard(0))
            {
                expected = plainArtboard.Size;
            }
            ArtboardHandle artboard = Track(file.InstantiateArtboard());

            Future<Size> original = artboard.GetSizeAsync();
            artboard.SetSize(new Size(321f, 123f));
            Future<Size> changed = artboard.GetSizeAsync();
            while (!changed.IsDone)
            {
                yield return null;
            }

            Assert.AreEqual(expected, original.Result);
            Assert.AreEqual(new Size(321f, 123f), changed.Result);
        }

        [UnityTest]
        public IEnumerator Disposing_InAnyOrder_DoesNotCrash()
        {
            FileHandle file = null;
            yield return Load(TestAssetReferences.riv_asset_databinding_test, (a, f) => file = f);
            ArtboardHandle artboard = file.InstantiateArtboard();
            StateMachineHandle stateMachine = artboard.InstantiateStateMachine();
            stateMachine.Advance(1f / 60f);

            artboard.Dispose();
            stateMachine.Advance(1f / 60f);
            stateMachine.Dispose();
            yield return null;

            Assert.IsTrue(artboard.IsDisposed && stateMachine.IsDisposed);
            Assert.AreEqual(0, m_mockLogger.LoggedExceptions.Count);
        }

        [UnityTest]
        public IEnumerator DroppedHandles_AreCollectedWithoutCrashing()
        {
            FileHandle file = null;
            yield return Load(TestAssetReferences.riv_asset_databinding_test, (a, f) => file = f);
            MakeAndDrop(file);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            yield return null;
            yield return null;

            Assert.AreEqual(0, m_mockLogger.LoggedExceptions.Count);
        }

        // Its own method so nothing on the stack keeps the handles alive.
        private static void MakeAndDrop(FileHandle file)
        {
            for (int i = 0; i < 5; i++)
            {
                ArtboardHandle artboard = file.InstantiateArtboard();
                StateMachineHandle stateMachine = artboard.InstantiateStateMachine();
                stateMachine.BindViewModelInstanceAsync(null);
                stateMachine.Advance(1f / 60f);
            }
        }
    }
}
