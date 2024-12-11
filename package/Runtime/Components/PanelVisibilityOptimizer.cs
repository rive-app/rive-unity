using Rive.Utils;
using UnityEngine;

namespace Rive.Components
{


    /// <summary>
    /// This hides and shows RivePanels based on the visibility of the mesh renderer. It should be attached to a GameObject with a Renderer component.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    internal class PanelVisibilityOptimizer : MonoBehaviour
    {
        [SerializeField] private RiveTextureRenderer m_panelRenderer;

        [SerializeField]
        [Tooltip("Determines whether to optimize rendering based on visibility or always render")]
        private VisibilityOptimizationMode m_visibilityMode = VisibilityOptimizationMode.RenderWhenVisible;

        // We use this flag to prevent the component from handling changes before it is fully initialized.
        // The Renderer.isVisible property seems to return false before Start() so we want to avoid hiding the object in that case if it is supposed to be visible or we'll get a flash.
        private bool m_readyForRenderingControl = false;


        private bool IsVisible
        {
            get
            {
                if (m_panelRenderer == null)
                {
                    return false;
                }

                return m_panelRenderer.Renderer.isVisible;
            }

        }

        /// <summary>
        /// Gets or sets the visibility mode. When set to AlwaysRender, the panel will render regardless of visibility.
        /// </summary>
        public VisibilityOptimizationMode VisibilityMode
        {
            get => m_visibilityMode;
            set
            {
                if (m_visibilityMode != value)
                {
                    m_visibilityMode = value;
                    HandleVisibility();
                }
            }
        }

        private void OnEnable()
        {
            if (m_panelRenderer == null)
            {
                m_panelRenderer = GetComponent<RiveTextureRenderer>();
            }

            if (m_panelRenderer == null)
            {
                DebugLogger.Instance.LogWarning($"No ${nameof(RiveTextureRenderer)} component found on the GameObject - " + gameObject.name);
                return;
            }

            HandleVisibility();
            SubscribeToRiveViewEvents(m_panelRenderer.RivePanel);
        }


        private void Start()
        {
            HandleVisibility();
            m_readyForRenderingControl = true;
        }

        private void OnDisable()
        {
            UnsubscribeFromRivePanelEvents(m_panelRenderer.RivePanel);
        }

        private void SubscribeToRiveViewEvents(IRivePanel rivePanel)
        {
            if (rivePanel != null)
            {
                rivePanel.OnRenderingStateChanged += HandleVisibility;
            }
        }

        private void UnsubscribeFromRivePanelEvents(IRivePanel rivePanel)
        {
            if (rivePanel != null)
            {
                rivePanel.OnRenderingStateChanged -= HandleVisibility;
            }
        }

        private bool m_handlingVisibility = false;



        /// <summary>
        /// Handles the visibility state by starting or stopping rendering based on the current visibility.
        /// </summary>
        private void HandleVisibility()
        {
            // Prevent recursive calls
            if (m_handlingVisibility)
            {
                return;
            }

            m_handlingVisibility = true;

            try
            {
                if (!m_readyForRenderingControl || m_panelRenderer == null || m_panelRenderer.RivePanel == null ||
                    m_panelRenderer.Renderer == null || (m_panelRenderer.RivePanel != null && !m_panelRenderer.RivePanel.Enabled))
                {
                    return;
                }

                if (m_visibilityMode == VisibilityOptimizationMode.AlwaysRender)
                {
                    if (!m_panelRenderer.RivePanel.IsRendering)
                    {
                        m_panelRenderer.RivePanel.StartRendering();
                    }
                    return;
                }

                if (IsVisible && !m_panelRenderer.RivePanel.IsRendering)
                {
                    m_panelRenderer.RivePanel.StartRendering();
                }
                else if (!IsVisible && m_panelRenderer.RivePanel.IsRendering)
                {
                    m_panelRenderer.RivePanel.StopRendering();
                }
            }
            finally
            {
                m_handlingVisibility = false;
            }
        }



        private void OnBecameVisible()
        {
            HandleVisibility();
        }

        private void OnBecameInvisible()
        {
            HandleVisibility();
        }



        private void OnValidate()
        {
            if (m_panelRenderer == null)
            {
                m_panelRenderer = GetComponent<RiveTextureRenderer>();
            }
        }
    }
}