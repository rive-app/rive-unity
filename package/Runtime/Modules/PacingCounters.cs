namespace Rive
{
    /// <summary>
    /// Counters the pacing overlay reads. Off unless a panel shows the overlay.
    /// </summary>
    internal static class PacingCounters
    {
        internal static bool Enabled;
        internal static int ImageBuilds;
        // Background panel recordings asked for, and the ones that went out.
        // The rest waited behind one still out and went later, merged.
        internal static int RecordsWanted;
        internal static int RecordsSent;
    }
}
