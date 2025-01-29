using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Rive.Tests.Utils;
using Rive.Utils;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rive.Tests
{
    public class SimpleInputTests
    {
        TestAssetLoadingManager testAssetLoadingManager;

        private Artboard m_loadedArtboard;

        private File m_loadedFile;

        private StateMachine m_stateMachine;
        MockLogger mockLogger;


        private const string BOOL_INPUT_NAME = "boolean_input";

        private const string NUMBER_INPUT_NAME = "number_input";

        private const string TRIGGER_INPUT_NAME = "trigger_input";

        private const string TRIGGER_EVENT_NAME = "ON_TRIGGER_EVENT";



        [UnitySetUp]
        public IEnumerator Setup()
        {
            testAssetLoadingManager = new TestAssetLoadingManager();
            mockLogger = new MockLogger();

            DebugLogger.Instance = mockLogger;

            Asset riveAsset = null;

            string riveAssetPath = TestAssetReferences.riv_simple_input_test;

            yield return testAssetLoadingManager.LoadAssetCoroutine<Rive.Asset>(riveAssetPath,
             (asset) => riveAsset = asset,
             () => Assert.Fail($"Failed to load asset at {riveAssetPath}"));

            m_loadedFile = Rive.File.Load(riveAsset);

            m_loadedArtboard = m_loadedFile.Artboard(0);

            m_stateMachine = m_loadedArtboard.StateMachine();


            Assert.IsNotNull(m_loadedArtboard, "Failed to load artboard");
            Assert.IsNotNull(m_stateMachine, "Failed to load state machine");

            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            if (m_loadedFile != null)
            {
                m_loadedFile.Dispose();
            }
            testAssetLoadingManager.UnloadAllAssets();
        }


        [Test]
        public void GetAndSet_BooleanInput_InSameFrame_Works()
        {
            var input = m_stateMachine.GetBool(BOOL_INPUT_NAME);

            Assert.IsNotNull(input, "Failed to get input");

            Assert.IsFalse(input.Value, "Input should be false");

            input.Value = true;

            Assert.IsTrue(input.Value, "Input should be true");

        }

        [Test]
        public void GetAndSet_NumberInput_InSameFrame_Works()
        {
            var input = m_stateMachine.GetNumber(NUMBER_INPUT_NAME);

            Assert.IsNotNull(input, "Failed to get input");

            Assert.AreEqual(0, input.Value, "Input should be 0");

            input.Value = 10;

            Assert.AreEqual(10, input.Value, "Input should be 10");

        }

        [UnityTest]
        public IEnumerator TriggerInput_Works_IfFired_OnInitialFrame_WithAdvance0()
        {
            var input = m_stateMachine.GetTrigger(TRIGGER_INPUT_NAME);

            Assert.IsNotNull(input, "Failed to get input");

            bool receivedEvent = false;

            void HandleRiveEvent(ReportedEvent reportedEvent)
            {
                if (reportedEvent.Name == TRIGGER_EVENT_NAME)
                {
                    receivedEvent = true;
                }
            }

            void CheckForEvents(StateMachine stateMachine)
            {
                foreach (var reportedEvent in stateMachine.ReportedEvents())
                {
                    HandleRiveEvent(reportedEvent);
                }
            }
            m_stateMachine.Advance(0f); // This is necessary to trigger the event on the first frame

            input.Fire();

            // Check every frame for 0.3 seconds to see if the event is triggered
            float elapsedTime = 0f;
            while (elapsedTime < 0.3f && !receivedEvent)
            {

                CheckForEvents(m_stateMachine);
                elapsedTime += Time.deltaTime;
                m_stateMachine.Advance(Time.deltaTime);

                if (receivedEvent)
                {
                    break;
                }
                yield return null;
            }

            Assert.IsTrue(receivedEvent, "Trigger event not received");

        }

        [UnityTest]
        public IEnumerator TriggerInput_Works_IfFired_OnInitialFrame_WithoutAdvance0()
        {
            var input = m_stateMachine.GetTrigger(TRIGGER_INPUT_NAME);

            Assert.IsNotNull(input, "Failed to get input");

            bool receivedEvent = false;

            void HandleRiveEvent(ReportedEvent reportedEvent)
            {
                if (reportedEvent.Name == TRIGGER_EVENT_NAME)
                {
                    receivedEvent = true;
                }
            }

            void CheckForEvents(StateMachine stateMachine)
            {
                foreach (var reportedEvent in stateMachine.ReportedEvents())
                {
                    HandleRiveEvent(reportedEvent);
                }
            }

            input.Fire();

            // Check every frame for 0.3 seconds to see if the event is triggered
            float elapsedTime = 0f;
            while (elapsedTime < 0.3f && !receivedEvent)
            {

                CheckForEvents(m_stateMachine);
                elapsedTime += Time.deltaTime;
                m_stateMachine.Advance(Time.deltaTime);

                if (receivedEvent)
                {
                    break;
                }
                yield return null;
            }

            Assert.IsTrue(receivedEvent, "Trigger event not received");

        }


    }
}
