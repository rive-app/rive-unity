using NUnit.Framework;

using UnityEngine.TestTools;
using System.Collections;
using Rive.Tests.Utils;
using UnityEngine;
using static Rive.File;
using System;
using System.Collections.Generic;
using System.Linq;
using Rive.Utils;


namespace Rive.Tests
{
    /// <summary>
    /// Abstract class to test loading Rive files works as expected. Implement the GetTestRiveAssetInfo and GetTestTextAssetInfo methods to return a list of test assets to load and verify.
    /// </summary>
    public abstract class BaseRiveFileTests
    {
        TestAssetLoadingManager testAssetLoadingManager;
        MockLogger mockLogger;

        public class EmbeddedAssetTestDataItem
        {
            public string ExpectedName { get; set; }
            public EmbeddedAssetType ExpectedType { get; set; }
            public uint ExpectedId { get; set; }
            public uint ExpectedBytesSize { get; set; }


        }

        /// <summary>
        /// Hold the test asset and the expected number of artboards to verify
        /// </summary>
        public class TestAssetData
        {
            public string addressableAssetPath;
            public int expectedArtboardCount;

            public EmbeddedAssetTestDataItem[] expectedReferencedAssetData;
        }

        /// <summary>
        /// Return a list of test Rive Asset data to load and test
        /// </summary>
        /// <returns></returns>
        protected abstract TestAssetData[] GetTestRiveAssetInfo();


        /// <summary>
        /// Return a list of test TextAsset data to load and test
        /// </summary>
        /// <returns></returns>
        protected abstract TestAssetData[] GetTestTextAssetInfo();

        [SetUp]
        public void Setup()
        {
            mockLogger = new MockLogger();
            DebugLogger.Instance = mockLogger;
        }

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            testAssetLoadingManager = new TestAssetLoadingManager();

        }

        [OneTimeTearDown]
        public void TearDown()
        {
            testAssetLoadingManager.UnloadAllAssets();
        }

        private IEnumerator LoadAssetAndTest<T>(TestAssetData testAssetInfo, Func<T, File> loadFunc) where T : UnityEngine.Object
        {
            T loadedAsset = default;
            yield return testAssetLoadingManager.LoadAssetCoroutine<T>(testAssetInfo.addressableAssetPath,
                (asset) => loadedAsset = asset,
                () => Assert.Fail($"Failed to load asset at {testAssetInfo.addressableAssetPath}"));

            Assert.IsNotNull(loadedAsset, $"No asset loaded at {testAssetInfo.addressableAssetPath}");

            File riveFile = loadFunc(loadedAsset);
            TestLoadedRiveFile(riveFile, testAssetInfo);
        }

        private void TestLoadedRiveFile(File riveFile, TestAssetData testAssetInfo)
        {
            Assert.IsNotNull(riveFile, $"Failed to load Rive file from {testAssetInfo.addressableAssetPath}");
            Assert.AreEqual(testAssetInfo.expectedArtboardCount, riveFile.ArtboardCount, "Artboard count mismatch");


            for (uint i = 0; i < riveFile.ArtboardCount; i++)
            {
                Artboard artboard = riveFile.Artboard(i);
                Assert.IsNotNull(artboard, $"Failed to load artboard at index {i}");
            }
        }

        /// <summary>
        /// Validate the embedded assets in the Rive file match the expected data during the asset loading callback
        /// </summary>
        /// <param name="testAssetInfo"></param>
        /// <param name="calledReferences"></param>
        private void ValidateEmbeddedAssets(TestAssetData testAssetInfo, List<EmbeddedAssetReference> calledReferences)
        {

            Assert.AreEqual(testAssetInfo.expectedReferencedAssetData.Length, calledReferences.Count,
                $"Mismatch in number of expected and actual asset references for {testAssetInfo.addressableAssetPath}");

            var expectedAssetDict = testAssetInfo.expectedReferencedAssetData.ToDictionary(a => a.ExpectedId, a => a);
            var actualAssetDict = calledReferences.ToDictionary(a => a.Id, a => a);

            foreach (var expectedPair in expectedAssetDict)
            {
                uint assetId = expectedPair.Key;
                var expectedAsset = expectedPair.Value;

                Assert.IsTrue(actualAssetDict.ContainsKey(assetId), $"No actual asset found with ID {assetId} for {testAssetInfo.addressableAssetPath}");
                var actualAsset = actualAssetDict[assetId];

                Assert.AreEqual(expectedAsset.ExpectedName, actualAsset.Name, $"Asset name mismatch for asset with ID {assetId} for {testAssetInfo.addressableAssetPath}");
                Assert.AreEqual(expectedAsset.ExpectedType, actualAsset.AssetType, $"Asset type mismatch for asset with ID {assetId} for {testAssetInfo.addressableAssetPath}");
                Assert.AreEqual(expectedAsset.ExpectedBytesSize, actualAsset.EmbeddededBytesSize, $"Asset bytes count mismatch for asset with ID {assetId} for {testAssetInfo.addressableAssetPath}");

            }

            // Check if there are any unexpected assets
            var unexpectedAssets = actualAssetDict.Keys.Except(expectedAssetDict.Keys);
            Assert.IsEmpty(unexpectedAssets, $"Unexpected assets found with IDs: {string.Join(", ", unexpectedAssets)} for {testAssetInfo.addressableAssetPath}");
        }


