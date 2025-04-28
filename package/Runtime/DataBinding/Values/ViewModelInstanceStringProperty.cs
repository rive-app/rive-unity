using System;
using System.Runtime.InteropServices;

namespace Rive
{
    /// <summary>
    /// A view model instance property that holds a string.
    /// </summary>
    public sealed class ViewModelInstanceStringProperty : ViewModelInstancePrimitiveProperty<string>
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct StringHashInfo
        {
            public IntPtr str;
            public uint length;
            public uint hash;
        }

        private StringHashInfo m_lastInfo;
        private string m_stringVal = null;

        internal ViewModelInstanceStringProperty(IntPtr instanceValuePtr, ViewModelInstance parentInstance) : base(instanceValuePtr, parentInstance)
        {
        }

        public override string Value
        {
            get
            {
                StringHashInfo currentInfo = getViewModelInstanceStringInfo(InstancePropertyPtr);

                // We check if either length or hash changed before marshalling the string
                // This is a performance optimization to avoid marshalling the string every frame if it hasn't changed as it allocates memory every time the value is read
                // This allocation can be expensive and might cause GC spikes if done frequently 

                // TODO: Since the VM Instance Runtime knows when the string changes, we should switch to using that instead instead of a hash at some point
                bool stringHasChanged = currentInfo.length != m_lastInfo.length || currentInfo.hash != m_lastInfo.hash;
                if (m_stringVal == null || stringHasChanged)
                {
                    m_lastInfo = currentInfo;

                    m_stringVal = Marshal.PtrToStringAnsi(currentInfo.str);
                }

                return m_stringVal;
            }
            set
            {
                setViewModelInstanceStringValue(InstancePropertyPtr, value);
            }
        }

        [DllImport(NativeLibrary.name)]
        private static extern IntPtr getViewModelInstanceStringValue(IntPtr instanceValue);

        [DllImport(NativeLibrary.name)]
        private static extern void setViewModelInstanceStringValue(IntPtr instanceValue, string value);

        [DllImport(NativeLibrary.name)]
        private static extern StringHashInfo getViewModelInstanceStringInfo(IntPtr instanceValue);
    }
}
