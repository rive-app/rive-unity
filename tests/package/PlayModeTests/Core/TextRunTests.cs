using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Rive.Tests.Utils;
using Rive.Utils;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rive.Tests
{
    public class TextRunTests
    {

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

            yield return testAssetLoadingManager.LoadAssetCoroutine<Rive.Asset>(TestAssetReferences.riv_runtime_nested_text_runs,
             (asset) => riveAsset = asset,
             () => Assert.Fail($"Failed to load asset at {TestAssetReferences.riv_runtime_nested_text_runs}"));

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
        public IEnumerator SetAndGetTextRunValueAtPath_HandlesNestedTextRunsCorrectly([ValueSource(nameof(NestedTextRunConfigs))] TextRunConfig config)
        {
            var artboard = m_loadedArtboard;

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
            var artboard = m_loadedArtboard;

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
            var artboard = m_loadedArtboard;
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
}
