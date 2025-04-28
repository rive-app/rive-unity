using UnityEngine.UIElements;
using System.Collections.Generic;
using System;
using UnityEditor;
using UnityEngine;
using UnityEditor.UIElements;
using Rive.Utils;

namespace Rive.EditorTools
{
    /// <summary>
    /// A visual element that allows the user to select from a list of choices or enter a custom value.
    /// </summary>
    internal class PopupOrTextField : VisualElement, INotifyValueChanged<string>
    {
        private PopupField<string> popupField;
        private TextField textField;
        private Button switchModeButton;
        private bool isCustomValue;
        private bool isUserEditing;
        private bool isProgrammaticChange;
        private SerializedProperty boundProperty;
        private SerializedObject serializedObject;
        private UnityEngine.Object targetObject;

        private string m_Value;
        public string value
        {
            get => m_Value;
            set
            {
                if (m_Value != value)
                {
                    using (var changeEvent = ChangeEvent<string>.GetPooled(m_Value, value))
                    {
                        changeEvent.target = this;
                        SetValueWithoutNotify(value);
                        SendEvent(changeEvent);
                    }
                }
            }
        }

        public List<string> Choices
        {
            get => popupField.choices;
            set
            {
                popupField.choices = value;
                bool valueIsInChoices = value.Contains(m_Value);

                // Handle transition from Popup to Custom
                if (!isCustomValue && !valueIsInChoices)
                {
                    isCustomValue = true;
                    isProgrammaticChange = true;
                    SetValueWithoutNotify(m_Value);
                    isProgrammaticChange = false;
                }
                // Handle transition from Custom to Popup
                else if (valueIsInChoices && isCustomValue)
                {
                    isCustomValue = false;
                    isProgrammaticChange = true;
                    SetValueWithoutNotify(m_Value);
                    isProgrammaticChange = false;
                }

                UpdateVisibility();
            }
        }

        public string Label
        {
            get => popupField.label;
            set
            {
                popupField.label = value;
                textField.label = value;
            }
        }

        public PopupOrTextField() : this(new List<string>(), "") { }

        public PopupOrTextField(List<string> choices, string currentValue, string labelText = null)
        {
            popupField = new PopupField<string>(choices, 0);
            textField = new TextField();
            switchModeButton = new Button(ToggleMode)
            {
                text = "✎",
                tooltip = "Switch to text input"
            };

            SetupUI();
            SetupCallbacks();
            SetInitialState(currentValue);

            if (labelText != null)
            {
                Label = labelText;
            }

            RegisterCallback<SerializedPropertyChangeEvent>(OnSerializedPropertyChange);
        }

        private void SetupUI()
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.width = new StyleLength(Length.Percent(100));
            container.Add(popupField);
            container.Add(textField);
            container.Add(switchModeButton);

            SetupFieldStyles(popupField);
            SetupFieldStyles(textField);
            SetupButtonStyles(switchModeButton);

            textField.style.display = DisplayStyle.None;
            textField.visible = false;
            popupField.style.display = DisplayStyle.Flex;
            popupField.visible = true;

            Add(container);
        }

        private void SetupFieldStyles(VisualElement field)
        {
            // This keeps inspector positioned around the same point as other unity fields. Otherwise the popup fills the whole row, when it should stop in the middle.
            field.AddToClassList(BaseField<PropertyField>.alignedFieldUssClassName); // Same as using "unity-base-field__aligned" in UXML


            field.style.flexGrow = 1;
            field.style.marginRight = 20;
            field.style.paddingBottom = 0;
            field.style.paddingTop = 0;
            field.style.paddingLeft = 0;
            field.style.marginBottom = 0;
            field.style.marginTop = 0;
            field.style.marginLeft = 0;
        }

        private void SetupButtonStyles(Button button)
        {
            button.style.position = Position.Absolute;
            button.style.right = 0;
            button.style.width = 20;
            button.style.height = popupField.style.height;
            button.style.marginRight = 0;
            button.style.marginLeft = 0;
            button.style.marginTop = 0;
            button.style.marginBottom = 0;
            button.style.paddingBottom = 0;
            button.style.paddingTop = 0;
            button.style.paddingLeft = 0;
            button.style.paddingRight = 0;
        }


        private void SetupCallbacks()
        {
            popupField.RegisterValueChangedCallback(evt =>
            {
                if (isProgrammaticChange)
                    return; // Ignore programmatic changes to prevent recursive calls

                value = evt.newValue;
            });

            textField.RegisterCallback<FocusInEvent>(evt => isUserEditing = true);
            textField.RegisterCallback<FocusOutEvent>(evt =>
            {
                isUserEditing = false;
                UpdateVisualState();
            });

            textField.RegisterValueChangedCallback(evt =>
            {
                if (isProgrammaticChange)
                    return;

                value = evt.newValue;
            });
        }

        private void SetInitialState(string initialValue)
        {
            m_Value = initialValue;
            isCustomValue = !Choices.Contains(initialValue);

            if (!isCustomValue)
            {
                popupField.SetValueWithoutNotify(initialValue);
            }
            else
            {
                textField.SetValueWithoutNotify(initialValue);
            }

            UpdateVisibility();
        }

