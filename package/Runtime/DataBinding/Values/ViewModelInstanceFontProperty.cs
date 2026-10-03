using System;
using Rive.Utils;
using Rive.Host;

namespace Rive
{
    /// <summary>
    /// A view model instance property for font properties.
    /// </summary>
    public sealed class ViewModelInstanceFontProperty : ViewModelInstancePrimitiveProperty
    {
        internal ViewModelInstanceFontProperty(ViewModelInstance instance, string name, int slot) : base(instance, name, slot)
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
            if (fontAsset != null && !fontAsset.NativeHandle.IsValid)
            {
                DebugLogger.Instance.LogWarning("Trying to assign an unloaded font asset.");
                return;
            }

            bool wasSuccess = ViewModelNative.SetAsset(InstanceHandle, Name, ViewModelDataType.AssetFont, fontAsset == null ? default : fontAsset.NativeHandle);

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
    }
}
