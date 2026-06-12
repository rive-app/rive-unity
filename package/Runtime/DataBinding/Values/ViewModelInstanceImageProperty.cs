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


        public ViewModelInstanceImageProperty(IntPtr instanceValuePtr, ViewModelInstance instance) : base(instanceValuePtr, instance)
        {
        }

        /// <summary>
        /// Sets the image asset for the property.
        /// </summary>
        /// <param name="imageAsset"> The image asset to set. </param>
        public ImageOutOfBandAsset Value
        {
            set
            {
                ThrowIfOwnerDisposed();
                SetImage(value);
            }
        }

        /// <summary>
        /// Raised when the image property is changed in the Rive graphic.
        /// </summary>
        public event Action OnValueChanged
        {
            add => AddPropertyCallback(value, ref m_onValueChanged);
            remove => RemovePropertyCallback(value, ref m_onValueChanged);
        }
        private Action m_onValueChanged;

        /// <summary>
        /// Sets the image asset for the property.
        /// </summary>
        /// <param name="imageAsset"> The image asset to set. </param>
        private void SetImage(ImageOutOfBandAsset imageAsset)
        {
#if RIVE_USING_EXPERIMENTAL
            // This prevents the image from being overridden by a previously set RenderTexture-backed image.
            if (RenderTextureImageManager.HasAnyBindings)
            {
                RenderTextureImageManager.Instance.Unbind(this);
            }
#endif

            if (imageAsset != null && imageAsset.NativeAsset == IntPtr.Zero)
            {
                DebugLogger.Instance.LogWarning("Trying to assign an unloaded image asset.");
                return;
            }

            bool wasSuccess = setViewModelInstanceImageValue(InstancePropertyPtr, imageAsset == null ? IntPtr.Zero : imageAsset.NativeAsset);

            if (!wasSuccess)
            {
                DebugLogger.Instance.LogWarning("Failed to set image asset.");
            }

        }

#if RIVE_USING_EXPERIMENTAL
        /// <summary>
        /// Binds a RenderTexture-backed image (e.g. video frames, custom GPU
        /// content) to this property. Pass null to clear.
        /// </summary>
        public void SetFromRenderTextureImageSource(RenderTextureImageSource image)
        {
            ThrowIfOwnerDisposed();
            if (image == null)
            {
                RenderTextureImageManager.Instance.Unbind(this);
                ApplyRenderImagePointer(IntPtr.Zero);
                return;
            }
            // The manager owns the image to property binding and the per-frame
            // re-push, so this only needs to be called once.
            if (!RenderTextureImageManager.Instance.BindPropertyToImage(image, this))
            {
                DebugLogger.Instance.LogWarning("Failed to bind RenderTexture image.");
            }
        }

        /// <summary>
        /// Pushes a raw native RenderImage pointer into this property. Used by
        /// <see cref="RenderTextureImageSource"/> to re-bind its per-frame pointer
        /// without re-attaching. Returns false if the push failed (e.g. owner
        /// disposed) so the per-frame path can stay quiet.
        /// </summary>
        internal bool ApplyRenderImagePointer(IntPtr nativeImagePtr)
        {
            if (RootInstance != null && RootInstance.IsDisposed)
            {
                return false;
            }
            return setViewModelInstanceImageValue(InstancePropertyPtr, nativeImagePtr);
        }
#endif // RIVE_USING_EXPERIMENTAL

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
        private static extern bool setViewModelInstanceImageValue(IntPtr instanceProperty,
            IntPtr imageAsset);
    }
}