        private void ToggleMode()
        {
            if (targetObject != null)
            {
                Undo.RecordObject(targetObject, "Toggle PopupOrTextField Mode");
            }

            var initialValue = m_Value;
            isCustomValue = !isCustomValue;

            if (isCustomValue)
            {
                textField.SetValueWithoutNotify(m_Value);
            }
            else
            {
                if (Choices.Contains(m_Value))
                {
                    popupField.SetValueWithoutNotify(m_Value);
                }
                else if (Choices.Count > 0)
                {
                    SetValueWithoutNotify(Choices[0]);
                }
                else
                {
                    popupField.SetValueWithoutNotify(string.Empty);
                }
            }

            UpdateVisibility();

            if (targetObject != null)
            {
                EditorUtility.SetDirty(targetObject);
            }

            if (initialValue != m_Value)
            {
                using (var changeEvent = ChangeEvent<string>.GetPooled(initialValue, m_Value))
                {
                    changeEvent.target = this;
                    SendEvent(changeEvent);
                }
            }
        }

        private void UpdateVisualState()
        {
            bool valueInChoices = Choices.Contains(m_Value);

            if (valueInChoices)
            {
                if (!isCustomValue)
                {
                    // We make sure the popupField reflects the current value
                    popupField.SetValueWithoutNotify(m_Value);
                }

                if (isCustomValue && !isUserEditing)
                {
                    isCustomValue = false;
                    UpdateVisibility();
                }
            }
            else
            {
                textField.SetValueWithoutNotify(m_Value);
                if (!isCustomValue)
                {
                    isCustomValue = true;
                    UpdateVisibility();
                }
            }
        }

        private void UpdateVisibility()
        {
            if (isCustomValue)
            {
                popupField.style.display = DisplayStyle.None;
                popupField.visible = false;

                textField.style.display = DisplayStyle.Flex;
                textField.visible = true;

                switchModeButton.text = "▼";
                switchModeButton.tooltip = "Switch to dropdown";
            }
            else
            {
                textField.style.display = DisplayStyle.None;
                textField.visible = false;

                popupField.style.display = DisplayStyle.Flex;
                popupField.visible = true;

                switchModeButton.text = "✎";
                switchModeButton.tooltip = "Switch to text input";
            }

            this.MarkDirtyRepaint();
        }

        public void SetValueWithoutNotify(string newValue)
        {
            if (serializedObject != null)
            {
                serializedObject.Update();
            }

            m_Value = newValue;

            // Force check if value is in choices and update mode accordingly
            bool valueInChoices = Choices.Contains(newValue);

            if (valueInChoices && (!isUserEditing || isProgrammaticChange))
            {
                isCustomValue = false;
                isProgrammaticChange = true;
                popupField.SetValueWithoutNotify(newValue);
                isProgrammaticChange = false;
            }
            else
            {
                if (!valueInChoices)
                {
                    isCustomValue = true;
                }
                isProgrammaticChange = true;
                textField.SetValueWithoutNotify(newValue);
                isProgrammaticChange = false;
            }

            UpdateVisibility();

            if (boundProperty != null)
            {
                boundProperty.stringValue = newValue;
                serializedObject.ApplyModifiedProperties();
            }
        }

        private void OnSerializedPropertyChange(SerializedPropertyChangeEvent evt)
        {
            if (evt.changedProperty == boundProperty)
            {
                isProgrammaticChange = true;
                SetValueWithoutNotify(boundProperty.stringValue);
                isProgrammaticChange = false;
            }
        }

        public void BindProperty(SerializedProperty property)
        {
            UnbindProperty();

            if (property != null && property.propertyType == SerializedPropertyType.String)
            {
                boundProperty = property;
                serializedObject = property.serializedObject;
                targetObject = serializedObject.targetObject;
                SetInitialState(property.stringValue);
                EditorApplication.update += UpdateFromSerializedProperty;
            }
            else
            {
                DebugLogger.Instance.LogError("PopupOrTextField: Attempted to bind to a null or non-string property.");
            }

            this.RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            this.RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            if (serializedObject != null && boundProperty != null)
            {
                serializedObject.Update();
                SetInitialState(boundProperty.stringValue);
            }
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            UnbindProperty();
        }

        private void UpdateFromSerializedProperty()
        {
            try
            {
                if (serializedObject == null || boundProperty == null)
                {
                    UnbindProperty();
                    return;
                }

                // Check if the serializedObject is still valid
                if (serializedObject.targetObject == null)
                {
                    UnbindProperty();
                    return;
                }

                serializedObject.Update();

                // Double-check everything is still valid after the update
                if (boundProperty == null || boundProperty.serializedObject == null || boundProperty.serializedObject.targetObject == null)
                {
                    UnbindProperty();
                    return;
                }

                if (boundProperty.propertyType == SerializedPropertyType.String)
                {
                    string newValue = boundProperty.stringValue;
                    if (m_Value != newValue && !isUserEditing)
                    {
                        SetValueWithoutNotify(newValue);
                    }
                }
                else
                {
                    DebugLogger.Instance.LogWarning($"PopupOrTextField: Bound property is not a string. Property path: {boundProperty.propertyPath}");
                }
            }
            catch (Exception)
            {
                UnbindProperty();
            }
        }

        private void UnbindProperty()
        {
            boundProperty = null;
            serializedObject = null;
            targetObject = null;
            EditorApplication.update -= UpdateFromSerializedProperty;
        }
    }
}