using UnityEngine;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Rive.Producer;
using Rive.Utils;

namespace Rive
{
    /// <summary>
    /// Represents a Rive Artboard with a File. An Artboard contains StateMachines and Animations.
    /// </summary>
    public class Artboard : IDisposable
    {
        private readonly NativeArtboardHandle m_nativeArtboard;
        private readonly FileContents.ArtboardInfo m_info;
        private readonly NativeLifetime m_lifetime;
        private ViewModelInstance m_currentViewModelInstance;
        private ViewModel m_defaultViewModel;

        private WeakReference<File> m_file;
        private bool m_isDisposed = false;

        internal NativeArtboardHandle NativeArtboard
        {
            get { return m_nativeArtboard; }
        }

        /// <summary>
        /// Returns true if the artboard has been disposed.
        /// </summary>
        public bool IsDisposed { get => m_isDisposed; }

        /// <summary>
        /// Constructor for the Artboard class.
        /// </summary>
        /// <param name="nativeArtboard"> Pointer to the native artboard.</param>
        /// <param name="file"> The file that instanced the artboard.</param>
        /// <param name="info"> The artboard's names, from the file.</param>
        internal Artboard(NativeArtboardHandle nativeArtboard, File file, FileContents.ArtboardInfo info)
        {
            m_nativeArtboard = nativeArtboard;
            m_file = new WeakReference<File>(file);
            m_info = info;
            m_lifetime = ArtboardNative.Lifetime(new NativeSlot<NativeArtboardHandle>(nativeArtboard), file?.Lifetime);
        }

        internal NativeLifetime Lifetime => m_lifetime;

        /// <summary>
        /// The file that instanced this artboard, or null if it has already been collected.
        /// </summary>
        internal File File => m_file != null && m_file.TryGetTarget(out var file) ? file : null;

