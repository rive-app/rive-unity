using Rive.EditorTools;
using Rive.Utils;
using Rive;
using UnityEngine;

namespace Rive.Components
{
#if UNITY_EDITOR
    [HelpURL(InspectorDocLinks.RiveCanvasRenderer)]
#endif
    [AddComponentMenu("Rive/Rive Canvas Renderer")]
    [HideComponents(hideFlags: HideFlags.HideInInspector, typeof(CanvasRendererRawImage), typeof(CanvasPanelInputProvider))]
    [RequireComponent(typeof(CanvasRendererRawImage), typeof(IRivePanel))]
    public class RiveCanvasRenderer : PanelRenderer
    {
        [Tooltip("The RiveRawImage to display the Rive content.")]
        [HideInInspector]
        [SerializeField]
        private CanvasRendererRawImage m_displayImage;

        // We hide the RivePanel field because we want to use the RivePanel on the GameObject that this component is attached to.
        [HideInInspector]
        [Tooltip("The RivePanel to display")]
        [SerializeField] private RivePanel m_initialRivePanel;

        [Tooltip("A custom UI material to use when rendering the Rive graphic.")]
        [SerializeField] private Material m_customMaterial;

        [Tooltip("Whether to match the canvas resolution to the RivePanel's resolution. \n\nThis is useful for keeping the Rive graphic crisp when using a Canvas Scaler. By default, the RivePanel resolution is defined by it's rect transform's width and height. Setting this to true will cause the RivePanel to be rendered at a higher resolution than the panel's rect transform's size if needed.\n\nThis feature is currently only supported when the RivePanel uses the SimpleRenderTargetStrategy, which is the default strategy used if none is provided.")]
        [SerializeField] private bool m_matchCanvasResolution = false;


        private IRivePanel m_rivePanel;

        internal CanvasRendererRawImage DisplayImage => m_displayImage;

        [HideInInspector]
        [SerializeField] private CanvasPanelInputProvider m_inputProvider;

        public Canvas Canvas => DisplayImage == null ? null : DisplayImage.canvas;

        public RectTransform RectTransform => DisplayImage == null ? null : DisplayImage.rectTransform;

        public override IRivePanel RivePanel { get => m_rivePanel; internal set => m_rivePanel = value; }

        /// <summary>
        /// The custom material to use when rendering the Rive graphic.
        /// </summary>
        public Material CustomMaterial
        {
            get => m_customMaterial;
            set
            {
                m_customMaterial = value;
                UpdateCustomMaterial();
            }
        }

        public bool MatchCanvasResolution
        {
            get => m_matchCanvasResolution;
            set
            {
                if (m_matchCanvasResolution != value)
                {
                    m_matchCanvasResolution = value;
                    AttachCanvasProviders(RivePanel);

                    RivePanel rPanel = RivePanel as RivePanel;
                    if (rPanel != null)
                    {
                        rPanel.SetDirty(); // Force a redraw to apply the new resolution.
                    }
                }
            }
        }

