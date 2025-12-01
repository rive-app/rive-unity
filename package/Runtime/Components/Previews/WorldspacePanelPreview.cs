using UnityEngine;
using System.Collections;

#if UNITY_EDITOR

using UnityEditor;
using UnityEditor.SceneManagement;

#endif

namespace Rive.Components
{

    /// <summary>
    /// Handles the preview rendering for the RivePanel component in the scene when in Edit mode.
    /// </summary>
    internal class WorldspacePanelPreview : PanelPreview
    {
#if UNITY_EDITOR
        private Texture m_lastPreviewTexture;
        private GameObject m_previewQuad;
        private Material m_previewMaterial;
        private bool m_isUpdating;
        private Coroutine m_updateCoroutine;

        public WorldspacePanelPreview(RivePanel panel) : base(panel) { }

        protected override void Initialize()
        {
            base.Initialize();
            SceneView.duringSceneGui += OnSceneGUI;
            CreatePreviewQuad();
        }

        public override void Dispose()
        {
            if (m_updateCoroutine != null && RivePanel != null)
            {
                RivePanel.StopCoroutine(m_updateCoroutine);
                m_updateCoroutine = null;
            }
            SceneView.duringSceneGui -= OnSceneGUI;
            DestroyPreviewQuad();
            base.Dispose();
        }

        protected override void OnEditorUpdate()
        {
            base.OnEditorUpdate();

            if ((!SceneViewIsVisible() || !IsRelevantObjectSelected()) && m_previewQuad != null)
            {
                m_previewQuad.SetActive(false);
            }
        }

        private bool IsRelevantObjectSelected()
        {
            if (Selection.activeGameObject == null) return false;

            if (RivePanel == null) return false;

            // We only want to show the preview  if the selected object is the panel itself
            if (Selection.activeGameObject == RivePanel.gameObject) return true;

            // Or if the selected object is a child widget of the panel
            return Selection.activeGameObject.transform.IsChildOf(RivePanel.transform);
        }

        private bool SceneViewIsVisible()
        {
            return EditorWindow.focusedWindow != null &&
                   (EditorWindow.focusedWindow.GetType().Name == "SceneView" ||
                    EditorWindow.focusedWindow.titleContent.text == "Scene");
        }

        protected override void UpdateEditorPreview()
        {
            if (!m_isUpdating && RivePanel.gameObject.activeInHierarchy && RivePanel.enabled)
            {
                // So we use a coroutine here instead of immediate execution because the preview quad's material needs to be updated in a specific timing relative to
                // Unity's render pipeline otherwise the graphic won't show up. The 'yield return null' in the coroutine ensures that our texture update happens at the right moment in the frame, preventing visual artifacts like white/blank textures that can happen with immediate execution.
                // Technically, we could use EditorApplication.delayCall here, but that introduces a visual delay which is not ideal.
                if (m_updateCoroutine == null)
                {
                    m_updateCoroutine = RivePanel.StartCoroutine(UpdatePreviewCoroutine());
                }
            }
        }

