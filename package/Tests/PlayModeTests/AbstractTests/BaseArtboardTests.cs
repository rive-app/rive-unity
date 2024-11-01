using NUnit.Framework;
using UnityEngine.TestTools;
using System.Collections;
using Rive.Tests.Utils;
using System;
using Rive.Utils;

namespace Rive.Tests
{
    public abstract class BaseArtboardTests
    {
        protected class TestArtboardAssetData
        {
            public string assetPath;
            /// <summary>
            /// Index of the artboard to test in the file
            /// </summary>
            public uint artboardIndex;

            public TestArtboardAssetData(string path, uint index = 0)
            {
                assetPath = path;
                artboardIndex = index;
            }
        }

        TestAssetLoadingManager testAssetLoadingManager;
        MockLogger mockLogger;

        /// <summary>
        /// Return test Rive Asset data for files containing artboards to test
        /// </summary>
        protected abstract TestArtboardAssetData[] GetTestRiveAssetData();

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            testAssetLoadingManager = new TestAssetLoadingManager();
        }

        [SetUp]
        public void Setup()
        {
            mockLogger = new MockLogger();
            DebugLogger.Instance = mockLogger;
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
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