        [UnityTest]
        public IEnumerator Load_ValidRiveFile_ReturnsFileInstanceFromRiveAsset()
        {
            var assetInfoList = GetTestRiveAssetInfo();
            Assert.IsTrue(assetInfoList.Length > 0, "No asset info to load");
            List<Rive.EmbeddedAssetReference> embeddedAssetReferences = new List<Rive.EmbeddedAssetReference>();


            const float MAX_LOAD_TIME_IN_SECONDS = 0.5f;
            foreach (var testAssetInfo in assetInfoList)
            {
                File loadedFile = null;
                float startTime = Time.realtimeSinceStartup;
                yield return LoadAssetAndTest<Asset>(testAssetInfo, (asset) => loadedFile = File.Load(asset, (reference) =>
                {
                    embeddedAssetReferences.Add(reference);
                    return false;
                }));
                float loadTime = Time.realtimeSinceStartup - startTime;


                Assert.IsTrue(loadTime <= MAX_LOAD_TIME_IN_SECONDS,
                    $"File loading took too long ({loadTime} seconds) for asset {testAssetInfo.addressableAssetPath}. Max allowed time is {MAX_LOAD_TIME_IN_SECONDS} seconds.");

                ValidateEmbeddedAssets(testAssetInfo, embeddedAssetReferences);

                embeddedAssetReferences.Clear();

                loadedFile.Dispose();
            }
        }


        [UnityTest]
        public IEnumerator Load_ValidRiveFile_ReturnsFileInstanceFromBytes()
        {
            var assetInfoList = GetTestRiveAssetInfo();
            Assert.IsTrue(assetInfoList.Length > 0, "No asset info to load");
            List<Rive.EmbeddedAssetReference> embeddedAssetReferences = new List<Rive.EmbeddedAssetReference>();
            foreach (var testAssetInfo in assetInfoList)
            {
                File loadedFile = null;
                yield return LoadAssetAndTest<Asset>(testAssetInfo, (asset) => loadedFile = File.Load(asset.Bytes, (reference) =>
                {
                    embeddedAssetReferences.Add(reference);
                    return false;
                }));

                ValidateEmbeddedAssets(testAssetInfo, embeddedAssetReferences);

                embeddedAssetReferences.Clear();

                // Clean up
                loadedFile.Dispose();

            }

        }

        [UnityTest]
        public IEnumerator Load_ValidRiveFile_ReturnsFileInstanceFromTextAsset()
        {
            var assetInfoList = GetTestTextAssetInfo();
            Assert.IsTrue(assetInfoList.Length > 0, "No asset info to load");
            List<Rive.EmbeddedAssetReference> embeddedAssetReferences = new List<Rive.EmbeddedAssetReference>();

            foreach (var testAssetInfo in assetInfoList)
            {
                File loadedFile = null;
                yield return LoadAssetAndTest<TextAsset>(testAssetInfo, (asset) =>
                {

                    loadedFile = File.Load(asset, (reference) =>
                    {
                        embeddedAssetReferences.Add(reference);
                        return false;
                    });

                    return loadedFile;
                });

                ValidateEmbeddedAssets(testAssetInfo, embeddedAssetReferences);

                embeddedAssetReferences.Clear();

                // Clean up
                loadedFile.Dispose();

            }
        }

