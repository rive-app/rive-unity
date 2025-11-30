using Rive.Components;
using UnityEditor;
using UnityEngine.UIElements;


namespace Rive.EditorTools
{

    [CustomEditor(typeof(RiveTextureRenderer), true)]
    internal class TexturePanelRendererEditor : PanelRendererInspector
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = base.CreateInspectorGUI() ?? new VisualElement();

            // For worldspace renderers, we display a button to convert materials on the current mesh renderer, if needed.
            // Makes it easier for users to switch to Rive materials without having to know the right ones to pick.
            var textureRenderer = (RiveTextureRenderer)target;
            if (textureRenderer != null && textureRenderer.Renderer != null)
            {
                System.Action clickAction = () =>
                {
                    // This will replace any non-Rive materials with Rive equivalents, even if the existing materials are not Unity defaults.
                    MaterialConversionUtility.ReplaceMaterialsWithRive(textureRenderer.Renderer);
                };
                var convertButton = new Button(() => clickAction())
                {
                    text = "Replace Materials with Rive Materials"
                };
                convertButton.name = "RiveConvertMaterialsButton";
                convertButton.userData = clickAction; // allow tests to invoke without event system/panel
                convertButton.style.marginTop = 6;
                root.Add(convertButton);
            }

            return root;
        }

    }
}

