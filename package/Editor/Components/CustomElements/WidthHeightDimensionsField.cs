using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UnityEditor;

namespace Rive.EditorTools
{
    /// <summary>
    /// A field for editing a Vector2Int representing width and height.
    /// </summary>
    internal class WidthHeightDimensionsField : VisualElement
    {

        public IntegerField WidthField { get; private set; }
        public IntegerField HeightField { get; private set; }

        public WidthHeightDimensionsField(string label, string widthLabel = "Width", string heightLabel = "Height", string tooltip = null)
        {
            var foldout = new Foldout
            {
                text = label,
                tooltip = tooltip,
                value = true // Start expanded
            };
            Add(foldout);

            var container = new VisualElement();
            foldout.Add(container);

            WidthField = new IntegerField(widthLabel)
            {
                style = { marginTop = 4 }
            };
            WidthField.AddToClassList(BaseField<int>.alignedFieldUssClassName);
            container.Add(WidthField);

            HeightField = new IntegerField(heightLabel)
            {
                style = { marginTop = 4 }
            };
            HeightField.AddToClassList(BaseField<int>.alignedFieldUssClassName);
            container.Add(HeightField);
        }

        public void BindProperty(SerializedProperty property)
        {
            WidthField.BindProperty(property.FindPropertyRelative("x"));
            HeightField.BindProperty(property.FindPropertyRelative("y"));
        }
    }
}
