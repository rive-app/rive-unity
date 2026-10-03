using System;
using System.Collections.Generic;
using Rive.Utils;

using Rive.Host;

namespace Rive
{
    /// <summary>
    /// A view model instance property that holds an enum.
    /// </summary>
    public sealed class ViewModelInstanceEnumProperty : ViewModelInstancePrimitiveProperty<string>
    {
        private string[] m_enumValues;

        internal ViewModelInstanceEnumProperty(ViewModelInstance rootInstance, string name, int slot, string[] enumValues) : base(rootInstance, name, slot)
        {
            m_enumValues = enumValues ?? Array.Empty<string>();
        }

        internal override string FromValue(in PropertyValue value) => value.Text;

        /// <summary>
        /// The current enum value of the property.
        /// </summary>
        public override string Value
        {
            get
            {
                ThrowIfOwnerDisposed();

                if (!IsAttached)
                {
                    DebugLogger.Instance.LogWarning("Trying to get a null enum property.");
                    return null;
                }

                if (m_enumValues.Length == 0)
                {
                    return null;
                }

                int valueIndex = ReadIndex();
                return valueIndex >= 0 && valueIndex < m_enumValues.Length ? m_enumValues[valueIndex] : null;
            }
            set
            {
                ThrowIfOwnerDisposed();

                int index = Array.IndexOf(m_enumValues, value);

                if (index == -1)
                {
                    DebugLogger.Instance.LogWarning("Invalid enum value: " + value);
                    return;
                }

                if (index < 0)
                {
                    DebugLogger.Instance.LogWarning("Trying to set a negative enum value.");
                    return;
                }

                WriteNative(0f, index, null);
            }
        }

        /// <summary>
        /// The current enum value index of the property in the enum values list.
        /// </summary>
        internal int ValueIndex
        {
            get
            {
                ThrowIfOwnerDisposed();

                if (!IsAttached)
                {
                    DebugLogger.Instance.LogWarning("Trying to get a null enum property.");
                    return -1;
                }

                return ReadIndex();
            }
            set
            {
                ThrowIfOwnerDisposed();

                if (value < 0 || value >= m_enumValues.Length)
                {
                    DebugLogger.Instance.LogWarning("Invalid enum value index: " + value);
                    return;
                }

                WriteNative(0f, value, null);
            }
        }

        /// <summary>
        /// The list of enum options for this property. 
        /// </summary>
        public IReadOnlyList<string> EnumValues
        {
            get { return m_enumValues; }
        }

        // A read replies with the value's name, then its index.
        private int ReadIndex()
        {
            return ReadNative((ref PayloadReader reader) =>
            {
                reader.String();
                return (int)reader.U32();
            }, -1);
        }
    }
}
