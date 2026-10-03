using System;
using System.Threading.Tasks;
using Rive.Producer;
using Rive.Utils;
using Rive.Host;

namespace Rive
{
    /// <summary>
    /// Base class for all primitive properties of a ViewModelInstance. This is usually used for types like numbers, strings, etc. that have a value change event.
    /// </summary>
    public abstract class ViewModelInstancePrimitiveProperty : ViewModelInstanceProperty, IWatchedValue
    {
        private static long s_nextCallbackKey;

        private readonly ViewModelInstance m_instance;
        private readonly string m_name;
        private readonly int m_slot;

        /// <summary>
        /// Unique per property object. What the callbacks hub keys on.
        /// </summary>
        internal readonly long CallbackKey = System.Threading.Interlocked.Increment(ref s_nextCallbackKey);

        /// <summary>
        /// The instance this property belongs to.
        /// </summary>
        internal ViewModelInstance RootInstance => m_instance;

        /// <summary>
        /// The property's name on <see cref="RootInstance"/>. One level, never a path.
        /// </summary>
        internal string Name => m_name;

        /// <summary>
        /// Where native keeps this property on its instance. Calls pass this rather than the name.
        /// </summary>
        internal int Slot => m_slot;

        /// <summary>
        /// False for a property that was never found natively. Every read and write on it does nothing.
        /// </summary>
        internal bool IsAttached => m_instance != null && m_slot >= 0;

        internal NativeViewModelInstanceHandle InstanceHandle => m_instance != null ? m_instance.NativeHandle : default;

        /// <summary>
        /// Throws <see cref="ObjectDisposedException"/> if the owning <see cref="ViewModelInstance"/> has been disposed.
        /// </summary>
        protected void ThrowIfOwnerDisposed()
        {
            if (m_instance != null && m_instance.IsDisposed)
            {
                throw new ObjectDisposedException(
                    nameof(ViewModelInstance),
                    $"Cannot access {GetType().Name}: the owning ViewModelInstance has been disposed.");
            }
        }

        // The newest change this property has reported. Other subscribers to
        // the same value keep their own, so nobody clears another's change.
        // Set in the drain, or when callbacks start.
        private ulong m_seenChange = ViewModelNative.NotWatching;

        /// Set by the factory that made it.
        internal ViewModelDataType PropertyType { get; set; }

        /// Waits for the value. fallback when the property isn't there.
        private protected T ReadNative<T>(ViewModelNative.ValueReader<T> read, T fallback)
        {
            return ViewModelNative.Read(InstanceHandle, m_name, PropertyType, read, fallback);
        }

        /// Held, and sent in order ahead of the next thing that's sent.
        private protected void WriteNative(float number, int integer, string text)
        {
            ViewModelNative.Set(InstanceHandle, m_name, PropertyType, number, integer, text);
        }

        NativeViewModelInstanceHandle IWatchedValue.WatchInstance => InstanceHandle;

        string IWatchedValue.WatchPath => m_name;

        ViewModelDataType IWatchedValue.WatchType => PropertyType;

        ulong IWatchedValue.SeenChange
        {
            get => m_seenChange;
            set => m_seenChange = value;
        }

        bool IWatchedValue.Gone => !IsAttached || (m_instance != null && m_instance.IsDisposed);

        /// <summary>
        /// Whether the value has changed since this property last reported a change.
        /// </summary>
        internal bool HasChanged => IsAttached && ViewModelNative.ChangedSince(InstanceHandle, m_name, PropertyType, m_seenChange);

        /// <param name="instance"> The instance this property belongs to.</param>
        /// <param name="name"> The property's name on that instance.</param>
        /// <param name="slot"> Where native keeps it.</param>
        internal ViewModelInstancePrimitiveProperty(ViewModelInstance instance, string name, int slot)
        {
            m_instance = instance;
            m_name = name;
            m_slot = slot;
        }

        /// <summary>
        /// Counts every change so far as seen by this property.
        /// </summary>
        internal void ClearChanges()
        {
            if (IsAttached)
            {
                m_seenChange = ViewModelNative.ChangeSequence();
            }
        }

        /// <summary>
        /// Called by ViewModelInstance when it detects this property has changed.
        /// </summary>
        internal virtual void RaiseChangedEvent()
        {
            // no-op in non-generic base; override in subclasses
        }

        /// With the value the capture saw. Types without a value ignore it.
        internal virtual void RaiseChangedEvent(in PropertyValue capturedValue)
        {
            RaiseChangedEvent();
        }

        /// <summary>
        /// Clears this property's callbacks and performs the normal cleanup path.
        /// Use this when the property is unsubscribing itself, because it also tells
        /// the owning <see cref="ViewModelInstance"/> to unregister the property.
        /// </summary>
        internal virtual void ClearAllCallbacks()
        {
            // If we've removed all subscribers, unregister
            m_instance?.UnregisterPropertyForCallbacks(this);
        }

        /// <summary>
        /// Clears only this property's stored callback delegates.
        /// Unlike <see cref="ClearAllCallbacks"/>, this does not unregister the property
        /// from its owning <see cref="ViewModelInstance"/>.
        /// This is used by <see cref="ViewModelInstance.ClearCallbacks"/> because that
        /// method is already walking the subscribed properties and handling hub cleanup
        /// itself. Calling the full unregister path there would change the collection
        /// while it is being looped over.
        /// </summary>
        internal virtual void ClearDelegatesOnly()
        {
        }

