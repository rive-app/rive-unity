using UnityEngine;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Rive.Producer;
using Rive.Utils;
using Rive.Host;

namespace Rive
{
    /// <summary>
    /// Represents a Rive StateMachine from an Artboard. A StateMachine contains Inputs.
    /// </summary>
    public class StateMachine : IDisposable
    {
        private readonly NativeStateMachineHandle m_nativeStateMachine;
        private ViewModelInstance m_currentViewModelInstance;

        // The artboard that instanced this state machine.
        private readonly Artboard m_artboard;

        // Strong refs + short-circuit cache for global view model instances on this SM. The key is the global view model name.
        private readonly Dictionary<string, ViewModelInstance> m_globalViewModelInstances =
            new Dictionary<string, ViewModelInstance>();

        // Reused across binds to avoid allocating a new list per call.
        private readonly List<KeyValuePair<string, ViewModelInstance>> m_appliedGlobalViewModelInstances =
            new List<KeyValuePair<string, ViewModelInstance>>();
        // The list is filled while the caller waits, so it must not be shared
        // with another caller. Reuse one per calling thread without a lock.
        [ThreadStatic]
        private static List<ReportedEventData> t_reportedEventScratch;

        private readonly string m_stateMachineName;
        private readonly NativeLifetime m_lifetime;
        // Asked for once. A state machine's inputs never change.
        private StateMachineNative.InputInfo[] m_inputs;
        private bool m_isDisposed = false;

        internal NativeStateMachineHandle NativeStateMachine => m_nativeStateMachine;

        /// <summary>
        /// Returns true if the state machine has been disposed.
        /// </summary>
        public bool IsDisposed { get => m_isDisposed; }

        internal StateMachine(NativeStateMachineHandle nativeStateMachine, Artboard artboard, string name)
        {
            m_nativeStateMachine = nativeStateMachine;
            m_artboard = artboard;
            m_stateMachineName = name;
            m_lifetime = StateMachineNative.Lifetime(
                new NativeSlot<NativeStateMachineHandle>(nativeStateMachine), artboard?.Lifetime);
        }

        internal NativeLifetime Lifetime => m_lifetime;

        private File RiveFile => m_artboard?.File;

        /// <summary>
        /// Returns false and logs when the state machine has been disposed. Call before any
        /// native access so we never P/Invoke through a dangling pointer.
        /// </summary>
        private bool IsNativeStateMachineValid()
        {
            if (m_isDisposed)
            {
                DebugLogger.Instance.LogError("Attempting to use a disposed StateMachine.");
                return false;
            }
            return true;
        }

        private static readonly IReadOnlyDictionary<string, ViewModelInstance> s_emptyGlobalViewModelInstances =
            new Dictionary<string, ViewModelInstance>();

        /// <summary>
        /// True when the instance can be handed to native code. A disposed instance's handle no longer resolves.
        /// </summary>
        private static bool IsViewModelInstanceBindable(ViewModelInstance instance)
        {
            return instance != null && !instance.IsDisposed;
        }

