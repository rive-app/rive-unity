#if UNITY_EDITOR
using UnityEditor;
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
        public ShaderUtil.ShaderPropertyType PropertyType { get; private set; }

        /// <summary>
        /// Creates a new MaterialPropertiesAttribute.
        /// </summary>
        /// <param name="materialsSourceName">Name of the member that provides the materials.</param>
        /// <param name="propertyType">Type of shader properties to display.</param>
        public MaterialPropertiesAttribute(string materialsSourceName, ShaderUtil.ShaderPropertyType propertyType = ShaderUtil.ShaderPropertyType.TexEnv)
        {
            MaterialsSourceName = materialsSourceName;
            PropertyType = propertyType;
        }
#endif
    }


}