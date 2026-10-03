using System;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using Rive.Tests.Utils;
using Rive.Utils;

namespace Rive.Tests
{
    /// <summary>
    /// Tests that loading and unloading OutOfBand assets works as expected. 
    /// </summary>
    public class OutOfBandAssetTests
    {
        private MockLogger mockLogger;

        [SetUp]
        public void Setup()
        {
            mockLogger = new MockLogger();
            DebugLogger.Instance = mockLogger;
        }

        [TearDown]
        public void TearDown()
        {
            DebugLogger.Instance = null;
        }

        [Test]
        public void Create_WithValidBytes_ReturnsInstance()
        {
            byte[] testBytes = new byte[] { 1, 2, 3, 4 };
            var fontAsset = OutOfBandAsset.Create<FontOutOfBandAsset>(testBytes);
            Assert.IsNotNull(fontAsset);
            Assert.AreEqual(testBytes, fontAsset.Bytes);
        }

        [Test]
        public void Load_IncrementsRefCount()
        {
            var imageAsset = OutOfBandAsset.Create<ImageOutOfBandAsset>(new byte[] { 1, 2, 3, 4 });
            imageAsset.Load();
            Assert.AreEqual(1, imageAsset.RefCount());

            imageAsset.Unload();
        }

        [Test]
        public void Unload_DecrementsRefCount()
        {
            var audioAsset = OutOfBandAsset.Create<AudioOutOfBandAsset>(new byte[] { 1, 2, 3, 4 });
            audioAsset.Load();
            audioAsset.Unload();
            Assert.AreEqual(0, audioAsset.RefCount());
        }

        [Test]
        public void MultipleLoadUnload_MaintainsCorrectRefCount()
        {
            var fontAsset = OutOfBandAsset.Create<FontOutOfBandAsset>(new byte[] { 1, 2, 3, 4 });
            fontAsset.Load();
            fontAsset.Load();
            Assert.AreEqual(2, fontAsset.RefCount());
            fontAsset.Unload();
            Assert.AreEqual(1, fontAsset.RefCount());
            fontAsset.Unload();
            Assert.AreEqual(0, fontAsset.RefCount());
        }

        [Test]
        public void Unload_WhenNotLoaded_DoesNotThrowException()
        {
            var imageAsset = OutOfBandAsset.Create<ImageOutOfBandAsset>(new byte[] { 1, 2, 3, 4 });
            Assert.DoesNotThrow(() => imageAsset.Unload());
        }

        /// <summary>
        /// Regression test for "1002 - The provided asset is not loaded in memory".
        /// Ensures Load() always recreates the native asset, even after an extra Unload() call.
        /// </summary>
        [Test]
        public void Load_AfterTooManyUnload_ReinitializesNativeAsset()
        {
            var validNativePointer = new NativeAssetHandle(12345);
            var asset = OutOfBandAsset.Create<TestOutOfBandAsset>(new byte[] { 1, 2, 3, 4 });
            asset.TestNativeAsset = validNativePointer;

            asset.Load();   // refCount 0 -> 1, native created
            asset.Unload(); // refCount 1 -> 0, native freed (set to Zero)
            asset.Unload(); // extra Unload is clamped, refCount stays 0, native still Zero

            asset.Load();   // native is null, so it's recreated regardless of ref count

            Assert.IsTrue(asset.NativeHandle.IsValid,
                "After Load(), the native asset must be valid so SetFont/SetImage does not fail with error 1002.");
            Assert.AreEqual(1, asset.RefCount());
        }

        [Test]
        public void Load_WithNoBytes_LogsErrorAndDoesNotLoad()
        {
            var asset = ScriptableObject.CreateInstance<TestOutOfBandAsset>();
            asset.TestNativeAsset = new NativeAssetHandle(12345);

            asset.Load();

            Assert.IsFalse(asset.NativeHandle.IsValid);
            Assert.AreEqual(0, asset.RefCount());
            Assert.IsTrue(mockLogger.LoggedErrorsContains("no serialized bytes"));
        }

        private class TestOutOfBandAsset : OutOfBandAsset
        {
            public NativeAssetHandle TestNativeAsset { get; set; }

            internal override ulong SendDecode(ulong requestId, byte[] bytes)
            {
                ReplyDecoded(requestId);
                return TestNativeAsset.Value;
            }

            internal override void DeleteNative(ulong handle) { }
            internal override EmbeddedAssetType AssetType => EmbeddedAssetType.Image;

        }

        private class CountingOutOfBandAsset : OutOfBandAsset
        {
            public int Loads;
            public int Unloads;

            internal override ulong SendDecode(ulong requestId, byte[] bytes)
            {
                Interlocked.Increment(ref Loads);
                ReplyDecoded(requestId);
                return 12345;
            }

            internal override void DeleteNative(ulong handle)
            {
                Interlocked.Increment(ref Unloads);
            }

            internal override EmbeddedAssetType AssetType => EmbeddedAssetType.Image;
        }

        // Answers the way a decode that worked does.
        private static void ReplyDecoded(ulong requestId)
        {
            byte[] ok = BitConverter.GetBytes(1u);
            Rive.Host.HostNative.riveHostEcho(requestId, ok, (uint)ok.Length);
        }

