using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Rive.Components;
using Rive.Tests.Utils;
using Rive.Utils;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rive.Tests
{
    /// <summary>
    /// The panel's ThreadingMode picks the widget's API: plain objects on the
    /// main thread, handles in the background. Load and LoadAsync only change
    /// how the caller waits.
    /// </summary>
    public class ThreadingModeTests
    {
        private TestAssetLoadingManager m_assetLoader;
        private MockLogger m_mockLogger;
        private readonly List<GameObject> m_objects = new List<GameObject>();
        private readonly List<System.IDisposable> m_toDispose = new List<System.IDisposable>();

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
            for (int i = 0; i < m_objects.Count; i++)
            {
                if (m_objects[i] != null)
                {
                    Object.Destroy(m_objects[i]);
                }
            }
            m_objects.Clear();
            for (int i = 0; i < m_toDispose.Count; i++)
            {
                m_toDispose[i]?.Dispose();
            }
            m_toDispose.Clear();
            m_assetLoader.UnloadAllAssets();
        }

        private IEnumerator LoadAsset(System.Action<Asset> onLoaded)
        {
            return m_assetLoader.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_asset_databinding_test,
                onLoaded,
                () => Assert.Fail("Failed to load the test asset"));
        }

        private RivePanel CreatePanel(ThreadingMode mode)
        {
            RivePanel panel = RivePanelTestUtils.CreatePanel();
            m_objects.Add(panel.gameObject);
            panel.ThreadingMode = mode;
            return panel;
        }

        private RiveWidget CreateWidget(RivePanel panel)
        {
            RiveWidget widget = RivePanelTestUtils.CreateWidget<RiveWidget>();
            widget.transform.SetParent(panel.WidgetContainer, false);
            RivePanelTestUtils.MakeWidgetFillPanel(widget);
            return widget;
        }

        private static IEnumerator WaitUntilLoaded(RiveWidget widget)
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (widget.Status != WidgetStatus.Loaded)
            {
                Assert.AreNotEqual(WidgetStatus.Error, widget.Status, "The load failed.");
                Assert.Less(Time.realtimeSinceStartup, deadline, "The widget didn't load within 10 seconds.");
                yield return null;
            }
        }

        private static IEnumerator WaitFor<T>(Future<T> operation)
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!operation.IsDone)
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, "It didn't finish within 10 seconds.");
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator MainThreadPanel_GivesPlainObjects()
        {
            Asset asset = null;
            yield return LoadAsset(a => asset = a);
            RiveWidget widget = CreateWidget(CreatePanel(ThreadingMode.MainThread));

            Future load = widget.LoadAsync(asset);

            Assert.IsTrue(load.IsDone, "In a MainThread panel the load has finished when LoadAsync returns.");
            Assert.AreEqual(WidgetStatus.Loaded, widget.Status);
            Assert.IsNotNull(widget.File);
            Assert.IsNotNull(widget.Artboard);
            Assert.IsNotNull(widget.StateMachine);
            Assert.IsNull(widget.FileHandle);
            Assert.IsNull(widget.ArtboardHandle);
            Assert.IsNull(widget.StateMachineHandle);
        }

        [NeedsRiveThread]
        [UnityTest]
        public IEnumerator BackgroundPanel_GivesHandles()
        {
            Asset asset = null;
            yield return LoadAsset(a => asset = a);
            RiveWidget widget = CreateWidget(CreatePanel(ThreadingMode.BackgroundThread));

            widget.Load(asset);
            Assert.AreEqual(WidgetStatus.Loading, widget.Status,
                "In a BackgroundThread panel Load returns before the load finishes.");
            yield return WaitUntilLoaded(widget);

            Assert.IsNotNull(widget.FileHandle);
            Assert.IsNotNull(widget.ArtboardHandle);
            Assert.IsNotNull(widget.StateMachineHandle);
            Assert.AreEqual(widget.FileHandle.GetArtboardNamesAsync().Result[0], widget.ArtboardHandle.GetNameAsync().Result);
            Assert.AreEqual(widget.ArtboardHandle.GetStateMachineNamesAsync().Result[0], widget.StateMachineHandle.GetNameAsync().Result);

            Assert.IsNull(widget.StateMachine, "The plain objects stay null in a BackgroundThread panel.");
            Assert.IsNull(widget.File);
            Assert.IsTrue(m_mockLogger.LoggedWarningsContains("BackgroundThread panel"),
                "Reaching for a plain object should say to use the handles.");
        }

        [UnityTest]
        public IEnumerator ThreadingMode_CantChangeWithLoadedWidgets()
        {
            Asset asset = null;
            yield return LoadAsset(a => asset = a);
            RivePanel panel = CreatePanel(ThreadingMode.MainThread);
            RiveWidget widget = CreateWidget(panel);
            widget.Load(asset);
            Assert.AreEqual(WidgetStatus.Loaded, widget.Status);

            panel.ThreadingMode = ThreadingMode.BackgroundThread;

            Assert.AreEqual(ThreadingMode.MainThread, panel.ThreadingMode);
            Assert.IsTrue(m_mockLogger.LoggedWarningsContains("can't change"));
        }

        [UnityTest]
        public IEnumerator FileHandle_InAMainThreadPanel_GivesItsFile()
        {
            Asset asset = null;
            yield return LoadAsset(a => asset = a);
            Future<FileHandle> loading = FileHandle.LoadAsync(asset);
            yield return WaitFor(loading);
            FileHandle file = loading.Result;
            m_toDispose.Add(file);
            RiveWidget widget = CreateWidget(CreatePanel(ThreadingMode.MainThread));

            widget.Load(file);

            Assert.AreEqual(WidgetStatus.Loaded, widget.Status);
            Assert.AreSame(file.File, widget.File, "The widget should use the file the handle holds.");
            Assert.IsNull(widget.FileHandle);
        }

        [UnityTest]
        public IEnumerator File_InABackgroundPanel_GivesAFileHandle()
        {
            Asset asset = null;
            yield return LoadAsset(a => asset = a);
            File file = File.Load(asset);
            m_toDispose.Add(file);
            RiveWidget widget = CreateWidget(CreatePanel(ThreadingMode.BackgroundThread));

            Future load = widget.LoadAsync(file);
            yield return WaitFor(load);

            Assert.AreEqual(WidgetStatus.Loaded, widget.Status);
            FileHandle view = widget.FileHandle;
            Assert.IsNotNull(view);
            var names = new List<string>();
            for (uint i = 0; i < file.ArtboardCount; i++)
            {
                names.Add(file.ArtboardName(i));
            }
            CollectionAssert.AreEqual(names, view.GetArtboardNamesAsync().Result);

            view.Dispose();
            Assert.IsFalse(view.IsDisposed, "The widget owns its handles, so Dispose is ignored.");
            Assert.IsTrue(m_mockLogger.LoggedWarningsContains("belongs to a widget"));
        }

        [UnityTest]
        public IEnumerator Reloading_ReleasesTheOldHandles()
        {
            Asset asset = null;
            yield return LoadAsset(a => asset = a);
            RiveWidget widget = CreateWidget(CreatePanel(ThreadingMode.BackgroundThread));
            yield return WaitFor(widget.LoadAsync(asset));
            ArtboardHandle first = widget.ArtboardHandle;

            yield return WaitFor(widget.LoadAsync(asset));

            Assert.IsTrue(first.IsDisposed, "The previous load's handles should be released.");
            Assert.IsNotNull(widget.ArtboardHandle);
            Assert.AreNotSame(first, widget.ArtboardHandle);
        }

        [UnityTest]
        public IEnumerator MovingToABackgroundPanel_ReloadsWithHandles()
        {
            Asset asset = null;
            yield return LoadAsset(a => asset = a);
            RiveWidget widget = CreateWidget(CreatePanel(ThreadingMode.MainThread));
            widget.Load(asset);
            Assert.IsNotNull(widget.StateMachine);

            RivePanel background = CreatePanel(ThreadingMode.BackgroundThread);
            widget.transform.SetParent(background.WidgetContainer, false);

            float deadline = Time.realtimeSinceStartup + 10f;
            while (widget.StateMachineHandle == null)
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, "The widget didn't reload within 10 seconds.");
                yield return null;
            }
            Assert.AreEqual(WidgetStatus.Loaded, widget.Status);
        }

        // A render object is made with the settings from when an async load
        // started, so changes made while it was out have to be applied after.
        [NeedsRiveThread]
        [UnityTest]
        public IEnumerator DisplaySettings_ChangedDuringABackgroundLoad_Apply()
        {
            Asset asset = null;
            yield return LoadAsset(a => asset = a);
            RiveWidget widget = CreateWidget(CreatePanel(ThreadingMode.BackgroundThread));
            widget.Fit = Fit.Contain;
            widget.Alignment = Alignment.Center;

            // A File starts the load straight away, so it has the old settings.
            File file = File.Load(asset);
            m_toDispose.Add(file);
            Future load = widget.LoadAsync(file);
            Assert.AreEqual(WidgetStatus.Loading, widget.Status);
            widget.Fit = Fit.Fill;
            widget.Alignment = Alignment.TopLeft;
            yield return WaitFor(load);

            Assert.AreEqual(Fit.Fill, widget.RenderObjectWithArtboard.Fit);
            Assert.AreEqual(Alignment.TopLeft, widget.RenderObjectWithArtboard.Alignment);
        }

        private static IEnumerator WaitFor(Future operation)
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!operation.IsDone)
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, "It didn't finish within 10 seconds.");
                yield return null;
            }
        }
    }
}
