#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace Rive.EditorTools
{
    /// <summary>
    /// Per user settings under Preferences > Rive. Kept in EditorPrefs, so they
    /// stay on this machine and never reach the project.
    /// </summary>
    [InitializeOnLoad]
    internal static class RivePreferences
    {
        private const string CallSiteKey = "Rive.HandleCallSites";

        static RivePreferences()
        {
            Apply();
        }

        internal static bool CaptureCallSites
        {
            get => EditorPrefs.GetBool(CallSiteKey, true);
            set
            {
                EditorPrefs.SetBool(CallSiteKey, value);
                Apply();
            }
        }

        private static void Apply()
        {
            HandleErrors.CaptureCallSites = CaptureCallSites;
        }

        [SettingsProvider]
        internal static SettingsProvider Create()
        {
            return new SettingsProvider("Preferences/Rive", SettingsScope.User)
            {
                label = "Rive",
                guiHandler = _ =>
                {
                    EditorGUILayout.Space();
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField("Handles", EditorStyles.boldLabel);
                    bool capture = EditorGUILayout.Toggle(
                        new GUIContent(
                            "Caller In Warnings",
                            "Handle warnings say which line of your code made the handle, and the console links to it. " +
                            "Each new handle looks up its caller, which costs a little in the editor. Builds never do this."),
                        CaptureCallSites);
                    if (capture != CaptureCallSites)
                    {
                        CaptureCallSites = capture;
                    }
                    EditorGUILayout.HelpBox(
                        "Editor only. Turn it off when profiling handle calls in the editor, so the numbers are " +
                        "closer to a build.",
                        MessageType.None);
                    EditorGUI.indentLevel--;
                },
                keywords = new[] { "Rive", "handle", "call site", "caller", "profiling", "stack trace" },
            };
        }
    }
}

#endif
