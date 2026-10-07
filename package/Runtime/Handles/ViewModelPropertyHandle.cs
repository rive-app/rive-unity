using System;
using System.Collections.Generic;
using Rive.Host;
using Rive.Utils;

namespace Rive
{
    /// <summary>
    /// Represents a Rive ViewModelInstance property, found by its path, for scenarios
    /// where immediate, non-blocking access is required.
    /// </summary>
    /// <remarks>
    /// Returned immediately by the Get…Property methods on <see cref="ViewModelInstanceHandle"/>. Rive checks the path later. That happens the first time it's used, or when you call its ResolveAsync.
    /// </remarks>
    public abstract class ViewModelPropertyHandle : IWatchedValue
    {
        // The instance the path starts from.
        private readonly ViewModelInstanceHandle m_instance;

        private readonly string m_path;

        // Main thread. The resolve has been sent.
        private bool m_resolveSent;

        // The newest change this handle has reported. Set in the drain.
        private ulong m_seenChange = ViewModelNative.NotWatching;

        // Editor only: where user code asked for the property, for reports.
        private readonly string m_callSite;

        // What Subscribe added. Main thread only.
        private Delegate m_callbacks;

        // Set when the resolve's reply lands.
        private readonly HandleResolution m_resolution = HandleResolution.Pending();

        internal ViewModelPropertyHandle(ViewModelInstanceHandle instance, string path)
        {
            m_instance = instance;
            m_path = path;
            m_callSite = HandleErrors.CaptureCallSite();
        }

        /// <summary>
        /// The property's path from <see cref="Instance"/>.
        /// </summary>
        public string Path => m_path;

        /// <summary>
        /// The instance the path starts from.
        /// </summary>
        public ViewModelInstanceHandle Instance => m_instance;

        internal abstract ViewModelDataType Type { get; }

        internal HandleResolution Resolution => m_resolution;

        // Where the lookup is up to. Internal, so scripts don't gate writes on it.
        internal HandleStatus Status => m_resolution.Status;

        // Why the lookup found nothing, or null. Kept after a dispose.
        internal RiveException Error => m_resolution.Error;

        /// Main thread. Checks the path now if nothing has yet.
        internal Future<THandle> Resolve<THandle>(THandle self) where THandle : ViewModelPropertyHandle
        {
            if (m_resolution.Status == HandleStatus.Pending)
            {
                EnsureResolveSent();
            }
            return m_resolution.Wait(self);
        }

        /// Main thread, when the instance is disposed or unloaded.
        internal void MarkDisposed()
        {
            m_resolution.MarkDisposed($"The view model instance for '{m_path}'");
        }

        /// The instance's native handle, filled when its lookup was sent.
        internal NativeViewModelInstanceHandle Root => m_instance.Native.Value;

        /// Main thread. Sends the path check once, before the first thing
        /// that uses the property, so a missing one is reported once.
        internal void EnsureResolveSent()
        {
            if (m_resolveSent)
            {
                return;
            }
            m_resolveSent = true;
            ViewModelInstanceNative.ResolveLater(this);
        }

        /// In the drain, from the resolve's reply. On an instance whose lookup
        /// failed it takes that error without reporting it again.
        internal void OnResolved(bool found, int typeAtPath)
        {
            HandleResolution instance = m_instance.Resolution;
            if (instance.FailedOnRive)
            {
                m_resolution.FailOnRive(instance.RiveError);
                return;
            }
            if (found)
            {
                m_resolution.SucceedOnRive();
                return;
            }
            RiveException missing = Missing(typeAtPath);
            HandleErrors.ReportLater(missing, m_instance.ErrorSink, m_callSite);
            m_resolution.FailOnRive(missing);
        }

        /// Any thread. Sends a problem found after the call returned to the
        /// main thread, where it's logged and passed to the owner.
        internal void ReportLater(RiveErrorCode code, string message, string callSite = null)
        {
            HandleErrors.ReportLater(code, message, m_instance.ErrorSink, callSite ?? m_callSite);
        }

