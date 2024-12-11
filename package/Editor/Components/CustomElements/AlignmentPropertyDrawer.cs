using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

#if !UNITY_2022_1_OR_NEWER
using UnityEditor.UIElements; // Required for Unity 2021
#endif
using UnityEngine.UIElements;

namespace Rive.EditorTools
{
    /// <summary>
    /// We don't directly use the property drawer for the Alignment class, but we keep it as a way to create the dropdown field in the inspector for RiveBaseEditor.
    /// We do this because the RiveBaseEditor class supports a bunch of the custom attributes we've created and having multiple drawers for the same class can cause conflicts so we do it all in the RiveBaseEditor class.
    /// </summary>
    //[CustomPropertyDrawer(typeof(Alignment))]
    internal class AlignmentPropertyDrawer : PropertyDrawer
    {
        private static readonly (string display, Alignment value)[] OPTIONS = new[]
        {
            ("Top Left", Alignment.TopLeft),
            ("Top Center", Alignment.TopCenter),
            ("Top Right", Alignment.TopRight),
            ("Center Left", Alignment.CenterLeft),
            ("Center", Alignment.Center),
            ("Center Right", Alignment.CenterRight),
            ("Bottom Left", Alignment.BottomLeft),
            ("Bottom Center", Alignment.BottomCenter),
            ("Bottom Right", Alignment.BottomRight)
        };



        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement();

            var xProp = property.FindPropertyRelative(Alignment.BindingPath_Xfield);
            var yProp = property.FindPropertyRelative(Alignment.BindingPath_Yfield);

            // Default to Center if we can't get the values
            var centerIndex = Array.FindIndex(OPTIONS, o => o.value.Equals(Alignment.Center));
            var currentIndex = centerIndex;

            if (xProp != null && yProp != null)
            {
                var currentAlignment = new Alignment(xProp.floatValue, yProp.floatValue);
                currentIndex = Array.FindIndex(OPTIONS, o => o.value.Equals(currentAlignment));
                if (currentIndex < 0) currentIndex = centerIndex;
            }

            var choices = OPTIONS.Select(o => o.display).ToList();

            var dropdown = new PopupField<string>(
                property.displayName,
                choices,
                currentIndex
            );

            dropdown.RegisterValueChangedCallback(evt =>
            {
                var index = choices.IndexOf(evt.newValue);
                if (index >= 0 && xProp != null && yProp != null)
                {
                    var selectedAlignment = OPTIONS[index].value;
                    xProp.floatValue = selectedAlignment.X;
                    yProp.floatValue = selectedAlignment.Y;
                    property.serializedObject.ApplyModifiedProperties();
                }
            });

            dropdown.AddToClassList(StyleHelper.CLASS_FIELD);

            // This ensures that the dropdown is aligned with other fields in the inspector
            dropdown.AddToClassList(BaseField<UnityEditor.UIElements.PropertyField>.alignedFieldUssClassName);
            container.Add(dropdown);
            return container;
        }


    }
}