        protected override void OnEnable()
        {
            Setup();
            base.OnEnable();


            if (m_matchCanvasResolution) AttachCanvasProviders(RivePanel);

            if (m_inputProvider != null && RivePanel != null)
            {
                RivePanel.RegisterInputProvider(m_inputProvider);

            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            if (m_inputProvider != null && RivePanel != null)
            {
                RivePanel.UnregisterInputProvider(m_inputProvider);

            }
        }

        /// <summary>
        /// Supersampling multiplier for the RenderTexture only.
        /// </summary>
        private float m_renderScale = 1f;


        /// <summary>
        /// Attach providers that the strategy can call to (1) size the RT and (2) pick draw scale.
        /// This keeps strategies/panels canvas-agnostic and lets other renderers (UITK, material)
        /// provide their own sizing rules or none at all.
        /// </summary>
        /// <param name="panel"></param>
        private void AttachCanvasProviders(IRivePanel panel)
        {
            var concretePanel = panel as RivePanel;
            if (concretePanel == null) return;

            var strategy = concretePanel.RenderTargetStrategy as RenderTargetStrategy;
            if (strategy == null) return;

            if (!m_matchCanvasResolution)
            {
                // Explicitly clear to legacy behavior
                strategy.ExternalPixelSizeProvider = null;
                strategy.ExternalDrawScaleProvider = null;
                return;
            }

            strategy.ExternalPixelSizeProvider = ComputeCanvasPixelSize;
            strategy.ExternalDrawScaleProvider = ComputeCanvasDrawScale;
        }


        /// <summary>
        /// Computes the pixel size of the canvas. Determines the size of the render texture based on the canvas scale factor and the local scale of the widget container.
        /// We do this because the Canvas Scaler (especially when set to "Scale With Screen Size") changes how many pixels each UI unit occupies on screen. Allocating the RT at that pixel size keeps Rive crisp.
        /// </summary>
        /// <param name="p">The panel to compute the pixel size for.</param>
        /// <returns>The pixel size of the canvas.</returns>
        private Vector2Int ComputeCanvasPixelSize(IRivePanel p)
        {
            var rt = p.WidgetContainer;
            var canvas = DisplayImage != null ? DisplayImage.canvas : null;
            if (rt == null || canvas == null) return new Vector2Int(1, 1);

            float canvasScale = canvas.scaleFactor;

            // Include local UI scale so “manually scaled” panels (e.g. 1080×2340 at 0.3333) resolve to the same on-screen pixels
            float uiW = rt.rect.width * Mathf.Abs(rt.localScale.x);
            float uiH = rt.rect.height * Mathf.Abs(rt.localScale.y);

            int w = Mathf.Max(1, Mathf.CeilToInt(uiW * canvasScale * m_renderScale));
            int h = Mathf.Max(1, Mathf.CeilToInt(uiH * canvasScale * m_renderScale));
            return new Vector2Int(w, h);
        }


        /// <summary>
        /// Computes the draw scale of the canvas. Used to ensure the graphics are drawn at the correct scale and position within the render texture.
        /// </summary>
        /// <param name="p">The panel to compute the draw scale for.</param>
        /// <returns>The draw scale of the canvas.</returns>
        private Vector2 ComputeCanvasDrawScale(IRivePanel p)
        {
            var rt = p.WidgetContainer;
            var canvas = DisplayImage != null ? DisplayImage.canvas : null;
            if (rt == null || canvas == null) return Vector2.one;

            float canvasScale = canvas.scaleFactor;

            float sx = canvasScale * Mathf.Abs(rt.localScale.x);
            float sy = canvasScale * Mathf.Abs(rt.localScale.y);
            return new Vector2(sx, sy);
        }

        private void Setup()
        {
            if (m_rivePanel == null)
            {
                m_rivePanel = m_initialRivePanel;

            }

            if (m_rivePanel == null)
            {
                m_rivePanel = GetComponent<IRivePanel>();

            }
            if (m_displayImage == null)
            {
                if (!TryGetComponent(out m_displayImage))
                {
                    m_displayImage = gameObject.AddComponent<CanvasRendererRawImage>();
                }
            }

            // We need to make sure the RivePanel's RawImage is a raycast target, otherwise we won't get any pointer input.
            if (!m_displayImage.raycastTarget)
            {
                m_displayImage.raycastTarget = true;

            }

            if (m_inputProvider == null && !TryGetComponent(out m_inputProvider))
            {
                m_inputProvider = gameObject.AddComponent<CanvasPanelInputProvider>();
            }




            // Initialize the input mode, in case it was not set.
            HandlePointerInputModeChanged();

            if (m_rivePanel == null)
            {
                DebugLogger.Instance.LogWarning($"No {nameof(RivePanel)} component found for this {nameof(RiveCanvasRenderer)} - {gameObject.name}");
            }
        }



        private void LogInputErrorWarningsIfNeeded()
        {
            if (PointerInputMode == PointerInputMode.DisablePointerInput)
            {
                return;
            }
            // If there's no event system in the scene, we log a warning.
            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                DebugLogger.Instance.LogWarning($"No EventSystem found in the scene. Please add an EventSystem to the scene to receive pointer input events.  If you don't want to receive pointer input events, set the {nameof(PointerInputMode)} to {PointerInputMode.DisablePointerInput}.");
            }

            // If there's no GraphicRaycaster in the scene, we log a warning.
            if (DisplayImage != null && !DisplayImage.canvas.TryGetComponent<UnityEngine.UI.GraphicRaycaster>(out _))
            {
                DebugLogger.Instance.LogWarning($"No GraphicRaycaster found in the scene. Please add a GraphicRaycaster to the scene to receive pointer input events. If you don't want to receive pointer input events, set the {nameof(PointerInputMode)} to {PointerInputMode.DisablePointerInput}.");
            }
        }

        private void Start()
        {
            LogInputErrorWarningsIfNeeded();

            UpdateCustomMaterial();

        }

        private void UpdateCustomMaterial()
        {
            if (DisplayImage == null)
            {
                return;
            }

            Material materialToApply = m_customMaterial;

            if (materialToApply == null && Application.isPlaying && TextureHelper.ProjectNeedsColorSpaceFix)
            {
                materialToApply = TextureHelper.GammaToLinearUIMaterial;
            }

            DisplayImage.material = materialToApply;
        }


        protected override void UpdateVisualTarget()
        {
            if (RivePanel == null)
            {
                return;
            }
            var renderTexture = RivePanel.RenderTexture;
            if (renderTexture == null)
            {
                return;
            }

            m_displayImage.texture = renderTexture;

            Vector2 offset = RivePanel.OffsetInRenderTexture;
            Vector2 scale = RivePanel.ScaleInRenderTexture;


            m_displayImage.uvRect = new Rect(offset, scale);
        }



        protected override void OnValidate()
        {
            base.OnValidate();

            if (m_initialRivePanel == null)
            {
                m_initialRivePanel = GetComponent<RivePanel>();
            }

            if (m_displayImage == null)
            {
                m_displayImage = GetComponent<CanvasRendererRawImage>();
            }

            if (m_inputProvider == null)
            {
                m_inputProvider = GetComponent<CanvasPanelInputProvider>();
            }
        }


        protected override void HandlePointerInputModeChanged()
        {
            if (m_displayImage == null)
            {
                return;
            }

            if (PointerInputMode == PointerInputMode.EnablePointerInput && !m_displayImage.raycastTarget)
            {
                m_displayImage.raycastTarget = true;
            }
            else if (PointerInputMode == PointerInputMode.DisablePointerInput && m_displayImage.raycastTarget)
            {
                m_displayImage.raycastTarget = false;
            }
        }


    }
}
