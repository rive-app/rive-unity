using System;
using System.Collections.Generic;
using System.Threading.Tasks;

#if UNITY_EDITOR
using System.Linq;
#endif
using Rive.EditorTools;
using Rive.Components.Utilities;
using Rive.Host;
using Rive.Utils;
using UnityEngine;
using UnityEngine.Events;

namespace Rive.Components
{
    /// <summary>
    /// This component is used to display a Rive file within a Rive Panel.
    /// </summary>
#if UNITY_EDITOR
    [HelpURL(InspectorDocLinks.RiveWidget)]
    [InspectorSection(WidgetInspectorSections.FileSettings, "File Settings")]
    [InspectorSection(WidgetInspectorSections.Display, "Display")]
    [InspectorSection(WidgetInspectorSections.Input, "Input")]
    [InspectorSection(WidgetInspectorSections.Data, "Data")]
#endif
    [AddComponentMenu("Rive/Rive Widget")]
    public sealed class RiveWidget : WidgetBehaviour, ISerializationCallbackReceiver
    {
        /// <summary>
        /// Determines whether ReportedEvents are pooled or not.
        /// </summary>
        public enum EventPoolingMode
        {
            /// <summary>
            /// Events are pooled and reused.
            /// </summary>
            Enabled = 0,
            /// <summary>
            /// Events are not pooled. A new event is created each time an event is reported.
            /// </summary>
            Disabled = 1
        }

        /// <summary>
        /// Determines how the widget should handle binding to a ViewModel instance.
        /// </summary>
        public enum DataBindingMode
        {
            /// <summary>
            /// Automatically binds to the default instance if available
            /// </summary>
            AutoBindDefault = 0,

            /// <summary>
            /// Automatically binds to a selected instance if available
            /// </summary>
            AutoBindSelected = 1,

            /// <summary>
            /// No automatic binding
            /// </summary>
            Manual = 2
        }

        private static class WidgetInspectorSections
        {

            public const string FileSettings = "file-settings";

            public const string Display = "display";
            public const string Input = "input";

            public const string Advanced = "advanced";

            public const string Data = "data";

        }

#if UNITY_EDITOR
        [OnValueChanged(nameof(OnAssetChangedInEditor))]
        [InspectorField(WidgetInspectorSections.FileSettings, helpUrl: InspectorDocLinks.AddingRiveAssets)]
#endif
        [Tooltip("The Rive file (.riv) to load.")]
        [SerializeField] private Asset m_asset;



#if UNITY_EDITOR
        [HideIf(nameof(ShouldHideArtboardNameAndStateMachineName))]
        [InspectorField(WidgetInspectorSections.FileSettings, helpUrl: InspectorDocLinks.Artboards)]
        [Tooltip("The name of the artboard to load.")]
        [OnValueChanged(nameof(OnArtboardChangedInEditor))]
        [Dropdown(nameof(GetDisplayArtboardNames), trackChanges: true)]
#endif
        [SerializeField] private string m_artboardName;

#if UNITY_EDITOR
        [HideIf(nameof(ShouldHideArtboardNameAndStateMachineName))]
        [InspectorField(WidgetInspectorSections.FileSettings, helpUrl: InspectorDocLinks.StateMachines)]
        [OnValueChanged(nameof(OnStateMachineChangedInEditor))]
        [Dropdown(nameof(GetDisplayStateMachineNames), trackChanges: true)]
#endif
        [Tooltip("The name of the state machine to load.")]
        [SerializeField] private string m_stateMachineName;




#if UNITY_EDITOR
        [InspectorField(WidgetInspectorSections.Display, helpUrl: InspectorDocLinks.FitAndAlignment)]
        [OnValueChanged(nameof(OnFitChangedInEditor))]
#endif
        [Tooltip("The fit mode to use when drawing the artboard.")]
        [SerializeField] private Fit m_fit = Fit.Contain;

#if UNITY_EDITOR
        [InspectorField(WidgetInspectorSections.Display, helpUrl: InspectorDocLinks.FitAndAlignment)]
        [HideIf(nameof(ShouldHideAlignment))]
        [OnValueChanged(nameof(OnAlignmentChangedInEditor))]
        [Tooltip("The alignment to use when drawing the artboard.")]
#endif
        [SerializeField]
        private Alignment m_alignment = Alignment.Center;

#if UNITY_EDITOR
        [ShowIf(nameof(ShouldShowLayoutOptions))]
        [InspectorField(WidgetInspectorSections.Display, helpUrl: InspectorDocLinks.LayoutScaleFactor)]
        [OnValueChanged(nameof(OnScaleFactorChangedInEditor))]
#endif
        [Tooltip("The scale factor to use when drawing the artboard when using the Layout fit mode. Increase this value to make the artboard appear larger.")]
        [SerializeField] private float m_layoutScaleFactor = 1.0f;

#if UNITY_EDITOR
        [ShowIf(nameof(ShouldShowLayoutOptions))]
        [InspectorField(WidgetInspectorSections.Display, helpUrl: InspectorDocLinks.LayoutScalingModes)]
        [OnValueChanged(nameof(OnLayoutScalingModeChangedInEditor))]
#endif
        [Tooltip("The layout scaling mode to use when drawing the artboard.")]
        [SerializeField] private LayoutScalingMode m_layoutScalingMode = LayoutScalingMode.ReferenceArtboardSize;


#if UNITY_EDITOR
        [InspectorField(WidgetInspectorSections.Display)]
        [ShowIf(nameof(ShouldShowDpiFields))]
        [OnValueChanged(nameof(OnFallbackDPIChanged))]
#endif
        [Tooltip("Fallback DPI to use if the screen DPI is not available.")]
        [SerializeField] private float m_fallbackDPI = 96f;

#if UNITY_EDITOR
        [InspectorField(WidgetInspectorSections.Display)]
        [ShowIf(nameof(ShouldShowDpiFields))]
        [OnValueChanged(nameof(OnReferenceDPIChanged))]
#endif
        [Tooltip("Specifies the screen density (DPI) that your UI is targeting as its baseline. For example, if you're targeting standard desktop displays (96 DPI) as your 1x baseline, set this to 96. This will be used to calculate the Device Pixel Ratio (screen DPI / reference DPI) and scale your content accordingly. This ensures your UI appears at the intended physical size across different screen densities. Similar to how @1x, @2x, @3x works on the web, where a DPR of 2 means the screen is twice as dense as your reference.")]
        [SerializeField] private float m_referenceDPI = 150f;


#if UNITY_EDITOR

        [Tooltip("Controls how hit testing is handled:\n" +
                 "\n" +
                 "- Opaque blocks all hits.\n" +
                 "\n" +
                 "- Translucent only blocks hits on listeners.\n" +
                 "\n" +
                 "- None disables hit testing completely.\n" +
                 "\n" +
                 "Opaque is recommended for asynchronous panel pointer input. Translucent uses synchronous hit testing and pointer handling so Unity can resolve raycasts immediately.")]
        [InspectorField(WidgetInspectorSections.Input, helpUrl: InspectorDocLinks.HitTesting)]
#endif
        [SerializeField] private HitTestBehavior m_hitTestBehavior = HitTestBehavior.Opaque;

#if UNITY_EDITOR
        [Tooltip("Determines whether ReportedEvents are pooled or not. If disabled, a new event is created each time an event is reported.")]
        [InspectorField(WidgetInspectorSections.Advanced)]
#endif
        [SerializeField] private EventPoolingMode m_eventPoolingMode = EventPoolingMode.Enabled;

