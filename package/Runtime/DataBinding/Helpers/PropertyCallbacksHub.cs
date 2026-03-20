using System;
using System.Collections.Generic;

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

        private readonly Dictionary<IntPtr, WeakReference<ViewModelInstancePrimitiveProperty>> m_subscribedProperties =
            new Dictionary<IntPtr, WeakReference<ViewModelInstancePrimitiveProperty>>();
        private readonly List<ViewModelInstancePrimitiveProperty> m_changedScratch = new List<ViewModelInstancePrimitiveProperty>();
        private readonly List<ViewModelInstancePrimitiveProperty> m_captureScratch = new List<ViewModelInstancePrimitiveProperty>();
        private readonly List<IntPtr> m_deadPointersScratch = new List<IntPtr>();
        private readonly object m_lock = new object();

        private PropertyCallbacksHub() { }

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

            IntPtr ptr = property.InstancePropertyPtr;
            if (ptr == IntPtr.Zero)
            {
                return;
            }

            lock (m_lock)
            {
                m_subscribedProperties[ptr] = new WeakReference<ViewModelInstancePrimitiveProperty>(property);
            }
        }

        /// <summary>
        /// Unregisters a property from the hub.
        /// </summary>
        /// <param name="instancePropertyPtr">The pointer to the property to unregister.</param>
        internal void Unregister(IntPtr instancePropertyPtr)
        {
            if (instancePropertyPtr == IntPtr.Zero)
            {
                return;
            }

            lock (m_lock)
            {
                m_subscribedProperties.Remove(instancePropertyPtr);
            }
        }

        /// <summary>
        /// Captures changed properties and clears their change flags.
        /// This should run in the same frame as panel/state machine advancement.
        /// </summary>
        /// <returns>True if any changed properties were captured.</returns>
        internal bool CaptureChanges()
        {
            m_changedScratch.Clear();
            m_deadPointersScratch.Clear();

            lock (m_lock)
            {
                m_captureScratch.Clear();
                foreach (var kvp in m_subscribedProperties)
                {
                    if (kvp.Value.TryGetTarget(out var property))
                    {
                        m_captureScratch.Add(property);
                    }
                    else
                    {
                        m_deadPointersScratch.Add(kvp.Key);
                    }
                }

                for (int i = 0; i < m_deadPointersScratch.Count; i++)
                {
                    m_subscribedProperties.Remove(m_deadPointersScratch[i]);
                }
            }

            for (int i = 0; i < m_captureScratch.Count; i++)
            {
                var property = m_captureScratch[i];

                if (property.HasChanged)
                {
                    property.ClearChanges();
                    m_changedScratch.Add(property);
                }
            }

            return m_changedScratch.Count > 0;
        }

        /// <summary>
        /// Dispatches callbacks that were previously captured by <see cref="CaptureChanges"/>.
        /// </summary>
        internal void FlushCapturedCallbacks()
        {
            for (int i = 0; i < m_changedScratch.Count; i++)
            {
                m_changedScratch[i].RaiseChangedEvent();
            }

            m_changedScratch.Clear();
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
