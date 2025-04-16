using System;
using System.Runtime.InteropServices;
using Rive.Utils;

namespace Rive
{
    /// <summary>
    /// A view model instance property that is a trigger.
    /// </summary>
    public sealed class ViewModelInstanceTriggerProperty : ViewModelInstancePrimitiveProperty
    {
        internal ViewModelInstanceTriggerProperty(IntPtr instanceValuePtr, ViewModelInstance rootInstance) : base(instanceValuePtr, rootInstance)
        {
        }

        /// <summary>
        /// Fires the trigger.
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
    }
}
