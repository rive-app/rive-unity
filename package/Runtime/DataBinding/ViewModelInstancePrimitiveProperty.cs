using System;
using System.Runtime.InteropServices;

namespace Rive
{
    /// <summary>
    /// Base class for all primitive properties of a ViewModelInstance. This is usually used for types like numbers, strings, etc. that have a value change event.
    /// </summary>
    public abstract class ViewModelInstancePrimitiveProperty : ViewModelInstanceProperty
    {
        private IntPtr m_instanceValuePropertyPtr;
        private ViewModelInstance m_instance; // The instance this property belongs to

        internal IntPtr InstancePropertyPtr => m_instanceValuePropertyPtr;

        /// <summary>
        /// Whether the value has changed since the last time it was read.
        /// </summary>
        internal bool HasChanged => viewModelInstancePropertyValueHasChanged(m_instanceValuePropertyPtr);

        /// <summary>
        /// Constructor for a ViewModelInstanceProperty.
        /// </summary>
        /// <param name="instanceValuePtr"> Pointer to the instance property value.</param>
        /// <param name="instance"> The instance this property belongs to.</param>
        internal ViewModelInstancePrimitiveProperty(IntPtr instanceValuePtr, ViewModelInstance instance)
        {
            m_instanceValuePropertyPtr = instanceValuePtr;
            m_instance = instance;
        }

        ~ViewModelInstancePrimitiveProperty()
        {
            if (m_instanceValuePropertyPtr != IntPtr.Zero)
            {
                ViewModelInstanceProperty.RemoveCachedPropertyForPointer(m_instanceValuePropertyPtr);
                m_instanceValuePropertyPtr = IntPtr.Zero;
            }
        }

        /// <summary>
        /// Reset the changed flag for this value.
        /// </summary>
        internal void ClearChanges()
        {
            clearViewModelInstancePropertyValueChanges(m_instanceValuePropertyPtr);
        }

        /// <summary>
        /// Called by ViewModelInstance when it detects this property has changed.
        /// </summary>
        internal virtual void RaiseChangedEvent()
        {
            // no-op in non-generic base; override in subclasses
        }

        /// <summary>
        /// Clears all callbacks registered with this property.
        /// </summary>
        internal virtual void ClearAllCallbacks()
        {
            // If we've removed all subscribers, unregister
            m_instance?.UnregisterPropertyForCallbacks(this);
        }

        internal void RegisterForCallbacks()
        {

            // Since we don't clean the changed flag for properties that don't have listeners,
            // we clean it the first time we add a listener to it
            ClearChanges();
            m_instance.RegisterPropertyForCallbacks(this);
        }

        internal void UnregisterForCallbacks()
        {
            m_instance.UnregisterPropertyForCallbacks(this);
        }

        [DllImport(NativeLibrary.name)]
        private static extern bool viewModelInstancePropertyValueHasChanged(IntPtr instanceValue);

        [DllImport(NativeLibrary.name)]
        private static extern void clearViewModelInstancePropertyValueChanges(IntPtr instanceValue);

        // Helpers to add/remove managed callbacks and register/unregister native notifications
        /// <summary>
        /// Adds a callback handler and, if first subscriber, clears stale changes and registers native callbacks.
        /// </summary>
        /// <param name="handler">The callback to invoke when this property changes.</param>
        /// <param name="backingField">Reference to the private delegate field storing subscribers.</param>
        protected void AddPropertyCallback(Action handler, ref Action backingField)
        {
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

        internal ViewModelInstancePrimitiveProperty(IntPtr instanceValuePtr, ViewModelInstance instance)
            : base(instanceValuePtr, instance) { }

        internal override void RaiseChangedEvent()
        {
            m_onValueChanged?.Invoke(Value);
        }

        internal override void ClearAllCallbacks()
        {
            m_onValueChanged = null;
            base.ClearAllCallbacks();
        }

        public abstract T Value { get; set; }
    }
}
