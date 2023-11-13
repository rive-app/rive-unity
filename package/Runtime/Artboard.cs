using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine.Rendering;

namespace Rive
{
    /// <summary>
    /// Represents a Rive Artboard with a File. An Artboard contains StateMachines and Animations.
    /// </summary>
    public class Artboard
    {
        private IntPtr m_nativeArtboard;

        internal IntPtr nativeArtboard
        {
            get { return m_nativeArtboard; }
        }

        internal Artboard(IntPtr nativeArtboard)
        {
            m_nativeArtboard = nativeArtboard;
        }

        ~Artboard()
        {
            unrefArtboard(m_nativeArtboard);
        }

        public Vector2 localCoordinate(
            Vector2 screenPosition,
            Rect screen,
            Fit fit,
            Alignment alignment
        )
        {
            Vec2D vec = screenToRive(
                screenPosition.x,
                screenPosition.y,
                screen.xMin,
                screen.yMin,
                screen.xMax,
                screen.yMax,
                (byte)fit,
                alignment.x,
                alignment.y,
                m_nativeArtboard
            );
            return new Vector2(vec.x, vec.y);
        }

        /// Returns the number of StateMachines stored in the artboard.
        public uint stateMachineCount
        {
            get { return getStateMachineCount(m_nativeArtboard); }
        }

        /// Returns the name of the StateMachine at the given index.
        public string stateMachineName(uint index)
        {
            return Marshal.PtrToStringAnsi(getStateMachineName(m_nativeArtboard, index));
        }

        /// Instance a StateMachine from the Artboard.
        public StateMachine stateMachine(uint index)
        {
            IntPtr ptr = instanceStateMachineAtIndex(m_nativeArtboard, index);
            if (ptr == IntPtr.Zero)
            {
                Debug.Log($"No StateMachine at index {index}.");
                return null;
            }
            return new StateMachine(ptr);
        }

        /// Instance a StateMachine from the Artboard.
        public StateMachine stateMachine(string name)
        {
            IntPtr ptr = instanceStateMachineWithName(m_nativeArtboard, name);
            if (ptr == IntPtr.Zero)
            {
                Debug.Log($"No StateMachine named \"{name}\".");
                return null;
            }
            return new StateMachine(ptr);
        }

        /// Instance the default StateMachine from the Artboard.
        public StateMachine stateMachine()
        {
            IntPtr ptr = instanceStateMachineDefault(m_nativeArtboard);
            if (ptr == IntPtr.Zero)
            {
                Debug.Log($"No default StateMachine found.");
                return null;
            }
            return new StateMachine(ptr);
        }

        #region Native Methods
        [DllImport(NativeLibrary.name)]
        internal static extern IntPtr unrefArtboard(IntPtr artboard);

        [DllImport(NativeLibrary.name)]
        internal static extern uint getStateMachineCount(IntPtr artboard);

        [DllImport(NativeLibrary.name)]
        internal static extern IntPtr getStateMachineName(IntPtr artboard, uint index);

        [DllImport(NativeLibrary.name)]
        internal static extern IntPtr instanceStateMachineAtIndex(IntPtr artboard, uint index);

        [DllImport(NativeLibrary.name)]
        internal static extern IntPtr instanceStateMachineWithName(IntPtr artboard, string name);

        [DllImport(NativeLibrary.name)]
        internal static extern IntPtr instanceStateMachineDefault(IntPtr artboard);

        [DllImport(NativeLibrary.name)]
        internal static extern Vec2D screenToRive(
            float x,
            float y,
            float screenX,
            float screenY,
            float screenWidth,
            float screenHeight,
            byte fit,
            float alignX,
            float alignY,
            IntPtr artboard
        );
        #endregion
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct Vec2D
{
    public float x;
    public float y;
}