        /// <summary>
        /// If the main view model is null, don't prepare one now; Bind() will automatically use the artboard's default if it exists. 
        /// If the instance has already been disposed, it can't be used.
        /// </summary>
        private static bool IsMainViewModelInstanceAcceptable(ViewModelInstance instance)
        {
            if (instance != null && instance.IsDisposed)
            {
                DebugLogger.Instance.LogError($"{nameof(ViewModelInstance)} has been disposed.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// The file's global view model names, or an empty list once the file has been collected.
        /// </summary>
        /// <remarks>
        /// Read once per bind and passed down from there, so the error below is logged once per
        /// failed call rather than once per name looked up.
        /// </remarks>
        private IReadOnlyList<string> GlobalViewModelNames
        {
            get
            {
                var file = RiveFile;
                if (file != null)
                {
                    return file.GlobalViewModelNames;
                }

                // Every global lookup is resolved against these names, so losing the file turns each
                // one into a silent no-op.
                DebugLogger.Instance.LogError(
                    "Cannot resolve global view models because the Rive file has been collected. Keep a reference to the File for as long as the state machine is in use.");
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Dispose of the StateMachine and release native resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!m_isDisposed)
            {
                if (disposing)
                {
                    // Hold refs to user-supplied / fetched instances but never dispose them,
                    // the same instance may be shared across widgets/artboards etc.
                    m_globalViewModelInstances.Clear();
                    m_appliedGlobalViewModelInstances.Clear();
                    m_currentViewModelInstance = null;
                }

                m_lifetime.ReleaseOwner();
                m_isDisposed = true;
            }
        }

        ~StateMachine()
        {
            Dispose(false);
        }

        public string Name => m_stateMachineName;

        /// <summary>
        /// The current ViewModelInstance set as the data context of the StateMachine.
        /// </summary>
        public ViewModelInstance ViewModelInstance
        {
            get { return m_currentViewModelInstance; }
        }

        public bool Advance(float seconds)
        {
            return StateMachineNative.Advance(m_nativeStateMachine, seconds);
        }

        /// The number of Inputs stored in the StateMachine.
        [Obsolete(ObsoleteMessages.Inputs)]
        public uint InputCount()
        {
            return (uint)InputInfos.Length;
        }

        private StateMachineNative.InputInfo[] InputInfos
        {
            get
            {
                if (m_inputs == null)
                {
                    m_inputs = StateMachineNative.ListInputs(m_nativeStateMachine);
                }
                return m_inputs;
            }
        }

        internal StateMachineNative.InputInfo InputAt(NativeSMIInputHandle input)
        {
            StateMachineNative.InputInfo[] inputs = InputInfos;
            return input.IsValid && input.Index < inputs.Length ? inputs[input.Index] : default;
        }

        // The first input with that name and kind, like core's lookups.
        private NativeSMIInputHandle FindInput(string name, ArtboardNative.InputKind kind)
        {
            StateMachineNative.InputInfo[] inputs = InputInfos;
            for (uint i = 0; i < inputs.Length; i++)
            {
                if (inputs[i].Kind == kind && inputs[i].Name == name)
                {
                    return new NativeSMIInputHandle(i);
                }
            }
            return default;
        }

        /// The SMIInput at the given index.
        [Obsolete(ObsoleteMessages.Inputs)]
        public SMIInput Input(uint index)
        {
            return index < InputInfos.Length ? new SMIInput(new NativeSMIInputHandle(index), this) : null;
        }

        [Obsolete(ObsoleteMessages.Inputs)]
        private SMIInput ConvertInput(SMIInput input)
        {
            if (input.IsBoolean)
            {
                return new SMIBool(input.NativeSMI, this);
            }
            else if (input.IsTrigger)
            {
                return new SMITrigger(input.NativeSMI, this);
            }
            else if (input.IsNumber)
            {
                return new SMINumber(input.NativeSMI, this);
            }
            else
            {
                return null;
            }
        }

        /// A list of all the SMIInputs stored in the StateMachine.
        [Obsolete(ObsoleteMessages.Inputs)]
        public List<SMIInput> Inputs()
        {
            var list = new List<SMIInput>();
            for (uint i = 0; i < InputCount(); i++)
            {
                var inputAtIndex = Input(i);
                if (inputAtIndex == null)
                {
                    continue;
                }

                var converted = ConvertInput(inputAtIndex);
                if (converted != null)
                {
                    list.Add(converted);
                }
            }

            return list;
        }

        /// <summary>
        /// Get a SMIBool by name.
        /// </summary>
        /// <remarks>
        /// A SMIBool.value is a boolean that can be get/set
        /// </remarks>
        [Obsolete(ObsoleteMessages.Inputs)]
        public SMIBool GetBool(string name)
        {
            NativeSMIInputHandle input = FindInput(name, ArtboardNative.InputKind.Boolean);
            if (input.IsValid)
                return new SMIBool(input, this);
            DebugLogger.Instance.Log($"No SMIBool found with name: {name}.");
            return null;
        }

        /// <summary>
        /// Get a SMITrigger by name.
        /// </summary>
        /// <remarks>
        /// A SMITrigger contains a fire method to trigger.
        /// </remarks>
        [Obsolete(ObsoleteMessages.Inputs)]
        public SMITrigger GetTrigger(string name)
        {
            NativeSMIInputHandle input = FindInput(name, ArtboardNative.InputKind.Trigger);
            if (input.IsValid)
                return new SMITrigger(input, this);
            DebugLogger.Instance.Log($"No SMITrigger found with name: {name}.");
            return null;
        }

        /// <summary>
        /// Get a SMINumber by name.
        /// </summary>
        /// <remarks>
        /// A SMINumber.value is a float that can be get/set
        /// </remarks>
        [Obsolete(ObsoleteMessages.Inputs)]
        public SMINumber GetNumber(string name)
        {
            NativeSMIInputHandle input = FindInput(name, ArtboardNative.InputKind.Number);
            if (input.IsValid)
                return new SMINumber(input, this);
            DebugLogger.Instance.Log($"No SMINumber found with name: {name}.");
            return null;
        }

        /// <summary>
        /// Move the pointer to the given position
        /// </summary>
        public HitResult PointerMove(Vector2 position, int pointerId = 0)
        {
            return (HitResult)StateMachineNative.PointerMoveStateMachineWithHit(m_nativeStateMachine, position.x, position.y, pointerId);
        }

        /// <summary>
        /// Press the pointer at the given position
        /// </summary>
        public HitResult PointerDown(Vector2 position, int pointerId = 0)
        {
            return (HitResult)StateMachineNative.PointerDownStateMachineWithHit(m_nativeStateMachine, position.x, position.y, pointerId);
        }

        /// <summary>
        /// Release the pointer at the given position
        /// </summary>
        public HitResult PointerUp(Vector2 position, int pointerId = 0)
        {
            return (HitResult)StateMachineNative.PointerUpStateMachineWithHit(m_nativeStateMachine, position.x, position.y, pointerId);
        }

        /// <summary>
        /// Exit the pointer at the given position
        /// </summary>
        public HitResult PointerExit(Vector2 position, int pointerId = 0)
        {
            return (HitResult)StateMachineNative.PointerExitStateMachineWithHit(m_nativeStateMachine, position.x, position.y, pointerId);
        }

        /// <summary>
        /// Performs a hit test at the given position
        /// </summary>
        /// <param name="position">The position to test in local coordinates</param>
        /// <returns>True if the position hits a component with a listener, false otherwise</returns>
        public bool HitTest(Vector2 position)
        {
            return StateMachineNative.HitTestStateMachine(m_nativeStateMachine, position.x, position.y);
        }

        /// <summary>
        /// A list of all the reported events received in the past frame.
        /// </summary>
        public List<ReportedEvent> ReportedEvents()
        {
            var list = new List<ReportedEvent>();
            ReportedEvents(list);
            return list;
        }



        /// <summary>
        /// Fetches the reported events received by the StateMachine in the past frame and populates the given list.
        /// </summary>
        /// <param name="reportedEvents"> The list to populate with reported events. </param>
        public void ReportedEvents(List<ReportedEvent> reportedEvents)
        {
            List<ReportedEventData> scratch = t_reportedEventScratch;
            if (scratch == null)
            {
                scratch = new List<ReportedEventData>();
                t_reportedEventScratch = scratch;
            }
            scratch.Clear();
            try
            {
                CollectReportedEvents(scratch);
                for (int i = 0; i < scratch.Count; i++)
                {
                    reportedEvents.Add(ReportedEvent.GetPooled(scratch[i]));
                }
            }
            finally
            {
                scratch.Clear();
            }
        }

        /// <summary>
        /// Adds what the last advance reported. Waits for it.
        /// </summary>
        internal void CollectReportedEvents(List<ReportedEventData> into)
        {
            StateMachineNative.ReportedEvents(m_nativeStateMachine, into);
        }

        /// <summary>
        /// Enumerates through all reported events received by the StateMachine in the past frame.
        /// </summary>
        /// <returns>An IEnumerable of ReportedEvents</returns>
        public IEnumerable<ReportedEvent> EnumerateReportedEvents()
        {
            var data = new List<ReportedEventData>();
            CollectReportedEvents(data);
            for (int i = 0; i < data.Count; i++)
            {
                yield return ReportedEvent.GetPooled(data[i]);
            }
        }

        /// <summary>
        /// Sets the StateMachine's data context to use the provided ViewModelInstance.
        /// </summary>
        /// <remarks>
        /// If <paramref name="viewModelInstance"/> is null, a default main view model instance will be created if the artboard supports it, along with any global view model instances that are empty.
        /// Otherwise, the provided instance will be used as the main view model instance.
        /// </remarks>
        /// <param name="viewModelInstance">The ViewModelInstance to bind, or null to auto-fill defaults.</param>
        public void BindViewModelInstance(ViewModelInstance viewModelInstance)
        {
            if (!IsNativeStateMachineValid())
            {
                return;
            }

            if (!IsMainViewModelInstanceAcceptable(viewModelInstance))
            {
                return;
            }

            // With an instance, the bind is queued and ViewModelInstance is
            // known now. With null, native makes the default, so it waits.
            if (viewModelInstance != null)
            {
                StateMachineNative.BindInstanceLater(m_nativeStateMachine, viewModelInstance.NativeHandle);
                m_currentViewModelInstance = viewModelInstance;
                return;
            }

            Bind();
        }

        /// <summary>
        /// Sets the main view model instance together with the given global view model instances.
        /// </summary>
        /// <remarks>
        /// If <paramref name="main"/> is null, the state machine will create a default main view model instance if the artboard allows it.
        /// For global view models, any names not listed in the dictionary are automatically filled in with default instances.
        /// If you rebind later and leave out a key, that global will keep whatever instance it already had.
        ///
        /// The operation is all-or-nothing: every dictionary entry is checked against <see cref="File.GlobalViewModelNames"/> before making any changes.
        /// If there's any error, nothing is updated and the method returns false.
        /// </remarks>
        /// <param name="main">The main ViewModelInstance to bind, or null to leave/auto-fill main.</param>
        /// <param name="globalViewModelInstancesByName">
        /// Global view model names (from <see cref="File.GlobalViewModelNames"/>) to instances.
        /// </param>
        /// <returns>
        /// Returns true if all entries in the map were set successfully. 
        /// Returns false if the main instance is disposed, or if any global name doesn't exist, or if any value is null or disposed. 
        /// In those cases, the state machine will not be updated.
        /// </returns>
        public bool BindViewModelInstance(
            ViewModelInstance main,
            IReadOnlyDictionary<string, ViewModelInstance> globalViewModelInstancesByName)
        {
            if (!IsNativeStateMachineValid())
            {
                return false;
            }

            if (!IsMainViewModelInstanceAcceptable(main))
            {
                return false;
            }

            IReadOnlyDictionary<string, ViewModelInstance> globals =
                globalViewModelInstancesByName ?? s_emptyGlobalViewModelInstances;
            IReadOnlyList<string> names = GlobalViewModelNames;

            if (!ValidateGlobalViewModelInstances(globals, names))
            {
                return false;
            }

            if (main != null)
            {
                SetViewModelInstanceWithoutBind(main);
            }

            ApplyGlobalViewModelInstancesWithoutBind(globals, names);
            Bind();
            CacheAppliedGlobalViewModelInstances();
            return true;
        }

        /// <summary>
        /// Returns the global view model instance currently bound under the given name, or null if
        /// none has been set. 
        /// </summary>
        /// <param name="name">The name of the global view model from <see cref="File.GlobalViewModelNames"/>.</param>
        public ViewModelInstance GetGlobalViewModelInstance(string name)
        {
            if (!IsNativeStateMachineValid() || string.IsNullOrEmpty(name))
            {
                return null;
            }

            if (m_globalViewModelInstances.TryGetValue(name, out var cached))
            {
                if (!cached.IsDisposed)
                {
                    return cached;
                }

                // The caller disposed an instance we handed out. Drop the entry so the read below
                // resolves a usable wrapper instead of returning one with a closed handle.
                m_globalViewModelInstances.Remove(name);
            }

            var instance = ViewModelInstance.GetOrCreateFromHandle(
                StateMachineNative.GetGlobalViewModelInstanceFromStateMachine(m_nativeStateMachine, name), RiveFile);
            if (instance == null)
            {
                return null;
            }

            CacheGlobalViewModelInstance(name, instance);
            return instance;
        }

        /// <summary>
        /// Sets the main view model instance without rebinding. Call <see cref="Bind"/> to apply.
        /// </summary>
        private void SetViewModelInstanceWithoutBind(ViewModelInstance instance)
        {
            if (!IsNativeStateMachineValid() || !IsViewModelInstanceBindable(instance))
            {
                return;
            }

            StateMachineNative.SetViewModelInstanceOnStateMachine(m_nativeStateMachine, instance.NativeHandle);
        }

        /// <summary>
        /// Sets a global view model instance without rebinding. Does not update the C# cache;
        /// call <see cref="CacheGlobalViewModelInstance"/> only after a successful <see cref="Bind"/>.
        /// </summary>
        private bool SetGlobalViewModelInstanceWithoutBind(
            string name,
            ViewModelInstance instance,
            IReadOnlyList<string> availableNames)
        {
            if (!IsNativeStateMachineValid() ||
                string.IsNullOrEmpty(name) ||
                !IsViewModelInstanceBindable(instance))
            {
                return false;
            }

            bool ok = StateMachineNative.SetGlobalViewModelInstanceOnStateMachine(
                m_nativeStateMachine,
                name,
                instance.NativeHandle);

            if (!ok)
            {
                DebugLogger.Instance.LogError(
                    $"No global view model named '{name}'. Available: {FormatAvailableGlobalNames(availableNames)}.");
            }

            return ok;
        }

        /// <summary>
        /// Records a global instance in the per-SM cache after a successful Bind.
        /// </summary>
        private void CacheGlobalViewModelInstance(string name, ViewModelInstance instance)
        {
            if (string.IsNullOrEmpty(name) || instance == null)
            {
                return;
            }

            m_globalViewModelInstances[name] = instance;
        }

        /// <summary>
        /// Applies any staged view model instance changes by rebinding once.
        /// Creates default instances for any empty main or global slots.
        /// </summary>
        /// Binds without waiting, making defaults like a null bind. The
        /// instance it leaves lands with the reply.
        internal void BindWithoutWaiting()
        {
            if (!IsNativeStateMachineValid())
            {
                return;
            }
            StateMachineNative.BindLater(m_nativeStateMachine, bound =>
                m_currentViewModelInstance = ViewModelInstance.GetOrCreateFromHandle(bound, RiveFile));
        }

        private void Bind()
        {
            if (!IsNativeStateMachineValid())
            {
                return;
            }

            // Null when there's no main after the bind, e.g. the artboard has no default view model.
            m_currentViewModelInstance = ViewModelInstance.GetOrCreateFromHandle(StateMachineNative.BindStateMachine(m_nativeStateMachine), RiveFile);
        }

        /// <summary>
        /// Returns false (and logs) if any map entry has an unknown name or a null/disposed instance.
        /// Does not touch native state.
        /// </summary>
        private static bool ValidateGlobalViewModelInstances(
            IReadOnlyDictionary<string, ViewModelInstance> globalViewModelInstancesByName,
            IReadOnlyList<string> availableNames)
        {
            if (globalViewModelInstancesByName == null || globalViewModelInstancesByName.Count == 0)
            {
                return true;
            }

            bool allValid = true;
            foreach (var kvp in globalViewModelInstancesByName)
            {
                string name = kvp.Key;
                ViewModelInstance instance = kvp.Value;

                if (!IsGlobalViewModelName(availableNames, name))
                {
                    DebugLogger.Instance.LogError(
                        $"No global view model named '{name ?? "(null)"}'. Available: {FormatAvailableGlobalNames(availableNames)}.");
                    allValid = false;
                    continue;
                }

                if (instance == null)
                {
                    DebugLogger.Instance.LogError(
                        $"Global view model '{name}' was given a null instance.");
                    allValid = false;
                }
                else if (instance.IsDisposed)
                {
                    DebugLogger.Instance.LogError(
                        $"Global view model '{name}' was given a disposed instance.");
                    allValid = false;
                }
            }

            return allValid;
        }

        /// <summary>
        /// Stages map entries that differ from the cache. Caller must have validated the map first.
        /// </summary>
        private void ApplyGlobalViewModelInstancesWithoutBind(
            IReadOnlyDictionary<string, ViewModelInstance> globalViewModelInstancesByName,
            IReadOnlyList<string> names)
        {
            m_appliedGlobalViewModelInstances.Clear();

            if (globalViewModelInstancesByName == null || globalViewModelInstancesByName.Count == 0)
            {
                return;
            }

            for (int i = 0; i < names.Count; i++)
            {
                string name = names[i];
                if (!globalViewModelInstancesByName.TryGetValue(name, out var instance))
                {
                    continue;
                }

                if (m_globalViewModelInstances.TryGetValue(name, out var cached) &&
                    ReferenceEquals(cached, instance))
                {
                    m_appliedGlobalViewModelInstances.Add(
                        new KeyValuePair<string, ViewModelInstance>(name, instance));
                    continue;
                }

                if (SetGlobalViewModelInstanceWithoutBind(name, instance, names))
                {
                    m_appliedGlobalViewModelInstances.Add(
                        new KeyValuePair<string, ViewModelInstance>(name, instance));
                }
            }
        }

        /// <summary>
        /// Saves the entries from the last time <see cref="ApplyGlobalViewModelInstancesWithoutBind"/> was called.
        /// Only entries that were successfully set are saved, so the cache will always match the state machine's actual bindings.
        /// </summary>
        private void CacheAppliedGlobalViewModelInstances()
        {
            for (int i = 0; i < m_appliedGlobalViewModelInstances.Count; i++)
            {
                var applied = m_appliedGlobalViewModelInstances[i];
                CacheGlobalViewModelInstance(applied.Key, applied.Value);
            }
            m_appliedGlobalViewModelInstances.Clear();
        }

        private static bool IsGlobalViewModelName(IReadOnlyList<string> availableNames, string name)
        {
            if (name == null)
            {
                return false;
            }

            for (int i = 0; i < availableNames.Count; i++)
            {
                if (availableNames[i] == name)
                {
                    return true;
                }
            }
            return false;
        }

        private static string FormatAvailableGlobalNames(IReadOnlyList<string> names)
        {
            if (names == null || names.Count == 0)
            {
                return "(none)";
            }

            var sb = new StringBuilder();
            for (int i = 0; i < names.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }
                sb.Append(names[i]);
            }
            return sb.ToString();
        }
    }
}
