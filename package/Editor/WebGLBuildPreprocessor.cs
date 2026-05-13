#if UNITY_EDITOR

using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

[assembly: InternalsVisibleTo("Rive.Tests.Editor")]

namespace Rive.EditorTools
{
    internal struct WebGLBuildConfig
    {
        public string EmscriptenVersion;
        public bool UseNoSimd;
        public string SourcePath;
    }

    internal interface IWebGLEnvironment
    {
        string UnityVersion { get; }
        bool DisableWasmSimd { get; }
        string PackageName { get; }
        bool DirectoryExists(string path);
    }

    internal class DefaultWebGLEnvironment : IWebGLEnvironment
    {
        public string UnityVersion => UnityEngine.Application.unityVersion;
        public bool DisableWasmSimd => RiveProjectSettings.instance.DisableWasmSimd;
        public string PackageName => PackageInfo.PACKAGE_NAME;
        public bool DirectoryExists(string path) => System.IO.Directory.Exists(path);
    }

    internal static class WebGLConfigResolver
    {
        public static bool IsUnity6OrNewer(string unityVersion)
        {
            if (string.IsNullOrEmpty(unityVersion)) return false;

            int dotIndex = unityVersion.IndexOf('.');
            string majorToken = dotIndex >= 0 ? unityVersion.Substring(0, dotIndex) : unityVersion;

            if (!int.TryParse(majorToken, out int major)) return false;

            return major >= 6000 || major == 2023;
        }

        public static WebGLBuildConfig Resolve(IWebGLEnvironment env)
        {
            bool isUnity6OrNewer = IsUnity6OrNewer(env.UnityVersion);

            string emscriptenVersion = isUnity6OrNewer ? "3.1.38" : "3.1.8";
            bool useNoSimd = isUnity6OrNewer && env.DisableWasmSimd;
            string simdSuffix = useNoSimd ? "_nosimd" : "";
            string sourcePath = System.IO.Path.Combine(
                "Packages", env.PackageName,
                "Runtime/Libraries/WebGL",
                $"emscripten_{emscriptenVersion}{simdSuffix}");

            return new WebGLBuildConfig
            {
                EmscriptenVersion = emscriptenVersion,
                UseNoSimd = useNoSimd,
                SourcePath = sourcePath
            };
        }

        public static void Validate(WebGLBuildConfig config, IWebGLEnvironment env)
        {
            if (!env.DirectoryExists(config.SourcePath))
            {
                string extraHint = config.UseNoSimd
                    ? " The no-SIMD library variant may not be included in this package version."
                    : "";
                throw new BuildFailedException(
                    $"Rive: Could not find WebGL libraries at {config.SourcePath}.{extraHint}");
            }
        }
    }

#if UNITY_WEBGL
    /// Handles WebGL native plugin selection based on Unity version.
    /// Different Unity versions require different Emscripten-compiled libraries:
    /// - Unity 2022.x and earlier use Emscripten 3.1.8
    /// - Unity 2023.x (Unity 6) uses Emscripten 3.1.38
    /// If we don't match the emscripten library Unity uses, the build will fail with an error like: 
    ///     - Building Library\Bee\artifacts\WebGL\build\debug_WebGL_wasm\build.js failed with output:
    ///     - wasm-ld: error: Library/PackageCache/app.rive.rive-unity/Runtime/Libraries/WebGL/librive_wasm.a(artboard.o): undefined symbol: std::__2::__vector_base_common<true>::__throw_length_error() const
    //      - emcc: error: 'C:/6000.0.26f1/Editor/Data/PlaybackEngines/WebGLSupport/BuildTools/Emscripten/llvm\wasm-ld.exe @C:\Users\AppData\Local\Temp\emscripten_7f06ey06.rsp.utf-8' failed (returned 1)
    /// 
    /// The ideal way to do this would've been to use BuildUtilities.RegisterShouldIncludeInBuildCallback, but that is only called for managed plugins and not native plugins: https://docs.unity3d.com/ScriptReference/PackageManager.BuildUtilities.RegisterShouldIncludeInBuildCallback.html
    /// The other ideal way would've been to use `Define Constraints`, but that also doesn't work for native plugins: https://discussions.unity.com/t/define-constraints-are-not-filtering-plugins-pluginimporter-defineconstraints-also-has-no-effect/873361/5
    /// 
    /// This preprocessor ensures the correct library is included during WebGL builds by temporarily copying the appropriate libraries to the project's Plugin folder during the build
    internal class WebGLBuildPreprocessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        private const string TEMP_PLUGINS_PATH = "Assets/Plugins/WebGL/Rive";
        private const string CREATED_FOLDERS_PREF = "RiveCreatedPluginFolders";

        public int callbackOrder => 0;

        internal IWebGLEnvironment Environment { get; set; } = new DefaultWebGLEnvironment();

        private static BuildReport currentBuildReport;


