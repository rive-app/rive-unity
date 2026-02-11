using System;
using System.Runtime.InteropServices;

namespace Rive
{
    /// <summary>
    /// An artboard that can be bound to view model properties.
    /// </summary>
    public class BindableArtboard : IDisposable
    {
        private IntPtr m_nativeBindableArtboard;
        private string m_artboardName;
        private bool m_isDisposed = false;
        private readonly ViewModelInstanceSafeHandle m_viewModelInstanceHandle;

        internal IntPtr NativeBindableArtboard
        {
            get { return m_nativeBindableArtboard; }
        }

        internal ViewModelInstanceSafeHandle ViewModelInstanceHandle
        {
            get { return m_viewModelInstanceHandle; }
        }



        /// <summary>
        /// Constructor for the BindableArtboard class.
        /// </summary>
        /// <param name="nativeBindableArtboard">Pointer to the native bindable artboard.</param>
        internal BindableArtboard(IntPtr nativeBindableArtboard)
            : this(nativeBindableArtboard, null)
        {
        }

        /// <summary>
        /// Constructor for the BindableArtboard class with an optional bound ViewModel instance.
        /// </summary>
        /// <param name="nativeBindableArtboard">Pointer to the native bindable artboard.</param>
        /// <param name="viewModelInstance">Optional ViewModel instance to bind to this artboard.</param>
        internal BindableArtboard(IntPtr nativeBindableArtboard, ViewModelInstance viewModelInstance)
        {
            m_nativeBindableArtboard = nativeBindableArtboard;
            if (viewModelInstance != null &&
                viewModelInstance.NativeSafeHandle != null &&
                !viewModelInstance.NativeSafeHandle.IsInvalid)
            {
                m_viewModelInstanceHandle = viewModelInstance.NativeSafeHandle;
            }
            else
            {
                m_viewModelInstanceHandle = ViewModelInstanceSafeHandle.Null;
            }
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
                if (m_nativeBindableArtboard != IntPtr.Zero)
                {
                    unrefBindableArtboard(m_nativeBindableArtboard);
                    m_nativeBindableArtboard = IntPtr.Zero;
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
        public string Name
        {
            get
            {
                if (m_artboardName == null && !m_isDisposed)
                {
                    m_artboardName = Marshal.PtrToStringAnsi(getBindableArtboardName(m_nativeBindableArtboard));
                }
                return m_artboardName;
            }
        }

        #region Native Methods
        [DllImport(NativeLibrary.name)]
        private static extern void unrefBindableArtboard(IntPtr artboard);

        [DllImport(NativeLibrary.name)]
        private static extern IntPtr getBindableArtboardName(IntPtr artboard);
        #endregion
    }
}
