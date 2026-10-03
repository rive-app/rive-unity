using System;
using System.Collections.Generic;
using Rive.Utils;

namespace Rive
{
    /// <summary>
    /// Represents a loaded Rive file, giving fast, direct access to its data and artboard names.
    /// Use this class if you need to work with Rive files immediately after loading, without waiting for background processing.
    /// </summary>
    /// <remarks>
    /// Remember to call <c>Dispose()</c> when you're done to free any resources.
    /// When created from a Rive Asset, the loaded file is shared across all handles from that asset.
    /// </remarks>
    public sealed class FileHandle : ILoadedFile
    {
        // Implementation details:
        // - m_file points to the shared File instance with artboard and animation data.
        // - m_contents caches the loaded artboard/animation names and info so lookups are immediate.
        // - m_isDisposed tracks disposal to avoid double-freeing native resources.

        private readonly File m_file;
        private readonly FileContents m_contents;
        // False for a widget's view of its own file. The widget releases it.
        private readonly bool m_owned;
        private bool m_isDisposed;

        internal FileHandle(File file, FileContents contents, bool owned = true)
        {
            m_file = file;
            m_contents = contents;
            m_owned = owned;
        }
        internal File File => m_file;

        internal FileContents Contents => m_contents;

        internal NativeFileHandle NativeFile => m_file.NativeFile;

        /// <summary>
        /// Loads a .riv file from a Rive Asset, with the out-of-band assets
        /// assigned to it in the inspector.
        /// </summary>
        /// <param name="asset">The Rive Asset (.riv) to load.</param>
        /// <returns>An operation that finishes on the main thread with the file, or fails with a <see cref="RiveException"/> if it can't be loaded.</returns>
        public static Future<FileHandle> LoadAsync(Asset asset)
        {
            return LoadAsync(asset, null);
        }

