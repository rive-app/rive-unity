using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using Rive.Utils;

namespace Rive.EditorTools
{
    [CustomPropertyDrawer(typeof(DropdownAttribute))]
    internal class DropdownDrawer : PropertyDrawer
    {
        private PopupOrTextField dropdown;
        private SerializedProperty property;
        private object target;
        private DropdownAttribute dropdownAttr;
        private MemberInfo optionsMember;

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            this.property = property;
            dropdownAttr = attribute as DropdownAttribute;
            target = property.serializedObject.targetObject;
            var targetType = target.GetType();

            // Try to find member (field, property, or method)
            optionsMember = targetType.GetField(dropdownAttr.OptionsMemberName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

            if (optionsMember == null)
            {
                optionsMember = targetType.GetProperty(dropdownAttr.OptionsMemberName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            }

            if (optionsMember == null)
            {
                optionsMember = targetType.GetMethod(dropdownAttr.OptionsMemberName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            }

            if (optionsMember == null)
            {
                var errorContainer = new VisualElement();
                errorContainer.Add(new HelpBox($"Member {dropdownAttr.OptionsMemberName} not found", HelpBoxMessageType.Error));
                errorContainer.Add(new PropertyField(property));
                return errorContainer;
            }

            dropdown = CreateDropdown();

            // Only register for updates if TrackChanges is enabled
            if (dropdownAttr.TrackChanges)
            {
                EditorApplication.update += UpdateDropdownOptions;

                dropdown.RegisterCallback<DetachFromPanelEvent>(evt =>
                {
                    EditorApplication.update -= UpdateDropdownOptions;
                });
            }
            else
            {
                // For non-tracked dropdowns, we still want to update when the panel is attached
                dropdown.RegisterCallback<AttachToPanelEvent>(evt =>
                {
                    UpdateDropdownOptions();
                });
            }

            return dropdown;
        }

        private PopupOrTextField CreateDropdown()
        {
            var options = GetCurrentOptions();
            var currentValue = property.stringValue;

            var label = ReflectionUtils.GetPropertyLabel(property);
            var dropdown = new PopupOrTextField(options, currentValue,
                label);

            dropdown.BindProperty(property);

            var inspectorFieldAttr = fieldInfo.GetCustomAttribute<InspectorFieldAttribute>();
            if (inspectorFieldAttr != null)
            {
                dropdown.AddToClassList(StyleHelper.CLASS_FIELD);
            }

            return dropdown;
        }

        private List<string> GetCurrentOptions()
        {
            object options = null;

            switch (optionsMember)
            {
                case FieldInfo field:
                    options = field.GetValue(target);
                    break;
                case PropertyInfo prop:
                    options = prop.GetValue(target);
                    break;
                case MethodInfo method:
                    options = method.Invoke(target, null);
                    break;
            }

            if (options is IEnumerable<string> enumerable)
            {
                return enumerable.ToList();
            }

            return new List<string>();
        }

        private void UpdateDropdownOptions()
        {
            if (dropdown?.panel == null) return;

            var newOptions = GetCurrentOptions();
            if (!AreOptionsEqual(dropdown.Choices, newOptions))
            {
                dropdown.Choices = newOptions;

            }


        }

        private bool AreOptionsEqual(List<string> a, List<string> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }
    }
}