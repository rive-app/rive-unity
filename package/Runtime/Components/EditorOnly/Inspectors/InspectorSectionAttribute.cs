using System;

namespace Rive.EditorTools
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    /// <summary>
    /// Attribute to mark a section in a custom inspector for a Rive component.
    /// </summary>
    internal class InspectorSectionAttribute : Attribute
    {
        /// <summary>
        /// The unique identifier for this section.
        /// </summary>
        public string Id { get; private set; }

        /// <summary>
        /// The display name shown in the inspector UI. If not specified, defaults to the Id.
        /// </summary>
        public string DisplayName { get; private set; }

        /// <summary>
        /// The order this section appears relative to other sections. Lower numbers appear first.
        /// </summary>
        public int Order { get; private set; }

        /// <summary>
        /// The style used to display this section - either as a header or foldout.
        /// </summary>
        public SectionStyle Style { get; private set; }

        /// <summary>
        /// Whether a foldout section should start expanded. Only applies when Style is Foldout.
        /// </summary>
        public bool StartExpanded { get; private set; }

        /// <summary>
        /// Creates a new section in the inspector.
        /// </summary>
        /// <param name="id">Unique identifier for the section</param>
        /// <param name="displayName">Display name shown in UI. Defaults to id if null</param>
        /// <param name="order">Order relative to other sections. Lower numbers appear first</param>
        /// <param name="style">How to display the section - as a header or foldout</param>
        /// <param name="startExpanded">Whether a foldout section starts expanded</param>
        public InspectorSectionAttribute(
            string id,
            string displayName = null,
            int order = 0,
            SectionStyle style = SectionStyle.Header,
            bool startExpanded = false)
        {
            Id = id;
            DisplayName = displayName ?? id;
            Order = order;
            Style = style;
            StartExpanded = startExpanded;
        }
    }

    /// <summary>
    /// Defines how a section should be displayed in the inspector.
    /// </summary>
    public enum SectionStyle
    {
        /// <summary>
        /// Shows a header label above the section content.
        /// </summary>
        Header = 0,

        /// <summary>
        /// Shows the section as a foldout that can be expanded/collapsed.
        /// </summary>
        Foldout = 1
    }
}