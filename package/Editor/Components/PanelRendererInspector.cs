using Rive.Components;
using UnityEditor;


namespace Rive.EditorTools
{
    [CustomEditor(typeof(RivePanelRenderer), true)]
    internal class PanelRendererInspector : RiveBaseEditor
    {

        protected RivePanelRenderer PanelRenderer => target as RivePanelRenderer;

    }
}
