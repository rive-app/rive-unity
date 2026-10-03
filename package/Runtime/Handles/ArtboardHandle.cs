using System;
using System.Collections.Generic;
using Rive.Utils;

namespace Rive
{
    /// <summary>
    /// Represents a Rive Artboard instance created from a <see cref="FileHandle"/> for scenarios
    /// where immediate, non-blocking access is required.
    /// </summary>
    /// <remarks>
    /// Returned immediately by <see cref="FileHandle.InstantiateArtboard(string)"/>, before Rive has made it. Use it straight away: what you queue on it runs after that. A widget's artboard already exists.
    /// </remarks>
    public sealed class ArtboardHandle : IDisposable
    {
        private readonly NativeSlot<NativeArtboardHandle> m_native;
        private readonly FileHandle m_file;
        private readonly FileContents.ArtboardInfo m_info;
        private readonly bool m_owned;
        private readonly HandleResolution m_resolution;
        // Released by this handle only when it owns the artboard.
        private readonly NativeLifetime m_lifetime;
        private bool m_isDisposed;

        internal ArtboardHandle(
            NativeSlot<NativeArtboardHandle> native, HandleResolution resolution, FileHandle file, FileContents.ArtboardInfo info, bool owned, NativeLifetime lifetime)
        {
            m_native = native;
            m_resolution = resolution;
            m_file = file;
            m_info = info;
            m_owned = owned;
            m_lifetime = lifetime;
        }

        ~ArtboardHandle()
        {
            if (m_owned && !m_isDisposed)
            {
                m_lifetime?.ReleaseOwner();
            }
        }

        internal NativeSlot<NativeArtboardHandle> Native => m_native;

        internal NativeLifetime Lifetime => m_lifetime;

        internal HandleResolution Resolution => m_resolution;

        // Where the lookup is up to. Internal, so scripts don't gate writes on it.
        internal HandleStatus Status => m_resolution.Status;

        // Why the lookup found nothing, or null. Kept after a dispose.
        internal RiveException Error => m_resolution.Error;

        /// <summary>
        /// Waits for Rive to look the artboard up. You don't need to before using the handle.
        /// </summary>
        /// <returns>An operation that finishes on the main thread with this handle once Rive has found what it refers to. It fails with the lookup's <see cref="RiveException"/> if Rive found nothing, the same one reported when that happened, or with <see cref="RiveErrorCode.ResourceDisposed"/> if it's disposed first. Each call gives its own operation.</returns>
        public Future<ArtboardHandle> ResolveAsync() => m_resolution.Wait(this);

        internal FileHandle File => m_file;

        internal string Name => m_info.Name;

        /// <summary>
        /// Gets the artboard's name.
        /// </summary>
        /// <returns>An operation that finishes on the main thread with the name.</returns>
        public Future<string> GetNameAsync() => Future<string>.FromResult(m_info.Name);

        internal IReadOnlyList<string> StateMachineNames => m_info.StateMachineNames;

        /// <summary>
        /// Gets the names of the artboard's state machines.
        /// </summary>
        /// <returns>An operation that finishes on the main thread with the names.</returns>
        public Future<IReadOnlyList<string>> GetStateMachineNamesAsync() =>
            Future<IReadOnlyList<string>>.FromResult(m_info.StateMachineNames);

        /// <summary>
        /// Makes an instance of one of the artboard's state machines.
        /// </summary>
        /// <param name="name">The state machine's name, or null for the artboard's default.</param>
        /// <returns>The handle, straight away. Null, with a warning, if the artboard has no state machine by that name.</returns>
        public StateMachineHandle InstantiateStateMachine(string name = null)
        {
            if (!CheckUsable(nameof(InstantiateStateMachine)))
            {
                return null;
            }
            int index = name == null
                ? m_info.DefaultStateMachineIndex
                : FileContents.IndexOf(m_info.StateMachineNames, name);
            if (index < 0)
            {
                DebugLogger.Instance.LogWarning(name == null
                    ? $"Artboard '{Name}' has no state machines."
                    : $"Artboard '{Name}' has no state machine named '{name}'. It has: {string.Join(", ", m_info.StateMachineNames)}.");
                return null;
            }
            var native = new NativeSlot<NativeStateMachineHandle>();
            var handle = new StateMachineHandle(
                native, HandleResolution.Pending(), this, m_info.StateMachineNames[index], owned: true,
                StateMachineNative.Lifetime(native, m_lifetime));
            StateMachineNative.InstantiateLater(this, handle, HandleErrors.CaptureCallSite());
            return handle;
        }

        /// <summary>
        /// Gets the view model the artboard uses by default.
        /// </summary>
        /// <returns>The view model, or null if the artboard doesn't have one.</returns>
        public ViewModelHandle GetDefaultViewModel()
        {
            int index = m_info.DefaultViewModelIndex;
            return index >= 0 ? m_file.GetViewModelAt(index) : null;
        }

        /// <summary>
        /// Sets the artboard instance's size. Queued, so it applies before the next advance.
        /// </summary>
        public void SetSize(Size size)
        {
            if (CheckUsable(nameof(SetSize)))
            {
                ArtboardNative.SetSize(m_native.Value, size);
            }
        }

        /// <summary>
        /// Gets the artboard instance's size. Layout and scripting in the file can change it while Rive runs.
        /// </summary>
        /// <returns>An operation that finishes on the main thread with the artboard's width and height.</returns>
        public Future<Size> GetSizeAsync()
        {
            if (!CheckUsable(nameof(GetSizeAsync)))
            {
                return Future<Size>.FromResult(default);
            }
            return ArtboardNative.GetSizeAsync(m_native);
        }

        /// <summary>
        /// True once the handle has been disposed, or its widget has unloaded it.
        /// </summary>
        public bool IsDisposed => m_isDisposed;

        /// <summary>
        /// Releases the artboard instance. Does nothing for a handle a widget owns.
        /// </summary>
        public void Dispose()
        {
            if (m_isDisposed)
            {
                return;
            }
            if (!m_owned)
            {
                DebugLogger.Instance.LogWarning($"Artboard '{Name}' belongs to a widget, which releases it. Dispose was ignored.");
                return;
            }
            Release();
            m_lifetime?.ReleaseOwner();
            GC.SuppressFinalize(this);
        }

        /// For a widget's view, when it unloads, and Dispose.
        internal void Release()
        {
            m_isDisposed = true;
            m_resolution.MarkDisposed($"Artboard '{Name}'");
        }

        private bool CheckUsable(string what)
        {
            if (!m_isDisposed)
            {
                return true;
            }
            DebugLogger.Instance.LogError($"{what}: artboard '{Name}' has been disposed.");
            return false;
        }
    }
}