        [Tooltip("Controls the playback speed of the graphic. A value of 1 is normal speed, 2 is double speed, 0.5 is half speed")]
        [InspectorField(WidgetInspectorSections.Advanced)]
        [SerializeField] private float m_speed = 1.0f;

#if UNITY_EDITOR
        [Tooltip("Determines how the widget should handle binding to a ViewModel instance.")]
        [InspectorField(WidgetInspectorSections.Data, helpUrl: InspectorDocLinks.UnityDataBindingOverview)]
        [OnValueChanged(nameof(OnDataBindingModeChangedInEditor))]
#endif
        [SerializeField] private DataBindingMode m_dataBindingMode = DataBindingMode.AutoBindDefault;



        [Tooltip("The ViewModel instance to bind to.")]
        [InspectorField(WidgetInspectorSections.Data, displayName: "ViewModel Instance")]
#if UNITY_EDITOR
        [ShowIf(nameof(ShouldShowDataBindingInstanceField))]
        [Dropdown(nameof(GetViewModelInstanceNames), trackChanges: true)]
#endif
        [SerializeField] private string m_viewModelInstanceName;


        [Tooltip("Optional custom audio provider to use for audio playback. If not set, a shared global audio provider will be used.")]
        [InspectorField(WidgetInspectorSections.Advanced)]
        [SerializeField] private AudioProvider m_customAudioProvider = null;


        bool m_needsLayoutRecalculationFix = false;
        private int m_artboardSizeVersion;


        private ArtboardLoadHelper m_controller;


#if UNITY_EDITOR
        [OnValueChanged(nameof(OnScaleFactorChangedInEditor))]
#endif
        private bool m_useFallbackDPI = true;



        /// <summary>
        /// If true, the widget will use the fallback DPI value when calculating the effective scale factor instead of the screen DPI.
        /// </summary>
        internal bool UseFallbackDPI
        {
            get => m_useFallbackDPI;
            set
            {
                if (m_useFallbackDPI != value)
                {
                    m_useFallbackDPI = value;
                    OnFallbackDPIChanged();
                }
            }
        }

        private ArtboardLoadHelper Controller
        {
            get
            {
                if (m_isDestroyed)
                {
                    return null;
                }

                if (m_controller == null)
                {
                    m_controller = new ArtboardLoadHelper();

                    SubscribeToControllerEvents(m_controller);

                }

                return m_controller;
            }
        }

        public override IRenderObject RenderObject { get => Controller?.RenderObject; }


        /// <summary>
        /// The Rive file that is currently loaded. Null in a <see cref="ThreadingMode.BackgroundThread"/> panel, where <see cref="FileHandle"/> is used instead.
        /// </summary>
        public File File { get => MainThreadOnly(Controller?.File); }


        /// <summary>
        /// The artboard that is currently loaded. Null in a <see cref="ThreadingMode.BackgroundThread"/> panel, where <see cref="ArtboardHandle"/> is used instead.
        /// </summary>
        public Artboard Artboard { get => MainThreadOnly(Controller?.Artboard); }

        /// <summary>
        /// The state machine that is currently loaded. Null in a <see cref="ThreadingMode.BackgroundThread"/> panel, where <see cref="StateMachineHandle"/> is used instead.
        /// </summary>
        public StateMachine StateMachine { get => MainThreadOnly(Controller?.StateMachine); }

        /// <summary>
        /// The loaded file, in a <see cref="ThreadingMode.BackgroundThread"/> panel. Null otherwise. The widget owns it.
        /// </summary>
        public FileHandle FileHandle => m_fileHandle;

        /// <summary>
        /// The loaded artboard, in a <see cref="ThreadingMode.BackgroundThread"/> panel. Null otherwise. The widget owns it.
        /// </summary>
        public ArtboardHandle ArtboardHandle => m_artboardHandle;

        /// <summary>
        /// The loaded state machine, in a <see cref="ThreadingMode.BackgroundThread"/> panel. Null otherwise. The widget owns it.
        /// </summary>
        public StateMachineHandle StateMachineHandle => m_stateMachineHandle;

        // What the widget itself uses, whichever family the load gave.
        internal File LoadedFile => Controller?.File;
        internal Artboard LoadedArtboard => Controller?.Artboard;
        internal StateMachine LoadedStateMachine => Controller?.StateMachine;

        // The family the current load uses, taken from the panel when it started.
        private ThreadingMode m_family = ThreadingMode.MainThread;
        private bool m_loggedFamily;
        private FileHandle m_fileHandle;
        private ArtboardHandle m_artboardHandle;
        private StateMachineHandle m_stateMachineHandle;
        // A FileHandle the caller passed in, handed back as is.
        private FileHandle m_givenFileHandle;

        private T MainThreadOnly<T>(T value) where T : class
        {
            if (value == null || m_family == ThreadingMode.MainThread)
            {
                return value;
            }
            if (!m_loggedFamily)
            {
                m_loggedFamily = true;
                DebugLogger.Instance.LogWarning(
                    $"{name} is in a BackgroundThread panel, so it gives handles. Use FileHandle, ArtboardHandle and StateMachineHandle.");
            }
            return null;
        }

        // The panel decides the family. A widget with no panel stays on the main thread.
        private ThreadingMode PanelThreadingMode
        {
            get
            {
                RivePanel panel = RivePanel != null ? RivePanel : GetComponentInParent<RivePanel>();
                return panel != null ? panel.ThreadingMode : ThreadingMode.MainThread;
            }
        }


        private static Future CompletedLoad()
        {
            var state = new FutureState<bool>();
            state.Succeed(true);
            return new Future(state);
        }

        private void CreateHandleViews()
        {
            File file = Controller.File;
            FileContents contents = m_givenFileHandle != null ? m_givenFileHandle.Contents : Controller.Contents;
            if (file == null || contents == null)
            {
                return;
            }
            m_fileHandle = m_givenFileHandle ?? new FileHandle(file, contents, owned: false);

            int artboardIndex = string.IsNullOrEmpty(m_artboardName) ? 0 : contents.ArtboardIndex(m_artboardName);
            if (artboardIndex < 0 || artboardIndex >= contents.Artboards.Length || Controller.Artboard == null)
            {
                return;
            }
            FileContents.ArtboardInfo info = contents.Artboards[artboardIndex];
            m_artboardHandle = new ArtboardHandle(
                new NativeSlot<NativeArtboardHandle>(Controller.Artboard.NativeArtboard), HandleResolution.Known(), m_fileHandle, info, owned: false,
                Controller.Artboard.Lifetime);

            if (Controller.StateMachine != null)
            {
                // The load takes the first state machine when none is named.
                string stateMachineName = !string.IsNullOrEmpty(m_stateMachineName)
                    ? m_stateMachineName
                    : (info.StateMachineNames.Length > 0 ? info.StateMachineNames[0] : null);
                m_stateMachineHandle = new StateMachineHandle(
                    new NativeSlot<NativeStateMachineHandle>(Controller.StateMachine.NativeStateMachine),
                    HandleResolution.Known(), m_artboardHandle, stateMachineName, owned: false,
                    Controller.StateMachine.Lifetime);
                m_stateMachineHandle.ErrorSink = RaiseError;
            }
        }

        private void ReleaseHandleViews()
        {
            m_stateMachineHandle?.Release();
            m_artboardHandle?.Release();
            if (m_fileHandle != null && !ReferenceEquals(m_fileHandle, m_givenFileHandle))
            {
                m_fileHandle.Release();
            }
            m_fileHandle = null;
            m_artboardHandle = null;
            m_stateMachineHandle = null;
        }


        public Fit Fit
        {
            get => m_fit; set
            {
                if (m_fit != value)
                {
                    m_fit = value;
                    OnFitChanged();
                }
            }
        }

        public Alignment Alignment
        {
            get => m_alignment;
            set
            {
                if (m_alignment != value)
                {
                    m_alignment = value;
                    OnAlignmentChanged();
                }
            }
        }

