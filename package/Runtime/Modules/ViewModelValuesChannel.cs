using System;
using System.Collections.Generic;
using Rive.Producer;
using Rive.Utils;
using Rive.Host;

namespace Rive
{
    /// <summary>
    /// A property a capture looks at: a plain property or a handle.
    /// </summary>
    internal interface IWatchedValue
    {
        NativeViewModelInstanceHandle WatchInstance { get; }
        string WatchPath { get; }
        ViewModelDataType WatchType { get; }

        /// The newest stamp its watcher has seen, NotWatching until it
        /// starts, or a watch token while its start is out.
        ulong SeenChange { get; set; }

        /// Left out of captures.
        bool Gone { get; }
    }

    /// <summary>
    /// Everything that produces a view model value goes through one Fifo
    /// channel: the tick capture, the capture after each pointer job or
    /// handle advance, and GetValueAsync reads, for both plain properties
    /// and property handles. Rive runs them in order and they're delivered
    /// in order, so a read and a change callback can't overtake each other.
    /// Callbacks and reads finish in the channel's afterLanded step, outside
    /// the delivery window. Main thread only.
    /// </summary>
    internal sealed class ViewModelValuesChannel
    {
        private enum Kind
        {
            TickCapture = 0,
            Capture = 1,
            Read = 2,
        }

        private interface IRead
        {
            void Send(ulong requestId);
            void Parse(Result result, ref PayloadReader reader);
            void Complete(Result result);
            void CompleteDirect();
            void Cancel();

            /// Back to its pool, once the channel is done with it.
            void Release();
        }

        private sealed class Request
        {
            internal readonly List<ViewModelInstancePrimitiveProperty> Properties =
                new List<ViewModelInstancePrimitiveProperty>();
            internal readonly List<ViewModelPropertyHandle> Handles = new List<ViewModelPropertyHandle>();
            // What the capture sent, in the order native indexes it.
            internal readonly List<IWatchedValue> Watched = new List<IWatchedValue>();
            internal Kind Kind;
            internal IRead Read;
            internal bool Delivered;
        }

        private sealed class Result
        {
            internal readonly List<PropertyCallbacksHub.CapturedChange> Changed =
                new List<PropertyCallbacksHub.CapturedChange>();
            internal readonly List<HandleChange> HandleChanged = new List<HandleChange>();
            internal PropertyValue Value;
            internal Exception Error;
        }

        // One per GetValueAsync, pooled, with a pooled future state. Its wait
        // hook is made once, so a read allocates nothing once warmed up.
        private sealed class ValueRead<T> : IRead
        {
            private static readonly Stack<ValueRead<T>> s_pool = new Stack<ValueRead<T>>();

            private readonly Action m_wait;
            private ViewModelValuesChannel m_channel;
            private ViewModelInstancePrimitiveProperty<T> m_property;
            private FutureState<T> m_state;
            // The state is pooled, so it's only finished at the version it had.
            private int m_version;

            private ValueRead()
            {
                m_wait = () => m_channel.WaitForRead(this);
            }

            internal static ValueRead<T> Rent(
                ViewModelValuesChannel channel, ViewModelInstancePrimitiveProperty<T> property, FutureState<T> state)
            {
                ValueRead<T> read = s_pool.Count > 0 ? s_pool.Pop() : new ValueRead<T>();
                read.m_channel = channel;
                read.m_property = property;
                read.m_state = state;
                read.m_version = state.Version;
                state.WaitDriver = read.m_wait;
                return read;
            }

            public void Release()
            {
                m_state.ClearWaitDriver(m_version);
                m_channel = null;
                m_property = null;
                m_state = null;
                s_pool.Push(this);
            }

            public void Send(ulong requestId)
            {
                ViewModelInstanceNative.SendRead(requestId, m_property.InstanceHandle, m_property.Name, m_property.PropertyType);
            }

            // Leaves the stamp, so OnValueChanged still fires for the change.
            public void Parse(Result result, ref PayloadReader reader)
            {
                if (m_property.RootInstance != null && m_property.RootInstance.IsDisposed)
                {
                    result.Error = new ObjectDisposedException(
                        nameof(ViewModelInstance), "The property's ViewModelInstance was disposed.");
                    return;
                }
                result.Value = reader.Ok()
                    ? ViewModelInstanceNative.ReadValue(m_property.PropertyType, ref reader)
                    : default;
            }