        /// <summary>
        /// Dispose of the Artboard and release native resources.
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
                m_lifetime.ReleaseOwner();
                m_isDisposed = true;
            }
        }

        ~Artboard()
        {
            Dispose(false);
        }

        /// <summary>
        /// Converts a screen position to the artboard's coordinates.
        /// </summary>
        public Vector2 LocalCoordinate(
            Vector2 screenPosition,
            Rect screen,
            Fit fit,
            Alignment alignment
        )
        {
            Vec2D vec = ArtboardNative.ScreenToRive(
                m_nativeArtboard,
                screenPosition.x,
                screenPosition.y,
                screen.xMin,
                screen.yMin,
                screen.xMax,
                screen.yMax,
                (byte)fit,
                alignment.X,
                alignment.Y
            );
            return new Vector2(vec.x, vec.y);
        }

        /// <summary>
        /// The artboard's name.
        /// </summary>
        public string Name => m_info.Name;

        /// <summary>
        /// Returns true if the artboard has audio.
        /// </summary>
        public bool HasAudio
        {
            get
            {
                return ArtboardNative.GetInfo(m_nativeArtboard).HasAudio;
            }
        }

        /// <summary>
        /// Sets the value of a text run with the provided name.
        /// </summary>
        /// <param name="name">The name of the text run.</param>
        /// <param name="value"> The new value for the text run.</param>
        /// <returns>True if the text run was found and set.</returns>
        public bool SetTextRun(string name, string value)
        {
            return ArtboardNative.SetTextRun(m_nativeArtboard, name, null, value);
        }

        /// <summary>
        /// Gets the value of a text run with the provided name.
        /// </summary>
        /// <param name="runName">The name of the text run.</param>
        /// <returns>The value of the text run, or null if not found.</returns>
        public string GetTextRunValue(string runName)
        {
            return ArtboardNative.GetTextRun(m_nativeArtboard, runName, null);
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
            if (path == null || value == null)
            {
                return false;
            }
            return ArtboardNative.SetTextRun(m_nativeArtboard, runName, path, value);
        }

        /// <summary>
        /// Gets the value of a text run with the provided name at the given path.
        /// </summary>
        /// <param name="runName">The name of the text run.</param>
        /// <param name="path">The path to the nested artboard where the text run is located.</param>
        /// <returns>The value of the text run, or null if not found.</returns>
        public string GetTextRunValueAtPath(string runName, string path)
        {
            if (path == null)
            {
                return null;
            }
            return ArtboardNative.GetTextRun(m_nativeArtboard, runName, path);
        }

        // Both at once, in one call to Rive's thread.
        internal Size Size
        {
            get => ArtboardNative.GetInfo(m_nativeArtboard).Size;
            set => ArtboardNative.SetSize(m_nativeArtboard, value);
        }

        /// <summary>
        /// Gets or sets the width of the artboard instance.
        /// </summary>
        public float Width
        {
            get => ArtboardNative.GetInfo(m_nativeArtboard).Size.Width;
            set => ArtboardNative.SetWidth(m_nativeArtboard, value);
        }

        /// <summary>
        /// Gets or sets the height of the artboard instance.
        /// </summary>
        public float Height
        {
            get => ArtboardNative.GetInfo(m_nativeArtboard).Size.Height;
            set => ArtboardNative.SetHeight(m_nativeArtboard, value);
        }

        /// <summary>
        /// Returns true if the artboard has changed since the last draw.
        /// </summary>
        public bool DidChange()
        {
            return ArtboardNative.GetInfo(m_nativeArtboard).DidChange;
        }

        /// <summary>
        /// The number of StateMachines stored in the artboard.
        /// </summary>
        public uint StateMachineCount
        {
            get { return (uint)m_info.StateMachineNames.Length; }
        }


        /// <summary>
        /// True if this artboard has a view model linked in the Rive file.
        /// </summary>
        internal bool HasDefaultViewModel
        {
            get
            {
                return m_info.DefaultViewModelIndex >= 0;
            }
        }

        /// <summary>
        /// The default ViewModel for the artboard.
        /// </summary>
        public ViewModel DefaultViewModel
        {
            get
            {
                if (m_defaultViewModel == null)
                {
                    if (!HasDefaultViewModel)
                    {
                        return null;
                    }

                    var file = m_file.TryGetTarget(out var target) ? target : null;
                    if (file != null)
                    {
                        m_defaultViewModel = file.ViewModelAt(m_info.DefaultViewModelIndex);
                    }
                }

                return m_defaultViewModel;
            }
        }

        /// <summary>
        /// Returns the name of the StateMachine at the given index.
        /// </summary>
        public string StateMachineName(uint index)
        {
            return index < m_info.StateMachineNames.Length ? m_info.StateMachineNames[index] : null;
        }

        /// With no name it's the first one that was missing.
        internal static void LogMissingStateMachine(string name)
        {
            DebugLogger.Instance.Log(name != null ? $"No StateMachine named \"{name}\"." : "No StateMachine at index 0.");
        }

        /// Instance a StateMachine from the Artboard.
        public StateMachine StateMachine(uint index)
        {
            string name = StateMachineName(index);
            NativeStateMachineHandle ptr = name != null ? StateMachineNative.Instantiate(m_nativeArtboard, name) : default;
            if (!ptr.IsValid)
            {
                DebugLogger.Instance.Log($"No StateMachine at index {index}.");
                return null;
            }
            return new StateMachine(ptr, this, name);
        }

        /// Instance a StateMachine from the Artboard.
        public StateMachine StateMachine(string name)
        {
            NativeStateMachineHandle ptr = !string.IsNullOrEmpty(name) ? StateMachineNative.Instantiate(m_nativeArtboard, name) : default;
            if (!ptr.IsValid)
            {
                DebugLogger.Instance.Log($"No StateMachine named \"{name}\".");
                return null;
            }
            return new StateMachine(ptr, this, name);
        }

        /// Instance the default StateMachine from the Artboard.
        public StateMachine StateMachine()
        {
            // The file's default, or the first when it has none.
            string name = m_info.DefaultStateMachineIndex >= 0 ? StateMachineName((uint)m_info.DefaultStateMachineIndex) : null;
            NativeStateMachineHandle ptr = name != null ? StateMachineNative.Instantiate(m_nativeArtboard, name) : default;
            if (!ptr.IsValid)
            {
                DebugLogger.Instance.Log($"No default StateMachine found.");
                return null;
            }
            return new StateMachine(ptr, this, name);
        }

        /// <summary>
        /// Sets the audio engine the artboard plays through.
        /// </summary>
        public void SetAudioEngine(AudioEngine audioEngine)
        {
            if (audioEngine == null)
            {
                DebugLogger.Instance.LogError("AudioEngine is null.");
                return;
            }
            ArtboardNative.SetAudioEngine(m_nativeArtboard, audioEngine);
        }

        private static bool HasInputNameAndPath(string inputName, string path)
        {
            if (string.IsNullOrEmpty(inputName))
            {
                DebugLogger.Instance.LogWarning($"No input name provided for path '{path}' .");
                return false;
            }

            if (string.IsNullOrEmpty(path))
            {
                DebugLogger.Instance.LogWarning($"No path provided for input '{inputName}'.");
                return false;
            }

            return true;
        }

        // Reads the input. False, with a warning, when it's missing or another kind.
        private bool TryGetInputAtPath(string inputName, string path, ArtboardNative.InputKind kind, string kindName, out float value)
        {
            value = 0f;
            if (!HasInputNameAndPath(inputName, path))
            {
                return false;
            }
            if (!ArtboardNative.GetInputAtPath(m_nativeArtboard, inputName, path, out ArtboardNative.InputKind found, out value))
            {
                LogMissingInputWarning(inputName, path);
                return false;
            }
            if (found != kind)
            {
                LogIncorrectInputTypeWarning(inputName, path, kindName);
                return false;
            }
            return true;
        }

        private void LogMissingInputWarning(string inputName, string path)
        {
            DebugLogger.Instance.LogWarning($"No input found at path '{path}' with name '{inputName}'.");
        }

        private void LogIncorrectInputTypeWarning(string inputName, string path, string expectedType)
        {
            DebugLogger.Instance.LogWarning($"Input '{inputName}' at path: '{path}' is not a {expectedType} input.");
        }

        // Add this description to the method: Set the boolean input with the provided name at the given path with value

        /// <summary>
        /// Set the boolean input with the provided name at the given path with value.
        /// </summary>
        /// <param name="inputName">The name of the input to set.</param>
        /// <param name="value">The value to set the input to.</param>
        /// <param name="path">The location of the input at an artboard level, detailing nested locations if applicable.</param>
        /// <remarks>If the input isn't found, the warning comes on a later frame.</remarks>
        [Obsolete(ObsoleteMessages.Inputs)]
        public void SetBooleanInputStateAtPath(string inputName, bool value, string path)
        {
            if (!HasInputNameAndPath(inputName, path))
            {
                return;
            }
            ArtboardNative.SetInputAtPath(this, inputName, path, ArtboardNative.InputKind.Boolean, value ? 1f : 0f);
        }

        /// <summary>
        /// Get the boolean input value with the provided name at the given path.
        /// </summary>
        /// <param name="inputName">The state machine input name</param>
        /// <param name="path">The location of the input at an artboard level, detailing nested locations if applicable.</param>
        /// <returns>The value of the boolean input.</returns>
        [Obsolete(ObsoleteMessages.Inputs)]
        public bool? GetBooleanInputStateAtPath(string inputName, string path)
        {
            return TryGetInputAtPath(inputName, path, ArtboardNative.InputKind.Boolean, "boolean", out float value)
                ? value != 0f
                : (bool?)null;
        }


        /// <summary>
        /// Set the number input with the provided name at the given path with value.
        /// </summary>
        /// <param name="inputName"The state machine input name</param>
        /// <param name="value">The number value to set the input to.</param>
        /// <param name="path">The location of the input at an artboard level, detailing nested locations if applicable.</param>
        /// <remarks>If the input isn't found, the warning comes on a later frame.</remarks>
        [Obsolete(ObsoleteMessages.Inputs)]
        public void SetNumberInputStateAtPath(string inputName, float value, string path)
        {
            if (!HasInputNameAndPath(inputName, path))
            {
                return;
            }
            ArtboardNative.SetInputAtPath(this, inputName, path, ArtboardNative.InputKind.Number, value);
        }

        /// <summary>
        /// Get the number input value with the provided name at the given path.
        /// </summary>
        /// <param name="inputName">The state machine input name</param>
        /// <param name="path">The location of the input at an artboard level, detailing nested locations if applicable.</param>
        /// <returns>The value of the number input.</returns>
        [Obsolete(ObsoleteMessages.Inputs)]
        public float? GetNumberInputStateAtPath(string inputName, string path)
        {
            return TryGetInputAtPath(inputName, path, ArtboardNative.InputKind.Number, "number", out float value)
                ? value
                : (float?)null;
        }


        /// <summary>
        /// Fire the trigger input with the provided name at the given path
        /// </summary>
        /// <param name="inputName">The state machine input name</param>
        /// <param name="path">The location of the input at an artboard level, detailing nested locations if applicable.</param>
        /// <remarks>If the input isn't found, the warning comes on a later frame.</remarks>
        [Obsolete(ObsoleteMessages.Inputs)]
        public void FireInputStateAtPath(string inputName, string path)
        {
            if (!HasInputNameAndPath(inputName, path))
            {
                return;
            }
            ArtboardNative.SetInputAtPath(this, inputName, path, ArtboardNative.InputKind.Trigger, 1f);
        }

        /// <summary>
        /// Resets the artboard to its original dimensions.
        /// </summary>
        public void ResetArtboardSize()
        {
            ArtboardNative.ResetSize(m_nativeArtboard);
        }


        /// <summary>
        /// Sets the data context of the Artboard from the given ViewModelInstance.
        /// </summary>
        /// <param name="viewModelInstance">The ViewModelInstance to bind to the Artboard.</param>
        /// <remarks>
        /// This method binds the ViewModelInstance to the Artboard only. If you intend to bind related state machines,
        /// you should use the <see cref="StateMachine.BindViewModelInstance(ViewModelInstance)"/> method instead as it automatically binds both the Artboard and the State Machine to the ViewModelInstance.
        /// </remarks>
        public void BindViewModelInstance(ViewModelInstance viewModelInstance)
        {
            if (viewModelInstance == null)
            {
                DebugLogger.Instance.LogError("ViewModelInstance is null.");
                return;
            }
            if (viewModelInstance.IsDisposed)
            {
                DebugLogger.Instance.LogError($"{nameof(ViewModelInstance)} has been disposed.");
                return;
            }

            ArtboardNative.BindViewModelInstanceToArtboard(NativeArtboard, viewModelInstance.NativeHandle);
            m_currentViewModelInstance = viewModelInstance;
        }
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct Vec2D
{
    public float x;
    public float y;
}
