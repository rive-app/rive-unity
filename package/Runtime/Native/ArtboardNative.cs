using System;
using System.Runtime.InteropServices;
using Rive.Producer;
using Rive.Utils;
using Rive.Host;

namespace Rive
{
    /// <summary>
    /// The host calls behind <see cref="Artboard"/> and <see cref="ArtboardHandle"/>.
    /// </summary>
    internal static class ArtboardNative
    {
        [DllImport(NativeLibrary.name)]
        private static extern ulong riveInstantiateArtboard(ulong requestId, ulong file, [MarshalAs(NativeText.Encoding)] string name);

        [DllImport(NativeLibrary.name)]
        private static extern void riveDeleteArtboard(ulong artboard);

        [DllImport(NativeLibrary.name)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool riveArtboardInfo(ulong requestId, ulong artboard);

        [DllImport(NativeLibrary.name)]
        private static extern void riveSetArtboardSize(ulong artboard, float width, float height);

        [DllImport(NativeLibrary.name)]
        private static extern void riveSetArtboardSide(ulong artboard, float value, [MarshalAs(UnmanagedType.U1)] bool height);

        [DllImport(NativeLibrary.name)]
        private static extern void riveResetArtboardSize(ulong artboard);

        [DllImport(NativeLibrary.name)]
        private static extern void riveSetArtboardAudioEngine(ulong artboard, IntPtr audioEngine);

        [DllImport(NativeLibrary.name)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool riveScreenToRive(
            ulong requestId,
            ulong artboard,
            float x,
            float y,
            float minX,
            float minY,
            float maxX,
            float maxY,
            byte fit,
            float alignX,
            float alignY);

        [DllImport(NativeLibrary.name)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool riveGetTextRun(
            ulong requestId,
            ulong artboard,
            [MarshalAs(NativeText.Encoding)] string name,
            [MarshalAs(NativeText.Encoding)] string path);

        [DllImport(NativeLibrary.name)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool riveSetTextRun(
            ulong requestId,
            ulong artboard,
            [MarshalAs(NativeText.Encoding)] string name,
            [MarshalAs(NativeText.Encoding)] string path,
            [MarshalAs(NativeText.Encoding)] string text);

        [DllImport(NativeLibrary.name)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool riveGetInputAtPath(
            ulong requestId,
            ulong artboard,
            [MarshalAs(NativeText.Encoding)] string name,
            [MarshalAs(NativeText.Encoding)] string path);

        [DllImport(NativeLibrary.name)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool riveSetInputAtPath(
            ulong requestId,
            ulong artboard,
            [MarshalAs(NativeText.Encoding)] string name,
            [MarshalAs(NativeText.Encoding)] string path,
            uint kind,
            float value);

        /// What the artboard instance is right now.
        internal struct Info
        {
            internal bool Found;
            internal Size Size;
            internal Size OriginalSize;
            internal bool HasAudio;
            internal bool DidChange;
        }

        /// Makes the artboard and waits for it. Invalid if the file has no
        /// artboard by that name.
        internal static NativeArtboardHandle Instantiate(NativeFileHandle file, string name)
        {
            bool ok = false;
            ulong handle = 0;
            RequestTicket ticket = CommandTransport.Send(
                id => handle = riveInstantiateArtboard(id, file.Value, name),
                (batch, message) => ok = new PayloadReader(batch, message).Ok());
            CommandTransport.Join(ref ticket);
            return ok ? new NativeArtboardHandle(handle) : default;
        }

        /// Doesn't wait. The handle's slot is filled straight away and the
        /// lookup lands later.
        internal static void InstantiateLater(NativeFileHandle file, ArtboardHandle handle, string callSite)
        {
            NativeSlot<NativeArtboardHandle> slot = handle.Native;
            HandleLookup.Later(
                handle.Resolution,
                null,
                id => slot.Value = new NativeArtboardHandle(riveInstantiateArtboard(id, file.Value, handle.Name)),
                () => new RiveException(RiveErrorCode.ArtboardNotFound, $"Rive couldn't make artboard '{handle.Name}'."),
                null,
                callSite);
        }

        /// For a caller that sends it with its own request id. The reply is
        /// a u32 ok.
        internal static NativeArtboardHandle InstantiateWithId(ulong requestId, NativeFileHandle file, string name)
        {
            return new NativeArtboardHandle(riveInstantiateArtboard(requestId, file.Value, name));
        }

        /// Doesn't wait. found runs in the drain that delivers the reply.
        internal static NativeArtboardHandle InstantiateAsync(NativeFileHandle file, string name, Action<bool> found)
        {
            ulong handle = 0;
            CommandTransport.Send(
                id => handle = riveInstantiateArtboard(id, file.Value, name),
                (batch, message) => found(new PayloadReader(batch, message).Ok()),
                keep: false);
            return new NativeArtboardHandle(handle);
        }

        /// Also deletes its state machines.
        internal static void Delete(NativeArtboardHandle artboard)
        {
            if (artboard.IsValid)
            {
                CommandTransport.SendNoReply(() => riveDeleteArtboard(artboard.Value));
            }
        }

        /// Deleted once its owner and every state machine made from it let go.
        internal static NativeLifetime Lifetime(NativeSlot<NativeArtboardHandle> artboard, NativeLifetime file)
        {
            return new NativeLifetime(() => Delete(artboard.Value), file);
        }

        private static Info ReadInfo(HostMessageBatch batch, HostMessage message)
        {
            var reader = new PayloadReader(batch, message);
            var info = new Info { Found = reader.Ok() };
            if (info.Found)
            {
                info.Size = new Size(reader.F32(), reader.F32());
                info.OriginalSize = new Size(reader.F32(), reader.F32());
                info.HasAudio = reader.Bool();
                info.DidChange = reader.Bool();
            }
            return info;
        }

        internal static Info GetInfo(NativeArtboardHandle artboard)
        {
            Info info = default;
            RequestTicket ticket = CommandTransport.Send(
                id => riveArtboardInfo(id, artboard.Value),
                (batch, message) => info = ReadInfo(batch, message));
            CommandTransport.Join(ref ticket);
            return info;
        }

        /// parse runs in the drain that delivers the reply, and the Future
        /// finishes on the main thread.
        internal static Future<T> GetInfoAsync<T>(NativeArtboardHandle artboard, Func<Info, T> parse)
        {
            return CommandTransport.SendFuture(
                id => riveArtboardInfo(id, artboard.Value),
                (batch, message) => parse(ReadInfo(batch, message)));
        }

        internal static Future<Size> GetSizeAsync(NativeSlot<NativeArtboardHandle> artboard)
        {
            ulong handle = artboard.Value.Value;
            return CommandTransport.SendFuture(
                id => riveArtboardInfo(id, handle),
                (batch, message) => ReadInfo(batch, message).Size);
        }

        internal static void SetSize(NativeArtboardHandle artboard, Size size)
        {
            CommandTransport.Write(() => riveSetArtboardSize(artboard.Value, size.Width, size.Height));
        }

        internal static void SetWidth(NativeArtboardHandle artboard, float width)
        {
            CommandTransport.Write(() => riveSetArtboardSide(artboard.Value, width, false));
        }

        internal static void SetHeight(NativeArtboardHandle artboard, float height)
        {
            CommandTransport.Write(() => riveSetArtboardSide(artboard.Value, height, true));
        }

        internal static void ResetSize(NativeArtboardHandle artboard)
        {
            CommandTransport.Write(() => riveResetArtboardSize(artboard.Value));
        }

        // Captures the engine, not its pointer, so its finalizer can't free
        // it before the write runs. After that the artboard holds a ref.
        internal static void SetAudioEngine(NativeArtboardHandle artboard, AudioEngine audioEngine)
        {
            CommandTransport.Write(() => riveSetArtboardAudioEngine(artboard.Value, audioEngine.m_nativeAudioEngine));
        }

        internal static Vec2D ScreenToRive(
            NativeArtboardHandle artboard,
            float x,
            float y,
            float minX,
            float minY,
            float maxX,
            float maxY,
            byte fit,
            float alignX,
            float alignY)
        {
            ScreenToRiveCall call = t_screenToRive ?? new ScreenToRiveCall();
            // Out while in use, so a call made while this one waits gets its own.
            t_screenToRive = null;
            call.Artboard = artboard.Value;
            call.X = x;
            call.Y = y;
            call.MinX = minX;
            call.MinY = minY;
            call.MaxX = maxX;
            call.MaxY = maxY;
            call.Fit = fit;
            call.AlignX = alignX;
            call.AlignY = alignY;
            call.Point = default;
            RequestTicket ticket = CommandTransport.Send(call.Send, call.OnReply);
            CommandTransport.Join(ref ticket);
            Vec2D point = call.Point;
            t_screenToRive = call;
            return point;
        }

        // Pointer hit tests convert every raycast, so the call and its
        // delegates are made once per thread rather than per call.
        private sealed class ScreenToRiveCall
        {
            internal readonly Action<ulong> Send;
            internal readonly Action<HostMessageBatch, HostMessage> OnReply;
            internal ulong Artboard;
            internal float X;
            internal float Y;
            internal float MinX;
            internal float MinY;
            internal float MaxX;
            internal float MaxY;
            internal byte Fit;
            internal float AlignX;
            internal float AlignY;
            internal Vec2D Point;

            internal ScreenToRiveCall()
            {
                Send = id => riveScreenToRive(id, Artboard, X, Y, MinX, MinY, MaxX, MaxY, Fit, AlignX, AlignY);
                OnReply = (batch, message) =>
                {
                    var reader = new PayloadReader(batch, message);
                    if (reader.Ok())
                    {
                        Point.x = reader.F32();
                        Point.y = reader.F32();
                    }
                };
            }
        }

        [ThreadStatic]
        private static ScreenToRiveCall t_screenToRive;

        /// Null when there's no run by that name. A null path looks on the
        /// artboard itself.
        internal static string GetTextRun(NativeArtboardHandle artboard, string name, string path)
        {
            if (name == null)
            {
                return null;
            }
            string text = null;
            RequestTicket ticket = CommandTransport.Send(
                id => riveGetTextRun(id, artboard.Value, name, path),
                (batch, message) =>
                {
                    var reader = new PayloadReader(batch, message);
                    if (reader.Ok())
                    {
                        text = reader.String();
                    }
                });
            CommandTransport.Join(ref ticket);
            return text;
        }

        internal static bool SetTextRun(NativeArtboardHandle artboard, string name, string path, string text)
        {
            if (name == null)
            {
                return false;
            }
            bool ok = false;
            RequestTicket ticket = CommandTransport.Send(
                id => riveSetTextRun(id, artboard.Value, name, path, text),
                (batch, message) => ok = new PayloadReader(batch, message).Ok());
            CommandTransport.Join(ref ticket);
            return ok;
        }

        /// Mirrors InputKind in artboard_routines.cpp.
        internal enum InputKind : uint
        {
            None = 0,
            Boolean = 1,
            Number = 2,
            Trigger = 3,
        }

        /// Looks the input up and reads it. False when there's none.
        internal static bool GetInputAtPath(
            NativeArtboardHandle artboard, string inputName, string path, out InputKind kind, out float value)
        {
            bool found = false;
            InputKind foundKind = InputKind.None;
            float foundValue = 0f;
            RequestTicket ticket = CommandTransport.Send(
                id => riveGetInputAtPath(id, artboard.Value, inputName, path),
                (batch, message) =>
                {
                    var reader = new PayloadReader(batch, message);
                    found = reader.Ok();
                    foundKind = (InputKind)reader.U32();
                    foundValue = reader.F32();
                });
            CommandTransport.Join(ref ticket);
            kind = foundKind;
            value = foundValue;
            return found;
        }

        /// Looks the input up when the write runs, since data binding can
        /// swap nested artboards. Warnings come back to the main thread.
        [Obsolete(ObsoleteMessages.Inputs)]
        internal static void SetInputAtPath(
            Artboard artboard, string inputName, string path, InputKind kind, float value)
        {
            ulong handle = artboard.NativeArtboard.Value;
            string artboardName = artboard.Name;
            object queuedAt = QueuedAt();
            CommandTransport.Send(
                id => riveSetInputAtPath(id, handle, inputName, path, (uint)kind, value),
                (batch, message) =>
                {
                    var reader = new PayloadReader(batch, message);
                    bool found = reader.Ok();
                    var foundKind = (InputKind)reader.U32();
                    if (found && foundKind == kind)
                    {
                        return;
                    }
                    string type = kind == InputKind.Boolean ? "boolean" : kind == InputKind.Number ? "number" : "trigger";
                    string problem = found
                        ? $"Input '{inputName}' at path: '{path}' is not a {type} input."
                        : $"No input found at path '{path}' with name '{inputName}'.";
                    string warning = $"{problem} Artboard: '{artboardName}'.{queuedAt}";
                    CommandTransport.PostToMainThread(() => DebugLogger.Instance.LogWarning(warning));
                },
                keep: false);
        }

        // Editor only. Where the write was queued, so a late warning still
        // points at the caller.
        private static object QueuedAt()
        {
#if UNITY_EDITOR
            return new QueuedAtTrace(new System.Diagnostics.StackTrace(2, true));
#else
            return null;
#endif
        }

        private sealed class QueuedAtTrace
        {
            private readonly System.Diagnostics.StackTrace m_trace;

            internal QueuedAtTrace(System.Diagnostics.StackTrace trace)
            {
                m_trace = trace;
            }

            public override string ToString()
            {
                return "\nQueued at:\n" + m_trace;
            }
        }

        // Data binding

        internal static void BindViewModelInstanceToArtboard(NativeArtboardHandle artboard, NativeViewModelInstanceHandle viewModelInstance)
        {
            ViewModelNative.BindToArtboard(artboard, viewModelInstance);
        }
    }
}
