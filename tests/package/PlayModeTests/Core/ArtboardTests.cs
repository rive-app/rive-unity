using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Rive.Tests.Utils;
using Rive.Utils;
using UnityEngine;
using UnityEngine.TestTools;
namespace Rive.Tests
{
    public class ArtboardTests
    {
        public class TestArtboardAssetData
        {
            public string assetPath;
            public uint artboardIndex;
            public string expectedArtboardName;
            public uint expectedStateMachineCount;
            public string[] expectedStateMachineNames;

            public TestArtboardAssetData(
                string path,
                uint index,
                string artboardName = null,
                uint stateMachineCount = 0,
                string[] stateMachineNames = null)
            {
                assetPath = path;
                artboardIndex = index;
                expectedArtboardName = artboardName;
                expectedStateMachineCount = stateMachineCount;
                expectedStateMachineNames = stateMachineNames;
            }
        }

        TestAssetLoadingManager testAssetLoadingManager;
        MockLogger mockLogger;
        private List<File> m_loadedFiles;

        private File LoadAndTrackFile(Asset asset)
        {
            var file = File.Load(asset);
            m_loadedFiles.Add(file);
            return file;
        }

        /// <summary>
        /// Return test Rive Asset data for files containing artboards to test
        /// </summary>
        protected TestArtboardAssetData[] GetTestRiveAssetData()
        {
            return new TestArtboardAssetData[]
        {
            new TestArtboardAssetData(
                TestAssetReferences.riv_sophiaHud,
                0,
                "SOPHIA III HUD",
                1,
                new string[] { "SOPHIA_III_DASHBOARD_SEQ" }
            ),
            new TestArtboardAssetData(
                TestAssetReferences.riv_stormtrooper_bird,
                0,
                "michael-myers-trooperbird.jpg",
                1,
                new string[] { "State Machine 1" }
            ),
        };
        }

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            testAssetLoadingManager = new TestAssetLoadingManager();
        }

        [SetUp]
        public void Setup()
        {
            mockLogger = new MockLogger();
            m_loadedFiles = new List<File>();
            DebugLogger.Instance = mockLogger;
        }


        [TearDown]
        public void TearDown()
        {
            foreach (var file in m_loadedFiles)
            {
                if (file != null && !file.IsDisposed)
                {
                    file.Dispose();
                }
            }
            m_loadedFiles.Clear();
            testAssetLoadingManager.UnloadAllAssets();
        }

        private IEnumerator LoadArtboardAndTest(TestArtboardAssetData assetData, Action<File, Artboard> testAction)
        {
            Asset riveAsset = null;
            yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                assetData.assetPath,
                (asset) => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {assetData.assetPath}")
            );

