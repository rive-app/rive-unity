using System;
using Rive.Utils;

namespace Rive
{
    /// <summary>
    /// A view model instance property for trigger properties.
    /// </summary>
    public sealed class ViewModelInstanceTriggerProperty : ViewModelInstancePrimitiveProperty
    {
        internal ViewModelInstanceTriggerProperty(ViewModelInstance rootInstance, string name, int slot) : base(rootInstance, name, slot)
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
            ThrowIfOwnerDisposed();

            if (!IsAttached)
            {
                DebugLogger.Instance.LogWarning("Trying to trigger a null trigger property.");
                return;
            }

            WriteNative(0f, 0, null);
        }

        internal override void RaiseChangedEvent()
        {
            m_onTriggered?.Invoke();
        }

        internal override void ClearAllCallbacks()
        {
            m_onTriggered = null;
            base.ClearAllCallbacks();
        }

        internal override void ClearDelegatesOnly()
        {
            m_onTriggered = null;
        }
    }
}
