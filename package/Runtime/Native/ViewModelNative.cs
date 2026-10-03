using System;
using System.Runtime.InteropServices;
using Rive.Host;
using Rive.Producer;

namespace Rive
{
    /// <summary>
    /// The host calls behind view models, their instances and properties. The
    /// externs are shared with <see cref="ViewModelInstanceNative"/>, which
    /// queues them for handles. The methods here wait, for the plain API.
    /// </summary>
    internal static class ViewModelNative
    {
        /// Mirrors the kinds riveVmiCreate takes.
        internal enum InstanceKind : uint
        {
            Default = 0,
            Named = 1,
            Blank = 2,
        }

        /// Mirrors ListOp in viewmodel_routines.cpp.
        internal enum ListOp : uint
        {
            Size = 1,
            Add = 2,
            InsertAt = 3,
            Remove = 4,
            RemoveAt = 5,
            Swap = 6,
            Clear = 7,
        }

        /// What a capture sends for a property nobody has started watching.
        internal const ulong NotWatching = ulong.MaxValue;

        /// A seen stamp with this bit set is a watch token: the watcher's
        /// start is out, and native knows its stamp.
        internal const ulong WatchToken = 1UL << 63;

        internal static bool IsWatchToken(ulong seen) => seen != NotWatching && (seen & WatchToken) != 0;

        private static long s_nextWatchToken;

        internal static ulong NextWatchToken() =>
            (ulong)System.Threading.Interlocked.Increment(ref s_nextWatchToken) | WatchToken;

        // Calls that give an instance return a new handle straight away and
        // reply with the canonical one.

        [DllImport(NativeLibrary.name)]
        internal static extern ulong riveVmiCreate(ulong requestId, ulong file, uint viewModel, uint kind, [MarshalAs(NativeText.Encoding)] string name);

        [DllImport(NativeLibrary.name)]
        internal static extern ulong riveVmiNested(ulong requestId, ulong instance, [MarshalAs(NativeText.Encoding)] string path);

        [DllImport(NativeLibrary.name)]
        internal static extern ulong riveVmiListItem(ulong requestId, ulong instance, [MarshalAs(NativeText.Encoding)] string path, int index);

        [DllImport(NativeLibrary.name)]
        internal static extern ulong riveVmiMain(ulong requestId, ulong stateMachine);

        [DllImport(NativeLibrary.name)]
        internal static extern ulong riveVmiGlobal(ulong requestId, ulong stateMachine, [MarshalAs(NativeText.Encoding)] string name);

        [DllImport(NativeLibrary.name)]
        internal static extern ulong riveSmBind(ulong requestId, ulong stateMachine, ulong expected);

        [DllImport(NativeLibrary.name)]
        internal static extern void riveVmiRelease(ulong instance);

        [DllImport(NativeLibrary.name)]
        [return: MarshalAs(UnmanagedType.U1)]
        internal static extern bool riveVmiInfo(ulong requestId, ulong instance);

        [DllImport(NativeLibrary.name)]
        [return: MarshalAs(UnmanagedType.U1)]
        internal static extern bool riveVmiResolve(ulong requestId, ulong instance, [MarshalAs(NativeText.Encoding)] string path, uint type);

        [DllImport(NativeLibrary.name)]
        [return: MarshalAs(UnmanagedType.U1)]
        internal static extern bool riveVmiRead(ulong requestId, ulong instance, [MarshalAs(NativeText.Encoding)] string path, uint type);

        [DllImport(NativeLibrary.name)]
        internal static extern void riveVmiSet(
            ulong requestId,
            ulong instance,
            [MarshalAs(NativeText.Encoding)] string path,
            uint type,
            float number,
            int integer,
            [MarshalAs(NativeText.Encoding)] string text);

        [DllImport(NativeLibrary.name)]
        internal static extern void riveVmiSetAsset(ulong requestId, ulong instance, [MarshalAs(NativeText.Encoding)] string path, uint type, ulong asset);

        [DllImport(NativeLibrary.name)]
        internal static extern void riveVmiSetArtboard(ulong requestId, ulong instance, [MarshalAs(NativeText.Encoding)] string path, ulong artboard, ulong boundInstance);

        [DllImport(NativeLibrary.name)]
        [return: MarshalAs(UnmanagedType.U1)]
        internal static extern bool riveVmiList(ulong requestId, ulong instance, [MarshalAs(NativeText.Encoding)] string path, uint op, ulong item, int index, int other);

