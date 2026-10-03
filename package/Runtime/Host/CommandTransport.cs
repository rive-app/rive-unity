using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Rive.Producer;
using Rive.Utils;
using Unity.Profiling;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Rive.Host
{
    /// <summary>
    /// What's waiting on a request. Default means nothing is.
    /// </summary>
    internal struct RequestTicket
    {
        internal ulong Id;

        internal bool IsPending => Id != 0;
    }

    /// <summary>
    /// Sends work to the command host and hands replies back on the main
    /// thread. Owns request ids, the wait guards, held writes and main thread
    /// posting. Main thread only, apart from Post and PostToMainThread.
    /// </summary>
    internal static class CommandTransport
    {
        private static readonly ProfilerMarker s_waitMarker =
            new ProfilerMarker("Rive.Wait.Server");
        private static readonly ProfilerMarker s_drainMarker =
            new ProfilerMarker("Rive.Messages.Drain");

        /// Pooled. One goes back once it has landed, left the table and no
        /// Join is waiting on it, all decided under s_lock.
        private sealed class Pending
        {
            internal Action<HostMessageBatch, HostMessage> OnReply;
            internal volatile bool Landed;
            // Kept after landing until IsDone or Join spends the ticket.
            internal bool Kept;
            // Sent from the main thread, so the reply is delivered there too.
            internal bool MainOnly;
            internal bool InTable;
            // Joins spinning on it.
            internal int Waiters;
        }

        private const int PendingPoolLimit = 256;
        private static readonly Stack<Pending> s_pendingPool = new Stack<Pending>();

        // Under s_lock.
        private static Pending RentPending(Action<HostMessageBatch, HostMessage> onReply, bool keep)
        {
            Pending pending = s_pendingPool.Count > 0 ? s_pendingPool.Pop() : new Pending();
            pending.OnReply = onReply;
            pending.Landed = false;
            pending.Kept = keep;
            pending.MainOnly = IsMainThread;
            pending.InTable = true;
            pending.Waiters = 0;
            return pending;
        }

        // Under s_lock, from every place that lets one go.
        private static void ReturnIfDone(Pending pending)
        {
            if (!pending.Landed || pending.InTable || pending.Waiters > 0 ||
                s_pendingPool.Count >= PendingPoolLimit)
            {
                return;
            }
            pending.OnReply = null;
            // Reads as in the table again, so a late second call can't return
            // it twice.
            pending.InTable = true;
            s_pendingPool.Push(pending);
        }

        private static void RemovePending(ulong id, Pending pending)
        {
            if (s_pending.Remove(id))
            {
                pending.InTable = false;
            }
        }

        // A main thread reply that another thread drained, held for the main
        // thread with its own copy of the bytes.
        private struct HeldReply
        {
            internal Pending Pending;
            internal ulong RequestId;
            internal HostMessageBatch Batch;
            internal HostMessage Message;
        }

        private static Thread s_mainThread;

        // Request ids, the pending table and held writes. Held while a send
        // goes out, so a thread's writes land ahead of what it sends next.
        // Never wait while holding it.
        private static readonly object s_lock = new object();
        private static ulong s_nextRequestId;
        private static readonly Dictionary<ulong, Pending> s_pending =
            new Dictionary<ulong, Pending>();
        private static List<Action> s_pendingWrites = new List<Action>();
        private static long s_requestsForTests;
        private static int s_pendingPeakForTests;

        // One thread drains at a time.
        private static readonly object s_drainLock = new object();
        private static readonly HostMessageBatch s_batch = new HostMessageBatch();
        private static readonly Queue<HeldReply> s_heldForMain = new Queue<HeldReply>();
        [ThreadStatic]
        private static bool t_draining;

        // Work handed over by finalizers, waiting for the main thread.
        private static readonly ConcurrentQueue<Action> s_posted =
            new ConcurrentQueue<Action>();

        // Async results waiting for the main thread. See PostToMainThread.
        private static readonly ConcurrentQueue<Action> s_mainThreadWork =
            new ConcurrentQueue<Action>();

        // Futures handed out that haven't finished. Cancelled on teardown, so
        // an await can't hang on a result that isn't coming.
        private static long s_nextAsyncId;
        private static readonly ConcurrentDictionary<long, Action> s_outstandingAsync =
            new ConcurrentDictionary<long, Action>();

        // Frame path code that must never wait on the server. Checked by Join
        // and Barrier in the editor and dev builds.
        [ThreadStatic]
        private static int t_noWaitDepth;
        [ThreadStatic]
        private static string t_noWaitReason;

        /// Undoes a NoWait or AllowWait. default does nothing.
        internal readonly struct WaitScope : IDisposable
        {
            private readonly int m_previousDepth;
            private readonly string m_previousReason;
            private readonly bool m_active;

            internal WaitScope(int previousDepth, string previousReason)
            {
                m_previousDepth = previousDepth;
                m_previousReason = previousReason;
                m_active = true;
            }

            public void Dispose()
            {
                if (!m_active)
                {
                    return;
                }
                t_noWaitDepth = m_previousDepth;
                t_noWaitReason = m_previousReason;
            }
        }

        /// Waiting on the server inside this logs an error with the reason.
        internal static WaitScope NoWait(string reason)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var scope = new WaitScope(t_noWaitDepth, t_noWaitReason);
            t_noWaitDepth++;
            t_noWaitReason = reason;
            return scope;
#else
            return default;
#endif
        }

        /// NoWait only when the condition holds, so callers can pick per panel.
        internal static WaitScope NoWaitIf(bool condition, string reason)
        {
            return condition ? NoWait(reason) : default;
        }

        /// For the real join points inside a NoWait scope.
        internal static WaitScope AllowWait(string reason)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var scope = new WaitScope(t_noWaitDepth, t_noWaitReason);
            t_noWaitDepth = 0;
            t_noWaitReason = reason;
            return scope;
#else
            return default;
#endif
        }

        /// Around user callbacks and continuations. The guard is for our frame
        /// path, and whether user code waits is up to the user.
        internal static WaitScope UserCode()
        {
            return AllowWait(null);
        }

        /// Tests only. True inside a NoWait scope.
        internal static bool InNoWaitScope => t_noWaitDepth > 0;

        /// Tests only. While set, every main thread wait that actually blocks
        /// adds its stack here.
        internal static List<string> MainThreadWaitsForTests;

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void NoteBlockingWait()
        {
            List<string> waits = MainThreadWaitsForTests;
            if (waits != null && IsMainThread)
            {
                waits.Add(Environment.StackTrace);
            }
        }

        /// The no-wait guard, for joins that live outside this class.
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        internal static void CheckWaitAllowedFor(string what)
        {
            if (t_noWaitDepth == 0)
            {
                return;
            }
            DebugLogger.Instance.LogError(
                $"CommandTransport.{what} waited on the server inside a no-wait "
                    + $"scope ({t_noWaitReason}).\n{Environment.StackTrace}");
        }

        /// True on the thread Unity runs the player loop on.
        internal static bool IsMainThread => s_mainThread == Thread.CurrentThread;

        /// False only on WebGL without threads, where the main thread runs the
        /// server when it drains.
        internal static bool IsThreaded => CommandHost.IsThreaded;

        // Both hooks run on the main thread, and the editor one covers edit
        // mode, where nothing runtime has fired yet.
