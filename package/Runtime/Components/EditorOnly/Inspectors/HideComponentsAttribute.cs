using System;
using UnityEngine;

namespace Rive.EditorTools
{
    /// <summary>
    /// Used to hide components in the inspector.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    internal class HideComponentsAttribute : Attribute
    {
        public Type[] ComponentTypes { get; private set; }
        public HideFlags HideFlags { get; private set; }

        /// <summary>
        /// Specifies which components should be hidden in the inspector.
        /// </summary>
        /// <param name="componentTypes">The types of components to hide</param>
        public HideComponentsAttribute(params Type[] componentTypes) : this(HideFlags.HideInInspector | HideFlags.HideAndDontSave, componentTypes)
        {
        }

        /// <summary>
        /// Specifies which components should be hidden in the inspector with custom hide flags.
        /// </summary>
        /// <param name="hideFlags">The HideFlags to apply to the components</param>
        /// <param name="componentTypes">The types of components to hide</param>
        public HideComponentsAttribute(HideFlags hideFlags, params Type[] componentTypes)
        {
            ComponentTypes = componentTypes;
            HideFlags = hideFlags;
        }
    }
}