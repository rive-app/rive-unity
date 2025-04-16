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
        /// Event that is called when the value of this property changes.
        /// </summary>
        public event Action OnValueChanged
        {
            add
            {
                bool wasEmpty = m_onValueChanged == null;
                m_onValueChanged += value;
                // If we previously had no subscribers, we register now
                if (wasEmpty && m_onValueChanged != null)
                {
                    // Since we don't clean the changed flag for properties that don't have listeners,
                    // we clean it the first time we add a listener to it
                    ClearChanges();

                    m_instance.RegisterPropertyForCallbacks(this);
                }
            }
            remove
            {
                m_onValueChanged -= value;
                // If we no longer have subscribers, unregister
                if (m_onValueChanged == null)
                {
                    m_instance.UnregisterPropertyForCallbacks(this);
                }
            }
        }
        private Action m_onValueChanged;

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
        internal void RaiseChangedEvent()
        {
            m_onValueChanged?.Invoke();
        }

        /// <summary>
        /// Clears all callbacks registered with this property.
        /// </summary>
        internal void ClearAllCallbacks()
        {
            m_onValueChanged = null;

            // If we've removed all subscribers, make sure we're unregistered
            m_instance?.UnregisterPropertyForCallbacks(this);
        }

        [DllImport(NativeLibrary.name)]
        private static extern bool viewModelInstancePropertyValueHasChanged(IntPtr instanceValue);

        [DllImport(NativeLibrary.name)]
        private static extern void clearViewModelInstancePropertyValueChanges(IntPtr instanceValue);
    }
}