            public void Complete(Result result)
            {
                if (result.Error != null)
                {
                    m_state.Fail(result.Error, m_version);
                    return;
                }
                m_state.Succeed(m_property.FromValue(result.Value), m_version);
            }

            // Main thread, inside a callback, where joining would wait on
            // entries behind the one being delivered. The queued read finds
            // it done later and does nothing.
            public void CompleteDirect()
            {
                try
                {
                    if (m_property.RootInstance != null && m_property.RootInstance.IsDisposed)
                    {
                        throw new ObjectDisposedException(
                            nameof(ViewModelInstance), "The property's ViewModelInstance was disposed.");
                    }
                    m_state.Succeed(m_property.Value, m_version);
                }
                catch (Exception e)
                {
                    m_state.Fail(e, m_version);
                }
            }

            public void Cancel()
            {
                m_state.Cancel(m_version);
            }
        }

        private struct HandleChange
        {
            internal ViewModelPropertyHandle Handle;
            internal PropertyValue Value;
        }

        // One per GetValueAsync on a property handle, pooled like ValueRead.
        private sealed class HandleRead<T> : IRead
        {
            private static readonly Stack<HandleRead<T>> s_pool = new Stack<HandleRead<T>>();

            private readonly Action m_wait;
            private ViewModelValuesChannel m_channel;
            private ViewModelPropertyHandle m_handle;
            private FutureState<T> m_state;
            private int m_version;

            private HandleRead()
            {
                m_wait = () => m_channel.WaitForRead(this);
            }

            internal static HandleRead<T> Rent(
                ViewModelValuesChannel channel, ViewModelPropertyHandle handle, FutureState<T> state)
            {
                HandleRead<T> read = s_pool.Count > 0 ? s_pool.Pop() : new HandleRead<T>();
                read.m_channel = channel;
                read.m_handle = handle;
                read.m_state = state;
                read.m_version = state.Version;
                state.WaitDriver = read.m_wait;
                return read;
            }

            public void Release()
            {
                m_state.ClearWaitDriver(m_version);
                m_channel = null;
                m_handle = null;
                m_state = null;
                s_pool.Push(this);
            }

            private T FromValue(in PropertyValue value) => ((IPropertyValueOf<T>)m_handle).FromValue(value);

            public void Send(ulong requestId)
            {
                ViewModelInstanceNative.SendRead(requestId, m_handle.Root, m_handle.Path, m_handle.Type);
            }

            // Leaves the stamp, like ValueRead.
            public void Parse(Result result, ref PayloadReader reader)
            {
                if (m_handle.Instance.IsDisposed)
                {
                    result.Error = new RiveException(RiveErrorCode.ResourceDisposed,
                        $"The view model instance for '{m_handle.Path}' has been disposed.");
                    return;
                }
                if (!reader.Ok())
                {
                    result.Error = m_handle.Missing();
                    return;
                }
                result.Value = ViewModelInstanceNative.ReadValue(m_handle.Type, ref reader);
            }

            public void Complete(Result result)
            {
                if (result.Error != null)
                {
                    m_state.Fail(result.Error, m_version);
                    return;
                }
                m_state.Succeed(FromValue(result.Value), m_version);
            }

            // Only when someone waits on the read inside a callback, which
            // asked for a wait.
            public void CompleteDirect()
            {
                try
                {
                    m_state.Succeed(FromValue(ViewModelInstanceNative.ReadNow(m_handle)), m_version);
                }
                catch (Exception e)
                {
                    m_state.Fail(e, m_version);
                }
            }

            public void Cancel()
            {
                m_state.Cancel(m_version);
            }
        }

        private static readonly Unity.Profiling.ProfilerMarker s_deliverMarker =
            new Unity.Profiling.ProfilerMarker("Rive.Values.Deliver");

        private readonly ProducerChannel<Request, Result> m_channel;
        private readonly Action<List<ViewModelInstancePrimitiveProperty>, List<ViewModelPropertyHandle>> m_snapshot;
        private readonly Action m_poll;
        private Request m_waitingTickCapture;
        // Recycling without delivering is expected here, not a loss.
        private bool m_quiet;
        private bool m_lostCapture;
        private bool m_capturedAny;

