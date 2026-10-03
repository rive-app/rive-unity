using System;

namespace Rive
{
    /// <summary>
    /// Represents a Rive image property for scenarios where immediate, non-blocking access is required.
    /// </summary>
    /// <remarks>
    /// Returned immediately by <see cref="ViewModelInstanceHandle.GetImageProperty(string)"/>.
    /// </remarks>
    public sealed class ImagePropertyHandle : ViewModelPropertyHandle
#if RIVE_USING_EXPERIMENTAL
        , IRenderImageTarget
#endif
    {
        /// <summary>
        /// Waits for Rive to check the path, queuing the check if nothing has yet. You don't need to before using the handle.
        /// </summary>
        /// <returns>An operation that finishes on the main thread with this handle once Rive has found the property. It fails with the lookup's <see cref="RiveException"/> if Rive found nothing, the same one reported when that happened, or with <see cref="RiveErrorCode.ResourceDisposed"/> if its instance is disposed first. Each call gives its own operation.</returns>
        public Future<ImagePropertyHandle> ResolveAsync() => Resolve(this);

        internal ImagePropertyHandle(ViewModelInstanceHandle instance, string path) : base(instance, path)
        {
        }

        internal override ViewModelDataType Type => ViewModelDataType.AssetImage;

        /// <summary>
        /// Calls <paramref name="callback"/> on the main thread after each advance in which the image changed.
        /// </summary>
        /// <returns>The subscription. Dispose it to stop the callback, for example in <c>OnDisable</c>.</returns>
        public PropertySubscription Subscribe(Action callback)
        {
            return AddCallback(callback);
        }

        /// <summary>
        /// Sets the image. Queued, so it applies before the next advance.
        /// </summary>
        /// <param name="image">The image, or null to clear it. It doesn't need loading first: the write decodes it if <see cref="OutOfBandAsset.LoadAsync"/> hasn't, and keeps it alive until the write has run. Load it ahead of time to keep the decode off the first frame that uses it.</param>
        public void SetValue(ImageOutOfBandAsset image)
        {
#if RIVE_USING_EXPERIMENTAL
            // So a RenderTexture image bound earlier doesn't overwrite it.
            if (RenderTextureImageManager.HasAnyBindings)
            {
                RenderTextureImageManager.Instance.Unbind(this);
            }
#endif
            SetAsset(nameof(SetValue), image, isImage: true);
        }

#if RIVE_USING_EXPERIMENTAL
        /// <summary>
        /// Binds a RenderTexture-backed image (e.g. video frames, custom GPU content) to this property. Pass null to clear.
        /// </summary>
        public void SetFromRenderTextureImageSource(RenderTextureImageSource image)
        {
            if (!CheckUsable(nameof(SetFromRenderTextureImageSource)))
            {
                return;
            }
            if (image == null)
            {
                RenderTextureImageManager.Instance.Unbind(this);
                SetAsset(nameof(SetFromRenderTextureImageSource), null, isImage: true);
                return;
            }
            if (!RenderTextureImageManager.Instance.BindPropertyToImage(image, this))
            {
                Utils.DebugLogger.Instance.LogWarning("Failed to bind RenderTexture image.");
            }
        }

        bool IRenderImageTarget.IsGone => Instance.IsDisposed;

        bool IRenderImageTarget.TryResolve(out NativeViewModelInstanceHandle instance, out string path)
        {
            EnsureResolveSent();
            instance = Root;
            path = Path;
            return instance.IsValid;
        }

        void IRenderImageTarget.ClearRenderImage()
        {
            if (!Instance.IsDisposed)
            {
                SetAsset(nameof(IRenderImageTarget.ClearRenderImage), null, isImage: true);
            }
        }
#endif


        internal override void RaiseChanged(in PropertyValue value)
        {
            (Callbacks as Action)?.Invoke();
        }
    }

