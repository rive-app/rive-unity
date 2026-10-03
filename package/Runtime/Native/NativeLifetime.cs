using System;
using System.Threading;

namespace Rive
{
    /// <summary>
    /// When a native object is deleted. Core deletes a file's artboards and an
    /// artboard's state machines with it, so the delete waits for the owner
    /// and for every child made from it. Any thread, finalizers included.
    /// </summary>
    internal sealed class NativeLifetime
    {
        private readonly Action m_delete;
        private readonly NativeLifetime m_parent;
        // The owner's ref plus one per child.
        private int m_refs = 1;
        private int m_ownerReleased;

        /// The child holds its parent until it's deleted.
        internal NativeLifetime(Action delete, NativeLifetime parent = null)
        {
            m_delete = delete;
            m_parent = parent;
            parent?.Retain();
        }

        private void Retain()
        {
            Interlocked.Increment(ref m_refs);
        }

        /// The owner lets go. Only the first call counts.
        internal void ReleaseOwner()
        {
            if (Interlocked.Exchange(ref m_ownerReleased, 1) == 0)
            {
                Release();
            }
        }

        internal bool IsOwnerReleased => Volatile.Read(ref m_ownerReleased) != 0;

        private void Release()
        {
            if (Interlocked.Decrement(ref m_refs) != 0)
            {
                return;
            }
            m_delete();
            m_parent?.Release();
        }
    }
}
