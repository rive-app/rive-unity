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
        internal class NativeLibrary
        {
                private delegate void LogDelegate(IntPtr message);

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

                [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
                static void OnBeforeSceneLoadRuntimeMethod()
                {
                        setUnityLog(UnityLog);

#if (UNITY_WEBGL || UNITY_VISIONOS || UNITY_IOS || UNITY_TVOS) && !UNITY_EDITOR
                        try
                        {
                                RiveRegisterRenderingPlugin();
                        }
                        catch (Exception e) {
                                DebugLogger.Instance.LogException(e);
                        }
#endif
                }

                [AOT.MonoPInvokeCallback(typeof(LogDelegate))]
                static void UnityLog(IntPtr message)
                {
                        DebugLogger.Instance.Log("RiveNative: " + Marshal.PtrToStringAnsi(message));
                }
        }
}
