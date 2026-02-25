using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rive.Components
{
    /// <summary>
    /// Central per-frame orchestrator for ticking panels/widgets and then processing databinding callbacks.
    ///
    /// This enables "command-server-like" batching:
    /// - panels register/unregister with the orchestrator
    /// - on each frame, auto panels are ticked
    /// - manual ticks execute immediately and notify the orchestrator
    /// - changed properties are triggered after all panels have been ticked.
    /// </summary>
    internal sealed class Orchestrator : MonoBehaviour
    {
        private static bool s_isDestroyed = false;

        private static Orchestrator s_instance;

        private static readonly List<RenderTargetStrategy> s_registeredRenderTargetStrategies = new List<RenderTargetStrategy>();

        public static Orchestrator Instance
        {
            get
            {

                if (s_isDestroyed)
                {
                    return null;
                }

                if (s_instance != null)
                {
                    return s_instance;
                }

                // Ensure the render pipeline handler exists; the orchestrator will be attached there.
                var handler = RenderPipelineHelper.GetOrCreateHandler() as MonoBehaviour;
                if (handler == null)
                {
                    return null;
                }

                s_instance = handler.GetComponent<Orchestrator>();
                if (s_instance == null)
                {
                    s_instance = handler.gameObject.AddComponent<Orchestrator>();
                }

                return s_instance;
            }
        }

        private readonly HashSet<RivePanel> m_registeredPanels = new HashSet<RivePanel>();
        private bool m_tickedThisFrame;

        private void OnDestroy()
        {
            m_tickedThisFrame = false;

            if (s_instance == this && !s_isDestroyed)
            {
                s_instance = null;
                s_isDestroyed = true;
            }
        }

        internal void RegisterPanel(RivePanel panel)
        {
            if (panel == null)
            {
                return;
            }
            m_registeredPanels.Add(panel);
        }

        internal void UnregisterPanel(RivePanel panel)
        {
            if (panel == null)
            {
                return;
            }
            m_registeredPanels.Remove(panel);
        }

        internal void NotifyManualTickOccurred(RivePanel panel)
        {
            if (panel == null)
            {
                return;
            }
            m_tickedThisFrame = true;
        }

        internal static void RegisterRenderTargetStrategy(RenderTargetStrategy strategy)
        {
            if (strategy == null)
            {
                return;
            }

            // Avoid duplicates.
            for (int i = 0; i < s_registeredRenderTargetStrategies.Count; i++)
            {
                if (ReferenceEquals(s_registeredRenderTargetStrategies[i], strategy))
                {
                    return;
                }
            }

            s_registeredRenderTargetStrategies.Add(strategy);
        }

        internal static void UnregisterRenderTargetStrategy(RenderTargetStrategy strategy)
        {
            if (strategy == null)
            {
                return;
            }

            // We set to null rather than remove to avoid shifting indices during reverse iteration.
            for (int i = s_registeredRenderTargetStrategies.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(s_registeredRenderTargetStrategies[i], strategy))
                {
                    s_registeredRenderTargetStrategies[i] = null;
                    return;
                }
            }
        }

        private static void PrepareRenderTargetsStrategies()
        {
            if (s_registeredRenderTargetStrategies.Count == 0)
            {
                return;
            }

            // We iterate in reverse so removals are safe .
            for (int i = s_registeredRenderTargetStrategies.Count - 1; i >= 0; i--)
            {
                var strategy = s_registeredRenderTargetStrategies[i];

                // Accounting for Unity "fake null" when the strategy is destroyed.
                if (strategy == null)
                {
                    s_registeredRenderTargetStrategies.RemoveAt(i);
                    continue;
                }

                strategy.PrepareRenderFromOrchestrator();
            }
        }

        private bool TickAutoPanels()
        {
            // Panels with Auto update mode are ticked here.
            if (m_registeredPanels.Count == 0)
            {
                return false;
            }

            float deltaTime = Time.deltaTime;
            bool tickedAny = false;

            foreach (var panel in m_registeredPanels)
            {
                if (panel == null || !panel.isActiveAndEnabled)
                {
                    continue;
                }

                if (panel.UpdateMode != RivePanel.PanelUpdateMode.Auto)
                {
                    continue;
                }

                tickedAny = true;

                panel.TickImmediate(deltaTime);
            }

            return tickedAny;
        }

        private void Update()
        {
            // We advance any Rive Panels set to Auto update mode. Manual panels notify the orchestrator when they tick via NotifyManualTickOccurred.
            if (TickAutoPanels())
            {
                m_tickedThisFrame = true;
            }

            TriggerCallbacksForChangedProperties();

        }

        /// <summary>
        /// Captures changed properties and triggers the Unity callbacks. This only triggers/checks for properties the user has subscribed to in code.
        /// </summary>
        private void TriggerCallbacksForChangedProperties()
        {
            if (RiveWidget.PropertyCallbackApproach == RiveWidget.DataBindingPropertyCallbackApproach.Orchestrator && m_tickedThisFrame)
            {
                if (PropertyCallbacksHub.Instance.CaptureChanges())
                {
                    PropertyCallbacksHub.Instance.FlushCapturedCallbacks(); // This triggers the Unity callbacks.
                }

            }
            m_tickedThisFrame = false;

        }



        private void LateUpdate()
        {
            // Prepare batched rendering after ticking panels.
            // This is intentionally called every frame so batched render requests (e.g. from
            // registration/size/layout changes) can be handled even when no panels ticked.
            PrepareRenderTargetsStrategies();
        }

#if UNITY_EDITOR
        // We account for Domain Reload in the editor being disabled
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Init()
        {
            s_instance = null;
            s_isDestroyed = false;
            s_registeredRenderTargetStrategies.Clear();
        }
#endif
    }
}

