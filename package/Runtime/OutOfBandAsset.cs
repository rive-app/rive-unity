using UnityEngine;
using System;
using System.Collections.Generic;
using Rive.Utils;

namespace Rive
{
    /// <summary>
    /// Interface for out-of-band assets.
    /// </summary>
    public interface IOutOfBandAsset
    {
        void Load();
        void Unload();
    }

    /// <summary>
    /// Represents an out-of-band Rive asset.
    ///
    /// Out-of-band assets are assets that are referenced in a Rive asset, but are not
    /// part of the Rive asset itself. For example, images and fonts can be marked as
    /// referenced and linked separately.
    /// </summary>
    public abstract class OutOfBandAsset : ScriptableObject, IOutOfBandAsset
    {
        [HideInInspector]
        [SerializeField]
        private byte[] bytes;

        // Guards the count and the handle, so Load and Unload can come from any thread.
        [NonSerialized]
        private readonly object m_lock = new object();

        [NonSerialized]
        private int m_refCount;

        [NonSerialized]
        private NativeAssetHandle m_nativeHandle;

        // The decode that's out for the current handle.
        private sealed class Decode
        {
            internal volatile bool Landed;
            internal bool Ok;
            internal readonly FutureState<bool> State = new FutureState<bool>();
        }

        [NonSerialized]
        private Decode m_decode;

        /// Queues the decode with requestId and returns its handle.
        internal abstract ulong SendDecode(ulong requestId, byte[] bytes);
        internal abstract void DeleteNative(ulong handle);

        /// <summary>
        /// Which kind of embedded asset this one can stand in for.
        /// </summary>
        internal abstract EmbeddedAssetType AssetType { get; }

        internal NativeAssetHandle NativeHandle
        {
            get { lock (m_lock) { return m_nativeHandle; } }
        }

        /// <summary>
        /// The raw bytes of the out-of-band asset.
        /// </summary>
        public byte[] Bytes { get { return bytes; } }

        /// <summary>
        /// Create an out-of-band asset instance from the given bytes.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="bytes"></param>
        /// <returns> The created out-of-band asset instance. </returns>
        public static T Create<T>(byte[] bytes) where T : OutOfBandAsset
        {
            var asset = ScriptableObject.CreateInstance<T>();
            asset.Init(bytes);
            return asset;
        }

        private void Init(byte[] bytes)
        {
            this.bytes = bytes;
        }

        internal void LoadIntoByteAssetMap(uint embeddedAssetId, EmbeddedAssetType embeddedAssetType, List<byte> assetMap)
        {
            // Write it into the asset map if the native asset was succesfully loaded.
            NativeAssetHandle handle = NativeHandle;
            if (handle.IsValid)
            {
                var bytes = BitConverter.GetBytes(embeddedAssetId);
                for (int j = 0; j < bytes.Length; j++)
                {
                    assetMap.Add(bytes[j]);
                }
                bytes = BitConverter.GetBytes((ushort)embeddedAssetType);
                for (int j = 0; j < bytes.Length; j++)
                {
                    assetMap.Add(bytes[j]);
                }
                bytes = BitConverter.GetBytes(handle.Value);
                for (int j = 0; j < bytes.Length; j++)
                {
                    assetMap.Add(bytes[j]);
                }
            }
        }


        /// <summary>
        /// Load the out-of-band asset. Call this before using the asset.
        /// </summary>
        /// <remarks>
        /// Waits for the decode. Handle code, in a BackgroundThread panel, uses <see cref="LoadAsync"/>.
        /// </remarks>
        public void Load()
        {
            if (Retain())
            {
                // A second caller waits on the first one's decode.
                Decode decode = StartDecode();
                OutOfBandAssetNative.Wait(() => decode.Landed);
            }
        }

        /// Load without waiting for the decode. Its handle is good to send
        /// straight away, since commands run in order.
        internal void LoadWithoutWaiting()
        {
            if (Retain())
            {
                StartDecode();
            }
        }

