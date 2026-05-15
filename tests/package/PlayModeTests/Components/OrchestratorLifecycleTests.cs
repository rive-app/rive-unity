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
    /// Catches Unity-as-a-Library (UaaL) reload regressions in <see cref="Orchestrator"/>.
    ///
    /// In UaaL, <c>[RuntimeInitializeOnLoadMethod]</c> only fires on the very first
    /// runEmbedded, not on subsequent ones, so static state survives across reloads.
    /// If the singleton creation fails due to a stale "destroyed" flag with no recovery,
    /// every RivePanel on the second runEmbedded shows up as a white square (panel
    /// never registers, never ticks, never draws).
    ///
    /// PlayMode tests have the same one-shot lifecycle, so we can fake a UaaL reload
    /// by destroying the orchestrator mid-test and not resetting any statics.
    /// </summary>
    public class OrchestratorLifecycleTests
    {
        private MockLogger m_mockLogger;
        private readonly List<GameObject> m_createdObjects = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            m_mockLogger = new MockLogger();
            DebugLogger.Instance = m_mockLogger;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in m_createdObjects)
            {
                if (go != null)
                {
                    UnityEngine.Object.Destroy(go);
                }
            }
            m_createdObjects.Clear();

            CleanupHandler();
        }


        [UnityTest]
        public IEnumerator Orchestrator_Instance_RecoversAfterPreviousInstanceDestroyed()
        {
            // First runEmbedded to confirm the orchestrator is created lazily.
            var first = Orchestrator.Instance;
            Assert.IsNotNull(
                first,
                "Sanity: orchestrator should be created lazily on first access.");

            var handlerMb = RenderPipelineHelper.CurrentHandler as MonoBehaviour;
            Assert.IsNotNull(
                handlerMb,
                "Sanity: RenderPipelineHelper.CurrentHandler should be a MonoBehaviour.");

            // Simulate calling unloadApplication by destroying the handler GameObject so the next session
            // has to lazily recreate it.
            UnityEngine.Object.DestroyImmediate(handlerMb.gameObject);

            yield return null;

            // Simulate runEmbedded by creating a brand new orchestrator. We deliberately don't reset any
            // statics. Unity-as-a-Library doesn't re-fire RuntimeInitializeOnLoadMethod, so the
            // production code has to recover on its own.
            var second = Orchestrator.Instance;
            Assert.IsNotNull(
                second,
                "Orchestrator.Instance returned null after the previous instance was " +
                "destroyed. Any static state guarding this getter must be lifecycle-aware -- " +
                "see the OrchestratorLifecycleTests doc comment for context.");
            Assert.AreNotSame(
                first, second,
                "Orchestrator.Instance returned the previously-destroyed instance instead " +
                "of creating a fresh one.");
        }

        [UnityTest]
        public IEnumerator Orchestrator_RecoversAfterHandlerDestroyed_SimulatesUnityAsLibraryReload()
        {
            var panel1 = RivePanelTestUtils.CreatePanel("Panel1_FirstSession");
            m_createdObjects.Add(panel1.gameObject);
            var strategy1 = panel1.gameObject.AddComponent<MockRenderTargetStrategy>();
            panel1.RenderTargetStrategy = strategy1;

            var widget1 = RivePanelTestUtils.CreateWidget<MockRiveWidget>("Widget1_FirstSession");
            panel1.AddToHierarchy(widget1);
            RivePanelTestUtils.MakeWidgetFillPanel(widget1);

            // Two frames so OnEnable + the first orchestrator Update have a chance to run.
            yield return null;
            yield return null;

            Assert.IsNotNull(
                Orchestrator.Instance,
                "Sanity: orchestrator must exist after the first 'runEmbedded'.");
            Assert.Greater(
                widget1.TickCount, 0,
                "Sanity: widget must tick on the first 'runEmbedded'. If this fails the " +
                "test is broken, not the production code.");

            // Simulate calling unloadApplication by destroying the handler GameObject so the next session
            // has to lazily recreate it.
            // Unity destroys all GameObjects when the embedded instance is unloaded.
            // DestroyImmediate fires OnDestroy synchronously.
            var handlerMb = RenderPipelineHelper.CurrentHandler as MonoBehaviour;
            Assert.IsNotNull(
                handlerMb,
                "Sanity: RenderPipelineHelper.CurrentHandler should be a MonoBehaviour.");
            UnityEngine.Object.DestroyImmediate(handlerMb.gameObject);
            UnityEngine.Object.DestroyImmediate(panel1.gameObject);

            yield return null;

            // Simulate runEmbedded by creating a brand new panel and widget.
            var panel2 = RivePanelTestUtils.CreatePanel("Panel2_SecondSession");
            m_createdObjects.Add(panel2.gameObject);
            var strategy2 = panel2.gameObject.AddComponent<MockRenderTargetStrategy>();
            panel2.RenderTargetStrategy = strategy2;

            var widget2 = RivePanelTestUtils.CreateWidget<MockRiveWidget>("Widget2_SecondSession");
            panel2.AddToHierarchy(widget2);
            RivePanelTestUtils.MakeWidgetFillPanel(widget2);

            // A few extra frames to give OnEnable + the new orchestrator's first Update a
            // chance to run.
            yield return null;
            yield return null;
            yield return null;

            Assert.IsNotNull(
                Orchestrator.Instance,
                "Orchestrator.Instance must be re-creatable after the previous orchestrator " +
                "GameObject was destroyed. Returning null here is what causes the white " +
                "squares on a second Unity-as-a-Library runEmbedded call.");

            Assert.Greater(
                widget2.TickCount, 0,
                "Widget on the second 'runEmbedded' was never ticked. This is the " +
                "white-square regression. The panel was never registered with the orchestrator, and TickAutoPanels never advanced its widgets.");
        }


        private static void CleanupHandler()
        {
            var handlerMb = RenderPipelineHelper.CurrentHandler as MonoBehaviour;
            if (handlerMb != null)
            {
                UnityEngine.Object.DestroyImmediate(handlerMb.gameObject);
            }
        }
    }
}
