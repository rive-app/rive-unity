using Rive.Components;
using UnityEditor;
using UnityEngine;

namespace Rive.EditorTools
{
    /// <summary>
    /// Gives panels the editor creates the project's new panel settings.
    /// </summary>
    [InitializeOnLoad]
    internal static class NewPanelDefaults
    {
        static NewPanelDefaults()
        {
            // Add Component. The menu and .riv drops call Apply themselves.
            ObjectFactory.componentWasAdded += OnComponentAdded;
        }

        private static void OnComponentAdded(Component component)
        {
            if (component is RivePanel panel)
            {
                Apply(panel);
                EditorUtility.SetDirty(panel);
            }
        }

        internal static void Apply(RivePanel panel)
        {
            panel.ThreadingMode = ToThreadingMode(RiveProjectSettings.instance.ResolvedNewPanelThreading);
        }

        internal static ThreadingMode ToThreadingMode(NewPanelThreading threading) =>
            threading == NewPanelThreading.BackgroundThread
                ? ThreadingMode.BackgroundThread
                : ThreadingMode.MainThread;
    }
}
