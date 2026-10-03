using System;
using System.Collections.Generic;
using Rive.Utils;

namespace Rive
{
    /// <summary>
    /// Represents a Rive ViewModelInstance for scenarios
    /// where immediate, non-blocking access is required.
    /// </summary>
    /// <remarks>
    /// Returned immediately by <see cref="ViewModelHandle.Instantiate()"/>, <see cref="StateMachineHandle.GetViewModelInstance"/>, <see cref="ListPropertyHandle.GetInstanceAt(int)"/> and <see cref="GetViewModelInstanceProperty(string)"/>. Rive looks the instance up afterwards, and you can use it straight away: what you queue on it runs after the lookup.
    /// </remarks>
    public sealed class ViewModelInstanceHandle : IDisposable
    {
        // Native view model instance slot.
        private readonly NativeSlot<NativeViewModelInstanceHandle> m_native;

        // The file's names and the instance's view model in it (-1 when that
        // isn't known up front, as for a list item).
        private readonly FileContents m_contents;
        private readonly int m_viewModelIndex;

        // The instance's own name, when it was made by name.
        private readonly string m_name;

        // Whether this handle instance is responsible for releasing the native resource.
        private readonly bool m_owned;

        private readonly HandleResolution m_resolution;

        // Property handles by type and path, so the same path gives the same handle.
        private readonly Dictionary<string, ViewModelPropertyHandle> m_properties =
            new Dictionary<string, ViewModelPropertyHandle>();
        private bool m_isDisposed;

        internal ViewModelInstanceHandle(
            NativeSlot<NativeViewModelInstanceHandle> native,
            HandleResolution resolution,
            FileContents contents,
            int viewModelIndex,
            bool owned,
            string name = null)
        {
            m_native = native;
            m_resolution = resolution;
            m_contents = contents;
            m_viewModelIndex = contents != null && viewModelIndex < contents.ViewModels.Length ? viewModelIndex : -1;
            m_owned = owned;
            m_name = name;
        }

        ~ViewModelInstanceHandle()
        {
            if (m_owned && !m_isDisposed)
            {
                ViewModelInstanceNative.UnrefLater(m_native);
            }
        }

        internal NativeSlot<NativeViewModelInstanceHandle> Native => m_native;

        internal HandleResolution Resolution => m_resolution;

        // Where the lookup is up to. Internal, so scripts don't gate writes on it.
        internal HandleStatus Status => m_resolution.Status;

        // Why the lookup found nothing, or null. Kept after a dispose.
        internal RiveException Error => m_resolution.Error;

        /// <summary>
        /// Waits for Rive to look the instance up. You don't need to before using the handle.
        /// </summary>
        /// <returns>An operation that finishes on the main thread with this handle once Rive has found what it refers to. It fails with the lookup's <see cref="RiveException"/> if Rive found nothing, the same one reported when that happened, or with <see cref="RiveErrorCode.ResourceDisposed"/> if it's disposed first. Each call gives its own operation.</returns>
        public Future<ViewModelInstanceHandle> ResolveAsync() => m_resolution.Wait(this);

        internal FileContents Contents => m_contents;

        internal int ViewModelIndex => m_viewModelIndex;

        /// Where problems found later go, besides the log. A widget's view
        /// passes them to the widget's OnError; handles made from this one
        /// inherit it.
        internal Action<RiveException> ErrorSink { get; set; }

        // Null when it isn't known up front. For messages.
        internal string ViewModelName => m_viewModelIndex >= 0 ? m_contents.ViewModels[m_viewModelIndex].Name : null;

        // Null when it wasn't made by name.
        internal string Name => m_name;

        /// <summary>
        /// Gets the instance's own name, from the Rive editor.
        /// </summary>
        /// <returns>An operation that finishes on the main thread with the name. It fails with <see cref="RiveErrorCode.ViewModelInstanceNotFound"/> if the handle refers to no instance.</returns>
        public Future<string> GetNameAsync()
        {
            if (m_name != null)
            {
                return Future<string>.FromResult(m_name);
            }
            return ViewModelInstanceNative.GetNameAsync(this, viewModelName: false);
        }

