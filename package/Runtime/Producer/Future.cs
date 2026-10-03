using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Rive.Producer;
using Rive.Utils;
using Rive.Host;

namespace Rive
{
    /// <summary>
    /// Where a <see cref="Future{T}"/> or <see cref="Future"/> is up to.
    /// </summary>
    public enum FutureStatus
    {
        /// <summary>Still running.</summary>
        Pending = 0,
        /// <summary>Finished with a result.</summary>
        Succeeded = 1,
        /// <summary>Finished with an exception, see Exception.</summary>
        Failed = 2,
        /// <summary>Ended without a result, because the Rive producer went away.</summary>
        Canceled = 3,
    }

    /// <summary>
    /// Where an awaiter asked to continue. Awaited on the main thread, it
    /// continues inline at delivery. Anywhere else goes to that thread's
    /// context, or the thread pool, so background code never lands on the
    /// main thread.
    /// </summary>
    internal readonly struct FutureContinuation
    {
        private static readonly SendOrPostCallback s_post = state => ((Action)state)();
        private static readonly WaitCallback s_pool = state => ((Action)state)();

        [ThreadStatic]
        private static bool t_resumingMisused;

        private readonly Action m_action;
        private readonly SynchronizationContext m_context;
        private readonly bool m_onMainThread;

        private FutureContinuation(Action action, SynchronizationContext context, bool onMainThread)
        {
            m_action = action;
            m_context = context;
            m_onMainThread = onMainThread;
        }

        internal bool IsSet => m_action != null;

        internal static FutureContinuation Capture(Action action)
        {
            bool onMainThread = CommandTransport.IsMainThread;
            return new FutureContinuation(
                action, onMainThread ? null : SynchronizationContext.Current, onMainThread);
        }

        internal void Resume()
        {
            Resume(m_action);
        }

        /// For a second await while the first is still waiting. GetResult in
        /// the method that did it throws, and the first awaiter is untouched.
        internal void ResumeMisused()
        {
            Action action = m_action;
            Resume(() =>
            {
                t_resumingMisused = true;
                try
                {
                    action();
                }
                finally
                {
                    t_resumingMisused = false;
                }
            });
        }

        /// Called by GetResult. True once, inside a misused resume.
        internal static bool TakeMisused()
        {
            if (!t_resumingMisused)
            {
                return false;
            }
            t_resumingMisused = false;
            return true;
        }

        private void Resume(Action action)
        {
            if (m_onMainThread)
            {
                if (CommandTransport.IsMainThread)
                {
                    FutureDelivery.RunUserCode(action);
                }
                else
                {
                    CommandTransport.PostToMainThread(() => FutureDelivery.RunUserCode(action));
                }
            }
            else if (m_context != null)
            {
                m_context.Post(s_post, action);
            }
            else
            {
                ThreadPool.UnsafeQueueUserWorkItem(s_pool, action);
            }
        }
    }

