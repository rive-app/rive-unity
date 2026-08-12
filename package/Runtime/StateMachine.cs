using UnityEngine;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Rive.Utils;

namespace Rive
{
    /// <summary>
    /// Represents a Rive StateMachine from an Artboard. A StateMachine contains Inputs.
    /// </summary>
    public class StateMachine : IDisposable
    {
        private readonly IntPtr m_nativeStateMachine;
        private ViewModelInstance m_currentViewModelInstance;

        // The artboard that instanced this state machine.
        private readonly Artboard m_artboard;

        // Strong refs + short-circuit cache for global view model instances on this SM. The key is the global view model name.
        private readonly Dictionary<string, ViewModelInstance> m_globalViewModelInstances =
            new Dictionary<string, ViewModelInstance>();

        // Reused across binds to avoid allocating a new list per call.
        private readonly List<KeyValuePair<string, ViewModelInstance>> m_appliedGlobalViewModelInstances =
            new List<KeyValuePair<string, ViewModelInstance>>();

        private string m_stateMachineName;
        private bool m_isDisposed = false;

        internal IntPtr NativeStateMachine => m_nativeStateMachine;

        /// <summary>
        /// Returns true if the state machine has been disposed.
        /// </summary>
        public bool IsDisposed { get => m_isDisposed; }

        internal StateMachine(IntPtr nativeStateMachine, Artboard artboard)
        {
            m_nativeStateMachine = nativeStateMachine;
            m_artboard = artboard;
        }

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
        /// True when the instance can be handed to native code. A disposed instance's SafeHandle is
        /// already closed, so marshalling it would throw rather than bind.
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

