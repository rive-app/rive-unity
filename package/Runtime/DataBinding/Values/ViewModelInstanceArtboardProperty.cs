using System;
using System.Runtime.InteropServices;
using Rive.Utils;

namespace Rive
{
    /// <summary>
    /// A view model instance property for artboard properties.
    /// </summary>
    public sealed class ViewModelInstanceArtboardProperty : ViewModelInstancePrimitiveProperty
    {
        private BindableArtboard m_lastAssignedArtboard = null;

        public ViewModelInstanceArtboardProperty(IntPtr instanceValuePtr, ViewModelInstance instance) : base(instanceValuePtr, instance)
        {
        }

        /// <summary>
        /// Sets the artboard value for this property.
        /// </summary>
        public BindableArtboard Value
        {
            set
            {
                SetArtboard(value);
            }
        }

        /// <summary>
        /// Raised when the artboard property is changed in the Rive graphic.
        /// </summary>
        public event Action<BindableArtboard> OnValueChanged
        {
            add => AddPropertyCallback(value, ref m_onValueChanged);
            remove => RemovePropertyCallback(value, ref m_onValueChanged);
        }
        private Action<BindableArtboard> m_onValueChanged;

        /// <summary>
        /// Sets the artboard for the property.
        /// </summary>
        /// <param name="artboard"> The artboard to set. </param>
        private void SetArtboard(BindableArtboard artboard)
        {
            if (artboard == null)
            {
                DebugLogger.Instance.LogError("Trying to assign a null artboard.");
                return;
            }

            if (artboard.NativeBindableArtboard == IntPtr.Zero)
            {
                DebugLogger.Instance.LogError("Trying to assign an invalid artboard.");
                return;
            }

            bool wasSuccess = setViewModelInstanceArtboardValue(InstancePropertyPtr, artboard?.NativeBindableArtboard ?? IntPtr.Zero);

            if (wasSuccess)
            {
                m_lastAssignedArtboard = artboard;
            }
            else
            {
                DebugLogger.Instance.LogError("Failed to set artboard.");
            }
        }

        internal override void RaiseChangedEvent()
        {
            m_onValueChanged?.Invoke(m_lastAssignedArtboard);
        }

        internal override void ClearAllCallbacks()
        {
            m_onValueChanged = null;
            base.ClearAllCallbacks();
        }

        [DllImport(NativeLibrary.name)]
        private static extern bool setViewModelInstanceArtboardValue(IntPtr instanceProperty, IntPtr artboard);
    }
}