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
            ThreadCheck.Main();
            DebugLogger.Instance.LogWarning(callSite == null ? error.Message : $"{error.Message} {callSite}");
            Notify(error, sink);
        }

        /// Main thread, for a problem found at the call. Logged as an error, like other misuse.
        internal static void ReportNow(RiveException error, Action<RiveException> sink)
        {
            ThreadCheck.Main();
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

#if UNITY_EDITOR
        /// Whether new handles note who called them, for warnings. On by
        /// default. Set from the editor's Rive preferences.
        internal static bool CaptureCallSites = true;

        // Far enough to get out of any Rive call chain. Past it, give up.
        private const int MaxCallSiteDepth = 32;
#endif

        /// Editor only. The first caller outside Rive, as "(at Assets/X.cs:12)"
        /// so the console links to it. Null in builds, where a stack walk per
        /// call would cost too much.
        internal static string CaptureCallSite()
        {
#if UNITY_EDITOR
            if (CaptureCallSites && TryFindCaller(out Caller caller))
            {
                return $"(from {caller.TypeName}.{caller.MethodName}, at {caller.File}:{caller.Line})";
            }
#endif
            return null;
        }

#if UNITY_EDITOR
        internal struct Caller
        {
            internal string TypeName;
            internal string MethodName;
            // Relative to the project when it's inside it.
            internal string File;
            internal int Line;
        }

        /// The first frame outside Rive that has a source file. One frame at a
        /// time from the caller, since a whole StackTrace walks every frame
        /// down to Unity's, which is many times slower in the editor.
        internal static bool TryFindCaller(out Caller caller)
        {
            for (int depth = 1; depth <= MaxCallSiteDepth; depth++)
            {
                var frame = new System.Diagnostics.StackFrame(depth, true);
                System.Reflection.MethodBase method = frame.GetMethod();
                if (method == null)
                {
                    break;
                }
                Type type = method.DeclaringType;
                string file = frame.GetFileName();
                if (type == null || IsRive(type) || string.IsNullOrEmpty(file))
                {
                    continue;
                }
                string methodName = method.Name;
                // Coroutines and lambdas run in compiler made types and
                // methods, like <Setup>d__4.MoveNext or <>c.<Setup>b__4_0.
                // Name the method they came from.
                while (type.Name.StartsWith("<") && type.DeclaringType != null)
                {
                    string inner = InsideAngles(type.Name);
                    if (!string.IsNullOrEmpty(inner))
                    {
                        methodName = inner;
                    }
                    type = type.DeclaringType;
                }
                string fromMethod = InsideAngles(methodName);
                if (!string.IsNullOrEmpty(fromMethod))
                {
                    methodName = fromMethod;
                }
                caller = new Caller
                {
                    TypeName = type.Name,
                    MethodName = methodName,
                    File = ProjectPath(file),
                    Line = frame.GetFileLineNumber(),
                };
                return true;
            }
            caller = default;
            return false;
        }
#endif

#if UNITY_EDITOR
        // "Setup" from "<Setup>d__4" or "<Setup>b__4_0", or null.
        private static string InsideAngles(string name)
        {
            if (name.Length < 3 || name[0] != '<')
            {
                return null;
            }
            int close = name.IndexOf('>');
            return close > 1 ? name.Substring(1, close - 1) : null;
        }

        // GetName allocates, so each assembly is checked once.
        private static readonly System.Collections.Generic.Dictionary<System.Reflection.Assembly, bool> s_isRive =
            new System.Collections.Generic.Dictionary<System.Reflection.Assembly, bool>();

        private static bool IsRive(Type type)
        {
            System.Reflection.Assembly assembly = type.Assembly;
            if (!s_isRive.TryGetValue(assembly, out bool isRive))
            {
                string name = assembly.GetName().Name;
                isRive = name == "Rive.Runtime" || name == "Rive.Runtime.Components";
                s_isRive[assembly] = isRive;
            }
            return isRive;
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