            using (File riveFile = File.Load(riveAsset))
            {
                Assert.IsNotNull(riveFile, $"Failed to load Rive file from {assetData.assetPath}");
                Assert.Greater(riveFile.ArtboardCount, assetData.artboardIndex,
                    $"File must contain at least {assetData.artboardIndex + 1} artboards");

                Artboard artboard = riveFile.Artboard(assetData.artboardIndex);
                Assert.IsNotNull(artboard, $"Failed to load artboard at index {assetData.artboardIndex}");

                testAction(riveFile, artboard);
            }
        }

        [UnityTest]
        public IEnumerator Load_ValidAsset_LoadsArtboardWithCorrectInfo()
        {
            TestArtboardAssetData[] testDataArray = GetTestRiveAssetData();
            Assert.Greater(testDataArray.Length, 0, "No test data found");

            foreach (TestArtboardAssetData testData in testDataArray)
            {
                Asset riveAsset = null;
                yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                    testData.assetPath,
                    (asset) => riveAsset = asset,
                    () => Assert.Fail($"Failed to load asset at {testData.assetPath}")
                );

                File riveFile = LoadAndTrackFile(riveAsset);
                Artboard artboard = riveFile.Artboard(testData.artboardIndex);

                // Test artboard name
                if (testData.expectedArtboardName != null)
                {
                    Assert.AreEqual(testData.expectedArtboardName, artboard.Name,
                        $"Artboard name mismatch for {testData.assetPath}");
                }

                // Test state machine count
                if (testData.expectedStateMachineCount > 0)
                {
                    Assert.AreEqual(testData.expectedStateMachineCount, artboard.StateMachineCount,
                        $"State machine count mismatch for {testData.assetPath}");
                }

                // Test state machine names
                if (testData.expectedStateMachineNames != null)
                {
                    for (uint i = 0; i < testData.expectedStateMachineNames.Length; i++)
                    {
                        string expectedName = testData.expectedStateMachineNames[i];
                        string actualName = artboard.StateMachineName(i);
                        Assert.AreEqual(expectedName, actualName,
                            $"State machine name mismatch at index {i} for {testData.assetPath}");

                        // Test state machine instantiation
                        StateMachine stateMachine = artboard.StateMachine(i);
                        Assert.IsNotNull(stateMachine,
                            $"Failed to instantiate state machine at index {i} for {testData.assetPath}");
                        Assert.AreEqual(expectedName, stateMachine.Name,
                            $"Instantiated state machine name mismatch at index {i} for {testData.assetPath}");
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator StateMachine_LoadByName_ReturnsCorrectStateMachine()
        {
            TestArtboardAssetData[] testDataArray = GetTestRiveAssetData();
            Assert.Greater(testDataArray.Length, 0, "No test data found");

            foreach (TestArtboardAssetData testData in testDataArray)
            {
                if (testData.expectedStateMachineNames == null) continue;

                Asset riveAsset = null;
                yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                    testData.assetPath,
                    (asset) => riveAsset = asset,
                    () => Assert.Fail($"Failed to load asset at {testData.assetPath}")
                );

                File riveFile = LoadAndTrackFile(riveAsset);
                Artboard artboard = riveFile.Artboard(testData.artboardIndex);

                foreach (string smName in testData.expectedStateMachineNames)
                {
                    StateMachine stateMachine = artboard.StateMachine(smName);
                    Assert.IsNotNull(stateMachine,
                        $"Failed to load state machine '{smName}' by name for {testData.assetPath}");
                    Assert.AreEqual(smName, stateMachine.Name,
                        $"Loaded state machine name mismatch for {testData.assetPath}");
                }

                // Test loading non-existent state machine
                StateMachine invalidStateMachine = artboard.StateMachine("NonExistentStateMachine");
                Assert.IsNull(invalidStateMachine,
                    $"Loading non-existent state machine should return null for {testData.assetPath}");
            }
        }

        [UnityTest]
        public IEnumerator SetWidth_ValidValue_UpdatesWidth()
        {
            foreach (var assetData in GetTestRiveAssetData())
            {
                yield return LoadArtboardAndTest(assetData, (file, artboard) =>
                {
                    float originalWidth = artboard.Width;
                    float newWidth = originalWidth * 2;

                    artboard.Width = newWidth;
                    Assert.AreEqual(newWidth, artboard.Width, "Artboard width should match new value");
                });
            }
        }

        [UnityTest]
        public IEnumerator SetHeight_ValidValue_UpdatesHeight()
        {
            foreach (var assetData in GetTestRiveAssetData())
            {
                yield return LoadArtboardAndTest(assetData, (file, artboard) =>
                {
                    float originalHeight = artboard.Height;
                    float newHeight = originalHeight * 2;

                    artboard.Height = newHeight;
                    Assert.AreEqual(newHeight, artboard.Height, "Artboard height should match new value");
                });
            }
        }

        [UnityTest]
        public IEnumerator ResetArtboardSize_ResetsToOriginalDimensions()
        {
            foreach (var assetData in GetTestRiveAssetData())
            {
                yield return LoadArtboardAndTest(assetData, (file, artboard) =>
                {
                    float originalWidth = artboard.Width;
                    float originalHeight = artboard.Height;

                    artboard.Width = originalWidth * 2;
                    artboard.Height = originalHeight * 2;

                    Assert.AreNotEqual(originalWidth, artboard.Width, "Width should have changed");
                    Assert.AreNotEqual(originalHeight, artboard.Height, "Height should have changed");

                    artboard.ResetArtboardSize();

                    Assert.AreEqual(originalWidth, artboard.Width, "Width should have reset to original value");
                    Assert.AreEqual(originalHeight, artboard.Height, "Height should have reset to original value");
                });
            }
        }
    }
}