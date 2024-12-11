using UnityEngine;

namespace Rive.EditorTools
{
    /// <summary>
    /// Attribute to invoke a callback method when a property value changes. Only works with custom editors that inherit from RiveBaseEditor.
    /// </summary>
    internal class OnValueChangedAttribute : PropertyAttribute
    {
        /// <summary>
        /// The name of the callback method to invoke when the value changes.
        /// </summary>
        public string CallbackName { get; private set; }

        /// <summary>
        /// Whether to invoke the callback during the initial setup of the property drawer.
        /// </summary>
        public bool InvokeOnInitialization { get; private set; }

        /// <summary>
        /// Attribute to invoke a callback method when a property value changes.
        /// </summary>
        /// <param name="callbackName">The name of the method to call when the value changes.</param>
        /// <param name="invokeOnInitialization">Whether to invoke the callback during initialization (default: false). This is useful for setting up initial values.</param>
        public OnValueChangedAttribute(string callbackName, bool invokeOnInitialization = false)
        {
            CallbackName = callbackName;
            InvokeOnInitialization = invokeOnInitialization;
        }
    }
}