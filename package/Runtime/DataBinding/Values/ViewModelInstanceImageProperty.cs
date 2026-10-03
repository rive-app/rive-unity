using System;
using Rive.Utils;
using Rive.Host;

namespace Rive
{
    /// <summary>
    /// A view model instance property for image properties.
    /// </summary>
    public sealed class ViewModelInstanceImageProperty : ViewModelInstancePrimitiveProperty
#if RIVE_USING_EXPERIMENTAL
        , IRenderImageTarget
#endif
    {


        internal ViewModelInstanceImageProperty(ViewModelInstance instance, string name, int slot) : base(instance, name, slot)
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

            if (imageAsset != null && !imageAsset.NativeHandle.IsValid)
            {
                DebugLogger.Instance.LogWarning("Trying to assign an unloaded image asset.");
                return;
            }

            bool wasSuccess = ViewModelNative.SetAsset(InstanceHandle, Name, ViewModelDataType.AssetImage, imageAsset == null ? default : imageAsset.NativeHandle);

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
                ClearRenderImage();
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
        /// Empties the property. False if that failed, e.g. the owner was disposed.
        /// </summary>
        internal bool ClearRenderImage()
        {
            if (RootInstance != null && RootInstance.IsDisposed)
            {
                return false;
            }
            return ViewModelNative.SetAsset(InstanceHandle, Name, ViewModelDataType.AssetImage, default);
        }

        bool IRenderImageTarget.IsGone => RootInstance != null && RootInstance.IsDisposed;

        bool IRenderImageTarget.TryResolve(out NativeViewModelInstanceHandle instance, out string path)
        {
            instance = InstanceHandle;
            path = Name;
            return IsAttached;
        }

        void IRenderImageTarget.ClearRenderImage()
        {
            ClearRenderImage();
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
    }
}
