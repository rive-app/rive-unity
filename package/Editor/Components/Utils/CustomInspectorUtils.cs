using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Rive.EditorTools
{
    internal class CustomInspectorUtils
    {

        public static void HideNonInteractiveComponents(MonoBehaviour target, List<Type> componentTypes, Editor editor, HideFlags hideFlags = HideFlags.HideInInspector | HideFlags.HideAndDontSave)
        {
            if (target == null || componentTypes == null) return;

            foreach (var type in componentTypes)
            {
                var component = target.GetComponent(type);
                if (component != null)
                {
                    component.hideFlags = hideFlags;

                }

            }
        }

        public static void DestroyRequiredHiddenComponents(GameObject gameObject, Type componentType, Func<UnityEngine.Component, bool> ComponenentFilter = null)
        {
            RequireComponent[] requiredComponentsAtts = Attribute.GetCustomAttributes(componentType, typeof(RequireComponent), true) as RequireComponent[];

            foreach (RequireComponent rc in requiredComponentsAtts)
            {
                if (rc != null)
                {
                    Type[] typesToRemove = new Type[] { rc.m_Type0, rc.m_Type1, rc.m_Type2 };
                    foreach (Type type in typesToRemove)
                    {
                        if (type != null)
                        {
                            UnityEngine.Component componentToDestroy = gameObject.GetComponent(type);
                            ComponenentFilter = ComponenentFilter ?? ShouldDestroyComponent;
                            if (componentToDestroy != null && ShouldDestroyComponent(componentToDestroy))
                            {
                                UnityEngine.Object.DestroyImmediate(componentToDestroy);
                            }
                        }
                    }
                }
            }
        }

        private static bool ShouldDestroyComponent(UnityEngine.Component component)
        {
            // Check if the component has HideFlags that indicate it should be automatically managed
            return (component.hideFlags & (HideFlags.HideInInspector | HideFlags.HideAndDontSave)) != 0;
        }


    }
}