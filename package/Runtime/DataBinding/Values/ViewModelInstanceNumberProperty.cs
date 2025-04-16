using System;
using System.Runtime.InteropServices;

namespace Rive
{
    /// <summary>
    /// A view model instance property that holds a number.
    /// </summary>
    public sealed class ViewModelInstanceNumberProperty : ViewModelInstancePrimitiveProperty
    {
        internal ViewModelInstanceNumberProperty(IntPtr instanceValuePtr, ViewModelInstance rootInstance) : base(instanceValuePtr, rootInstance)
        {
        }



        /// <summary>
        /// The value of the property.
        /// </summary>
        public float Value { get { return getViewModelInstanceNumberValue(InstancePropertyPtr); } set { setViewModelInstanceNumberValue(InstancePropertyPtr, value); } }

        [DllImport(NativeLibrary.name)]
        private static extern float getViewModelInstanceNumberValue(IntPtr instanceProperty);

        [DllImport(NativeLibrary.name)]
        private static extern void setViewModelInstanceNumberValue(IntPtr instanceProperty, float value);
    }
}
