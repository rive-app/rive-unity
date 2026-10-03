using System;
using System.Collections.Generic;
using Rive.Utils;

namespace Rive
{
    /// <summary>
    /// Represents a Rive StateMachine instance created from an Artboard for scenarios 
    /// where immediate, non-blocking access is required.
    /// </summary>
    /// <remarks>
    /// Returned immediately by <see cref="ArtboardHandle.InstantiateStateMachine(string)"/>, before Rive has made it. Use it straight away: what you queue on it runs after that. A widget's state machine already exists.
    /// </remarks>
    public sealed class StateMachineHandle : IDisposable
    {
        // Native state machine slot.
        private readonly NativeSlot<NativeStateMachineHandle> m_native;

        // The artboard from which this state machine instance originated.
        private readonly ArtboardHandle m_artboard;

        private readonly string m_name;

        // Whether this handle instance is responsible for releasing the native resource.
        private readonly bool m_owned;

        private readonly HandleResolution m_resolution;

        // Released by this handle only when it owns the state machine.
        private readonly NativeLifetime m_lifetime;

        private bool m_isDisposed;

        internal StateMachineHandle(
            NativeSlot<NativeStateMachineHandle> native, HandleResolution resolution, ArtboardHandle artboard, string name, bool owned, NativeLifetime lifetime)
        {
            m_native = native;
            m_resolution = resolution;
            m_artboard = artboard;
            m_name = name;
            m_owned = owned;
            m_lifetime = lifetime;
        }

        ~StateMachineHandle()
        {
            if (m_owned && !m_isDisposed)
            {
                m_lifetime?.ReleaseOwner();
            }
        }

        internal NativeSlot<NativeStateMachineHandle> Native => m_native;

        internal HandleResolution Resolution => m_resolution;

        // Where the lookup is up to. Internal, so scripts don't gate writes on it.
        internal HandleStatus Status => m_resolution.Status;

        // Why the lookup found nothing, or null. Kept after a dispose.
        internal RiveException Error => m_resolution.Error;

        /// <summary>
        /// Waits for Rive to make the state machine. You don't need to before using the handle.
        /// </summary>
        /// <returns>An operation that finishes on the main thread with this handle once Rive has found what it refers to. It fails with the lookup's <see cref="RiveException"/> if Rive found nothing, the same one reported when that happened, or with <see cref="RiveErrorCode.ResourceDisposed"/> if it's disposed first. Each call gives its own operation.</returns>
        public Future<StateMachineHandle> ResolveAsync() => m_resolution.Wait(this);

        /// Where problems found later go, besides the log. Instances this
        /// hands out inherit it.
        internal Action<RiveException> ErrorSink { get; set; }

        internal string Name => m_name;

        /// <summary>
        /// Gets the state machine's name.
        /// </summary>
        /// <returns>An operation that finishes on the main thread with the name.</returns>
        public Future<string> GetNameAsync() => Future<string>.FromResult(m_name);

        /// <summary>
        /// The artboard the state machine was made from.
        /// </summary>
        public ArtboardHandle Artboard => m_artboard;

        /// <summary>
        /// Advances the state machine. Queued, in order with other writes.
        /// </summary>
        /// <param name="seconds">How far to advance, in seconds.</param>
        /// <remarks>
        /// A widget advances its own state machine every frame. This is for state machines you drive yourself.
        /// </remarks>
        public void Advance(float seconds)
        {
            if (!CheckUsable(nameof(Advance)))
            {
                return;
            }
            StateMachineNative.AdvanceLater(m_native.Value, seconds);
            // No panel captures for a state machine you drive yourself.
            PropertyCallbacksHub.Instance.SubmitCaptureAfterAdvance();
        }

