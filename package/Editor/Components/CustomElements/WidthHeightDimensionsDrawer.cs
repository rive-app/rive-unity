using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using Rive.Utils;

namespace Rive.EditorTools
{
    [CustomPropertyDrawer(typeof(WidthHeightDimensionsAttribute))]
    internal class WidthHeightDimensionsDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var attr = attribute as WidthHeightDimensionsAttribute;
            var label = ReflectionUtils.GetPropertyLabel(property) ?? attr.Label;

            // Get tooltip from TooltipAttribute if present
            string tooltip = null;
            var tooltipAttribute = fieldInfo.GetCustomAttributes(typeof(TooltipAttribute), true);
            if (tooltipAttribute.Length > 0)
            {
                tooltip = (tooltipAttribute[0] as TooltipAttribute).tooltip;
            }

            var field = new WidthHeightDimensionsField(
                label,
                attr.WidthLabel,
                attr.HeightLabel,
                tooltip
            );

            field.BindProperty(property);
            return field;
        }


    }
}