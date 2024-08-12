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

        /// <summary>
        /// Sets the value of a text run with the provided name.
        /// </summary>
        /// <param name="name">The name of the text run.</param>
        /// <param name="value"> The new value for the text run.</param>
        /// <returns></returns>
        public bool SetTextRun(string name, string value)
        {
            return artboardSetRunValue(m_nativeArtboard, name, value);
        }

        /// <summary>
        /// Gets the value of a text run with the provided name.
        /// </summary>
        /// <param name="runName">The name of the text run.</param>
        /// <returns>The value of the text run, or null if not found.</returns>
        public string GetTextRunValue(string runName)
        {
            return Marshal.PtrToStringAnsi(artboardGetTextRunValue(m_nativeArtboard, runName));
        }

        /// <summary>
        /// Sets the value of a text run with the provided name at the given path.
        /// </summary>
        /// <param name="runName">The name of the text run.</param>
        /// <param name="path">The path to the nested artboard where the text run is located.</param>
        /// <param name="value">The new value for the text run.</param>
        /// <returns>True if the text run was successfully set, false otherwise.</returns>
        public bool SetTextRunValueAtPath(string runName, string path, string value)
        {
            return artboardSetTextRunValueAtPath(m_nativeArtboard, runName, path, value);
        }

        /// <summary>
        /// Gets the value of a text run with the provided name at the given path.
        /// </summary>
        /// <param name="runName">The name of the text run.</param>
        /// <param name="path">The path to the nested artboard where the text run is located.</param>
        /// <returns>The value of the text run, or null if not found.</returns>
        public string GetTextRunValueAtPath(string runName, string path)
        {
            return Marshal.PtrToStringAnsi(artboardGetTextRunValueAtPath(m_nativeArtboard, runName, path));
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

        internal IntPtr GetInputAtPath(string inputName, string path)
        {
            // Validate the input parameters
            if (string.IsNullOrEmpty(inputName))
            {
                Debug.LogWarning($"No input name provided for path '{path}' .");
                return IntPtr.Zero;
            }

            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning($"No path provided for input '{inputName}'.");
                return IntPtr.Zero;
            }


            IntPtr ptr = getSMIInputAtPathArtboard(m_nativeArtboard, inputName, path);

            return ptr;
        }

        private void LogMissingInputWarning(string inputName, string path)
        {
            Debug.LogWarning($"No input found at path '{path}' with name '{inputName}'.");
        }

        private void LogIncorrectInputTypeWarning(string inputName, string path, string expectedType)
        {
            Debug.LogWarning($"Input '{inputName}' at path: '{path}' is not a {expectedType} input.");
        }

        // Add this description to the method: Set the boolean input with the provided name at the given path with value

        /// <summary>
        /// Set the boolean input with the provided name at the given path with value.
        /// </summary>
        /// <param name="inputName">The name of the input to set.</param>
        /// <param name="value">The value to set the input to.</param>
        /// <param name="path">The location of the input at an artboard level, detailing nested locations if applicable.</param>
        public void SetBooleanInputStateAtPath(string inputName, bool value, string path)
        {
            var nativeSmi = GetInputAtPath(inputName, path);
            if (nativeSmi == IntPtr.Zero)
            {
                LogMissingInputWarning(inputName, path);
                return;
            }

            if (SMIInput.isSMIBoolean(nativeSmi))
            {

                SMIBool.setSMIBoolValueStateMachine(nativeSmi, value);
            }
            else
            {
                LogIncorrectInputTypeWarning(inputName, path, "boolean");
            }

        }

        /// <summary>
        /// Get the boolean input value with the provided name at the given path.
        /// </summary>
        /// <param name="inputName">The state machine input name</param>
        /// <param name="path">The location of the input at an artboard level, detailing nested locations if applicable.</param>
        /// <returns>The value of the boolean input.</returns>
        public bool? GetBooleanInputStateAtPath(string inputName, string path)
        {
            var nativeSmi = GetInputAtPath(inputName, path);
            if (nativeSmi == IntPtr.Zero)
            {
                LogMissingInputWarning(inputName, path);
                return null;
            }

            if (SMIInput.isSMIBoolean(nativeSmi))
            {
                return SMIBool.getSMIBoolValueStateMachine(nativeSmi);
            }
            else
            {
                LogIncorrectInputTypeWarning(inputName, path, "boolean");
                return null;
            }
        }

        /// <summary>
        /// Set the number input with the provided name at the given path with value.
        /// </summary>
        /// <param name="inputName"The state machine input name</param>
        /// <param name="value">The number value to set the input to.</param>
        /// <param name="path">The location of the input at an artboard level, detailing nested locations if applicable.</param>
        public void SetNumberInputStateAtPath(string inputName, float value, string path)
        {
            var nativeSmi = GetInputAtPath(inputName, path);
            if (nativeSmi == IntPtr.Zero)
            {
                LogMissingInputWarning(inputName, path);
                return;
            }

            if (SMIInput.isSMINumber(nativeSmi))
            {
                SMINumber.setSMINumberValueStateMachine(nativeSmi, value);
            }
            else
            {
                LogIncorrectInputTypeWarning(inputName, path, "number");
            }
        }

        /// <summary>
        /// Get the number input value with the provided name at the given path.
        /// </summary>
        /// <param name="inputName">The state machine input name</param>
        /// <param name="path">The location of the input at an artboard level, detailing nested locations if applicable.</param>
        /// <returns>The value of the number input.</returns>
        public float? GetNumberInputStateAtPath(string inputName, string path)
        {
            var nativeSmi = GetInputAtPath(inputName, path);
            if (nativeSmi == IntPtr.Zero)
            {
                LogMissingInputWarning(inputName, path);
                return null;
            }

            if (SMIInput.isSMINumber(nativeSmi))
            {
                return SMINumber.getSMINumberValueStateMachine(nativeSmi);
            }
            else
            {
                LogIncorrectInputTypeWarning(inputName, path, "number");
                return null;
            }
        }

        /// <summary>
        /// Fire the trigger input with the provided name at the given path
        /// </summary>
        /// <param name="inputName">The state machine input name</param>
        /// <param name="path">The location of the input at an artboard level, detailing nested locations if applicable.</param>
        public void FireInputStateAtPath(string inputName, string path)
        {
            var nativeSmi = GetInputAtPath(inputName, path);
            if (nativeSmi == IntPtr.Zero)
            {
                LogMissingInputWarning(inputName, path);
                return;
            }

            if (SMIInput.isSMITrigger(nativeSmi))
            {
                SMITrigger.fireSMITriggerStateMachine(nativeSmi);
            }
            else
            {
                LogIncorrectInputTypeWarning(inputName, path, "trigger");
            }
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

        [DllImport(NativeLibrary.name)]
        internal static extern IntPtr getSMIInputAtPathArtboard(IntPtr artboard, string inputName, string path);

        [DllImport(NativeLibrary.name)]
        internal static extern IntPtr artboardGetTextRunValue(IntPtr artboard, string runName);

        [DllImport(NativeLibrary.name)]
        internal static extern bool artboardSetTextRunValueAtPath(IntPtr artboard, string runName, string path, string text);

        [DllImport(NativeLibrary.name)]
        internal static extern IntPtr artboardGetTextRunValueAtPath(IntPtr artboard, string runName, string path);
        #endregion
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct Vec2D
{
    public float x;
    public float y;
}
