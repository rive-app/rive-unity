namespace Rive
{
    /// <summary>
    /// Counters the pacing overlay reads. Off unless a panel shows the overlay.
    /// </summary>
    internal static class PacingCounters
    {
        internal static bool Enabled;
        internal static int ImageBuilds;
    }
}
