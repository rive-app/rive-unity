using UnityEngine;

namespace Rive.EditorTools
{
    /// <summary>
    /// Attribute to create a dropdown field in the inspector that can be populated from a field, property, or method.
    /// </summary>
    internal class DropdownAttribute : PropertyAttribute
    {
        /// <summary>
        /// The name of the member (field, property, or method) that provides the dropdown options.
        /// </summary>
        public string OptionsMemberName { get; private set; }

        /// <summary>
        /// The name of the method that provides the default index for the dropdown.
        /// If not specified, FallbackDefaultIndex will be used.
        /// </summary>
        public string DefaultIndexMethodName { get; private set; }

        /// <summary>
        /// The default index to use when no DefaultIndexMethodName is specified or when it returns an invalid value.
        /// </summary>
        public int FallbackDefaultIndex { get; private set; }

        /// <summary>
        /// Whether to continuously track changes to the options source using EditorApplication.update.
        /// Enable this if your options can change dynamically during editor time.
        /// </summary>
        public bool TrackChanges { get; private set; }

        /// <summary>
        /// Creates a dropdown attribute that populates its options from a specified member.
        /// </summary>
        /// <param name="optionsMemberName">Name of the field, property, or method that provides the options.</param>
        /// <param name="defaultIndexMethodName">Optional method name that provides the default selected index.</param>
        /// <param name="fallbackDefaultIndex">Default index to use when no method is specified or when it returns an invalid value.</param>
        /// <param name="trackChanges">Whether to continuously check for changes to the options.</param>
        public DropdownAttribute(
            string optionsMemberName,
            string defaultIndexMethodName = null,
            int fallbackDefaultIndex = 0,
            bool trackChanges = false)
        {
            OptionsMemberName = optionsMemberName;
            DefaultIndexMethodName = defaultIndexMethodName;
            FallbackDefaultIndex = fallbackDefaultIndex;
            TrackChanges = trackChanges;
        }
    }
}