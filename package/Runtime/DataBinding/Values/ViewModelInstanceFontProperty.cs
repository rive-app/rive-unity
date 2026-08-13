using System;
using System.Runtime.InteropServices;
using Rive.Utils;

namespace Rive
{
    /// <summary>
    /// A view model instance property for font properties.
    /// </summary>
    public sealed class ViewModelInstanceFontProperty : ViewModelInstancePrimitiveProperty
    {
        public ViewModelInstanceFontProperty(IntPtr instanceValuePtr, ViewModelInstance instance) : base(instanceValuePtr, instance)
        {
        }

        /// <summary>
        /// Sets the font asset for the property.
        /// </summary>
        public FontOutOfBandAsset Value
        {
            set
            {
                ThrowIfOwnerDisposed();
                SetFont(value);
            }
        }

        /// <summary>
        /// Raised when the font property is changed in the Rive graphic.
        /// </summary>
        public event Action OnValueChanged
        {
            add => AddPropertyCallback(value, ref m_onValueChanged);
            remove => RemovePropertyCallback(value, ref m_onValueChanged);
        }
        private Action m_onValueChanged;

        private void SetFont(FontOutOfBandAsset fontAsset)
        {
            if (fontAsset != null && fontAsset.NativeAsset == IntPtr.Zero)
            {
                DebugLogger.Instance.LogWarning("Trying to assign an unloaded font asset.");
                return;
            }

            bool wasSuccess = setViewModelInstanceFontValue(
                InstancePropertyPtr,
                fontAsset == null ? IntPtr.Zero : fontAsset.NativeAsset);

            if (!wasSuccess)
            {
                DebugLogger.Instance.LogWarning("Failed to set font asset.");
            }
        }

        internal override void RaiseChangedEvent()
        {
            m_onValueChanged?.Invoke();
        }

        internal override void ClearAllCallbacks()
        {
            m_onValueChanged = null;
            base.ClearAllCallbacks();
        }

        internal override void ClearDelegatesOnly()
        {
            m_onValueChanged = null;
        }

        [DllImport(NativeLibrary.name)]
        private static extern bool setViewModelInstanceFontValue(IntPtr instanceProperty,
            IntPtr fontAsset);
    }
}