        /// <summary>
        /// Gets the view model instance bound as the state machine's data context.
        /// </summary>
        /// <returns>The handle, straight away, before Rive has looked it up. It refers to the instance bound when Rive gets to it, after the binds queued before it, and keeps referring to that one if another is bound later. If none is bound then, it refers to nothing: work queued on it is skipped, and that's reported once. Writes to it made in a widget's load callback land before the widget's first advance.</returns>
        public ViewModelInstanceHandle GetViewModelInstance()
        {
            if (!CheckUsable(nameof(GetViewModelInstance)))
            {
                return null;
            }
            // Any view model's instance can be bound, so which one isn't known until Rive looks.
            var handle = new ViewModelInstanceHandle(
                new NativeSlot<NativeViewModelInstanceHandle>(), HandleResolution.Pending(), m_artboard.File.Contents, -1, owned: true)
            {
                ErrorSink = ErrorSink
            };
            ViewModelInstanceNative.GetMainLater(this, handle, HandleErrors.CaptureCallSite());
            return handle;
        }

        /// <summary>
        /// Binds a view model instance as the state machine's data context. Queued straight away, in order with other writes.
        /// </summary>
        /// <param name="instance">The instance to bind, which the state machine keeps alive while bound. Or null to keep the instance it has, or bind an instance of the artboard's default view model if it has none, and fill in the file's globals.</param>
        /// <returns>An operation that finishes on the main thread once Rive has done the bind. It fails with a <see cref="RiveException"/> if the instance is disposed or refers to nothing, and the problem is also reported to a widget's <c>OnError</c>. Use <see cref="GetViewModelInstance"/> to see what's bound.</returns>
        public Future BindViewModelInstanceAsync(ViewModelInstanceHandle instance)
        {
            if (m_isDisposed)
            {
                return Reject(RiveErrorCode.ResourceDisposed, $"state machine '{Name}' has been disposed.");
            }
            if (instance != null && instance.IsDisposed)
            {
                return Reject(RiveErrorCode.ResourceDisposed, "the instance is disposed.");
            }
            return ViewModelInstanceNative.BindAsync(
                this, instance, Array.Empty<string>(), Array.Empty<ViewModelInstanceHandle>(), null, HandleErrors.CaptureCallSite());
        }

        /// <summary>
        /// Binds a main view model instance together with global view model instances. Queued straight away, in order with other writes, so it lands before the next advance.
        /// </summary>
        /// <remarks>
        /// The globals are assigned in order, then the main instance, and the state machine is bound once with what was assigned. An entry that can't be assigned is skipped and reported, and the others still apply, so a failed bind may have changed some things. Globals you leave out get default instances. If you rebind later and leave one out, it keeps the instance it has. Use <see cref="GetViewModelInstance"/> and <see cref="GetGlobalViewModelInstance(string)"/> to see what's bound.
        /// </remarks>
        /// <param name="main">The main instance, or null to keep the current one (or get a default one).</param>
        /// <param name="globals">Instances by global view model name, from <see cref="FileHandle.GetGlobalViewModelNamesAsync"/>.</param>
        /// <returns>An operation that finishes on the main thread once Rive has done the bind. It fails with a <see cref="RiveException"/> if any entry wasn't assigned, even though the rest were: <see cref="RiveErrorCode.ViewModelNotFound"/> for a name that isn't a global in the file, <see cref="RiveErrorCode.ResourceDisposed"/> for a disposed instance, and <see cref="RiveErrorCode.ViewModelInstanceNotFound"/> for one that's null or refers to nothing when Rive gets to it. With more than one, the message lists them all and the code is the first one's. Each is also reported to a widget's <c>OnError</c>.</returns>
        public Future BindViewModelInstanceAsync(
            ViewModelInstanceHandle main, IReadOnlyDictionary<string, ViewModelInstanceHandle> globals)
        {
            if (m_isDisposed)
            {
                return Reject(RiveErrorCode.ResourceDisposed, $"state machine '{Name}' has been disposed.");
            }
            string callSite = HandleErrors.CaptureCallSite();
            List<RiveException> rejected = null;
            if (main != null && main.IsDisposed)
            {
                Note(ref rejected, RiveErrorCode.ResourceDisposed, "the main instance is disposed, so it wasn't assigned.");
                main = null;
            }
            IReadOnlyList<string> available = m_artboard.File.GlobalViewModelNames;
            var names = new List<string>();
            var instances = new List<ViewModelInstanceHandle>();
            if (globals != null)
            {
                foreach (KeyValuePair<string, ViewModelInstanceHandle> entry in globals)
                {
                    if (!ContainsName(available, entry.Key))
                    {
                        Note(ref rejected, RiveErrorCode.ViewModelNotFound,
                            $"'{entry.Key}' isn't a global view model in the file, so it wasn't assigned. It has: {string.Join(", ", available)}.");
                    }
                    else if (entry.Value == null)
                    {
                        Note(ref rejected, RiveErrorCode.ViewModelInstanceNotFound, $"the instance for global '{entry.Key}' is null, so it wasn't assigned.");
                    }
                    else if (entry.Value.IsDisposed)
                    {
                        Note(ref rejected, RiveErrorCode.ResourceDisposed, $"the instance for global '{entry.Key}' is disposed, so it wasn't assigned.");
                    }
                    else
                    {
                        names.Add(entry.Key);
                        instances.Add(entry.Value);
                    }
                }
            }
            return ViewModelInstanceNative.BindAsync(this, main, names.ToArray(), instances.ToArray(), rejected, callSite);
        }

