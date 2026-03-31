using System;
using System.Runtime.InteropServices;

namespace Rive
{
    /// <summary>
    /// A view model instance property that holds a boolean.
    /// </summary>
    public sealed class ViewModelInstanceBooleanProperty : ViewModelInstancePrimitiveProperty<bool>
    {
        internal ViewModelInstanceBooleanProperty(IntPtr instanceValuePtr, ViewModelInstance rootInstance) : base(instanceValuePtr, rootInstance)
        {
        }

        /// <summary> 
        /// The value of the property.
        /// </summary>
        public override bool Value
        {
            get
            {
                ThrowIfOwnerDisposed();
                return getViewModelInstanceBooleanValue(InstancePropertyPtr);
            }
            set
            {
                ThrowIfOwnerDisposed();
                setViewModelInstanceBooleanValue(InstancePropertyPtr, value);
            }
        }


        [DllImport(NativeLibrary.name)]
        private static extern bool getViewModelInstanceBooleanValue(IntPtr instanceProperty);

        [DllImport(NativeLibrary.name)]
        private static extern void setViewModelInstanceBooleanValue(IntPtr instanceProperty, bool value);
    }
}
