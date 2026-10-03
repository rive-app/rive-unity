// Inputs are deprecated but still covered.
#pragma warning disable CS0618
using System.Collections;
using NUnit.Framework;
using Rive.Tests.Utils;
using Rive.Utils;
using UnityEngine;
using UnityEngine.TestTools;
using Rive.Host;

namespace Rive.Tests
{
    public class NestedInputTests
    {
        public enum InputType
        {
            Boolean = 0,
            Number = 1,
            Trigger = 2
        }
        /// <summary>
        /// Represents the configuration for an artboard input
        /// </summary>
        class ArtboardConfig
        {
            /// <summary>
            /// The path to the artboard
            /// </summary>
            public readonly string Path;

            /// <summary>
            /// The name of the input to set, get, or trigger
            /// </summary>
            public readonly string InputName;

            /// <summary>
            /// The type of the input
            /// </summary>
            public readonly InputType Type;

            public ArtboardConfig(string path, string inputName, InputType type)
            {
                Path = path;
                InputName = inputName;
                Type = type;
            }
        }


        private readonly ArtboardConfig cardConfig = new ArtboardConfig("Card", "isFlipped", InputType.Boolean);
        private readonly ArtboardConfig nestedButtonConfig = new ArtboardConfig("Buttons/NestedUploadButton", "width", InputType.Number);
        private readonly ArtboardConfig buttonClickConfig = new ArtboardConfig("Buttons", "click", InputType.Trigger);

        private readonly ArtboardConfig starConfig = new ArtboardConfig("Stars", "Rating", InputType.Number);



        TestAssetLoadingManager testAssetLoadingManager;

        private Artboard m_loadedArtboard;

        private File m_loadedFile;
        MockLogger mockLogger;



