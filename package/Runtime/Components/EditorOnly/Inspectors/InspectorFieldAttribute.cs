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
        /// Optional URL to open when the help/info button is clicked in the inspector.
        /// </summary>
        public string HelpUrl { get; private set; }

        /// <summary>
        /// Convenience flag to check if a help URL was supplied.
        /// </summary>
        public bool HasHelpUrl => !string.IsNullOrEmpty(HelpUrl);

        /// <summary>
        /// Creates a new field attribute for the inspector.
        /// </summary>
        /// <param name="sectionId">Section ID this field belongs to. If null, displays outside sections</param>
        /// <param name="displayName">Display name shown in UI. If null, uses field name</param>
        /// <param name="order">Order relative to other fields. Lower numbers appear first</param>
        /// <param name="helpUrl">Optional URL opened when the info button is clicked</param>
        public InspectorFieldAttribute(
            string sectionId = null,
            string displayName = null,
            int order = 0,
            string helpUrl = null)
        {
            SectionId = sectionId;
            DisplayName = displayName;
            Order = order;
            HelpUrl = helpUrl;
        }
    }
}