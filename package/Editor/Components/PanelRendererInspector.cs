using Rive.Components;
using UnityEditor;


namespace Rive.EditorTools
{
    [CustomEditor(typeof(PanelRenderer), true)]
    internal class PanelRendererInspector : RiveBaseEditor
    {

        protected PanelRenderer PanelRenderer => target as PanelRenderer;

    }
}
