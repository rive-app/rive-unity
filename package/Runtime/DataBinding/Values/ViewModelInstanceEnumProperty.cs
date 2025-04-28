using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Rive.Utils;

namespace Rive
{
    /// <summary>
    /// A view model instance property that holds an enum.
    /// </summary>
    public sealed class ViewModelInstanceEnumProperty : ViewModelInstancePrimitiveProperty<string>
    {
        private string[] m_enumValues;

        /// <summary>
        /// Constructor for the enum property. This is used when the enum values are known ahead of time.
        /// </summary>
        /// <param name="instanceValuePtr"> The pointer to the instance property. </param>
        /// <param name="rootInstance"> The root instance of the view model. </param>
        /// <param name="enumValues"> The list of enum values. </param>
        internal ViewModelInstanceEnumProperty(IntPtr instanceValuePtr, ViewModelInstance rootInstance, string[] enumValues) : base(instanceValuePtr, rootInstance)
        {
            m_enumValues = enumValues;
        }

        /// <summary>
        /// Constructor for the enum property. This is used when the enum values are not known ahead of time.
        /// </summary>
        /// <param name="instanceValuePtr"> The pointer to the instance property. </param>
        /// <param name="rootInstance"> The root instance of the view model. </param>
        internal ViewModelInstanceEnumProperty(IntPtr instanceValuePtr, ViewModelInstance rootInstance) : base(instanceValuePtr, rootInstance)
        {
            PopulateEnumValuesIfNeeded();
        }

        /// <summary>
        /// The current enum value of the property.
        /// </summary>
        public override string Value
        {
            get
            {
                if (InstancePropertyPtr == IntPtr.Zero)
                {
                    DebugLogger.Instance.LogWarning("Trying to get a null enum property.");
                    return null;
                }

                if (m_enumValues.Length == 0)
                {
                    return null;
                }


                return m_enumValues[(int)getViewModelInstanceEnumIndex(InstancePropertyPtr)];
            }
            set
            {
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

                setViewModelInstanceEnumIndex(InstancePropertyPtr, (uint)index);
            }
        }

        /// <summary>
        /// The current enum value index of the property in the enum values list.
        /// </summary>
        internal int ValueIndex
        {
            get
            {
                if (InstancePropertyPtr == IntPtr.Zero)
                {
                    DebugLogger.Instance.LogWarning("Trying to get a null enum property.");
                    return -1;
                }

                return (int)getViewModelInstanceEnumIndex(InstancePropertyPtr);
            }
            set
            {
                if (value < 0 || value >= m_enumValues.Length)
                {
                    DebugLogger.Instance.LogWarning("Invalid enum value index: " + value);
                    return;
                }

                setViewModelInstanceEnumIndex(InstancePropertyPtr, (uint)value);
            }
        }

        private void PopulateEnumValuesIfNeeded()
        {
            if (m_enumValues == null)
            {
                IntPtr m_enumValuesPtr = getViewModelInstanceEnumValues(InstancePropertyPtr);
                nuint valueCount = getViewModelInstanceEnumValueCount(m_enumValuesPtr);
                m_enumValues = new string[(int)valueCount];
                for (nuint i = 0; i < valueCount; i++)
                {
                    m_enumValues[i] = Marshal.PtrToStringAnsi(getViewModelInstanceEnumValueAtIndex(m_enumValuesPtr, i));
                }

                freeViewModelInstanceEnumValues(m_enumValuesPtr);
            }

        }

        /// <summary>
        /// The list of enum options for this property. 
        /// </summary>
        public IReadOnlyList<string> EnumValues
        {
            get
            {
                if (m_enumValues == null)
                {
                    PopulateEnumValuesIfNeeded();
                }

                return m_enumValues;
            }
        }

        [DllImport(NativeLibrary.name)]
        private static extern uint getViewModelInstanceEnumIndex(IntPtr instanceProperty);

        [DllImport(NativeLibrary.name)]
        private static extern void setViewModelInstanceEnumIndex(IntPtr instanceProperty, uint value);

        [DllImport(NativeLibrary.name)]
        private static extern IntPtr getViewModelInstanceEnumValues(IntPtr instanceProperty);

        [DllImport(NativeLibrary.name)]
        private static extern nuint getViewModelInstanceEnumValueCount(IntPtr listPtr);

        [DllImport(NativeLibrary.name)]
        private static extern IntPtr getViewModelInstanceEnumValueAtIndex(IntPtr listPtr, nuint index);

        [DllImport(NativeLibrary.name)]
        private static extern void freeViewModelInstanceEnumValues(IntPtr listPtr);
    }
}
