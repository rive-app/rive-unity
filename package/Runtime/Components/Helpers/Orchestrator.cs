using System;
using System.Collections;
using System.Collections.Generic;
using Rive.Producer;
using Rive.Utils;
using UnityEngine;
using Rive.Host;

namespace Rive.Components
{
    /// <summary>
    /// Central per-frame orchestrator for ticking panels/widgets and then processing databinding callbacks.
    ///
    /// This enables "command-server-like" batching:
    /// - panels register/unregister with the orchestrator
    /// - on each frame, every panel with time waiting for it is advanced, whether that time came from the frame or from a Tick call
    /// - changed properties are triggered after all panels have been ticked.
    /// </summary>
    internal sealed class Orchestrator : MonoBehaviour
    {

        private static Orchestrator s_instance;

        private static readonly List<RenderTargetStrategy> s_registeredRenderTargetStrategies = new List<RenderTargetStrategy>();

        public static Orchestrator Instance
        {
            get
            {
                // This check uses Unity's overloaded `==` operator, so it
                // returns false for destroyed MonoBehaviours and we lazily create a fresh one.
                // We don't add a boolean `s_isDestroyed` style flag here because it survives
                // Unity-as-a-Library reloads (RuntimeInitializeOnLoadMethod doesn't fire again
                // on subsequent runEmbedded calls) and would prevent the Instance getter from
                // creating a fresh one.
                if (s_instance != null)
                {
                    return s_instance;
                }

                if (!Application.isPlaying)
                {
                    return null;
                }

#if UNITY_EDITOR

                if (s_isApplicationQuitting)
                {
                    return null;
                }
#endif

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
        private readonly List<RivePanel> m_panelTickList = new List<RivePanel>(); // Used to store the panels to tick in the TickAutoPanels method to avoid modifying the HashSet while iterating.
        private readonly PointerInputModule m_pointerInput = new PointerInputModule();
        // Every phase runs across these in order. Pointer events land after the
        // capture that came before them whatever the order here, see
        // PointerInputModule.OnLanded.
        private readonly List<FrameModule> m_modules = new List<FrameModule>();

        internal PointerInputModule PointerInput => m_pointerInput;

        private void Awake()
        {
#if RIVE_USING_EXPERIMENTAL
            m_modules.Add(new RenderImageFrameModule());
#endif
            m_modules.Add(new PropertyCaptureFrameModule());
            m_modules.Add(m_pointerInput);

            // The core frame loop runs these, after every script's Update
            // and LateUpdate, so the order is fixed rather than whatever
            // Unity picks among components.
            RiveFrameLoop.SetPasses(this, RunUpdatePassIfEnabled, RunLatePassIfEnabled);
            RiveFrameLoop.Ensure(verify: true);
        }

        private void RunUpdatePassIfEnabled()
        {
            if (this != null && isActiveAndEnabled)
            {
                RunUpdatePass();
            }
        }

        private void RunLatePassIfEnabled()
        {
            if (this != null && isActiveAndEnabled)
            {
                RunLatePass();
            }
        }

        internal event Action OnPostRenderPreparation;

        private void OnDestroy()
        {
            RiveFrameLoop.ClearPasses(this);
            for (int i = 0; i < m_modules.Count; i++)
            {
                m_modules[i].Dispose();
            }


            if (s_instance == this)
            {
                s_instance = null;

#if RIVE_USING_EXPERIMENTAL
            // Session teardown: dispose any RenderTexture-backed
            // images still bound.
            RenderTextureImageManager.Instance.Clear();
#endif

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
            for (int i = 0; i < m_modules.Count; i++)
            {
                m_modules[i].OnPanelRemoved(panel);
            }
            GpuCanvasResources.RequestFlush();
        }

        internal void QueuePointerInput(
            RivePanel panel,
            RiveWidget widget,
            Vector2 point,
            int pointerId,
            RiveWidget.PointerEventKind kind)
        {
            m_pointerInput.Queue(panel, widget, point, pointerId, kind);
        }

        internal void JoinPointerInputs()
        {
            m_pointerInput.Join();
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
                    GpuCanvasResources.RequestFlush();
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

        /// <summary>
        /// Advances every panel that has time waiting for it. Auto panels get the frame time, Manual panels get whatever their Tick calls queued.
        ///
        /// Everything is submitted before anything is waited for. Waiting inside the walk would hold up every panel behind it in the list.
        /// </summary>
        // The tick pass's sends: each panel's advance and the capture after.
        private static readonly Unity.Profiling.ProfilerMarker s_sendMarker =
            new Unity.Profiling.ProfilerMarker("Rive.Send");

        internal bool RunTickPass()
        {
            if (m_registeredPanels.Count == 0)
            {
                return false;
            }

            float deltaTime = Time.deltaTime;
            bool tickedAny = false;
            bool advancedAny = false;

            m_panelTickList.Clear();
            m_panelTickList.AddRange(m_registeredPanels);

            bool waitsForChanges = false;
            for (int i = 0; i < m_panelTickList.Count; i++)
            {
                var panel = m_panelTickList[i];
                if (panel != null && panel.isActiveAndEnabled &&
                    panel.ThreadingMode == ThreadingMode.MainThread &&
                    (panel.UpdateMode == RivePanel.PanelUpdateMode.Auto || panel.HasTimeWaiting))
                {
                    waitsForChanges = true;
                    break;
                }
            }

            if (waitsForChanges && PropertyCallbacksHub.Instance.ProducerCapturePending)
            {
                JoinModulesForSync();
            }

            // Take what landed while the main thread was elsewhere, so a panel
            // that has been waited for can advance again this frame.
            for (int i = 0; i < m_panelTickList.Count; i++)
            {
                m_panelTickList[i]?.PollAdvance();
            }

            s_sendMarker.Begin();
            for (int i = 0; i < m_panelTickList.Count; i++)
            {
                var panel = m_panelTickList[i];

                if (panel == null || !panel.isActiveAndEnabled)
                {
                    continue;
                }

                if (panel.UpdateMode != RivePanel.PanelUpdateMode.Auto && !panel.HasTimeWaiting)
                {
                    continue;
                }

                tickedAny = true;

                try
                {
                    advancedAny |= panel.SubmitTick(deltaTime);
                }
                catch (System.Exception e)
                {
                    DebugLogger.Instance.LogException(e);
                }
            }

            for (int i = 0; i < m_modules.Count; i++)
            {
                m_modules[i].Submit(advancedAny);
            }
            s_sendMarker.End();

            for (int i = 0; i < m_panelTickList.Count; i++)
            {
                var panel = m_panelTickList[i];

                if (panel == null || panel.ThreadingMode != ThreadingMode.MainThread)
                {
                    continue;
                }

                try
                {
                    using (CommandTransport.AllowWait("sync panel join"))
                    {
                        panel.JoinAdvance();
                    }
                }
                catch (System.Exception e)
                {
                    DebugLogger.Instance.LogException(e);
                }
            }

            if (waitsForChanges)
            {
                JoinModulesForSync();
            }

            m_panelTickList.Clear();

            return tickedAny;
        }

        private void DispatchReadyCallbacks()
        {
            for (int i = 0; i < m_modules.Count; i++)
            {
                m_modules[i].Poll();
            }
        }

        // Synchronous panels read results straight after this.
        private void JoinModulesForSync()
        {
            using (CommandTransport.AllowWait("sync module join"))
            {
                for (int i = 0; i < m_modules.Count; i++)
                {
                    m_modules[i].JoinForSync();
                }
            }
        }

        internal void RunUpdatePass()
        {
            // Async results first, so a task finished on the producer is
            // visible to everything that runs after it this frame. Outside the
            // guard, it runs the caller's continuations.
            CommandTransport.DrainMainThread();

            using (CommandTransport.NoWait("orchestrator update"))
            {
                DispatchReadyCallbacks();
                for (int i = 0; i < m_modules.Count; i++)
                {
                    m_modules[i].BeforeTick();
                }
                RunTickPass();
                DispatchReadyCallbacks();
            }
        }

        /// <summary>
        /// Captures changed properties and triggers Unity callbacks after an immediate state machine advance.
        /// </summary>
        internal void FlushPropertyCallbacksForImmediateAdvance()
        {
            FlushPropertyCallbacksForChangedProperties();
        }

        /// <summary>
        /// Captures changed properties and triggers the Unity callbacks, after anything captured before. This only triggers/checks for properties the user has subscribed to in code.
        /// </summary>
        private void FlushPropertyCallbacksForChangedProperties()
        {
            PropertyCallbacksHub.Instance.CaptureChanges();
        }



        internal void RunLatePass()
        {
            // The producer has usually taken this frame's work by now, so
            // whatever goes to the render thread goes with this frame.
            for (int i = 0; i < m_modules.Count; i++)
            {
                m_modules[i].LateFrame();
            }

            // Prepare batched rendering after ticking panels.
            // This is intentionally called every frame so batched render requests (e.g. from
            // registration/size/layout changes) can be handled even when no panels ticked.
            PrepareRenderTargetsStrategies();

            OnPostRenderPreparation?.Invoke();

            // Anything released this frame may have queued destroys with no
            // frame left to ride on.
            GpuCanvasResources.FlushIfRequested();
        }

#if UNITY_EDITOR
       
        private static bool s_isApplicationQuitting;

         // Resets static state when entering Play Mode in the editor with Domain Reload
         // disabled, so stale editor-only state does not survive across play sessions.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Init()
        {
            s_instance = null;
            s_isApplicationQuitting = false;
            s_registeredRenderTargetStrategies.Clear();

            Application.quitting -= OnApplicationQuitting;
            Application.quitting += OnApplicationQuitting;
        }

        private static void OnApplicationQuitting()
        {
            s_isApplicationQuitting = true;
        }
#endif
    }
}