        /// In the drain. Why the path doesn't resolve, once its resolve has
        /// landed.
        internal RiveException Missing()
        {
            return Missing(-1);
        }

        private RiveException Missing(int typeAtPath)
        {
            if (m_resolution.FailedOnRive)
            {
                return m_resolution.RiveError;
            }
            if (m_instance.Resolution.FailedOnRive)
            {
                return m_instance.Resolution.RiveError;
            }
            if (!Root.IsValid)
            {
                return new RiveException(RiveErrorCode.ViewModelInstanceNotFound,
                    $"There's no view model instance to reach '{m_path}' from. A list item, nested or global instance that isn't there when Rive gets to it refers to nothing.");
            }
            string on = m_instance.ViewModelName != null ? $" on view model '{m_instance.ViewModelName}'" : string.Empty;
            if (typeAtPath >= 0)
            {
                return new RiveException(RiveErrorCode.TypeMismatch,
                    $"'{m_path}'{on} is a {(ViewModelDataType)typeAtPath} property, not a {Type} property.");
            }
            return new RiveException(RiveErrorCode.PropertyNotFound, $"There's no property at '{m_path}'{on}.");
        }

        /// Queues work on the property, skipped when it doesn't exist.
        internal void WriteLater(string what, Action<NativeViewModelInstanceHandle> write)
        {
            if (!CheckUsable(what))
            {
                return;
            }
            EnsureResolveSent();
            ViewModelInstanceNative.WriteLater(this, write);
        }

        /// Holds the asset until the queued write has run.
        internal void SetAsset(string what, OutOfBandAsset asset, bool isImage)
        {
            if (!CheckUsable(what) || (asset != null && !asset.Retain()))
            {
                return;
            }
            EnsureResolveSent();
            ViewModelInstanceNative.SetAssetLater(this, asset, isImage);
        }

        internal bool CheckUsable(string what)
        {
            if (!m_instance.IsDisposed)
            {
                return true;
            }
            DebugLogger.Instance.LogError($"{what}: the view model instance for '{m_path}' has been disposed.");
            return false;
        }

        /// Main thread, as a watch starts. Changes from before don't count.
        internal void BeginWatching(ulong token)
        {
            m_seenChange = token;
        }

        /// In the drain, from the start's reply, unless a capture has moved
        /// the stamp on since.
        internal void StartedWatching(ulong sequence, ulong token)
        {
            if (m_seenChange == token)
            {
                m_seenChange = sequence;
            }
        }

        NativeViewModelInstanceHandle IWatchedValue.WatchInstance => Root;

        string IWatchedValue.WatchPath => m_path;

        ViewModelDataType IWatchedValue.WatchType => Type;

        ulong IWatchedValue.SeenChange
        {
            get => m_seenChange;
            set => m_seenChange = value;
        }

        // A capture sent before the instance was let go still counts.
        bool IWatchedValue.Gone => m_instance.IsDisposed && !m_resolveSent;

        /// Main thread, from the values channel.
        internal abstract void RaiseChanged(in PropertyValue value);

        internal Future<T> ReadAsync<T>(string what)
        {
            if (m_instance.IsDisposed)
            {
                var failed = new FutureState<T>();
                failed.Fail(new RiveException(RiveErrorCode.ResourceDisposed,
                    $"{what}: the view model instance for '{m_path}' has been disposed."));
                return new Future<T>(failed);
            }
            EnsureResolveSent();
            return PropertyCallbacksHub.Instance.ReadHandleAsync<T>(this);
        }

        internal Delegate Callbacks => m_callbacks;

        /// Main thread. Adds a callback until the subscription is disposed.
        internal PropertySubscription AddCallback(Delegate callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }
            if (!CheckUsable("Subscribe"))
            {
                return PropertySubscription.Inactive(m_instance);
            }
            m_callbacks = Delegate.Combine(m_callbacks, callback);
            UpdateSubscription(true);
            return new PropertySubscription(m_instance, () =>
            {
                m_callbacks = Delegate.Remove(m_callbacks, callback);
                UpdateSubscription(m_callbacks != null);
            });
        }

