using System;
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
    /// RiveWidget in a BackgroundThread panel, mirroring the parts of <see cref="RiveWidgetTests"/> that
    /// don't depend on inputs or the plain objects.
    /// </summary>
    public class RiveWidgetHandleTests
    {
        private TestAssetLoadingManager m_assetLoader;
        private MockLogger m_mockLogger;
        private RivePanel m_panel;
        private RiveWidget m_widget;
        private readonly List<IDisposable> m_toDispose = new List<IDisposable>();

        [SetUp]
        public void SetUp()
        {
            m_mockLogger = new MockLogger();
            DebugLogger.Instance = m_mockLogger;
            m_assetLoader = new TestAssetLoadingManager();
            m_panel = RivePanelTestUtils.CreatePanel();
            m_panel.ThreadingMode = ThreadingMode.BackgroundThread;
            m_widget = RivePanelTestUtils.CreateWidget<RiveWidget>();
            m_widget.transform.SetParent(m_panel.WidgetContainer, false);
            RivePanelTestUtils.MakeWidgetFillPanel(m_widget);
        }

        [TearDown]
        public void TearDown()
        {
            if (m_panel != null)
            {
                UnityEngine.Object.Destroy(m_panel.gameObject);
            }
            for (int i = m_toDispose.Count - 1; i >= 0; i--)
            {
                m_toDispose[i]?.Dispose();
            }
            m_toDispose.Clear();
            m_assetLoader.UnloadAllAssets();
        }

        private IEnumerator LoadAsset(string path, Action<Asset> onLoaded)
        {
            return m_assetLoader.LoadAssetCoroutine<Asset>(path, onLoaded, () => Assert.Fail($"Failed to load {path}"));
        }

        private IEnumerator WaitUntilLoaded()
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (m_widget.Status != WidgetStatus.Loaded)
            {
                Assert.AreNotEqual(WidgetStatus.Error, m_widget.Status, "The load failed.");
                Assert.Less(Time.realtimeSinceStartup, deadline, "The widget didn't load within 10 seconds.");
                yield return null;
            }
        }

        [NeedsRiveThread]
        [UnityTest]
        public IEnumerator Reload_WithoutNames_UsesTheDefaults([Values] bool fromFile)
        {
            Asset asset = null;
            yield return LoadAsset(TestAssetReferences.riv_sophiaHud, a => asset = a);
            File file = null;
            if (fromFile)
            {
                file = File.Load(asset);
                m_toDispose.Add(file);
            }

            if (fromFile) m_widget.Load(file, "DASHBOARD", "State Machine 1");
            else m_widget.Load(asset, "DASHBOARD", "State Machine 1");
            yield return WaitUntilLoaded();
            Assert.AreEqual("DASHBOARD", m_widget.ArtboardHandle.GetNameAsync().Result);
            Assert.AreEqual("State Machine 1", m_widget.StateMachineHandle.GetNameAsync().Result);

            if (fromFile) m_widget.Load(file);
            else m_widget.Load(asset);
            Assert.AreEqual(WidgetStatus.Loading, m_widget.Status);
            yield return WaitUntilLoaded();

            Assert.AreEqual("SOPHIA III HUD", m_widget.ArtboardHandle.GetNameAsync().Result);
            Assert.AreEqual("SOPHIA_III_DASHBOARD_SEQ", m_widget.StateMachineHandle.GetNameAsync().Result);
        }

        [UnityTest]
        public IEnumerator Destroying_ReleasesTheFile_OnlyIfTheWidgetLoadedIt([Values] bool fromFile)
        {
            Asset asset = null;
            yield return LoadAsset(TestAssetReferences.riv_asset_databinding_test, a => asset = a);
            File given = null;
            if (fromFile)
            {
                given = File.Load(asset);
                m_toDispose.Add(given);
                m_widget.Load(given);
            }
            else
            {
                m_widget.Load(asset);
            }
            yield return WaitUntilLoaded();
            NativeFileHandle native = m_widget.FileHandle.NativeFile;
            Assert.IsTrue(NativeFileInterface.IsRiveFileValid(native));

            UnityEngine.Object.Destroy(m_widget);
            yield return null;
            yield return null;

            Assert.AreEqual(fromFile, NativeFileInterface.IsRiveFileValid(native),
                fromFile ? "A file you gave the widget is yours to release." : "A file the widget loaded goes with it.");
        }

        [UnityTest]
        public IEnumerator OnWidgetStatusChanged_SubscribedBeforeTheLoad_SeesLoadingThenLoaded()
        {
            var subscriberObject = new GameObject("Subscriber");
            var statuses = new List<WidgetStatus>();
            var subscriber = subscriberObject.AddComponent<StatusRecorder>();
            subscriber.Widget = m_widget;
            subscriber.Seen = statuses;
            try
            {
                Asset asset = null;
                yield return LoadAsset(TestAssetReferences.riv_sophiaHud, a => asset = a);
                m_widget.Load(asset);
                yield return WaitUntilLoaded();

                CollectionAssert.Contains(statuses, WidgetStatus.Loading);
                Assert.AreEqual(WidgetStatus.Loaded, statuses[statuses.Count - 1]);
            }
            finally
            {
                UnityEngine.Object.Destroy(subscriberObject);
            }
        }

        [UnityTest]
        public IEnumerator DisplaySettings_ChangedAfterTheLoad_ReachTheRenderObject()
        {
            Asset asset = null;
            yield return LoadAsset(TestAssetReferences.riv_asset_databinding_test, a => asset = a);
            m_widget.Load(asset);
            yield return WaitUntilLoaded();

            m_widget.Fit = Fit.Cover;
            m_widget.Alignment = Alignment.BottomRight;

            Assert.AreEqual(Fit.Cover, m_widget.RenderObjectWithArtboard.Fit);
            Assert.AreEqual(Alignment.BottomRight, m_widget.RenderObjectWithArtboard.Alignment);
        }

        [UnityTest]
        public IEnumerator Speed_CanBeSetAndRead()
        {
            Asset asset = null;
            yield return LoadAsset(TestAssetReferences.riv_asset_databinding_test, a => asset = a);
            m_widget.Load(asset);
            yield return WaitUntilLoaded();

            m_widget.Speed = 0.5f;
            Assert.AreEqual(0.5f, m_widget.Speed);
        }

        private sealed class StatusRecorder : MonoBehaviour
        {
            internal RiveWidget Widget;
            internal List<WidgetStatus> Seen;

            private void Start()
            {
                Widget.OnWidgetStatusChanged += Record;
            }

            private void OnDisable()
            {
                if (Widget != null)
                {
                    Widget.OnWidgetStatusChanged -= Record;
                }
            }

            private void Record()
            {
                Seen.Add(Widget.Status);
            }
        }
    }
}
