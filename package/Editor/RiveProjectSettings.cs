#if UNITY_EDITOR

using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[assembly: InternalsVisibleTo("Rive.Editor.Components")]

namespace Rive.EditorTools
{
    /// <summary>
    /// The threading mode the editor gives panels it creates.
    /// </summary>
    internal enum NewPanelThreading
    {
        // Unity can't save "no value", and a missing field reads as 0, so this
        // stands in for it. Projects that never picked one follow the package
        // default when it changes.
        PackageDefault = 0,
        MainThread = 1,
        BackgroundThread = 2
    }

    [FilePath("ProjectSettings/RiveProjectSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    internal class RiveProjectSettings : ScriptableSingleton<RiveProjectSettings>
    {
        [SerializeField] private bool m_DisableWasmSimd;
        [SerializeField] private NewPanelThreading m_NewPanelThreading;

        // What PackageDefault means for now. Background thread is where we're
        // heading, so we'll flip this in a later release.
        internal const NewPanelThreading PackageDefaultThreading = NewPanelThreading.MainThread;

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

        /// <summary>
        /// What the project picked, or <see cref="NewPanelThreading.PackageDefault"/> if it never did.
        /// The settings page saves it.
        /// </summary>
        internal NewPanelThreading NewPanelThreading
        {
            get => m_NewPanelThreading;
            set => m_NewPanelThreading = value;
        }

        /// <summary>
        /// The mode new panels get, with the package default filled in.
        /// </summary>
        internal NewPanelThreading ResolvedNewPanelThreading => Resolve(m_NewPanelThreading);

        internal static NewPanelThreading Resolve(NewPanelThreading choice) =>
            choice == NewPanelThreading.PackageDefault ? PackageDefaultThreading : choice;

        internal void SaveSettings() => Save(true);
    }

    internal class RiveProjectSettingsProvider : SettingsProvider
    {
        private SerializedObject m_SerializedSettings;
        private static bool s_IsUnity6OrNewer;
        private static readonly IWebGLEnvironment s_Environment = new DefaultWebGLEnvironment();

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

            DrawPanelSettings();

            EditorGUILayout.Space();
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

                // Otherwise this toggle looks like it's doing something when it isn't. Ask the
                // resolver instead of re-deriving when no-SIMD applies, so this can't drift from
                // the variant the build actually picks.
                var config = WebGLConfigResolver.Resolve(s_Environment);
                if (RiveProjectSettings.instance.DisableWasmSimd && !config.UseNoSimd)
                {
                    string enabled = config.UseThreads
                        ? "Target WebAssembly 2023 and Native C/C++ Multithreading are"
                        : "Target WebAssembly 2023 is";
                    EditorGUILayout.HelpBox(
                        $"{enabled} enabled in Player Settings, so Rive uses the matching " +
                        "library variant and this option has no effect.",
                        MessageType.Info);
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

        private static readonly GUIContent s_NewPanelThreadingLabel = new GUIContent(
            "New Panel Threading Mode",
            "The threading mode for panels created from the GameObject menu, by dropping in a .riv " +
            "file, or with Add Component. Panels that already exist keep theirs. Background Thread " +
            "is where Rive is heading, and the package default will switch to it in a later release.");

        private static readonly GUIContent[] s_NewPanelThreadingOptions =
        {
            new GUIContent($"Package Default ({ObjectNames.NicifyVariableName(RiveProjectSettings.PackageDefaultThreading.ToString())})"),
            new GUIContent("Main Thread"),
            new GUIContent("Background Thread"),
        };

        private void DrawPanelSettings()
        {
            EditorGUILayout.LabelField("Panels", EditorStyles.boldLabel);
            var settings = RiveProjectSettings.instance;
            EditorGUI.BeginChangeCheck();
            int picked = EditorGUILayout.Popup(
                s_NewPanelThreadingLabel, (int)settings.NewPanelThreading, s_NewPanelThreadingOptions);
            if (EditorGUI.EndChangeCheck())
            {
                settings.NewPanelThreading = (NewPanelThreading)picked;
                settings.SaveSettings();
                m_SerializedSettings.Update();
            }
        }

        [SettingsProvider]
        internal static SettingsProvider Create()
        {
            return new RiveProjectSettingsProvider
            {
                keywords = new[] { "Rive", "WebGL", "SIMD", "Safari", "iOS", "compatibility", "Panel", "Threading", "Background" }
            };
        }
    }
}

#endif
