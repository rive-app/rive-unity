using UnityEngine;

namespace Rive.EditorTools
{
    /// <summary>
    /// Attribute to specify that a property is a width/height dimensions pair for a Vector2.
    /// </summary>
    internal class WidthHeightDimensionsAttribute : PropertyAttribute
    {
        public string Label { get; private set; }
        public string WidthLabel { get; private set; }
        public string HeightLabel { get; private set; }

        public WidthHeightDimensionsAttribute(
            string label,
            string widthLabel = "Width",
            string heightLabel = "Height")
        {
            Label = label;
            WidthLabel = widthLabel;
            HeightLabel = heightLabel;
        }
    }
}