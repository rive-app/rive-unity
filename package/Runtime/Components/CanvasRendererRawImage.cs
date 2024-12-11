using UnityEngine;
using UnityEngine.UI;

namespace Rive.Components
{
    /// <summary>
    /// A RawImage that can display Rive content. Also used to display Rive content in the editor.
    /// </summary>
    internal class CanvasRendererRawImage : RawImage
    {
#if UNITY_EDITOR
        private Texture m_editorPreviewTexture;
        private bool m_isPopulatingMesh = false;

        internal void UpdateEditorPreview(Texture previewTexture)
        {
            if (Application.isPlaying) return;
            if (m_editorPreviewTexture != previewTexture)
            {
                m_editorPreviewTexture = previewTexture;
                // we're using SetAllDirty() to mark the graphic for rebuilding
                SetAllDirty();
            }
        }

        internal void CleanupEditorPreview()
        {
            if (!Application.isPlaying)
            {
                m_editorPreviewTexture = null;
                SetAllDirty();
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (!Application.isPlaying)
            {
                CleanupEditorPreview();
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (!Application.isPlaying)
            {
                CleanupEditorPreview();
            }
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            if (!Application.isPlaying)
            {
                SetAllDirty();
            }
        }
        // We need to override mainTexture to return the editor preview texture when not playing
        // We do this to avoid dirtying the scene view when the editor preview texture changes
        public override Texture mainTexture
        {
            get
            {
                if (!Application.isPlaying && m_editorPreviewTexture != null)
                {
                    return m_editorPreviewTexture;
                }
                return base.mainTexture;
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            // We've added an m_isPopulatingMesh flag to prevent recursive calls to OnPopulateMesh.

            if (m_isPopulatingMesh) return;

            m_isPopulatingMesh = true;
            base.OnPopulateMesh(vh);
            m_isPopulatingMesh = false;
        }
#endif
    }
}