        /// <summary>
        /// Loads the out-of-band asset without waiting. Pair it with <see cref="Unload"/> like <see cref="Load"/>.
        /// </summary>
        /// <remarks>
        /// A handle's <c>SetValue(asset)</c> can follow straight away; it runs after the decode.
        /// </remarks>
        /// <returns>An operation that finishes on the main thread once the asset is ready. It fails with <see cref="RiveErrorCode.LoadFailed"/> if it has no bytes or a font can't be decoded. Images decode when they're first drawn, so a bad image isn't caught here.</returns>
        public Future LoadAsync()
        {
            var state = new FutureState<bool>();
            if (!Retain())
            {
                state.Fail(new RiveException(RiveErrorCode.LoadFailed, $"Out-of-band asset '{name}' has no bytes."));
                return new Future(state);
            }
            new Future<bool>(StartDecode().State).Completed += decode =>
            {
                if (decode.Status != FutureStatus.Succeeded)
                {
                    state.Fail(decode.Exception ?? new RiveException(RiveErrorCode.LoadFailed, "The decode was cancelled."));
                }
                else if (!decode.Result)
                {
                    state.Fail(new RiveException(RiveErrorCode.LoadFailed, $"Out-of-band asset '{name}' couldn't be decoded."));
                }
                else
                {
                    state.Succeed(true);
                }
            };
            return new Future(state);
        }

        /// Counts a use. False, with an error, when there's nothing to decode.
        internal bool Retain()
        {
            if (bytes == null || bytes.Length == 0)
            {
                DebugLogger.Instance.LogError("Cannot load out-of-band asset: no serialized bytes are present.");
                return false;
            }
            lock (m_lock)
            {
                if (m_refCount < 0)
                {
                    m_refCount = 0;
                }
                m_refCount++;
            }
            return true;
        }

        /// For a caller that already holds a retain.
        internal void DecodeIfNeeded()
        {
            StartDecode();
        }

        /// Sends the decode unless one is already out for the current handle.
        private Decode StartDecode()
        {
            lock (m_lock)
            {
                if (m_decode != null)
                {
                    return m_decode;
                }
                var decode = new Decode();
                ulong handle = OutOfBandAssetNative.Decode(
                    id => SendDecode(id, bytes),
                    ok => Landed(decode, ok),
                    decode.State);
                m_nativeHandle = new NativeAssetHandle(handle);
                m_decode = decode;
                return decode;
            }
        }

        private void Landed(Decode decode, bool ok)
        {
            ulong failed = 0;
            lock (m_lock)
            {
                // A failed decode leaves nothing to use, so the next load tries again.
                if (!ok && m_decode == decode)
                {
                    failed = m_nativeHandle.Value;
                    m_nativeHandle = default;
                    m_decode = null;
                }
            }
            if (failed != 0)
            {
                DeleteNative(failed);
            }
            decode.Ok = ok;
            decode.Landed = true;
        }

        /// <summary>
        /// Unload the out-of-band asset. This allows the engine to clean it up when it is not used by any more animations.
        /// </summary>
        public void Unload()
        {
            NativeAssetHandle released = default;
            lock (m_lock)
            {
                if (m_refCount <= 0)
                {
                    m_refCount = 0;
                    return;
                }

                m_refCount--;
                if (m_refCount == 0 && m_nativeHandle.IsValid)
                {
                    released = m_nativeHandle;
                    m_nativeHandle = default;
                    m_decode = null;
                }
            }

            if (released.IsValid)
            {
                DeleteNative(released.Value);

                // Releasing the asset queues its GPU resources for destruction,
                // and there may be no panel left to render a frame that would
                // carry them.
                GpuCanvasResources.RequestFlush();
            }
        }

        /// <summary>
        /// Check the reference count of the out-of-band asset.
        /// </summary>
        /// <returns></returns>
        internal int RefCount()
        {
            lock (m_lock)
            {
                return m_refCount;
            }
        }
    }
}
