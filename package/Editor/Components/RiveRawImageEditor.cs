using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using Rive.Components;

namespace Rive.EditorTools
{
    [CustomEditor(typeof(CanvasRendererRawImage))]
    internal class RiveRawImageEditor : Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();

            // We want to show the texture field in the inspector when in play mode, but we want it to be read-only.
            if (Application.isPlaying)
            {
                var textureField = new ObjectField("Texture")
                {
                    objectType = typeof(Texture),
                    value = (target as CanvasRendererRawImage)?.texture,
                };

                textureField.SetEnabled(false);

                root.Add(textureField);

                // Update the texture field when the selection changes
                EditorApplication.update += () =>
                {
                    if (target != null && textureField != null)
                    {
                        textureField.value = (target as CanvasRendererRawImage)?.texture;
                    }
                };
            }

            return root;
        }
    }
}