        // A problem found at the call: reported now, and the bind's Future fails with it too.
        private void Note(ref List<RiveException> rejected, RiveErrorCode code, string problem)
        {
            var error = new RiveException(code, $"BindViewModelInstanceAsync: {problem}");
            HandleErrors.ReportNow(error, ErrorSink);
            if (rejected == null)
            {
                rejected = new List<RiveException>();
            }
            rejected.Add(error);
        }

        // A bind that fails at the call: nothing is queued.
        private Future Reject(RiveErrorCode code, string problem)
        {
            var error = new RiveException(code, $"BindViewModelInstanceAsync: {problem} Nothing was bound.");
            HandleErrors.ReportNow(error, ErrorSink);
            var state = new FutureState<bool>();
            state.Fail(error);
            return new Future(state);
        }

        /// <summary>
        /// Gets the global view model instance the state machine has bound under a name.
        /// </summary>
        /// <param name="name">The global view model's name, from <see cref="FileHandle.GetGlobalViewModelNamesAsync"/>.</param>
        /// <returns>The handle, straight away, before Rive has looked it up. It refers to the instance bound when Rive gets to it, after the binds queued before it. If none is bound then, it refers to nothing: work queued on it is skipped, and that's reported once. Null, with a warning, if the file has no global by that name.</returns>
        public ViewModelInstanceHandle GetGlobalViewModelInstance(string name)
        {
            if (!CheckUsable(nameof(GetGlobalViewModelInstance)))
            {
                return null;
            }
            IReadOnlyList<string> available = m_artboard.File.GlobalViewModelNames;
            if (!ContainsName(available, name))
            {
                DebugLogger.Instance.LogWarning(
                    $"The file has no global view model named '{name}'. It has: {string.Join(", ", available)}.");
                return null;
            }
            // A global's slot can hold another view model's instance, so which one isn't known until Rive looks.
            var handle = new ViewModelInstanceHandle(
                new NativeSlot<NativeViewModelInstanceHandle>(), HandleResolution.Pending(), m_artboard.File.Contents, -1, owned: true)
            {
                ErrorSink = ErrorSink
            };
            ViewModelInstanceNative.GetGlobalLater(this, name, handle, HandleErrors.CaptureCallSite());
            return handle;
        }

        private static bool ContainsName(IReadOnlyList<string> names, string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }
            for (int i = 0; i < names.Count; i++)
            {
                if (names[i] == name)
                {
                    return true;
                }
            }
            return false;
        }

        private bool CheckUsable(string what)
        {
            if (!m_isDisposed)
            {
                return true;
            }
            DebugLogger.Instance.LogError($"{what}: state machine '{Name}' has been disposed.");
            return false;
        }

        /// <summary>
        /// True once the handle has been disposed, or its widget has unloaded it.
        /// </summary>
        public bool IsDisposed => m_isDisposed;

        /// <summary>
        /// Releases the state machine instance. Does nothing for a handle a widget owns.
        /// </summary>
        public void Dispose()
        {
            if (m_isDisposed)
            {
                return;
            }
            if (!m_owned)
            {
                DebugLogger.Instance.LogWarning($"State machine '{Name}' belongs to a widget, which releases it. Dispose was ignored.");
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
            m_resolution.MarkDisposed($"State machine '{Name}'");
        }
    }
}
