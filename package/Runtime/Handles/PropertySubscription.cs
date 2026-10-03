using System;

namespace Rive
{
    /// <summary>
    /// A callback on a view model property, from a property handle's <c>Subscribe</c>.
    /// </summary>
    /// <remarks>
    /// Keep it, and dispose it to stop the callback, for example in <c>OnDisable</c>. Disposing it more than once is fine. Disposing the view model instance handle it came from also stops it.
    /// </remarks>
    public sealed class PropertySubscription : IDisposable
    {
        private readonly ViewModelInstanceHandle m_instance;

        // Null once disposed.
        private Action m_unsubscribe;

        internal PropertySubscription(ViewModelInstanceHandle instance, Action unsubscribe)
        {
            m_instance = instance;
            m_unsubscribe = unsubscribe;
        }

        /// <summary>
        /// True until it's disposed, or the view model instance handle it came from is.
        /// </summary>
        public bool IsActive => m_unsubscribe != null && !m_instance.IsDisposed;

        /// <summary>
        /// Stops the callback.
        /// </summary>
        public void Dispose()
        {
            Action unsubscribe = m_unsubscribe;
            m_unsubscribe = null;
            unsubscribe?.Invoke();
        }

        /// For a subscribe that couldn't happen, like on a disposed instance.
        internal static PropertySubscription Inactive(ViewModelInstanceHandle instance)
        {
            return new PropertySubscription(instance, null);
        }
    }
}
