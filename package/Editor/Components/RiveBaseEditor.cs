using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Rive.Utils;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rive.EditorTools
{
#if UNITY_EDITOR
    /// <summary>
    /// Base class for custom inspectors for Rive components.
    /// </summary>
    internal class RiveBaseEditor : Editor
    {
        protected VisualElement rootElement;

        private GameObject m_gameObject;

        private Dictionary<string, VisualElement> sections = new Dictionary<string, VisualElement>();

        protected virtual void OnEnable()
        {
            if (target is MonoBehaviour)
            {
                m_gameObject = (target as MonoBehaviour).gameObject;
            }


            // Using Editor.update, queue a repaint for the next frame to hide components
            // Hiding immediately causes issues with the inspector layout
            EditorApplication.update += FirstRepaint;

        }

        private void FirstRepaint()
        {
            HandleHideComponents();

            EditorApplication.update -= FirstRepaint;

        }


        private void HandleHideComponents()
        {
            var hideComponentsAttrs = target.GetType().GetCustomAttributes<HideComponentsAttribute>();
            var targetComponent = target as MonoBehaviour;

            if (targetComponent != null)
            {
                foreach (var attr in hideComponentsAttrs)
                {
                    CustomInspectorUtils.HideNonInteractiveComponents(
                        targetComponent,
                        new List<Type>(attr.ComponentTypes),
                        this,
                        attr.HideFlags
                    );
                }

            }
        }



        public override VisualElement CreateInspectorGUI()
        {
            rootElement = new VisualElement();
            rootElement.styleSheets.Add(StyleHelper.StyleSheet);
            rootElement.AddToClassList("rive-inspector");

            var serializedFields = GetSerializedFields();

            // Get all fields with InspectorFieldAttribute
            var attributeFields = serializedFields
                .Where(f => f.GetCustomAttribute<InspectorFieldAttribute>() != null)
                .OrderBy(f => f.GetCustomAttribute<InspectorFieldAttribute>().Order);

            // Split into fields with and without sections
            var sectionFields = attributeFields.Where(f =>
                !string.IsNullOrEmpty(f.GetCustomAttribute<InspectorFieldAttribute>().SectionId));
            var nonSectionFields = attributeFields.Where(f =>
                string.IsNullOrEmpty(f.GetCustomAttribute<InspectorFieldAttribute>().SectionId));

            // Get fields without any attributes
            var plainFields = serializedFields
                .Except(attributeFields);

            // Process non-sectioned fields first (both plain and attributed)
            foreach (var field in plainFields.Concat(nonSectionFields))
            {
                var attr = field.GetCustomAttribute<InspectorFieldAttribute>();
                CreateFieldElement(field, attr, rootElement);
            }

            // Get sections that have fields
            var usedSectionIds = sectionFields
                .Select(f => f.GetCustomAttribute<InspectorFieldAttribute>().SectionId)
                .Distinct()
                .ToHashSet();

            CreateSections(usedSectionIds);

            foreach (var field in sectionFields)
            {
                var attr = field.GetCustomAttribute<InspectorFieldAttribute>();
                var container = sections[attr.SectionId];
                CreateFieldElement(field, attr, container);
            }


            return rootElement;
        }


        private HashSet<FieldInfo> GetSerializedFields()
        {
            var fields = new HashSet<FieldInfo>();
            var currentType = target.GetType();

            // Walk up the inheritance chain until we hit MonoBehaviour
            while (currentType != typeof(MonoBehaviour) && currentType != null)
            {
                var typeFields = currentType
                    .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Where(f =>
                        (f.GetCustomAttribute<SerializeField>() != null || f.IsPublic) &&
                        f.GetCustomAttribute<HideInInspector>() == null &&
                        IsUnitySerializable(f.FieldType)); // check for Unity-serializable types

                foreach (var field in typeFields)
                {
                    fields.Add(field);
                }

                currentType = currentType.BaseType;
            }

            return fields;
        }

        // Helper method to check if a type is serializable by Unity. We do this to avoid types like Actions, Funcs, etc not showing up in the inspector but still taking up space.
        private bool IsUnitySerializable(Type type)
        {
            if (type == null) return false;

            if (Attribute.IsDefined(type, typeof(SerializableAttribute)))
                return true;

            if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal))
                return true;

            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                return true;

            if (type.IsEnum)
                return true;

            if (type.IsValueType && !type.IsPrimitive)
                return true;

            if (typeof(UnityEngine.Events.UnityEventBase).IsAssignableFrom(type))
                return true;

            if (type.IsArray)
                return IsUnitySerializable(type.GetElementType());
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                return IsUnitySerializable(type.GetGenericArguments()[0]);


            return false;
        }

        private void CreateSections(HashSet<string> usedSectionIds)
        {
            var sectionAttrs = target.GetType()
                .GetCustomAttributes<InspectorSectionAttribute>()
                .OrderBy(s => s.Order);

            foreach (var attr in sectionAttrs)
            {
                // Only create sections that have fields
                if (!usedSectionIds.Contains(attr.Id))
                {
                    continue;
                }

                VisualElement section;
                switch (attr.Style)
                {
                    case SectionStyle.Foldout:
                        var foldout = new Foldout { text = attr.DisplayName };
                        foldout.viewDataKey = $"RiveFoldout_{target.GetType().Name}_{attr.Id}";
                        // The initial value will be used only if there's no saved state
                        foldout.value = attr.StartExpanded;
                        section = foldout;
                        break;

                    case SectionStyle.Header:
                    default:
                        section = new VisualElement();
                        if (!string.IsNullOrEmpty(attr.DisplayName))
                        {
                            var label = new Label(attr.DisplayName);
                            label.AddToClassList(StyleHelper.CLASS_SECTION_LABEL);
                            section.Add(label);
                        }
                        break;
                }

                section.AddToClassList(StyleHelper.CLASS_SECTION);
                sections[attr.Id] = section;
                rootElement.Add(section);
            }
        }

        public static VisualElement GetVisualElementForField(FieldInfo field, SerializedProperty property, string label = null)
        {
            VisualElement element;

            // We do this to show the alignment dropdown because some versions of Unity seems to have issues with the default PropertyDrawer (e.g Unity 2022.3.10)
            // In those versions, the default PropertyDrawer doesn't show the dropdown, but rather the X and Y fields.
            // It's possible that this is a bug in Unity, but this is a workaround for now.
            if (field.FieldType == typeof(Alignment))
            {
                var alignmentDrawer = new AlignmentPropertyDrawer();
                element = alignmentDrawer.CreatePropertyGUI(property);

            }
            else
            {
                var propertyField = new PropertyField
                {
                    bindingPath = field.Name
                };

                if (label != null)
                {
                    propertyField.label = label;
                }

                element = propertyField;
            }



            return element;

        }


        private void CreateFieldElement(FieldInfo field, InspectorFieldAttribute attr, VisualElement container)
        {
            string displayName = attr?.DisplayName ?? ObjectNames.NicifyVariableName(field.Name);
            var property = serializedObject.FindProperty(field.Name);

            VisualElement element = GetVisualElementForField(field, property, displayName);
            string uniqueId = $"field-{target.GetInstanceID()}-{target.GetType().Name}-{field.Name}";
            element.name = uniqueId;

            HandleValueChangedIfNeeded(field, element);

            VisualElement fieldRoot = element;
            if (attr != null && attr.HasHelpUrl)
            {
                var fieldContainer = new VisualElement();
                fieldContainer.AddToClassList(StyleHelper.CLASS_FIELD_CONTAINER);
                fieldContainer.style.flexDirection = FlexDirection.Row;
                fieldContainer.style.alignItems = Align.Center;

                element.AddToClassList(StyleHelper.CLASS_FIELD_CONTENT);
                fieldContainer.Add(element);
                fieldContainer.Add(CreateHelpButton(attr.HelpUrl, displayName));
                fieldRoot = fieldContainer;
            }

            HandleConditionalVisibilityIfNeeded(field, fieldRoot, property);

            element.Bind(serializedObject);
            fieldRoot.AddToClassList(StyleHelper.CLASS_FIELD);
            container.Add(fieldRoot);
        }

        private void HandleValueChangedIfNeeded(FieldInfo field, VisualElement element)
        {
            var onValueChangedAttr = field.GetCustomAttribute<OnValueChangedAttribute>();
            if (onValueChangedAttr != null)
            {
                var methodInfo = target.GetType().GetMethod(onValueChangedAttr.CallbackName,
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

                if (methodInfo != null)
                {
                    bool isInitializing = true;
                    element.RegisterCallback<AttachToPanelEvent>(evt =>
                    {
                        element.schedule.Execute(() =>
                        {
                            isInitializing = false;
                        });
                    });


                    if (element is PropertyField propertyField)
                    {
                        propertyField.RegisterValueChangeCallback(evt =>
                        {
                            if (!isInitializing || onValueChangedAttr.InvokeOnInitialization)
                            {
                                methodInfo.Invoke(target, null);
                            }
                        });
                        return;
                    }

                    // The alignment dropdown is a PopupField<string>
                    // Get the PopupField<string> from the element. It's possible that the passed in element might be a container so we might need to find the PopupField<string> in the children.
                    var popupField = element.Q<PopupField<string>>();

                    if (element != null)
                    {
                        popupField.RegisterValueChangedCallback(evt =>
                        {
                            if (!isInitializing || onValueChangedAttr.InvokeOnInitialization)
                            {
                                methodInfo.Invoke(target, null);
                            }
                        });
                    }
                }
            }
        }

        private void HandleConditionalVisibilityIfNeeded(FieldInfo field, VisualElement element, SerializedProperty property)
        {
            var showIfAttr = field.GetCustomAttribute<ShowIfAttribute>();
            var hideIfAttr = field.GetCustomAttribute<HideIfAttribute>();

            if (showIfAttr == null && hideIfAttr == null) return;
            string conditionName = showIfAttr?.ConditionName ?? hideIfAttr?.ConditionName;
            bool isHideIf = hideIfAttr != null;

            void UpdateVisibility()
            {
                if (property.serializedObject == null || property.serializedObject.targetObject == null)
                {
                    return;
                }

                var target = property.serializedObject.targetObject;
                if (ReflectionUtils.TryGetBoolValue(target, conditionName, out bool condition))
                {
                    element.style.display = (condition != isHideIf) ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }

            element.RegisterCallback<AttachToPanelEvent>(evt =>
            {
                property.serializedObject.Update();
                UpdateVisibility();
            });

            UpdateVisibility();
            // Update visibility whenever the inspector updates
            scheduledUpdate = element.schedule.Execute(() =>
            {
                if (property.serializedObject == null || property.serializedObject.targetObject == null)
                {
                    // Stop scheduling future updates
                    scheduledUpdate?.Pause();
                    return;
                }

                property.serializedObject.Update();
                UpdateVisibility();
            }).Every(100);
        }

        private IVisualElementScheduledItem scheduledUpdate;

        private Button CreateHelpButton(string helpUrl, string displayName)
        {
            var button = new Button(() =>
            {
                if (!string.IsNullOrEmpty(helpUrl))
                {
                    Application.OpenURL(helpUrl);
                }
            });

            button.tooltip = "Open documentation for this field";
            button.focusable = false;
            button.AddToClassList(StyleHelper.CLASS_FIELD_HELP_BUTTON);

            var iconContent = EditorGUIUtility.IconContent("_Help");
            if (iconContent?.image != null)
            {
                var icon = new Image
                {
                    image = iconContent.image,
                    scaleMode = ScaleMode.ScaleToFit
                };
                button.Add(icon);
            }
            else
            {
                button.text = "?";
            }

            return button;
        }

        private void OnDestroy()
        {
            if (Application.isPlaying) return;



            bool componentRemoved = m_gameObject != null && m_gameObject.GetComponent(target.GetType()) == null;


            //If the component was removed but not the gameobject, let's destroy the required components it added that are hidden
            // If they're not hidden, then the user can remove them manually.
            if (componentRemoved)
            {
                var hideComponentsAttrs = target.GetType().GetCustomAttributes<HideComponentsAttribute>();
                foreach (var attr in hideComponentsAttrs)
                {
                    CustomInspectorUtils.DestroyRequiredHiddenComponents(
                        m_gameObject,
                        target.GetType(),
                        component => (component.hideFlags & attr.HideFlags) != 0
                    );
                }
            }
        }


    }
#endif
}