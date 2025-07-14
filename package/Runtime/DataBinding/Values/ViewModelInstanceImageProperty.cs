using System;
using System.Runtime.InteropServices;
using Rive.Utils;

namespace Rive
{
    /// <summary>
    /// A view model instance property for image properties.
    /// </summary>
    public sealed class ViewModelInstanceImageProperty : ViewModelInstancePrimitiveProperty
    {
        private ImageOutOfBandAsset m_lastAssignedImageAsset = null;

        public ViewModelInstanceImageProperty(IntPtr instanceValuePtr, ViewModelInstance instance) : base(instanceValuePtr, instance)
        {
        }

        /// <summary>
        /// Sets the image asset for the property.
        /// </summary>
        /// <param name="imageAsset"> The image asset to set. </param>
        public ImageOutOfBandAsset Value
        {
            set => SetImage(value);
        }

        /// <summary>
        /// Raised when the image property is changed in the Rive graphic.
        /// </summary>
        public event Action<ImageOutOfBandAsset> OnValueChanged
        {
            add => AddPropertyCallback(value, ref m_onValueChanged);
            remove => RemovePropertyCallback(value, ref m_onValueChanged);
        }
        private Action<ImageOutOfBandAsset> m_onValueChanged;

        /// <summary>
        /// Sets the image asset for the property.
        /// </summary>
        /// <param name="imageAsset"> The image asset to set. </param>
        private void SetImage(ImageOutOfBandAsset imageAsset)
        {

            if (imageAsset != null && imageAsset.NativeAsset == IntPtr.Zero)
            {
                DebugLogger.Instance.LogWarning("Trying to assign an unloaded image asset.");
                return;
            }

            bool wasSuccess = setViewModelInstanceImageValue(InstancePropertyPtr, imageAsset == null ? IntPtr.Zero : imageAsset.NativeAsset);

            if (wasSuccess)
            {
                m_lastAssignedImageAsset = imageAsset;
            }
            else
            {
                DebugLogger.Instance.LogWarning("Failed to set image asset.");
            }

        }

        internal override void RaiseChangedEvent()
        {
            m_onValueChanged?.Invoke(m_lastAssignedImageAsset);
        }

        internal override void ClearAllCallbacks()
        {
            m_onValueChanged = null;
            base.ClearAllCallbacks();
        }

        [DllImport(NativeLibrary.name)]
        private static extern bool setViewModelInstanceImageValue(IntPtr instanceProperty,
            IntPtr imageAsset);
    }
}
