using System;

namespace Rive
{
    /// <summary>
    /// An artboard that can be bound to view model properties.
    /// </summary>
    public class BindableArtboard : IDisposable
    {
        // Core's artboard instances are what a property takes.
        private NativeArtboardHandle m_nativeHandle;
        private readonly string m_artboardName;
        private bool m_isDisposed = false;

        // Held so the instance isn't finalized while this can still bind it.
        private readonly ViewModelInstance m_viewModelInstance;

        internal NativeArtboardHandle NativeHandle => m_nativeHandle;

        /// <summary>
        /// The instance to bind alongside the artboard, or none.
        /// </summary>
        internal NativeViewModelInstanceHandle BoundInstanceHandle =>
            m_viewModelInstance != null && !m_viewModelInstance.IsDisposed ? m_viewModelInstance.NativeHandle : default;

        /// <param name="nativeHandle">The native artboard.</param>
        /// <param name="name">The artboard's name.</param>
        /// <param name="viewModelInstance">Optional ViewModel instance to bind to this artboard.</param>
        internal BindableArtboard(NativeArtboardHandle nativeHandle, string name, ViewModelInstance viewModelInstance = null)
        {
            m_nativeHandle = nativeHandle;
            m_artboardName = name;
            m_viewModelInstance = viewModelInstance;
        }

        /// <summary>
        /// Dispose of the BindableArtboard and release native resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!m_isDisposed)
            {
                if (m_nativeHandle.IsValid)
                {
                    ArtboardNative.Delete(m_nativeHandle);
                    m_nativeHandle = default;
                }

                m_isDisposed = true;
            }
        }

        ~BindableArtboard()
        {
            Dispose(false);
        }

        /// <summary>
        /// Gets the name of the artboard.
        /// </summary>
        public string Name => m_isDisposed ? null : m_artboardName;
    }
}
