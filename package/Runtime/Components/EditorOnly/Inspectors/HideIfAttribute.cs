using UnityEngine;

namespace Rive.EditorTools
{
    /// <summary>
    /// Attribute to hide a field in the inspector if a condition is met. Only works with custom inspectors that inherit from RiveBaseEditor.
    /// </summary>
    internal class HideIfAttribute : PropertyAttribute
    {
        public string ConditionName { get; private set; }

        /// <summary>
        /// Attribute to hide a field in the inspector if a condition is met.
        /// </summary>
        /// <param name="conditionName"> The name of the condition method to check. This can also be a property name.</param>
        public HideIfAttribute(string conditionName)
        {
            ConditionName = conditionName;
        }
    }
}