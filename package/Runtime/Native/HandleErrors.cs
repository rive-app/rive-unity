using System;
using Rive.Producer;
using Rive.Utils;
using Rive.Host;

namespace Rive
{
    /// <summary>
    /// Problems found on Rive's thread after a handle call returned, like a
    /// path that doesn't exist or an index past the end of a list. Each is
    /// logged once on the main thread and handed to the owner's sink, which
    /// is the widget's OnError for a widget's handles.
    /// </summary>
    internal static class HandleErrors
    {
        /// Rive's thread.
        internal static void ReportLater(RiveErrorCode code, string message, Action<RiveException> sink, string callSite)
        {
            ReportLater(new RiveException(code, message), sink, callSite);
        }

        /// Rive's thread. For an error that also fails a Future, so both see the same one.
        internal static void ReportLater(RiveException error, Action<RiveException> sink, string callSite)
        {
            CommandTransport.PostToMainThread(() => Report(error, sink, callSite));
        }

        /// Main thread.
        internal static void Report(RiveException error, Action<RiveException> sink, string callSite)
        {
            DebugLogger.Instance.LogWarning(callSite == null ? error.Message : $"{error.Message} {callSite}");
            Notify(error, sink);
        }

        /// Main thread, for a problem found at the call. Logged as an error, like other misuse.
        internal static void ReportNow(RiveException error, Action<RiveException> sink)
        {
            DebugLogger.Instance.LogError(error.Message);
            Notify(error, sink);
        }

        private static void Notify(RiveException error, Action<RiveException> sink)
        {
            if (sink == null)
            {
                return;
            }
            try
            {
                using (UserCallbacks.Scope())
                {
                    sink(error);
                }
            }
            catch (Exception e)
            {
                DebugLogger.Instance.LogException(e);
            }
        }

        /// Editor only: the first caller outside Rive, as "(at Assets/X.cs:12)"
        /// so the console links to it. Null in builds, where a stack walk per
        /// call would cost too much.
        internal static string CaptureCallSite()
        {
#if UNITY_EDITOR
            var trace = new System.Diagnostics.StackTrace(1, true);
            for (int i = 0; i < trace.FrameCount; i++)
            {
                System.Diagnostics.StackFrame frame = trace.GetFrame(i);
                Type type = frame.GetMethod()?.DeclaringType;
                string file = frame.GetFileName();
                if (type == null || string.IsNullOrEmpty(file) || IsRive(type))
                {
                    continue;
                }
                return $"(from {type.Name}.{frame.GetMethod().Name}, at {ProjectPath(file)}:{frame.GetFileLineNumber()})";
            }
#endif
            return null;
        }

#if UNITY_EDITOR
        private static bool IsRive(Type type)
        {
            string assembly = type.Assembly.GetName().Name;
            return assembly == "Rive.Runtime" || assembly == "Rive.Runtime.Components";
        }

        private static string ProjectPath(string file)
        {
            string root = System.IO.Path.GetDirectoryName(UnityEngine.Application.dataPath);
            file = file.Replace('\\', '/');
            if (root != null)
            {
                root = root.Replace('\\', '/') + "/";
                if (file.StartsWith(root, StringComparison.Ordinal))
                {
                    return file.Substring(root.Length);
                }
            }
            return file;
        }
#endif
    }
}
