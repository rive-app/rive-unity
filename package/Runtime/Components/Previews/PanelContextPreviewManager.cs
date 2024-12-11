using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Rive.Components
{
    /// <summary>
    /// Manages the preview of the RivePanel depending on its context. e.g Within a Canvas or in World Space as a texture.
    /// </summary>
#if UNITY_EDITOR
    [ExecuteInEditMode]
#endif
    internal class PanelContextPreviewManager : MonoBehaviour
    {
        private enum PanelContextOption
        {
            Unset = 0,
            Canvas = 1,
            World = 2
        }

        [HideInInspector]
        [SerializeField] private RivePanel m_rivePanel;

#if UNITY_EDITOR
        private PanelContextOption m_panelContext;
        private PanelPreview m_currentPreview;

        private PanelContextOption PanelContext
        {
            get => m_panelContext;
            set
            {
                if (m_panelContext != value || m_currentPreview == null)
                {
                    m_panelContext = value;
                    SpawnPreviewForContext();
                }
            }
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                return;
            }

            if (m_rivePanel == null)
            {
                m_rivePanel = GetComponent<RivePanel>();
            }
            CheckContext();
        }

        private void OnDisable()
        {
            if (Application.isPlaying)
            {
                return;
            }


            if (m_currentPreview != null)
            {
                m_currentPreview.Dispose();
                m_currentPreview = null;
            }
        }

        private void SpawnPreviewForContext()
        {
            if (Application.isPlaying || m_rivePanel == null)
            {
                return;
            }

            // Cleanup existing preview
            if (m_currentPreview != null)
            {
                m_currentPreview.Dispose();
                m_currentPreview = null;
            }


            // Create new preview based on context
            if (PanelContext == PanelContextOption.Canvas)
            {
                m_currentPreview = new CanvasPanelPreview(m_rivePanel);
            }
            else if (PanelContext == PanelContextOption.World)
            {
                m_currentPreview = new WorldspacePanelPreview(m_rivePanel);
            }
        }

        private bool IsPanelInOrOnCanvas()
        {
            if (m_rivePanel == null)
            {
                return false;
            }

            Canvas canvas = m_rivePanel.GetComponentInParent<Canvas>();
            return canvas != null;
        }

        private void CheckContext()
        {
            if (m_rivePanel == null || Application.isPlaying)
            {
                return;
            }

            PanelContext = IsPanelInOrOnCanvas() ? PanelContextOption.Canvas : PanelContextOption.World;
        }

        private void OnTransformParentChanged()
        {
            CheckContext();
        }

        private void OnValidate()
        {
            if (m_rivePanel == null)
            {
                m_rivePanel = GetComponent<RivePanel>();
            }
        }

        void Reset()
        {
            // This is called as soon as component is added in inspector
            // This component in particular seems to cause issues with the HideFlags when used with the HideComponentsAttribute in the RivePanel component.
            // This is a workaround to ensure that the HideFlags are set correctly.
            this.hideFlags = HideFlags.HideInInspector;
        }

#endif
    }
}