        /// <summary>
        /// The scale factor to use when drawing the artboard when using the Layout fit mode.
        /// </summary>
        public float ScaleFactor
        {
            get
            {
                // We return the user set scale factor as that is what's used to calculate the effective scale factor.
                return m_layoutScaleFactor;
            }
            set
            {
                if (m_layoutScaleFactor == value)
                {
                    return;
                }
                m_layoutScaleFactor = value;

                OnScaleFactorChanged();

            }
        }


        /// <summary>
        /// The layout scaling mode to use when drawing the artboard.
        /// </summary>
        public LayoutScalingMode ScalingMode
        {
            get
            {
                if (RenderObjectWithArtboard == null)
                {
                    return m_layoutScalingMode;
                }
                return m_layoutScalingMode;
            }
            set
            {
                if (m_layoutScalingMode == value)
                {
                    return;
                }

                m_layoutScalingMode = value;

                OnScaleModeChanged();
            }
        }

        /// <summary>
        /// The name of the artboard that is currently loaded.
        /// </summary>
        public string ArtboardName { get => m_artboardName; }

        /// <summary>
        /// The name of the state machine that is currently loaded.
        /// </summary>
        public string StateMachineName { get => m_stateMachineName; }

        /// <summary>
        /// The asset that is currently loaded.
        /// </summary>
        public Asset Asset { get => m_asset; }



        internal ArtboardRenderObject RenderObjectWithArtboard => Controller?.RenderObject;

        /// The producer half of what's loaded, or null.
        internal WidgetCore Core => Controller?.Core;

        /// <summary>
        /// The DPI to use if the screen DPI is not available.
        /// </summary>
        public float FallbackDPI
        {
            get => m_fallbackDPI; set
            {
                if (m_fallbackDPI != value)
                {
                    m_fallbackDPI = value;
                    OnFallbackDPIChanged();
                }
            }
        }

        /// <summary>
        /// Specifies the screen density (DPI) that your UI is targeting as its baseline. For example, if you're targeting standard desktop displays (96 DPI) as your 1x baseline, set this to 96. This will be used to calculate the Device Pixel Ratio (screen DPI / reference DPI) and scale your content accordingly. This ensures your UI appears at the intended physical size across different screen densities. Similar to how @1x, @2x, @3x works on the web, where a DPR of 2 means the screen is twice as dense as your reference.
        /// </summary>
        public float ReferenceDPI
        {
            get => m_referenceDPI; set
            {
                if (m_referenceDPI != value)
                {
                    m_referenceDPI = value;
                    OnReferenceDPIChanged();
                }
            }
        }


        public override HitTestBehavior HitTestBehavior { get => m_hitTestBehavior; set => m_hitTestBehavior = value; }

        /// <summary>
        /// Determines whether ReportedEvents are pooled or not.
        /// </summary>
        public EventPoolingMode ReportedEventPoolingMode { get => m_eventPoolingMode; set => m_eventPoolingMode = value; }

        /// <summary>
        /// Determines how the widget should handle binding to a ViewModel instance.
        /// </summary>
        public DataBindingMode BindingMode
        {
            get => m_dataBindingMode;
            set
            {
                m_dataBindingMode = value;
            }
        }

        /// <summary>
        /// The name of the ViewModel instance to bind to.
        /// </summary>
        public string ViewModelInstanceName { get => m_viewModelInstanceName; set => m_viewModelInstanceName = value; }

        /// <summary>
        /// Controls the playback speed of the graphic. A value of 1 is normal speed, 2 is double speed, 0.5 is half speed.
        /// </summary>
        public float Speed
        {
            get => m_speed;
            set
            {
                m_speed = value;
            }
        }

        /// <summary>
        /// Event that is triggered when a Rive event is reported.
        /// </summary>
        public event Action<ReportedEvent> OnRiveEventReported;



        private Asset m_fileLoadedFromAsset = null;

        private bool m_isDestroyed = false;

        private static AudioProvider s_globalAudioProvider = null;

        /// <summary>
        /// The shared audio provider for all Rive widgets if the user has not assigned one to the widget.
        /// </summary>
        private static AudioProvider GlobalAudioProvider
        {
            get
            {
                if (!Application.isPlaying)
                {
                    return null;
                }

                if (s_globalAudioProvider == null)
                {
                    // Since the rpHandler already exists, we can store the global audio provider on it.
                    MonoBehaviour rpHandler = RenderPipelineHelper.CurrentHandler as MonoBehaviour;
                    GameObject globalAudioProviderObject = rpHandler == null ? new GameObject("GlobalAudioProvider") : rpHandler.gameObject;

                    // If we spawned the gameobject because the rpHandler wasn't in the scene, we need to set it to not destroy on load.
                    if (rpHandler == null)
                    {
                        DontDestroyOnLoad(globalAudioProviderObject);
                    }

                    s_globalAudioProvider = globalAudioProviderObject.AddComponent<AudioProvider>();
                }

                return s_globalAudioProvider;
            }
        }

        /// <summary>
        /// The AudioProvider currently used by this widget for audio playback.
        /// This will be the custom provider if one has been assigned; otherwise, it will be the shared global provider.
        /// On WebGL builds, this will always be null since AudioProvider is not supported on that platform.
        /// </summary>
        internal AudioProvider AudioProvider => m_customAudioProvider == null ? GlobalAudioProvider : m_customAudioProvider;

        /// <summary>
        /// An optional custom AudioProvider override for this widget.
        /// When set to a non-null value, the widget will use this provider instead of the shared global provider.
        /// When set to null, the widget will fall back to using the shared global provider.
        /// </summary>
        /// <remarks> On WebGL, custom AudioProviders are not supported. Audio will be routed through the system instead of Unity's AudioSource.</remarks>
        public AudioProvider CustomAudioProvider
        {
            get => m_customAudioProvider;
            set
            {
                m_customAudioProvider = value;
                SetUpAudioIfNeeded(Controller?.Artboard);
            }
        }


        protected override void OnEnable()
        {
            base.OnEnable();

        }

        // HitTestBehavior.Transparent was 2. Scenes saved with it get its
        // replacement rather than a value that hits nothing.
        private const int RemovedTransparentHitTest = 2;

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if ((int)m_hitTestBehavior == RemovedTransparentHitTest)
            {
                m_hitTestBehavior = HitTestBehavior.Translucent;
            }
        }


        private void Start()
        {
            if (m_asset == null || Status != WidgetStatus.Uninitialized)
            {
                return;
            }

            // Nothing awaits this. The widget's status is how the load reports back.
            _ = LoadFromAssetForPanel();
        }


        internal override void DispatchAdvanceCallbacks()
        {
            if (Controller == null || Status != WidgetStatus.Loaded)
            {
                return;
            }

            Controller.DispatchCollectedEvents(ReportedEventPoolingMode);
        }

        internal override void PrepareAdvance(WidgetAdvance slot)
        {
            slot.Core = Core;
            slot.Active = slot.Core != null && Status == WidgetStatus.Loaded;
            if (!slot.Active)
            {
                return;
            }
            slot.Speed = Speed;
            slot.CollectEvents = OnRiveEventReported != null;
            // Before the advance, because after it affects triggers on the first frame.
            slot.LayoutFix = m_needsLayoutRecalculationFix;
            m_needsLayoutRecalculationFix = false;
            slot.SizeVersion = m_artboardSizeVersion;
        }

        internal override bool WriteAdvance(WidgetAdvance slot, PayloadWriter entries)
        {
            if (!slot.Active)
            {
                return false;
            }
            // Layout and data binding can resize the artboard, so the size
            // comes back with the advance and drawing never has to ask.
            slot.Core.WriteTick(entries, slot.Delta, slot.Speed, slot.CollectEvents, slot.LayoutFix);
            return true;
        }