        private static void RunOnThreads(int count, ThreadStart work)
        {
            var threads = new Thread[count];
            for (int i = 0; i < count; i++)
            {
                threads[i] = new Thread(work);
            }
            foreach (var thread in threads)
            {
                thread.Start();
            }
            foreach (var thread in threads)
            {
                thread.Join();
            }
        }

        [NeedsManagedThreads]
        [NeedsRiveThread]
        [Test]
        public void Load_FromManyThreads_DecodesOnce()
        {
            var asset = OutOfBandAsset.Create<CountingOutOfBandAsset>(new byte[] { 1, 2, 3, 4 });

            RunOnThreads(8, () => asset.Load());

            Assert.AreEqual(1, asset.Loads, "Only the first Load should decode.");
            Assert.AreEqual(8, asset.RefCount());
        }

        [NeedsManagedThreads]
        [NeedsRiveThread]
        [Test]
        public void LoadAndUnload_FromManyThreads_KeepTheCountRight()
        {
            var asset = OutOfBandAsset.Create<CountingOutOfBandAsset>(new byte[] { 1, 2, 3, 4 });

            RunOnThreads(8, () =>
            {
                for (int i = 0; i < 1000; i++)
                {
                    asset.Load();
                    asset.Unload();
                }
            });

            Assert.AreEqual(0, asset.RefCount());
            Assert.IsFalse(asset.NativeHandle.IsValid);
            Assert.AreEqual(asset.Loads, asset.Unloads, "Every decode should be released exactly once.");
        }

        [Test]
        public void LoadIntoByteAssetMap_WithValidNativeAsset_AddsCorrectBytesToMap()
        {
            var asset = OutOfBandAsset.Create<TestOutOfBandAsset>(new byte[] { 1, 2, 3, 4 });
            asset.TestNativeAsset = new NativeAssetHandle(12345);
            asset.Load(); // This will set the NativeHandle

            // We're using an arbitrary ID for testing. This could be any uint value.
            uint embeddedAssetId = 1000;
            EmbeddedAssetType embeddedAssetType = EmbeddedAssetType.Image;
            var assetMap = new List<byte>();

            asset.LoadIntoByteAssetMap(embeddedAssetId, embeddedAssetType, assetMap);

            // Now we need to check that the asset map contains the correct bytes

            var expectedIdBytes = BitConverter.GetBytes(embeddedAssetId);
            var expectedTypeBytes = BitConverter.GetBytes((ushort)embeddedAssetType);

            var expectedHandleBytes = BitConverter.GetBytes(asset.NativeHandle.Value);

            int expectedTotalSizeInBytes = expectedIdBytes.Length + expectedTypeBytes.Length + expectedHandleBytes.Length;

            Assert.AreEqual(expectedTotalSizeInBytes, assetMap.Count,
                $"Expected {expectedTotalSizeInBytes} bytes, but got {assetMap.Count}");

            // Check that the embedded asset ID is correct
            // The first few bytes in the asset map should represent our embeddedAssetId
            CollectionAssert.AreEqual(expectedIdBytes, assetMap.GetRange(0, expectedIdBytes.Length));

            // Check that the embedded asset type is correct
            // The next couple of bytes should represent our embeddedAssetType
            CollectionAssert.AreEqual(expectedTypeBytes, assetMap.GetRange(expectedIdBytes.Length, expectedTypeBytes.Length));

            // The last eight bytes are the handle
            var handleStartIndex = expectedIdBytes.Length + expectedTypeBytes.Length;
            CollectionAssert.AreEqual(expectedHandleBytes, assetMap.GetRange(handleStartIndex, expectedHandleBytes.Length));
        }


        [Test]
        public void LoadIntoByteAssetMap_WithUnloadedNativeAsset_DoesNotAddToMap()
        {
            var asset = OutOfBandAsset.Create<TestOutOfBandAsset>(new byte[] { 1, 2, 3, 4 });
            asset.TestNativeAsset = default;
            asset.Load();

            uint embeddedAssetId = 42;
            EmbeddedAssetType embeddedAssetType = EmbeddedAssetType.Image;
            var assetMap = new List<byte>();

            asset.LoadIntoByteAssetMap(embeddedAssetId, embeddedAssetType, assetMap);

            Assert.AreEqual(0, assetMap.Count);
        }


        [Test]
        public void LoadIntoByteAssetMap_WithDifferentAssetTypes_AddsCorrectType()
        {
            var asset = OutOfBandAsset.Create<TestOutOfBandAsset>(new byte[] { 1, 2, 3, 4 });
            asset.TestNativeAsset = new NativeAssetHandle(12345);
            asset.Load();

            uint embeddedAssetId = 42;
            var assetMap = new List<byte>();

            const int EmbeddedAssetIdSize = sizeof(uint);
            const int AssetTypeSize = sizeof(ushort);

            foreach (EmbeddedAssetType assetType in Enum.GetValues(typeof(EmbeddedAssetType)))
            {
                assetMap.Clear();
                asset.LoadIntoByteAssetMap(embeddedAssetId, assetType, assetMap);

                byte[] actualAssetTypeBytes = assetMap.GetRange(EmbeddedAssetIdSize, AssetTypeSize).ToArray();
                byte[] expectedAssetTypeBytes = BitConverter.GetBytes((ushort)assetType);

                CollectionAssert.AreEqual(expectedAssetTypeBytes, actualAssetTypeBytes,
                    $"Asset type bytes do not match for {assetType}");
            }
        }

    }
}