        [UnityTest]
        public IEnumerator Load_SameAssetWithoutCustomLoader_ReturnsSameInstance()
        {
            TestAssetData testAssetInfo = GetTestRiveAssetInfo()[0];
            Assert.IsNotNull(testAssetInfo, "No asset info to load");
            Asset riveAsset = null;

            yield return testAssetLoadingManager.LoadAssetCoroutine<Rive.Asset>(testAssetInfo.addressableAssetPath,
                (asset) => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {testAssetInfo.addressableAssetPath}"));

            Assert.IsNotNull(riveAsset, "No asset loaded");

            Rive.File riveFile1 = Rive.File.Load(riveAsset);
            yield return null;
            Rive.File riveFile2 = Rive.File.Load(riveAsset);
            yield return null;

            Rive.File riveFileFromBytes1 = Rive.File.Load(riveAsset.Bytes, 24);
            yield return null;
            Rive.File riveFileFromBytes2 = Rive.File.Load(riveAsset.Bytes, 24);
            yield return null;

            Assert.IsTrue(ReferenceEquals(riveFile1, riveFile2), "File instances should be the same when not using custom loader");
            Assert.IsTrue(ReferenceEquals(riveFileFromBytes1, riveFileFromBytes2), "File instances should be the same when loading from bytes and not using custom loader");

            riveFile1.Dispose();
            riveFile2.Dispose();
            riveFileFromBytes1.Dispose();
            riveFileFromBytes2.Dispose();
        }

        [UnityTest]
        public IEnumerator Load_SameAssetWithCustomLoader_ReturnsDifferentInstances()
        {
            TestAssetData testAssetInfo = GetTestRiveAssetInfo()[0];
            Assert.IsNotNull(testAssetInfo, "No asset info to load");
            Asset riveAsset = null;

            yield return testAssetLoadingManager.LoadAssetCoroutine<Rive.Asset>(testAssetInfo.addressableAssetPath,
                (asset) => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {testAssetInfo.addressableAssetPath}"));

            Assert.IsNotNull(riveAsset, "No asset loaded");

            CustomAssetLoaderCallback customLoader = (Rive.EmbeddedAssetReference assetReference) => false;

            Rive.File riveFile1 = Rive.File.Load(riveAsset, customLoader);
            yield return null;
            Rive.File riveFile2 = Rive.File.Load(riveAsset, customLoader);
            yield return null;

            Assert.IsFalse(ReferenceEquals(riveFile1, riveFile2), "File instances should be different when using custom loader");

            // Clean up
            riveFile1.Dispose();
            riveFile2.Dispose();
        }

        [UnityTest]
        public IEnumerator Load_MixedUsageWithAndWithoutCustomLoader_ReturnsDifferentInstances()
        {
            TestAssetData testAssetInfo = GetTestRiveAssetInfo()[0];
            Assert.IsNotNull(testAssetInfo, "No asset info to load");
            Asset riveAsset = null;

            yield return testAssetLoadingManager.LoadAssetCoroutine<Rive.Asset>(testAssetInfo.addressableAssetPath,
                (asset) => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {testAssetInfo.addressableAssetPath}"));

            Assert.IsNotNull(riveAsset, "No asset loaded");

            CustomAssetLoaderCallback customLoader = (Rive.EmbeddedAssetReference assetReference) => false;

            Rive.File riveFile1WithNoAssetLoader = Rive.File.Load(riveAsset);
            yield return null;
            Rive.File riveFile2WithAssetLoader = Rive.File.Load(riveAsset, customLoader);
            yield return null;

            Assert.IsFalse(ReferenceEquals(riveFile1WithNoAssetLoader, riveFile2WithAssetLoader), "File instances should be different between custom and non-custom loader usage");

            // Clean up
            riveFile1WithNoAssetLoader.Dispose();
            riveFile2WithAssetLoader.Dispose();
        }




        [UnityTest]
        public IEnumerator Load_InvalidRiveFile_ReturnsNull()
        {
            byte[] invalidBytes = new byte[] { 0, 1, 2, 3 };
            File invalidFile = File.Load(invalidBytes, null);
            Assert.IsTrue(mockLogger.AnyLogTypeContains(FileLoader.LogCodes.ERROR_RIVE_FILE_LOAD_FAILED));
            Assert.IsNull(invalidFile, "Loading invalid bytes should return null");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Load_InvalidRiveAsset_ReturnsNull()
        {

            Asset invalidAsset = ScriptableObject.CreateInstance<Asset>();
            File invalidFile = File.Load(invalidAsset);
            Assert.IsTrue(mockLogger.AnyLogTypeContains(FileLoader.LogCodes.ERROR_EMPTY_RIVE_FILE_BYTES));

            Assert.IsNull(invalidFile, "Loading invalid asset should return null");
            yield return null;
        }

        // MEMORY RELEASE CHECKS
        [UnityTest]
        public IEnumerator Load_RiveFile_ReleasesNativeFileWhenDisposed()
        {
            TestAssetData testAssetInfo = GetTestRiveAssetInfo()[0];
            Assert.IsNotNull(testAssetInfo, "No asset info to load");
            Asset riveAsset = null;

            yield return testAssetLoadingManager.LoadAssetCoroutine<Rive.Asset>(testAssetInfo.addressableAssetPath,
                (asset) => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {testAssetInfo.addressableAssetPath}"));



            Rive.File riveFile = Rive.File.Load(riveAsset);

            Assert.IsTrue(NativeFileInterface.isRiveFileValid(riveFile.NativeFile), "RiveFile should be valid immediately after it is loaded");

            yield return null;

            riveFile.Dispose();

            yield return null;

            Assert.IsFalse(NativeFileInterface.isRiveFileValid(riveFile.NativeFile), "RiveFile should be invalid after it is disposed");

        }

        [UnityTest]
        public IEnumerator Load_RiveFile_CalledMultipleTimesWithoutAssetLoader_ReleasesNativeFileWhenDisposed()
        {
            TestAssetData testAssetInfo = GetTestRiveAssetInfo()[0];
            Assert.IsNotNull(testAssetInfo, "No asset info to load");
            Asset riveAsset = null;

            yield return testAssetLoadingManager.LoadAssetCoroutine<Rive.Asset>(testAssetInfo.addressableAssetPath,
                (asset) => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {testAssetInfo.addressableAssetPath}"));

            Rive.File riveFile1 = Rive.File.Load(riveAsset);
            // Load the same asset again
            Rive.File riveFile2 = Rive.File.Load(riveAsset);


            Assert.IsTrue(ReferenceEquals(riveFile1, riveFile2), "File instances should be the same when loading the same asset multiple times without callback");

            Assert.IsTrue(NativeFileInterface.isRiveFileValid(riveFile1.NativeFile), "RiveFile should be valid immediately after it is loaded");
            Assert.IsTrue(NativeFileInterface.isRiveFileValid(riveFile2.NativeFile), "RiveFile should be valid immediately after it is loaded");

            yield return null;

            riveFile1.Dispose();

            yield return null;

            // Because we return the same file instance if the same asset is loaded multiple times, the native file should still be valid
            Assert.IsTrue(NativeFileInterface.isRiveFileValid(riveFile1.NativeFile), "RiveFile should be valid after it is disposed if there are other references to it");

            riveFile2.Dispose();

            yield return null;

            // Now that all references are disposed, the native file should be invalid
            Assert.IsFalse(NativeFileInterface.isRiveFileValid(riveFile1.NativeFile), "RiveFile should be invalid after it is disposed");


        }

        [UnityTest]
        public IEnumerator Load_RiveFile_CalledMultipleTimesWithAssetLoader_ReleasesNativeFileWhenDisposed()
        {
            TestAssetData testAssetInfo = GetTestRiveAssetInfo()[0];
            Assert.IsNotNull(testAssetInfo, "No asset info to load");
            Asset riveAsset = null;

            yield return testAssetLoadingManager.LoadAssetCoroutine<Rive.Asset>(testAssetInfo.addressableAssetPath,
                (asset) => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {testAssetInfo.addressableAssetPath}"));

            Rive.File riveFile1 = Rive.File.Load(riveAsset, (reference) => false);
            // Load the same asset again with a custom loader
            Rive.File riveFile2 = Rive.File.Load(riveAsset, (reference) => false);

            Assert.IsFalse(ReferenceEquals(riveFile1, riveFile2), "File instances should be different when loading the same asset multiple times with callback");

            Assert.IsTrue(NativeFileInterface.isRiveFileValid(riveFile1.NativeFile), "RiveFile should be valid immediately after it is loaded");
            Assert.IsTrue(NativeFileInterface.isRiveFileValid(riveFile2.NativeFile), "RiveFile should be valid immediately after it is loaded");

            yield return null;

            riveFile1.Dispose();

            yield return null;

            // Because we return different file instances if the same asset is loaded multiple times with a callback, the native file should not be valid
            Assert.IsFalse(NativeFileInterface.isRiveFileValid(riveFile1.NativeFile), "RiveFile should be invalid after it is disposed");

            riveFile2.Dispose();

            yield return null;

            Assert.IsFalse(NativeFileInterface.isRiveFileValid(riveFile2.NativeFile), "RiveFile should be invalid after it is disposed");
        }


        [UnityTest]
        public IEnumerator Load_RiveFile_ShouldReleaseFile_IfNotDisposedExplicitly()
        {
            TestAssetData testAssetInfo = GetTestRiveAssetInfo()[0];
            Assert.IsNotNull(testAssetInfo, "No asset info to load");
            Asset riveAsset = null;

            yield return testAssetLoadingManager.LoadAssetCoroutine<Rive.Asset>(testAssetInfo.addressableAssetPath,
                (asset) => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {testAssetInfo.addressableAssetPath}"));



            Rive.File riveFile = Rive.File.Load(riveAsset);

            IntPtr nativeFile = riveFile.NativeFile;
            Assert.IsTrue(NativeFileInterface.isRiveFileValid(nativeFile), "RiveFile should be alive immediately after it is loaded");

            // Load the same asset again to increase the ref count
            Rive.File riveFile2 = Rive.File.Load(riveAsset);

            yield return null;

            // We explicitly cleanup the second file to simulate a case where the user only disposes the file once
            riveFile2.Dispose();
            riveFile2 = null;

            yield return null;

            Assert.IsTrue(NativeFileInterface.isRiveFileValid(nativeFile), "RiveFile should be alive after one reference is disposed");

            yield return null;


            TriggerFinalizer(riveFile);

            Assert.IsFalse(NativeFileInterface.isRiveFileValid(nativeFile), "The native file should be disposed after Unity RiveFile is garbage collected if not disposed explicitly");

        }

        [UnityTest]
        public IEnumerator Load_RiveFile_ShouldReleaseInternallyLoadedOobAssets_WhenDisposed()
        {
            //Get an item with embedded assets and out of band assets are not null
            TestAssetData testAssetInfo = GetTestRiveAssetInfo().FirstOrDefault(item => item.expectedReferencedAssetData.Length > 0 && item.expectedReferencedAssetData.Any(a => a.ExpectedBytesSize <= 0 && a.ExpectedType == EmbeddedAssetType.Image));
            Assert.IsNotNull(testAssetInfo, "No asset info to load. Make sure you have at least one RiveFile with referenced assets");

            Asset riveAsset = null;

            yield return testAssetLoadingManager.LoadAssetCoroutine<Rive.Asset>(testAssetInfo.addressableAssetPath,
                (asset) => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {testAssetInfo.addressableAssetPath}"));



            Rive.File riveFile = Rive.File.Load(riveAsset);


            var oobAssets = riveAsset.EmbeddedAssets.Select(e => e.OutOfBandAsset).Where(a => a != null).ToList();

            Assert.IsTrue(oobAssets.Count > 0, "Embedded assets should have out of band assets");

            var assetToCheck = oobAssets[0];


            Assert.IsTrue(assetToCheck.RefCount() > 0, " Embedded assets should be loaded when the RiveFile is loaded");

            yield return null;

            riveFile.Dispose();

            yield return null;

            Assert.IsFalse(assetToCheck.RefCount() > 0, "Embedded assets should be unloaded when the RiveFile is released");


        }


        private void TriggerFinalizer(File file)
        {
            System.Type type = file.GetType();
            System.Reflection.MethodInfo method = type.GetMethod("Finalize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method.Invoke(file, null);
        }



        // INVALID ACCESS CHECKS
        [UnityTest]
        public IEnumerator AccessingDisposedFile_LogsErrors()
        {
            TestAssetData testAssetInfo = GetTestRiveAssetInfo()[0];
            Assert.IsNotNull(testAssetInfo, "No asset info to load");
            Asset riveAsset = null;

            yield return testAssetLoadingManager.LoadAssetCoroutine<Rive.Asset>(testAssetInfo.addressableAssetPath,
                (asset) => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {testAssetInfo.addressableAssetPath}"));



            Rive.File riveFile = Rive.File.Load(riveAsset);
            riveFile.Dispose();

            yield return null;

            Assert.IsFalse(riveFile.IsNativeFileValid());

            // Test ArtboardCount
            Assert.AreEqual(0, riveFile.ArtboardCount);
            Assert.IsTrue(mockLogger.AnyLogTypeContains(LogCodes.ERROR_INVALID_FILE_ACCESS));

            // Test ArtboardName
            Assert.IsNull(riveFile.ArtboardName(0));
            Assert.IsTrue(mockLogger.AnyLogTypeContains(LogCodes.ERROR_INVALID_FILE_ACCESS));

            // Test Artboard(uint index)
            Assert.IsNull(riveFile.Artboard(0));
            Assert.IsTrue(mockLogger.AnyLogTypeContains(LogCodes.ERROR_INVALID_FILE_ACCESS));

            // Test Artboard(string name)
            Assert.IsNull(riveFile.Artboard("NonExistentArtboard"));
            Assert.IsTrue(mockLogger.AnyLogTypeContains(LogCodes.ERROR_INVALID_FILE_ACCESS));
        }

    }


}