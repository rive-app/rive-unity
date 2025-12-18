using System.Collections;
using System.Collections.Generic;
using Rive.Components;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rive.EditorTools
{
    [CustomEditor(typeof(WidgetBehaviour), true)]
    internal class RiveWidgetInspector : RiveBaseEditor
    {

        public override VisualElement CreateInspectorGUI()
        {
            var root = base.CreateInspectorGUI();

            if (target is RiveWidget widget)
            {
                var playgroundRow = new VisualElement();
                playgroundRow.style.marginTop = 6;

                var playgroundButton = new Button(() =>
                {
                    DataBindingPlaygroundWindow.Open(widget);
                })
                {
                    text = "Open Playground",
                    tooltip = "Open a data binding playground for this widget"
                };

                playgroundRow.Add(playgroundButton);
                root.Add(playgroundRow);
            }

            return root;
        }
    }
}