        // We use this to cleanup the plugin files in case of build failure
        // This is necessary because the IPostprocessBuildWithReport callback is not called when the build fails, only when it succeeds
        private static void OnEditorUpdate()
        {
            if (currentBuildReport != null && (currentBuildReport.summary.result == BuildResult.Failed || currentBuildReport.summary.result == BuildResult.Cancelled))
            {
                // Unsubscribe first to prevent any potential multiple calls
                EditorApplication.update -= OnEditorUpdate;

                CleanupPluginFiles();

                currentBuildReport = null;
            }
        }

        private void TrackCreatedFolder(string path)
        {
            var createdFolders = new HashSet<string>(
                SessionState.GetString(CREATED_FOLDERS_PREF, "").Split(
                    new[] { '|' }, System.StringSplitOptions.RemoveEmptyEntries)
            );
            createdFolders.Add(path);
            SessionState.SetString(CREATED_FOLDERS_PREF, string.Join("|", createdFolders));
        }

        private static bool WasCreatedByUs(string path)
        {
            var createdFolders = SessionState.GetString(CREATED_FOLDERS_PREF, "").Split(
                new[] { '|' }, System.StringSplitOptions.RemoveEmptyEntries
            );
            return System.Array.IndexOf(createdFolders, path) != -1;
        }

        private static void ClearFolderTracking(string path)
        {
            var createdFolders = new HashSet<string>(
                SessionState.GetString(CREATED_FOLDERS_PREF, "").Split(
                    new[] { '|' }, System.StringSplitOptions.RemoveEmptyEntries)
            );
            createdFolders.Remove(path);
            SessionState.SetString(CREATED_FOLDERS_PREF, string.Join("|", createdFolders));
        }

        private static void CleanupBuildPrefs()
        {
            SessionState.EraseString(CREATED_FOLDERS_PREF);
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.WebGL)
                return;


            // Store the build report so we can cleanup the plugin files in case of build failure
            currentBuildReport = report;
            EditorApplication.update += OnEditorUpdate;

            // Clear any leftover prefs from previous builds that might have failed
            CleanupBuildPrefs();

            var config = WebGLConfigResolver.Resolve(Environment);
            WebGLConfigResolver.Validate(config, Environment);

            CopyLibrariesToPlugins(config.SourcePath);
        }

        private void CopyLibrariesToPlugins(string sourcePath)
        {
            string[] folders = { "Assets/Plugins", "Assets/Plugins/WebGL", TEMP_PLUGINS_PATH };
            foreach (string folder in folders)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    System.IO.Directory.CreateDirectory(folder);
                    TrackCreatedFolder(folder);
                }
            }

            // Copy all .a files and configure them for WebGL
            foreach (string file in System.IO.Directory.GetFiles(sourcePath, "*.a"))
            {
                string fileName = System.IO.Path.GetFileName(file);
                string destFile = System.IO.Path.Combine(TEMP_PLUGINS_PATH, fileName);
                System.IO.File.Copy(file, destFile, true);

                AssetDatabase.ImportAsset(destFile);
                var importer = AssetImporter.GetAtPath(destFile) as PluginImporter;
                if (importer != null)
                {
                    importer.SetCompatibleWithAnyPlatform(false);
                    importer.SetCompatibleWithPlatform(BuildTarget.WebGL, true);
                    importer.SaveAndReimport();
                }
            }

            AssetDatabase.Refresh();
        }

        private static bool IsDirectoryEmpty(string path)
        {
            return !AssetDatabase.FindAssets(string.Empty, new[] { path }).Any();
        }

        private static void DeleteAssetPath(string path)
        {
            if (AssetDatabase.DeleteAsset(path))
            {
                ClearFolderTracking(path);
            }
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.WebGL)
                return;

            try
            {
                CleanupPluginFiles();
            }
            finally
            {
                // Unsubscribe from editor update since we're handling the cleanup here
                EditorApplication.update -= OnEditorUpdate;
                currentBuildReport = null;
            }
        }

        private static void CleanupPluginFiles()
        {
            try
            {
                if (AssetDatabase.IsValidFolder(TEMP_PLUGINS_PATH))
                {
                    DeleteAssetPath(TEMP_PLUGINS_PATH);
                }

                // Check and clean up parent directories if empty and created by us
                string webglPath = "Assets/Plugins/WebGL";
                if (AssetDatabase.IsValidFolder(webglPath) &&
                    IsDirectoryEmpty(webglPath) &&
                    WasCreatedByUs(webglPath))
                {
                    DeleteAssetPath(webglPath);

                    string pluginsPath = "Assets/Plugins";
                    if (AssetDatabase.IsValidFolder(pluginsPath) &&
                        IsDirectoryEmpty(pluginsPath) &&
                        WasCreatedByUs(pluginsPath))
                    {
                        DeleteAssetPath(pluginsPath);
                    }
                }

                AssetDatabase.Refresh();
            }
            finally
            {
                // Cleanup prefs to avoid stale data on next build
                CleanupBuildPrefs();
            }
        }
    }
#endif
}

#endif