#if UNITY_EDITOR
        [InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RecordMainThread()
        {
            s_mainThread = Thread.CurrentThread;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // With domain reload off, the previous session's statics survive,
            // and so does the host with everything it holds.
            while (s_mainThreadWork.TryDequeue(out _))
            {
            }
            CancelOutstandingAsync();
            HookLifecycle();
        }

        // A background thread can't start the host, so it's up before any
        // user code runs. The editor starts it for edit mode too.
#if UNITY_EDITOR
        [InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void StartOnLoad()
        {
            if (NativeUsageGuard.IsNativeAvailable)
            {
                EnsureStarted();
                // Its late step flushes held writes, panels or not.
                RiveFrameLoop.Ensure();
            }
        }

        private static void HookLifecycle()
        {
#if UNITY_EDITOR
            AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
            AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
            // Application.quitting also fires on leaving play mode in the
            // editor, and the host outlives that.
            EditorApplication.quitting -= Shutdown;
            EditorApplication.quitting += Shutdown;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
#else
            Application.quitting -= Shutdown;
            Application.quitting += Shutdown;
#endif
        }

#if UNITY_EDITOR
        // The host and its objects stay for edit mode, but nothing awaited in
        // play mode should resume there.
        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode)
            {
                DrainMainThread();
                CancelOutstandingAsync();
            }
        }
