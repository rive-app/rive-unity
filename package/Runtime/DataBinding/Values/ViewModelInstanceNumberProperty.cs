using System;
using System.Runtime.InteropServices;

namespace Rive
{
    /// <summary>
    /// A view model instance property that holds a number.
    /// </summary>
    public sealed class ViewModelInstanceNumberProperty : ViewModelInstancePrimitiveProperty<float>
    {
        internal ViewModelInstanceNumberProperty(IntPtr instanceValuePtr, ViewModelInstance rootInstance) : base(instanceValuePtr, rootInstance)
        {
        }

        /// <summary>
        /// The value of the property.
        /// </summary>
        public override float Value
        {
            get
            {
                ThrowIfOwnerDisposed();
                return getViewModelInstanceNumberValue(InstancePropertyPtr);
            }
            set
            {
                ThrowIfOwnerDisposed();
                setViewModelInstanceNumberValue(InstancePropertyPtr, value);
            }
        }

        [DllImport(NativeLibrary.name)]
        private static extern float getViewModelInstanceNumberValue(IntPtr instanceProperty);

        [DllImport(NativeLibrary.name)]
        private static extern void setViewModelInstanceNumberValue(IntPtr instanceProperty, float value);
    }
}
