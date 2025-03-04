using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rive.Tests.Utils;
using Rive.Utils;

namespace Rive.Tests
{
    public class FallbackFileAssetLoaderTests
    {
        private class MockFileAssetLoader : IFileAssetLoader
        {
            private bool m_loadContentsReturnValue = false;
            private int m_loadContentsCallCount = 0;

            public int LoadContentsCallCount => m_loadContentsCallCount;
            public bool LoadContents(EmbeddedAssetReference assetReference)
            {
                m_loadContentsCallCount++;
                return m_loadContentsReturnValue;
            }
            public MockFileAssetLoader(bool loadContentsReturnValue = false)
            {
                m_loadContentsReturnValue = loadContentsReturnValue;
            }

            public MockFileAssetLoader()
            {
            }


        }


        private MockLogger mockLogger;
        private FallbackFileAssetLoader fallbackLoader;

        private List<OutOfBandAsset> createdAssets = new List<OutOfBandAsset>();

        private OutOfBandAsset CreateOutOfBandAsset<T>(byte[] bytes) where T : OutOfBandAsset
        {
            var asset = OutOfBandAsset.Create<T>(bytes);
            createdAssets.Add(asset);
            return asset;
        }

        [SetUp]
        public void Setup()
        {
            mockLogger = new MockLogger();
            DebugLogger.Instance = mockLogger;
            fallbackLoader = new FallbackFileAssetLoader();
        }

        [TearDown]
        public void Teardown()
        {
            foreach (var asset in createdAssets)
            {
                asset.Unload();
            }
            DebugLogger.Instance = null;
        }


        [Test]
        public void AddLoader_ShouldAddLoaderToList()
        {
            var mockLoader = new MockFileAssetLoader();
            fallbackLoader.AddLoader(mockLoader);
            Assert.Contains(mockLoader, fallbackLoader.Loaders);
        }

        [Test]
        public void GenerateAssetMapBytesFromEmbeddedAssets_ShouldLoadOutOfBandAssets()
        {
            var mockAsset = CreateOutOfBandAsset<ImageOutOfBandAsset>(new byte[100]);

            EmbeddedAssetData[] embeddedAssetData = new EmbeddedAssetData[]
            {
                new EmbeddedAssetData(EmbeddedAssetType.Image, 1u, "test", 100u) { OutOfBandAsset = mockAsset }
            };

            fallbackLoader.GenerateAssetMapBytesFromEmbeddedAssets(embeddedAssetData);

            Assert.AreEqual(mockAsset.NativeAsset, fallbackLoader.GetLoadedOobAsset(1).NativeAsset);

        }

        [Test]
        public void GenerateAssetMapBytesFromEmbeddedAssets_ShouldNotReloadOutOfBandAsset_IfAlreadyInCache()
        {
            var mockAsset = CreateOutOfBandAsset<ImageOutOfBandAsset>(new byte[100]);

            EmbeddedAssetData[] embeddedAssetData = new EmbeddedAssetData[]
            {
                new EmbeddedAssetData(EmbeddedAssetType.Image, 1u, "test", 100u) { OutOfBandAsset = mockAsset }
            };

            Assert.AreEqual(mockAsset.RefCount(), 0);
            Assert.IsNull(fallbackLoader.GetLoadedOobAsset(embeddedAssetData[0].Id));

            fallbackLoader.GenerateAssetMapBytesFromEmbeddedAssets(embeddedAssetData);

            Assert.AreEqual(mockAsset.NativeAsset, fallbackLoader.GetLoadedOobAsset(1).NativeAsset);

            Assert.AreEqual(mockAsset.RefCount(), 1);

            fallbackLoader.GenerateAssetMapBytesFromEmbeddedAssets(embeddedAssetData);

            Assert.AreEqual(mockAsset.NativeAsset, fallbackLoader.GetLoadedOobAsset(1).NativeAsset);

            Assert.AreEqual(mockAsset.RefCount(), 1);

            EmbeddedAssetReference.InitializationData initializationData = new EmbeddedAssetReference.InitializationData(EmbeddedAssetType.Image, 1, "test", 100u, 0, mockAsset);
            // We check that if the asset is already loaded, it is not loaded again in the LoadContents method after the Rive File is loaded.
            fallbackLoader.LoadContents(new ImageEmbeddedAssetReference(initializationData));

            Assert.AreEqual(mockAsset.RefCount(), 1);

        }

        [Test]
        public void LoadContents_ShouldTryOtherLoadersFirst()
        {
            var mockLoader = new MockFileAssetLoader(true);
            fallbackLoader.AddLoader(mockLoader);

            EmbeddedAssetReference.InitializationData initializationData = new EmbeddedAssetReference.InitializationData(EmbeddedAssetType.Image, 1, "test", 100u, 0, null);

            var assetReference = new ImageEmbeddedAssetReference(initializationData);


            Assert.IsTrue(fallbackLoader.LoadContents(assetReference));

            Assert.AreEqual(1, mockLoader.LoadContentsCallCount);
        }

        [Test]
        public void LoadContents_ShouldLoadOutOfBandAsset_WhenOtherLoadersOptOut()
        {
            var mockAsset = CreateOutOfBandAsset<ImageOutOfBandAsset>(new byte[100]);

            EmbeddedAssetReference.InitializationData initializationData = new EmbeddedAssetReference.InitializationData(EmbeddedAssetType.Image, 1, "test", 100u, 0, mockAsset);
            var assetReference = new ImageEmbeddedAssetReference(initializationData);

            // Setup the first loader to fail
            var mockLoader = new MockFileAssetLoader(false);
            fallbackLoader.AddLoader(mockLoader);

            Assert.IsTrue(fallbackLoader.LoadContents(assetReference));
            Assert.AreEqual(mockAsset.NativeAsset, fallbackLoader.GetLoadedOobAsset(1).NativeAsset);
        }