        /// Keeps the hub subscription in step with the callbacks.
        private void UpdateSubscription(bool hasCallbacks)
        {
            if (hasCallbacks && !m_instance.IsDisposed)
            {
                if (PropertyCallbacksHub.Instance.RegisterHandle(this))
                {
                    ViewModelInstanceNative.StartWatchingLater(this);
                }
            }
            else if (!hasCallbacks)
            {
                PropertyCallbacksHub.Instance.UnregisterHandle(this);
            }
        }
    }

    /// <summary>
    /// Represents a Rive ViewModelInstance property with a value, for scenarios
    /// where immediate, non-blocking access is required.
    /// </summary>
    /// <typeparam name="T">The value's type.</typeparam>
    public abstract class ViewModelPropertyHandle<T> : ViewModelPropertyHandle, IPropertyValueOf<T>
    {
        internal ViewModelPropertyHandle(ViewModelInstanceHandle instance, string path) : base(instance, path)
        {
        }

        /// <summary>
        /// Calls <paramref name="callback"/> on the main thread after each advance that changed the value, with the new value.
        /// </summary>
        /// <returns>The subscription. Dispose it to stop the callback, for example in <c>OnDisable</c>.</returns>
        public PropertySubscription Subscribe(Action<T> callback)
        {
            return AddCallback(callback);
        }

        /// <summary>
        /// Sets the value. Queued, so it applies before the next advance.
        /// </summary>
        public void SetValue(T value)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            CheckUnseen(value);
#endif
            WriteLater(nameof(SetValue), instance => Write(value));
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private protected virtual void CheckUnseen(T value)
        {
        }
#endif

        /// <summary>
        /// Gets the value.
        /// </summary>
        /// <returns>An operation that finishes on the main thread with the value. It comes back in order with <see cref="Subscribe(Action{T})"/> callbacks, so an older value never follows a newer one. It fails with <see cref="RiveErrorCode.PropertyNotFound"/> when the path doesn't name a property of this type.</returns>
        public Future<T> GetValueAsync()
        {
            return ReadAsync<T>(nameof(GetValueAsync));
        }

        /// Sent as a held write, where Root is filled.
        internal abstract void Write(T value);

        internal override void RaiseChanged(in PropertyValue value)
        {
            (Callbacks as Action<T>)?.Invoke(FromValue(value));
        }

        /// This type's value, from what a read or capture replied with.
        internal abstract T FromValue(in PropertyValue value);

        T IPropertyValueOf<T>.FromValue(in PropertyValue value) => FromValue(value);
    }

    /// <summary>
    /// Represents a Rive number property for scenarios where immediate, non-blocking access is required.
    /// </summary>
    /// <remarks>
    /// Returned immediately by <see cref="ViewModelInstanceHandle.GetNumberProperty(string)"/>.
    /// </remarks>
    public sealed class NumberPropertyHandle : ViewModelPropertyHandle<float>
    {
        /// <summary>
        /// Waits for Rive to check the path, queuing the check if nothing has yet. You don't need to before using the handle.
        /// </summary>
        /// <returns>An operation that finishes on the main thread with this handle once Rive has found the property. It fails with the lookup's <see cref="RiveException"/> if Rive found nothing, the same one reported when that happened, or with <see cref="RiveErrorCode.ResourceDisposed"/> if its instance is disposed first. Each call gives its own operation.</returns>
        public Future<NumberPropertyHandle> ResolveAsync() => Resolve(this);

        internal NumberPropertyHandle(ViewModelInstanceHandle instance, string path) : base(instance, path)
        {
        }

        internal override ViewModelDataType Type => ViewModelDataType.Number;

        internal override float FromValue(in PropertyValue value) => value.Number;

        internal override void Write(float value)
        {
            ViewModelInstanceNative.Set(this, value, 0, null);
        }
    }

    /// <summary>
    /// Represents a Rive string property for scenarios where immediate, non-blocking access is required.
    /// </summary>
    /// <remarks>
    /// Returned immediately by <see cref="ViewModelInstanceHandle.GetStringProperty(string)"/>.
    /// </remarks>
    public sealed class StringPropertyHandle : ViewModelPropertyHandle<string>
    {
        /// <summary>
        /// Waits for Rive to check the path, queuing the check if nothing has yet. You don't need to before using the handle.
        /// </summary>
        /// <returns>An operation that finishes on the main thread with this handle once Rive has found the property. It fails with the lookup's <see cref="RiveException"/> if Rive found nothing, the same one reported when that happened, or with <see cref="RiveErrorCode.ResourceDisposed"/> if its instance is disposed first. Each call gives its own operation.</returns>
        public Future<StringPropertyHandle> ResolveAsync() => Resolve(this);

        internal StringPropertyHandle(ViewModelInstanceHandle instance, string path) : base(instance, path)
        {
        }

        internal override ViewModelDataType Type => ViewModelDataType.String;

        internal override string FromValue(in PropertyValue value) => value.Text;

        internal override void Write(string value)
        {
            ViewModelInstanceNative.Set(this, 0f, 0, value ?? string.Empty);
        }
    }

    /// <summary>
    /// Represents a Rive boolean property for scenarios where immediate, non-blocking access is required.
    /// </summary>
    /// <remarks>
    /// Returned immediately by <see cref="ViewModelInstanceHandle.GetBooleanProperty(string)"/>.
    /// </remarks>
    public sealed class BooleanPropertyHandle : ViewModelPropertyHandle<bool>
    {
        /// <summary>
        /// Waits for Rive to check the path, queuing the check if nothing has yet. You don't need to before using the handle.
        /// </summary>
        /// <returns>An operation that finishes on the main thread with this handle once Rive has found the property. It fails with the lookup's <see cref="RiveException"/> if Rive found nothing, the same one reported when that happened, or with <see cref="RiveErrorCode.ResourceDisposed"/> if its instance is disposed first. Each call gives its own operation.</returns>
        public Future<BooleanPropertyHandle> ResolveAsync() => Resolve(this);

        internal BooleanPropertyHandle(ViewModelInstanceHandle instance, string path) : base(instance, path)
        {
        }

        internal override ViewModelDataType Type => ViewModelDataType.Boolean;

        internal override bool FromValue(in PropertyValue value) => value.Bits != 0;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private UnseenBoolCheck m_unseen;

        private protected override void CheckUnseen(bool value) => m_unseen.Write(value, Path);
#endif

        internal override void Write(bool value)
        {
            ViewModelInstanceNative.Set(this, 0f, value ? 1 : 0, null);
        }
    }

    /// <summary>
    /// Represents a Rive color property for scenarios where immediate, non-blocking access is required.
    /// </summary>
    /// <remarks>
    /// Returned immediately by <see cref="ViewModelInstanceHandle.GetColorProperty(string)"/>.
    /// </remarks>
    public sealed class ColorPropertyHandle : ViewModelPropertyHandle<UnityEngine.Color>
    {
        /// <summary>
        /// Waits for Rive to check the path, queuing the check if nothing has yet. You don't need to before using the handle.
        /// </summary>
        /// <returns>An operation that finishes on the main thread with this handle once Rive has found the property. It fails with the lookup's <see cref="RiveException"/> if Rive found nothing, the same one reported when that happened, or with <see cref="RiveErrorCode.ResourceDisposed"/> if its instance is disposed first. Each call gives its own operation.</returns>
        public Future<ColorPropertyHandle> ResolveAsync() => Resolve(this);

        internal ColorPropertyHandle(ViewModelInstanceHandle instance, string path) : base(instance, path)
        {
        }

        internal override ViewModelDataType Type => ViewModelDataType.Color;

        internal override UnityEngine.Color FromValue(in PropertyValue value) =>
            ViewModelInstanceColorProperty.ArgbToColor((int)value.Bits);

        internal override void Write(UnityEngine.Color value)
        {
            ViewModelInstanceNative.Set(this, 0f, ViewModelInstanceColorProperty.ColorToArgb(value), null);
        }
    }

    /// <summary>
    /// Represents a Rive enum property for scenarios where immediate, non-blocking access is required.
    /// </summary>
    /// <remarks>
    /// Returned immediately by <see cref="ViewModelInstanceHandle.GetEnumProperty(string)"/>. Values are the enum's value names.
    /// </remarks>
    public sealed class EnumPropertyHandle : ViewModelPropertyHandle<string>
    {
        /// <summary>
        /// Waits for Rive to check the path, queuing the check if nothing has yet. You don't need to before using the handle.
        /// </summary>
        /// <returns>An operation that finishes on the main thread with this handle once Rive has found the property. It fails with the lookup's <see cref="RiveException"/> if Rive found nothing, the same one reported when that happened, or with <see cref="RiveErrorCode.ResourceDisposed"/> if its instance is disposed first. Each call gives its own operation.</returns>
        public Future<EnumPropertyHandle> ResolveAsync() => Resolve(this);

        internal EnumPropertyHandle(ViewModelInstanceHandle instance, string path) : base(instance, path)
        {
        }

        /// <summary>
        /// Gets the enum's value names.
        /// </summary>
        /// <returns>An operation that finishes on the main thread with the names. It fails like <see cref="ViewModelPropertyHandle{T}.GetValueAsync"/> when the path doesn't name an enum property.</returns>
        public Future<IReadOnlyList<string>> GetEnumValuesAsync()
        {
            // Known from the file unless the instance's view model isn't, as for a list item.
            IReadOnlyList<string> known = Instance.Contents?.EnumValuesAt(Instance.ViewModelIndex, Path);
            if (known != null && !Instance.IsDisposed)
            {
                return Future<IReadOnlyList<string>>.FromResult(known);
            }
            return ViewModelInstanceNative.GetEnumValuesAsync(this);
        }

        internal override ViewModelDataType Type => ViewModelDataType.Enum;

        internal override string FromValue(in PropertyValue value) => value.Text;

        internal override void Write(string value)
        {
            // A null value would mean an index natively.
            ViewModelInstanceNative.Set(this, 0f, 0, value ?? string.Empty);
        }
    }

    /// <summary>
    /// Represents a Rive trigger property for scenarios where immediate, non-blocking access is required.
    /// </summary>
    /// <remarks>
    /// Returned immediately by <see cref="ViewModelInstanceHandle.GetTriggerProperty(string)"/>.
    /// </remarks>
    public sealed class TriggerPropertyHandle : ViewModelPropertyHandle
    {
        /// <summary>
        /// Waits for Rive to check the path, queuing the check if nothing has yet. You don't need to before using the handle.
        /// </summary>
        /// <returns>An operation that finishes on the main thread with this handle once Rive has found the property. It fails with the lookup's <see cref="RiveException"/> if Rive found nothing, the same one reported when that happened, or with <see cref="RiveErrorCode.ResourceDisposed"/> if its instance is disposed first. Each call gives its own operation.</returns>
        public Future<TriggerPropertyHandle> ResolveAsync() => Resolve(this);

        internal TriggerPropertyHandle(ViewModelInstanceHandle instance, string path) : base(instance, path)
        {
        }

        internal override ViewModelDataType Type => ViewModelDataType.Trigger;

        /// <summary>
        /// Calls <paramref name="callback"/> on the main thread after each advance in which the trigger fired.
        /// </summary>
        /// <returns>The subscription. Dispose it to stop the callback, for example in <c>OnDisable</c>.</returns>
        public PropertySubscription Subscribe(Action callback)
        {
            return AddCallback(callback);
        }

        /// <summary>
        /// Fires the trigger. Queued, so it applies before the next advance.
        /// </summary>
        public void Fire()
        {
            WriteLater(nameof(Fire), instance => ViewModelInstanceNative.Set(this, 0f, 0, null));
        }

        internal override void RaiseChanged(in PropertyValue value)
        {
            (Callbacks as Action)?.Invoke();
        }
    }
}