    /// <summary>
    /// Represents a Rive font property for scenarios where immediate, non-blocking access is required.
    /// </summary>
    /// <remarks>
    /// Returned immediately by <see cref="ViewModelInstanceHandle.GetFontProperty(string)"/>.
    /// </remarks>
    public sealed class FontPropertyHandle : ViewModelPropertyHandle
    {
        /// <summary>
        /// Waits for Rive to check the path, queuing the check if nothing has yet. You don't need to before using the handle.
        /// </summary>
        /// <returns>An operation that finishes on the main thread with this handle once Rive has found the property. It fails with the lookup's <see cref="RiveException"/> if Rive found nothing, the same one reported when that happened, or with <see cref="RiveErrorCode.ResourceDisposed"/> if its instance is disposed first. Each call gives its own operation.</returns>
        public Future<FontPropertyHandle> ResolveAsync() => Resolve(this);

        internal FontPropertyHandle(ViewModelInstanceHandle instance, string path) : base(instance, path)
        {
        }

        internal override ViewModelDataType Type => ViewModelDataType.AssetFont;

        /// <summary>
        /// Calls <paramref name="callback"/> on the main thread after each advance in which the font changed.
        /// </summary>
        /// <returns>The subscription. Dispose it to stop the callback, for example in <c>OnDisable</c>.</returns>
        public PropertySubscription Subscribe(Action callback)
        {
            return AddCallback(callback);
        }

        /// <summary>
        /// Sets the font. Queued, so it applies before the next advance.
        /// </summary>
        /// <param name="font">The font, or null to clear it. It doesn't need loading first: the write decodes it if <see cref="OutOfBandAsset.LoadAsync"/> hasn't, and keeps it alive until the write has run.</param>
        public void SetValue(FontOutOfBandAsset font)
        {
            SetAsset(nameof(SetValue), font, isImage: false);
        }


        internal override void RaiseChanged(in PropertyValue value)
        {
            (Callbacks as Action)?.Invoke();
        }
    }

    /// <summary>
    /// Represents a Rive artboard property for scenarios where immediate, non-blocking access is required.
    /// </summary>
    /// <remarks>
    /// Returned immediately by <see cref="ViewModelInstanceHandle.GetArtboardProperty(string)"/>.
    /// </remarks>
    public sealed class ArtboardPropertyHandle : ViewModelPropertyHandle
    {
        /// <summary>
        /// Waits for Rive to check the path, queuing the check if nothing has yet. You don't need to before using the handle.
        /// </summary>
        /// <returns>An operation that finishes on the main thread with this handle once Rive has found the property. It fails with the lookup's <see cref="RiveException"/> if Rive found nothing, the same one reported when that happened, or with <see cref="RiveErrorCode.ResourceDisposed"/> if its instance is disposed first. Each call gives its own operation.</returns>
        public Future<ArtboardPropertyHandle> ResolveAsync() => Resolve(this);

        internal ArtboardPropertyHandle(ViewModelInstanceHandle instance, string path) : base(instance, path)
        {
        }

        internal override ViewModelDataType Type => ViewModelDataType.Artboard;

        /// <summary>
        /// Calls <paramref name="callback"/> on the main thread after each advance in which the artboard changed.
        /// </summary>
        /// <returns>The subscription. Dispose it to stop the callback, for example in <c>OnDisable</c>.</returns>
        public PropertySubscription Subscribe(Action callback)
        {
            return AddCallback(callback);
        }

        /// <summary>
        /// Sets the artboard. Queued, so it applies before the next advance.
        /// </summary>
        /// <param name="artboard">The artboard, from <see cref="FileHandle.GetBindableArtboard(string, ViewModelInstanceHandle)"/>, or null to clear it.</param>
        public void SetValue(BindableArtboardHandle artboard)
        {
            if (artboard != null && artboard.IsDisposed)
            {
                Utils.DebugLogger.Instance.LogError($"SetValue: the artboard for '{Path}' has been disposed.");
                return;
            }
            if (CheckUsable(nameof(SetValue)))
            {
                ViewModelInstanceNative.SetArtboardLater(this, artboard);
            }
        }


        internal override void RaiseChanged(in PropertyValue value)
        {
            (Callbacks as Action)?.Invoke();
        }
    }
}
