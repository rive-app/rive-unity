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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private UnseenBoolCheck m_unseen;
#endif

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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                m_unseen.Write(value, Name);
#endif
                WriteNative(0f, value ? 1 : 0, null);
            }
        }
    }
}
