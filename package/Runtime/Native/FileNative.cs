using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Rive.Host;
using Rive.Producer;

namespace Rive
{
    /// <summary>
    /// The host calls behind <see cref="File"/> and <see cref="FileHandle"/>.
    /// </summary>
    internal static class FileNative
    {
        [DllImport(NativeLibrary.name)]
        private static extern ulong riveLoadFile(ulong requestId, byte[] bytes, uint size, byte[] assetMap, uint assetMapSize);

        [DllImport(NativeLibrary.name)]
        private static extern void riveDeleteFile(ulong file);

        [DllImport(NativeLibrary.name)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool riveIsFileAlive(ulong requestId, ulong file);

        [DllImport(NativeLibrary.name)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool riveUpdateFileAsset(ulong requestId, ulong file, uint assetId, ulong assetHandle);

        [DllImport(NativeLibrary.name)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool riveClearFileAsset(ulong requestId, ulong file, uint assetId);

        [DllImport(NativeLibrary.name)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool riveListFileAssets(ulong requestId, byte[] bytes, uint size);

        /// A load's result. Contents is null when it didn't load.
        internal sealed class Loaded
        {
            internal NativeFileHandle Handle;
            internal FileContents Contents;
        }

        // Every routine reply here leads with a u32, 0 for failure.
        private static bool ReadOk(HostMessageBatch batch, HostMessage message)
        {
            return message.PayloadSize >= 4 &&
                   BitConverter.ToUInt32(batch.Bytes, message.PayloadOffset) != 0;
        }

        private static FileContents ReadContents(HostMessageBatch batch, HostMessage message)
        {
            return ReadOk(batch, message)
                ? FileContents.Parse(batch.Bytes, message.PayloadOffset + 4)
                : null;
        }

        /// Imports the file and waits for it, with its describe. assetMap
        /// names assets C# already has, see FallbackFileAssetLoader.
        internal static Loaded Load(byte[] bytes, byte[] assetMap)
        {
            var loaded = new Loaded();
            RequestTicket ticket = CommandTransport.Send(
                id => loaded.Handle = new NativeFileHandle(
                    riveLoadFile(id, bytes, (uint)bytes.Length, assetMap, (uint)assetMap.Length)),
                (batch, message) => loaded.Contents = ReadContents(batch, message));
            CommandTransport.Join(ref ticket);
            DropIfFailed(loaded);
            return loaded;
        }

        /// Load without waiting. Finishes on the main thread.
        internal static Future<Loaded> LoadAsync(byte[] bytes, byte[] assetMap)
        {
            var loaded = new Loaded();
            return CommandTransport.SendFuture(
                id => loaded.Handle = new NativeFileHandle(
                    riveLoadFile(id, bytes, (uint)bytes.Length, assetMap, (uint)assetMap.Length)),
                (batch, message) =>
                {
                    loaded.Contents = ReadContents(batch, message);
                    DropIfFailed(loaded);
                    return loaded;
                });
        }

        // The handle names a file core may still hold, even when the import
        // didn't work.
        private static void DropIfFailed(Loaded loaded)
        {
            if (loaded.Contents == null && loaded.Handle.IsValid)
            {
                Delete(loaded.Handle);
                loaded.Handle = default;
            }
        }

        /// Deletes the file, and its artboards and state machines with it.
        internal static void Delete(NativeFileHandle file)
        {
            if (file.IsValid)
            {
                CommandTransport.SendNoReply(() => riveDeleteFile(file.Value));
            }
        }

        /// True while core still holds the file. Waits for everything sent
        /// before it, deletes included.
        internal static bool IsAlive(NativeFileHandle file)
        {
            if (!file.IsValid)
            {
                return false;
            }
            bool alive = false;
            RequestTicket ticket = CommandTransport.Send(
                id => riveIsFileAlive(id, file.Value),
                (batch, message) => alive = ReadOk(batch, message));
            CommandTransport.Join(ref ticket);
            return alive;
        }

        /// Gives the file's asset what the out-of-band asset decoded to. False
        /// when the file has no asset with that id.
        internal static bool UpdateAsset(NativeFileHandle file, uint assetId, NativeAssetHandle asset)
        {
            bool ok = false;
            RequestTicket ticket = CommandTransport.Send(
                id => riveUpdateFileAsset(id, file.Value, assetId, asset.Value),
                (batch, message) => ok = ReadOk(batch, message));
            CommandTransport.Join(ref ticket);
            return ok;
        }

        /// Empties the file's asset.
        internal static void ClearAsset(NativeFileHandle file, uint assetId)
        {
            RequestTicket ticket = CommandTransport.Send(
                id => riveClearFileAsset(id, file.Value, assetId));
            CommandTransport.Join(ref ticket);
        }

        /// The assets a .riv references, without keeping it loaded. Null if it
        /// can't be read.
        internal static FileContents.AssetInfo[] ListAssets(byte[] bytes)
        {
            FileContents.AssetInfo[] assets = null;
            RequestTicket ticket = CommandTransport.Send(
                id => riveListFileAssets(id, bytes, (uint)bytes.Length),
                (batch, message) =>
                {
                    if (ReadOk(batch, message))
                    {
                        assets = FileContents.ParseAssets(batch.Bytes, message.PayloadOffset + 4);
                    }
                });
            CommandTransport.Join(ref ticket);
            return assets;
        }

        /// Runs on the main thread each time the host stops, which takes every
        /// native file with it.
        internal static void WhenHostStops(Action forget)
        {
            CommandTransport.Stopped += forget;
        }

        /// A FileHandle once the file lands, or a LoadFailed error.
        internal static Future<FileHandle> ToFileHandle(Future<File> loading)
        {
            return new Future<FileHandle>(FutureState<FileHandle>.Then(loading, (landed, state) =>
            {
                if (state.TryForwardFailure(landed))
                {
                    return;
                }
                File file = landed.Result;
                if (file == null)
                {
                    state.Fail(new RiveException(
                        RiveErrorCode.LoadFailed,
                        "The Rive file could not be loaded. The log says why."));
                    return;
                }
                state.Succeed(new FileHandle(file, file.Contents));
            }));
        }
    }
}
