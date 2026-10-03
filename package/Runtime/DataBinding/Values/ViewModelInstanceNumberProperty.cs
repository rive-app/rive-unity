using System;

using Rive.Host;

namespace Rive
{
    /// <summary>
    /// A view model instance property that holds a number.
    /// </summary>
    public sealed class ViewModelInstanceNumberProperty : ViewModelInstancePrimitiveProperty<float>
    {
        internal ViewModelInstanceNumberProperty(ViewModelInstance rootInstance, string name, int slot) : base(rootInstance, name, slot)
        {
        }

        internal override float FromValue(in PropertyValue value) => value.Number;

        /// <summary>
        /// The value of the property.
        /// </summary>
        public override float Value
        {
            get
            {
                ThrowIfOwnerDisposed();
                return ReadNative((ref PayloadReader reader) => reader.F32(), 0f);
            }
            set
            {
                ThrowIfOwnerDisposed();
                WriteNative(value, 0, null);
            }
        }
    }
}
