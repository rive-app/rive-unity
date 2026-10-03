using System;

namespace Rive
{
    /// <summary>
    /// A loaded Rive file: a <see cref="File"/> or a <see cref="FileHandle"/>.
    /// </summary>
    public interface ILoadedFile : IDisposable
    {
        /// <summary>
        /// True once the file has been disposed.
        /// </summary>
        bool IsDisposed { get; }
    }
}
