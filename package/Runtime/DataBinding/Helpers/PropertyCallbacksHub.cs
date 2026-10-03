using System;
using System.Collections.Generic;
using Rive.Producer;
using Rive.Host;

namespace Rive
{
    /// <summary>
    /// Centralized callback pump for view model instance properties. Used by the Orchestrator
    /// to trigger callbacks for all subscribed properties instead of traversing the ViewModelInstance hierarchy.
    /// Uses weak references so it does not prevent properties from being garbage collected;
    /// the owning <see cref="ViewModelInstance"/> is responsible for keeping subscribed properties alive.
    /// </summary>
    internal sealed class PropertyCallbacksHub
    {
        private static PropertyCallbacksHub s_instance;

        internal static PropertyCallbacksHub Instance
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = new PropertyCallbacksHub();
                }
                return s_instance;
            }
        }

        private readonly Dictionary<long, WeakReference<ViewModelInstancePrimitiveProperty>> m_subscribedProperties =
            new Dictionary<long, WeakReference<ViewModelInstancePrimitiveProperty>>();
        private readonly List<long> m_deadKeysScratch = new List<long>();
        // Held strongly while subscribed. Unsubscribing or disposing the
        // instance drops them.
        private readonly HashSet<ViewModelPropertyHandle> m_subscribedHandles = new HashSet<ViewModelPropertyHandle>();
        private readonly object m_lock = new object();
        private readonly ViewModelValuesChannel m_values;

        internal struct CapturedChange
        {
            internal ViewModelInstancePrimitiveProperty Property;
            internal PropertyValue Value;
        }

        private PropertyCallbacksHub()
        {
            m_values = new ViewModelValuesChannel(SnapshotProperties);
        }

        /// <summary>
        /// Registers a property with the hub via a weak reference.
        /// The owning <see cref="ViewModelInstance"/> keeps the property alive.
        /// </summary>
        /// <param name="property">The property to register.</param>
        internal void Register(ViewModelInstancePrimitiveProperty property)
        {
            if (property == null)
            {
                return;
            }

            lock (m_lock)
            {
                m_subscribedProperties[property.CallbackKey] = new WeakReference<ViewModelInstancePrimitiveProperty>(property);
            }
        }

        /// <summary>
        /// Unregisters a property from the hub.
        /// </summary>
        /// <param name="property">The property to unregister.</param>
        internal void Unregister(ViewModelInstancePrimitiveProperty property)
        {
            if (property == null)
            {
                return;
            }

            lock (m_lock)
            {
                m_subscribedProperties.Remove(property.CallbackKey);
            }
        }

        /// True if it wasn't registered already.
        internal bool RegisterHandle(ViewModelPropertyHandle handle)
        {
            lock (m_lock)
            {
                return m_subscribedHandles.Add(handle);
            }
        }

        internal void UnregisterHandle(ViewModelPropertyHandle handle)
        {
            lock (m_lock)
            {
                m_subscribedHandles.Remove(handle);
            }
        }

        /// After a StateMachineHandle advance.
        internal void SubmitCaptureAfterAdvance()
        {
            m_values.SendCaptureWithoutPanel();
        }

        /// <summary>
        /// Captures changed properties now and delivers their callbacks, after
        /// everything sent before it. For synchronous paths, like a pointer
        /// event on a synchronous panel.
        /// </summary>
        /// <returns>True if any changed properties were captured.</returns>
        internal bool CaptureChanges()
        {
            return m_values.CaptureNow();
        }

        /// After the tick pass.
        internal void SubmitProducerCapture()
        {
            m_values.SendTickCapture();
        }

        /// Straight after an async pointer job. See ViewModelValuesChannel.SendCapture.
        internal object SubmitPointerCapture()
        {
            return m_values.SendCapture();
        }

        internal void CancelPointerCapture(object capture)
        {
            m_values.TryCancelCapture(capture);
        }

        /// Delivers whatever has landed. Never waits.
        internal void PollProducerCapture()
        {
            m_values.Poll();
        }

        /// Waits for every capture and read sent so far and delivers them.
        /// False when a capture went with the producer.
        internal bool JoinProducerCapture()
        {
            return m_values.Join();
        }

        /// True while any capture or read is in flight.
        internal bool ProducerCapturePending => m_values.Pending;

        /// True while a value callback or read is being delivered.
        internal bool Delivering => m_values.Delivering;

        internal Future<T> ReadAsync<T>(ViewModelInstancePrimitiveProperty<T> property)
        {
            return m_values.Read(property);
        }

        internal Future<T> ReadHandleAsync<T>(ViewModelPropertyHandle handle)
        {
            return m_values.Read<T>(handle);
        }

        private void SnapshotProperties(
            List<ViewModelInstancePrimitiveProperty> properties,
            List<ViewModelPropertyHandle> handles)
        {
            properties.Clear();
            handles.Clear();
            m_deadKeysScratch.Clear();

            lock (m_lock)
            {
                foreach (ViewModelPropertyHandle handle in m_subscribedHandles)
                {
                    handles.Add(handle);
                }

                foreach (var kvp in m_subscribedProperties)
                {
                    if (kvp.Value.TryGetTarget(out var property))
                    {
                        properties.Add(property);
                    }
                    else
                    {
                        m_deadKeysScratch.Add(kvp.Key);
                    }
                }

                for (int i = 0; i < m_deadKeysScratch.Count; i++)
                {
                    m_subscribedProperties.Remove(m_deadKeysScratch[i]);
                }
            }
        }

        /// <summary>
        /// Delivers captured callbacks that have landed. Never waits.
        /// </summary>
        internal void FlushCapturedCallbacks()
        {
            using var noWait = CommandTransport.NoWait("callback flush");
            m_values.Poll();
        }

#if UNITY_EDITOR
        // Account for Editor Domain Reload being disabled.
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            s_instance = null;
        }
#endif
    }
}
