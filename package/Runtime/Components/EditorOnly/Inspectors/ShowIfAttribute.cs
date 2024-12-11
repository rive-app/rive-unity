using UnityEngine;

namespace Rive.EditorTools
{
    /// <summary>
    /// Attribute to show a property in the inspector if a condition is met. Only works with custom inspectors that inherit from RiveBaseEditor.
    /// </summary>
    internal class ShowIfAttribute : PropertyAttribute
    {
        public string ConditionName { get; private set; }

        /// <summary>
        /// Attribute to show a property in the inspector if a condition is met.
        /// </summary>
        /// <param name="conditionName"> The name of the condition method to check. This can also be a property name.</param>
        public ShowIfAttribute(string conditionName)
        {
            ConditionName = conditionName;
        }
    }
}