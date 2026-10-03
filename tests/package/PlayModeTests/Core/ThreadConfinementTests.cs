using System.Collections;
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
    /// Host calls have to come from the main thread and routines have to run
    /// on the server. The plugin counts the ones that don't, and these read
    /// that counter.
    /// </summary>
    public class ThreadConfinementTests
    {
        private TestAssetLoadingManager m_assetLoader;
        private File m_file;
        private Artboard m_artboard;
        private StateMachine m_stateMachine;

        private const string BoolInput = "boolean_input";
        private const string NumberInput = "number_input";
        private const string TriggerInput = "trigger_input";

        [UnitySetUp]
        public IEnumerator Setup()
        {
            m_assetLoader = new TestAssetLoadingManager();
            DebugLogger.Instance = new MockLogger();

            Asset riveAsset = null;
            string path = TestAssetReferences.riv_simple_input_test;
            yield return m_assetLoader.LoadAssetCoroutine<Rive.Asset>(
                path,
                (asset) => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {path}"));

            m_file = Rive.File.Load(riveAsset);
            m_artboard = m_file.Artboard(0);
            m_stateMachine = m_artboard.StateMachine();

            Assert.IsNotNull(m_artboard, "Failed to load artboard");
            Assert.IsNotNull(m_stateMachine, "Failed to load state machine");
        }

        [TearDown]
        public void TearDown()
        {
            if (m_file != null)
            {
                m_file.Dispose();
            }
            m_assetLoader.UnloadAllAssets();
        }

        /// Touches the bindings a game would, setters included. Setters are
        /// the ones that get missed, because the getter beside them is
        /// already wrapped and looks like proof the pair is fine.
        private void ExerciseApi()
        {
            m_artboard.Width = m_artboard.Width;
            m_artboard.Height = m_artboard.Height;

            SMIBool flag = m_stateMachine.GetBool(BoolInput);
            flag.Value = !flag.Value;
            flag.Value = !flag.Value;

            SMINumber number = m_stateMachine.GetNumber(NumberInput);
            number.Value = number.Value + 1f;

            m_stateMachine.GetTrigger(TriggerInput).Fire();
            m_stateMachine.Advance(0.016f);
        }

        private static void AssertStayedOnTheirThreads(string caller)
        {
            HostNative.riveGetThreadViolations(out ThreadViolations v);
            Assert.AreEqual(
                0u,
                v.MainViolations,
                $"{caller} made {v.MainViolations} host calls off the main "
                    + $"thread. First one was '{v.FirstMainOffender}'.");
            Assert.AreEqual(
                0u,
                v.ServerViolations,
                $"{caller} ran {v.ServerViolations} routines off the server "
                    + $"thread. First one was '{v.FirstServerOffender}'.");
            Assert.Greater(
                v.ServerCalls,
                0u,
                "No routines were counted, so this proved nothing.");
        }

        [NeedsRiveThread]
        [Test]
        public void PublicApi_FromTheMainThread_StaysOnItsThreads()
        {
            HostNative.riveResetThreadViolations();
            ExerciseApi();
            AssertStayedOnTheirThreads("The main thread");
        }

        [NeedsManagedThreads]
        [NeedsRiveThread]
        [UnityTest]
        public IEnumerator PublicApi_FromABackgroundThread_StaysOnItsThreads()
        {
            HostNative.riveResetThreadViolations();

            var caller = new Thread(ExerciseApi);
            caller.Start();
            while (caller.IsAlive)
            {
                yield return null;
            }

            AssertStayedOnTheirThreads("A background thread");
        }
    }
}
