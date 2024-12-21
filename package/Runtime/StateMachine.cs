using UnityEngine;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Rive.Utils;

namespace Rive
{
    /// <summary>
    /// Represents a Rive StateMachine from an Artboard. A StateMachine contains Inputs.
    /// </summary>
    public class StateMachine
    {
        private readonly IntPtr m_nativeStateMachine;

        internal IntPtr NativeStateMachine => m_nativeStateMachine;

        internal StateMachine(IntPtr nativeStateMachine)
        {
            m_nativeStateMachine = nativeStateMachine;
        }

        ~StateMachine()
        {
            unrefStateMachine(m_nativeStateMachine);
        }

        public string Name => Marshal.PtrToStringAnsi(stateMachineGetName(m_nativeStateMachine));

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
        public HitResult PointerMove(Vector2 position)
        {
            return (HitResult)pointerMoveStateMachineWithHit(m_nativeStateMachine, position.x, position.y);
        }

        /// <summary>
        /// Press the pointer at the given position
        /// </summary>
        public HitResult PointerDown(Vector2 position)
        {
            return (HitResult)pointerDownStateMachineWithHit(m_nativeStateMachine, position.x, position.y);
        }

        /// <summary>
        /// Release the pointer at the given position
        /// </summary>
        public HitResult PointerUp(Vector2 position)
        {
            return (HitResult)pointerUpStateMachineWithHit(m_nativeStateMachine, position.x, position.y);
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
        internal static extern byte pointerMoveStateMachineWithHit(IntPtr smi, float x, float y);

        [DllImport(NativeLibrary.name)]
        internal static extern byte pointerDownStateMachineWithHit(IntPtr smi, float x, float y);

        [DllImport(NativeLibrary.name)]
        internal static extern byte pointerUpStateMachineWithHit(IntPtr smi, float x, float y);

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
        #endregion
    }
}
