using System.Collections;
using NUnit.Framework;
using Rive;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

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

    // Make sure this scene is added to the build settings
    private const string testSceneName = "NestedInputsTest";
    // The RiveScreen instance in the test scene
    private RiveScreen riveScreen;


    AsyncOperation LoadTestScene()
    {
        return SceneManager.LoadSceneAsync(testSceneName);
    }

    [UnitySetUp]
    public IEnumerator Setup()
    {
        // Load the test scene
        AsyncOperation asyncLoad = LoadTestScene();
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        riveScreen = GameObject.FindObjectOfType<RiveScreen>();

        yield return null;
    }

    [UnityTest]
    public IEnumerator SetAndGetBooleanInputStateAtPath_HandleNestedBooleanInputsCorrectly()
    {

        var initialFlippedValue = riveScreen.Artboard.GetBooleanInputStateAtPath(cardConfig.InputName, cardConfig.Path);

        Assert.IsFalse(initialFlippedValue, "Initial value should be false");

        riveScreen.Artboard.SetBooleanInputStateAtPath(cardConfig.InputName, true, cardConfig.Path);

        yield return null;

        var updatedValue = riveScreen.Artboard.GetBooleanInputStateAtPath(cardConfig.InputName, cardConfig.Path);

        Assert.IsTrue(updatedValue, "Value should be true after setting it to true");

        yield return null;

        // Verify that the value is set back to false correctly
        riveScreen.Artboard.SetBooleanInputStateAtPath(cardConfig.InputName, false, cardConfig.Path);

        yield return null;

        var finalValue = riveScreen.Artboard.GetBooleanInputStateAtPath(cardConfig.InputName, cardConfig.Path);

        Assert.IsFalse(finalValue, "Value should be false after setting it back to false");


        yield return null;
    }

    [UnityTest]
    public IEnumerator SetAndGetNumberInputStateAtPath_HandleNestedNumberInputsCorrectly()
    {
        var initialNumberValue = riveScreen.Artboard.GetNumberInputStateAtPath(starConfig.InputName, starConfig.Path);

        Assert.AreEqual(0f, initialNumberValue, "Initial Rating value should be 0");

        riveScreen.Artboard.SetNumberInputStateAtPath(starConfig.InputName, 2f, starConfig.Path);

        yield return null;

        var updatedValue = riveScreen.Artboard.GetNumberInputStateAtPath(starConfig.InputName, starConfig.Path);

        Assert.AreEqual(2f, updatedValue, "Rating value should be 2 after setting it to 2");

        yield return null;

        // Set the value to 4
        riveScreen.Artboard.SetNumberInputStateAtPath(starConfig.InputName, 4f, starConfig.Path);

        yield return null;

        var updatedValue2 = riveScreen.Artboard.GetNumberInputStateAtPath(starConfig.InputName, starConfig.Path);

        Assert.AreEqual(4f, updatedValue2, "Rating value should be 4 after setting it to 4");

        yield return null;

        // Verify that the value is set back to 0 correctly
        riveScreen.Artboard.SetNumberInputStateAtPath(starConfig.InputName, 0f, starConfig.Path);

        yield return null;

        var finalValue = riveScreen.Artboard.GetNumberInputStateAtPath(starConfig.InputName, starConfig.Path);

        Assert.AreEqual(0f, finalValue, "Rating value should be 0 after setting it back to 0");

        yield return null;
    }

    [UnityTest]
    public IEnumerator SetAndGetNumberInputStateAtPath_HandleNestedNumberInputsMultipleLevelsDeep()
    {
        var initialWidthValue = riveScreen.Artboard.GetNumberInputStateAtPath(nestedButtonConfig.InputName, nestedButtonConfig.Path);

        Assert.AreEqual(50.0f, initialWidthValue, "Initial Width value should be 50.0f");

        riveScreen.Artboard.SetNumberInputStateAtPath(nestedButtonConfig.InputName, 100f, nestedButtonConfig.Path);

        yield return null;

        var updatedValue = riveScreen.Artboard.GetNumberInputStateAtPath(nestedButtonConfig.InputName, nestedButtonConfig.Path);

        Assert.AreEqual(100f, updatedValue, "Width value should be 100 after setting it to 100");

        yield return null;

        // Set the value to 200
        riveScreen.Artboard.SetNumberInputStateAtPath(nestedButtonConfig.InputName, 200f, nestedButtonConfig.Path);

        yield return null;

        var updatedValue2 = riveScreen.Artboard.GetNumberInputStateAtPath(nestedButtonConfig.InputName, nestedButtonConfig.Path);

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

        riveScreen.OnRiveEvent += HandleRiveEvent;

        yield return null;

        riveScreen.Artboard.FireInputStateAtPath(buttonClickConfig.InputName, buttonClickConfig.Path);

        // Wait for the event to be triggered
        yield return new WaitForSeconds(0.3f);

        Assert.IsTrue(receivedButtonClickedEvent, "Button Clicked event should be received");

        riveScreen.OnRiveEvent -= HandleRiveEvent;

        yield return null;
    }


    [UnityTest]
    public IEnumerator NestedInputSettersAndGetters_LogAppropriateMessages_WithInvalidInputOrPath()
    {

        string nonExistentInputName = "nonExistentInput";
        string nonExistentArtboardPath = "nonExistentArtboard";

        string expectedWarningMessage = $"No input found at path '{nonExistentArtboardPath}' with name '{nonExistentInputName}'.";

        // Test Boolean Input
        LogAssert.Expect(LogType.Warning, expectedWarningMessage);
        riveScreen.Artboard.SetBooleanInputStateAtPath(nonExistentInputName, true, nonExistentArtboardPath);
        yield return null;

        // Test Number Input
        LogAssert.Expect(LogType.Warning, expectedWarningMessage);
        riveScreen.Artboard.SetNumberInputStateAtPath(nonExistentInputName, 123f, nonExistentArtboardPath);
        yield return null;

        // Test Trigger Input
        LogAssert.Expect(LogType.Warning, expectedWarningMessage);
        riveScreen.Artboard.FireInputStateAtPath(nonExistentInputName, nonExistentArtboardPath);

        yield return null;
    }

    [UnityTest]
    public IEnumerator NestedInputSettersAndGetters_LogAppropriateMessages_WithValidPathButWrongInputType()
    {

        // Test Boolean Input
        LogAssert.Expect(LogType.Warning, $"Input '{starConfig.InputName}' at path: '{starConfig.Path}' is not a boolean input.");
        riveScreen.Artboard.SetBooleanInputStateAtPath(starConfig.InputName, true, starConfig.Path);
        yield return null;

        // Test Number Input
        LogAssert.Expect(LogType.Warning, $"Input '{cardConfig.InputName}' at path: '{cardConfig.Path}' is not a number input.");
        riveScreen.Artboard.SetNumberInputStateAtPath(cardConfig.InputName, 123f, cardConfig.Path);
        yield return null;

        // Test Trigger Input
        LogAssert.Expect(LogType.Warning, $"Input '{starConfig.InputName}' at path: '{starConfig.Path}' is not a trigger input.");
        riveScreen.Artboard.FireInputStateAtPath(starConfig.InputName, starConfig.Path);

        yield return null;
    }

    [UnityTest]
    public IEnumerator NestedInputGetters_ReturnNull_WithInvalidInputOrPath()
    {
        string nonExistentInputName = "nonExistentInput";
        string nonExistentArtboardPath = "nonExistentArtboard";

        var booleanInputValue = riveScreen.Artboard.GetBooleanInputStateAtPath(nonExistentInputName, nonExistentArtboardPath);
        Assert.IsNull(booleanInputValue, "Boolean input return value should be null");

        var numberInputValue = riveScreen.Artboard.GetNumberInputStateAtPath(nonExistentInputName, nonExistentArtboardPath);
        Assert.IsNull(numberInputValue, "Number input return value should be null");


        yield return null;
    }

    [UnityTest]
    public IEnumerator NestedInputGetters_ReturnNull_WithValidPathButWrongInputType()
    {
        var booleanInputValue = riveScreen.Artboard.GetBooleanInputStateAtPath(starConfig.InputName, starConfig.Path);
        Assert.IsNull(booleanInputValue, "Boolean input return value should be null");

        var numberInputValue = riveScreen.Artboard.GetNumberInputStateAtPath(cardConfig.InputName, cardConfig.Path);
        Assert.IsNull(numberInputValue, "Number input return value should be null");

        yield return null;
    }
}