        internal void RegisterForCallbacks()
        {

            // Changes from before the first listener don't count.
            ClearChanges();
            m_instance?.RegisterPropertyForCallbacks(this);
        }

        internal void UnregisterForCallbacks()
        {
            m_instance?.UnregisterPropertyForCallbacks(this);
        }

        // Helpers to add/remove managed callbacks and register/unregister native notifications
        /// <summary>
        /// Adds a callback handler and, if first subscriber, clears stale changes and registers native callbacks.
        /// </summary>
        /// <param name="handler">The callback to invoke when this property changes.</param>
        /// <param name="backingField">Reference to the private delegate field storing subscribers.</param>
        protected void AddPropertyCallback(Action handler, ref Action backingField)
        {
            ThrowIfOwnerDisposed();
            bool wasEmpty = backingField == null;
            backingField += handler;
            if (wasEmpty)
            {
                ClearChanges();
                RegisterForCallbacks();
            }
        }

        /// <summary>
        /// Removes a callback handler and, if no subscribers remain, unregisters native callbacks.
        /// </summary>
        /// <param name="handler">The callback to remove.</param>
        /// <param name="backingField">Reference to the private delegate field storing subscribers.</param>
        protected void RemovePropertyCallback(Action handler, ref Action backingField)
        {
            backingField -= handler;
            if (backingField == null)
            {
                UnregisterForCallbacks();
            }
        }

        /// <summary>
        /// Adds a typed callback handler and, if first subscriber, clears changes and registers native callbacks.
        /// </summary>
        protected void AddPropertyCallback<T>(Action<T> handler, ref Action<T> backingField)
        {
            ThrowIfOwnerDisposed();
            bool wasEmpty = backingField == null;
            backingField += handler;
            if (wasEmpty)
            {
                ClearChanges();
                RegisterForCallbacks();
            }
        }

        /// <summary>
        /// Removes a typed callback handler and unregisters native callbacks if no subscribers remain.
        /// </summary>
        protected void RemovePropertyCallback<T>(Action<T> handler, ref Action<T> backingField)
        {
            backingField -= handler;
            if (backingField == null)
            {
                UnregisterForCallbacks();
            }
        }
    }

    /// <summary>
    /// Generic subclass of ViewModelInstancePrimitiveProperty for primitive types. This class allows you to register a callback for when the value changes and provides a typed Value property.
    /// </summary>
    /// <typeparam name="T"> The type of the value.</typeparam>
    /// <remarks> This class is used for primitive types like strings, numbers, etc. </remarks>
    public abstract class ViewModelInstancePrimitiveProperty<T> : ViewModelInstancePrimitiveProperty
    {
        /// <summary>
        /// Event raised when the value changes, passing the new value.
        /// </summary>
        public event Action<T> OnValueChanged
        {
            add => AddPropertyCallback(value, ref m_onValueChanged);
            remove => RemovePropertyCallback(value, ref m_onValueChanged);
        }
        private Action<T> m_onValueChanged;

        internal ViewModelInstancePrimitiveProperty(ViewModelInstance instance, string name, int slot)
            : base(instance, name, slot) { }

        internal override void RaiseChangedEvent()
        {
            m_onValueChanged?.Invoke(Value);
        }

        internal override void RaiseChangedEvent(in PropertyValue capturedValue)
        {
            m_onValueChanged?.Invoke(FromValue(capturedValue));
        }

        /// This type's value, from what a read or capture replied with.
        internal abstract T FromValue(in PropertyValue value);

        internal override void ClearAllCallbacks()
        {
            m_onValueChanged = null;
            base.ClearAllCallbacks();
        }

        internal override void ClearDelegatesOnly()
        {
            m_onValueChanged = null;
        }

        public abstract T Value { get; set; }

        /// <summary>
        /// Reads the value without blocking the caller.
        /// </summary>
        /// <remarks>
        /// The result comes back in order with <see cref="OnValueChanged"/>, so after <c>OnValueChanged += Show; Show(await GetValueAsync());</c> an older value never follows a newer one. A value can repeat: a read that lands between an advance and its capture returns the new value, then OnValueChanged reports the same value. Reading doesn't count as seeing the change, so OnValueChanged still fires. To read every frame, subscribe to OnValueChanged instead.
        /// </remarks>
        /// <returns>A Future that finishes on the main thread with the value, or fails with ObjectDisposedException if the owning instance is disposed first.</returns>
        /// <exception cref="ObjectDisposedException">The owning ViewModelInstance has been disposed.</exception>
        internal Future<T> GetValueAsync()
        {
            ThrowIfOwnerDisposed();
            return PropertyCallbacksHub.Instance.ReadAsync(this);
        }

        /// <summary>
        /// Writes the value without blocking the caller.
        /// </summary>
        /// <param name="value">The value to write.</param>
        /// <returns>An operation that finishes on the main thread once the write has gone out.</returns>
        /// <exception cref="ObjectDisposedException">The owning ViewModelInstance has been disposed.</exception>
        internal Future SetValueAsync(T value)
        {
            ThrowIfOwnerDisposed();
            Value = value;
            return ViewModelNative.FenceAsync();
        }
    }
}
