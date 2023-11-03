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

        #region Native Methods
        [DllImport(NativeLibrary.name)]
        internal static extern IntPtr unrefStateMachine(IntPtr StateMachine);

        [DllImport(NativeLibrary.name)]
        internal static extern bool advanceStateMachine(IntPtr StateMachine, float seconds);

        [DllImport(NativeLibrary.name)]
        internal static extern void pointerMoveStateMachine(IntPtr StateMachine, float x, float y);

        [DllImport(NativeLibrary.name)]
        internal static extern void pointerDownStateMachine(IntPtr StateMachine, float x, float y);

        [DllImport(NativeLibrary.name)]
        internal static extern void pointerUpStateMachine(IntPtr StateMachine, float x, float y);
        #endregion
    }
}