        [DllImport(NativeLibrary.name)]
        internal static extern void riveVmiReplaceNested(ulong requestId, ulong instance, [MarshalAs(NativeText.Encoding)] string path, ulong replacement);

        [DllImport(NativeLibrary.name)]
        [return: MarshalAs(UnmanagedType.U1)]
        internal static extern bool riveVmiEnumType(ulong requestId, ulong instance, ulong file, [MarshalAs(NativeText.Encoding)] string name);

        [DllImport(NativeLibrary.name)]
        [return: MarshalAs(UnmanagedType.U1)]
        internal static extern bool riveVmiEnumValues(ulong requestId, ulong instance, [MarshalAs(NativeText.Encoding)] string path);

        [DllImport(NativeLibrary.name)]
        [return: MarshalAs(UnmanagedType.U1)]
        internal static extern bool riveVmiChangeSequence(ulong requestId);

        [DllImport(NativeLibrary.name)]
        [return: MarshalAs(UnmanagedType.U1)]
        internal static extern bool riveVmiStartWatch(ulong requestId, ulong token);

        [DllImport(NativeLibrary.name)]
        internal static extern void riveVmiEndWatchToken(ulong token);

        [DllImport(NativeLibrary.name)]
        [return: MarshalAs(UnmanagedType.U1)]
        internal static extern bool riveVmiCapture(ulong requestId, byte[] watched, uint size);

        [DllImport(NativeLibrary.name)]
        internal static extern void riveSmSetMain(ulong requestId, ulong stateMachine, ulong instance);

        [DllImport(NativeLibrary.name)]
        internal static extern void riveSmSetGlobal(ulong requestId, ulong stateMachine, [MarshalAs(NativeText.Encoding)] string name, ulong instance);

        [DllImport(NativeLibrary.name)]
        internal static extern void riveArtboardBindVmi(ulong artboard, ulong instance);

        /// The canonical handle from a reply that starts with one.
        internal static NativeViewModelInstanceHandle ReadInstance(ref PayloadReader reader)
        {
            return new NativeViewModelInstanceHandle(reader.U64());
        }

        // Sends one instance call and waits for its canonical handle. The
        // handle it was sent under becomes an alias, so the claim goes with
        // the canonical one.
        private static NativeViewModelInstanceHandle WaitForInstance(Func<ulong, ulong> send)
        {
            NativeViewModelInstanceHandle canonical = default;
            RequestTicket ticket = CommandTransport.Send(
                id => send(id),
                (batch, message) =>
                {
                    var reader = new PayloadReader(batch, message);
                    canonical = ReadInstance(ref reader);
                });
            CommandTransport.Join(ref ticket);
            return canonical;
        }

        /// Doesn't wait. A new instance has no other handle, so the one given
        /// back is the canonical one. The caller checks a name against the
        /// file first, since that's the only way it can fail.
        internal static NativeViewModelInstanceHandle Create(NativeFileHandle file, uint viewModel, InstanceKind kind, string name)
        {
            ulong handle = 0;
            CommandTransport.SendNoReply(() => handle = riveVmiCreate(0, file.Value, viewModel, (uint)kind, name));
            return new NativeViewModelInstanceHandle(handle);
        }

        internal static NativeViewModelInstanceHandle Nested(NativeViewModelInstanceHandle instance, string path)
        {
            return WaitForInstance(id => riveVmiNested(id, instance.Value, path));
        }

        internal static NativeViewModelInstanceHandle ListItem(NativeViewModelInstanceHandle instance, string path, int index)
        {
            return WaitForInstance(id => riveVmiListItem(id, instance.Value, path, index));
        }

        internal static NativeViewModelInstanceHandle Main(NativeStateMachineHandle stateMachine)
        {
            return WaitForInstance(id => riveVmiMain(id, stateMachine.Value));
        }

        internal static NativeViewModelInstanceHandle Global(NativeStateMachineHandle stateMachine, string name)
        {
            return WaitForInstance(id => riveVmiGlobal(id, stateMachine.Value, name));
        }

        /// Binds and gives the main instance after the bind.
        internal static NativeViewModelInstanceHandle Bind(NativeStateMachineHandle stateMachine)
        {
            return WaitForInstance(id => riveSmBind(id, stateMachine.Value, 0));
        }

