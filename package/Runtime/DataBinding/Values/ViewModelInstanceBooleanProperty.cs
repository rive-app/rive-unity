using System;
using System.Runtime.InteropServices;

namespace Rive
{
    /// <summary>
    /// A view model instance property that holds a boolean.
    /// </summary>
    public sealed class ViewModelInstanceBooleanProperty : ViewModelInstancePrimitiveProperty
    {
        internal ViewModelInstanceBooleanProperty(IntPtr instanceValuePtr, ViewModelInstance rootInstance) : base(instanceValuePtr, rootInstance)
        {
        }

        /// <summary> 
        /// The value of the property.
        /// </summary>
        public bool Value
        {
            get { return getViewModelInstanceBooleanValue(InstancePropertyPtr); }
            set { setViewModelInstanceBooleanValue(InstancePropertyPtr, value); }
        }


        [DllImport(NativeLibrary.name)]
        private static extern bool getViewModelInstanceBooleanValue(IntPtr instanceProperty);

        [DllImport(NativeLibrary.name)]
        private static extern void setViewModelInstanceBooleanValue(IntPtr instanceProperty, bool value);
    }
}
