using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine.Rendering;

namespace Rive
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct ReportedEventData
    {
        public IntPtr nativeEvent;
        public float secondsDelay;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct UnityCustomProperty
    {
        public IntPtr nativeProperty;
        public IntPtr name;
        public ushort type;
    }


    /// <summary>
    /// Represents an event reported by a StateMachine.
    /// </summary>
    public class ReportedEvent
    {
        private float m_secondsDelay;
        private string m_name;
        private Dictionary<string, object> m_properties;

        public float secondsDelay
        {
            get { return m_secondsDelay; }
        }

        public String name
        {
            get { return m_name; }
        }


        public object this[string index]
        {
            get { return m_properties == null ? null : m_properties[index]; }
        }

        internal ReportedEvent(ReportedEventData data)
        {
            m_secondsDelay = data.secondsDelay;
            m_name = Marshal.PtrToStringAnsi(getEventName(data.nativeEvent));
            var propertyCount = getEventCustomPropertyCount(data.nativeEvent);
            if (propertyCount != 0)
            {
                m_properties = new Dictionary<string, object>();
                for (uint i = 0; i < propertyCount; i++)
                {
                    var property = getEventCustomProperty(data.nativeEvent, i);
                    var name = Marshal.PtrToStringAnsi(property.name);
                    switch (property.type)
                    {
                        case 129:
                            m_properties[name] = getCustomBool(property.nativeProperty);
                            break;

                        case 130:
                            m_properties[name] = Marshal.PtrToStringAnsi(getCustomString(property.nativeProperty));
                            break;

                        case 127:
                            m_properties[name] = getCustomNumber(property.nativeProperty);
                            break;
                    }

                }
            }
        }

        #region Native Methods
        [DllImport(NativeLibrary.name)]
        internal static extern IntPtr getEventName(IntPtr nativeEvent);

        [DllImport(NativeLibrary.name)]
        internal static extern uint getEventCustomPropertyCount(IntPtr nativeEvent);

        [DllImport(NativeLibrary.name)]
        internal static extern UnityCustomProperty getEventCustomProperty(IntPtr nativeEvent, uint index);

        [DllImport(NativeLibrary.name)]
        internal static extern IntPtr getCustomString(IntPtr nativeProperty);

        [DllImport(NativeLibrary.name)]
        internal static extern bool getCustomBool(IntPtr nativeProperty);

        [DllImport(NativeLibrary.name)]
        internal static extern float getCustomNumber(IntPtr nativeProperty);

        #endregion

    }
}

