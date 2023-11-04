using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using UnityEngine.Rendering;
using UnityEngine;

[assembly: InternalsVisibleTo("Rive.Editor")]

namespace Rive
{
    internal class NativeLibrary
    {
        private delegate void LogDelegate(IntPtr message);

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void RegisterPlugin();
#endif

#if (UNITY_IOS || UNITY_TVOS || UNITY_WEBGL || UNITY_SWITCH) && !UNITY_EDITOR
        public const string name = "__Internal";
#else
        public const string name = "rive";
#endif

        [DllImport(NativeLibrary.name)]
        private static extern void setUnityLog(LogDelegate callback);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void OnBeforeSceneLoadRuntimeMethod()
        {
            setUnityLog(UnityLog);
            NativeLibrary.initialize();

#if UNITY_WEBGL && !UNITY_EDITOR
            RegisterPlugin();
#endif
        }

        [AOT.MonoPInvokeCallback(typeof(LogDelegate))]
        static void UnityLog(IntPtr message)
        {
            Debug.Log("RiveNative: " + Marshal.PtrToStringAnsi(message));
        }

        public static bool m_initialized;

        public static void initialize()
        {
            if (m_initialized)
            {
                return;
            }
            m_initialized = true;
            var commandBuffer = new CommandBuffer();
            commandBuffer.IssuePluginEvent(getInitGraphics(), 0);
            Graphics.ExecuteCommandBuffer(commandBuffer);
            GL.InvalidateState();
        }

        [DllImport(NativeLibrary.name)]
        internal static extern IntPtr getInitGraphics();
    }
}