#endif

        /// Starts the host if it isn't running. Main thread only. From any
        /// other thread it does nothing, and sends fail until the main thread
        /// starts it again.
        internal static void EnsureStarted()
        {
            if (s_mainThread == null)
            {
                RecordMainThread();
            }
            if (CommandHost.IsRunning || !IsMainThread)
            {
                return;
            }
            CommandHost.EnsureStarted();
            HookLifecycle();
        }

        /// Stops the host. Held writes go out first, and anything still
        /// waiting on a reply is let go. Main thread only.
        internal static void Shutdown()
        {
            if (!CommandHost.IsRunning)
            {
                return;
            }
            FlushWrites();
            DrainPosted();
            s_serving = false;
            // A drain on another thread copies out of the host's buffer, so
            // the host can't go while one is running. Drain lock first, same
            // order as Drain.
            lock (s_drainLock)
            {
                lock (s_lock)
                {
                    CommandHost.Stop();
                    foreach (Pending pending in s_pending.Values)
                    {
                        pending.Landed = true;
                    }
                    s_pending.Clear();
                }
                s_heldForMain.Clear();
            }
            DrainMainThread();
            CancelOutstandingAsync();
            Stopped?.Invoke();
        }

        /// Main thread, after a stop. Everything native went with the host, so
        /// caches of native objects let go of them here.
        internal static event Action Stopped;

        /// Sends one request from any thread. send queues the native command
        /// with the request id it's given. onReply runs in the drain that
        /// delivers the reply, on the main thread if this was sent from it.
        /// With keep, the ticket stays pending until IsDone or Join spends it.
        internal static RequestTicket Send(
            Action<ulong> send,
            Action<HostMessageBatch, HostMessage> onReply = null,
            bool keep = true)
        {
            EnsureStarted();
            lock (s_lock)
            {
                if (!CommandHost.IsRunning)
                {
                    throw new InvalidOperationException(
                        "Rive's command host isn't running. It starts on the main thread.");
                }
                FlushWritesLocked();
                ulong id = ++s_nextRequestId;
                s_requestsForTests++;
                Pending pending = RentPending(onReply, keep);
                s_pending[id] = pending;
                if (s_pending.Count > s_pendingPeakForTests)
                {
                    s_pendingPeakForTests = s_pending.Count;
                }
                try
                {
                    send(id);
                }
                catch
                {
                    RemovePending(id, pending);
                    throw;
                }
                return new RequestTicket { Id = id };
            }
        }

        /// Send for a command with no reply. Held writes still go first.
        internal static void SendNoReply(Action send)
        {
            EnsureStarted();
            lock (s_lock)
            {
                FlushWritesLocked();
                send();
            }
        }

        /// A Future that finishes on the main thread once the reply lands,
        /// with what parse makes of it. A throw from parse fails it, and a
        /// teardown before the reply cancels it.
        internal static Future<T> SendFuture<T>(
            Action<ulong> send,
            Func<HostMessageBatch, HostMessage, T> parse)
        {
            // Its early step is what delivers the reply.
            RiveFrameLoop.Ensure();
            var state = new FutureState<T>();
            long asyncId = TrackAsync(state);
            state.OnProducer = true;
            Send(send, (batch, message) =>
            {
                T result = default;
                Exception failure = null;
                try
                {
                    result = parse(batch, message);
                }
                catch (Exception e)
                {
                    failure = e;
                }
                state.WorkFinished = true;
                PostToMainThread(() =>
                {
                    ForgetAsync(asyncId);
                    if (failure != null)
                    {
                        state.Fail(failure);
                    }
                    else
                    {
                        state.Succeed(result);
                    }
                });
            }, keep: false);
            return new Future<T>(state);
        }

        /// For setters, from any thread. Held back and sent in order ahead of
        /// the next thing that's sent, or at the end of the frame.
        internal static void Write(Action send)
        {
            if (send == null)
            {
                return;
            }
            lock (s_lock)
            {
                s_pendingWrites.Add(send);
            }
        }

        /// Sends held writes now rather than with the next send.
        internal static void FlushWrites()
        {
            lock (s_lock)
            {
                FlushWritesLocked();
            }
        }

        private static void FlushWritesLocked()
        {
            if (s_pendingWrites.Count == 0)
            {
                return;
            }
            List<Action> writes = s_pendingWrites;
            s_pendingWrites = new List<Action>();
            // One bad write shouldn't take the rest with it.
            for (int i = 0; i < writes.Count; ++i)
            {
                try
                {
                    writes[i]();
                }
                catch (Exception e)
                {
                    DebugLogger.Instance.LogException(e);
                }
            }
        }

        internal static int PendingWriteCount
        {
            get
            {
                lock (s_lock)
                {
                    return s_pendingWrites.Count;
                }
            }
        }

        /// Delivers what the server has posted. From the frame loop's Early
        /// step and from inside waits, on any thread. A reply to a main thread
        /// request that another thread drains waits for the main thread.
        internal static void Drain()
        {
            if (t_draining || !CommandHost.IsRunning)
            {
                return;
            }
            lock (s_drainLock)
            {
                t_draining = true;
                try
                {
                    BeforeDrainForTests?.Invoke();
                    using (s_drainMarker.Auto())
                    {
                        bool main = IsMainThread;
                        if (main)
                        {
                            while (s_heldForMain.Count > 0)
                            {
                                HeldReply held = s_heldForMain.Dequeue();
                                Deliver(held.Pending, held.RequestId, held.Batch, held.Message);
                            }
                        }
                        CommandHost.Drain(s_batch);
                        List<HostMessage> messages = s_batch.Messages;
                        for (int i = 0; i < messages.Count; i++)
                        {
                            HostMessage message = messages[i];
                            Pending pending;
                            lock (s_lock)
                            {
                                if (message.RequestId == 0 ||
                                    !s_pending.TryGetValue(message.RequestId, out pending))
                                {
                                    continue;
                                }
                            }
                            if (pending.MainOnly && !main)
                            {
                                s_heldForMain.Enqueue(Hold(pending, message));
                                continue;
                            }
                            Deliver(pending, message.RequestId, s_batch, message);
                        }
                    }
                }
                finally
                {
                    t_draining = false;
                }
            }
        }

        private static HeldReply Hold(Pending pending, HostMessage message)
        {
            var copy = new HostMessageBatch
            {
                Bytes = new byte[HostMessageBatch.RecordSize + message.PayloadSize],
            };
            Buffer.BlockCopy(s_batch.Bytes, message.PayloadOffset, copy.Bytes,
                             HostMessageBatch.RecordSize, message.PayloadSize);
            message.PayloadOffset = HostMessageBatch.RecordSize;
            return new HeldReply
            {
                Pending = pending,
                RequestId = message.RequestId,
                Batch = copy,
                Message = message,
            };
        }

        private static void Deliver(
            Pending pending,
            ulong requestId,
            HostMessageBatch batch,
            HostMessage message)
        {
            if (!pending.Kept)
            {
                lock (s_lock)
                {
                    RemovePending(requestId, pending);
                }
            }
            try
            {
                pending.OnReply?.Invoke(batch, message);
            }
            catch (Exception e)
            {
                DebugLogger.Instance.LogException(e);
            }
            finally
            {
                lock (s_lock)
                {
                    pending.Landed = true;
                    ReturnIfDone(pending);
                }
            }
        }

        /// Landed yet? Never blocks. Spends the ticket once it says true.
        internal static bool IsDone(ref RequestTicket ticket)
        {
            if (!ticket.IsPending)
            {
                return true;
            }
            lock (s_lock)
            {
                if (s_pending.TryGetValue(ticket.Id, out Pending pending))
                {
                    if (!pending.Landed)
                    {
                        return false;
                    }
                    RemovePending(ticket.Id, pending);
                    ReturnIfDone(pending);
                }
            }
            ticket = default;
            return true;
        }

        /// Waits for the reply, draining while it waits. False when the host
        /// stopped before it came.
        internal static bool Join(ref RequestTicket ticket)
        {
            CheckWaitAllowedFor("Join");
            return JoinUnguarded(ref ticket);
        }

        private static bool JoinUnguarded(ref RequestTicket ticket)
        {
            if (!ticket.IsPending)
            {
                return true;
            }

            ulong id = ticket.Id;
            ticket = default;
            Pending pending;
            lock (s_lock)
            {
                if (!s_pending.TryGetValue(id, out pending))
                {
                    return true;
                }
                // Keeps it out of the pool while this spins on it.
                pending.Waiters++;
            }
            if (!pending.Landed && t_draining)
            {
                // The drain that would deliver it is the one running this.
                DebugLogger.Instance.LogError(
                    "CommandTransport.Join was called while delivering a reply, "
                        + "so the reply it waits for can't land.\n" + Environment.StackTrace);
                lock (s_lock)
                {
                    pending.Waiters--;
                    RemovePending(id, pending);
                    ReturnIfDone(pending);
                }
                return false;
            }
            if (!pending.Landed)
            {
                NoteBlockingWait();
                using (s_waitMarker.Auto())
                {
                    long notServingSince = 0;
                    while (!pending.Landed && CommandHost.IsRunning)
                    {
                        Drain();
                        if (!pending.Landed && !Spin(ref notServingSince))
                        {
                            break;
                        }
                    }
                }
            }
            bool landed = pending.Landed;
            lock (s_lock)
            {
                pending.Waiters--;
                RemovePending(id, pending);
                ReturnIfDone(pending);
            }
            return landed;
        }

        private const long ServerStartSeconds = 5;
        private static bool s_serving;

        // One turn of a wait. False once the server thread has had long enough
        // to start and hasn't, since the wait would spin forever.
        private static bool Spin(ref long notServingSince)
        {
            HostNative.riveHostYield();
            if (s_serving || (s_serving = HostNative.riveHostServing()))
            {
                return true;
            }
            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            if (notServingSince == 0)
            {
                notServingSince = now;
            }
            if (now - notServingSince < ServerStartSeconds * System.Diagnostics.Stopwatch.Frequency)
            {
                return true;
            }
            DebugLogger.Instance.LogError(
                "Rive's server thread never started, so this wait can't finish. On WebGL with Native C/C++ "
                    + "Multithreading, it needs a free worker in the pthread pool.");
            return false;
        }

        /// Drains until done says so, for waits several callers can share.
        /// False when the host stopped first.
        internal static bool WaitFor(Func<bool> done, string what)
        {
            if (done())
            {
                return true;
            }
            CheckWaitAllowedFor(what);
            if (t_draining)
            {
                DebugLogger.Instance.LogError(
                    $"CommandTransport.{what} waited while delivering a reply, so "
                        + "what it waits for can't land.\n" + Environment.StackTrace);
                return false;
            }
            NoteBlockingWait();
            using (s_waitMarker.Auto())
            {
                long notServingSince = 0;
                while (!done() && CommandHost.IsRunning)
                {
                    Drain();
                    if (!done() && !Spin(ref notServingSince))
                    {
                        break;
                    }
                }
            }
            return done();
        }

        /// Returns once everything sent before it has run and its replies
        /// have been delivered. Skips the guard, so the caller checks it.
        internal static void Barrier()
        {
            RequestTicket ticket = Send(id => HostNative.riveHostEcho(id, Array.Empty<byte>(), 0));
            JoinUnguarded(ref ticket);
        }

        /// For finalizers, which run on the GC thread and can't touch native
        /// state. Parked until the main thread drains.
        internal static void Post(Action work)
        {
            if (work != null)
            {
                s_posted.Enqueue(work);
            }
        }

        private static void DrainPosted()
        {
            while (s_posted.TryDequeue(out Action work))
            {
                try
                {
                    work();
                }
                catch (Exception e)
                {
                    DebugLogger.Instance.LogException(e);
                }
            }
        }

        /// Results on their way back to the main thread, so a continuation
        /// always runs where Unity's API is usable.
        internal static void PostToMainThread(Action work)
        {
            if (work != null)
            {
                s_mainThreadWork.Enqueue(work);
            }
        }

        /// Main thread only, once a frame. Also runs parked finalizer work.
        internal static void DrainMainThread()
        {
            DrainPosted();
            // Anything a continuation posts waits for the next drain, so a
            // chain of them can't hold the frame.
            int budget = s_mainThreadWork.Count;
            while (budget-- > 0 && s_mainThreadWork.TryDequeue(out Action work))
            {
                try
                {
                    work();
                }
                catch (Exception e)
                {
                    DebugLogger.Instance.LogException(e);
                }
            }
        }

        internal static int MainThreadWorkCount => s_mainThreadWork.Count;

        /// Runs it now on the main thread, or at the next drain from anywhere else.
        internal static void RunOnMainThread(Action work)
        {
            if (work == null)
            {
                return;
            }
            if (IsMainThread)
            {
                work();
                return;
            }
            PostToMainThread(work);
        }

        /// Notes the operation so a teardown can cancel it, and hands back the
        /// id to forget it by.
        internal static long TrackAsync<T>(FutureState<T> state)
        {
            long id = Interlocked.Increment(ref s_nextAsyncId);
            s_outstandingAsync[id] = state.Cancel;
            return id;
        }

        internal static void ForgetAsync(long id)
        {
            s_outstandingAsync.TryRemove(id, out _);
        }

        /// Cancels every Future still waiting, so an await doesn't hang past
        /// the session that started it. Unity has no destroy-scoped token
        /// before 2022.2 and the package targets 2021.3.
        private static void CancelOutstandingAsync()
        {
            foreach (long id in s_outstandingAsync.Keys)
            {
                if (!s_outstandingAsync.TryRemove(id, out Action cancel))
                {
                    continue;
                }
                try
                {
                    cancel();
                }
                catch (Exception e)
                {
                    DebugLogger.Instance.LogException(e);
                }
            }
        }

        /// Tests only. Play mode exit, quit and domain reload are the real
        /// callers, and none of them can be reached from inside a test.
        internal static void CancelOutstandingAsyncForTests() => CancelOutstandingAsync();

        /// Tests only. Requests still waiting on a reply.
        internal static int PendingRequestsForTests
        {
            get
            {
                lock (s_lock)
                {
                    return s_pending.Count;
                }
            }
        }

        /// Tests only. Request ids handed out, so a test can count sends.
        internal static long RequestsForTests => s_requestsForTests;

        /// Tests only. The most requests waiting at once since the last reset.
        internal static int PendingPeakForTests => s_pendingPeakForTests;

        internal static void ResetPendingPeakForTests()
        {
            lock (s_lock)
            {
                s_pendingPeakForTests = s_pending.Count;
            }
        }

        /// Tests only. Runs at the start of every drain, where a test that
        /// holds the server can let it go.
        internal static Action BeforeDrainForTests;
    }
}
