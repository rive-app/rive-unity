using UnityEngine;
using System;
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
    public class ReportedEvent : IDisposable
    {
        private static readonly ObjectPool<ReportedEvent> s_Pool = new ObjectPool<ReportedEvent>(actionOnGet: (ReportedEvent e) => { }, actionOnRelease: (ReportedEvent e) => { });

        /// <summary>
        /// Factory method to retrieve a pooled instance.
        /// </summary>
        internal static ReportedEvent GetPooled(ReportedEventData data)
        {
            var evt = s_Pool.Get();
            evt.Initialize(data);
            return evt;
        }

        private bool m_isDisposed;


        private float m_secondsDelay;
        private string m_name;
        private uint m_propertyCount;
        private ushort m_type;
        private IntPtr m_nativeEvent;

        private bool m_nameLoaded;



        private Dictionary<string, object> m_properties;

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
            get
            {
                if (!m_nameLoaded && m_nativeEvent != IntPtr.Zero)
                {
                    m_name = Marshal.PtrToStringAnsi(NativeEventInterface.getEventName(m_nativeEvent));
                    m_nameLoaded = true;
                }
                return m_name;
            }
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
        public Dictionary<string, object> Properties
        {
            get
            {
                if (m_properties == null)
                {
                    m_properties = new Dictionary<string, object>();
                    for (uint i = 0; i < PropertyCount; i++)
                    {
                        var property = NativeEventInterface.getEventCustomProperty(m_nativeEvent, i);
                        var name = Marshal.PtrToStringAnsi(property.name);
                        switch (property.type)
                        {
                            case (ushort)PropertyType.Bool:
                                m_properties[name] = NativeEventInterface.getCustomBool(property.nativeProperty);
                                break;

                            case (ushort)PropertyType.String:
                                m_properties[name] = Marshal.PtrToStringAnsi(
                                    NativeEventInterface.getCustomString(property.nativeProperty)
                                );
                                break;

                            case (ushort)PropertyType.Number:
                                m_properties[name] = NativeEventInterface.getCustomNumber(property.nativeProperty);
                                break;
                        }
                    }
                }
                return m_properties;
            }
        }
        /// <summary>
        /// The specific kind of event fired (i.e. General, OpenUrl)
        /// </summary>
        public ushort Type
        {
            get
            {
                return m_type;
            }
        }

        /// <summary>
        /// The number of custom properties associated with the event.
        /// </summary>
        public uint PropertyCount
        {
            get
            {
                return m_propertyCount;
            }
        }

        /// <summary>
        /// Get a property by name.
        /// </summary>
        public object this[string index]
        {
            get
            {
                if (Properties == null)
                {
                    return null;
                }
                return Properties.TryGetValue(index, out var val) ? val : null;
            }
        }

        // constructor for pooling
        public ReportedEvent() { }

        internal void Initialize(ReportedEventData data)
        {
            // If a previous consumer called Dispose(), it doesn't matter because
            // once we pop from the pool, we fully reset the object here.
            m_isDisposed = false;

            // Reset old data
            m_nativeEvent = IntPtr.Zero;
            m_secondsDelay = 0f;
            m_type = 0;
            m_propertyCount = 0;
            m_name = null;
            m_properties?.Clear();

            m_nameLoaded = false;

            m_nativeEvent = data.nativeEvent;
            m_secondsDelay = data.secondsDelay;

            m_type = NativeEventInterface.getEventType(m_nativeEvent);
            m_propertyCount = NativeEventInterface.getEventCustomPropertyCount(m_nativeEvent);


        }



        /// <summary>
        /// Dispose of the event. This will return the instance to the pool for reuse.
        /// </summary>
        public void Dispose()
        {
            if (m_isDisposed)
                return;

            m_isDisposed = true;
            ReleaseToPool();
        }

        private void ReleaseToPool()
        {
            s_Pool.Release(this);
        }

        /// <summary>
        /// Get a custom property by index.
        /// </summary>
        /// <param name="index"> The index of the property to get.</param>
        /// <returns> The property at the given index.</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public Property GetProperty(uint index)
        {
            if (index >= PropertyCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            return new Property(NativeEventInterface.getEventCustomProperty(m_nativeEvent, index));
        }



        /// <summary>
        /// Represents a custom property of a ReportedEvent.
        /// </summary>
        public struct Property
        {
            private readonly IntPtr m_nativeProperty;
            private readonly IntPtr m_nativeName;
            private string m_name;
            private readonly PropertyType m_type;

            internal Property(UnityCustomProperty native)
            {
                m_nativeProperty = native.nativeProperty;
                m_name = null; // lazy load to avoid unnecessary string allocations
                m_type = (PropertyType)native.type;
                m_nativeName = native.name;

            }
            /// <summary>
            /// The name of this property.
            /// </summary>
            public string Name
            {
                get
                {
                    if (m_name == null)
                    {
                        m_name = Marshal.PtrToStringAnsi(m_nativeName);
                    }
                    return m_name;
                }
            }

            /// <summary>
            /// The type of this property.
            /// </summary>
            public PropertyType Type => m_type;

            /// <summary>
            /// Attempts to get the numeric value of this property.
            /// </summary>
            /// <param name="value">The output float value if successful.</param>
            /// <returns>True if the property is a number type and the value was retrieved, false otherwise.</returns>
            public bool TryGetNumber(out float value)
            {
                if (m_type != PropertyType.Number)
                {
                    value = default;
                    return false;
                }
                value = NativeEventInterface.getCustomNumber(m_nativeProperty);
                return true;
            }

            /// <summary>
            /// Attempts to get the boolean value of this property.
            /// </summary>
            /// <param name="value">The output boolean value if successful.</param>
            /// <returns>True if the property is a boolean type and the value was retrieved, false otherwise.</returns>
            public bool TryGetBool(out bool value)
            {
                if (m_type != PropertyType.Bool)
                {
                    value = default;
                    return false;
                }
                value = NativeEventInterface.getCustomBool(m_nativeProperty);
                return true;
            }

            /// <summary>
            /// Attempts to get the string value of this property.
            /// </summary>
            /// <param name="value">The output string value if successful.</param>
            /// <returns>True if the property is a string type and the value was retrieved, false otherwise.</returns>
            public bool TryGetString(out string value)
            {
                if (m_type != PropertyType.String)
                {
                    value = default;
                    return false;
                }
                value = Marshal.PtrToStringAnsi(NativeEventInterface.getCustomString(m_nativeProperty));
                return true;
            }
        }

        public enum PropertyType : ushort
        {
            Number = 127,
            Bool = 129,
            String = 130,
        }

    }

    /// <summary>
    /// Helper class to interface with native event data.
    /// </summary>
    internal class NativeEventInterface
    {
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
    }


}
