#if UNITY_EDITOR && UNITY_WEBGL

using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using System.Collections.Generic;
using System.Linq;

namespace Rive.EditorTools
{

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



        private const string PACKAGE_NAME = PackageInfo.PACKAGE_NAME;
        private const string TEMP_PLUGINS_PATH = "Assets/Plugins/WebGL/Rive";
        private const string CREATED_FOLDERS_PREF = "RiveCreatedPluginFolders";

        public int callbackOrder => 0;

        private void TrackCreatedFolder(string path)
        {
            var createdFolders = new HashSet<string>(
                SessionState.GetString(CREATED_FOLDERS_PREF, "").Split(
                    new[] { '|' }, System.StringSplitOptions.RemoveEmptyEntries)
            );
            createdFolders.Add(path);
            SessionState.SetString(CREATED_FOLDERS_PREF, string.Join("|", createdFolders));
        }

        private bool WasCreatedByUs(string path)
        {
            var createdFolders = SessionState.GetString(CREATED_FOLDERS_PREF, "").Split(
                new[] { '|' }, System.StringSplitOptions.RemoveEmptyEntries
            );
            return System.Array.IndexOf(createdFolders, path) != -1;
        }

        private void ClearFolderTracking(string path)
        {
            var createdFolders = new HashSet<string>(
                SessionState.GetString(CREATED_FOLDERS_PREF, "").Split(
                    new[] { '|' }, System.StringSplitOptions.RemoveEmptyEntries)
            );
            createdFolders.Remove(path);
            SessionState.SetString(CREATED_FOLDERS_PREF, string.Join("|", createdFolders));
        }

        private void CleanupBuildPrefs()
        {
            SessionState.EraseString(CREATED_FOLDERS_PREF);
        }
        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.WebGL)
                return;

            // Clear any leftover prefs from previous builds that might have failed
            CleanupBuildPrefs();

            bool isUnity6OrNewer = UnityEngine.Application.unityVersion.StartsWith("6000") ||
                                  UnityEngine.Application.unityVersion.StartsWith("2023");

            string emscriptenVersion = isUnity6OrNewer ? "3.1.38" : "3.1.8";
            string sourcePath = System.IO.Path.Combine("Packages", PACKAGE_NAME, "Runtime/Libraries/WebGL", $"emscripten_{emscriptenVersion}");

            if (!System.IO.Directory.Exists(sourcePath))
            {
                throw new BuildFailedException($"Rive: Could not find WebGL libraries at {sourcePath}");
            }

            // Create and track directories we need so we can clean them up later
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

        private bool IsDirectoryEmpty(string path)
        {
            return !AssetDatabase.FindAssets(string.Empty, new[] { path }).Any();
        }

        private void DeleteAssetPath(string path)
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
}
#endif