        internal override void ApplyAdvance(WidgetAdvance slot)
        {
            // A local resize since it went out is newer than what it saw.
            if (!slot.HasSize || slot.SizeVersion != m_artboardSizeVersion)
            {
                return;
            }
            // Reloaded while it was out.
            if (!ReferenceEquals(Core, slot.Core))
            {
                return;
            }
            RenderObjectWithArtboard?.SetArtboardSize(slot.ArtboardSize);
        }

        // Keeps the drawn size in step with a resize made here, ahead of the
        // advance that will confirm it.
        private void SetArtboardSizeLocally(Vector2 size)
        {
            m_artboardSizeVersion++;
            RenderObjectWithArtboard?.SetArtboardSize(size);
        }

        private Vector2 CurrentArtboardSize =>
            RenderObjectWithArtboard != null && RenderObjectWithArtboard.TryGetArtboardSize(out Vector2 size)
                ? size
                : (Vector2)LoadedArtboard.Size;

        private void SubscribeToControllerEvents(ArtboardLoadHelper controller)
        {
            if (controller == null)
            {
                return;
            }

            controller.OnRiveEventReported += HandleRiveEventReported;
        }
        private void UnsubscribeFromControllerEvents(ArtboardLoadHelper controller)
        {
            if (controller == null)
            {
                return;
            }
            controller.OnRiveEventReported -= HandleRiveEventReported;

        }

        private void HandleRiveEventReported(ReportedEvent report)
        {
            OnRiveEventReported?.Invoke(report);
        }

        private void HandleLoadError(ArtboardLoadHelper.LoadErrorEventData eventData)
        {
            RiveErrorCode code;
            switch (eventData.ErrorType)
            {
                case ArtboardLoadHelper.LoadErrorType.ArtboardNotFound:
                    code = RiveErrorCode.ArtboardNotFound;
                    break;
                case ArtboardLoadHelper.LoadErrorType.StateMachineNotFound:
                    code = RiveErrorCode.StateMachineNotFound;
                    break;
                default:
                    code = RiveErrorCode.LoadFailed;
                    break;
            }
            Fail(new RiveException(code, eventData.Message ?? "The load failed."));
        }

        /// <summary>
        /// Raised on the main thread when something goes wrong: a load that fails, or, in a <see cref="ThreadingMode.BackgroundThread"/> panel, a problem found after a handle call returned, like a property path that doesn't exist or a list index that's out of range.
        /// </summary>
        /// <remarks>
        /// Load failures also set <see cref="WidgetBehaviour.Status"/> to <see cref="WidgetStatus.Error"/>, after this is raised. Each error is logged as well.
        /// </remarks>
        public event Action<RiveException> OnError;

        private void Fail(RiveException error)
        {
            RaiseError(error);
            Status = WidgetStatus.Error;
        }

        private static RiveException AsRiveError(Exception error)
        {
            return error as RiveException ?? new RiveException(RiveErrorCode.LoadFailed, error?.Message ?? "The load failed.");
        }

        private void RaiseError(RiveException error)
        {
            try
            {
                OnError?.Invoke(error);
            }
            catch (Exception e)
            {
                DebugLogger.Instance.LogException(e);
            }
        }



        /// <summary>
        /// Flips the normalized point on the y-axis if needed, based on the graphics API. If we don't do this, the pointer interaction will be positioned incorrectly on some platforms.
        /// </summary>
        /// <param name="normalizedPoint"></param>
        /// <returns> The normalized point with the y-axis flipped if needed.</returns>
        private Vector2 FlipNormalizedPointIfNeeded(Vector2 normalizedPoint)
        {
            bool shouldFlip = TextureHelper.ShouldFlipInput();

            if (shouldFlip)
            {
                normalizedPoint.y = 1 - normalizedPoint.y;
            }

            return normalizedPoint;
        }

        internal enum PointerEventKind
        {
            Down = 0,
            Up = 1,
            Move = 2,
            Exit = 3,
            Enter = 4
        }

        internal struct PointerEventWork
        {
            internal WidgetCore Core;
            internal Vector2 ScreenPosition;
            internal Rect ScreenRect;
            internal Fit Fit;
            internal Alignment Alignment;
            internal bool CollectEvents;
            internal int PointerId;
            internal PointerEventKind Kind;
        }

        internal bool TryPreparePointerEvent(
            Vector2 normalizedPoint,
            int pointerId,
            PointerEventKind kind,
            out PointerEventWork work)
        {
            work = default;
            WidgetCore core = Core;
            if (core == null || Status != WidgetStatus.Loaded || RenderObjectWithArtboard == null)
            {
                return false;
            }

            GetPointerCoordinates(normalizedPoint, out Rect rect, out Vector2 screenPosition);
            work = new PointerEventWork
            {
                Core = core,
                ScreenPosition = screenPosition,
                ScreenRect = rect,
                Fit = RenderObjectWithArtboard.Fit,
                Alignment = RenderObjectWithArtboard.Alignment,
                CollectEvents = OnRiveEventReported != null,
                PointerId = pointerId,
                Kind = kind
            };
            return true;
        }

        internal void CompletePointerEvent(PointerEventKind kind, bool hit)
        {
            if (hit && (kind == PointerEventKind.Down || kind == PointerEventKind.Up))
            {
                TriggerRedrawNeededEvent();
            }
        }

        private void GetPointerCoordinates(
            Vector2 normalizedPoint,
            out Rect rect,
            out Vector2 screenPosition)
        {
            normalizedPoint = FlipNormalizedPointIfNeeded(normalizedPoint);
            rect = RectTransform.rect;
            if (Fit == Fit.Layout)
            {
                float effectiveScale = GetEffectiveScaleFactor();
                rect = new Rect(0, 0, rect.width / effectiveScale, rect.height / effectiveScale);
            }
            else
            {
                rect = new Rect(0, 0, rect.width, rect.height);
            }

            screenPosition = new Vector2(normalizedPoint.x * rect.width,
                normalizedPoint.y * rect.height);
        }

        /// <summary>
        /// Tries to get the Rive point from the local normalized point in the frame.
        /// </summary>
        /// <param name="localNormalizedPointInFrame"> The local normalized point in the frame.</param>
        /// <param name="rivePoint"> The point in Rive coordinates.</param>
        /// <returns> True if the Rive point was successfully retrieved, false otherwise.</returns>
        private bool TryGetRivePoint(Vector2 localNormalizedPointInFrame, out Vector2 rivePoint)
        {
            rivePoint = Vector2.zero;
            GetPointerCoordinates(localNormalizedPointInFrame, out Rect rect,
                out Vector2 riveScreenPosition);
            Fit fit = RenderObjectWithArtboard.Fit;
            Alignment alignment = RenderObjectWithArtboard.Alignment;
            Artboard artboard = RenderObjectWithArtboard.Artboard;

            rivePoint = artboard.LocalCoordinate(
                riveScreenPosition,
                rect,
                fit,
                alignment
            );

            return true;
        }


        public override bool HitTest(Vector2 normalizedPointInRect)
        {
            RivePanel?.JoinForSynchronousPointerInput();
            Vector2 rivePoint;

            if (!TryGetRivePoint(normalizedPointInRect, out rivePoint))
            {
                return false;
            }

            return LoadedStateMachine.HitTest(rivePoint);
        }

        // Synchronous pointer input. Events a listener fires during the call
        // come back with it, before the next advance clears them.
        private HitResult PointerAndWait(Vector2 normalizedPoint, int pointerId, PointerEventKind kind)
        {
            if (!TryPreparePointerEvent(normalizedPoint, pointerId, kind, out PointerEventWork work))
            {
                return HitResult.None;
            }
            return (HitResult)work.Core.PointerAndWait(work);
        }