        internal ViewModelValuesChannel(
            Action<List<ViewModelInstancePrimitiveProperty>, List<ViewModelPropertyHandle>> snapshot)
        {
            m_snapshot = snapshot;
            m_channel = new ProducerChannel<Request, Result>(
                ChannelMode.Fifo,
                SendRequest,
                ParseReply,
                null,
                Reset,
                AfterLanded);
            m_poll = Poll;
            // A read the stopped host took with it is cancelled when it's
            // polled, and the frame loop may not poll again, as on quit or a
            // domain reload. A wait on another thread would hang until then.
            // The hub owns one channel for the domain, so this never unhooks.
            CommandTransport.Stopped += m_poll;
        }

        internal bool Pending => m_channel.HasInFlight;

        /// True while a callback or read from this channel is being delivered.
        internal bool Delivering => m_channel.InAfterLanded;

        /// After the tick pass. Skipped while an earlier one hasn't started, and
        /// the next one picks up whatever this would have.
        internal void SendTickCapture()
        {
            if (m_waitingTickCapture != null && m_channel.IsUnstarted(m_waitingTickCapture))
            {
                return;
            }
            Request request = BeginCapture(Kind.TickCapture);
            if (request == null)
            {
                return;
            }
            m_waitingTickCapture = request;
            Send(request);
        }

        /// Straight after a pointer job, from the same call, so the value it
        /// set is captured before anything after it. Returns what to pass to
        /// TryCancelCapture, or null if nothing's subscribed.
        internal object SendCapture()
        {
            Request request = BeginCapture(Kind.Capture);
            if (request == null)
            {
                return null;
            }
            Send(request);
            return request;
        }

        /// For a merged-away pointer move, whose capture the next one covers.
        internal void TryCancelCapture(object capture)
        {
            if (!(capture is Request request))
            {
                return;
            }
            m_quiet = true;
            try
            {
                m_channel.TryCancel(request);
            }
            finally
            {
                m_quiet = false;
            }
        }

        internal Future<T> Read<T>(ViewModelInstancePrimitiveProperty<T> property)
        {
            var state = FutureState<T>.Rent();
            SendRead(ValueRead<T>.Rent(this, property, state));
            return new Future<T>(state);
        }

        internal Future<T> Read<T>(ViewModelPropertyHandle handle)
        {
            var state = FutureState<T>.Rent();
            SendRead(HandleRead<T>.Rent(this, handle, state));
            return new Future<T>(state);
        }

        /// After a handle advance, which has no panel to capture for it.
        internal void SendCaptureWithoutPanel()
        {
            if (SendCapture() != null)
            {
                PollWithoutPanel();
            }
        }

        // With no panels the frame loop's early step delivers this channel.
        private void PollWithoutPanel()
        {
            RiveFrameLoop.AddEarly(m_poll);
            RiveFrameLoop.Ensure();
        }

        private void SendRead(IRead read)
        {
            PollWithoutPanel();
            Request request = m_channel.Begin();
            request.Kind = Kind.Read;
            request.Read = read;
            Send(request);
        }

        /// Delivers what has landed, in order. Never waits.
        internal void Poll()
        {
            m_channel.Poll();
        }

        /// Waits for everything sent and delivers it. False when a capture went
        /// with the producer. Inside a delivery from this channel it does
        /// nothing and the outer loop carries on.
        internal bool Join()
        {
            m_lostCapture = false;
            m_channel.JoinAll();
            return !m_lostCapture;
        }

        /// Captures now and delivers it after everything sent before it. True
        /// if it found changes.
        internal bool CaptureNow()
        {
            m_capturedAny = false;
            SendCapture();
            Join();
            return m_capturedAny;
        }

        private Request BeginCapture(Kind kind)
        {
            Request request = m_channel.Begin();
            m_snapshot(request.Properties, request.Handles);
            if (request.Properties.Count == 0 && request.Handles.Count == 0)
            {
                m_quiet = true;
                try
                {
                    m_channel.Discard(request);
                }
                finally
                {
                    m_quiet = false;
                }
                return null;
            }
            request.Kind = kind;
            return request;
        }

        private void Send(Request request)
        {
            m_channel.Send(request);
            if (!CommandTransport.IsThreaded)
            {
                // It ran inline, so deliver it in call order now.
                m_channel.Poll();
            }
        }

        private void WaitForRead(IRead read)
        {
            if (m_channel.InAfterLanded)
            {
                read.CompleteDirect();
                return;
            }
            Join();
        }

        private static readonly PayloadWriter s_watched = new PayloadWriter();

