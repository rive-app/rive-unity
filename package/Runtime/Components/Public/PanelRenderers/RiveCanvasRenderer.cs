using Rive.EditorTools;
using Rive.Utils;
using UnityEngine;

namespace Rive.Components
{
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


        private IRivePanel m_rivePanel;

        internal CanvasRendererRawImage DisplayImage => m_displayImage;

        [HideInInspector]
        [SerializeField] private CanvasPanelInputProvider m_inputProvider;

        public Canvas Canvas => DisplayImage == null ? null : DisplayImage.canvas;

        public RectTransform RectTransform => DisplayImage == null ? null : DisplayImage.rectTransform;

        public override IRivePanel RivePanel { get => m_rivePanel; internal set => m_rivePanel = value; }



        protected override void OnEnable()
        {
            Setup();
            base.OnEnable();

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
