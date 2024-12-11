using System;
#if UNITY_EDITOR
using System.Linq;
#endif
using Rive.Components.Utilities;
using Rive.EditorTools;
using Rive.Utils;
using UnityEngine;
using UnityEngine.Events;

namespace Rive.Components
{
    /// <summary>
    /// This component is used to display a Rive file within a Rive Panel.
    /// </summary>
    [InspectorSection(WidgetInspectorSections.FileSettings, "File Settings")]
    [InspectorSection(WidgetInspectorSections.Display, "Display")]
    [InspectorSection(WidgetInspectorSections.Input, "Input")]
    [AddComponentMenu("Rive/Rive Widget")]
    public sealed class RiveWidget : WidgetBehaviour
    {
        private static class WidgetInspectorSections
        {

            public const string FileSettings = "file-settings";

            public const string Display = "display";
            public const string Input = "input";

        }

#if UNITY_EDITOR
        [OnValueChanged(nameof(OnAssetChangedInEditor))]
        [InspectorField(WidgetInspectorSections.FileSettings)]
#endif
        [Tooltip("The Rive file (.riv) to load.")]
        [SerializeField] private Asset m_asset;



#if UNITY_EDITOR
        [HideIf(nameof(ShouldHideArtboardNameAndStateMachineName))]
        [InspectorField(WidgetInspectorSections.FileSettings)]
        [Tooltip("The name of the artboard to load.")]
        [OnValueChanged(nameof(OnArtboardChangedInEditor))]
        [Dropdown(nameof(GetDisplayArtboardNames), trackChanges: true)]
#endif
        [SerializeField] private string m_artboardName;

#if UNITY_EDITOR
        [HideIf(nameof(ShouldHideArtboardNameAndStateMachineName))]
        [InspectorField(WidgetInspectorSections.FileSettings)]
        [OnValueChanged(nameof(OnStateMachineChangedInEditor))]
        [Dropdown(nameof(GetDisplayStateMachineNames), trackChanges: true)]
#endif
        [Tooltip("The name of the state machine to load.")]
        [SerializeField] private string m_stateMachineName;



#if UNITY_EDITOR
        [InspectorField(WidgetInspectorSections.Display)]
        [OnValueChanged(nameof(OnFitChangedInEditor))]
#endif
        [Tooltip("The fit mode to use when drawing the artboard.")]
        [SerializeField] private Fit m_fit = Fit.Contain;

#if UNITY_EDITOR
        [InspectorField(WidgetInspectorSections.Display)]
        [HideIf(nameof(ShouldHideAlignment))]
        [OnValueChanged(nameof(OnAlignmentChangedInEditor))]
        [Tooltip("The alignment to use when drawing the artboard.")]
#endif
        [SerializeField]
        private Alignment m_alignment = Alignment.Center;

#if UNITY_EDITOR
        [ShowIf(nameof(ShouldShowLayoutOptions))]
        [InspectorField(WidgetInspectorSections.Display)]
        [OnValueChanged(nameof(OnScaleFactorChangedInEditor))]
#endif
        [Tooltip("The scale factor to use when drawing the artboard when using the Layout fit mode. Increase this value to make the artboard appear larger.")]
        [SerializeField] private float m_layoutScaleFactor = 1.0f;

#if UNITY_EDITOR
        [ShowIf(nameof(ShouldShowLayoutOptions))]
        [InspectorField(WidgetInspectorSections.Display)]
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



        [Tooltip("Controls how hit testing is handled:\n" +
                 "\n" +
                 "- Opaque blocks all hits.\n" +
                 "\n" +
                 "- Translucent only blocks hits on listeners.\n" +
                 "\n" +
                 "- Transparent allows hits to pass through while still detecting listeners.\n" +
                 "\n" +
                 "- None disables hit testing completely.")]
        [InspectorField(WidgetInspectorSections.Input)]
        [SerializeField] private HitTestBehavior m_hitTestBehavior = HitTestBehavior.Opaque;


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
        /// The Rive file that is currently loaded.
        /// </summary>
        public File File { get => Controller?.File; }


        /// <summary>
        /// The artboard that is currently loaded.
        /// </summary>
        public Artboard Artboard { get => Controller?.Artboard; }

        /// <summary>
        /// The state machine that is currently loaded.
        /// </summary>
        public StateMachine StateMachine { get => Controller?.StateMachine; }


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


        private ArtboardRenderObject RenderObjectWithArtboard => Controller?.RenderObject;

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
        /// Event that is triggered when a Rive event is reported.
        /// </summary>
        [InspectorField(InspectorSections.Events)]
        public UnityEvent<ReportedEvent> OnRiveEventReported = new UnityEvent<ReportedEvent>();



        private Asset m_fileLoadedFromAsset = null;

        private bool m_isDestroyed = false;

        protected override void OnEnable()
        {
            base.OnEnable();

            if (m_asset != null && Status == WidgetStatus.Uninitialized)
            {
                LoadFromAssetIfNeeded();
            }
        }


