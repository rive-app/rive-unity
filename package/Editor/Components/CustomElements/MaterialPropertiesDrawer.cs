using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Collections.Generic;
using System.Linq;
using Rive.Utils;

namespace Rive.EditorTools
{
    /// <summary>
    /// Draws a list of properties from a material on a component as a dropdown. This is useful if you want to display a list of properties from a material on a component in the inspector.
    /// </summary>
    [CustomPropertyDrawer(typeof(MaterialPropertiesAttribute))]
    internal class MaterialPropertiesDrawer : PropertyDrawer
    {
        private VisualElement m_root;
        private List<string> m_availablePropertyNames = new List<string>();
        private SerializedObject m_serializedObject;

        private Material[] GetMaterialsFromSource(object target, string sourceName)
        {
            // Try to get materials directly
            if (ReflectionUtils.TryGetValue<Material[]>(target, sourceName, out var materials))
            {
                return materials;
            }

            // If we got a renderer instead, get its materials
            if (ReflectionUtils.TryGetValue<UnityEngine.Renderer>(target, sourceName, out var renderer))
            {
                return renderer?.sharedMaterials;
            }

            return null;
        }

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var attr = attribute as MaterialPropertiesAttribute;
            m_root = new VisualElement();
            m_serializedObject = property.serializedObject;

            var target = property.serializedObject.targetObject;
            var materials = GetMaterialsFromSource(target, attr.MaterialsSourceName);

            if (materials == null)
            {
                m_root.Add(new HelpBox($"Could not find materials source: {attr.MaterialsSourceName}", HelpBoxMessageType.Error));
                return m_root;
            }

            UpdateUI(property, materials, attr.PropertyType);
            return m_root;
        }

        private void UpdateUI(SerializedProperty property, Material[] materials, ShaderUtil.ShaderPropertyType propertyType)
        {
            m_root.Clear();
            UpdateAvailablePropertyNames(materials, propertyType);

            var keysProperty = property.FindPropertyRelative(SerializedDictionary<int, Components.RiveTextureRenderer.PropertyNameListHolder>.BindingPath_Keys);
            var valuesProperty = property.FindPropertyRelative(SerializedDictionary<int, Components.RiveTextureRenderer.PropertyNameListHolder>.BindingPath_Values);

            // Pre-create property holders for all materials
            EnsurePropertyHoldersExist(keysProperty, valuesProperty, materials.Length);

            for (int i = 0; i < materials.Length; i++)
            {
                var material = materials[i];
                if (material == null) continue;

                var materialFoldout = new Foldout { text = $"Material {i}: {material.name}" };
                m_root.Add(materialFoldout);

                var propertyListHolder = FindPropertyListHolder(keysProperty, valuesProperty, i);
                if (propertyListHolder != null)
                {
                    var propertyList = propertyListHolder.FindPropertyRelative(Components.RiveTextureRenderer.PropertyNameListHolder.BindingPath_PropertyNames);
                    if (propertyList != null && propertyList.serializedObject != null)
                    {
                        var listView = CreateListView(propertyList);
                        materialFoldout.Add(listView);
                    }
                }
            }

            // Apply any changes made during setup
            property.serializedObject.ApplyModifiedProperties();
        }

        private void EnsurePropertyHoldersExist(SerializedProperty keysProperty, SerializedProperty valuesProperty, int materialCount)
        {
            // First, create a list of existing material indices
            var existingIndices = new HashSet<int>();
            for (int i = 0; i < keysProperty.arraySize; i++)
            {
                existingIndices.Add(keysProperty.GetArrayElementAtIndex(i).intValue);
            }

            // Create missing property holders
            for (int i = 0; i < materialCount; i++)
            {
                if (!existingIndices.Contains(i))
                {
                    keysProperty.InsertArrayElementAtIndex(keysProperty.arraySize);
                    keysProperty.GetArrayElementAtIndex(keysProperty.arraySize - 1).intValue = i;

                    valuesProperty.InsertArrayElementAtIndex(valuesProperty.arraySize);
                }
            }
        }

        private SerializedProperty FindPropertyListHolder(SerializedProperty keysProperty, SerializedProperty valuesProperty, int materialIndex)
        {
            for (int i = 0; i < keysProperty.arraySize; i++)
            {
                if (keysProperty.GetArrayElementAtIndex(i).intValue == materialIndex)
                {
                    return valuesProperty.GetArrayElementAtIndex(i);
                }
            }
            return null;
        }

        private ListView CreateListView(SerializedProperty propertyList)
        {
            var listView = new ListView()
            {
                reorderable = true,
                showAddRemoveFooter = true,
                showBorder = true,
                showFoldoutHeader = false,
                showBoundCollectionSize = false,
                virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight
            };

            // Delay binding until the next frame to ensure proper initialization
            m_root.schedule.Execute(() =>
            {
                listView.bindingPath = propertyList.propertyPath;
                listView.BindProperty(propertyList.serializedObject);
            });

            listView.makeItem = () => new PopupOrTextField(m_availablePropertyNames, "");
            listView.bindItem = (element, index) =>
            {
                var popupOrTextField = element as PopupOrTextField;
                popupOrTextField.Choices = m_availablePropertyNames;

                if (propertyList != null && propertyList.serializedObject != null)
                {
                    var itemProperty = propertyList.GetArrayElementAtIndex(index);
                    popupOrTextField.BindProperty(itemProperty);
                }
            };

            listView.itemsAdded += (indexes) =>
            {
                if (propertyList != null && propertyList.serializedObject != null)
                {
                    foreach (int index in indexes)
                    {
                        var itemProperty = propertyList.GetArrayElementAtIndex(index);
                        if (string.IsNullOrEmpty(itemProperty.stringValue))
                        {
                            itemProperty.stringValue = m_availablePropertyNames.FirstOrDefault() ?? "";
                            propertyList.serializedObject.ApplyModifiedProperties();
                        }
                    }
                }
                listView.Rebuild();
            };

            return listView;
        }

        private void UpdateAvailablePropertyNames(Material[] materials, ShaderUtil.ShaderPropertyType propertyType)
        {
            m_availablePropertyNames.Clear();

            foreach (var material in materials)
            {
                if (material != null)
                {
                    var shader = material.shader;
                    for (int i = 0; i < ShaderUtil.GetPropertyCount(shader); i++)
                    {
                        if (ShaderUtil.GetPropertyType(shader, i) == propertyType)
                        {
                            string propertyName = ShaderUtil.GetPropertyName(shader, i);
                            if (!m_availablePropertyNames.Contains(propertyName))
                            {
                                m_availablePropertyNames.Add(propertyName);
                            }
                        }
                    }
                }
            }
        }



    }
}