        [UnitySetUp]
        public IEnumerator Setup()
        {
            testAssetLoadingManager = new TestAssetLoadingManager();
            mockLogger = new MockLogger();

            DebugLogger.Instance = mockLogger;

            Asset riveAsset = null;

            string riveAssetPath = TestAssetReferences.riv_nestedinputtest;

            yield return testAssetLoadingManager.LoadAssetCoroutine<Rive.Asset>(riveAssetPath,
             (asset) => riveAsset = asset,
             () => Assert.Fail($"Failed to load asset at {riveAssetPath}"));

            m_loadedFile = Rive.File.Load(riveAsset);

            m_loadedArtboard = m_loadedFile.Artboard(0);

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



        [UnityTest]
        public IEnumerator SetAndGetBooleanInputStateAtPath_HandleNestedBooleanInputsCorrectly()
        {

            var initialFlippedValue = m_loadedArtboard.GetBooleanInputStateAtPath(cardConfig.InputName, cardConfig.Path);

            Assert.IsFalse(initialFlippedValue, "Initial value should be false");

            m_loadedArtboard.SetBooleanInputStateAtPath(cardConfig.InputName, true, cardConfig.Path);

            yield return null;

            var updatedValue = m_loadedArtboard.GetBooleanInputStateAtPath(cardConfig.InputName, cardConfig.Path);

            Assert.IsTrue(updatedValue, "Value should be true after setting it to true");

            yield return null;

            // Verify that the value is set back to false correctly
            m_loadedArtboard.SetBooleanInputStateAtPath(cardConfig.InputName, false, cardConfig.Path);

            yield return null;

            var finalValue = m_loadedArtboard.GetBooleanInputStateAtPath(cardConfig.InputName, cardConfig.Path);

            Assert.IsFalse(finalValue, "Value should be false after setting it back to false");


            yield return null;
        }

        [UnityTest]
        public IEnumerator SetAndGetNumberInputStateAtPath_HandleNestedNumberInputsCorrectly()
        {
            var initialNumberValue = m_loadedArtboard.GetNumberInputStateAtPath(starConfig.InputName, starConfig.Path);

            Assert.AreEqual(0f, initialNumberValue, "Initial Rating value should be 0");

            m_loadedArtboard.SetNumberInputStateAtPath(starConfig.InputName, 2f, starConfig.Path);

            yield return null;

            var updatedValue = m_loadedArtboard.GetNumberInputStateAtPath(starConfig.InputName, starConfig.Path);

            Assert.AreEqual(2f, updatedValue, "Rating value should be 2 after setting it to 2");

            yield return null;

            // Set the value to 4
            m_loadedArtboard.SetNumberInputStateAtPath(starConfig.InputName, 4f, starConfig.Path);

            yield return null;

            var updatedValue2 = m_loadedArtboard.GetNumberInputStateAtPath(starConfig.InputName, starConfig.Path);

            Assert.AreEqual(4f, updatedValue2, "Rating value should be 4 after setting it to 4");

            yield return null;

            // Verify that the value is set back to 0 correctly
            m_loadedArtboard.SetNumberInputStateAtPath(starConfig.InputName, 0f, starConfig.Path);

            yield return null;

            var finalValue = m_loadedArtboard.GetNumberInputStateAtPath(starConfig.InputName, starConfig.Path);

            Assert.AreEqual(0f, finalValue, "Rating value should be 0 after setting it back to 0");

            yield return null;
        }

        [UnityTest]
        public IEnumerator SetAndGetNumberInputStateAtPath_HandleNestedNumberInputsMultipleLevelsDeep()
        {
            var initialWidthValue = m_loadedArtboard.GetNumberInputStateAtPath(nestedButtonConfig.InputName, nestedButtonConfig.Path);

            Assert.AreEqual(50.0f, initialWidthValue, "Initial Width value should be 50.0f");

            m_loadedArtboard.SetNumberInputStateAtPath(nestedButtonConfig.InputName, 100f, nestedButtonConfig.Path);

            yield return null;

            var updatedValue = m_loadedArtboard.GetNumberInputStateAtPath(nestedButtonConfig.InputName, nestedButtonConfig.Path);

            Assert.AreEqual(100f, updatedValue, "Width value should be 100 after setting it to 100");

            yield return null;

            m_loadedArtboard.SetNumberInputStateAtPath(nestedButtonConfig.InputName, 200f, nestedButtonConfig.Path);

            yield return null;

            var updatedValue2 = m_loadedArtboard.GetNumberInputStateAtPath(nestedButtonConfig.InputName, nestedButtonConfig.Path);

            Assert.AreEqual(200f, updatedValue2, "Width value should be 200 after setting it to 200");

            yield return null;

        }

        [UnityTest]
        public IEnumerator FireInputStateAtPath_TriggersNestedInputsCorrectly()
        {
            // The rive file is set up to trigger an event when the button is clicked
            // so we can use this to test that the trigger input works correctly since we can't directly check the value of a trigger input.
            string buttonClickEventName = "LIGHT_BUTTON_CLICKED";
            bool receivedButtonClickedEvent = false;
            void HandleRiveEvent(ReportedEvent reportedEvent)
            {
                if (reportedEvent.Name == buttonClickEventName)
                {
                    receivedButtonClickedEvent = true;
                }
            }

            void CheckForEvents(StateMachine stateMachine)
            {
                foreach (var reportedEvent in stateMachine.ReportedEvents())
                {
                    HandleRiveEvent(reportedEvent);
                }
            }

            StateMachine stateMachine = m_loadedArtboard.StateMachine();

            stateMachine.Advance(0f);

            yield return null;
            foreach (var reportedEvent in stateMachine.ReportedEvents())
            {
                HandleRiveEvent(reportedEvent);
            }

            m_loadedArtboard.FireInputStateAtPath(buttonClickConfig.InputName, buttonClickConfig.Path);


            // Check every frame for 0.3 seconds to see if the event is triggered
            float elapsedTime = 0f;
            while (elapsedTime < 0.3f && !receivedButtonClickedEvent)
            {

                CheckForEvents(stateMachine);
                elapsedTime += Time.deltaTime;
                stateMachine.Advance(Time.deltaTime);

                if (receivedButtonClickedEvent)
                {
                    break;
                }
                yield return null;
            }



            Assert.IsTrue(receivedButtonClickedEvent, "Button Clicked event should be received");


            yield return null;
        }


        // The lookup runs with the write, so the warning comes a frame later.
        private IEnumerator ExpectWarningAfterWrite(System.Action write, string expected)
        {
            mockLogger.Clear();
            write();
            Assert.IsFalse(mockLogger.LoggedWarningsContains(expected), "The write shouldn't have run yet.");
            yield return null;
            Assert.IsTrue(mockLogger.LoggedWarningsContains(expected), $"Expected a warning containing: {expected}");
            Assert.IsTrue(mockLogger.LoggedWarningsContains($"Artboard: '{m_loadedArtboard.Name}'"),
                "The warning should name the artboard.");
        }

        [UnityTest]
        public IEnumerator NestedInputSettersAndGetters_LogAppropriateMessages_WithInvalidInputOrPath()
        {
            string nonExistentInputName = "nonExistentInput";
            string nonExistentArtboardPath = "nonExistentArtboard";
            string expected = $"No input found at path '{nonExistentArtboardPath}' with name '{nonExistentInputName}'.";

            yield return ExpectWarningAfterWrite(
                () => m_loadedArtboard.SetBooleanInputStateAtPath(nonExistentInputName, true, nonExistentArtboardPath), expected);
            yield return ExpectWarningAfterWrite(
                () => m_loadedArtboard.SetNumberInputStateAtPath(nonExistentInputName, 123f, nonExistentArtboardPath), expected);
            yield return ExpectWarningAfterWrite(
                () => m_loadedArtboard.FireInputStateAtPath(nonExistentInputName, nonExistentArtboardPath), expected);
        }

        [UnityTest]
        public IEnumerator NestedInputSettersAndGetters_LogAppropriateMessages_WithValidPathButWrongInputType()
        {
            yield return ExpectWarningAfterWrite(
                () => m_loadedArtboard.SetBooleanInputStateAtPath(starConfig.InputName, true, starConfig.Path),
                $"Input '{starConfig.InputName}' at path: '{starConfig.Path}' is not a boolean input.");
            yield return ExpectWarningAfterWrite(
                () => m_loadedArtboard.SetNumberInputStateAtPath(cardConfig.InputName, 123f, cardConfig.Path),
                $"Input '{cardConfig.InputName}' at path: '{cardConfig.Path}' is not a number input.");
            yield return ExpectWarningAfterWrite(
                () => m_loadedArtboard.FireInputStateAtPath(starConfig.InputName, starConfig.Path),
                $"Input '{starConfig.InputName}' at path: '{starConfig.Path}' is not a trigger input.");
        }

        [NeedsRiveThread]
        [UnityTest]
        public IEnumerator NestedInputSetters_NeverWaitOnTheProducer()
        {
            var waits = new System.Collections.Generic.List<string>();
            Rive.Host.CommandTransport.MainThreadWaitsForTests = waits;
            try
            {
                m_loadedArtboard.SetBooleanInputStateAtPath(cardConfig.InputName, true, cardConfig.Path);
                m_loadedArtboard.SetNumberInputStateAtPath(nestedButtonConfig.InputName, 150f, nestedButtonConfig.Path);
                m_loadedArtboard.FireInputStateAtPath(buttonClickConfig.InputName, buttonClickConfig.Path);
                m_loadedArtboard.Size = new Size(300f, 200f);
                m_loadedArtboard.ResetArtboardSize();
            }
            finally
            {
                Rive.Host.CommandTransport.MainThreadWaitsForTests = null;
            }
            CollectionAssert.IsEmpty(waits, "A method that returns nothing should only queue.");

            // Still applied, in order, before the next read.
            Assert.AreEqual(150f, m_loadedArtboard.GetNumberInputStateAtPath(nestedButtonConfig.InputName, nestedButtonConfig.Path));
            Assert.AreEqual(true, m_loadedArtboard.GetBooleanInputStateAtPath(cardConfig.InputName, cardConfig.Path));
            yield return null;
            Assert.IsFalse(mockLogger.LoggedWarnings.Count > 0, "Valid inputs shouldn't warn.");
        }

        [UnityTest]
        public IEnumerator NestedInputGetters_ReturnNull_WithInvalidInputOrPath()
        {
            string nonExistentInputName = "nonExistentInput";
            string nonExistentArtboardPath = "nonExistentArtboard";

            var booleanInputValue = m_loadedArtboard.GetBooleanInputStateAtPath(nonExistentInputName, nonExistentArtboardPath);
            Assert.IsNull(booleanInputValue, "Boolean input return value should be null");

            var numberInputValue = m_loadedArtboard.GetNumberInputStateAtPath(nonExistentInputName, nonExistentArtboardPath);
            Assert.IsNull(numberInputValue, "Number input return value should be null");


            yield return null;
        }

        [UnityTest]
        public IEnumerator NestedInputGetters_ReturnNull_WithValidPathButWrongInputType()
        {
            var booleanInputValue = m_loadedArtboard.GetBooleanInputStateAtPath(starConfig.InputName, starConfig.Path);
            Assert.IsNull(booleanInputValue, "Boolean input return value should be null");

            var numberInputValue = m_loadedArtboard.GetNumberInputStateAtPath(cardConfig.InputName, cardConfig.Path);
            Assert.IsNull(numberInputValue, "Number input return value should be null");

            yield return null;
        }
    }
}
