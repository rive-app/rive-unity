using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine.Rendering;

namespace Rive
{
    /// <summary>
    /// Represents a Rive StateMachine from an Artboard. A StateMachine contains Inputs.
    /// </summary>
    public class StateMachine
    {
        private IntPtr m_nativeStateMachine;

        internal IntPtr nativeStateMachine
        {
            get { return m_nativeStateMachine; }
        }

        internal StateMachine(IntPtr nativeStateMachine)
        {
            m_nativeStateMachine = nativeStateMachine;
        }

        ~StateMachine()
        {
            unrefStateMachine(m_nativeStateMachine);
        }

        public bool advance(float seconds)
        {
            return advanceStateMachine(m_nativeStateMachine, seconds);
        }

        public void pointerMove(Vector2 position)
        {
            pointerMoveStateMachine(m_nativeStateMachine, position.x, position.y);
        }

        public void pointerDown(Vector2 position)
        {
            pointerDownStateMachine(m_nativeStateMachine, position.x, position.y);
        }

        public void pointerUp(Vector2 position)
        {
            pointerUpStateMachine(m_nativeStateMachine, position.x, position.y);
        }

        public List<ReportedEvent> reportedEvents()
        {
            uint count = getReportedEventCount(m_nativeStateMachine);
            var list = new List<ReportedEvent>();
            for (uint i = 0; i < count; i++)
            {
                list.Add(new ReportedEvent(getReportedEventAt(m_nativeStateMachine, i)));
            }
            return list;
        }

        #region Native Methods
        [DllImport(NativeLibrary.name)]
        internal static extern IntPtr unrefStateMachine(IntPtr stateMachine);

        [DllImport(NativeLibrary.name)]
        internal static extern bool advanceStateMachine(IntPtr stateMachine, float seconds);

        [DllImport(NativeLibrary.name)]
        internal static extern void pointerMoveStateMachine(IntPtr stateMachine, float x, float y);

        [DllImport(NativeLibrary.name)]
        internal static extern void pointerDownStateMachine(IntPtr stateMachine, float x, float y);

        [DllImport(NativeLibrary.name)]
        internal static extern void pointerUpStateMachine(IntPtr stateMachine, float x, float y);

        [DllImport(NativeLibrary.name)]
        internal static extern uint getReportedEventCount(IntPtr stateMachine);

        [DllImport(NativeLibrary.name)]
        internal static extern ReportedEventData getReportedEventAt(IntPtr stateMachine, uint index);
        #endregion
    }
}
