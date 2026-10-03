using System;

namespace Rive
{
    /// <summary>
    /// Represents a Rive BindableArtboard for scenarios
    /// where immediate, non-blocking access is required.
    /// </summary>
    /// <remarks>
    /// Returned immediately by <see cref="FileHandle.GetBindableArtboard(string, ViewModelInstanceHandle)"/>, before Rive has made it. Use it straight away: what you queue on it runs after that.
    /// </remarks>
    public sealed class BindableArtboardHandle : IDisposable
    {
        // Native artboard slot. Core's artboards are what a property takes.
        private readonly NativeSlot<NativeArtboardHandle> m_native;

        private readonly string m_name;

        // The view model instance the artboard uses, if any.
        private readonly ViewModelInstanceHandle m_boundInstance;

        private readonly HandleResolution m_resolution;

        private bool m_isDisposed;

        internal BindableArtboardHandle(
            NativeSlot<NativeArtboardHandle> native, HandleResolution resolution, string name, ViewModelInstanceHandle boundInstance)
        {
            m_native = native;
            m_resolution = resolution;
            m_name = name;
            m_boundInstance = boundInstance;
        }

        ~BindableArtboardHandle()
        {
            if (!m_isDisposed)
            {
                ViewModelInstanceNative.UnrefBindableArtboardLater(m_native);
            }
        }

        internal NativeSlot<NativeArtboardHandle> Native => m_native;

        internal HandleResolution Resolution => m_resolution;

        // Where the lookup is up to. Internal, so scripts don't gate writes on it.
        internal HandleStatus Status => m_resolution.Status;

        // Why the lookup found nothing, or null. Kept after a dispose.
        internal RiveException Error => m_resolution.Error;

        /// <summary>
        /// Waits for Rive to look the artboard up. You don't need to before using the handle.
        /// </summary>
        /// <returns>An operation that finishes on the main thread with this handle once Rive has found what it refers to. It fails with the lookup's <see cref="RiveException"/> if Rive found nothing, the same one reported when that happened, or with <see cref="RiveErrorCode.ResourceDisposed"/> if it's disposed first. Each call gives its own operation.</returns>
        public Future<BindableArtboardHandle> ResolveAsync() => m_resolution.Wait(this);

        internal ViewModelInstanceHandle BoundInstance => m_boundInstance;

        internal string Name => m_name;

        /// <summary>
        /// Gets the artboard's name.
        /// </summary>
        /// <returns>An operation that finishes on the main thread with the name.</returns>
        public Future<string> GetNameAsync() => Future<string>.FromResult(m_name);

        /// <summary>
        /// True once the handle has been disposed.
        /// </summary>
        public bool IsDisposed => m_isDisposed;

        /// <summary>
        /// Releases this handle's hold on the artboard. Properties it was set on keep it.
        /// </summary>
        public void Dispose()
        {
            if (m_isDisposed)
            {
                return;
            }
            m_isDisposed = true;
            m_resolution.MarkDisposed($"Bindable artboard '{m_name}'");
            ViewModelInstanceNative.ReleaseBindableArtboardLater(m_native);
            GC.SuppressFinalize(this);
        }
    }
}
