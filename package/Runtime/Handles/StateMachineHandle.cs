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
    /// Returned immediately by <see cref="ArtboardHandle.InstantiateStateMachine(string)"/>, before Rive has made it. You can use it straight away. What you queue on it runs after that. A widget's state machine already exists.
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
            NativeSlot<NativeStateMachineHandle> native, HandleResolution resolution, ArtboardHandle artboard, string name, bool owned, NativeLifetime lifetime,
            GamepadTracker gamepads = null)
        {
            m_native = native;
            m_resolution = resolution;
            m_artboard = artboard;
            m_name = name;
            m_owned = owned;
            m_lifetime = lifetime;
            m_gamepads = gamepads ?? new GamepadTracker();
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
        /// Waits for Rive to make the state machine. You don't have to wait for this before using the handle. Anything you do with it is queued and runs once Rive has made it.
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
        /// Whether anything in this state machine can take focus, even if nothing has it yet.
        /// </summary>
        /// <returns>An operation that finishes on the main thread with the answer, in order with other writes.</returns>
        public Future<bool> HasFocusNodesAsync() => FocusAsync(nameof(HasFocusNodesAsync), StateMachineNative.FocusOp.Query, s_hasFocusNodes);

        /// <summary>
        /// Whether something in this state machine has focus.
        /// </summary>
        /// <returns>An operation that finishes on the main thread with the answer, in order with other writes.</returns>
        public Future<bool> HasFocusAsync() => FocusAsync(nameof(HasFocusAsync), StateMachineNative.FocusOp.Query, s_hasFocus);

        /// <summary>
        /// Moves focus to the next focusable node in tree order. Queued, in order with other writes.
        /// </summary>
        /// <returns>An operation that finishes on the main thread with true if focus moved, including into the tree. False at the end, or at an edge that stops.</returns>
        public Future<bool> FocusNextAsync() => FocusAsync(nameof(FocusNextAsync), StateMachineNative.FocusOp.Next, s_moved);

        /// <summary>
        /// Moves focus to the previous focusable node in tree order. Queued, in order with other writes.
        /// </summary>
        /// <returns>An operation that finishes on the main thread with true if focus moved, including into the tree. False at the start, or at an edge that stops.</returns>
        public Future<bool> FocusPreviousAsync() => FocusAsync(nameof(FocusPreviousAsync), StateMachineNative.FocusOp.Previous, s_moved);

        /// <summary>
        /// Moves focus to the nearest focusable node to the left. Does nothing when nothing has focus. Queued, in order with other writes.
        /// </summary>
        /// <returns>An operation that finishes on the main thread with true if focus moved.</returns>
        public Future<bool> FocusLeftAsync() => FocusAsync(nameof(FocusLeftAsync), StateMachineNative.FocusOp.Left, s_moved);

        /// <summary>
        /// Moves focus to the nearest focusable node to the right. Does nothing when nothing has focus. Queued, in order with other writes.
        /// </summary>
        /// <returns>An operation that finishes on the main thread with true if focus moved.</returns>
        public Future<bool> FocusRightAsync() => FocusAsync(nameof(FocusRightAsync), StateMachineNative.FocusOp.Right, s_moved);

        /// <summary>
        /// Moves focus to the nearest focusable node above. Does nothing when nothing has focus. Queued, in order with other writes.
        /// </summary>
        /// <returns>An operation that finishes on the main thread with true if focus moved.</returns>
        public Future<bool> FocusUpAsync() => FocusAsync(nameof(FocusUpAsync), StateMachineNative.FocusOp.Up, s_moved);

        /// <summary>
        /// Moves focus to the nearest focusable node below. Does nothing when nothing has focus. Queued, in order with other writes.
        /// </summary>
        /// <returns>An operation that finishes on the main thread with true if focus moved.</returns>
        public Future<bool> FocusDownAsync() => FocusAsync(nameof(FocusDownAsync), StateMachineNative.FocusOp.Down, s_moved);

        /// <summary>
        /// Drops focus from this state machine, which runs the file's blur behavior. Queued, in order with other writes.
        /// </summary>
        public void ClearFocus()
        {
            if (!CheckUsable(nameof(ClearFocus)))
            {
                return;
            }
            StateMachineNative.FocusAsync(m_native.Value, StateMachineNative.FocusOp.Clear, s_moved);
        }

        /// <summary>
        /// Whether the focused node takes typed text, like a text input.
        /// </summary>
        /// <returns>An operation that finishes on the main thread with the answer, in order with other writes. False when nothing has focus.</returns>
        public Future<bool> ExpectsKeyboardInputAsync() => FocusAsync(nameof(ExpectsKeyboardInputAsync), StateMachineNative.FocusOp.Query, s_expectsKeyboardInput);

        /// <summary>
        /// Reads the text selected in the focused node, to copy to the clipboard.
        /// </summary>
        /// <returns>An operation that finishes on the main thread with the selection, in order with other writes. Empty when nothing is selected.</returns>
        public Future<string> SelectedTextAsync()
        {
            if (!CheckUsable(nameof(SelectedTextAsync)))
            {
                return Future<string>.FromResult(string.Empty);
            }
            return StateMachineNative.FocusAsync(
                m_native.Value, StateMachineNative.FocusOp.Query, s_selectedText, StateMachineNative.FocusReadSelection);
        }

        /// <summary>
        /// Sends a key press or release to the focused node, then to its ancestors until one handles it. Queued, in order with other writes.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="modifiers">The modifier keys held with it.</param>
        /// <param name="pressed">True when the key goes down, false when it comes up.</param>
        /// <param name="repeat">True for a repeat while the key is held.</param>
        /// <returns>An operation that finishes on the main thread with true if something handled the key.</returns>
        public Future<bool> KeyInputAsync(RiveKey key, RiveKeyModifiers modifiers, bool pressed, bool repeat = false)
        {
            if (!CheckUsable(nameof(KeyInputAsync)))
            {
                return Future<bool>.FromResult(false);
            }
            return StateMachineNative.KeyAsync(m_native.Value, key, modifiers, pressed, repeat, s_claimed);
        }

        /// <summary>
        /// Types text into the focused node, when it takes text. Send keys like Backspace and Enter with <see cref="KeyInputAsync"/>. Queued, in order with other writes.
        /// </summary>
        /// <param name="text">The committed text.</param>
        /// <returns>An operation that finishes on the main thread with true if something took the text.</returns>
        public Future<bool> TextInputAsync(string text)
        {
            if (string.IsNullOrEmpty(text) || !CheckUsable(nameof(TextInputAsync)))
            {
                return Future<bool>.FromResult(false);
            }
            return StateMachineNative.TextAsync(m_native.Value, text, s_claimed);
        }

        /// <summary>
        /// Sets how far a gamepad button is pressed. Listeners hear it through the focused node and its ancestors, and scripts with a gamepad handler hear it whatever has focus. Only sent when the value changed. Queued, in order with other writes.
        /// </summary>
        /// <param name="deviceId">Any number that stays the same for one gamepad. The first value from a new one announces it as connected.</param>
        /// <param name="button">The button.</param>
        /// <param name="value">From 0, released, to 1, fully pressed. Anything from 0.5 counts as pressed.</param>
        public void SetGamepadButton(int deviceId, RiveGamepadButton button, float value)
        {
            if (CheckUsable(nameof(SetGamepadButton)))
            {
                StateMachineNative.SendGamepad(m_native.Value, m_gamepads.SetButton(deviceId, button, value));
            }
        }

        /// <summary>
        /// Sets a gamepad axis. Heard the same way as <see cref="SetGamepadButton"/>. Queued, in order with other writes.
        /// </summary>
        /// <param name="deviceId">Any number that stays the same for one gamepad. The first value from a new one announces it as connected.</param>
        /// <param name="axis">The axis.</param>
        /// <param name="value">From -1 to 1 for a stick, with up and left negative, or 0 to 1 for a trigger.</param>
        public void SetGamepadAxis(int deviceId, RiveGamepadAxis axis, float value)
        {
            if (CheckUsable(nameof(SetGamepadAxis)))
            {
                StateMachineNative.SendGamepad(m_native.Value, m_gamepads.SetAxis(deviceId, axis, value));
            }
        }

        /// <summary>
        /// Tells listeners a gamepad went away. Does nothing for one this state machine hasn't heard from. Queued, in order with other writes.
        /// </summary>
        /// <param name="deviceId">The number its values were sent with.</param>
        public void DisconnectGamepad(int deviceId)
        {
            if (CheckUsable(nameof(DisconnectGamepad)))
            {
                StateMachineNative.SendGamepad(m_native.Value, m_gamepads.Disconnect(deviceId));
            }
        }

        // Shared with the StateMachine a widget's handle views, so both know what was already sent.
        private readonly GamepadTracker m_gamepads;

        internal GamepadTracker Gamepads => m_gamepads;

        private static readonly Func<StateMachineNative.FocusResult, bool> s_moved = result => result.Moved;
        private static readonly Func<StateMachineNative.FocusResult, bool> s_claimed = result => result.Claimed;
        private static readonly Func<StateMachineNative.FocusResult, bool> s_expectsKeyboardInput = result => result.State.ExpectsKeyboardInput;
        private static readonly Func<StateMachineNative.FocusResult, string> s_selectedText = result => result.SelectedText ?? string.Empty;
        private static readonly Func<StateMachineNative.FocusResult, bool> s_hasFocus = result => result.State.HasFocus;
        private static readonly Func<StateMachineNative.FocusResult, bool> s_hasFocusNodes = result => result.State.HasFocusNodes;

        private Future<bool> FocusAsync(string what, StateMachineNative.FocusOp op, Func<StateMachineNative.FocusResult, bool> pick)
        {
            if (!CheckUsable(what))
            {
                return Future<bool>.FromResult(false);
            }
            return StateMachineNative.FocusAsync(m_native.Value, op, pick);
        }

        /// <summary>
        /// Gets the view model instance bound as the state machine's data context.
        /// </summary>
        /// <returns>The handle, straight away, before Rive has looked it up. It refers to the instance bound when Rive gets to it, after the binds queued before it, and keeps referring to that one if another is bound later. If none is bound then, it refers to nothing. Work queued on it is skipped, and that's reported once. Writes to it made in a widget's load callback land before the widget's first advance.</returns>
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
            // Normally the artboard's view model. Only the value tracker uses this.
            handle.SetLikelyViewModel(m_artboard.DefaultViewModelIndex);
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
        /// <returns>An operation that finishes on the main thread once Rive has done the bind. It fails with a <see cref="RiveException"/> if any entry wasn't assigned, even though the rest were. The code is <see cref="RiveErrorCode.ViewModelNotFound"/> for a name that isn't a global in the file, <see cref="RiveErrorCode.ResourceDisposed"/> for a disposed instance, and <see cref="RiveErrorCode.ViewModelInstanceNotFound"/> for one that's null or refers to nothing when Rive gets to it. With more than one, the message lists them all and the code is the first one's. Each is also reported to a widget's <c>OnError</c>.</returns>
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
        /// <returns>The handle, straight away, before Rive has looked it up. It refers to the instance bound when Rive gets to it, after the binds queued before it. If none is bound then, it refers to nothing. Work queued on it is skipped, and that's reported once. Null, with a warning, if the file has no global by that name.</returns>
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
