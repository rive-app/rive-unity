using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using UnityEngine.Rendering;
using UnityEngine;
using Rive.Utils;
#if UNITY_EDITOR
using UnityEditor;
#endif

[assembly: InternalsVisibleTo("Rive.Runtime.Components")]
[assembly: InternalsVisibleTo("Rive.Editor")]
[assembly: InternalsVisibleTo("Rive.Editor.Components")]
[assembly: InternalsVisibleTo("Rive.Tests.PlayMode")]
[assembly: InternalsVisibleTo("Rive.Tests.Shared")]
[assembly: InternalsVisibleTo("Rive.Tests.Editor")]
namespace Rive
{
        /// <summary>
        /// Indicates the state of the native Rive renderer.
        /// </summary>
        internal enum RendererStatus : uint
        {
                /// <summary> The renderer has not been initialized yet. </summary>
                Uninitialized = 0,
                /// <summary> A fully functional renderer is active. </summary>
                Initialized = 1,
                /// <summary> A no-op renderer is active (unsupported graphics API or headless mode). </summary>
                NoOp = 2,
        }

        internal class NativeLibrary
        {
                private delegate void LogDelegate(IntPtr message);

                // Held, or the GC collects what native calls through.
                private static LogDelegate s_logDelegate;
                private static bool s_loggedUnsupportedGraphicsApi;

#if (UNITY_IOS || UNITY_TVOS || UNITY_WEBGL || UNITY_SWITCH || UNITY_VISIONOS) && !UNITY_EDITOR
                public const string name = "__Internal";
#else
                public const string name = "rive";
#endif

                [DllImport(NativeLibrary.name)]
                private static extern void setUnityLog(LogDelegate callback);

                [DllImport(NativeLibrary.name)]
                private static extern void riveDrainLogs();

                // Explicit registration entry point for platforms (like iOS/tvOS/visionOS/WebGL)
                // where we can't rely solely on Unity calling UnityPluginLoad
                [DllImport(NativeLibrary.name)]
                private static extern void RiveRegisterRenderingPlugin();

                [DllImport(NativeLibrary.name)]
                private static extern uint getRendererStatus();

                /// <summary>
                /// Returns the current status of the native Rive renderer.
                /// </summary>
                public static RendererStatus GetRendererStatus()
                {
                        try
                        {
                                return (RendererStatus)getRendererStatus();
                        }
                        catch (DllNotFoundException)
                        {
                                NativeUsageGuard.MarkNativeLoadFailed(NativeLoadFailureReason.LibraryNotFound);
                                return RendererStatus.Uninitialized;
                        }
                        catch (EntryPointNotFoundException)
                        {
                                NativeUsageGuard.MarkNativeLoadFailed(NativeLoadFailureReason.EntryPointMissing);
                                return RendererStatus.Uninitialized;
                        }
                        catch (Exception e)
                        {
                                DebugLogger.Instance.LogException(e);
                                return RendererStatus.Uninitialized;
                        }
                }

