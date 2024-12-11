using System;
using System.Reflection;
using Rive.EditorTools;
using UnityEditor;

namespace Rive.Utils
{
    internal static class ReflectionUtils
    {
        private const BindingFlags DefaultBindingFlags =
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        public static bool TryGetBoolValue(object target, string memberName, out bool value)
        {
            value = false;
            if (target == null || string.IsNullOrEmpty(memberName)) return false;

            try
            {
                var type = target.GetType();

                var field = type.GetField(memberName, DefaultBindingFlags);
                if (field != null)
                {
                    value = (bool)field.GetValue(target);
                    return true;
                }

                var prop = type.GetProperty(memberName, DefaultBindingFlags);
                if (prop != null)
                {
                    value = (bool)prop.GetValue(target);
                    return true;
                }

                var method = type.GetMethod(memberName, DefaultBindingFlags);
                if (method != null)
                {
                    value = (bool)method.Invoke(target, null);
                    return true;
                }

                return false;
            }
            catch (Exception e)
            {
                DebugLogger.Instance.LogError($"Error getting bool value for member '{memberName}': {e.Message}");
                return false;
            }
        }

        public static bool TryGetValue<T>(object target, string memberName, out T value)
        {
            value = default;
            if (target == null || string.IsNullOrEmpty(memberName)) return false;

            try
            {
                var type = target.GetType();

                var field = type.GetField(memberName, DefaultBindingFlags);
                if (field != null)
                {
                    value = (T)field.GetValue(target);
                    return true;
                }

                var prop = type.GetProperty(memberName, DefaultBindingFlags);
                if (prop != null)
                {
                    value = (T)prop.GetValue(target);
                    return true;
                }

                var method = type.GetMethod(memberName, DefaultBindingFlags);
                if (method != null)
                {
                    value = (T)method.Invoke(target, null);
                    return true;
                }

                return false;
            }
            catch (Exception e)
            {
                DebugLogger.Instance.LogError($"Error getting value of type {typeof(T)} for member '{memberName}': {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get the display name for a serialized property. This accounts for a custom label being set via an InspectorFieldAttribute.
        /// </summary>
        /// <param name="property"> The property to get the label for. </param>
        /// <returns> The display name for the property. </returns>
        public static string GetPropertyLabel(SerializedProperty property)
        {
            if (property == null) return string.Empty;

            try
            {
                var target = property.serializedObject.targetObject;
                var fieldInfo = target.GetType().GetField(property.name, DefaultBindingFlags);
                var inspectorAttr = fieldInfo?.GetCustomAttribute<InspectorFieldAttribute>();

                return inspectorAttr?.DisplayName ?? ObjectNames.NicifyVariableName(property.name);
            }
            catch (Exception e)
            {
                DebugLogger.Instance.LogError($"Error getting label for property '{property.name}': {e.Message}");
                return ObjectNames.NicifyVariableName(property.name);

            }
        }
    }
}