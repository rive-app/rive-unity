namespace Rive
{
    /// <summary>
    /// Where a handle is up to. Methods that return a handle straight away queue the work that finds what it refers to, so the handle starts out <see cref="Pending"/>.
    /// </summary>
    internal enum HandleStatus
    {
        /// <summary>Rive hasn't looked it up yet. You can still use it: work you queue on it runs after the lookup.</summary>
        Pending = 0,
        /// <summary>It refers to something.</summary>
        Valid = 1,
        /// <summary>The lookup found nothing. The handle's Error says why, and work queued on it is skipped.</summary>
        Invalid = 2,
        /// <summary>It has been disposed, or its widget unloaded it.</summary>
        Disposed = 3,
    }
}
