using System;
using System.Runtime.InteropServices;
using Rive.Utils;

namespace Rive
{
    /// <summary>
    /// A view model instance property for trigger properties.
    /// </summary>
    public sealed class ViewModelInstanceTriggerProperty : ViewModelInstancePrimitiveProperty
    {
        internal ViewModelInstanceTriggerProperty(IntPtr instanceValuePtr, ViewModelInstance rootInstance) : base(instanceValuePtr, rootInstance)
        {
        }

        /// <summary>
        /// Raised when the trigger property is fired in the Rive graphic.
        /// </summary>
        public event Action OnTriggered
        {
            add => AddPropertyCallback(value, ref m_onTriggered);
            remove => RemovePropertyCallback(value, ref m_onTriggered);
        }
        private Action m_onTriggered;

        /// <summary>
        /// Fires the trigger
        /// </summary>
        public void Trigger()
        {
            if (InstancePropertyPtr == IntPtr.Zero)
            {
                DebugLogger.Instance.LogWarning("Trying to trigger a null trigger property.");
                return;
            }

            fireViewModelInstanceTrigger(InstancePropertyPtr);
        }

        [DllImport(NativeLibrary.name)]
        private static extern void fireViewModelInstanceTrigger(IntPtr instanceProperty);

        internal override void RaiseChangedEvent()
        {
            m_onTriggered?.Invoke();
        }

        internal override void ClearAllCallbacks()
        {
            m_onTriggered = null;
            base.ClearAllCallbacks();
        }
    }
}
