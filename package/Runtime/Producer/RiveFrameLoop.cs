using Rive.Host;
using System;
using System.Collections.Generic;
using Rive.Utils;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Rive.Producer
{
    /// <summary>
    /// The one per-frame driver. Three steps in Unity's player loop:
    /// Early, just before scripts' Update, delivers async results and view
    /// model values. Update, just after scripts' Update, runs the panel pass,
    /// so a Tick or write from any Update goes out that frame. Late, just
    /// after scripts' LateUpdate, sends this frame's work to the render thread.
    ///
    /// The components assembly fills in Update and Late when its Orchestrator
    /// starts. Without it, Early still runs, which is all the low-level API
    /// needs. Main thread only.
    /// </summary>
    internal static class RiveFrameLoop
    {
        internal struct RiveEarlyUpdate { }
        internal struct RiveUpdate { }
        internal struct RiveLateUpdate { }

        private static readonly List<Action> s_early = new List<Action>();
        private static object s_owner;
        private static Action s_update;
        private static Action s_late;
        private static bool s_installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_early.Clear();
            s_owner = null;
            s_update = null;
            s_late = null;
            s_installed = false;
        }

        /// Installs the steps once per play session. Outside play mode in the
        /// editor, Early runs from the editor update instead. verify checks
        /// the real loop, for when something reset it under us (Unity as a
        /// Library relaunching, another package's SetPlayerLoop). It
        /// allocates, so it's for startup points, not every call.
        internal static void Ensure(bool verify = false)
        {
            if (!CommandTransport.IsMainThread)
            {
                return;
            }
            if (s_installed && (!verify || IsInstalled()))
            {
                return;
            }
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorApplication.update -= RunEarly;
                EditorApplication.update += RunEarly;
                return;
            }
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
#endif
            PlayerLoopSystem loop = PlayerLoop.GetCurrentPlayerLoop();
            Remove(ref loop);
            Insert(ref loop, typeof(Update), typeof(Update.ScriptRunBehaviourUpdate),
                Step<RiveEarlyUpdate>(RunEarly), Step<RiveUpdate>(RunUpdate));
            Insert(ref loop, typeof(PreLateUpdate), typeof(PreLateUpdate.ScriptRunBehaviourLateUpdate),
                default, Step<RiveLateUpdate>(RunLate));
            PlayerLoop.SetPlayerLoop(loop);
            s_installed = true;
        }

        private static bool IsInstalled()
        {
            foreach (PlayerLoopSystem phase in PlayerLoop.GetCurrentPlayerLoop().subSystemList)
            {
                if (phase.subSystemList == null)
                {
                    continue;
                }
                foreach (PlayerLoopSystem system in phase.subSystemList)
                {
                    if (system.type == typeof(RiveUpdate))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// Runs in Early, after async results. Never waits.
        internal static void AddEarly(Action poll)
        {
            if (!s_early.Contains(poll))
            {
                s_early.Add(poll);
            }
        }

        /// The components assembly's passes. One owner at a time.
        internal static void SetPasses(object owner, Action update, Action late)
        {
            s_owner = owner;
            s_update = update;
            s_late = late;
        }

        internal static void ClearPasses(object owner)
        {
            if (!ReferenceEquals(s_owner, owner))
            {
                return;
            }
            s_owner = null;
            s_update = null;
            s_late = null;
        }

        private static PlayerLoopSystem Step<T>(PlayerLoopSystem.UpdateFunction run)
        {
            return new PlayerLoopSystem { type = typeof(T), updateDelegate = run };
        }

        private static void RunEarly()
        {
            CommandTransport.Drain();
            CommandTransport.DrainMainThread();
            for (int i = 0; i < s_early.Count; i++)
            {
                Run(s_early[i]);
            }
        }

        private static void RunUpdate()
        {
            Run(s_update);
        }

        private static void RunLate()
        {
            Run(s_late);
            RecordReplayedRenderers();
            // Held writes with nothing after them this frame. Here rather than
            // in a pass, so it runs without the components too.
            CommandTransport.FlushWrites();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Rive's counters for the profiler. Not in release players.
            if (CommandHost.IsRunning)
            {
                HostNative.riveProfilerFrame();
            }
#endif
        }

        private static void RecordReplayedRenderers()
        {
            try
            {
                Renderer.RecordReplayed();
            }
            catch (Exception e)
            {
                DebugLogger.Instance.LogException(e);
            }
        }

        private static void Run(Action step)
        {
            if (step == null)
            {
                return;
            }
            try
            {
                step();
            }
            catch (Exception e)
            {
                DebugLogger.Instance.LogException(e);
            }
        }

        // before and after go either side of anchor inside phase. A default
        // system is skipped.
        private static void Insert(
            ref PlayerLoopSystem loop,
            Type phase,
            Type anchor,
            PlayerLoopSystem before,
            PlayerLoopSystem after)
        {
            PlayerLoopSystem[] phases = loop.subSystemList;
            for (int i = 0; i < phases.Length; i++)
            {
                if (phases[i].type != phase)
                {
                    continue;
                }
                var systems = new List<PlayerLoopSystem>(phases[i].subSystemList);
                int at = systems.FindIndex(system => system.type == anchor);
                if (at < 0)
                {
                    // Something replaced it, so go at the end rather than not at all.
                    at = systems.Count - 1;
                }
                if (after.type != null)
                {
                    systems.Insert(at + 1, after);
                }
                if (before.type != null)
                {
                    systems.Insert(at, before);
                }
                phases[i].subSystemList = systems.ToArray();
                return;
            }
        }

        // With domain reload off the previous session's steps are still there.
        private static void Remove(ref PlayerLoopSystem loop)
        {
            PlayerLoopSystem[] phases = loop.subSystemList;
            for (int i = 0; i < phases.Length; i++)
            {
                if (phases[i].subSystemList == null)
                {
                    continue;
                }
                var systems = new List<PlayerLoopSystem>(phases[i].subSystemList);
                systems.RemoveAll(system =>
                    system.type == typeof(RiveEarlyUpdate) ||
                    system.type == typeof(RiveUpdate) ||
                    system.type == typeof(RiveLateUpdate));
                phases[i].subSystemList = systems.ToArray();
            }
        }

#if UNITY_EDITOR
        // Leaves edit mode with Unity's loop as it found it.
        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.ExitingPlayMode)
            {
                return;
            }
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            PlayerLoopSystem loop = PlayerLoop.GetCurrentPlayerLoop();
            Remove(ref loop);
            PlayerLoop.SetPlayerLoop(loop);
            s_installed = false;
        }
#endif
    }
}
