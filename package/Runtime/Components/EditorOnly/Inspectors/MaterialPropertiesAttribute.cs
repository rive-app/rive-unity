#if UNITY_EDITOR
using UnityEditor;
#if UNITY_6000_3_OR_NEWER
using MaterialShaderPropertyType = UnityEngine.Rendering.ShaderPropertyType;
#else
using MaterialShaderPropertyType = UnityEditor.ShaderUtil.ShaderPropertyType;
#endif
#endif
using UnityEngine;

namespace Rive.EditorTools
{
    /// <summary>
    /// Attribute for drawing material property lists for each material.
    /// </summary>
    internal class MaterialPropertiesAttribute : PropertyAttribute
    {
#if UNITY_EDITOR
        /// <summary>
        /// Name of the member that provides the materials (can be a field, property, or method).
        /// </summary>
        public string MaterialsSourceName { get; private set; }

        /// <summary>
        /// The type of shader properties to display (e.g., TexEnv, Float, Color).
        /// </summary>
        public MaterialShaderPropertyType PropertyType { get; private set; }

        /// <summary>
        /// Creates a new MaterialPropertiesAttribute.
        /// </summary>
        /// <param name="materialsSourceName">Name of the member that provides the materials.</param>
        /// <param name="propertyType">Type of shader properties to display.</param>
        public MaterialPropertiesAttribute(
            string materialsSourceName,
            MaterialShaderPropertyType propertyType =
#if UNITY_6000_3_OR_NEWER
            MaterialShaderPropertyType.Texture
#else
            MaterialShaderPropertyType.TexEnv
#endif
        )
        {
            MaterialsSourceName = materialsSourceName;
            PropertyType = propertyType;
        }
#endif
    }


}