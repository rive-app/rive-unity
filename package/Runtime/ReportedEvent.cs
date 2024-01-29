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
        private readonly float m_secondsDelay;
        private readonly string m_name;

        private readonly ushort m_type;

        private readonly Dictionary<string, object> m_properties;

        /// <summary>
        /// The number of seconds after the event was triggered that it was reported.
        /// </summary>
        public float SecondsDelay
        {
            get { return m_secondsDelay; }
        }

        /// <summary>
        /// The name of the event.
        /// </summary>
        public string Name
        {
            get { return m_name; }
        }

        /// <summary>
        /// The associated properties of the event.
        ///
        /// The properties are a dictionary of key/value pairs.
        /// The key is the name of the property.
        /// The value can be a string, float, or boolean.
        /// </summary>
        /// <remarks>
        /// The properties can also be accessed by name using the indexer.
        /// </remarks>
        /// <example>
        /// <code>
        /// event["myProperty"];
        /// event.properties["myProperty"]; // instead of
        /// </code>
        /// </example>
        public Dictionary<string, object> Properties => m_properties;

        public ushort Type
        {
            get { return m_type; }
        }

        /// <summary>
        /// Get a property by name.
        /// </summary>
        public object this[string index]
        {
            get { return m_properties?[index]; }
        }

        internal ReportedEvent(ReportedEventData data)
        {
            m_secondsDelay = data.secondsDelay;
            m_name = Marshal.PtrToStringAnsi(getEventName(data.nativeEvent));
            m_type = getEventType(data.nativeEvent);
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
                            m_properties[name] = Marshal.PtrToStringAnsi(
                                getCustomString(property.nativeProperty)
                            );
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
        internal static extern ushort getEventType(IntPtr nativeEvent);

        [DllImport(NativeLibrary.name)]
        internal static extern uint getEventCustomPropertyCount(IntPtr nativeEvent);

        [DllImport(NativeLibrary.name)]
        internal static extern UnityCustomProperty getEventCustomProperty(
            IntPtr nativeEvent,
            uint index
        );

        [DllImport(NativeLibrary.name)]
        internal static extern IntPtr getCustomString(IntPtr nativeProperty);

        [DllImport(NativeLibrary.name)]
        internal static extern bool getCustomBool(IntPtr nativeProperty);

        [DllImport(NativeLibrary.name)]
        internal static extern float getCustomNumber(IntPtr nativeProperty);

        #endregion
    }
}