        /// Any thread, finalizers included.
        internal static void Release(NativeViewModelInstanceHandle instance)
        {
            if (instance.IsValid)
            {
                CommandTransport.SendNoReply(() => riveVmiRelease(instance.Value));
            }
        }

        internal struct Info
        {
            internal bool Found;
            internal string ViewModelName;
            internal string Name;
            internal int RefCount;
        }

        internal static Info ReadInfo(ref PayloadReader reader)
        {
            var info = new Info { Found = reader.Ok() };
            if (info.Found)
            {
                info.ViewModelName = reader.String();
                info.Name = reader.String();
                info.RefCount = (int)reader.U32();
            }
            return info;
        }

        internal static Info GetInfo(NativeViewModelInstanceHandle instance)
        {
            Info info = default;
            RequestTicket ticket = CommandTransport.Send(
                id => riveVmiInfo(id, instance.Value),
                (batch, message) =>
                {
                    var reader = new PayloadReader(batch, message);
                    info = ReadInfo(ref reader);
                });
            CommandTransport.Join(ref ticket);
            return info;
        }

        /// True when there's a property of the type at the path.
        internal static bool HasProperty(NativeViewModelInstanceHandle instance, string path, ViewModelDataType type)
        {
            bool found = false;
            RequestTicket ticket = CommandTransport.Send(
                id => riveVmiResolve(id, instance.Value, path, (uint)type),
                (batch, message) => found = new PayloadReader(batch, message).Ok());
            CommandTransport.Join(ref ticket);
            return found;
        }

        internal delegate T ValueReader<T>(ref PayloadReader reader);

        /// The value, or fallback when the property isn't there.
        internal static T Read<T>(
            NativeViewModelInstanceHandle instance, string path, ViewModelDataType type, ValueReader<T> read, T fallback)
        {
            T value = fallback;
            RequestTicket ticket = CommandTransport.Send(
                id => riveVmiRead(id, instance.Value, path, (uint)type),
                (batch, message) =>
                {
                    var reader = new PayloadReader(batch, message);
                    if (reader.Ok())
                    {
                        value = read(ref reader);
                    }
                });
            CommandTransport.Join(ref ticket);
            return value;
        }

        /// Held and sent in order ahead of the next thing that's sent.
        internal static void Set(
            NativeViewModelInstanceHandle instance, string path, ViewModelDataType type, float number, int integer, string text)
        {
            CommandTransport.Write(() => riveVmiSet(0, instance.Value, path, (uint)type, number, integer, text));
        }

        /// Waits. False when the property isn't there.
        internal static bool SetAsset(NativeViewModelInstanceHandle instance, string path, ViewModelDataType type, NativeAssetHandle asset)
        {
            bool found = false;
            RequestTicket ticket = CommandTransport.Send(
                id => riveVmiSetAsset(id, instance.Value, path, (uint)type, asset.Value),
                (batch, message) => found = new PayloadReader(batch, message).Ok());
            CommandTransport.Join(ref ticket);
            return found;
        }

        /// Waits. False when the property isn't there.
        internal static bool SetArtboard(
            NativeViewModelInstanceHandle instance, string path, NativeArtboardHandle artboard, NativeViewModelInstanceHandle bound)
        {
            bool found = false;
            RequestTicket ticket = CommandTransport.Send(
                id => riveVmiSetArtboard(id, instance.Value, path, artboard.Value, bound.Value),
                (batch, message) => found = new PayloadReader(batch, message).Ok());
            CommandTransport.Join(ref ticket);
            return found;
        }

        internal struct ListResult
        {
            internal bool Found;
            internal bool Ok;
            internal int Count;
        }

        internal static ListResult ReadList(ref PayloadReader reader)
        {
            return new ListResult { Found = reader.Ok(), Ok = reader.Bool(), Count = (int)reader.U32() };
        }

        /// Waits for the op and gives the list's size after it.
        internal static ListResult List(
            NativeViewModelInstanceHandle instance, string path, ListOp op, NativeViewModelInstanceHandle item = default, int index = 0, int other = 0)
        {
            ListResult result = default;
            RequestTicket ticket = CommandTransport.Send(
                id => riveVmiList(id, instance.Value, path, (uint)op, item.Value, index, other),
                (batch, message) =>
                {
                    var reader = new PayloadReader(batch, message);
                    result = ReadList(ref reader);
                });
            CommandTransport.Join(ref ticket);
            return result;
        }

