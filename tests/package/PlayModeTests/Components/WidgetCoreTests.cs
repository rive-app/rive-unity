using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Rive.Components;
using Rive.Producer;
using Rive.Tests.Utils;
using Rive.Utils;
using UnityEngine;
using UnityEngine.TestTools;
using Rive.Host;

namespace Rive.Tests
{
    public class WidgetCoreTests
    {
        private MockLogger m_mockLogger;
        private TestAssetLoadingManager m_assets;
        private Asset m_asset;
        private readonly List<GameObject> m_objects = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            m_mockLogger = new MockLogger();
            DebugLogger.Instance = m_mockLogger;
            m_assets = new TestAssetLoadingManager();
            CommandTransport.EnsureStarted();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in m_objects)
            {
                if (go != null)
                {
                    Object.Destroy(go);
                }
            }
            m_objects.Clear();
            m_assets.UnloadAllAssets();
        }

        private IEnumerator LoadWidget(System.Action<RiveWidget> loaded)
        {
            yield return m_assets.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_sophiaHud,
                a => m_asset = a,
                () => Assert.Fail("Failed to load the test asset"));

            RivePanel panel = RivePanelTestUtils.CreatePanel("WidgetCorePanel");
            m_objects.Add(panel.gameObject);
            RiveWidget widget = RivePanelTestUtils.CreateWidget<RiveWidget>();
            widget.transform.SetParent(panel.WidgetContainer, false);
            widget.Load(m_asset);
            yield return null;
            Assert.AreEqual(WidgetStatus.Loaded, widget.Status);
            loaded(widget);
        }

        [UnityTest]
        public IEnumerator Reload_GivesTheWidgetANewCore()
        {
            RiveWidget widget = null;
            yield return LoadWidget(w => widget = w);
            WidgetCore first = widget.Core;

            widget.Load(m_asset);
            yield return null;

            Assert.IsNotNull(widget.Core);
            Assert.AreNotSame(first, widget.Core, "Work still out for the old load keeps the old core.");
        }
    }
}
