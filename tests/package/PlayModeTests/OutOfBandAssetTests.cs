using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Rive.Tests
{
    /// <summary>
    /// Tests that loading and unloading OutOfBand assets works as expected. 
    /// </summary>
    public class OutOfBandAssetTests
    {
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

        private class TestOutOfBandAsset : OutOfBandAsset
        {
            public IntPtr TestNativeAsset { get; set; }

            protected override IntPtr LoadNative(byte[] bytes) => TestNativeAsset;
            protected override void UnloadNative(IntPtr nativePtr) { }

            public new IntPtr NativeAsset => base.NativeAsset;
        }

        [Test]
        public void LoadIntoByteAssetMap_WithValidNativeAsset_AddsCorrectBytesToMap()
        {
            var asset = ScriptableObject.CreateInstance<TestOutOfBandAsset>();
            asset.TestNativeAsset = new IntPtr(12345);
            asset.Load(); // This will set the NativeAsset

            // We're using an arbitrary ID for testing. This could be any uint value.
            uint embeddedAssetId = 1000;
            EmbeddedAssetType embeddedAssetType = EmbeddedAssetType.Image;
            var assetMap = new List<byte>();

            asset.LoadIntoByteAssetMap(embeddedAssetId, embeddedAssetType, assetMap);

            // Now we need to check that the asset map contains the correct bytes

            var expectedIdBytes = BitConverter.GetBytes(embeddedAssetId);
            var expectedTypeBytes = BitConverter.GetBytes((ushort)embeddedAssetType);

#if UNITY_WEBGL && !UNITY_EDITOR
    // For WebGL, we always use a 32-bit integer to represent the pointer
    var expectedPointerBytes = BitConverter.GetBytes((int)asset.NativeAsset);
#else
            // For other platforms, we use nint (native int), which varies based on architecture (32-bit or 64-bit)
            var expectedPointerBytes = BitConverter.GetBytes((nint)asset.NativeAsset);
#endif

            int expectedTotalSizeInBytes = expectedIdBytes.Length + expectedTypeBytes.Length + expectedPointerBytes.Length;

            Assert.AreEqual(expectedTotalSizeInBytes, assetMap.Count,
                $"Expected {expectedTotalSizeInBytes} bytes, but got {assetMap.Count}");

            // Check that the embedded asset ID is correct
            // The first few bytes in the asset map should represent our embeddedAssetId
            CollectionAssert.AreEqual(expectedIdBytes, assetMap.GetRange(0, expectedIdBytes.Length));

            // Check that the embedded asset type is correct
            // The next couple of bytes should represent our embeddedAssetType
            CollectionAssert.AreEqual(expectedTypeBytes, assetMap.GetRange(expectedIdBytes.Length, expectedTypeBytes.Length));

            // The remaining bytes should represent our pointer
            var pointerStartIndex = expectedIdBytes.Length + expectedTypeBytes.Length;
            CollectionAssert.AreEqual(expectedPointerBytes, assetMap.GetRange(pointerStartIndex, expectedPointerBytes.Length));
        }


        [Test]
        public void LoadIntoByteAssetMap_WithUnloadedNativeAsset_DoesNotAddToMap()
        {
            var asset = ScriptableObject.CreateInstance<TestOutOfBandAsset>();
            asset.TestNativeAsset = IntPtr.Zero;
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
            var asset = ScriptableObject.CreateInstance<TestOutOfBandAsset>();
            asset.TestNativeAsset = new IntPtr(12345);
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