        /// <summary>
        /// Loads a .riv file from a Rive Asset, overriding some of its
        /// out-of-band assets.
        /// </summary>
        /// <param name="asset">The Rive Asset (.riv) to load.</param>
        /// <param name="assets">Assets keyed by the asset id shown in the Rive editor. Ids this doesn't name keep what's assigned in the inspector.</param>
        /// <returns>An operation that finishes on the main thread with the file, or fails with a <see cref="RiveException"/> if it can't be loaded.</returns>
        public static Future<FileHandle> LoadAsync(Asset asset, IReadOnlyDictionary<uint, OutOfBandAsset> assets)
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }
            return FileNative.ToFileHandle(File.Loader.LoadAsync(asset, assets));
        }

        /// <summary>
        /// Loads a .riv file from bytes, for example ones downloaded at runtime.
        /// </summary>
        /// <param name="bytes">The raw bytes of the .riv file.</param>
        /// <param name="assets">Out-of-band assets keyed by the asset id shown in the Rive editor, or null.</param>
        /// <returns>An operation that finishes on the main thread with the file, or fails with a <see cref="RiveException"/> if it can't be loaded.</returns>
        public static Future<FileHandle> LoadAsync(byte[] bytes, IReadOnlyDictionary<uint, OutOfBandAsset> assets = null)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }
            return FileNative.ToFileHandle(File.Loader.LoadAsync(bytes, null, assets));
        }

        internal IReadOnlyList<string> ArtboardNames => m_contents.ArtboardNames;

        /// <summary>
        /// Gets the names of the artboards in the file.
        /// </summary>
        /// <returns>An operation that finishes on the main thread with the names.</returns>
        public Future<IReadOnlyList<string>> GetArtboardNamesAsync() =>
            Future<IReadOnlyList<string>>.FromResult(m_contents.ArtboardNames);

        /// <summary>
        /// Makes an instance of one of the file's artboards.
        /// </summary>
        /// <param name="name">The artboard's name, or null for the file's first artboard.</param>
        /// <returns>The handle, straight away, before Rive has looked it up. Rive makes the artboard in order, and anything you do with the handle runs after that. Null, with a warning, if the file has no artboard by that name.</returns>
        public ArtboardHandle InstantiateArtboard(string name = null)
        {
            if (m_isDisposed)
            {
                DebugLogger.Instance.LogError("InstantiateArtboard: the file has been disposed.");
                return null;
            }
            int index = name == null
                ? (m_contents.Artboards.Length > 0 ? 0 : -1)
                : m_contents.ArtboardIndex(name);
            if (index < 0)
            {
                DebugLogger.Instance.LogWarning(name == null
                    ? "The file has no artboards."
                    : $"The file has no artboard named '{name}'. It has: {string.Join(", ", m_contents.ArtboardNames)}.");
                return null;
            }
            var native = new NativeSlot<NativeArtboardHandle>();
            var handle = new ArtboardHandle(
                native, HandleResolution.Pending(), this, m_contents.Artboards[index], owned: true,
                ArtboardNative.Lifetime(native, m_file.Lifetime));
            ArtboardNative.InstantiateLater(NativeFile, handle, HandleErrors.CaptureCallSite());
            return handle;
        }

        internal IReadOnlyList<string> ViewModelNames => m_contents.ViewModelNames;

        /// <summary>
        /// Gets the names of the view models in the file.
        /// </summary>
        /// <returns>An operation that finishes on the main thread with the names.</returns>
        public Future<IReadOnlyList<string>> GetViewModelNamesAsync() =>
            Future<IReadOnlyList<string>>.FromResult(m_contents.ViewModelNames);

        /// <summary>
        /// Gets one of the file's artboards to set on an artboard property.
        /// </summary>
        /// <param name="name">The artboard's name.</param>
        /// <param name="boundInstance">A view model instance for the artboard to use, or null for none.</param>
        /// <returns>The handle, straight away, before Rive has looked it up. Null, with a warning, if the file has no artboard by that name.</returns>
        public BindableArtboardHandle GetBindableArtboard(string name, ViewModelInstanceHandle boundInstance = null)
        {
            if (m_isDisposed)
            {
                DebugLogger.Instance.LogError("GetBindableArtboard: the file has been disposed.");
                return null;
            }
            if (m_contents.ArtboardIndex(name) < 0)
            {
                DebugLogger.Instance.LogWarning(
                    $"The file has no artboard named '{name}'. It has: {string.Join(", ", m_contents.ArtboardNames)}.");
                return null;
            }
            var handle = new BindableArtboardHandle(
                new NativeSlot<NativeArtboardHandle>(), HandleResolution.Pending(), name, boundInstance);
            ViewModelInstanceNative.GetBindableArtboardLater(NativeFile, name, handle, HandleErrors.CaptureCallSite());
            return handle;
        }

        /// <summary>
        /// Gets one of the file's view models, for making instances of it.
        /// </summary>
        /// <param name="name">The view model's name.</param>
        /// <returns>The view model. Null, with a warning, if the file has no view model by that name.</returns>
        public ViewModelHandle GetViewModel(string name)
        {
            int index = m_contents.ViewModelIndex(name);
            if (index < 0)
            {
                DebugLogger.Instance.LogWarning(
                    $"The file has no view model named '{name}'. It has: {string.Join(", ", m_contents.ViewModelNames)}.");
                return null;
            }
            return GetViewModelAt(index);
        }

        internal ViewModelHandle GetViewModelAt(int index)
        {
            return new ViewModelHandle(this, index, m_contents.ViewModels[index]);
        }

        internal IReadOnlyList<ViewModelEnumData> ViewModelEnums => m_contents.Enums;

        /// <summary>
        /// Gets the file's enums, with their values.
        /// </summary>
        /// <returns>An operation that finishes on the main thread with the enums.</returns>
        public Future<IReadOnlyList<ViewModelEnumData>> GetViewModelEnumsAsync() =>
            Future<IReadOnlyList<ViewModelEnumData>>.FromResult(m_contents.Enums);

        internal IReadOnlyList<string> GlobalViewModelNames => m_contents.GlobalViewModelNames;

        /// <summary>
        /// Gets the names of the file's global view models.
        /// </summary>
        /// <returns>An operation that finishes on the main thread with the names.</returns>
        public Future<IReadOnlyList<string>> GetGlobalViewModelNamesAsync() =>
            Future<IReadOnlyList<string>>.FromResult(m_contents.GlobalViewModelNames);

        /// <summary>
        /// True once the handle has been disposed, or its widget has unloaded it.
        /// </summary>
        public bool IsDisposed => m_isDisposed;

        /// <summary>
        /// Releases this handle's hold on the file. The file is freed once nothing else uses it. Does nothing for a handle a widget owns.
        /// </summary>
        public void Dispose()
        {
            if (m_isDisposed)
            {
                return;
            }
            if (!m_owned)
            {
                DebugLogger.Instance.LogWarning("This file belongs to a widget, which releases it. Dispose was ignored.");
                return;
            }
            m_isDisposed = true;
            m_file.Dispose();
        }

        /// For a widget's view, when it unloads. The widget releases the file.
        internal void Release()
        {
            m_isDisposed = true;
        }
    }
}