        private IEnumerator UpdatePreviewCoroutine()
        {
            m_isUpdating = true;
            yield return null;

            if (!EditorSceneManager.GetActiveScene().isLoaded || Application.isPlaying || RivePanel == null)
            {
                m_isUpdating = false;
                m_updateCoroutine = null;
                yield break;
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

            // Apply color correction in Linear color space for correct preview display
            // The scene view renders 3D objects normally, so we need to use a material that decodes 
            // Rive's gamma output to linear for the scene view to display correctly.
            if (Rive.TextureHelper.ProjectNeedsColorSpaceFix)
            {
                var decodeMat = Rive.TextureHelper.GammaToLinearUIMaterial;
                if (decodeMat != null && m_previewMaterial != null && m_previewMaterial.shader != decodeMat.shader)
                {
                    m_previewMaterial.shader = decodeMat.shader;
                }
            }
            else
            {
                // In Gamma color space, use Unlit/Transparent, no need to decode anything.
                if (m_previewMaterial != null && m_previewMaterial.shader.name != "Unlit/Transparent")
                {
                    Shader unlitTransparent = Shader.Find("Unlit/Transparent");
                    if (unlitTransparent != null)
                    {
                        m_previewMaterial.shader = unlitTransparent;
                    }
                }
            }

            if (previewTexture != m_lastPreviewTexture)
            {
                if (m_previewMaterial != null)
                {
                    m_previewMaterial.mainTexture = previewTexture;
                }

                m_lastPreviewTexture = previewTexture;
                PreviewRenderTexture = previewTexture as RenderTexture;
            }

            m_isUpdating = false;
            m_updateCoroutine = null;
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (m_previewQuad != null && m_lastPreviewTexture != null && IsRelevantObjectSelected())
            {
                AlignPreviewQuad();
                m_previewQuad.SetActive(true);
            }
        }

        private void CreatePreviewQuad()
        {
            if (m_previewQuad == null)
            {
                m_previewQuad = new GameObject("RivePreviewQuad");
                m_previewQuad.hideFlags = HideFlags.HideAndDontSave;

                MeshFilter mf = m_previewQuad.AddComponent<MeshFilter>();
                MeshRenderer mr = m_previewQuad.AddComponent<MeshRenderer>();

                Mesh mesh = new Mesh
                {
                    vertices = new Vector3[]
                    {
                    new Vector3(-0.5f, -0.5f, 0),
                    new Vector3(0.5f, -0.5f, 0),
                    new Vector3(-0.5f, 0.5f, 0),
                    new Vector3(0.5f, 0.5f, 0)
                    },
                    uv = new Vector2[]
                    {
                    new Vector2(0, 0),
                    new Vector2(1, 0),
                    new Vector2(0, 1),
                    new Vector2(1, 1)
                    },
                    triangles = new int[]
                    { 
                    // Front face
                    0, 2, 1,
                    2, 3, 1,    
                    
                    // Back face 
                    1, 2, 0,
                    1, 3, 2
                    }
                };
                mf.mesh = mesh;

                // We use the transparent shader (instead of something like "Unlit/Texture") to make sure Rive files with transparency are displayed correctly in the scene view
                Shader previewShader = Shader.Find("Unlit/Transparent");
                m_previewMaterial = new Material(previewShader);
                m_previewMaterial.renderQueue = 3000;

                m_previewMaterial = new Material(previewShader);
                mr.material = m_previewMaterial;

                if (SceneViewIsVisible())
                {
                    m_previewQuad.SetActive(true);
                }
                else
                {
                    m_previewQuad.SetActive(false);
                }
            }
        }

        private void DestroyPreviewQuad()
        {
            if (m_previewQuad != null)
            {
                Object.DestroyImmediate(m_previewQuad);
            }
            if (m_previewMaterial != null)
            {
                Object.DestroyImmediate(m_previewMaterial);
            }
        }

        private void AlignPreviewQuad()
        {
            RectTransform rectTransform = RivePanel.transform as RectTransform;
            if (rectTransform == null || m_previewQuad == null)
                return;

            // Match the position and rotation of the RectTransform
            m_previewQuad.transform.position = rectTransform.position;
            m_previewQuad.transform.rotation = rectTransform.rotation;

            Vector2 size = rectTransform.rect.size;
            Vector3 rectScale = rectTransform.lossyScale;
            m_previewQuad.transform.localScale = new Vector3(size.x * rectScale.x, size.y * rectScale.y, 1);

            // Ensure the quad is slightly in front of the actual object
            m_previewQuad.transform.position += m_previewQuad.transform.forward * 0.01f;
        }

        protected override void CleanupResources()
        {
            base.CleanupResources();
            DestroyPreviewQuad();
            m_lastPreviewTexture = null;
            m_isUpdating = false;
        }
#endif
    }
}
