using System;
using System.Linq;
using Rive.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rive.EditorTools
{
    /// <summary>
    /// Adds a menu item to copy useful environment information to the clipboard for support tickets.
    /// </summary>
    internal static class SupportInfoMenu
    {
        private const string MenuPath = "Tools/Rive/Copy Support Info";

        [MenuItem(MenuPath, priority = 1000)]
        private static void CopySupportInfo()
        {
            try
            {
                string supportInfo = GenerateSupportInfo();
                EditorGUIUtility.systemCopyBuffer = supportInfo;
                EditorUtility.DisplayDialog("Rive", "Support info copied to clipboard.", "OK");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to copy Rive support info: {ex}");
            }
        }

        private static string GenerateSupportInfo()
        {
            string unityVersion = Application.unityVersion;
            BuildTarget activeBuildTarget = EditorUserBuildSettings.activeBuildTarget;

            var targetGroup = BuildPipeline.GetBuildTargetGroup(activeBuildTarget);
            var apis = PlayerSettings.GetGraphicsAPIs(activeBuildTarget);
            string graphicsApis = apis != null && apis.Length > 0
                ? string.Join(", ", apis.Select(api => api.ToString()).ToArray())
                : "Auto (Unity default)";

            string renderPipeline = GetRenderPipelineDescription();

            string operatingSystem = SystemInfo.operatingSystem;
            string graphicsDevice = SystemInfo.graphicsDeviceName + " (" + SystemInfo.graphicsDeviceType + ")";

            string riveVersion = GetPackageVersion(PackageInfo.PACKAGE_NAME);

            return
                "Rive Unity Support Info\n" +
                "------------------------\n" +
                $"Unity Version: {unityVersion}\n" +
                $"Active Build Target: {activeBuildTarget}\n" +
                $"Build Target Group: {targetGroup}\n" +
                $"Graphics APIs: {graphicsApis}\n" +
                $"Render Pipeline: {renderPipeline}\n" +
                $"OS: {operatingSystem}\n" +
                $"GPU: {graphicsDevice}\n" +
                $"Rive Plugin: {PackageInfo.PACKAGE_NAME} {riveVersion}\n";
        }

        private static string GetRenderPipelineDescription()
        {
            var asset = GraphicsSettings.currentRenderPipeline;
            if (asset == null)
            {
                return "Built-in Render Pipeline";
            }

            var srpType = asset.GetType();
            string pipelineName = srpType.Name;

            // Try to fetch version via known properties if available
            string version = null;

            var versionProperty = srpType.GetProperty("version") ?? srpType.GetProperty("Version");
            if (versionProperty != null)
            {
                try
                {
                    var value = versionProperty.GetValue(asset, null);
                    version = value != null ? value.ToString() : null;
                }
                catch { }
            }

            // Fallback to package if known SRPs
            if (string.IsNullOrEmpty(version))
            {
                string packageId = null;
                if (srpType.FullName.Contains("UniversalRenderPipeline"))
                {
                    packageId = "com.unity.render-pipelines.universal";
                }
                else if (srpType.FullName.Contains("HDRenderPipeline"))
                {
                    packageId = "com.unity.render-pipelines.high-definition";
                }

                if (!string.IsNullOrEmpty(packageId))
                {
                    version = GetPackageVersion(packageId);
                }
            }

            return string.IsNullOrEmpty(version) ? pipelineName : pipelineName + " " + version;
        }

        private static string GetPackageVersion(string packageName)
        {
            // Use UnityEditor.PackageManager for reliable version when available.
            try
            {
                var request = UnityEditor.PackageManager.Client.List(true, true);
                // We've added a busy-wait with timeout to avoid async flow in menu command
                var start = DateTime.UtcNow;
                while (!request.IsCompleted)
                {
                    if ((DateTime.UtcNow - start).TotalSeconds > 5)
                    {
                        break;
                    }
                }

                if (request.IsCompleted && request.Status == UnityEditor.PackageManager.StatusCode.Success)
                {
                    var pkg = request.Result.FirstOrDefault(p => p.name == packageName);
                    if (pkg != null)
                    {
                        return pkg.version;
                    }
                }
            }
            catch
            {
            }

            return "(version unknown)";
        }
    }
}


