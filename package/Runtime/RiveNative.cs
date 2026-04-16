using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using UnityEngine.Rendering;
using UnityEngine;
using Rive.Utils;

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
                private static bool s_loggedUnsupportedGraphicsApi;

#if (UNITY_IOS || UNITY_TVOS || UNITY_WEBGL || UNITY_SWITCH || UNITY_VISIONOS) && !UNITY_EDITOR
                public const string name = "__Internal";
#else
                public const string name = "rive";
#endif

                [DllImport(NativeLibrary.name)]
                private static extern void setUnityLog(LogDelegate callback);

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
                                setUnityLog(UnityLog);
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

                [AOT.MonoPInvokeCallback(typeof(LogDelegate))]
                static void UnityLog(IntPtr message)
                {
                        DebugLogger.Instance.Log("RiveNative: " + Marshal.PtrToStringAnsi(message));
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