        /// <summary>
        /// Advances the state machine after a pointer event occurs on the widget. This is needed to ensure that intermediate state, such as a view model property set on pointer down, is processed immediately, even when the down and up occur within the same frame.
        /// </summary>
        private void AdvanceAfterPointerEvent()
        {
            if (Controller == null || Status != WidgetStatus.Loaded)
            {
                return;
            }

            Controller.DispatchCollectedEvents(ReportedEventPoolingMode);
            Core?.AdvanceAndWait(0f, Speed, OnRiveEventReported != null);
            Orchestrator.Instance?.FlushPropertyCallbacksForImmediateAdvance();
            TriggerRedrawNeededEvent();
        }


        public override bool OnPointerDown(Vector2 normalizedPointInRect, int pointerId)
        {
            if (LoadedStateMachine == null)
            {
                return false;
            }
            Vector2 rivePoint;
            if (!TryGetRivePoint(normalizedPointInRect, out rivePoint))
            {
                return false;
            }

            HitResult hitResult = PointerAndWait(normalizedPointInRect, pointerId, PointerEventKind.Down);
            if (hitResult != HitResult.None)
            {
                AdvanceAfterPointerEvent();
            }

            return hitResult != HitResult.None;
        }

        /// <summary>
        /// Called when a pointer is released on the widget.
        /// </summary>
        /// <param name="normalizedPointInRect">The normalized point of the pointer release in the widget's rectangle. The coordinates are in the range [0,1] where (0,0) is the bottom-left corner and (1,1) is the top-right corner.</param>
        /// <param name="pointerId">The unique id for the active pointer/touch.</param>
        public override bool OnPointerUp(Vector2 normalizedPointInRect, int pointerId)
        {
            if (LoadedStateMachine == null)
            {
                return false;
            }
            Vector2 rivePoint;
            if (!TryGetRivePoint(normalizedPointInRect, out rivePoint))
            {
                return false;
            }

            HitResult hitResult = PointerAndWait(normalizedPointInRect, pointerId, PointerEventKind.Up);
            if (hitResult != HitResult.None)
            {
                AdvanceAfterPointerEvent();
            }

            return hitResult != HitResult.None;

        }

        /// <summary>
        /// Called when a pointer is moved on the widget.
        /// </summary>
        /// <param name="normalizedPointInRect">The normalized point of the pointer position in the widget's rectangle. The coordinates are in the range [0,1] where (0,0) is the bottom-left corner and (1,1) is the top-right corner.</param>
        /// <param name="pointerId">The unique id for the active pointer/touch.</param>
        public override bool OnPointerMove(Vector2 normalizedPointInRect, int pointerId)
        {
            if (LoadedStateMachine == null)
            {
                return false;
            }

            Vector2 rivePoint;
            if (!TryGetRivePoint(normalizedPointInRect, out rivePoint))
            {
                return false;
            }

            HitResult hitResult = PointerAndWait(normalizedPointInRect, pointerId, PointerEventKind.Move);

            return hitResult != HitResult.None;

        }

        public override bool OnPointerExit(Vector2 normalizedPointInRect, int pointerId)
        {
            if (LoadedStateMachine == null)
            {
                return false;
            }

            Vector2 rivePoint;
            if (!TryGetRivePoint(normalizedPointInRect, out rivePoint))
            {
                return false;
            }
            HitResult hitResult = PointerAndWait(normalizedPointInRect, pointerId, PointerEventKind.Exit);

            return hitResult != HitResult.None;

        }

        public override bool OnPointerEnter(Vector2 normalizedPointInRect, int pointerId)
        {
            if (LoadedStateMachine == null)
            {
                return false;
            }

            Vector2 rivePoint;
            if (!TryGetRivePoint(normalizedPointInRect, out rivePoint))
            {
                return false;
            }

            // Rive has no pointer enter, so a move tells it where the pointer is.
            HitResult hitResult = PointerAndWait(normalizedPointInRect, pointerId, PointerEventKind.Move);

            return hitResult != HitResult.None;

        }


        /// <summary>
        /// Internal method to handle loading from either an asset or direct file
        /// </summary>
        private void LoadInternal(File file, Asset fromAsset = null)
        {

            if (file == null)
            {
                DebugLogger.Instance.LogError("Attempted to load a null Rive file.");
                Fail(new RiveException(RiveErrorCode.LoadFailed, "Attempted to load a null Rive file."));
                return;
            }

            ReleaseHandleViews();
            m_family = ThreadingMode.MainThread;
            Status = WidgetStatus.Loading;
            m_fileLoadedFromAsset = fromAsset;
            ArtboardLoadHelper.LoadResult result = Controller.Load(file, m_fit, m_alignment, m_artboardName, m_stateMachineName, GetEffectiveScaleFactor(), new ArtboardLoadHelper.DataBindingLoadInfo(BindingMode, ViewModelInstanceName));

            if (result.Success)
            {
                SetUpAudioIfNeeded(Controller.Artboard, Controller.ArtboardHasAudio);
                HandleLoadComplete();
            }
            else
            {
                HandleLoadError(result.ErrorData);
            }
        }

        // Set while an async load finishes, so the first-frame advance doesn't wait.
        private bool m_finishingAsyncLoad;
        // The async load in flight, for WaitForCompletion.
        private Future<ArtboardLoadHelper.LoadResult> m_pendingLoad;

        // The artboard, state machine and binding are built on the producer,
        // and only attached here when they land, so nothing waits.
        private void LoadInternalAsync(File file, Asset fromAsset, FutureState<bool> state)
        {
            if (file == null)
            {
                DebugLogger.Instance.LogError("Attempted to load a null Rive file.");
                Fail(new RiveException(RiveErrorCode.LoadFailed, "Attempted to load a null Rive file."));
                state.Succeed(true);
                return;
            }

            ReleaseHandleViews();
            m_family = ThreadingMode.BackgroundThread;
            Status = WidgetStatus.Loading;
            m_fileLoadedFromAsset = fromAsset;
            Future<ArtboardLoadHelper.LoadResult> load = Controller.LoadAsync(file, m_fit, m_alignment, m_artboardName, m_stateMachineName, GetEffectiveScaleFactor(), new ArtboardLoadHelper.DataBindingLoadInfo(BindingMode, ViewModelInstanceName), describeFile: m_givenFileHandle == null);
            m_pendingLoad = load;
            load.Completed += landed => FinishLoadInternalAsync(landed, state);
        }

        private void FinishLoadInternalAsync(Future<ArtboardLoadHelper.LoadResult> landed, FutureState<bool> state)
        {
            if (landed.Status == FutureStatus.Canceled || this == null)
            {
                // A newer load or the widget going away got there first.
                state.Cancel();
                return;
            }
            if (landed.Status == FutureStatus.Failed)
            {
                Fail(AsRiveError(landed.Exception));
                state.Fail(landed.Exception);
                return;
            }

            ArtboardLoadHelper.LoadResult result = landed.Result;
            if (result.Success)
            {
                CreateHandleViews();
                ApplyDisplaySettingsSetDuringLoad();
                SetUpAudioIfNeeded(Controller.Artboard, Controller.ArtboardHasAudio);
                m_finishingAsyncLoad = true;
                try
                {
                    HandleLoadComplete();
                }
                finally
                {
                    m_finishingAsyncLoad = false;
                }
            }
            else
            {
                HandleLoadError(result.ErrorData);
            }
            state.Succeed(true);

            // After the code awaiting the load has run, so what it sets is in
            // the first frame too.
            if (result.Success && this != null)
            {
                SendLayoutRecalculationFix();
            }
        }

