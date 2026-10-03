using System;
using System.Runtime.InteropServices;
using Rive.Host;
using Rive.Producer;

namespace Rive
{
    /// <summary>
    /// The host calls behind out-of-band assets. Decodes run in order on
    /// Rive's thread, so a load sent after one can already name its handle.
    /// </summary>
    internal static class OutOfBandAssetNative
    {
        [DllImport(NativeLibrary.name)]
        private static extern ulong riveDecodeImage(ulong requestId, byte[] bytes, uint size);

        [DllImport(NativeLibrary.name)]
        private static extern ulong riveDecodeFont(ulong requestId, byte[] bytes, uint size);

        [DllImport(NativeLibrary.name)]
        private static extern ulong riveDecodeAudio(ulong requestId, byte[] bytes, uint size);

        [DllImport(NativeLibrary.name)]
        private static extern void riveDeleteImage(ulong handle);

        [DllImport(NativeLibrary.name)]
        private static extern void riveDeleteFont(ulong handle);

        [DllImport(NativeLibrary.name)]
        private static extern void riveDeleteAudio(ulong handle);

        internal static ulong DecodeImage(ulong requestId, byte[] bytes) =>
            riveDecodeImage(requestId, bytes, (uint)bytes.Length);

        internal static ulong DecodeFont(ulong requestId, byte[] bytes) =>
            riveDecodeFont(requestId, bytes, (uint)bytes.Length);

        internal static ulong DecodeAudio(ulong requestId, byte[] bytes) =>
            riveDecodeAudio(requestId, bytes, (uint)bytes.Length);

        internal static void DeleteImage(ulong handle) =>
            CommandTransport.SendNoReply(() => riveDeleteImage(handle));

        internal static void DeleteFont(ulong handle) =>
            CommandTransport.SendNoReply(() => riveDeleteFont(handle));

        internal static void DeleteAudio(ulong handle) =>
            CommandTransport.SendNoReply(() => riveDeleteAudio(handle));

        /// Sends a decode and returns its handle. landed runs with whether it
        /// worked, in the drain that delivers the reply, and state finishes on
        /// the main thread after it.
        internal static ulong Decode(
            Func<ulong, ulong> send,
            Action<bool> landed,
            FutureState<bool> state)
        {
            long asyncId = CommandTransport.TrackAsync(state);
            state.OnProducer = true;
            ulong handle = 0;
            CommandTransport.Send(
                id => handle = send(id),
                (batch, message) =>
                {
                    bool ok = message.PayloadSize >= 4 &&
                              BitConverter.ToUInt32(batch.Bytes, message.PayloadOffset) != 0;
                    landed(ok);
                    state.WorkFinished = true;
                    CommandTransport.PostToMainThread(() =>
                    {
                        CommandTransport.ForgetAsync(asyncId);
                        state.Succeed(ok);
                    });
                },
                keep: false);
            return handle;
        }

        /// Waits until done says the decode landed.
        internal static void Wait(Func<bool> done)
        {
            CommandTransport.WaitFor(done, "Load");
        }
    }
}
