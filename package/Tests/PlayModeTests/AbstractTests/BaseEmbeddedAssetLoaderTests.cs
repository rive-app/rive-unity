using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rive.Tests.Utils;
using Rive.Utils;
using UnityEngine.TestTools;

namespace Rive.Tests
{
    /// <summary>
    /// Abstract class to test the EmbeddedAssetLoader class. Implement the GetTestCases method to provide test data.
    /// </summary>
    public abstract class BaseEmbeddedAssetLoaderTests
    {
        public class EmbeddedAssetTestDataItem
        {
            public string ExpectedName { get; set; }
            public EmbeddedAssetType ExpectedType { get; set; }
            public uint ExpectedId { get; set; }
            public uint ExpectedBytes { get; set; }


        }

        /// <summary>
        /// Test data for embedded assets in a Rive file.
        /// </summary>
        public class EmbeddedAssetTestOption
        {
            public string AssetPath { get; set; }

            public List<EmbeddedAssetTestDataItem> EmbeddedDataList { get; set; }

            public override string ToString()
            {
                return $"RiveFile: {AssetPath}";
            }
        }

        TestAssetLoadingManager testAssetLoadingManager;

        MockLogger mockLogger;

        EmbeddedAssetDataLoader embeddedAssetDataLoader;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            testAssetLoadingManager = new TestAssetLoadingManager();
            mockLogger = new MockLogger();
            DebugLogger.Instance = mockLogger;
            embeddedAssetDataLoader = new EmbeddedAssetDataLoader();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            testAssetLoadingManager.UnloadAllAssets();
        }

        /// <summary>
        /// Test cases for Rive files with embedded assets. 
        /// </summary>
        /// <returns></returns>
        public abstract IEnumerable<EmbeddedAssetTestOption> GetTestCases();

        [Test]
        public void EnsureTestCasesExist()
        {
            var testCases = GetTestCases();

            Assert.IsNotNull(testCases);
            Assert.IsNotEmpty(testCases);

            Assert.Greater(testCases.Count(), 0);

        }

        [UnityTest]
        public IEnumerator LoadEmbeddedAssetDataFromRiveFileBytes_YieldsCorrectData()
        {

            foreach (var testData in GetTestCases())
            {
                byte[] riveFileBytes = null;

                yield return testAssetLoadingManager.LoadAssetCoroutine<Rive.Asset>(testData.AssetPath, (loadedRiv) =>
                {
                    riveFileBytes = loadedRiv.Bytes;
                });

                var result = embeddedAssetDataLoader.LoadEmbeddedAssetDataFromRiveFileBytes(riveFileBytes).ToList();

                Assert.IsNotNull(result);
                Assert.AreEqual(testData.EmbeddedDataList.Count, result.Count);

                for (int i = 0; i < testData.EmbeddedDataList.Count; i++)
                {
                    Assert.AreEqual(testData.EmbeddedDataList[i].ExpectedName, result[i].Name);
                    Assert.AreEqual(testData.EmbeddedDataList[i].ExpectedType, result[i].AssetType);
                    Assert.AreEqual(testData.EmbeddedDataList[i].ExpectedId, result[i].Id);
                    Assert.AreEqual(testData.EmbeddedDataList[i].ExpectedBytes, result[i].InBandBytesSize);
                }

                testAssetLoadingManager.ReleaseAsset(testData.AssetPath);
            }

        }

        [UnityTest]
        public IEnumerator LoadEmbeddedAssetDataFromRiveFileBytes_YieldsEmptyEnumerableForInvalidInput()
        {
            byte[] invalidRiveFileBytes = new byte[0];

            var result = embeddedAssetDataLoader.LoadEmbeddedAssetDataFromRiveFileBytes(invalidRiveFileBytes).ToList();

            Assert.IsNotNull(result);
            Assert.IsEmpty(result);

            Assert.IsTrue(mockLogger.AnyLogTypeContains(EmbeddedAssetDataLoader.ERROR_CODE_RIVE_FILE_BYTES_NULL_OR_EMPTY));

            yield return null;
        }

        [UnityTest]
        public IEnumerator LoadEmbeddedAssetDataFromRiveFileBytes_YieldsCorrectOrderOfAssets()
        {
            foreach (var testData in GetTestCases())
            {
                byte[] riveFileBytes = null;

                yield return testAssetLoadingManager.LoadAssetCoroutine<Rive.Asset>(testData.AssetPath, (loadedRiv) =>
                {
                    riveFileBytes = loadedRiv.Bytes;
                });

                var result = embeddedAssetDataLoader.LoadEmbeddedAssetDataFromRiveFileBytes(riveFileBytes).ToList();

                Assert.IsNotNull(result);
                Assert.AreEqual(testData.EmbeddedDataList.Count, result.Count);

                for (int i = 0; i < testData.EmbeddedDataList.Count; i++)
                {
                    Assert.AreEqual(testData.EmbeddedDataList[i].ExpectedName, result[i].Name);
                    Assert.AreEqual(testData.EmbeddedDataList[i].ExpectedType, result[i].AssetType);
                    Assert.AreEqual(testData.EmbeddedDataList[i].ExpectedId, result[i].Id);
                    Assert.AreEqual(testData.EmbeddedDataList[i].ExpectedBytes, result[i].InBandBytesSize);
                }

                testAssetLoadingManager.ReleaseAsset(testData.AssetPath);
            }

        }

        [UnityTest]
        public IEnumerator LoadEmbeddedAssetDataFromRiveFileBytes_CanBeEnumeratedMultipleTimes()
        {
            foreach (var testData in GetTestCases())
            {
                byte[] riveFileBytes = null;

                yield return testAssetLoadingManager.LoadAssetCoroutine<Rive.Asset>(testData.AssetPath, (loadedRiv) =>
                {
                    riveFileBytes = loadedRiv.Bytes;
                });

                var firstEnumeration = embeddedAssetDataLoader.LoadEmbeddedAssetDataFromRiveFileBytes(riveFileBytes).ToList();
                Assert.AreEqual(testData.EmbeddedDataList.Count, firstEnumeration.Count);

                var secondEnumeration = embeddedAssetDataLoader.LoadEmbeddedAssetDataFromRiveFileBytes(riveFileBytes).ToList();
                Assert.AreEqual(testData.EmbeddedDataList.Count, secondEnumeration.Count);

                // Compare both
                for (int i = 0; i < testData.EmbeddedDataList.Count; i++)
                {
                    Assert.AreEqual(firstEnumeration[i].Name, secondEnumeration[i].Name);
                    Assert.AreEqual(firstEnumeration[i].AssetType, secondEnumeration[i].AssetType);
                    Assert.AreEqual(firstEnumeration[i].Id, secondEnumeration[i].Id);
                    Assert.AreEqual(firstEnumeration[i].InBandBytesSize, secondEnumeration[i].InBandBytesSize);
                }

                testAssetLoadingManager.ReleaseAsset(testData.AssetPath);
            }
        }

    }
}