using System;
using Rive.Utils;
using Rive.Host;

namespace Rive
{
    /// <summary>
    /// A view model instance property for artboard properties.
    /// </summary>
    public sealed class ViewModelInstanceArtboardProperty : ViewModelInstancePrimitiveProperty
    {
        internal ViewModelInstanceArtboardProperty(ViewModelInstance instance, string name, int slot) : base(instance, name, slot)
        {
        }

        /// <summary>
        /// Sets the artboard value for this property.
        /// </summary>
        public BindableArtboard Value
        {
            set
            {
                ThrowIfOwnerDisposed();
                SetArtboardInternal(value);
            }
        }

        /// <summary>
        /// Raised when the artboard property is changed in the Rive graphic.
        /// </summary>
        public event Action OnValueChanged
        {
            add => AddPropertyCallback(value, ref m_onValueChanged);
            remove => RemovePropertyCallback(value, ref m_onValueChanged);
        }
        private Action m_onValueChanged;

        /// <summary>
        /// Sets the artboard for the property.
        /// </summary>
        private void SetArtboardInternal(BindableArtboard artboard)
        {
            if (artboard != null && !artboard.NativeHandle.IsValid)
            {
                DebugLogger.Instance.LogError("Trying to assign an invalid artboard.");
                return;
            }


            bool wasSuccess = ViewModelNative.SetArtboard(InstanceHandle, Name, artboard != null ? artboard.NativeHandle : default, artboard != null ? artboard.BoundInstanceHandle : default);


            if (!wasSuccess)
            {
                DebugLogger.Instance.LogError("Failed to set artboard.");
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
