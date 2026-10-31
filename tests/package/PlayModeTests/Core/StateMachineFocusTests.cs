using System.Collections;
using NUnit.Framework;
using Rive.Tests.Utils;
using Rive.Utils;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rive.Tests
{
    public class StateMachineFocusTests
    {
        private TestAssetLoadingManager m_testAssetLoadingManager;
        private MockLogger m_mockLogger;
        private File m_loadedFile;
        private Artboard m_loadedArtboard;
        private StateMachine m_stateMachine;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            m_testAssetLoadingManager = new TestAssetLoadingManager();
            m_mockLogger = new MockLogger();
            DebugLogger.Instance = m_mockLogger;

            Asset riveAsset = null;
            string path = TestAssetReferences.riv_simple_input_test;
            yield return m_testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                path,
                (asset) => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {path}"));

            m_loadedFile = Rive.File.Load(riveAsset);
            m_loadedArtboard = m_loadedFile.Artboard(0);
            m_stateMachine = m_loadedArtboard.StateMachine();
            Assert.IsNotNull(m_stateMachine);
        }

        [TearDown]
        public void TearDown()
        {
            if (m_loadedFile != null)
            {
                m_loadedFile.Dispose();
            }

            m_testAssetLoadingManager?.UnloadAllAssets();
        }

        [Test]
        public void FileWithoutFocusNodes_ReportsEmptyFocusState()
        {
            m_stateMachine.Advance(0f);

            Assert.IsFalse(m_stateMachine.HasFocusNodes);
            Assert.IsFalse(m_stateMachine.HasFocus);
            Assert.IsFalse(m_stateMachine.FocusNext());
            Assert.IsFalse(m_stateMachine.FocusPrevious());
            Assert.IsFalse(m_stateMachine.FocusLeft());
            Assert.IsFalse(m_stateMachine.FocusRight());
            Assert.IsFalse(m_stateMachine.FocusUp());
            Assert.IsFalse(m_stateMachine.FocusDown());
            Assert.IsFalse(m_stateMachine.TryGetPrimaryFocusBounds(out _));

            FocusState state = m_stateMachine.FocusState;
            Assert.IsFalse(state.HasFocus);
            Assert.IsFalse(state.ExpectsKeyboardInput);
        }

        [Test]
        public void ClearFocus_OnUnfocusedStateMachine_LeavesHasFocusFalse()
        {
            m_stateMachine.Advance(0f);
            m_stateMachine.ClearFocus();

            Assert.IsFalse(m_stateMachine.HasFocus);
            Assert.IsFalse(m_stateMachine.FocusState.HasFocus);
            Assert.IsFalse(m_stateMachine.FocusState.ExpectsKeyboardInput);
        }

        [Test]
        public void FocusState_NeverReportsKeyboardWithoutFocus()
        {
            m_stateMachine.Advance(0f);
            FocusState state = m_stateMachine.FocusState;
            if (!state.HasFocus)
            {
                Assert.IsFalse(state.ExpectsKeyboardInput);
            }
        }

        [Test]
        public void DisposedStateMachine_DoesNotInvokeNativeFocusApis()
        {
            m_stateMachine.Dispose();

            Assert.DoesNotThrow(() => m_stateMachine.ClearFocus());
            Assert.IsFalse(m_stateMachine.HasFocusNodes);
            Assert.IsFalse(m_stateMachine.FocusNext());
            Assert.IsFalse(m_stateMachine.FocusPrevious());
            Assert.IsFalse(m_stateMachine.FocusLeft());
            Assert.IsFalse(m_stateMachine.FocusRight());
            Assert.IsFalse(m_stateMachine.FocusUp());
            Assert.IsFalse(m_stateMachine.FocusDown());
            Assert.IsFalse(m_stateMachine.TryGetPrimaryFocusBounds(out _));
            Assert.IsFalse(m_stateMachine.HasFocus);
            Assert.IsFalse(m_stateMachine.FocusState.HasFocus);
            Assert.IsTrue(m_mockLogger.LoggedErrors.Count > 0);
        }
    }
}
