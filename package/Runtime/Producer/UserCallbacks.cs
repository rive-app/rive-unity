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
        // Channel deliveries on the stack. Afterward waits for these too, a
        // join from inside one would throw.
        private static int s_deliveries;
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
                if (--s_depth == 0 && s_deliveries == 0)
                {
                    RunAfterward();
                }
            }
        }

        internal readonly struct DeliveryScope : IDisposable
        {
            private readonly bool m_active;

            internal DeliveryScope(bool active)
            {
                m_active = active;
            }

            public void Dispose()
            {
                if (!m_active)
                {
                    return;
                }
                if (--s_deliveries == 0 && s_depth == 0)
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

        /// For a channel landing a result. User code it calls can ask for
        /// Afterward, and that waits until the delivery has finished.
        internal static DeliveryScope Delivery()
        {
            s_deliveries++;
            return new DeliveryScope(true);
        }

        /// Runs once the user code and channel deliveries on the stack have
        /// returned, or now if there aren't any. For changes that would join
        /// or reorder delivery.
        internal static void Afterward(Action action)
        {
            if (s_depth == 0 && s_deliveries == 0)
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
