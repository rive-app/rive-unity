using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

namespace Rive.Components
{
    /// <summary>
    /// Handles the preview rendering for the RiveCanvasPanel component in the editor.
    /// </summary>
    internal class CanvasPanelPreview : PanelPreview
    {
#if UNITY_EDITOR
        private CanvasRendererRawImage m_displayImage;
        private Texture m_lastPreviewTexture;
        private bool m_isUpdating;

        public CanvasPanelPreview(RivePanel panel) : base(panel)
        {
            m_displayImage = panel.GetComponent<CanvasRendererRawImage>();
        }

        protected override void Initialize()
        {
            EditorSceneManager.sceneLoaded += OnSceneLoaded;
            base.Initialize();
        }

        public override void Dispose()
        {
            EditorSceneManager.sceneLoaded -= OnSceneLoaded;
            CleanupResources();
            base.Dispose();
        }

        private void OnSceneLoaded(Scene arg0, LoadSceneMode arg1)
        {
            UpdateEditorPreview();
        }

        protected override void CleanupResources()
        {
            base.CleanupResources();
            if (m_displayImage != null)
            {
                m_displayImage.CleanupEditorPreview();
            }
            m_lastPreviewTexture = null;
            m_isUpdating = false;
        }

        protected override void UpdateEditorPreview()
        {
            // We want to prevent multiple calls to UpdateEditorPreview in the same frame 
            // as that can cause performance issues and glitches
            if (!m_isUpdating && RivePanel.gameObject.activeInHierarchy && RivePanel.enabled)
            {
                DelayedUpdatePreview();
            }

        }

        private void DelayedUpdatePreview()
        {
            // If the scene is not loaded, we don't want to update the preview 
            // because it can cause issues when switching scenes
            if (m_displayImage == null && RivePanel != null)
            {
                m_displayImage = RivePanel.GetComponent<CanvasRendererRawImage>();
            }

            if (m_displayImage == null)
            {
                m_isUpdating = false;
                return;
            }

            if (!EditorSceneManager.GetActiveScene().isLoaded ||
                Application.isPlaying ||
                RivePanel == null ||
                m_displayImage == null)
            {
                m_isUpdating = false;
                return;
            }

            Texture previewTexture;

            if (RivePanel == null)
            {
                previewTexture = GetDefaultTexture();
            }
            else
            {
                RenderTexture rt = RenderPreview();
                previewTexture = rt != null ? rt : GetDefaultTexture();
            }

            if (previewTexture != m_lastPreviewTexture || m_displayImage.mainTexture != previewTexture)
            {
                m_displayImage.UpdateEditorPreview(previewTexture);

                m_lastPreviewTexture = previewTexture;

                // Update PreviewRenderTexture only if the new texture is a RenderTexture
                if (previewTexture is RenderTexture)
                {
                    PreviewRenderTexture = previewTexture as RenderTexture;
                }
                else
                {
                    PreviewRenderTexture = null;
                }
            }

            m_isUpdating = false;

        }
#endif
    }
}