        /// <summary>
        /// Loads a Rive file and specified artboard and state machine.
        /// </summary>
        /// <param name="file"> The Rive file to load: a <see cref="Rive.File"/> or a <see cref="Rive.FileHandle"/>. Either works in either kind of panel.</param>
        /// <param name="artboardName"> The name of the artboard to load.</param>
        /// <param name="stateMachineName"> The name of the state machine to load.</param>
        /// <remarks>In a <see cref="ThreadingMode.BackgroundThread"/> panel this returns before the load finishes. <see cref="WidgetBehaviour.OnWidgetStatusChanged"/> reports when it has.</remarks>
        public void Load(ILoadedFile file, string artboardName, string stateMachineName)
        {
            m_artboardName = artboardName;
            m_stateMachineName = stateMachineName;
            _ = LoadGivenFile(file);
        }

        /// <summary>
        /// Loads a Rive file using the default artboard and state machine.
        /// </summary>
        /// <param name="file"> The Rive file to load: a <see cref="Rive.File"/> or a <see cref="Rive.FileHandle"/>. Either works in either kind of panel.</param>
        /// <remarks>In a <see cref="ThreadingMode.BackgroundThread"/> panel this returns before the load finishes. <see cref="WidgetBehaviour.OnWidgetStatusChanged"/> reports when it has.</remarks>
        public void Load(ILoadedFile file)
        {
            ResetToDefaultArtboardAndStateMachineName();
            _ = LoadGivenFile(file);
        }

        /// <summary>
        /// Loads a Rive file and specified artboard and state machine, returning something to await.
        /// </summary>
        /// <param name="file"> The Rive file to load: a <see cref="Rive.File"/> or a <see cref="Rive.FileHandle"/>.</param>
        /// <param name="artboardName"> The name of the artboard to load.</param>
        /// <param name="stateMachineName"> The name of the state machine to load.</param>
        /// <returns>An operation that finishes on the main thread once the widget has reached <see cref="WidgetStatus.Loaded"/> or <see cref="WidgetStatus.Error"/>. Already finished in a <see cref="ThreadingMode.MainThread"/> panel.</returns>
        public Future LoadAsync(ILoadedFile file, string artboardName, string stateMachineName)
        {
            m_artboardName = artboardName;
            m_stateMachineName = stateMachineName;
            return LoadGivenFile(file);
        }

        /// <summary>
        /// Loads a Rive file using the default artboard and state machine, returning something to await.
        /// </summary>
        /// <param name="file"> The Rive file to load: a <see cref="Rive.File"/> or a <see cref="Rive.FileHandle"/>.</param>
        /// <returns>An operation that finishes on the main thread once the widget has reached <see cref="WidgetStatus.Loaded"/> or <see cref="WidgetStatus.Error"/>. Already finished in a <see cref="ThreadingMode.MainThread"/> panel.</returns>
        public Future LoadAsync(ILoadedFile file)
        {
            ResetToDefaultArtboardAndStateMachineName();
            return LoadGivenFile(file);
        }

        // A caller's File or FileHandle. The widget doesn't own either.
        private Future LoadGivenFile(ILoadedFile given)
        {
            m_loadVersion++;
            ReleaseFileIfResponsibleForLoading();
            FileHandle handle = given as FileHandle;
            File file = handle != null ? handle.File : given as File;
            if (given != null && given.IsDisposed)
            {
                file = null;
            }
            ReleaseHandleViews();
            m_givenFileHandle = handle;

            if (PanelThreadingMode == ThreadingMode.BackgroundThread)
            {
                var state = new FutureState<bool>();
                state.WaitDriver = WaitForPendingLoad;
                LoadInternalAsync(file, null, state);
                return new Future(state);
            }
            LoadInternal(file, null);
            return CompletedLoad();
        }

        /// <summary>
        /// Loads from a Rive asset and specified artboard and state machine.
        /// </summary>
        /// <param name="asset"> The Rive asset to load.</param>
        /// <param name="artboardName"> The name of the artboard to load.</param>
        /// <param name="stateMachineName"> The name of the state machine to load.</param>
        public void Load(Asset asset, string artboardName, string stateMachineName)
        {

            m_asset = asset;
            m_artboardName = artboardName;
            m_stateMachineName = stateMachineName;

            _ = LoadFromAssetForPanel();
        }

        /// <summary>
        /// Loads from a Rive asset using the default artboard and state machine.
        /// </summary>
        /// <param name="asset"> The Rive asset to load.</param>
        public void Load(Asset asset)
        {
            m_asset = asset;
            ResetToDefaultArtboardAndStateMachineName();
            _ = LoadFromAssetForPanel();
        }

        /// <summary>
        /// Loads from a Rive asset using the default artboard and state machine, without blocking the caller.
        /// </summary>
        /// <param name="asset"> The Rive asset to load.</param>
        /// <returns>An operation that finishes on the main thread once the widget has reached <see cref="WidgetStatus.Loaded"/> or <see cref="WidgetStatus.Error"/>.</returns>
        public Future LoadAsync(Asset asset)
        {
            m_asset = asset;
            ResetToDefaultArtboardAndStateMachineName();
            return LoadFromAssetForPanel();
        }

        /// <summary>
        /// Loads from a Rive asset and specified artboard and state machine, without blocking the caller.
        /// </summary>
        /// <param name="asset"> The Rive asset to load.</param>
        /// <param name="artboardName"> The name of the artboard to load.</param>
        /// <param name="stateMachineName"> The name of the state machine to load.</param>
        /// <returns>An operation that finishes on the main thread once the widget has reached <see cref="WidgetStatus.Loaded"/> or <see cref="WidgetStatus.Error"/>.</returns>
        public Future LoadAsync(Asset asset, string artboardName, string stateMachineName)
        {
            m_asset = asset;
            m_artboardName = artboardName;
            m_stateMachineName = stateMachineName;
            return LoadFromAssetForPanel();
        }

        private Future LoadFromAssetForPanel()
        {
            ReleaseHandleViews();
            m_givenFileHandle = null;
            m_family = PanelThreadingMode;
            if (m_family == ThreadingMode.BackgroundThread)
            {
                return LoadFromAssetAsyncIfNeeded();
            }
            LoadFromAssetIfNeeded();
            return CompletedLoad();
        }

        private Future LoadFromAssetAsyncIfNeeded()
        {
            var state = new FutureState<bool>();
            var operation = new Future(state);

            if (m_asset == null)
            {
                Fail(new RiveException(RiveErrorCode.LoadFailed, "There's no Rive asset to load."));
                state.Succeed(true);
                return operation;
            }

            // Already loaded from this asset, so reuse it rather than move the ref count.
            if (m_fileLoadedFromAsset != null && ReferenceEquals(m_asset, m_fileLoadedFromAsset))
            {
                m_loadVersion++;
                state.WaitDriver = WaitForPendingLoad;
                LoadInternalAsync(LoadedFile, m_fileLoadedFromAsset, state);
                return operation;
            }

            ReleaseFileIfResponsibleForLoading();

            Status = WidgetStatus.Loading;
            Asset asset = m_asset;
            int version = ++m_loadVersion;
            Future<File> load = Rive.File.Loader.LoadAsync(asset, null);
            state.WaitDriver = () =>
            {
                if (!load.IsDone)
                {
                    load.WaitInternal();
                }
                // The import landing starts the rest on the producer.
                WaitForPendingLoad();
            };
            load.Completed += landed => FinishLoadFromAsset(landed, asset, version, state);
            return operation;
        }

        // Counts loads, so an asset import that lands after a newer load, of
        // a file or another asset, knows it lost.
        private int m_loadVersion;

        private void WaitForPendingLoad()
        {
            if (!m_pendingLoad.IsDone)
            {
                m_pendingLoad.WaitInternal();
            }
        }

