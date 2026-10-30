#if UNITY_EDITOR
using System.Collections.Generic;
using Rive.Host;

namespace Rive
{
    /// <summary>
    /// Editor only. Records the values C# sets on the view model properties
    /// the Value Tracker window asks for, matched by view model and property
    /// name in any file, with the line that set each one. Costs one check per
    /// set while nothing is tracked.
    /// Tracked handles also watch for changes, and a change no recent C# set
    /// explains is recorded as set by Rive, like a binding or a listener.
    /// </summary>
    internal static class ValueTracker
    {
        internal const int Capacity = 2000;

        internal readonly struct Rule
        {
            internal readonly string ViewModel;
            internal readonly string Property;

            internal Rule(string viewModel, string property)
            {
                ViewModel = viewModel;
                Property = property;
            }
        }

        internal sealed class Entry
        {
            internal int Frame;
            internal string ViewModel;
            internal string Property;
            // Where the write went, as far as the handle knows, like "Cell_13/VideoZoomedInY".
            internal string Path;
            internal string Value;
            internal bool HasCaller;
            internal HandleErrors.Caller Caller;
            // Rive changed it, not C#.
            internal bool ByRive;
        }

        private struct RecentSet
        {
            internal int Frame;
            internal string ViewModel;
            internal string Property;
            internal string Value;
        }

        // C# sets, kept through Clear so their echoes still match.
        private const int RecentCapacity = 256;
        private static readonly RecentSet[] s_recent = new RecentSet[RecentCapacity];
        private static int s_recentNext;

        // Handles watching for changes Rive makes. Main thread only.
        private static readonly List<System.WeakReference<ViewModelPropertyHandle>> s_watching =
            new List<System.WeakReference<ViewModelPropertyHandle>>();

        private static readonly object s_lock = new object();
        private static Rule[] s_rules = new Rule[0];
        private static readonly Entry[] s_entries = new Entry[Capacity];
        private static int s_next;
        private static int s_count;
        private static int s_version;

        /// True while any rule is set. Write paths check this first.
        internal static volatile bool Active;

        /// Also print each write to the Console.
        internal static bool LogToConsole;

        /// Goes up with every recorded write or clear, for views to notice.
        internal static int Version => s_version;

        internal static void SetRules(IEnumerable<Rule> rules)
        {
            var list = new List<Rule>(rules);
            lock (s_lock)
            {
                s_rules = list.ToArray();
                Active = s_rules.Length > 0;
            }
            // Main thread. Handles that no longer match stop watching.
            var watching = new List<System.WeakReference<ViewModelPropertyHandle>>(s_watching);
            s_watching.Clear();
            foreach (var weak in watching)
            {
                if (weak.TryGetTarget(out ViewModelPropertyHandle handle))
                {
                    handle.UpdateTrackerWatch();
                }
            }
        }

        /// Main thread. Remembered so a rule change can stop it watching.
        internal static void Watch(ViewModelPropertyHandle handle)
        {
            ThreadCheck.Main();
            s_watching.Add(new System.WeakReference<ViewModelPropertyHandle>(handle));
        }

        /// Main thread. Handles watching for the tracker, for tests.
        internal static int WatchingCountForTests
        {
            get
            {
                s_watching.RemoveAll(weak => !weak.TryGetTarget(out _));
                return s_watching.Count;
            }
        }

        /// Colors as #RRGGBBAA, the same for every way of setting them.
        internal static string ArgbText(int argb)
        {
            return $"#{(argb >> 16) & 0xFF:X2}{(argb >> 8) & 0xFF:X2}{argb & 0xFF:X2}{(argb >> 24) & 0xFF:X2}";
        }

        internal static bool Matches(string viewModel, string property)
        {
            if (viewModel == null || property == null)
            {
                return false;
            }
            Rule[] rules = s_rules;
            for (int i = 0; i < rules.Length; i++)
            {
                if (rules[i].ViewModel == viewModel && rules[i].Property == property)
                {
                    return true;
                }
            }
            return false;
        }

        /// Records the write if a rule matches it. Callers check Active first.
        internal static void Record(string viewModel, string property, string path, string value)
        {
            if (!Matches(viewModel, property))
            {
                return;
            }
            var entry = new Entry
            {
                Frame = UnityEngine.Time.frameCount,
                ViewModel = viewModel,
                Property = property,
                Path = string.IsNullOrEmpty(path) ? property : path,
                Value = value,
            };
            entry.HasCaller = HandleErrors.TryFindCaller(out entry.Caller);
            lock (s_lock)
            {
                s_recent[s_recentNext] = new RecentSet { Frame = entry.Frame, ViewModel = viewModel, Property = property, Value = value };
                s_recentNext = (s_recentNext + 1) % RecentCapacity;
            }
            Add(entry);
        }

        /// Main thread, as a watching handle's change lands. Records it as set
        /// by Rive unless C# set the same value on the same view model and
        /// property from sinceFrame on, since that's C#'s own set coming back.
        /// Rive setting a value C# just set looks the same, so it's skipped.
        internal static void RecordChange(string viewModel, string property, string path, string value, int sinceFrame)
        {
            ThreadCheck.Main();
            if (!Matches(viewModel, property))
            {
                return;
            }
            lock (s_lock)
            {
                for (int i = 0; i < RecentCapacity; i++)
                {
                    RecentSet set = s_recent[(s_recentNext - 1 - i + RecentCapacity) % RecentCapacity];
                    if (set.Value == null || set.Frame < sinceFrame)
                    {
                        break;
                    }
                    if (set.ViewModel == viewModel && set.Property == property && set.Value == value)
                    {
                        return;
                    }
                }
            }
            Add(new Entry
            {
                Frame = UnityEngine.Time.frameCount,
                ViewModel = viewModel,
                Property = property,
                Path = string.IsNullOrEmpty(path) ? property : path,
                Value = value,
                ByRive = true,
            });
        }

        private static void Add(Entry entry)
        {
            lock (s_lock)
            {
                s_entries[s_next] = entry;
                s_next = (s_next + 1) % Capacity;
                s_count = System.Math.Min(s_count + 1, Capacity);
                s_version++;
            }
            if (LogToConsole)
            {
                string from = entry.ByRive
                    ? " (by Rive, not C#)"
                    : entry.HasCaller
                        ? $" (from {entry.Caller.TypeName}.{entry.Caller.MethodName}, at {entry.Caller.File}:{entry.Caller.Line})"
                        : string.Empty;
                UnityEngine.Debug.Log($"[Rive value set] frame {entry.Frame}: {entry.Path} = {entry.Value}{from}");
            }
        }

        /// The recorded writes, oldest first.
        internal static List<Entry> Snapshot()
        {
            lock (s_lock)
            {
                var list = new List<Entry>(s_count);
                int first = (s_next - s_count + Capacity) % Capacity;
                for (int i = 0; i < s_count; i++)
                {
                    list.Add(s_entries[(first + i) % Capacity]);
                }
                return list;
            }
        }

        internal static void Clear()
        {
            lock (s_lock)
            {
                System.Array.Clear(s_entries, 0, s_entries.Length);
                s_next = 0;
                s_count = 0;
                s_version++;
            }
        }
    }
}
#endif