        /// <summary>
        /// Gets the name of the view model this is an instance of.
        /// </summary>
        /// <returns>An operation that finishes on the main thread with the name. It fails with <see cref="RiveErrorCode.ViewModelInstanceNotFound"/> if the handle refers to no instance.</returns>
        public Future<string> GetViewModelNameAsync()
        {
            if (ViewModelName != null)
            {
                return Future<string>.FromResult(ViewModelName);
            }
            return ViewModelInstanceNative.GetNameAsync(this, viewModelName: true);
        }

        /// <summary>
        /// Gets a number property.
        /// </summary>
        /// <param name="path">The property's name, or a path like <c>"player/health"</c> through nested view models.</param>
        /// <returns>The property handle, straight away. The same path gives the same handle. Rive checks the path the first time it's used, or when you call its ResolveAsync.</returns>
        public NumberPropertyHandle GetNumberProperty(string path) =>
            GetProperty(path, ViewModelDataType.Number, p => new NumberPropertyHandle(this, p));

        /// <inheritdoc cref="GetNumberProperty"/>
        /// <summary>Gets a string property.</summary>
        public StringPropertyHandle GetStringProperty(string path) =>
            GetProperty(path, ViewModelDataType.String, p => new StringPropertyHandle(this, p));

        /// <inheritdoc cref="GetNumberProperty"/>
        /// <summary>Gets a boolean property.</summary>
        public BooleanPropertyHandle GetBooleanProperty(string path) =>
            GetProperty(path, ViewModelDataType.Boolean, p => new BooleanPropertyHandle(this, p));

        /// <inheritdoc cref="GetNumberProperty"/>
        /// <summary>Gets a color property.</summary>
        public ColorPropertyHandle GetColorProperty(string path) =>
            GetProperty(path, ViewModelDataType.Color, p => new ColorPropertyHandle(this, p));

        /// <inheritdoc cref="GetNumberProperty"/>
        /// <summary>Gets an enum property.</summary>
        public EnumPropertyHandle GetEnumProperty(string path) =>
            GetProperty(path, ViewModelDataType.Enum, p => new EnumPropertyHandle(this, p));

        /// <inheritdoc cref="GetNumberProperty"/>
        /// <summary>Gets a trigger property.</summary>
        public TriggerPropertyHandle GetTriggerProperty(string path) =>
            GetProperty(path, ViewModelDataType.Trigger, p => new TriggerPropertyHandle(this, p));

        /// <inheritdoc cref="GetNumberProperty"/>
        /// <summary>Gets a list property.</summary>
        public ListPropertyHandle GetListProperty(string path) =>
            GetProperty(path, ViewModelDataType.List, p => new ListPropertyHandle(this, p));

        /// <inheritdoc cref="GetNumberProperty"/>
        /// <summary>Gets an image property.</summary>
        public ImagePropertyHandle GetImageProperty(string path) =>
            GetProperty(path, ViewModelDataType.AssetImage, p => new ImagePropertyHandle(this, p));

        /// <inheritdoc cref="GetNumberProperty"/>
        /// <summary>Gets a font property.</summary>
        public FontPropertyHandle GetFontProperty(string path) =>
            GetProperty(path, ViewModelDataType.AssetFont, p => new FontPropertyHandle(this, p));

        /// <inheritdoc cref="GetNumberProperty"/>
        /// <summary>Gets an artboard property.</summary>
        public ArtboardPropertyHandle GetArtboardProperty(string path) =>
            GetProperty(path, ViewModelDataType.Artboard, p => new ArtboardPropertyHandle(this, p));