        // Main thread, once the import has finished.
        private void FinishLoadFromAsset(Future<File> landed, Asset asset, int version, FutureState<bool> state)
        {
            if (landed.Status == FutureStatus.Canceled)
            {
                state.Cancel();
                return;
            }
            // A later load won the race, so this one was cancelled, whether
            // its import worked or not. Its widget status isn't this load's to
            // change any more.
            if (this != null && (version != m_loadVersion || !ReferenceEquals(asset, m_asset)))
            {
                if (landed.Status == FutureStatus.Succeeded && landed.Result != null)
                {
                    // A later load of the same asset shares this import and only
                    // takes its reference once every callback here has run, so
                    // let go after that or the file goes with this one.
                    Rive.Host.CommandTransport.PostToMainThread(landed.Result.Dispose);
                }
                state.Cancel();
                return;
            }
            if (landed.Status == FutureStatus.Failed)
            {
                if (this != null)
                {
                    Fail(AsRiveError(landed.Exception));
                }
                state.Fail(landed.Exception);
                return;
            }

            try
            {
                File loadedFile = landed.Result;
                if (this == null)
                {
                    // Destroyed while the import was running, so nothing owns it.
                    loadedFile?.Dispose();
                }
                else if (loadedFile == null)
                {
                    Fail(new RiveException(RiveErrorCode.LoadFailed, "The Rive file couldn't be loaded."));
                }
                else
                {
                    // Finishes the state when the rest lands.
                    LoadInternalAsync(loadedFile, asset, state);
                    return;
                }
            }
            catch (System.Exception e)
            {
                state.Fail(e);
                return;
            }
            state.Succeed(true);
        }

        private void ResetToDefaultArtboardAndStateMachineName()
        {
            m_artboardName = null;
            m_stateMachineName = null;
        }


        private void LoadFromAssetIfNeeded()
        {
            if (m_asset == null)
            {
                Fail(new RiveException(RiveErrorCode.LoadFailed, "There's no Rive asset to load."));
                return;
            }

            // If we already have a loaded file from this asset then we can use it directly to avoid updating the asset ref count
            if (m_fileLoadedFromAsset != null && ReferenceEquals(m_asset, m_fileLoadedFromAsset))
            {
                LoadInternal(LoadedFile, m_fileLoadedFromAsset);

                return;
            }

            ReleaseFileIfResponsibleForLoading();


            Status = WidgetStatus.Loading;
            var loadedFile = File.Load(Asset);

            if (loadedFile == null)
            {
                Fail(new RiveException(RiveErrorCode.LoadFailed, "The Rive file couldn't be loaded."));
                return;
            }

            LoadInternal(loadedFile, m_asset);
        }



        // The render object is made with the fit, alignment and scale from when
        // an async load started. Any set while it was out were only stored.
        private void ApplyDisplaySettingsSetDuringLoad()
        {
            ArtboardRenderObject renderObject = RenderObjectWithArtboard;
            if (renderObject == null)
            {
                return;
            }
            if (renderObject.Alignment != m_alignment)
            {
                OnAlignmentChanged();
            }
            if (renderObject.Fit != m_fit)
            {
                OnFitChanged();
            }
            else
            {
                ResizeArtboardForLayoutIfNeeded();
            }
        }

        private void OnScaleFactorChanged()
        {
            if (RenderObjectWithArtboard != null && Fit == Fit.Layout)
            {
                ResizeArtboardForLayoutIfNeeded();
                TriggerRedrawNeededEvent();
            }
        }

        private void OnAlignmentChanged()
        {
            if (RenderObjectWithArtboard != null)
            {
                RenderObjectWithArtboard.Alignment = Alignment;
                TriggerRedrawNeededEvent();
            }
        }

        private void OnFitChanged()
        {
            if (RenderObjectWithArtboard != null)
            {

                RenderObjectWithArtboard.Fit = Fit;

                if (LoadedArtboard != null)
                {
                    // Check if the original artboard size is different from the current artboard size
                    // When outside of layout mode, we should reset the artboard size to the original size if it has been changed.

                    Vector2 currentSize = CurrentArtboardSize;
                    bool artboardSizeIsDifferentFromOriginal = (currentSize.x != Controller.OriginalArtboardWidth || currentSize.y != Controller.OriginalArtboardHeight);

                    bool shouldResetArtboardSize = Fit != Fit.Layout && artboardSizeIsDifferentFromOriginal;

                    if (shouldResetArtboardSize)
                    {
                        LoadedArtboard.ResetArtboardSize();
                        SetArtboardSizeLocally(new Vector2(Controller.OriginalArtboardWidth, Controller.OriginalArtboardHeight));
                    }
                }



                ResizeArtboardForLayoutIfNeeded();
                TriggerRedrawNeededEvent();
            }
        }

        private void OnScaleModeChanged()
        {
            if (LoadedArtboard != null && Fit == Fit.Layout)
            {
                ResizeArtboardForLayoutIfNeeded();
                TriggerRedrawNeededEvent();
            }
        }

        private void OnFallbackDPIChanged()
        {
            if (RenderObjectWithArtboard != null && Fit == Fit.Layout && m_layoutScalingMode == LayoutScalingMode.ConstantPhysicalSize)
            {
                ResizeArtboardForLayoutIfNeeded();
                TriggerRedrawNeededEvent();
            }
        }

        private void OnReferenceDPIChanged()
        {
            if (RenderObjectWithArtboard != null && Fit == Fit.Layout && m_layoutScalingMode == LayoutScalingMode.ConstantPhysicalSize)
            {
                ResizeArtboardForLayoutIfNeeded();
                TriggerRedrawNeededEvent();
            }
        }

        private float GetEffectiveScaleFactor()
        {
            if (Controller == null)
            {
                return 1.0f;
            }

            Vector2 ogArtboardSize = new Vector2(Controller.OriginalArtboardWidth, Controller.OriginalArtboardHeight);
            var screenDPI = UseFallbackDPI ? m_fallbackDPI : Screen.dpi;
            return ArtboardLoadHelper.CalculateEffectiveScaleFactor(m_layoutScalingMode, m_layoutScaleFactor, ogArtboardSize, RectTransform.rect, m_referenceDPI, fallbackDPI: m_fallbackDPI, screenDPI: screenDPI);

        }

        private void ResizeArtboardForLayoutIfNeeded()
        {
            if (LoadedArtboard != null && Fit == Fit.Layout && RenderObjectWithArtboard != null)
            {

                float effectiveScale = GetEffectiveScaleFactor();
                var rect = RectTransform.rect;

                if (ArtboardLoadHelper.CalculateArtboardDimensionsForLayout(rect, effectiveScale, out float newWidth, out float newHeight))
                {
                    RenderObjectWithArtboard.EffectiveLayoutScaleFactor = effectiveScale;

                    LoadedArtboard.Size = new Size(newWidth, newHeight);
                    SetArtboardSizeLocally(new Vector2(newWidth, newHeight));


                }


            }
        }

