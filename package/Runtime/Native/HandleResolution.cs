using System;
using System.Collections.Generic;
using Rive.Producer;
using Rive.Host;

namespace Rive
{
    /// <summary>
    /// A handle's status. The lookup runs on Rive's thread and records its
    /// result there first, so work queued after it can skip a failed one,
    /// then lands on the main thread, where the status changes.
    /// </summary>
    internal sealed class HandleResolution
    {
        private const int RivePending = 0;
        private const int RiveValid = 1;
        private const int RiveInvalid = 2;

        private static readonly Action s_waitDriver = () =>
        {
            CommandTransport.Barrier();
            CommandTransport.DrainMainThread();
        };

        // Main thread.
        private HandleStatus m_status;
        private RiveException m_error;
        private RiveException m_disposedError;
        private List<Action> m_waiters;

        // Rive's thread. Set by the lookup before anything queued after it runs.
        private int m_rive;
        private RiveException m_riveError;

        private HandleResolution(bool known)
        {
            if (known)
            {
                m_status = HandleStatus.Valid;
                m_rive = RiveValid;
            }
        }

        /// For a handle that refers to something from the start.
        internal static HandleResolution Known() => new HandleResolution(true);

        /// For a handle whose lookup is queued.
        internal static HandleResolution Pending() => new HandleResolution(false);

        internal HandleStatus Status => m_status;

        /// Why it's invalid. Kept after a dispose.
        internal RiveException Error => m_error;

        /// Rive's thread. True once the lookup has run.
        internal bool IsResolvedOnRive => m_rive != RivePending;

        /// Rive's thread. The lookup ran and found nothing.
        internal bool FailedOnRive => m_rive == RiveInvalid;

        /// Rive's thread. Why, for handles built on this one.
        internal RiveException RiveError => m_riveError;

        /// Rive's thread, from the lookup.
        internal void SucceedOnRive()
        {
            if (m_rive != RivePending)
            {
                return;
            }
            m_rive = RiveValid;
            CommandTransport.PostToMainThread(() => Land(null));
        }

        /// Rive's thread, from the lookup. The caller reports the error, or
        /// doesn't when it came from a handle this one was built on.
        internal void FailOnRive(RiveException error)
        {
            if (m_rive != RivePending)
            {
                return;
            }
            m_rive = RiveInvalid;
            m_riveError = error;
            CommandTransport.PostToMainThread(() => Land(error));
        }

        private void Land(RiveException error)
        {
            if (m_status != HandleStatus.Pending)
            {
                return;
            }
            m_status = error == null ? HandleStatus.Valid : HandleStatus.Invalid;
            m_error = error;
            RunWaiters();
        }

        /// Main thread. Pending waiters fail with ResourceDisposed.
        internal void MarkDisposed(string what)
        {
            if (m_status == HandleStatus.Disposed)
            {
                return;
            }
            m_status = HandleStatus.Disposed;
            m_disposedError = new RiveException(RiveErrorCode.ResourceDisposed, $"{what} has been disposed.");
            RunWaiters();
        }

        /// Main thread. A new waiter per call, so each caller gets its own Future.
        internal Future<T> Wait<T>(T handle)
        {
            var state = new FutureState<T>();
            if (m_status == HandleStatus.Pending)
            {
                state.WaitDriver = s_waitDriver;
                if (m_waiters == null)
                {
                    m_waiters = new List<Action>();
                }
                m_waiters.Add(() => Finish(state, handle));
            }
            else
            {
                Finish(state, handle);
            }
            return new Future<T>(state);
        }

        private void Finish<T>(FutureState<T> state, T handle)
        {
            switch (m_status)
            {
                case HandleStatus.Valid:
                    state.Succeed(handle);
                    break;
                case HandleStatus.Invalid:
                    state.Fail(m_error);
                    break;
                case HandleStatus.Disposed:
                    state.Fail(m_disposedError);
                    break;
            }
        }

        private void RunWaiters()
        {
            List<Action> waiters = m_waiters;
            m_waiters = null;
            if (waiters == null)
            {
                return;
            }
            for (int i = 0; i < waiters.Count; i++)
            {
                waiters[i]();
            }
        }
    }
}
