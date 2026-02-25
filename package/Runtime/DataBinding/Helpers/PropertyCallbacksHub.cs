using System;
using System.Collections.Generic;

namespace Rive
{
    /// <summary>
    /// Centralized callback pump for view model instance properties. Used by the Orchestrator to trigger callbacks for all subscribed properties instead of traversing the ViewModelInstance hierarchy
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

        // Set of subscribed native vm instance property pointers
        private readonly HashSet<IntPtr> m_propertyPointers = new HashSet<IntPtr>();
        private readonly List<ViewModelInstancePrimitiveProperty> m_changedScratch = new List<ViewModelInstancePrimitiveProperty>();
        private readonly List<IntPtr> m_deadPointersScratch = new List<IntPtr>();

        private PropertyCallbacksHub() { }

        /// <summary>
        /// Registers a property with the hub.
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

            m_propertyPointers.Add(ptr);
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

            m_propertyPointers.Remove(instancePropertyPtr);
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

            // Collect and clear changed properties in the same frame as advancement,
            // then raise callbacks later on the main thread.
            foreach (var ptr in m_propertyPointers)
            {
                if (!ViewModelInstanceProperty.TryGetGloballyCachedVMPropertyForPointer(ptr, out var vmProp) ||
                    !(vmProp is ViewModelInstancePrimitiveProperty property) ||
                    property == null)
                {
                    m_deadPointersScratch.Add(ptr);
                    continue;
                }

                if (property.HasChanged)
                {
                    property.ClearChanges();
                    m_changedScratch.Add(property);
                }
            }

            for (int i = 0; i < m_deadPointersScratch.Count; i++)
            {
                m_propertyPointers.Remove(m_deadPointersScratch[i]);
            }

            if (m_changedScratch.Count == 0)
            {
                return false;
            }

            return true;
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

