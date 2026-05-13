#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rive.EditorTools
{
    [FilePath("ProjectSettings/RiveProjectSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    internal class RiveProjectSettings : ScriptableSingleton<RiveProjectSettings>
    {
        [SerializeField] private bool m_DisableWasmSimd;

        internal bool DisableWasmSimd
        {
            get => m_DisableWasmSimd;
            set
            {
                if (m_DisableWasmSimd == value) return;
                m_DisableWasmSimd = value;
                Save(true);
            }
        }

        internal void SaveSettings() => Save(true);
    }

    internal class RiveProjectSettingsProvider : SettingsProvider
    {
        private SerializedObject m_SerializedSettings;
        private static bool s_IsUnity6OrNewer;

        private RiveProjectSettingsProvider()
            : base("Project/Rive", SettingsScope.Project)
        {
            s_IsUnity6OrNewer = WebGLConfigResolver.IsUnity6OrNewer(Application.unityVersion);
        }

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            RiveProjectSettings.instance.hideFlags = HideFlags.HideAndDontSave & ~HideFlags.NotEditable;
            m_SerializedSettings = new SerializedObject(RiveProjectSettings.instance);
        }

        public override void OnGUI(string searchContext)
        {
            m_SerializedSettings.Update();

            EditorGUILayout.Space();
            EditorGUI.indentLevel++;

            EditorGUILayout.LabelField("WebGL", EditorStyles.boldLabel);


            if (s_IsUnity6OrNewer)
            {
                var simdProp = m_SerializedSettings.FindProperty("m_DisableWasmSimd");
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(simdProp, new GUIContent(
                    "Disable WASM SIMD",
                    "Use no-SIMD native libraries for broader browser compatibility. " +
                    "Required for Safari 15 / iOS 15."
                ));
                if (EditorGUI.EndChangeCheck())
                {
                    m_SerializedSettings.ApplyModifiedProperties();
                    RiveProjectSettings.instance.SaveSettings();
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "The Disable WASM SIMD option is only available on Unity 6 and newer.",
                    MessageType.Info);
            }

            EditorGUI.indentLevel--;
        }

        [SettingsProvider]
        internal static SettingsProvider Create()
        {
            return new RiveProjectSettingsProvider
            {
                keywords = new[] { "Rive", "WebGL", "SIMD", "Safari", "iOS", "compatibility" }
            };
        }
    }
}

#endif