        public override bool Tick(float deltaTime)
        {
            bool needsRedraw = base.Tick(deltaTime);

            if (Controller == null || Status != WidgetStatus.Loaded)
            {
                return needsRedraw;
            }
            Controller.Tick(deltaTime);

            return needsRedraw;

        }

        private void SubscribeToControllerEvents(ArtboardLoadHelper controller)
        {
            if (controller == null)
            {
                return;
            }

            controller.OnLoadProcessComplete += HandleLoadComplete;
            controller.OnLoadError += HandleLoadError;
            controller.OnRiveEventReported += HandleRiveEventReported;
        }
        private void UnsubscribeFromControllerEvents(ArtboardLoadHelper controller)
        {
            if (controller == null)
            {
                return;
            }
            controller.OnLoadProcessComplete -= HandleLoadComplete;
            controller.OnLoadError -= HandleLoadError;
            controller.OnRiveEventReported -= HandleRiveEventReported;

        }

        private void HandleRiveEventReported(ReportedEvent report)
        {
            OnRiveEventReported?.Invoke(report);
        }

        private void HandleLoadError(ArtboardLoadHelper.LoadErrorEventData eventData)
        {
            Status = WidgetStatus.Error;
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

        /// <summary>
        /// Tries to get the Rive point from the local normalized point in the frame.
        /// </summary>
        /// <param name="localNormalizedPointInFrame"> The local normalized point in the frame.</param>
        /// <param name="rivePoint"> The point in Rive coordinates.</param>
        /// <returns> True if the Rive point was successfully retrieved, false otherwise.</returns>
        private bool TryGetRivePoint(Vector2 localNormalizedPointInFrame, out Vector2 rivePoint)
        {
            rivePoint = Vector2.zero;


            // Flip Y coordinate if needed
            localNormalizedPointInFrame = FlipNormalizedPointIfNeeded(localNormalizedPointInFrame);

            var rect = RectTransform.rect;

            // When in Layout fit mode, we need to account for the effective scale
            if (Fit == Fit.Layout)
            {
                float effectiveScale = GetEffectiveScaleFactor();
                // Scale the rect dimensions by the effective scale factor
                rect = new Rect(
                    0,
                    0,
                    rect.width / effectiveScale,
                    rect.height / effectiveScale
                );
            }
            else
            {
                // For other fit modes, use the regular rect with 0,0 origin
                rect = new Rect(0, 0, rect.width, rect.height);
            }


            Fit fit = RenderObjectWithArtboard.Fit;
            Alignment alignment = RenderObjectWithArtboard.Alignment;
            Artboard artboard = RenderObjectWithArtboard.Artboard;

            Vector2 riveScreenPosition = new Vector2(localNormalizedPointInFrame.x * rect.width, localNormalizedPointInFrame.y * rect.height);

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
            Vector2 rivePoint;

            if (!TryGetRivePoint(normalizedPointInRect, out rivePoint))
            {
                return false;
            }

            return StateMachine.HitTest(rivePoint);
        }




        public override bool OnPointerDown(Vector2 normalizedPointInRect)
        {
            if (StateMachine == null)
            {
                return false;
            }
            Vector2 rivePoint;
            if (!TryGetRivePoint(normalizedPointInRect, out rivePoint))
            {
                return false;
            }

            HitResult hitResult = StateMachine.PointerDown(rivePoint);

            return hitResult != HitResult.None;
        }

        /// <summary>
        /// Called when a pointer is released on the widget.
        /// </summary>
        /// <param name="normalizedPointInRect">The normalized point of the pointer release in the widget's rectangle. The coordinates are in the range [0,1] where (0,0) is the bottom-left corner and (1,1) is the top-right corner.</param>
        public override bool OnPointerUp(Vector2 normalizedPointInRect)
        {
            if (StateMachine == null)
            {
                return false;
            }
            Vector2 rivePoint;
            if (!TryGetRivePoint(normalizedPointInRect, out rivePoint))
            {
                return false;
            }

            HitResult hitResult = StateMachine.PointerUp(rivePoint);

            return hitResult != HitResult.None;

        }

        /// <summary>
        /// Called when a pointer is moved on the widget.
        /// </summary>
        /// <param name="normalizedPointInRect">The normalized point of the pointer position in the widget's rectangle. The coordinates are in the range [0,1] where (0,0) is the bottom-left corner and (1,1) is the top-right corner.</param>
        public override bool OnPointerMove(Vector2 normalizedPointInRect)
        {
            if (StateMachine == null)
            {
                return false;
            }

            Vector2 rivePoint;
            if (!TryGetRivePoint(normalizedPointInRect, out rivePoint))
            {
                return false;
            }

            HitResult hitResult = StateMachine.PointerMove(rivePoint);

            return hitResult != HitResult.None;

        }





        /// <summary>
        /// Internal method to handle loading from either an asset or direct file
        /// </summary>
        private void LoadInternal(File file, Asset fromAsset = null)
        {

            if (file == null)
            {
                Status = WidgetStatus.Error;

                DebugLogger.Instance.LogError("Attempted to load a null Rive file.");
                return;
            }

            Status = WidgetStatus.Loading;
            m_fileLoadedFromAsset = fromAsset;
            Controller.Load(file, m_fit, m_alignment, m_artboardName, m_stateMachineName, GetEffectiveScaleFactor());
        }

        /// <summary>
        /// Loads a Rive file and specified artboard and state machine.
        /// </summary>
        /// <param name="file"> The Rive file to load.</param>
        /// <param name="fit"> The fit mode to use.</param>
        /// <param name="alignment"> The alignment to use.</param>
        /// <param name="artboardName"> The name of the artboard to load.</param>
        /// <param name="stateMachineName"> The name of the state machine to load.</param>
        public void Load(File file, string artboardName, string stateMachineName)
        {
            m_artboardName = artboardName;
            m_stateMachineName = stateMachineName;

            ReleaseFileIfResponsibleForLoading();

            LoadInternal(file, null);
        }


        /// <summary>
        /// Loads a Rive file using the default artboard and state machine.
        /// </summary>
        /// <param name="file"> The Rive file to load.</param>
        public void Load(File file)
        {
            ReleaseFileIfResponsibleForLoading();

            LoadInternal(file, null);
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

            LoadFromAssetIfNeeded();
        }

        /// <summary>
        /// Loads from a Rive asset using the default artboard and state machine.
        /// </summary>
        /// <param name="asset"> The Rive asset to load.</param>
        public void Load(Asset asset)
        {
            m_asset = asset;
            LoadFromAssetIfNeeded();
        }


        private void LoadFromAssetIfNeeded()
        {
            if (m_asset == null)
            {
                Status = WidgetStatus.Error;
                return;
            }

            // If we already have a loaded file from this asset then we can use it directly to avoid updating the asset ref count
            if (m_fileLoadedFromAsset != null && ReferenceEquals(m_asset, m_fileLoadedFromAsset))
            {
                LoadInternal(File, m_fileLoadedFromAsset);

                return;
            }

            ReleaseFileIfResponsibleForLoading();


            Status = WidgetStatus.Loading;
            var loadedFile = File.Load(Asset);

            if (loadedFile == null)
            {
                Status = WidgetStatus.Error;
                return;
            }

            LoadInternal(loadedFile, m_asset);
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

                if (Artboard != null)
                {
                    // Check if the original artboard size is different from the current artboard size
                    // When outside of layout mode, we should reset the artboard size to the original size if it has been changed.

                    bool artboardSizeIsDifferentFromOriginal = (Artboard.Width != Controller.OriginalArtboardWidth || Artboard.Height != Controller.OriginalArtboardHeight);

                    bool shouldResetArtboardSize = Fit != Fit.Layout && artboardSizeIsDifferentFromOriginal;

                    if (shouldResetArtboardSize)
                    {
                        Artboard.ResetArtboardSize();
                    }
                }



                ResizeArtboardForLayoutIfNeeded();
                TriggerRedrawNeededEvent();
            }
        }

