using System;
using System.Collections.Generic;
using Rive.Utils;
using Rive.Host;

namespace Rive.Producer
{
    /// <summary>
    /// For calling user code from our frame path. The no-wait guard is for our
    /// code, so it's paused here, and whether user code waits is up to the user.
    /// Main thread only.
    /// </summary>
    internal static class UserCallbacks
    {
        private static int s_depth;
        private static readonly List<Action> s_afterward = new List<Action>();

        /// True while user code we called is running.
        internal static bool Running => s_depth > 0;

        internal readonly struct CallbackScope : IDisposable
        {
            private readonly CommandTransport.WaitScope m_wait;
            private readonly bool m_active;

            internal CallbackScope(CommandTransport.WaitScope wait)
            {
                m_wait = wait;
                m_active = true;
            }

            public void Dispose()
            {
                if (!m_active)
                {
                    return;
                }
                m_wait.Dispose();
                if (--s_depth == 0)
                {
                    RunAfterward();
                }
            }
        }

        internal static CallbackScope Scope()
        {
            s_depth++;
            return new CallbackScope(CommandTransport.UserCode());
        }

        /// Runs once the user code on the stack has returned, or now if there
        /// isn't any. For changes that would join or reorder delivery.
        internal static void Afterward(Action action)
        {
            if (s_depth == 0)
            {
                action();
                return;
            }
            s_afterward.Add(action);
        }

        private static void RunAfterward()
        {
            while (s_afterward.Count > 0)
            {
                Action action = s_afterward[0];
                s_afterward.RemoveAt(0);
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
}
