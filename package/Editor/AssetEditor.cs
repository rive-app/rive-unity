using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

namespace Rive
{
    [CustomEditor(typeof(Asset))]
    public class AssetEditor : Editor
    {
        File m_file;
        private Artboard m_artboard;
        private StateMachine m_stateMachine;
        private double m_lastTime = 0.0;
        private bool m_showAssets = true;

        public override bool HasPreviewGUI() => true;

        public override bool RequiresConstantRepaint()
        {
            return true;
        }

        public override void OnInspectorGUI()
        {
            var riveAsset = (Asset)target;

            m_showAssets = EditorGUILayout.BeginFoldoutHeaderGroup(m_showAssets, "File Assets");
            if (m_showAssets)
            {
                foreach (var embeddedAsset in riveAsset.assets)
                {
                    EditorGUILayout.EnumPopup("Type:", embeddedAsset.type);
                    EditorGUILayout.TextField("Name: ", embeddedAsset.name);
                    EditorGUILayout.TextField("ID: ", embeddedAsset.id.ToString());
                    if (embeddedAsset.asset == null)
                    {
                        EditorGUILayout.TextField(
                            "Embedded: ",
                            FormatBytes(embeddedAsset.embeddedBytes)
                        );
                    }
                    else
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.PrefixLabel(
                            new GUIContent("Asset", "The referenced asset.")
                        );
                        switch (embeddedAsset.type)
                        {
                            case EmbeddedAssetType.font:

                                {
                                    EditorGUILayout.ObjectField(
                                        embeddedAsset.asset,
                                        typeof(FontAsset),
                                        false
                                    );
                                }
                                break;

                            case EmbeddedAssetType.image:

                                {
                                    EditorGUILayout.ObjectField(
                                        embeddedAsset.asset,
                                        typeof(ImageAsset),
                                        false
                                    );
                                }
                                break;

                            case EmbeddedAssetType.audio:

                                {
                                    EditorGUILayout.ObjectField(
                                        embeddedAsset.asset,
                                        typeof(AudioAsset),
                                        false
                                    );
                                }
                                break;
                        }
                        EditorGUILayout.EndHorizontal();
                    }

                    EditorGUILayout.Space();
                }
            }
        }

        public override Texture2D RenderStaticPreview(
            string assetPath,
            Object[] subAssets,
            int width,
            int height
        )
        {
            RenderTexture prev = RenderTexture.active;
            var rect = new Rect(0, 0, width, height);
            RenderTexture rt = Render(rect, true);

            if (rt != null)
            {
                RenderTexture.active = rt;

                Texture2D tex = new Texture2D(width, height);
                tex.ReadPixels(rect, 0, 0);
                tex.Apply(true);

                RenderTexture.active = prev;
                return tex;
            }
            return null;
        }

        RenderTexture Render(Rect rect, bool isStatic = false)
        {
            int width = (int)rect.width;
            int height = (int)rect.height;

            var descriptor = Rive.TextureHelper.Descriptor(width, height);
            RenderTexture rt = RenderTexture.GetTemporary(descriptor);

            var cmb = new CommandBuffer();

            cmb.SetRenderTarget(rt);
            // cmb.ClearRenderTarget(true, true, UnityEngine.Color.blue, 0.0f);

            if (m_file == null)
            {
                var riveAsset = (Rive.Asset)target;
                m_file = Rive.File.Load(riveAsset);
                m_artboard = m_file?.Artboard(0);
                m_stateMachine = m_artboard?.StateMachine();
            }

            if (m_artboard != null)
            {
                var rq = new RenderQueue(
                    UnityEngine.SystemInfo.graphicsDeviceType == GraphicsDeviceType.Metal
                        ? null
                        : rt
                );
                var renderer = rq.Renderer();
                renderer.Align(Fit.contain, Alignment.Center, m_artboard);
                renderer.Draw(m_artboard);
                renderer.AddToCommandBuffer(cmb);
                if (!isStatic)
                {
                    var now = EditorApplication.timeSinceStartup;
                    double time = now - m_lastTime;
                    m_stateMachine?.Advance((float)(now - m_lastTime));
                    m_lastTime = now;
                }
                else
                {
                    m_stateMachine?.Advance(0.0f);
                }
            }
            var prev = RenderTexture.active;
            Graphics.ExecuteCommandBuffer(cmb);
            GL.InvalidateState();
            cmb.Clear();

            if (isStatic && FlipY())
            {
                RenderTexture temp = RenderTexture.GetTemporary(
                    width,
                    height,
                    0,
                    RenderTextureFormat.Default,
                    RenderTextureReadWrite.Default
                );
                temp.Create();

                Graphics.Blit(rt, temp, new Vector2(1, -1), new Vector2(0, 1));
                RenderTexture.ReleaseTemporary(rt);
                rt = temp;
            }

            RenderTexture.ReleaseTemporary(rt);
            return rt;
        }

        public override void OnPreviewGUI(Rect rect, GUIStyle background)
        {
            if (Event.current.type == EventType.Repaint)
            {
                RenderTexture rt = Render(rect);

                UnityEditor.EditorGUI.DrawPreviewTexture(
                    FlipY()
                        ? new Rect(rect.x, rect.y + rect.height, rect.width, -rect.height)
                        : rect,
                    rt
                );
            }
        }

        private void UnloadPreview()
        {
            m_stateMachine = null;
            m_artboard = null;
            m_file = null;
        }

        public void OnDisable()
        {
            var riveAsset = (Rive.Asset)target;
            UnloadPreview();
        }

        private static bool FlipY()
        {
            switch (UnityEngine.SystemInfo.graphicsDeviceType)
            {
                case GraphicsDeviceType.Metal:
                case GraphicsDeviceType.Direct3D11:
                    return true;
                default:
                    return false;
            }
        }

        static string FormatBytes(uint byteCount)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            while (byteCount >= 1024 && order < sizes.Length - 1)
            {
                order++;
                byteCount /= 1024;
            }

            // Adjust the format string to your preferences. For example "{0:0.#}{1}" would
            // show a single decimal place, and no space.
            return string.Format("{0:0.##} {1}", byteCount, sizes[order]);
        }
    }
}
