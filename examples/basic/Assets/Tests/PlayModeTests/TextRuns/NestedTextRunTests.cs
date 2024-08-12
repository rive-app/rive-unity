using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Rive;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class NestedTextRunTests
{
    // Make sure this scene is added to the build settings
    private const string testSceneName = "NestedTextRunsTest";
    private RiveScreen riveScreen;

    public class TextRunConfig
    {
        public string Path { get; }
        public string RunName { get; }
        public string InitialValue { get; }
        public string UpdatedValue { get; }

        public TextRunConfig(string path, string runName, string initialValue, string updatedValue)
        {
            Path = path;
            RunName = runName;
            InitialValue = initialValue;
            UpdatedValue = updatedValue;
        }

        public override string ToString()
        {
            return RunName;
        }
    }

    public static IEnumerable<TextRunConfig> NestedTextRunConfigs()
    {
        yield return new TextRunConfig("ArtboardB-1", "ArtboardBRun", "Artboard B Run", "Artboard B1 Run Updated");
        yield return new TextRunConfig("ArtboardB-1/ArtboardC-1", "ArtboardCRun", "Artboard C Run", "Artboard B1C1 Run Updated");
    }

    public static IEnumerable<TextRunConfig> NonNestedTextRunConfigs()
    {
        yield return new TextRunConfig("", "ArtboardARun", "Artboard A Run", "New A Value");
    }

    [UnitySetUp]
    public IEnumerator Setup()
    {
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(testSceneName);
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        riveScreen = GameObject.FindObjectOfType<RiveScreen>();
        yield return null;
    }

    [UnityTest]
    public IEnumerator SetAndGetTextRunValueAtPath_HandlesNestedTextRunsCorrectly([ValueSource(nameof(NestedTextRunConfigs))] TextRunConfig config)
    {
        var artboard = riveScreen.Artboard;

        var initialValue = artboard.GetTextRunValueAtPath(config.RunName, config.Path);
        Assert.AreEqual(config.InitialValue, initialValue, $"Initial value for {config.RunName} should be {config.InitialValue}");

        bool setResult = artboard.SetTextRunValueAtPath(config.RunName, config.Path, config.UpdatedValue);
        Assert.IsTrue(setResult, $"Setting value for {config.RunName} should succeed");

        yield return null;

        var updatedValue = artboard.GetTextRunValueAtPath(config.RunName, config.Path);
        Assert.AreEqual(config.UpdatedValue, updatedValue, $"Updated value for {config.RunName} should be {config.UpdatedValue}");

        yield return null;
    }

    [UnityTest]
    public IEnumerator SetAndGetTextRunValue_HandlesNonNestedTextRunsCorrectly([ValueSource(nameof(NonNestedTextRunConfigs))] TextRunConfig config)
    {
        var artboard = riveScreen.Artboard;

        var initialValue = artboard.GetTextRunValue(config.RunName);
        Assert.AreEqual(config.InitialValue, initialValue, $"Initial value for {config.RunName} should be {config.InitialValue}");

        bool setResult = artboard.SetTextRun(config.RunName, config.UpdatedValue);
        Assert.IsTrue(setResult, $"Setting value for {config.RunName} should succeed");

        yield return null;

        var updatedValue = artboard.GetTextRunValue(config.RunName);
        Assert.AreEqual(config.UpdatedValue, updatedValue, $"Updated value for {config.RunName} should be {config.UpdatedValue}");

        yield return null;
    }

    [UnityTest]
    public IEnumerator TextRunOperations_ReturnNullOrFalse_WithInvalidInputOrPath()
    {
        var artboard = riveScreen.Artboard;
        string nonExistentRun = "NonExistentRun";
        string nonExistentPath = "NonExistentPath";

        var nullResult = artboard.GetTextRunValueAtPath(nonExistentRun, "ArtboardB-1");
        Assert.IsNull(nullResult, "GetTextRunValueAtPath should return null for non-existent run");

        nullResult = artboard.GetTextRunValueAtPath("ArtboardBRun", nonExistentPath);
        Assert.IsNull(nullResult, "GetTextRunValueAtPath should return null for non-existent path");

        bool falseResult = artboard.SetTextRunValueAtPath(nonExistentRun, "ArtboardB-1", "New Value");
        Assert.IsFalse(falseResult, "SetTextRunValueAtPath should return false for non-existent run");

        falseResult = artboard.SetTextRunValueAtPath("ArtboardBRun", nonExistentPath, "New Value");
        Assert.IsFalse(falseResult, "SetTextRunValueAtPath should return false for non-existent path");

        yield return null;
    }
}