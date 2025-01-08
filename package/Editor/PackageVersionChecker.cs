using System.Collections;
using System.Collections.Generic;
using Rive.Utils;
using UnityEditor;
using UnityEditor.PackageManager;

namespace Rive.EditorTools
{
    /// <summary>
    /// Checks if the Rive package was updated and shows a dialog to restart the Unity Editor.
    /// We do this because the Rive plugin is a native plugin and the Unity Editor needs to be restarted in order unload the old version and load the new one.
    /// </summary>
    [InitializeOnLoad]
    internal class PackageVersionChecker
    {

        static PackageVersionChecker()
        {
            Events.registeredPackages += OnPackagesRegistered;
        }

        private static void OnPackagesRegistered(PackageRegistrationEventArgs args)
        {
            // Check if the Rive package was updated
            foreach (var changedPackage in args.changedTo)
            {
                if (changedPackage.name == PackageInfo.PACKAGE_NAME)
                {
                    ShowRestartDialog(changedPackage.version);
                    break;
                }
            }
        }

        private static void ShowRestartDialog(string newVersion)
        {
            EditorUtility.DisplayDialog(
                "Package Update Detected",
                $"The Rive plugin has been updated to version {newVersion}.\n\n" +
                "Please restart Unity to load the new version.",
                "OK"
            );

            DebugLogger.Instance.LogWarning(
                $"[{PackageInfo.PACKAGE_NAME}] Package updated to {newVersion}. " +
                "Please restart the Unity Editor to make sure the new version is fully loaded. If you skip this step, you might run into issues, and riv files may not work properly."
            );
        }
    }

}