        [Test]
        public void NativeUnityAssetLoaderCallback_ShouldCreateAssetReference()
        {
            var result = fallbackLoader.NativeUnityAssetLoaderCallback(1u, (ushort)EmbeddedAssetType.Image, "test", 100u);


            var assetReference = fallbackLoader.GetAssetReference(1);
            Assert.IsNotNull(assetReference);
            Assert.IsTrue(assetReference is ImageEmbeddedAssetReference);

            Assert.AreEqual(1u, assetReference.Id);
            Assert.AreEqual(EmbeddedAssetType.Image, assetReference.AssetType);

        }



        [Test]
        public void NativeUnityAssetLoaderCallback_ShouldReturnNullAsset_ForUnsupportedType()
        {
            var result = fallbackLoader.NativeUnityAssetLoaderCallback(1u, 9999, "test", 100u);


            Assert.AreEqual(IntPtr.Zero, result);
        }



        [Test]
        public void UnloadOutOfBandAssets_ShouldUnloadAllAssets()
        {
            var mockAsset1 = CreateOutOfBandAsset<ImageOutOfBandAsset>(new byte[100]);
            var mockAsset2 = CreateOutOfBandAsset<ImageOutOfBandAsset>(new byte[200]);

            EmbeddedAssetData[] embeddedAssetData = new EmbeddedAssetData[]
            {
                new EmbeddedAssetData(EmbeddedAssetType.Image, 1, "test", 100u) { OutOfBandAsset = mockAsset1 },
                new EmbeddedAssetData(EmbeddedAssetType.Image, 2, "test", 100u) { OutOfBandAsset = mockAsset2 }
            };

            fallbackLoader.GenerateAssetMapBytesFromEmbeddedAssets(embeddedAssetData);

            Assert.IsNotNull(fallbackLoader.GetLoadedOobAsset(1));
            Assert.IsNotNull(fallbackLoader.GetLoadedOobAsset(2));


            fallbackLoader.UnloadInternallyLoadedAssets();

            Assert.IsNull(fallbackLoader.GetLoadedOobAsset(1));
            Assert.IsNull(fallbackLoader.GetLoadedOobAsset(2));

            Assert.IsNull(fallbackLoader.GetLoadedOobAsset(1));
            Assert.IsNull(fallbackLoader.GetLoadedOobAsset(2));
        }

        [Test]
        public void LoadContents_ShouldReturnTrue_WithCorrectAssetType()
        {
            var mockFontAsset = CreateOutOfBandAsset<FontOutOfBandAsset>(new byte[100]);

            EmbeddedAssetReference.InitializationData fontInitializationData = new EmbeddedAssetReference.InitializationData(EmbeddedAssetType.Font, 1, "test", 100u, 0, mockFontAsset);
            var fontReference = new FontEmbeddedAssetReference(fontInitializationData);


            fallbackLoader.LoadContents(fontReference);
            Assert.AreEqual(mockFontAsset.NativeAsset, fallbackLoader.GetLoadedOobAsset(1).NativeAsset);

            var mockImageAsset = CreateOutOfBandAsset<ImageOutOfBandAsset>(new byte[100]);

            EmbeddedAssetReference.InitializationData imageInitializationData = new EmbeddedAssetReference.InitializationData(EmbeddedAssetType.Image, 2, "test", 100u, 0, mockImageAsset);

            var imageReference = new ImageEmbeddedAssetReference(imageInitializationData);



            Assert.IsTrue(fallbackLoader.LoadContents(imageReference));
            Assert.AreEqual(mockImageAsset.NativeAsset, fallbackLoader.GetLoadedOobAsset(2).NativeAsset);

            var mockAudioAsset = CreateOutOfBandAsset<AudioOutOfBandAsset>(new byte[120]);

            EmbeddedAssetReference.InitializationData audioInitializationData = new EmbeddedAssetReference.InitializationData(EmbeddedAssetType.Audio, 3, "test", 100u, 0, mockAudioAsset);

            var audioReference = new AudioEmbeddedAssetReference(audioInitializationData);

            Assert.IsTrue(fallbackLoader.LoadContents(audioReference));
            Assert.AreEqual(mockAudioAsset.NativeAsset, fallbackLoader.GetLoadedOobAsset(3).NativeAsset);
        }

        [Test]
        public void LoadContents_ShouldReturnFalse_ForMismatchedTypes()
        {
            var mockImageAsset = CreateOutOfBandAsset<ImageOutOfBandAsset>(new byte[100]);

            EmbeddedAssetReference.InitializationData initializationData = new EmbeddedAssetReference.InitializationData(EmbeddedAssetType.Font, 1, "test", 100u, 0, mockImageAsset);

            var fontReference = new FontEmbeddedAssetReference(initializationData);

            Assert.IsFalse(fallbackLoader.LoadContents(fontReference));
            Assert.IsTrue(mockLogger.LoggedErrors.Exists(s => s.Contains(FallbackFileAssetLoader.LogCodes.ERROR_ASSET_TYPE_MISMATCH)));
        }


    }
}