        // hasAudio when the load already knows, so nothing asks the runtime.
        private void SetUpAudioIfNeeded(Artboard artboard, bool? hasAudio = null)
        {
            if (m_isDestroyed)
            {
                return;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
// WebGL doesn't support OnAudioFilterRead, so don't use the audio engine in this case. On WebGL, rive will use system audio instead, which bypasses Unity's audio system.
return;
#endif


            if (artboard == null || !(hasAudio ?? artboard.HasAudio))
            {
                return;
            }

            var provider = m_customAudioProvider ?? GlobalAudioProvider;

            if (provider == null)
            {
                return;
            }

            artboard.SetAudioEngine(provider.AudioEngine);
        }


        protected override void HandleLoadComplete()
        {
            ResizeArtboardForLayoutIfNeeded();
            TriggerWidgetLoadedEvent();

            m_needsLayoutRecalculationFix = true;

            ApplyLayoutRecalculationFixIfNeeded();
        }


        private void TriggerWidgetLoadedEvent()
        {
            base.HandleLoadComplete();
        }

        private void ApplyLayoutRecalculationFixIfNeeded()
        {
            // This is a workaround for a bug where the layout is not recalculated correctly when the widget is first loaded. This seems to only happen with some files, like duelist.riv where we see the initial layout shift if we don't do this.
            // TODO: check if we need to do something in the C++ layer to fix this instead of doing it here.
            // An async finish sends it once the load's Future is done.
            if (m_finishingAsyncLoad)
            {
                return;
            }
            if (m_needsLayoutRecalculationFix && LoadedStateMachine != null)
            {
                m_needsLayoutRecalculationFix = false;
                // On the initial frame, force the state machine to update the layout. We do this after base.HandleLoadComplete(); because that's where the OnWidgetStatusChanged event is triggered, and we want values that were set there to be applied before we advance the state machine.
                // If we do this before base.HandleLoadComplete(); the values set in the OnWidgetStatusChanged event will not be applied on the first frame.
                Core?.AdvanceAndWait(0f, 1f, OnRiveEventReported != null);
            }
        }

        // Doesn't wait. Goes out behind every write made so far. If a panel
        // advance already took the flag, it did the same thing.
        private void SendLayoutRecalculationFix()
        {
            if (m_needsLayoutRecalculationFix && LoadedStateMachine != null)
            {
                m_needsLayoutRecalculationFix = false;
                Core?.AdvanceLater(0f, 1f, OnRiveEventReported != null);
            }
        }

        protected override void OnRectTransformDimensionsChange()
        {
            // Do this before the base call so that the base call can recalculate the widget layout before we tell the panel to redraw
            ResizeArtboardForLayoutIfNeeded();

            base.OnRectTransformDimensionsChange();


        }

        private void ReleaseFileIfResponsibleForLoading()
        {
            if (LoadedFile != null && m_fileLoadedFromAsset)
            {
                LoadedFile.Dispose();
                m_fileLoadedFromAsset = null;
            }
        }



        // Moved to a panel in the other mode, so reload in that panel's family.
        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();
            if (Status != WidgetStatus.Loaded || m_family == PanelThreadingMode)
            {
                return;
            }
            DebugLogger.Instance.Log($"{name} moved to a {PanelThreadingMode} panel, so it reloads to match.");
            if (m_asset != null && m_fileLoadedFromAsset != null)
            {
                _ = LoadFromAssetForPanel();
                return;
            }
            ILoadedFile given = (ILoadedFile)m_givenFileHandle ?? LoadedFile;
            if (given != null)
            {
                _ = LoadGivenFile(given);
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            ReleaseFileIfResponsibleForLoading();
            ReleaseHandleViews();

            if (m_controller != null)
            {
                UnsubscribeFromControllerEvents(m_controller);
                m_controller.Dispose();
            }
            m_isDestroyed = true;
        }




#if UNITY_EDITOR
        // Inspector-specific methods (used for custom inspector logic)

        /// <summary>
        /// Sets the asset reference in the editor.
        /// </summary>
        /// <param name="asset"> The Rive asset to set.</param>
        internal void SetEditorAssetReference(Asset asset)
        {
            if (Application.isPlaying)
            {
                return;
            }
            m_asset = asset;

            OnAssetChangedInEditor();
        }

        private bool ShouldHideArtboardNameAndStateMachineName()
        {
            return Asset == null;
        }

        private string[] GetDisplayArtboardNames()
        {
            if (Asset == null) return new string[0];
            return Asset.EditorOnlyMetadata.GetArtboardNames();
        }

        private string[] GetDisplayStateMachineNames()
        {
            if (Asset == null || string.IsNullOrEmpty(ArtboardName)) return new string[0];
            return Asset.EditorOnlyMetadata.GetStateMachineNames(ArtboardName);
        }

        private void OnAssetChangedInEditor()
        {
            // If in play mode, make sure we haven't already loaded the asset or this might cause a double load
            if (Application.isPlaying && Status == WidgetStatus.Loaded)
            {
                return;
            }

            var names = GetDisplayArtboardNames();
            // If the artboard name is not in the list of artboards, set it to the first artboard in the list.
            // We only want to do this if the user manually changes the asset, not if the asset is changed by the system.
            if (names.Length > 0 && !names.Contains(ArtboardName))
            {
                m_artboardName = names[0];
            }
            OnArtboardChangedInEditor();


        }

        private void OnArtboardChangedInEditor()
        {

            var names = GetDisplayStateMachineNames();
            // If the state machine name is not in the list of state machines for the current artboard, set it to the first state machine in the list.
            if (names.Length > 0 && !names.Contains(StateMachineName))
            {
                m_stateMachineName = names[0];
            }

            OnStateMachineChangedInEditor();
        }

        private void OnStateMachineChangedInEditor()
        {
            // In play mode, reload the asset in the panel's family. The sync
            // path would switch a Background widget to plain objects. Not while
            // a load is out: the inspector's bindings report a change when it's
            // rebuilt on entering play mode, and a second load would race it.
            if (Application.isPlaying && Status != WidgetStatus.Loaded && Status != WidgetStatus.Loading && m_asset != null)
            {
                _ = LoadFromAssetForPanel();
                return;
            }
        }


        private void OnLayoutScalingModeChangedInEditor()
        {
            if (!Application.isPlaying)
            {
                return;
            }
            OnScaleModeChanged();

        }

        private void OnScaleFactorChangedInEditor()
        {
            if (!Application.isPlaying)
            {
                return;
            }
            OnScaleFactorChanged();
        }

        private void OnFitChangedInEditor()
        {
            if (!Application.isPlaying)
            {
                return;
            }
            OnFitChanged();
        }

        private void OnAlignmentChangedInEditor()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            OnAlignmentChanged();
        }

        private void OnDataBindingModeChangedInEditor()
        {
            // If we're not in play mode, then set the view model instance name to default
            if (Application.isPlaying)
            {
                return;
            }

            // Set the view model instance name to default if we're not in auto bind mode
            if (m_dataBindingMode == DataBindingMode.AutoBindSelected)
            {
                m_viewModelInstanceName = GetInitialViewModelNameForArtboard();
            }
        }


        private bool ShouldShowDpiFields()
        {
            if (!ShouldShowLayoutOptions())
            {
                return false;
            }
            return m_layoutScalingMode == LayoutScalingMode.ConstantPhysicalSize;
        }

        private bool ShouldShowLayoutOptions()
        {
            return Fit == Fit.Layout;
        }

        private bool ShouldHideAlignment()
        {
            return Fit == Fit.Layout;
        }

        private bool ShouldShowDataBindingInstanceField()
        {
            return m_dataBindingMode == DataBindingMode.AutoBindSelected;
        }

        private List<string> GetViewModelInstanceNames()
        {
            if (Asset == null || string.IsNullOrEmpty(ArtboardName))
            {
                return new List<string>();
            }

            var metadata = Asset.EditorOnlyMetadata.GetArtboard(m_artboardName);
            if (metadata == null)
            {
                return new List<string>();
            }

            return metadata.DefaultViewModel.InstanceNames;
        }

        private string GetInitialViewModelNameForArtboard()
        {
            if (Asset == null || string.IsNullOrEmpty(ArtboardName))
            {
                return string.Empty;
            }

            var metadata = Asset.EditorOnlyMetadata.GetArtboard(m_artboardName);
            if (metadata == null)
            {
                return string.Empty;
            }

            if (metadata.DefaultViewModel.InstanceNames.Count == 0)
            {
                return string.Empty;
            }

            return metadata.DefaultViewModel.InstanceNames[0];
        }


#endif

    }
}
