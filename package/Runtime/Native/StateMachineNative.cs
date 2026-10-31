using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Rive.Producer;
using Rive.Host;

namespace Rive
{
    /// <summary>
    /// The host calls behind <see cref="StateMachine"/> and <see cref="StateMachineHandle"/>.
    /// </summary>
    internal static class StateMachineNative
    {
        [DllImport(NativeLibrary.name)]
        private static extern ulong riveInstantiateStateMachine(ulong requestId, ulong artboard, [MarshalAs(NativeText.Encoding)] string name);

        [DllImport(NativeLibrary.name)]
        private static extern void riveDeleteStateMachine(ulong stateMachine);

        [DllImport(NativeLibrary.name)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool riveTick(ulong requestId, byte[] entries, uint size);

        [DllImport(NativeLibrary.name)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool riveReportedEvents(ulong requestId, ulong stateMachine);

        [DllImport(NativeLibrary.name)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool riveListInputs(ulong requestId, ulong stateMachine);

        [DllImport(NativeLibrary.name)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool riveGetInput(ulong requestId, ulong stateMachine, uint index);

        [DllImport(NativeLibrary.name)]
        private static extern void riveSetInput(ulong stateMachine, uint index, float value);

        /// Makes the state machine and waits for it. Invalid if the artboard
        /// has none by that name.
        internal static NativeStateMachineHandle Instantiate(NativeArtboardHandle artboard, string name)
        {
            bool ok = false;
            ulong handle = 0;
            RequestTicket ticket = CommandTransport.Send(
                id => handle = riveInstantiateStateMachine(id, artboard.Value, name),
                (batch, message) => ok = new PayloadReader(batch, message).Ok());
            CommandTransport.Join(ref ticket);
            return ok ? new NativeStateMachineHandle(handle) : default;
        }

        /// Doesn't wait. The handle's slot is filled straight away and the
        /// lookup lands later.
        internal static void InstantiateLater(ArtboardHandle artboard, StateMachineHandle handle, string callSite)
        {
            NativeSlot<NativeStateMachineHandle> slot = handle.Native;
            ulong parent = artboard.Native.Value.Value;
            HandleLookup.Later(
                handle.Resolution,
                artboard.Resolution,
                id => slot.Value = new NativeStateMachineHandle(riveInstantiateStateMachine(id, parent, handle.Name)),
                () => new RiveException(RiveErrorCode.StateMachineNotFound,
                    $"Rive couldn't make state machine '{handle.Name}' on artboard '{artboard.Name}'."),
                null,
                callSite);
        }

        /// Doesn't wait. found runs in the drain that delivers the reply.
        internal static NativeStateMachineHandle InstantiateAsync(NativeArtboardHandle artboard, string name, Action<bool> found)
        {
            ulong handle = 0;
            CommandTransport.Send(
                id => handle = riveInstantiateStateMachine(id, artboard.Value, name),
                (batch, message) => found(new PayloadReader(batch, message).Ok()),
                keep: false);
            return new NativeStateMachineHandle(handle);
        }

        /// For a channel that hands out its own request id.
        internal static void Tick(ulong requestId, PayloadWriter entries)
        {
            riveTick(requestId, entries.Bytes, (uint)entries.Size);
        }

        internal static void Delete(NativeStateMachineHandle stateMachine)
        {
            if (stateMachine.IsValid)
            {
                CommandTransport.SendNoReply(() => riveDeleteStateMachine(stateMachine.Value));
            }
        }

        /// Deleted once its owner lets go. Keeps its artboard until then.
        internal static NativeLifetime Lifetime(NativeSlot<NativeStateMachineHandle> stateMachine, NativeLifetime artboard)
        {
            return new NativeLifetime(() => Delete(stateMachine.Value), artboard);
        }

        // Tick entries. Mirrors kTick... in artboard_routines.cpp.
        internal const uint TickLayoutFix = 1;
        internal const uint TickCollectEvents = 2;

        /// One state machine's part of a tick. artboard can be invalid when
        /// the size isn't wanted.
        internal static void WriteTickEntry(
            PayloadWriter entries, NativeStateMachineHandle stateMachine, NativeArtboardHandle artboard, float seconds, uint flags)
        {
            entries.U64(stateMachine.Value);
            entries.U64(artboard.Value);
            entries.F32(seconds);
            entries.U32(flags);
        }

        /// Sends the entries, which the native side copies. The reply has
        /// each entry in order, see ReadTickEntry.
        /// A tick sent on its own, with its payload and send made once. Native
        /// copies the payload as it goes out, so it's free again once Send
        /// returns.
        internal sealed class TickSender
        {
            internal readonly PayloadWriter Entries = new PayloadWriter();
            private readonly Action<ulong> m_send;
            private readonly Action m_sendNoReply;

            internal TickSender()
            {
                m_send = id => riveTick(id, Entries.Bytes, (uint)Entries.Size);
                m_sendNoReply = () => riveTick(0, Entries.Bytes, (uint)Entries.Size);
            }

            internal RequestTicket Send(Action<HostMessageBatch, HostMessage> onReply, bool keep)
            {
                return CommandTransport.Send(m_send, onReply, keep);
            }

            internal void SendNoReply()
            {
                CommandTransport.SendNoReply(m_sendNoReply);
            }
        }

        internal struct TickResult
        {
            internal bool Found;
            internal bool KeepGoing;
            internal Size ArtboardSize;
            internal FocusState Focus;
        }

        /// Reads the next entry of a tick reply. Events go into events when
        /// it collected them.
        internal static TickResult ReadTickEntry(ref PayloadReader reader, List<ReportedEventData> events)
        {
            var result = new TickResult
            {
                Found = reader.Ok(),
                KeepGoing = reader.Bool(),
                ArtboardSize = new Size(reader.F32(), reader.F32()),
            };
            result.Focus = ReadFocusState(ref reader);
            ReadEvents(ref reader, events);
            return result;
        }

        /// Advances and waits. False once it has settled.
        internal static bool Advance(NativeStateMachineHandle stateMachine, float seconds)
        {
            AdvanceCall call = t_advance ?? new AdvanceCall();
            // Out while in use, so a call made while this one waits gets its own.
            t_advance = null;
            call.Sender.Entries.Clear();
            WriteTickEntry(call.Sender.Entries, stateMachine, default, seconds, 0);
            call.KeepGoing = false;
            RequestTicket ticket = call.Sender.Send(call.OnReply, keep: true);
            CommandTransport.Join(ref ticket);
            bool keepGoing = call.KeepGoing;
            t_advance = call;
            return keepGoing;
        }

        // Scripts call Advance every frame, so it reuses one of these per
        // thread rather than allocating per call.
        private sealed class AdvanceCall
        {
            internal readonly TickSender Sender = new TickSender();
            internal readonly Action<HostMessageBatch, HostMessage> OnReply;
            internal bool KeepGoing;

            internal AdvanceCall()
            {
                OnReply = (batch, message) =>
                {
                    var reader = new PayloadReader(batch, message);
                    KeepGoing = ReadTickEntry(ref reader, null).KeepGoing;
                };
            }
        }

        [ThreadStatic]
        private static AdvanceCall t_advance;

        [ThreadStatic]
        private static TickSender t_advanceLater;

        /// Doesn't wait. In order with other writes.
        internal static void AdvanceLater(NativeStateMachineHandle stateMachine, float seconds)
        {
            TickSender sender = t_advanceLater ??= new TickSender();
            sender.Entries.Clear();
            WriteTickEntry(sender.Entries, stateMachine, default, seconds, 0);
            sender.SendNoReply();
        }

        /// What the last advance reported.
        internal static void ReportedEvents(NativeStateMachineHandle stateMachine, List<ReportedEventData> into)
        {
            RequestTicket ticket = CommandTransport.Send(
                id => riveReportedEvents(id, stateMachine.Value),
                (batch, message) =>
                {
                    var reader = new PayloadReader(batch, message);
                    if (reader.Ok())
                    {
                        ReadEvents(ref reader, into);
                    }
                });
            CommandTransport.Join(ref ticket);
        }

        // A u32 count, then each event. Skips them when into is null.
        private static void ReadEvents(ref PayloadReader reader, List<ReportedEventData> into)
        {
            uint count = reader.U32();
            for (uint i = 0; i < count; i++)
            {
                var data = new ReportedEventData
                {
                    Name = reader.String(),
                    SecondsDelay = reader.F32(),
                    Type = (ushort)reader.U32(),
                };
                uint properties = reader.U32();
                data.Properties = properties == 0
                    ? Array.Empty<ReportedEventPropertyData>()
                    : new ReportedEventPropertyData[properties];
                for (uint j = 0; j < properties; j++)
                {
                    data.Properties[j] = new ReportedEventPropertyData
                    {
                        Name = reader.String(),
                        Type = (ushort)reader.U32(),
                        NumberValue = reader.F32(),
                        BoolValue = reader.Bool(),
                        StringValue = reader.String(),
                    };
                }
                into?.Add(data);
            }
        }

        internal struct InputInfo
        {
            internal string Name;
            internal ArtboardNative.InputKind Kind;
        }

        /// The state machine's inputs in order. Empty if it's gone.
        internal static InputInfo[] ListInputs(NativeStateMachineHandle stateMachine)
        {
            InputInfo[] inputs = Array.Empty<InputInfo>();
            RequestTicket ticket = CommandTransport.Send(
                id => riveListInputs(id, stateMachine.Value),
                (batch, message) =>
                {
                    var reader = new PayloadReader(batch, message);
                    if (!reader.Ok())
                    {
                        return;
                    }
                    inputs = new InputInfo[reader.U32()];
                    for (int i = 0; i < inputs.Length; i++)
                    {
                        inputs[i].Name = reader.String();
                        inputs[i].Kind = (ArtboardNative.InputKind)reader.U32();
                    }
                });
            CommandTransport.Join(ref ticket);
            return inputs;
        }

        /// 0 or false when the state machine is gone.
        internal static float GetInput(NativeStateMachineHandle stateMachine, NativeSMIInputHandle input)
        {
            float value = 0f;
            RequestTicket ticket = CommandTransport.Send(
                id => riveGetInput(id, stateMachine.Value, input.Index),
                (batch, message) =>
                {
                    var reader = new PayloadReader(batch, message);
                    if (reader.Ok())
                    {
                        reader.U32();
                        value = reader.F32();
                    }
                });
            CommandTransport.Join(ref ticket);
            return value;
        }

        /// Booleans take anything but 0 as true. Triggers fire.
        internal static void SetInput(NativeStateMachineHandle stateMachine, NativeSMIInputHandle input, float value)
        {
            CommandTransport.Write(() => riveSetInput(stateMachine.Value, input.Index, value));
        }

        // Pointer input

        /// Mirrored in native as FocusOp.
        internal enum FocusOp : uint
        {
            Next = 0,
            Previous = 1,
            Left = 2,
            Right = 3,
            Up = 4,
            Down = 5,
            Clear = 6,
            // Changes nothing, only reports the state.
            Query = 7,
        }

        private const uint FocusHas = 1 << 0;
        private const uint FocusExpectsKeyboard = 1 << 1;
        private const uint FocusHasNodes = 1 << 2;
        private const uint FocusHasBounds = 1 << 3;

        /// Advances by 0 after a change, so what focus listeners set applies
        /// before the next frame's advance, like a pointer press does.
        internal const uint FocusApply = 1 << 1;
        internal const uint FocusCollectEvents = 1 << 2;
        /// Reads the focused selection before the input, for the clipboard.
        internal const uint FocusReadSelection = 1 << 3;
        /// Also deletes that selection when the focused node takes text.
        internal const uint FocusCutSelection = 1 << 4;

        /// What a focus move, key or text did, and the focus it left.
        internal struct FocusResult
        {
            internal bool Found;
            // Whether focus moved, or for a key or text, whether something claimed it.
            internal bool Moved;
            internal FocusState State;
            // What was selected before the input, when it asked to read it.
            internal string SelectedText;

            internal bool Claimed => Moved;
        }

        [DllImport(NativeLibrary.name)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool riveSmFocus(ulong requestId, ulong stateMachine, uint op, uint flags);

        internal static bool SendFocus(ulong requestId, NativeStateMachineHandle stateMachine, FocusOp op, uint flags)
        {
            return riveSmFocus(requestId, stateMachine.Value, (uint)op, flags);
        }

        [DllImport(NativeLibrary.name)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool riveSmKey(
            ulong requestId,
            ulong stateMachine,
            uint key,
            uint modifiers,
            [MarshalAs(UnmanagedType.U1)] bool pressed,
            [MarshalAs(UnmanagedType.U1)] bool repeat,
            uint flags);

        [DllImport(NativeLibrary.name)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool riveSmText(
            ulong requestId,
            ulong stateMachine,
            [MarshalAs(NativeText.Encoding)] string text,
            uint flags);

        /// A key for the focused node and its ancestors. The reply is read
        /// with ReadFocus, Claimed saying whether one took it.
        internal static bool SendKey(
            ulong requestId, NativeStateMachineHandle stateMachine, RiveKey key, RiveKeyModifiers modifiers, bool pressed, bool repeat, uint flags)
        {
            return riveSmKey(requestId, stateMachine.Value, (uint)key, (uint)modifiers, pressed, repeat, flags);
        }

        /// Committed text for the focused node and its ancestors. The reply is
        /// read with ReadFocus, Claimed saying whether one took it.
        internal static bool SendText(ulong requestId, NativeStateMachineHandle stateMachine, string text, uint flags)
        {
            return riveSmText(requestId, stateMachine.Value, text, flags);
        }

        [DllImport(NativeLibrary.name)]
        private static extern void riveSmGamepad(ulong stateMachine, byte[] data, uint length);

        /// A batch from GamepadTracker. Doesn't wait, and shows on the next advance.
        internal static void SendGamepad(NativeStateMachineHandle stateMachine, byte[] batch)
        {
            if (batch == null)
            {
                return;
            }
            CommandTransport.Write(() => riveSmGamepad(stateMachine.Value, batch, (uint)batch.Length));
        }

        /// Events go into events when it collected them. flags are the ones it was sent with.
        internal static FocusResult ReadFocus(ref PayloadReader reader, List<ReportedEventData> events, uint flags = 0)
        {
            var result = new FocusResult { Found = reader.Ok() };
            if (!result.Found)
            {
                return result;
            }
            result.Moved = reader.Bool();
            result.State = ReadFocusState(ref reader);
            ReadEvents(ref reader, events);
            if ((flags & (FocusReadSelection | FocusCutSelection)) != 0)
            {
                result.SelectedText = reader.String();
            }
            return result;
        }

        private static FocusState ReadFocusState(ref PayloadReader reader)
        {
            uint flags = reader.U32();
            var state = new FocusState(
                (flags & FocusHas) != 0,
                (flags & FocusExpectsKeyboard) != 0,
                (flags & FocusHasNodes) != 0);
            if ((flags & FocusHasBounds) != 0)
            {
                state = state.WithBounds(new AABB(reader.F32(), reader.F32(), reader.F32(), reader.F32()));
            }
            return state;
        }

        /// Main thread or any thread allowed to wait. Doesn't apply, so the
        /// caller advances if it moved.
        internal static FocusResult FocusAndWait(NativeStateMachineHandle stateMachine, FocusOp op, uint flags = 0)
        {
            return InputAndWait(id => SendFocus(id, stateMachine, op, flags), flags);
        }

        /// Like FocusAndWait, for a key.
        internal static FocusResult KeyAndWait(
            NativeStateMachineHandle stateMachine, RiveKey key, RiveKeyModifiers modifiers, bool pressed, bool repeat)
        {
            return InputAndWait(id => SendKey(id, stateMachine, key, modifiers, pressed, repeat, 0), 0);
        }

        /// Like FocusAndWait, for committed text.
        internal static FocusResult TextAndWait(NativeStateMachineHandle stateMachine, string text)
        {
            return InputAndWait(id => SendText(id, stateMachine, text, 0), 0);
        }

        /// Doesn't wait. Applies after a change, and finishes with what pick
        /// takes from the result.
        internal static Future<T> FocusAsync<T>(NativeStateMachineHandle stateMachine, FocusOp op, Func<FocusResult, T> pick, uint flags = 0)
        {
            return InputAsync(id => SendFocus(id, stateMachine, op, flags | FocusApply), flags, pick);
        }

        /// Like FocusAsync, for a key.
        internal static Future<T> KeyAsync<T>(
            NativeStateMachineHandle stateMachine, RiveKey key, RiveKeyModifiers modifiers, bool pressed, bool repeat, Func<FocusResult, T> pick)
        {
            return InputAsync(id => SendKey(id, stateMachine, key, modifiers, pressed, repeat, FocusApply), 0, pick);
        }

        /// Like FocusAsync, for committed text.
        internal static Future<T> TextAsync<T>(NativeStateMachineHandle stateMachine, string text, Func<FocusResult, T> pick)
        {
            return InputAsync(id => SendText(id, stateMachine, text, FocusApply), 0, pick);
        }

        private static FocusResult InputAndWait(Action<ulong> send, uint flags)
        {
            FocusResult result = default;
            RequestTicket ticket = CommandTransport.Send(
                send,
                (batch, message) =>
                {
                    var reader = new PayloadReader(batch, message);
                    result = ReadFocus(ref reader, null, flags);
                });
            CommandTransport.Join(ref ticket);
            return result;
        }

        private static Future<T> InputAsync<T>(Action<ulong> send, uint flags, Func<FocusResult, T> pick)
        {
            return CommandTransport.SendFuture(
                send,
                (batch, message) =>
                {
                    var reader = new PayloadReader(batch, message);
                    return pick(ReadFocus(ref reader, null, flags));
                });
        }

        internal enum PointerKind : uint
        {
            Down = 0,
            Up = 1,
            Move = 2,
            Exit = 3,
            HitTest = 4,
        }

        /// x and y are in the frame, and map into the artboard with fit and
        /// alignment.
        internal const uint PointerInFrame = 1 << 0;
        /// A down or up that hits advances by 0.
        internal const uint PointerSettle = 1 << 1;
        internal const uint PointerCollectEvents = 1 << 2;

        [DllImport(NativeLibrary.name)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool riveSmPointer(
            ulong requestId,
            ulong stateMachine,
            ulong artboard,
            uint kind,
            float x,
            float y,
            int pointerId,
            uint flags,
            float minX,
            float minY,
            float maxX,
            float maxY,
            byte fit,
            float alignX,
            float alignY);

        [DllImport(NativeLibrary.name)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool riveCancelPointer(ulong requestId);

        /// True if the pointer request hadn't run yet, and now won't.
        internal static bool CancelPointer(ulong requestId) => riveCancelPointer(requestId);

        /// Sends a pointer event with the given requestId. The reply is read
        /// with ReadPointer.
        internal static bool SendPointer(
            ulong requestId,
            NativeStateMachineHandle stateMachine,
            NativeArtboardHandle artboard,
            PointerKind kind,
            UnityEngine.Vector2 point,
            int pointerId,
            uint flags,
            UnityEngine.Rect frame,
            Fit fit,
            Alignment alignment)
        {
            return riveSmPointer(
                requestId, stateMachine.Value, artboard.Value, (uint)kind, point.x, point.y, pointerId, flags,
                frame.xMin, frame.yMin, frame.xMax, frame.yMax, (byte)fit, alignment.X, alignment.Y);
        }

        /// The hit, or 0 when the state machine is gone. Events go into events
        /// when it collected them.
        internal static uint ReadPointer(ref PayloadReader reader, List<ReportedEventData> events)
        {
            if (!reader.Ok())
            {
                return 0;
            }
            uint hit = reader.U32();
            ReadEvents(ref reader, events);
            return hit;
        }

        /// A point already in the artboard. Waits.
        private static uint PointerAndWait(NativeStateMachineHandle stateMachine, PointerKind kind, float x, float y, int pointerId)
        {
            PointerCall call = t_pointerCall ?? new PointerCall();
            // Out while in use, so a call made while this one waits gets its own.
            t_pointerCall = null;
            call.StateMachine = stateMachine.Value;
            call.Kind = (uint)kind;
            call.X = x;
            call.Y = y;
            call.PointerId = pointerId;
            call.Hit = 0;
            RequestTicket ticket = CommandTransport.Send(call.Send, call.OnReply);
            CommandTransport.Join(ref ticket);
            uint hit = call.Hit;
            t_pointerCall = call;
            return hit;
        }

        // Translucent hit testing waits on one of these every raycast, so the
        // call and its delegates are made once per thread rather than per call.
        private sealed class PointerCall
        {
            internal readonly Action<ulong> Send;
            internal readonly Action<HostMessageBatch, HostMessage> OnReply;
            internal ulong StateMachine;
            internal uint Kind;
            internal float X;
            internal float Y;
            internal int PointerId;
            internal uint Hit;

            internal PointerCall()
            {
                Send = id => riveSmPointer(id, StateMachine, 0, Kind, X, Y, PointerId, 0, 0, 0, 0, 0, 0, 0, 0);
                OnReply = (batch, message) =>
                {
                    var reader = new PayloadReader(batch, message);
                    Hit = ReadPointer(ref reader, null);
                };
            }
        }

        [ThreadStatic]
        private static PointerCall t_pointerCall;

        internal static byte PointerMoveStateMachineWithHit(NativeStateMachineHandle smi, float x, float y, int pointerId)
        {
            return (byte)PointerAndWait(smi, PointerKind.Move, x, y, pointerId);
        }

        internal static byte PointerDownStateMachineWithHit(NativeStateMachineHandle smi, float x, float y, int pointerId)
        {
            return (byte)PointerAndWait(smi, PointerKind.Down, x, y, pointerId);
        }

        internal static byte PointerUpStateMachineWithHit(NativeStateMachineHandle smi, float x, float y, int pointerId)
        {
            return (byte)PointerAndWait(smi, PointerKind.Up, x, y, pointerId);
        }

        internal static byte PointerExitStateMachineWithHit(NativeStateMachineHandle smi, float x, float y, int pointerId)
        {
            return (byte)PointerAndWait(smi, PointerKind.Exit, x, y, pointerId);
        }

        internal static bool HitTestStateMachine(NativeStateMachineHandle stateMachine, float x, float y)
        {
            return PointerAndWait(stateMachine, PointerKind.HitTest, x, y, 0) != 0;
        }

        // Data binding

        /// Sets the instance and binds without waiting. What the bind leaves
        /// bound comes back under a handle of its own, which is let go.
        internal static void BindInstanceLater(NativeStateMachineHandle stateMachine, NativeViewModelInstanceHandle instance)
        {
            CommandTransport.SendNoReply(() =>
            {
                ViewModelNative.riveSmSetMain(0, stateMachine.Value, instance.Value);
                ulong bound = ViewModelNative.riveSmBind(0, stateMachine.Value, 0);
                ViewModelNative.riveVmiRelease(bound);
            });
        }

        internal static void SetViewModelInstanceOnStateMachine(NativeStateMachineHandle stateMachine, NativeViewModelInstanceHandle viewModelInstance)
        {
            ViewModelNative.SetMain(stateMachine, viewModelInstance);
        }

        internal static bool SetGlobalViewModelInstanceOnStateMachine(
            NativeStateMachineHandle stateMachine,
            string name,
            NativeViewModelInstanceHandle viewModelInstance)
        {
            return ViewModelNative.SetGlobal(stateMachine, name, viewModelInstance);
        }

        internal static NativeViewModelInstanceHandle GetGlobalViewModelInstanceFromStateMachine(NativeStateMachineHandle stateMachine, string name)
        {
            return ViewModelNative.Global(stateMachine, name);
        }

        /// Binds without waiting. landed gets the main instance after the bind,
        /// on the main thread.
        internal static void BindLater(NativeStateMachineHandle stateMachine, Action<NativeViewModelInstanceHandle> landed)
        {
            CommandTransport.Send(
                id => ViewModelNative.riveSmBind(id, stateMachine.Value, 0),
                (batch, message) =>
                {
                    var reader = new PayloadReader(batch, message);
                    NativeViewModelInstanceHandle bound = ViewModelNative.ReadInstance(ref reader);
                    CommandTransport.RunOnMainThread(() => landed(bound));
                },
                keep: false);
        }

        /// Binds, and gives the main instance after the bind.
        internal static NativeViewModelInstanceHandle BindStateMachine(NativeStateMachineHandle stateMachine)
        {
            return ViewModelNative.Bind(stateMachine);
        }
    }
}