    internal static class FutureDelivery
    {
        /// Outside the no-wait guard, in its own try/catch.
        internal static void RunUserCode(Action action)
        {
            using (UserCallbacks.Scope())
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    DebugLogger.Instance.LogException(e);
                }
            }
        }
    }

    /// <summary>
    /// Shared by the generic and non-generic future. Completes on the main
    /// thread, runs each callback in its own try/catch outside the no-wait
    /// guard, and never holds its lock while calling out.
    /// </summary>
    internal sealed class FutureState<T>
    {
        private struct Callback
        {
            internal Delegate Key;
            internal Action Invoke;
        }

        private readonly object m_gate = new object();
        private FutureStatus m_status;
        private T m_result;
        private Exception m_exception;
        private FutureContinuation m_continuation;
        private List<Callback> m_callbacks;
        private TaskCompletionSource<T> m_task;

        // Bumped when a pooled state is reused, so the Future it was used
        // through throws from then on.
        internal int Version;

        // From Rent, and goes back to the pool after its Future's one use.
        private bool m_pooled;

        private const int PoolLimit = 64;
        private static readonly Stack<FutureState<T>> s_pool = new Stack<FutureState<T>>();

        /// A state that goes back to a pool once its Future has been awaited or
        /// waited on. Whatever finishes it must use the calls that take the
        /// version it was rented at, since it can be reused after that.
        internal static FutureState<T> Rent()
        {
            FutureState<T> state = null;
            lock (s_pool)
            {
                if (s_pool.Count > 0)
                {
                    state = s_pool.Pop();
                }
            }
            state ??= new FutureState<T>();
            state.m_pooled = true;
            return state;
        }

        /// After the one use. Does nothing for a state that wasn't rented.
        internal void RecycleAfterUse()
        {
            lock (m_gate)
            {
                if (!m_pooled || m_status == FutureStatus.Pending)
                {
                    return;
                }
                m_pooled = false;
                Version++;
                m_status = FutureStatus.Pending;
                m_result = default;
                m_exception = null;
                m_continuation = default;
                m_callbacks = null;
                m_task = null;
                WorkFinished = false;
                OnProducer = false;
                OnWorker = false;
                WaitDriver = null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                m_consumed = false;
#endif
            }
            lock (s_pool)
            {
                if (s_pool.Count < PoolLimit)
                {
                    s_pool.Push(this);
                }
            }
        }

        /// Clears the wait hook, unless the state has moved on to another use.
        internal void ClearWaitDriver(int version)
        {
            lock (m_gate)
            {
                if (Version == version)
                {
                    WaitDriver = null;
                }
            }
        }

        // Set by whatever produced the result, before it heads for the main
        // thread. WaitForCompletion uses it to tell "still running" from
        // "finished, waiting for the drain".
        internal volatile bool WorkFinished;

        // The work went through the producer queue, so a join tells us it ran.
        internal bool OnProducer;

        // The work runs on a worker thread, so it finishes without the main thread.
        internal bool OnWorker;

        // For a Future built on another one, waits on that instead.
        internal Action WaitDriver;

        /// Finishes this the way the source failed or was canceled. False if
        /// the source succeeded, and this is left alone.
        internal bool TryForwardFailure<TIn>(Future<TIn> source)
        {
            switch (source.Status)
            {
                case FutureStatus.Failed:
                    Fail(source.Exception);
                    return true;
                case FutureStatus.Canceled:
                    Cancel();
                    return true;
                default:
                    return false;
            }
        }

        /// A state finished from another Future. Waiting on it waits on the
        /// source, without using up the source's one await.
        internal static FutureState<T> Then<TIn>(Future<TIn> source, Action<Future<TIn>, FutureState<T>> finish)
        {
            var state = new FutureState<T>();
            state.WaitDriver = source.WaitInternal;
            source.Completed += landed => finish(landed, state);
            return state;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private bool m_consumed;
#endif

        internal FutureStatus Status
        {
            get { lock (m_gate) { return m_status; } }
        }

        internal bool IsDone => Status != FutureStatus.Pending;

        internal Exception Exception
        {
            get { lock (m_gate) { return m_exception; } }
        }

        internal void Succeed(T result)
        {
            Finish(FutureStatus.Succeeded, result, null, -1);
        }

        internal void Fail(Exception exception)
        {
            Finish(FutureStatus.Failed, default, exception, -1);
        }

        internal void Cancel()
        {
            Finish(FutureStatus.Canceled, default, null, -1);
        }

        /// For a rented state. Does nothing once it has moved on to another use.
        internal void Succeed(T result, int version)
        {
            Finish(FutureStatus.Succeeded, result, null, version);
        }

        internal void Fail(Exception exception, int version)
        {
            Finish(FutureStatus.Failed, default, exception, version);
        }

        internal void Cancel(int version)
        {
            Finish(FutureStatus.Canceled, default, null, version);
        }

        // version is -1 for a state that's never reused.
        private void Finish(FutureStatus status, T result, Exception exception, int version)
        {
            FutureContinuation continuation;
            List<Callback> callbacks;
            TaskCompletionSource<T> task;
            lock (m_gate)
            {
                if (m_status != FutureStatus.Pending || (version >= 0 && version != Version))
                {
                    return;
                }
                m_status = status;
                m_result = result;
                m_exception = exception;
                continuation = m_continuation;
                m_continuation = default;
                callbacks = m_callbacks;
                m_callbacks = null;
                task = m_task;
            }

            if (task != null)
            {
                SetTask(task, status, result, exception);
            }
            if (callbacks != null)
            {
                for (int i = 0; i < callbacks.Count; i++)
                {
                    FutureDelivery.RunUserCode(callbacks[i].Invoke);
                }
            }
            if (continuation.IsSet)
            {
                continuation.Resume();
            }
        }

        /// The await continuation. One slot, like the awaiters it copies. A
        /// second await while it's taken would lose the first, so the second
        /// is resumed straight away to fail instead, in every build.
        internal void SetContinuation(FutureContinuation continuation)
        {
            bool misused = false;
            lock (m_gate)
            {
                if (m_status == FutureStatus.Pending)
                {
                    if (!m_continuation.IsSet)
                    {
                        m_continuation = continuation;
                        return;
                    }
                    misused = true;
                }
            }
            if (misused)
            {
                continuation.ResumeMisused();
                return;
            }
            // Finished between IsCompleted and here.
            continuation.Resume();
        }

        internal void AddCallback(Delegate key, Action invoke)
        {
            lock (m_gate)
            {
                if (m_status == FutureStatus.Pending)
                {
                    if (m_callbacks == null)
                    {
                        m_callbacks = new List<Callback>();
                    }
                    m_callbacks.Add(new Callback { Key = key, Invoke = invoke });
                    return;
                }
            }
            // Already done, so it runs straight away.
            FutureDelivery.RunUserCode(invoke);
        }

        internal void RemoveCallback(Delegate key)
        {
            lock (m_gate)
            {
                if (m_callbacks == null)
                {
                    return;
                }
                for (int i = m_callbacks.Count - 1; i >= 0; i--)
                {
                    if (Equals(m_callbacks[i].Key, key))
                    {
                        m_callbacks.RemoveAt(i);
                        return;
                    }
                }
            }
        }

        /// Reading Result doesn't count as the one use.
        internal T Peek()
        {
            lock (m_gate)
            {
                switch (m_status)
                {
                    case FutureStatus.Pending:
                        throw new InvalidOperationException(
                            "The Future hasn't finished. Check IsDone, await it, or call WaitForCompletion.");
                    case FutureStatus.Failed:
                        ExceptionDispatchInfo.Capture(m_exception).Throw();
                        return default;
                    case FutureStatus.Canceled:
                        throw new OperationCanceledException(
                            "The Future was canceled because the Rive producer went away.");
                    default:
                        return m_result;
                }
            }
        }

        /// await, AsTask and WaitForCompletion each count as the one use.
        internal void MarkConsumed()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            lock (m_gate)
            {
                if (m_consumed)
                {
                    throw new InvalidOperationException(
                        "A Future can only be awaited once. Call AsTask() to await it "
                            + "more than once, store it, or use it with WhenAll.");
                }
                m_consumed = true;
            }
#endif
        }

        // Continuations run inline where it's completed, which is the main
        // thread, so await AsTask() there keeps its place with our callbacks.
        internal Task<T> AsTask()
        {
            MarkConsumed();
            TaskCompletionSource<T> task;
            FutureStatus status;
            T result;
            Exception exception;
            lock (m_gate)
            {
                if (m_task != null)
                {
                    return m_task.Task;
                }
                m_task = new TaskCompletionSource<T>();
                task = m_task;
                status = m_status;
                result = m_result;
                exception = m_exception;
            }
            if (status != FutureStatus.Pending)
            {
                SetTask(task, status, result, exception);
            }
            return task.Task;
        }

        private static void SetTask(
            TaskCompletionSource<T> task, FutureStatus status, T result, Exception exception)
        {
            using (UserCallbacks.Scope())
            {
                switch (status)
                {
                    case FutureStatus.Succeeded:
                        task.TrySetResult(result);
                        break;
                    case FutureStatus.Failed:
                        task.TrySetException(exception);
                        break;
                    case FutureStatus.Canceled:
                        task.TrySetCanceled();
                        break;
                }
            }
        }

        /// Blocks until it's done. Main thread drains the result itself, which
        /// runs everything else waiting to be delivered too, other futures'
        /// callbacks and continuations included. Another thread waits for the
        /// main thread to finish it.
        internal void Wait()
        {
            CommandTransport.CheckWaitAllowedFor("WaitForCompletion");
            bool main = CommandTransport.IsMainThread;
            if (main && WaitDriver != null && !IsDone)
            {
                // Finishes it now rather than waiting for the frame, like
                // Addressables' WaitForCompletion. Drivers deliver main
                // thread work, so another thread waits for the frame.
                WaitDriver();
            }
            if (main && !OnProducer && !OnWorker && !IsDone)
            {
                throw new InvalidOperationException(
                    "This Future finishes during Rive's update pass and can't be finished early. "
                        + "Await it or yield it instead.");
            }

            bool joined = false;
            while (!IsDone)
            {
                if (!main)
                {
                    // The main thread finishes it.
                    Thread.Sleep(1);
                    continue;
                }
                if (WorkFinished)
                {
                    CommandTransport.DrainMainThread();
                    continue;
                }
                if (OnProducer && !joined)
                {
                    // Everything queued before this has run once it returns.
                    CommandTransport.Barrier();
                    joined = true;
                    continue;
                }
                if (OnProducer)
                {
                    // Joined and it still never ran, so the producer dropped it.
                    Cancel();
                    break;
                }
                Thread.Sleep(1);
            }
        }
    }

    /// <summary>
    /// A Rive call that finishes later, with a result. Await it, yield it from a coroutine, subscribe to <see cref="Completed"/>, or call <see cref="AsTask"/>.
    /// </summary>
    /// <remarks>
    /// Await a Future once. <see cref="IsDone"/>, <see cref="Status"/>, <see cref="Exception"/>, <see cref="Result"/> and <see cref="Completed"/> are fine any time before that. To await more than once, keep it, or use it with Task.WhenAll, call <see cref="AsTask"/> and use the Task. Awaiting it again while an earlier await is still waiting throws in the second method.
    ///
    /// A Future from a value read (GetValueAsync) is recycled once it's been awaited or waited on, so awaiting a read allocates nothing. Using it after that throws InvalidOperationException.
    ///
    /// It always finishes on the main thread, at the same point in the frame. With no producer thread (edit mode, WebGL) it's already done when it's returned.
    ///
    /// Awaited on the main thread, the code after the await runs on the main thread as soon as it finishes. Awaited anywhere else, it runs on that thread's SynchronizationContext, or the thread pool if there isn't one, never the main thread.
    ///
    /// For values read every frame, don't await. Listen for changes instead, which carry the value: a property handle's Subscribe, or a plain property's OnValueChanged.
    /// </remarks>
    public readonly struct Future<T> : IEnumerator
    {
        private readonly FutureState<T> m_state;
        private readonly int m_version;
        // The result when there's no state: a Future that finished when it was made.
        private readonly T m_value;

        internal Future(FutureState<T> state)
        {
            m_state = state;
            m_version = state.Version;
            m_value = default;
        }

        private Future(T value)
        {
            m_state = null;
            m_version = 0;
            m_value = value;
        }

        /// WaitForCompletion without using up the one await. For Futures
        /// built on this one.
        internal void WaitInternal()
        {
            // A recycled source was already done, so there's nothing to wait for.
            if (m_state != null && m_state.Version == m_version)
            {
                m_state.Wait();
            }
        }

        /// Already finished, for values that are cached. Allocates nothing.
        internal static Future<T> FromResult(T value)
        {
            return new Future<T>(value);
        }

        private FutureState<T> State
        {
            get
            {
                if (m_state != null && m_state.Version != m_version)
                {
                    throw new InvalidOperationException("This Future has already been used and recycled.");
                }
                return m_state;
            }
        }

        /// <summary>True once it has finished, whichever way.</summary>
        public bool IsDone => State?.IsDone ?? true;

        /// <summary>Where it's up to.</summary>
        public FutureStatus Status => State?.Status ?? FutureStatus.Succeeded;

        /// <summary>What it failed with, or null.</summary>
        public Exception Exception => State?.Exception;

        /// <summary>
        /// The result. Throws if it hasn't finished, rethrows what it failed with, and throws OperationCanceledException if it was canceled.
        /// </summary>
        public T Result => State == null ? m_value : State.Peek();

        /// <summary>
        /// Runs once it finishes, on the main thread. Runs straight away if it already has.
        /// </summary>
        public event Action<Future<T>> Completed
        {
            add
            {
                if (value == null)
                {
                    return;
                }
                if (State == null)
                {
                    value(this);
                    return;
                }
                Future<T> self = this;
                AddCompleted(value, () => value(self));
            }
            remove
            {
                RemoveCompleted(value);
            }
        }

        internal void AddCompleted(Delegate key, Action invoke)
        {
            if (State == null)
            {
                invoke();
                return;
            }
            State.AddCallback(key, invoke);
        }

        internal void RemoveCompleted(Delegate key)
        {
            if (key != null)
            {
                State?.RemoveCallback(key);
            }
        }

        /// <summary>
        /// A Task for this Future, for awaiting more than once, storing, or WhenAll. Counts as the one use.
        /// </summary>
        /// <remarks>
        /// The Task completes on the main thread, so await AsTask() there continues inline. ContinueWith without a scheduler runs on the thread pool.
        /// </remarks>
        public Task<T> AsTask()
        {
            return State == null ? Task.FromResult(m_value) : State.AsTask();
        }

        /// <summary>
        /// Blocks until it finishes and returns the result. Counts as the one use. Can't be called on the Rive producer thread.
        /// </summary>
        /// <remarks>
        /// On the main thread it delivers everything else that's finished while it waits, so other Futures' callbacks and continuations can run inside this call. On another thread it waits for the main thread to finish it, so don't call it from a thread the main thread is waiting on.
        /// </remarks>
        public T WaitForCompletion()
        {
            if (State == null)
            {
                return m_value;
            }
            FutureState<T> state = State;
            state.Wait();
            state.MarkConsumed();
            try
            {
                return state.Peek();
            }
            finally
            {
                state.RecycleAfterUse();
            }
        }

        public Awaiter GetAwaiter()
        {
            return new Awaiter(this);
        }

        /// <summary>Lets await work without allocating.</summary>
        public readonly struct Awaiter : ICriticalNotifyCompletion
        {
            private readonly Future<T> m_future;

            internal Awaiter(Future<T> future)
            {
                m_future = future;
            }

            public bool IsCompleted => m_future.IsDone;

            public T GetResult()
            {
                if (FutureContinuation.TakeMisused())
                {
                    throw new InvalidOperationException(
                        "This Future was awaited again while an earlier await was still waiting. "
                            + "Await a Future once, and use AsTask() to share it.");
                }
                FutureState<T> state = m_future.State;
                if (state == null)
                {
                    return m_future.m_value;
                }
                state.MarkConsumed();
                try
                {
                    return state.Peek();
                }
                finally
                {
                    state.RecycleAfterUse();
                }
            }

            public void OnCompleted(Action continuation)
            {
                UnsafeOnCompleted(continuation);
            }

            public void UnsafeOnCompleted(Action continuation)
            {
                FutureState<T> state = m_future.State;
                if (state == null)
                {
                    continuation();
                    return;
                }
                state.SetContinuation(FutureContinuation.Capture(continuation));
            }
        }

        object IEnumerator.Current => null;

        bool IEnumerator.MoveNext() => !IsDone;

        void IEnumerator.Reset() { }
    }

    /// <summary>
    /// A Rive call that finishes later, without a result. See <see cref="Future{T}"/> for how to use it.
    /// </summary>
    public readonly struct Future : IEnumerator
    {
        private readonly Future<bool> m_inner;

        internal Future(FutureState<bool> state)
        {
            m_inner = new Future<bool>(state);
        }

        /// <summary>True once it has finished, whichever way.</summary>
        public bool IsDone => m_inner.IsDone;

        /// <summary>Where it's up to.</summary>
        public FutureStatus Status => m_inner.Status;

        /// <summary>What it failed with, or null.</summary>
        public Exception Exception => m_inner.Exception;

        /// <summary>
        /// Runs once it finishes, on the main thread. Runs straight away if it already has.
        /// </summary>
        public event Action<Future> Completed
        {
            add
            {
                if (value == null)
                {
                    return;
                }
                Future self = this;
                m_inner.AddCompleted(value, () => value(self));
            }
            remove
            {
                m_inner.RemoveCompleted(value);
            }
        }

        /// <summary>
        /// A Task for this Future, for awaiting more than once, storing, or WhenAll. Counts as the one use. See <see cref="Future{T}.AsTask"/>.
        /// </summary>
        public Task AsTask()
        {
            return m_inner.AsTask();
        }

        /// <summary>
        /// Blocks until it finishes. Counts as the one use. Can't be called on the Rive producer thread.
        /// </summary>
        /// <remarks>
        /// On the main thread it delivers everything else that's finished while it waits, so other Futures' callbacks and continuations can run inside this call. On another thread it waits for the main thread to finish it, so don't call it from a thread the main thread is waiting on.
        /// </remarks>
        public void WaitForCompletion()
        {
            m_inner.WaitForCompletion();
        }

        public Awaiter GetAwaiter()
        {
            return new Awaiter(m_inner.GetAwaiter());
        }

        /// <summary>Lets await work without allocating.</summary>
        public readonly struct Awaiter : ICriticalNotifyCompletion
        {
            private readonly Future<bool>.Awaiter m_inner;

            internal Awaiter(Future<bool>.Awaiter inner)
            {
                m_inner = inner;
            }

            public bool IsCompleted => m_inner.IsCompleted;

            public void GetResult()
            {
                m_inner.GetResult();
            }

            public void OnCompleted(Action continuation)
            {
                m_inner.OnCompleted(continuation);
            }

            public void UnsafeOnCompleted(Action continuation)
            {
                m_inner.UnsafeOnCompleted(continuation);
            }
        }

        object IEnumerator.Current => null;

        bool IEnumerator.MoveNext() => !IsDone;

        void IEnumerator.Reset() { }
    }
}
