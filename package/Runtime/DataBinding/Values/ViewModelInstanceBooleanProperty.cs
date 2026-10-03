using System;

using Rive.Host;

namespace Rive
{
    /// <summary>
    /// A view model instance property that holds a boolean.
    /// </summary>
    public sealed class ViewModelInstanceBooleanProperty : ViewModelInstancePrimitiveProperty<bool>
    {
        internal ViewModelInstanceBooleanProperty(ViewModelInstance rootInstance, string name, int slot) : base(rootInstance, name, slot)
        {
        }

        internal override bool FromValue(in PropertyValue value) => value.Bits != 0;

        /// <summary>
        /// The value of the property.
        /// </summary>
        public override bool Value
        {
            get
            {
                ThrowIfOwnerDisposed();
                return ReadNative((ref PayloadReader reader) => reader.Bool(), false);
            }
            set
            {
                ThrowIfOwnerDisposed();
                WriteNative(0f, value ? 1 : 0, null);
            }
        }
    }
}
