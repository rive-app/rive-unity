using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

namespace Rive
{
    [CustomEditor(typeof(Rive.Asset))]
    public class AssetEditor : Editor
    {
        Rive.File m_file;
        private Artboard m_artboard;
        private StateMachine m_stateMachine;
        private double m_lastTime = 0.0;

        public override bool HasPreviewGUI() => true;

        public override bool RequiresConstantRepaint()
        {
            return true;
        }

        public override void OnPreviewGUI(UnityEngine.Rect rect, GUIStyle background)
        {
            if (Event.current.type == EventType.Repaint)
            {
                int width = (int)rect.width;
                int height = (int)rect.height;
                RenderTexture rt = RenderTexture.GetTemporary(
                    width,
                    height,
                    0,
                    RenderTextureFormat.ARGB32
                );

                rt.enableRandomWrite = true;
                rt.Create();

                var cmb = new CommandBuffer();
                // cmb.SetRenderTarget(rt);
                // cmb.ClearRenderTarget(true, true, UnityEngine.Color.clear, 0.0f);

                if (m_file == null)
                {
                    var riveAsset = (Rive.Asset)target;
                    m_file = Rive.File.load(
                        riveAsset.name,
                        riveAsset.bytes,
                        riveAsset.GetInstanceID()
                    );
                    m_artboard = m_file?.artboard(0);
                    m_stateMachine = m_artboard?.stateMachine();
                }
                if (m_artboard != null)
                {
                    // On Metal the temporary texture doesn't seem to work.
                    var rq = new RenderQueue(
                        UnityEngine.SystemInfo.graphicsDeviceType
                        == UnityEngine.Rendering.GraphicsDeviceType.Metal
                            ? null
                            : rt
                    );
                    rq.align(Fit.contain, Alignment.center, m_artboard);
                    rq.draw(m_artboard);
                    rq.addToCommandBuffer(cmb);
                    var now = EditorApplication.timeSinceStartup;
                    double time = now - m_lastTime;
                    m_stateMachine?.advance((float)(now - m_lastTime));
                    m_lastTime = now;
                }

                Graphics.ExecuteCommandBuffer(cmb);
                GL.InvalidateState();
                UnityEditor.EditorGUI.DrawPreviewTexture(
                    flipY() ? new Rect(rect.x, rect.y + height, width, -height) : rect,
                    rt
                );
                cmb.Clear();
                RenderTexture.ReleaseTemporary(rt);
            }
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

        public void OnDisable()
        {
            m_stateMachine = null;
            m_artboard = null;
            m_file = null;
        }
    }
}
