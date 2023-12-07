using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;

namespace Rive
{
    [CustomEditor(typeof(Rive.Asset))]
    public class AssetEditor : Editor
    {
        Rive.File m_file;
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
            var riveAsset = (Rive.Asset)target;

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
                            formatBytes(embeddedAsset.embeddedBytes)
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
            // Because we create our own MTLCommandBuffer there's contention for
            // when Unity flushes and we do, which means Textures shared across
            // those buffers may not be ready in time.
            switch (UnityEngine.SystemInfo.graphicsDeviceType)
            {
                case UnityEngine.Rendering.GraphicsDeviceType.Metal:
                    return null;
            }
            RenderTexture prev = RenderTexture.active;
            var rect = new UnityEngine.Rect(0, 0, width, height);
            RenderTexture rt = render(rect, true);

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

        RenderTexture render(UnityEngine.Rect rect, bool isStatic = false)
        {
            int width = (int)rect.width;
            int height = (int)rect.height;

            var descriptor = new RenderTextureDescriptor(
                width,
                height,
                RenderTextureFormat.Default
            );

            if (
                UnityEngine.SystemInfo.graphicsDeviceType
                == UnityEngine.Rendering.GraphicsDeviceType.Direct3D11
            )
            {
                // Enabling random write with metal textures was causing
                // problems with render to texture so we make sure to set
                // this only when we really need it.
                descriptor.enableRandomWrite = true;
            }

            RenderTexture rt = RenderTexture.GetTemporary(descriptor);

            var cmb = new CommandBuffer();

            cmb.SetRenderTarget(rt);
            // cmb.ClearRenderTarget(true, true, UnityEngine.Color.blue, 0.0f);

            if (m_file == null)
            {
                var riveAsset = (Rive.Asset)target;
                m_file = Rive.File.load(riveAsset);
                m_artboard = m_file?.artboard(0);
                m_stateMachine = m_artboard?.stateMachine();
            }

            if (m_artboard != null)
            {
                var rq = new RenderQueue(rt);
                rq.align(Fit.contain, Alignment.center, m_artboard);
                rq.draw(m_artboard);
                rq.addToCommandBuffer(cmb);
                if (!isStatic)
                {
                    var now = EditorApplication.timeSinceStartup;
                    double time = now - m_lastTime;
                    m_stateMachine?.advance((float)(now - m_lastTime));
                    m_lastTime = now;
                }
                else
                {
                    m_stateMachine?.advance(0.0f);
                }
            }
            var prev = RenderTexture.active;
            Graphics.ExecuteCommandBuffer(cmb);
            GL.InvalidateState();
            cmb.Clear();

            if (isStatic && flipY())
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

        public override void OnPreviewGUI(UnityEngine.Rect rect, GUIStyle background)
        {
            if (Event.current.type == EventType.Repaint)
            {
                RenderTexture rt = render(rect);

                UnityEditor.EditorGUI.DrawPreviewTexture(
                    flipY()
                        ? new Rect(rect.x, rect.y + rect.height, rect.width, -rect.height)
                        : rect,
                    rt
                );
            }
        }

        private void unloadPreview()
        {
            m_stateMachine = null;
            m_artboard = null;
            m_file = null;
        }

        public void OnDisable()
        {
            var riveAsset = (Rive.Asset)target;
            unloadPreview();
        }

        private static bool flipY()
        {
            switch (UnityEngine.SystemInfo.graphicsDeviceType)
            {
                case UnityEngine.Rendering.GraphicsDeviceType.Metal:
                case UnityEngine.Rendering.GraphicsDeviceType.Direct3D11:
                    return true;
                default:
                    return false;
            }
        }

        static string formatBytes(uint byteCount)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            while (byteCount >= 1024 && order < sizes.Length - 1)
            {
                order++;
                byteCount = byteCount / 1024;
            }

            // Adjust the format string to your preferences. For example "{0:0.#}{1}" would
            // show a single decimal place, and no space.
            return string.Format("{0:0.##} {1}", byteCount, sizes[order]);
        }
    }
}
