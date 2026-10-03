using System;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace Rive
{
    internal struct ReportedEventData
    {
        internal string Name;
        internal float SecondsDelay;
        internal ushort Type;
        internal ReportedEventPropertyData[] Properties;
    }

    internal struct ReportedEventPropertyData
    {
        internal string Name;
        internal ushort Type;
        internal float NumberValue;
        internal bool BoolValue;
        internal string StringValue;
    }


    /// <summary>
    /// Represents an event reported by a StateMachine.
    /// </summary>
    public class ReportedEvent : IDisposable
    {
        private static readonly ObjectPool<ReportedEvent> s_Pool = new ObjectPool<ReportedEvent>(actionOnGet: (ReportedEvent e) => { }, actionOnRelease: (ReportedEvent e) => { }, createFunc: () => new ReportedEvent());

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
        private ReportedEventPropertyData[] m_propertyData;

        private bool m_propertiesLoaded = false;



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
        public Dictionary<string, object> Properties
        {
            get
            {
                if (m_properties == null)
                {
                    m_properties = new Dictionary<string, object>();
                }

                if (!m_propertiesLoaded)
                {
                    for (uint i = 0; i < PropertyCount; i++)
                    {
                        var property = m_propertyData[i];
                        switch (property.Type)
                        {
                            case (ushort)PropertyType.Bool:
                                m_properties[property.Name] = property.BoolValue;
                                break;

                            case (ushort)PropertyType.String:
                                m_properties[property.Name] = property.StringValue;
                                break;

                            case (ushort)PropertyType.Number:
                                m_properties[property.Name] = property.NumberValue;
                                break;
                        }
                    }

                    m_propertiesLoaded = true;
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
        internal ReportedEvent() { }

        internal void Initialize(ReportedEventData data)
        {
            // A previous consumer may have disposed this pooled instance.
            // Reset it before exposing the next event.
            m_isDisposed = false;
            m_properties?.Clear();

            // The values are already snapshotted. Build the dictionary only if
            // Properties is requested; indexed getters read the snapshot directly.
            m_propertiesLoaded = false;
            m_name = data.Name;
            m_secondsDelay = data.SecondsDelay;
            m_type = data.Type;
            m_propertyData = data.Properties;
            m_propertyCount = (uint)(m_propertyData?.Length ?? 0);
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
            return new Property(m_propertyData[index]);
        }



        /// <summary>
        /// Represents a custom property of a ReportedEvent.
        /// </summary>
        public struct Property
        {
            private readonly ReportedEventPropertyData m_data;

            internal Property(ReportedEventPropertyData data)
            {
                m_data = data;
            }
            /// <summary>
            /// The name of this property.
            /// </summary>
            public string Name
            {
                get { return m_data.Name; }
            }

            /// <summary>
            /// The type of this property.
            /// </summary>
            public PropertyType Type => (PropertyType)m_data.Type;

            /// <summary>
            /// Attempts to get the numeric value of this property.
            /// </summary>
            /// <param name="value">The output float value if successful.</param>
            /// <returns>True if the property is a number type and the value was retrieved, false otherwise.</returns>
            public bool TryGetNumber(out float value)
            {
                if (Type != PropertyType.Number)
                {
                    value = default;
                    return false;
                }
                value = m_data.NumberValue;
                return true;
            }

            /// <summary>
            /// Attempts to get the boolean value of this property.
            /// </summary>
            /// <param name="value">The output boolean value if successful.</param>
            /// <returns>True if the property is a boolean type and the value was retrieved, false otherwise.</returns>
            public bool TryGetBool(out bool value)
            {
                if (Type != PropertyType.Bool)
                {
                    value = default;
                    return false;
                }
                value = m_data.BoolValue;
                return true;
            }

            /// <summary>
            /// Attempts to get the string value of this property.
            /// </summary>
            /// <param name="value">The output string value if successful.</param>
            /// <returns>True if the property is a string type and the value was retrieved, false otherwise.</returns>
            public bool TryGetString(out string value)
            {
                if (Type != PropertyType.String)
                {
                    value = default;
                    return false;
                }
                value = m_data.StringValue;
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
}