                [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
                static void OnBeforeSceneLoadRuntimeMethod()
                {
                        if (!NativeUsageGuard.IsNativeAvailable)
                        {
                                return;
                        }

                        try
                        {
                                RegisterUnityLog();
                        }
                        catch (DllNotFoundException)
                        {
#if (UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX) && !UNITY_6000_0_OR_NEWER
                                NativeUsageGuard.MarkNativeLoadFailed(
                                        NativeLoadFailureReason.LibraryNotFound,
                                        "Rive is not supported on Linux with Unity versions older than Unity 6."
                                );
#else
                                NativeUsageGuard.MarkNativeLoadFailed(NativeLoadFailureReason.LibraryNotFound);
#endif
                                return;
                        }
                        catch (EntryPointNotFoundException)
                        {
                                NativeUsageGuard.MarkNativeLoadFailed(NativeLoadFailureReason.EntryPointMissing);
                                return;
                        }

#if (UNITY_WEBGL || UNITY_VISIONOS || UNITY_IOS || UNITY_TVOS) && !UNITY_EDITOR
                        try
                        {
                                RiveRegisterRenderingPlugin();
                        }
                        catch (Exception e) {
                                DebugLogger.Instance.LogException(e);
                        }
#endif

                        LogUnsupportedGraphicsAPIIfNeeded();
                }

#if UNITY_EDITOR
                // Every editor domain load, play mode or not. Hooking
                // afterAssemblyReload from the old domain would not fire,
                // subscriptions don't survive a reload.
                [InitializeOnLoadMethod]
                static void OnEditorDomainLoad()
                {
                        try
                        {
                                RegisterUnityLog();
                        }
                        catch (DllNotFoundException) { return; }
                        catch (EntryPointNotFoundException) { return; }

                        AssemblyReloadEvents.beforeAssemblyReload -= DropUnityLog;
                        AssemblyReloadEvents.beforeAssemblyReload += DropUnityLog;
                }
#endif

                static void RegisterUnityLog()
                {
                        // Native takes the new one before the old loses
                        // its root, or the old can be collected while
                        // native still points at it.
                        LogDelegate replacement = UnityLog;
                        setUnityLog(replacement);
                        s_logDelegate = replacement;

                        Application.onBeforeRender -= DrainLogs;
                        Application.onBeforeRender += DrainLogs;
#if UNITY_EDITOR
                        EditorApplication.update -= DrainLogs;
                        EditorApplication.update += DrainLogs;
#endif
                }

                // Where native's queued logs come out.
                static void DrainLogs()
                {
                        try
                        {
                                riveDrainLogs();
                        }
                        catch (DllNotFoundException) { }
                        catch (EntryPointNotFoundException) { }
                }

#if UNITY_EDITOR
                // The plugin outlives the domain, so a delegate from
                // this one would be a dangling call after the reload.
                static void DropUnityLog()
                {
                        Application.onBeforeRender -= DrainLogs;
                        EditorApplication.update -= DrainLogs;
                        try
                        {
                                setUnityLog(null);
                        }
                        catch (DllNotFoundException) { }
                        catch (EntryPointNotFoundException) { }
                        s_logDelegate = null;
                }
#endif

#if UNITY_EDITOR
                // Tests only. A real domain reload takes the test with it,
                // so they drive the same drop and re-register by hand.
                internal static void DropUnityLogForTests() => DropUnityLog();

                internal static void RegisterUnityLogForTests() =>
                        RegisterUnityLog();
#endif

                [AOT.MonoPInvokeCallback(typeof(LogDelegate))]
                static void UnityLog(IntPtr message)
                {
                        DebugLogger.Instance.Log("RiveNative: " + NativeText.FromNative(message));
                }

                private static void LogUnsupportedGraphicsAPIIfNeeded()
                {
                        if (s_loggedUnsupportedGraphicsApi || GetRendererStatus() != RendererStatus.NoOp)
                        {
                                return;
                        }
                        s_loggedUnsupportedGraphicsApi = true;

                        GraphicsDeviceType currentGraphicsApi = SystemInfo.graphicsDeviceType;
                        if (currentGraphicsApi == GraphicsDeviceType.Null)
                        {
                                DebugLogger.Instance.LogWarning(
                                    "Rive rendering is disabled because Unity has no active graphics device. " +
                                    "This is expected in headless, batch, or -nographics environments."
                                );

                                return;
                        }



#if UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX                        
                        if (currentGraphicsApi != GraphicsDeviceType.Vulkan)
                        {
                                DebugLogger.Instance.LogError(
                                        $"Rive does not support {currentGraphicsApi} on Linux. " +
                                        "Please use Vulkan. You can change the graphics API in the Player Settings."
                                );
                                return;
                        }
#endif



                        DebugLogger.Instance.LogError(
                                $"Rive does not support this graphics API ({currentGraphicsApi}) in the current environment."
                        );
                }
        }
}