        /// <summary>
        /// Gets a nested view model instance.
        /// </summary>
        /// <param name="path">The property's name, or a path through nested view models.</param>
        /// <returns>The handle, straight away, before Rive has looked it up. It refers to the instance at the path when Rive gets to it, and keeps referring to that one if the property is replaced later. If there's none, it refers to nothing: work queued on it is skipped, and that's reported once.</returns>
        public ViewModelInstanceHandle GetViewModelInstanceProperty(string path)
        {
            if (!CheckUsable(nameof(GetViewModelInstanceProperty)) || !CheckPath(path))
            {
                return null;
            }
            var slot = new NativeSlot<NativeViewModelInstanceHandle>();
            int nested = m_contents != null ? m_contents.ViewModelAt(m_viewModelIndex, path) : -1;
            var handle = new ViewModelInstanceHandle(slot, HandleResolution.Pending(), m_contents, nested, owned: true)
            {
                ErrorSink = ErrorSink
            };
            ViewModelInstanceNative.GetViewModelLater(this, path, handle, HandleErrors.CaptureCallSite());
            return handle;
        }

        /// <summary>
        /// Replaces a nested view model instance. Queued, so it applies before the next advance.
        /// </summary>
        /// <param name="path">The nested view model property's name, or a path through nested view models.</param>
        /// <param name="instance">The instance to put there. The property keeps it alive.</param>
        /// <remarks>
        /// Property handles with paths through it follow the new instance. A path that isn't a nested view model property is reported once Rive gets to it.
        /// </remarks>
        public void SetViewModelInstanceProperty(string path, ViewModelInstanceHandle instance)
        {
            if (!CheckUsable(nameof(SetViewModelInstanceProperty)) || !CheckPath(path))
            {
                return;
            }
            if (instance == null || instance.IsDisposed)
            {
                DebugLogger.Instance.LogError(
                    $"SetViewModelInstanceProperty: the instance for '{path}' is {(instance == null ? "null" : "disposed")}.");
                return;
            }
            ViewModelInstanceNative.ReplaceViewModelLater(this, path, instance, HandleErrors.CaptureCallSite());
        }

        /// <summary>
        /// True once the handle has been disposed, or its widget has unloaded it.
        /// </summary>
        public bool IsDisposed => m_isDisposed;

        /// <summary>
        /// Releases this handle's hold on the instance and drops its property subscriptions. The instance lives on while a state machine or list still uses it. Does nothing for a handle a widget owns.
        /// </summary>
        public void Dispose()
        {
            if (m_isDisposed)
            {
                return;
            }
            if (!m_owned)
            {
                DebugLogger.Instance.LogWarning(
                    "This view model instance belongs to a widget, which releases it. Dispose was ignored.");
                return;
            }
            Release();
            ViewModelInstanceNative.ReleaseLater(m_native);
            GC.SuppressFinalize(this);
        }

        /// For a widget's view, when it unloads, and Dispose.
        internal void Release()
        {
            m_isDisposed = true;
            m_resolution.MarkDisposed("The view model instance");
            // Callbacks stay, so changes captured before this still arrive.
            foreach (ViewModelPropertyHandle property in m_properties.Values)
            {
                PropertyCallbacksHub.Instance.UnregisterHandle(property);
                property.MarkDisposed();
            }
        }

        private T GetProperty<T>(string path, ViewModelDataType type, Func<string, T> create)
            where T : ViewModelPropertyHandle
        {
            if (!CheckPath(path))
            {
                return null;
            }
            string key = (int)type + ":" + path;
            if (m_properties.TryGetValue(key, out ViewModelPropertyHandle existing))
            {
                return (T)existing;
            }
            T property = create(path);
            m_properties.Add(key, property);
            return property;
        }

        private static bool CheckPath(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                return true;
            }
            DebugLogger.Instance.LogError("A property path can't be null or empty.");
            return false;
        }

        private bool CheckUsable(string what)
        {
            if (!m_isDisposed)
            {
                return true;
            }
            DebugLogger.Instance.LogError($"{what}: the view model instance has been disposed.");
            return false;
        }
    }
}
