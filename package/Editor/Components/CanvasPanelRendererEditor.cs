using Rive.Components;
using UnityEditor;

namespace Rive.EditorTools
{
    [CustomEditor(typeof(RiveCanvasRenderer), true)]
    internal class CanvasPanelRendererEditor : PanelRendererInspector
    {
        protected override void OnEnable()
        {
            base.OnEnable();

            if (PanelRenderer.RivePanel == null)
            {
                RivePanel existingPanel = PanelRenderer.GetComponent<RivePanel>();

                if (existingPanel != null)
                {
                    PanelRenderer.RivePanel = existingPanel;
                }
            }
        }

    }
}
