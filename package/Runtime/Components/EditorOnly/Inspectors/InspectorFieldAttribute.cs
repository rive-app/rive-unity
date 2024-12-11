using UnityEngine;

namespace Rive.EditorTools
{
    /// <summary>
    /// Attribute to mark a field to be displayed in a custom inspector for a Rive component.
    /// </summary>
    internal class InspectorFieldAttribute : PropertyAttribute
    {
        /// <summary>
        /// The section ID this field belongs to. If null, the field will be displayed outside any section.
        /// </summary>
        public string SectionId { get; private set; }

        /// <summary>
        /// The display name shown in the inspector UI. If null, defaults to the field name.
        /// </summary>
        public string DisplayName { get; private set; }

        /// <summary>
        /// The order this field appears relative to other fields. Lower numbers appear first.
        /// </summary>
        public int Order { get; private set; }

        /// <summary>
        /// Creates a new field attribute for the inspector.
        /// </summary>
        /// <param name="sectionId">Section ID this field belongs to. If null, displays outside sections</param>
        /// <param name="displayName">Display name shown in UI. If null, uses field name</param>
        /// <param name="order">Order relative to other fields. Lower numbers appear first</param>
        public InspectorFieldAttribute(
            string sectionId = null,
            string displayName = null,
            int order = 0)
        {
            SectionId = sectionId;
            DisplayName = displayName;
            Order = order;
        }
    }
}