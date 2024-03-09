using UnityEngine;
using System;
using System.Runtime.InteropServices;

namespace Rive
{
    /// <summary>
    /// Represents a Rive Artboard with a File. An Artboard contains StateMachines and Animations.
    /// </summary>
    public class Artboard
    {
        private readonly IntPtr m_nativeArtboard;

        internal IntPtr NativeArtboard
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

        public Vector2 LocalCoordinate(
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
                alignment.X,
                alignment.Y,
                m_nativeArtboard
            );
            return new Vector2(vec.x, vec.y);
        }

        public Component Component(string name)
        {
            var ptr = artboardComponentNamed(m_nativeArtboard, name);
            if (ptr == IntPtr.Zero)
            {
                return null;
            }
            return new Component(ptr);
        }

        public bool SetTextRun(string name, string value)
        {
            return artboardSetRunValue(m_nativeArtboard, name, value);
        }

        /// <summary>
        /// Returns the width of the artboard instance.
        /// </summary>
        public float Width => getArtboardWidth(m_nativeArtboard);

        /// <summary>
        /// Returns the height of the artboard instance.
        /// </summary>
        public float Height => getArtboardHeight(m_nativeArtboard);

        /// Returns the number of StateMachines stored in the artboard.
        public uint StateMachineCount
        {
            get { return getStateMachineCount(m_nativeArtboard); }
        }

        /// Returns the name of the StateMachine at the given index.
        public string StateMachineName(uint index)
        {
            return Marshal.PtrToStringAnsi(getStateMachineName(m_nativeArtboard, index));
        }

        /// Instance a StateMachine from the Artboard.
        public StateMachine StateMachine(uint index)
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
        public StateMachine StateMachine(string name)
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
        public StateMachine StateMachine()
        {
            IntPtr ptr = instanceStateMachineDefault(m_nativeArtboard);
            if (ptr == IntPtr.Zero)
            {
                Debug.Log($"No default StateMachine found.");
                return null;
            }
            return new StateMachine(ptr);
        }

        public void SetAudioEngine(AudioEngine audioEngine)
        {
            setArtboardAudioEngine(m_nativeArtboard, audioEngine.m_nativeAudioEngine);
        }

        #region Native Methods
        [DllImport(NativeLibrary.name)]
        internal static extern void unrefArtboard(IntPtr artboard);

        [DllImport(NativeLibrary.name)]
        internal static extern uint getStateMachineCount(IntPtr artboard);

        [DllImport(NativeLibrary.name)]
        internal static extern IntPtr getStateMachineName(IntPtr artboard, uint index);

        [DllImport(NativeLibrary.name)]
        internal static extern float getArtboardWidth(IntPtr artboard);

        [DllImport(NativeLibrary.name)]
        internal static extern float getArtboardHeight(IntPtr artboard);

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

        [DllImport(NativeLibrary.name)]
        internal static extern void setArtboardAudioEngine(IntPtr artboard, IntPtr audioEngine);

        [DllImport(NativeLibrary.name)]
        internal static extern IntPtr artboardComponentNamed(IntPtr artboard, string name);

        [DllImport(NativeLibrary.name)]
        internal static extern bool artboardSetRunValue(
            IntPtr artboard,
            string runName,
            string text
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