                if (m_nativeStateMachine != IntPtr.Zero)
                {
                    unrefStateMachine(m_nativeStateMachine);
                }
                m_isDisposed = true;
            }
        }

        ~StateMachine()
        {
            Dispose(false);
        }

        public string Name
        {
            get
            {
                if (m_stateMachineName == null)
                {
                    m_stateMachineName = Marshal.PtrToStringAnsi(stateMachineGetName(m_nativeStateMachine));
                }
                return m_stateMachineName;
            }
        }

        /// <summary>
        /// The current ViewModelInstance set as the data context of the StateMachine.
        /// </summary>
        public ViewModelInstance ViewModelInstance
        {
            get { return m_currentViewModelInstance; }
        }

        public bool Advance(float seconds)
        {
            return advanceStateMachine(m_nativeStateMachine, seconds);
        }

        /// The number of Inputs stored in the StateMachine.
        public uint InputCount()
        {
            return getSMIInputCountStateMachine(m_nativeStateMachine);
        }

        /// The SMIInput at the given index.
        public SMIInput Input(uint index)
        {
            IntPtr ptr = getSMIInputFromIndexStateMachine(m_nativeStateMachine, index);
            return ptr == IntPtr.Zero ? null : new SMIInput(ptr, this);
        }

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
        public SMIBool GetBool(string name)
        {
            IntPtr ptr = getSMIBoolStateMachine(m_nativeStateMachine, name);
            if (ptr != IntPtr.Zero)
                return new SMIBool(ptr, this);
            DebugLogger.Instance.Log($"No SMIBool found with name: {name}.");
            return null;
        }

        /// <summary>
        /// Get a SMITrigger by name.
        /// </summary>
        /// <remarks>
        /// A SMITrigger contains a fire method to trigger.
        /// </remarks>
        public SMITrigger GetTrigger(string name)
        {
            IntPtr ptr = getSMITriggerStateMachine(m_nativeStateMachine, name);
            if (ptr != IntPtr.Zero)
                return new SMITrigger(ptr, this);
            DebugLogger.Instance.Log($"No SMITrigger found with name: {name}.");
            return null;
        }

        /// <summary>
        /// Get a SMINumber by name.
        /// </summary>
        /// <remarks>
        /// A SMINumber.value is a float that can be get/set
        /// </remarks>
        public SMINumber GetNumber(string name)
        {
            IntPtr ptr = getSMINumberStateMachine(m_nativeStateMachine, name);
            if (ptr != IntPtr.Zero)
                return new SMINumber(ptr, this);
            DebugLogger.Instance.Log($"No SMINumber found with name: {name}.");
            return null;
        }

        /// <summary>
        /// Move the pointer to the given position
        /// </summary>
        public HitResult PointerMove(Vector2 position, int pointerId = 0)
        {
            return (HitResult)pointerMoveStateMachineWithHit(m_nativeStateMachine, position.x, position.y, pointerId);
        }

        /// <summary>
        /// Press the pointer at the given position
        /// </summary>
        public HitResult PointerDown(Vector2 position, int pointerId = 0)
        {
            return (HitResult)pointerDownStateMachineWithHit(m_nativeStateMachine, position.x, position.y, pointerId);
        }

        /// <summary>
        /// Release the pointer at the given position
        /// </summary>
        public HitResult PointerUp(Vector2 position, int pointerId = 0)
        {
            return (HitResult)pointerUpStateMachineWithHit(m_nativeStateMachine, position.x, position.y, pointerId);
        }

        /// <summary>
        /// Exit the pointer at the given position
        /// </summary>
        public HitResult PointerExit(Vector2 position, int pointerId = 0)
        {
            return (HitResult)pointerExitStateMachineWithHit(m_nativeStateMachine, position.x, position.y, pointerId);
        }

        /// <summary>
        /// Performs a hit test at the given position
        /// </summary>
        /// <param name="position">The position to test in local coordinates</param>
        /// <returns>True if the position hits a component with a listener, false otherwise</returns>
        public bool HitTest(Vector2 position)
        {
            return hitTestStateMachine(m_nativeStateMachine, position.x, position.y);
        }

        /// <summary>
        /// A list of all the reported events received in the past frame.
        /// </summary>
        public List<ReportedEvent> ReportedEvents()
        {
            uint count = getReportedEventCount(m_nativeStateMachine);
            var list = new List<ReportedEvent>();
            for (uint i = 0; i < count; i++)
            {
                list.Add(ReportedEvent.GetPooled(getReportedEventAt(m_nativeStateMachine, i)));
            }
            return list;
        }



        /// <summary>
        /// Fetches the reported events received by the StateMachine in the past frame and populates the given list.
        /// </summary>
        /// <param name="reportedEvents"> The list to populate with reported events. </param>
        public void ReportedEvents(List<ReportedEvent> reportedEvents)
        {
            uint count = getReportedEventCount(m_nativeStateMachine);
            for (uint i = 0; i < count; i++)
            {
                reportedEvents.Add(ReportedEvent.GetPooled(getReportedEventAt(m_nativeStateMachine, i)));
            }
        }

        /// <summary>
        /// Enumerates through all reported events received by the StateMachine in the past frame.
        /// </summary>
        /// <returns>An IEnumerable of ReportedEvents</returns>
        public IEnumerable<ReportedEvent> EnumerateReportedEvents()
        {
            uint count = getReportedEventCount(m_nativeStateMachine);
            for (uint i = 0; i < count; i++)
            {
                yield return ReportedEvent.GetPooled(getReportedEventAt(m_nativeStateMachine, i));
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

            if (viewModelInstance != null)
            {
                SetViewModelInstanceWithoutBind(viewModelInstance);
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

            IntPtr ptr = getGlobalViewModelInstanceFromStateMachine(m_nativeStateMachine, name);
            if (ptr == IntPtr.Zero)
            {
                return null;
            }

            var instance = ViewModelInstance.GetOrCreateFromPointer(ptr, RiveFile);
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

            setViewModelInstanceOnStateMachine(m_nativeStateMachine, instance.NativeSafeHandle);
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

            bool ok = setGlobalViewModelInstanceOnStateMachine(
                m_nativeStateMachine,
                name,
                instance.NativeSafeHandle);

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
        private void Bind()
        {
            if (!IsNativeStateMachineValid())
            {
                return;
            }

            IntPtr ptr = bindStateMachine(m_nativeStateMachine);
            if (ptr != IntPtr.Zero)
            {
                // Must consume the returned ref on every call or it leaks one ref per bind.
                m_currentViewModelInstance = ViewModelInstance.GetOrCreateFromPointer(ptr, RiveFile);
            }
            else
            {
                // No main after bind (e.g. artboard has no default view model).
                m_currentViewModelInstance = null;
            }
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

        #region Native Methods
        [DllImport(NativeLibrary.name)]
        internal static extern void unrefStateMachine(IntPtr stateMachine);

        [DllImport(NativeLibrary.name)]
        internal static extern bool advanceStateMachine(IntPtr stateMachine, float seconds);

        [DllImport(NativeLibrary.name)]
        internal static extern uint getSMIInputCountStateMachine(IntPtr stateMachine);

        [DllImport(NativeLibrary.name)]
        internal static extern IntPtr getSMIInputFromIndexStateMachine(
            IntPtr stateMachine,
            uint index
        );

        [DllImport(NativeLibrary.name)]
        internal static extern IntPtr getSMIBoolStateMachine(IntPtr stateMachine, string name);

        [DllImport(NativeLibrary.name)]
        internal static extern IntPtr getSMITriggerStateMachine(IntPtr stateMachine, string name);

        [DllImport(NativeLibrary.name)]
        internal static extern IntPtr getSMINumberStateMachine(IntPtr stateMachine, string name);


        [DllImport(NativeLibrary.name)]
        internal static extern byte pointerMoveStateMachineWithHit(IntPtr smi, float x, float y, int pointerId);

        [DllImport(NativeLibrary.name)]
        internal static extern byte pointerDownStateMachineWithHit(IntPtr smi, float x, float y, int pointerId);

        [DllImport(NativeLibrary.name)]
        internal static extern byte pointerUpStateMachineWithHit(IntPtr smi, float x, float y, int pointerId);

        [DllImport(NativeLibrary.name)]
        internal static extern byte pointerExitStateMachineWithHit(IntPtr smi, float x, float y, int pointerId);

        [DllImport(NativeLibrary.name)]
        internal static extern bool hitTestStateMachine(IntPtr stateMachine, float x, float y);

        [DllImport(NativeLibrary.name)]
        internal static extern uint getReportedEventCount(IntPtr stateMachine);

        [DllImport(NativeLibrary.name)]
        internal static extern ReportedEventData getReportedEventAt(
            IntPtr stateMachine,
            uint index
        );

        [DllImport(NativeLibrary.name)]
        internal static extern IntPtr stateMachineGetName(IntPtr stateMachine);

        // Data binding

        [DllImport(NativeLibrary.name)]
        private static extern void setViewModelInstanceOnStateMachine(IntPtr stateMachine, ViewModelInstanceSafeHandle viewModelInstance);

        [DllImport(NativeLibrary.name)]
        private static extern bool setGlobalViewModelInstanceOnStateMachine(
            IntPtr stateMachine,
            string name,
            ViewModelInstanceSafeHandle viewModelInstance);

        [DllImport(NativeLibrary.name)]
        private static extern IntPtr getGlobalViewModelInstanceFromStateMachine(IntPtr stateMachine, string name);

        [DllImport(NativeLibrary.name)]
        private static extern IntPtr bindStateMachine(IntPtr stateMachine);


        #endregion
    }
}
