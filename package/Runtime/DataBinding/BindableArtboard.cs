using System;
using System.Runtime.InteropServices;

namespace Rive
{
    /// <summary>
    /// An artboard that can be bound to view model properties.
    /// </summary>
    public class BindableArtboard
    {
        private readonly IntPtr m_nativeBindableArtboard;
        private string m_artboardName;

        internal IntPtr NativeBindableArtboard
        {
            get { return m_nativeBindableArtboard; }
        }

        /// <summary>
        /// Constructor for the BindableArtboard class.
        /// </summary>
        /// <param name="nativeBindableArtboard">Pointer to the native bindable artboard.</param>
        internal BindableArtboard(IntPtr nativeBindableArtboard)
        {
            m_nativeBindableArtboard = nativeBindableArtboard;
        }

        ~BindableArtboard()
        {
            unrefBindableArtboard(m_nativeBindableArtboard);
        }

        /// <summary>
        /// Gets the name of the artboard.
        /// </summary>
        public string Name
        {
            get
            {
                if (m_artboardName == null)
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