        /// Waits. False when path isn't a nested view model that can take it.
        internal static bool ReplaceNested(
            NativeViewModelInstanceHandle instance, string path, NativeViewModelInstanceHandle replacement)
        {
            bool ok = false;
            RequestTicket ticket = CommandTransport.Send(
                id => riveVmiReplaceNested(id, instance.Value, path, replacement.Value),
                (batch, message) => ok = new PayloadReader(batch, message).Ok());
            CommandTransport.Join(ref ticket);
            return ok;
        }

        /// The index into the file's enums of the enum property's type, -1
        /// when it isn't one of them, with the enum's values.
        internal static int EnumType(NativeViewModelInstanceHandle instance, NativeFileHandle file, string name, out string[] values)
        {
            int index = -1;
            string[] read = Array.Empty<string>();
            RequestTicket ticket = CommandTransport.Send(
                id => riveVmiEnumType(id, instance.Value, file.Value, name),
                (batch, message) =>
                {
                    var reader = new PayloadReader(batch, message);
                    index = (int)reader.U32();
                    read = ReadStrings(ref reader);
                });
            CommandTransport.Join(ref ticket);
            values = read;
            return index;
        }

        /// A u32 count, then each string.
        internal static string[] ReadStrings(ref PayloadReader reader)
        {
            var strings = new string[reader.U32()];
            for (int i = 0; i < strings.Length; i++)
            {
                strings[i] = reader.String();
            }
            return strings;
        }

        /// The newest change stamp handed out so far.
        internal static ulong ChangeSequence()
        {
            ulong sequence = 0;
            RequestTicket ticket = CommandTransport.Send(
                id => riveVmiChangeSequence(id),
                (batch, message) => sequence = new PayloadReader(batch, message).U64());
            CommandTransport.Join(ref ticket);
            return sequence;
        }

        /// Finishes on the main thread once everything sent before it has run.
        internal static Future FenceAsync()
        {
            return new Future(FutureState<bool>.Then(
                CommandTransport.SendFuture(id => riveVmiChangeSequence(id), (batch, message) => true),
                (landed, state) =>
                {
                    if (!state.TryForwardFailure(landed))
                    {
                        state.Succeed(true);
                    }
                }));
        }

        private static readonly PayloadWriter s_oneWatched = new PayloadWriter();

        /// Waits. True when the value changed after the stamp seen.
        internal static bool ChangedSince(NativeViewModelInstanceHandle instance, string path, ViewModelDataType type, ulong seen)
        {
            bool changed = false;
            RequestTicket ticket = CommandTransport.Send(
                id =>
                {
                    PayloadWriter writer = s_oneWatched;
                    writer.Clear();
                    writer.U64(instance.Value);
                    writer.String(path);
                    writer.U32((uint)type);
                    writer.U64(seen);
                    riveVmiCapture(id, writer.Bytes, (uint)writer.Size);
                },
                (batch, message) =>
                {
                    var reader = new PayloadReader(batch, message);
                    reader.U64();
                    changed = reader.U32() > 0;
                });
            CommandTransport.Join(ref ticket);
            return changed;
        }

        /// Waits. False when the state machine or instance isn't there.
        internal static bool SetMain(NativeStateMachineHandle stateMachine, NativeViewModelInstanceHandle instance)
        {
            bool ok = false;
            RequestTicket ticket = CommandTransport.Send(
                id => riveSmSetMain(id, stateMachine.Value, instance.Value),
                (batch, message) => ok = new PayloadReader(batch, message).Ok());
            CommandTransport.Join(ref ticket);
            return ok;
        }

        /// Waits. False when the name isn't a global, or something isn't there.
        internal static bool SetGlobal(NativeStateMachineHandle stateMachine, string name, NativeViewModelInstanceHandle instance)
        {
            bool ok = false;
            RequestTicket ticket = CommandTransport.Send(
                id => riveSmSetGlobal(id, stateMachine.Value, name, instance.Value),
                (batch, message) => ok = new PayloadReader(batch, message).Ok());
            CommandTransport.Join(ref ticket);
            return ok;
        }

        /// Held, like other writes.
        internal static void BindToArtboard(NativeArtboardHandle artboard, NativeViewModelInstanceHandle instance)
        {
            CommandTransport.Write(() => riveArtboardBindVmi(artboard.Value, instance.Value));
        }
    }
}
