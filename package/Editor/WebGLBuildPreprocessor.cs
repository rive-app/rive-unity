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
        public bool UseWasm2023;
        public bool UseThreads;
        public string SourcePath;
    }

    internal interface IWebGLEnvironment
    {
        string UnityVersion { get; }
        bool DisableWasmSimd { get; }

        /// Whether the build targets WebAssembly 2023. This is the effective value, not the stored
        /// Player Setting: enabling Native C/C++ Multithreading turns it on implicitly.
        bool TargetsWasm2023 { get; }
        bool UsesThreads { get; }
        string PackageName { get; }
        bool DirectoryExists(string path);
    }

    internal class DefaultWebGLEnvironment : IWebGLEnvironment
    {
        public string UnityVersion => UnityEngine.Application.unityVersion;
        public bool DisableWasmSimd => RiveProjectSettings.instance.DisableWasmSimd;

        public bool TargetsWasm2023
        {
            get
            {
#if UNITY_6000_0_OR_NEWER
                // Enabling Native C/C++ Multithreading forces WebAssembly 2023 on: the checkbox
                // renders ticked but greyed out, while the stored setting stays false. So the
                // property alone under-reports it, and we have to fold threads in to get the value
                // the build actually uses.
                return PlayerSettings.WebGL.wasm2023 || PlayerSettings.WebGL.threadsSupport;
#else
                // Player Settings > "Target WebAssembly 2023" doesn't exist before Unity 6.
                return false;
#endif
            }
        }

        // Player Settings > "Native C/C++ Multithreading".
        public bool UsesThreads => PlayerSettings.WebGL.threadsSupport;

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
            bool useWasm2023 = isUnity6OrNewer && env.TargetsWasm2023;
            // In Unity, Native C/C++ Multithreading is only available with Target WebAssembly
            // 2023, and the threaded build is made for both, so we derive it from wasm2023.
            // threadsSupport exists in older Unity versions too though, where wasm2023 does not.
            // Validate catches that and shows a clear error rather than failing at link time.
            bool useThreads = useWasm2023 && env.UsesThreads;

            // No-SIMD is the Safari 15 / iOS 15 variant, and that browser runs neither wasm2023 nor
            // threads, so it only applies with wasm2023 off (which rules threads out too).
            bool useNoSimd = isUnity6OrNewer && !useWasm2023 && env.DisableWasmSimd;

            string variantSuffix = "";
            if (useWasm2023) variantSuffix += "_wasm2023";
            if (useThreads) variantSuffix += "_mt";
            if (useNoSimd) variantSuffix += "_nosimd";
            string sourcePath = System.IO.Path.Combine(
                "Packages", env.PackageName,
                "Runtime/Libraries/WebGL",
                $"emscripten_{emscriptenVersion}{variantSuffix}");

            return new WebGLBuildConfig
            {
                EmscriptenVersion = emscriptenVersion,
                UseNoSimd = useNoSimd,
                UseWasm2023 = useWasm2023,
                UseThreads = useThreads,
                SourcePath = sourcePath
            };
        }

        public static void Validate(WebGLBuildConfig config, IWebGLEnvironment env)
        {
            // threadsSupport goes back to 2019, well before wasm2023, so it can be on where we
            // have no threaded variant to offer. Say so plainly instead of leaving it to wasm-ld.
            if (env.UsesThreads && !config.UseThreads)
            {
                // Report what we actually read: the usual cause is Target WebAssembly 2023 having
                // been turned back off while multithreading stayed on, which the UI doesn't
                // obviously surface.
                throw new BuildFailedException(
                    "Rive: WebGL builds with Native C/C++ Multithreading require Unity 6 or newer"
                    + " with Target WebAssembly 2023 enabled in Player Settings. Turn off"
                    + " multithreading, or enable Target WebAssembly 2023."
                    + $" (Unity {env.UnityVersion}, Native C/C++ Multithreading"
                    + $" {(env.UsesThreads ? "on" : "off")}, Target WebAssembly 2023"
                    + $" {(env.TargetsWasm2023 ? "on" : "off")}.)");
            }

            if (!env.DirectoryExists(config.SourcePath))
            {
                string extraHint = "";
                if (config.UseWasm2023)
                {
                    string settings = config.UseThreads
                        ? "Target WebAssembly 2023 and Native C/C++ Multithreading"
                        : "Target WebAssembly 2023";
                    extraHint = $" This variant may not be included in this package version."
                        + $" You can turn off {settings} in Player Settings to use the default libraries.";
                }
                else if (config.UseNoSimd)
                {
                    extraHint = " The no-SIMD library variant may not be included in this package version.";
                }
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