        // A read is one routine. A capture is one routine listing every
        // watched property with the stamp its watcher has seen.
        private static void SendRequest(Request request, ulong requestId)
        {
            if (request.Kind == Kind.Read)
            {
                request.Read.Send(requestId);
                return;
            }
            request.Watched.Clear();
            for (int i = 0; i < request.Properties.Count; i++)
            {
                IWatchedValue property = request.Properties[i];
                if (!property.Gone)
                {
                    request.Watched.Add(property);
                }
            }
            for (int i = 0; i < request.Handles.Count; i++)
            {
                IWatchedValue handle = request.Handles[i];
                if (!handle.Gone)
                {
                    request.Watched.Add(handle);
                }
            }
            PayloadWriter writer = s_watched;
            writer.Clear();
            for (int i = 0; i < request.Watched.Count; i++)
            {
                IWatchedValue watched = request.Watched[i];
                writer.U64(watched.WatchInstance.Value);
                writer.String(watched.WatchPath);
                writer.U32((uint)watched.WatchType);
                writer.U64(watched.SeenChange);
            }
            ViewModelInstanceNative.SendCapture(requestId, writer);
        }

        // In the drain, in send order. A change counts once, so two captures
        // out at once can't both report it.
        private static void ParseReply(Request request, Result result, HostMessageBatch batch, HostMessage message)
        {
            var reader = new PayloadReader(batch, message);
            if (request.Kind == Kind.Read)
            {
                request.Read.Parse(result, ref reader);
                return;
            }
            ulong newest = reader.U64();
            uint changes = reader.U32();
            for (uint i = 0; i < changes; i++)
            {
                int index = (int)reader.U32();
                ulong stamp = reader.U64();
                if (index < 0 || index >= request.Watched.Count)
                {
                    return;
                }
                IWatchedValue watched = request.Watched[index];
                PropertyValue value = ViewModelInstanceNative.ReadValue(watched.WatchType, ref reader);
                ulong seen = watched.SeenChange;
                // A token's stamp was checked natively.
                if (seen == ViewModelNative.NotWatching || (!ViewModelNative.IsWatchToken(seen) && stamp <= seen))
                {
                    continue;
                }
                watched.SeenChange = newest;
                if (watched is ViewModelInstancePrimitiveProperty property)
                {
                    result.Changed.Add(new PropertyCallbacksHub.CapturedChange { Property = property, Value = value });
                }
                else if (watched is ViewModelPropertyHandle handle)
                {
                    result.HandleChanged.Add(new HandleChange { Handle = handle, Value = value });
                }
            }
        }

        private void AfterLanded(Request request, Result result)
        {
            request.Delivered = true;
            if (request.Kind == Kind.Read)
            {
                IRead read = request.Read;
                request.Read = null;
                try
                {
                    read.Complete(result);
                }
                finally
                {
                    read.Release();
                }
                return;
            }
            if (result.Changed.Count == 0 && result.HandleChanged.Count == 0)
            {
                return;
            }
            m_capturedAny = true;
            using var delivering = s_deliverMarker.Auto();
            for (int i = 0; i < result.Changed.Count; i++)
            {
                PropertyCallbacksHub.CapturedChange change = result.Changed[i];
                try
                {
                    using (UserCallbacks.Scope())
                    {
                        change.Property.RaiseChangedEvent(change.Value);
                    }
                }
                catch (Exception e)
                {
                    DebugLogger.Instance.LogException(e);
                }
            }
            for (int i = 0; i < result.HandleChanged.Count; i++)
            {
                HandleChange change = result.HandleChanged[i];
                try
                {
                    using (UserCallbacks.Scope())
                    {
                        change.Handle.RaiseChanged(change.Value);
                    }
                }
                catch (Exception e)
                {
                    DebugLogger.Instance.LogException(e);
                }
            }
        }

        private void Reset(Request request, Result result)
        {
            if (!request.Delivered && !m_quiet)
            {
                if (request.Kind == Kind.Read)
                {
                    // Went with a stopped host, so it'd never finish otherwise.
                    request.Read?.Cancel();
                }
                else
                {
                    m_lostCapture = true;
                }
            }
            request.Read?.Release();
            if (ReferenceEquals(m_waitingTickCapture, request))
            {
                m_waitingTickCapture = null;
            }
            request.Properties.Clear();
            request.Handles.Clear();
            request.Watched.Clear();
            request.Kind = Kind.TickCapture;
            request.Read = null;
            request.Delivered = false;
            result.Changed.Clear();
            result.HandleChanged.Clear();
            result.Value = default;
            result.Error = null;
        }
    }
}
