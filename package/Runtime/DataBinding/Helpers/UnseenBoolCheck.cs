#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Rive.Utils;

namespace Rive
{
    /// <summary>
    /// Development builds only. Warns when a view model bool is set and then
    /// set to something else before any state machine advanced, since a state
    /// machine only sees the last value. Once per property.
    /// </summary>
    internal struct UnseenBoolCheck
    {
        // Counts every advance written, from any state machine. So it can
        // miss a lost value, but never warns about one that was seen.
        internal static int Advances;

        private int m_advances;
        private bool m_value;
        private bool m_written;
        private bool m_warned;

        internal void Write(bool value, string name)
        {
            if (m_written && !m_warned && m_advances == Advances && value != m_value)
            {
                m_warned = true;
                DebugLogger.Instance.LogWarning(
                    $"View model bool '{name}' was set to {m_value} and then {value} before the state machine advanced, " +
                    $"so it only sees {value}. Keep it set until the next frame, or use a trigger for one-shots. " +
                    "Logged once per property, in development builds only.");
            }
            m_written = true;
            m_advances = Advances;
            m_value = value;
        }
    }
}
#endif
