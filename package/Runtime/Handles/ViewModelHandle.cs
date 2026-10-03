using System.Collections.Generic;
using Rive.Utils;

namespace Rive
{
    /// <summary>
    /// Represents a Rive ViewModel from a <see cref="FileHandle"/> for scenarios
    /// where immediate, non-blocking access is required.
    /// </summary>
    /// <remarks>
    /// Returned immediately by <see cref="FileHandle.GetViewModel(string)"/> and <see cref="ArtboardHandle.GetDefaultViewModel"/>.
    /// </remarks>
    public sealed class ViewModelHandle
    {
        // The file the view model belongs to.
        private readonly FileHandle m_file;

        // The view model's index in the file.
        private readonly int m_index;

        private readonly FileContents.ViewModelInfo m_info;

        internal ViewModelHandle(FileHandle file, int index, FileContents.ViewModelInfo info)
        {
            m_file = file;
            m_index = index;
            m_info = info;
        }

        internal int Index => m_index;

        internal string Name => m_info.Name;

        /// <summary>
        /// Gets the view model's name.
        /// </summary>
        /// <returns>An operation that finishes on the main thread with the name.</returns>
        public Future<string> GetNameAsync() => Future<string>.FromResult(m_info.Name);

        internal IReadOnlyList<ViewModelPropertyData> Properties => m_info.Properties;

        /// <summary>
        /// Gets the view model's properties, with their types.
        /// </summary>
        /// <returns>An operation that finishes on the main thread with the properties.</returns>
        public Future<IReadOnlyList<ViewModelPropertyData>> GetPropertiesAsync() =>
            Future<IReadOnlyList<ViewModelPropertyData>>.FromResult(m_info.Properties);

        internal IReadOnlyList<string> InstanceNames => m_info.InstanceNames;

        /// <summary>
        /// Gets the names of the instances the file defines for this view model.
        /// </summary>
        /// <returns>An operation that finishes on the main thread with the names.</returns>
        public Future<IReadOnlyList<string>> GetInstanceNamesAsync() =>
            Future<IReadOnlyList<string>>.FromResult(m_info.InstanceNames);

        /// <summary>
        /// Makes a copy of the view model's default instance, as set in the Rive editor.
        /// </summary>
        /// <returns>The handle, straight away, before Rive has made it.</returns>
        public ViewModelInstanceHandle Instantiate()
        {
            return Make(nameof(Instantiate), ViewModelInstanceNative.InstanceKind.Default, null);
        }

        /// <summary>
        /// Makes a copy of one of the instances the file defines.
        /// </summary>
        /// <param name="name">The instance's name, from <see cref="GetInstanceNamesAsync"/>.</param>
        /// <returns>The handle, straight away, before Rive has made it. Null, with a warning, if the view model has no instance by that name.</returns>
        public ViewModelInstanceHandle Instantiate(string name)
        {
            if (FileContents.IndexOf(m_info.InstanceNames, name) < 0)
            {
                DebugLogger.Instance.LogWarning(
                    $"View model '{Name}' has no instance named '{name}'. It has: {string.Join(", ", m_info.InstanceNames)}.");
                return null;
            }
            return Make(nameof(Instantiate), ViewModelInstanceNative.InstanceKind.Named, name);
        }

        /// <summary>
        /// Makes a new instance that isn't a copy of any the file defines.
        /// </summary>
        /// <returns>The handle, straight away, before Rive has made it.</returns>
        public ViewModelInstanceHandle InstantiateBlank()
        {
            return Make(nameof(InstantiateBlank), ViewModelInstanceNative.InstanceKind.Blank, null);
        }

        private ViewModelInstanceHandle Make(string what, ViewModelInstanceNative.InstanceKind kind, string name)
        {
            if (m_file.IsDisposed)
            {
                DebugLogger.Instance.LogError($"{what}: the file has been disposed.");
                return null;
            }
            var handle = new ViewModelInstanceHandle(
                new NativeSlot<NativeViewModelInstanceHandle>(), HandleResolution.Pending(), m_file.Contents, m_index, owned: true,
                name: kind == ViewModelInstanceNative.InstanceKind.Named ? name : null);
            ViewModelInstanceNative.InstantiateLater(
                m_file.NativeFile, (uint)m_index, Name, kind, name, handle, HandleErrors.CaptureCallSite());
            return handle;
        }
    }
}
