using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Rive.Utils;

namespace Rive.Host
{
    /// <summary>
    /// Counts calls that land off the thread their method expects, so a wrong
    /// one shows up in the tests without changing what the call does. The
    /// editor also warns once for each API called off the main thread.
    /// Editor and development builds only.
    /// </summary>
    internal static class ThreadCheck
    {
        private static int s_mainCalls;
        private static int s_mainViolations;
        private static string s_firstMainOffender;
#if UNITY_EDITOR
        private static readonly HashSet<string> s_warned = new HashSet<string>();
#endif

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Main([CallerMemberName] string caller = null)
        {
            Interlocked.Increment(ref s_mainCalls);
            if (CommandTransport.IsMainThread)
            {
                return;
            }
            if (Interlocked.Increment(ref s_mainViolations) == 1)
            {
                s_firstMainOffender = Describe(caller);
            }
#if UNITY_EDITOR
            WarnOnce();
#endif
        }

        internal static int MainCalls => Volatile.Read(ref s_mainCalls);
        internal static int MainViolations => Volatile.Read(ref s_mainViolations);
        internal static string FirstMainOffender => Volatile.Read(ref s_firstMainOffender);

        internal static void Reset()
        {
            Interlocked.Exchange(ref s_mainCalls, 0);
            Interlocked.Exchange(ref s_mainViolations, 0);
            Volatile.Write(ref s_firstMainOffender, null);
#if UNITY_EDITOR
            lock (s_warned)
            {
                s_warned.Clear();
            }
#endif
        }

        // Only runs on a violation, so the stack walk costs nothing otherwise.
        private static string Describe(string caller)
        {
            var method = new StackFrame(2, false).GetMethod();
            return method?.DeclaringType != null
                ? $"{method.DeclaringType.Name}.{method.Name}"
                : caller;
        }

#if UNITY_EDITOR
        private static void WarnOnce()
        {
            string api = EntryPoint();
            lock (s_warned)
            {
                if (!s_warned.Add(api))
                {
                    return;
                }
            }
            DebugLogger.Instance.LogWarning(
                $"{api} was called off the main thread. Rive's API is main thread only, so call it from there.");
        }

        // The outermost frame in Rive's runtime assemblies, which is the
        // method the script called.
        private static string EntryPoint()
        {
            System.Reflection.MethodBase entry = null;
            foreach (var frame in new StackTrace(1, false).GetFrames() ?? new StackFrame[0])
            {
                var method = frame.GetMethod();
                var type = method?.DeclaringType;
                if (type == typeof(ThreadCheck))
                {
                    continue;
                }
                if (type == null || !IsRuntimeAssembly(type.Assembly.GetName().Name))
                {
                    break;
                }
                entry = method;
            }
            if (entry == null)
            {
                return "A Rive method";
            }
            string owner = entry.DeclaringType.Name;
            int tick = owner.IndexOf('`');
            return $"{(tick < 0 ? owner : owner.Substring(0, tick))}.{entry.Name}";
        }

        private static bool IsRuntimeAssembly(string name)
        {
            return name == "Rive.Runtime" || name == "Rive.Runtime.Components";
        }
#endif
    }
}
