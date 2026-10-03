using System;

using Rive.Host;

namespace Rive
{
    /// <summary>
    /// A view model instance property that holds a string.
    /// </summary>
    public sealed class ViewModelInstanceStringProperty : ViewModelInstancePrimitiveProperty<string>
    {
        internal ViewModelInstanceStringProperty(ViewModelInstance parentInstance, string name, int slot) : base(parentInstance, name, slot)
        {
        }

        internal override string FromValue(in PropertyValue value) => value.Text;

        public override string Value
        {
            get
            {
                ThrowIfOwnerDisposed();
                return ReadNative((ref PayloadReader reader) => reader.String(), string.Empty);
            }
            set
            {
                ThrowIfOwnerDisposed();
                WriteNative(0f, 0, value ?? string.Empty);
            }
        }
    }
}