        private void OnScaleModeChanged()
        {
            if (Artboard != null && Fit == Fit.Layout)
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
            if (Artboard != null && Fit == Fit.Layout && RenderObjectWithArtboard != null)
            {

                float effectiveScale = GetEffectiveScaleFactor();
                var rect = RectTransform.rect;

                if (ArtboardLoadHelper.CalculateArtboardDimensionsForLayout(rect, effectiveScale, out float newWidth, out float newHeight))
                {
                    RenderObjectWithArtboard.EffectiveLayoutScaleFactor = effectiveScale;

                    Artboard.Width = newWidth;

                    Artboard.Height = newHeight;


                    if (StateMachine != null)
                    {
                        // Force the state machine to update the layout
                        StateMachine.Advance(0f);

                    }

                }


            }
        }

        protected override void HandleLoadComplete()
        {
            ResizeArtboardForLayoutIfNeeded();
            base.HandleLoadComplete();
        }


        protected override void OnRectTransformDimensionsChange()
        {
            // Do this before the base call so that the base call can recalculate the widget layout before we tell the panel to redraw
            ResizeArtboardForLayoutIfNeeded();

            base.OnRectTransformDimensionsChange();


        }

        private void ReleaseFileIfResponsibleForLoading()
        {
            if (File != null && m_fileLoadedFromAsset)
            {
                File.Dispose();
                m_fileLoadedFromAsset = null;
            }
        }



        protected override void OnDestroy()
        {
            base.OnDestroy();

            ReleaseFileIfResponsibleForLoading();

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
            // If we're in play mode, reload the asset
            if (Application.isPlaying)
            {
                LoadFromAssetIfNeeded();
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


#endif

    }
}