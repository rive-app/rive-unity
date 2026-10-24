using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rive.EditorTools
{
    /// <summary>
    /// The value tracker's rules, kept in SessionState so they survive entering Play Mode,
    /// domain reloads and scene changes, and go away when the editor closes. Applied
    /// whenever the editor domain loads, which is before scene code runs in Play Mode.
    /// </summary>
    [InitializeOnLoad]
    internal static class ValueTrackerSession
    {
        private const string RulesKey = "Rive.ValueTracker.Rules";
        private const string ConsoleKey = "Rive.ValueTracker.Console";
        private const string FileKey = "Rive.ValueTracker.File";

        static ValueTrackerSession()
        {
            Apply();
        }

        /// The rules as stored, for noticing changes made from another window.
        internal static string StoredRules => SessionState.GetString(RulesKey, string.Empty);

        internal static List<ValueTracker.Rule> Rules
        {
            get
            {
                var rules = new List<ValueTracker.Rule>();
                foreach (string line in SessionState.GetString(RulesKey, string.Empty).Split('\n'))
                {
                    int tab = line.IndexOf('\t');
                    if (tab > 0 && tab < line.Length - 1)
                    {
                        rules.Add(new ValueTracker.Rule(line.Substring(0, tab), line.Substring(tab + 1)));
                    }
                }
                return rules;
            }
        }

        internal static bool LogToConsole
        {
            get => SessionState.GetBool(ConsoleKey, false);
            set
            {
                SessionState.SetBool(ConsoleKey, value);
                Apply();
            }
        }

        /// The file the window last listed, by asset GUID.
        internal static string FileGuid
        {
            get => SessionState.GetString(FileKey, string.Empty);
            set => SessionState.SetString(FileKey, value ?? string.Empty);
        }

        internal static bool IsTracked(string viewModel, string property)
        {
            return Rules.Any(r => r.ViewModel == viewModel && r.Property == property);
        }

        internal static void SetTracked(string viewModel, string property, bool tracked)
        {
            var rules = Rules.Where(r => !(r.ViewModel == viewModel && r.Property == property)).ToList();
            if (tracked)
            {
                rules.Add(new ValueTracker.Rule(viewModel, property));
            }
            Save(rules);
        }

        internal static void ClearRules()
        {
            Save(new List<ValueTracker.Rule>());
        }

        private static void Save(List<ValueTracker.Rule> rules)
        {
            SessionState.SetString(RulesKey, string.Join("\n", rules.Select(r => r.ViewModel + "\t" + r.Property)));
            Apply();
        }

        /// Applies what SessionState holds again, for after it's changed directly.
        internal static void Reload()
        {
            Apply();
        }

        private static void Apply()
        {
            ValueTracker.SetRules(Rules);
            ValueTracker.LogToConsole = LogToConsole;
        }
    }

    /// <summary>
    /// Window > Rive > Value Tracker. Pick properties from a Rive file, and every value C# sets on
    /// them in Play Mode is listed with its value and the line that made it, in any scene
    /// and on any instance of that view model.
    /// </summary>
    internal class RiveValueTrackerWindow : EditorWindow
    {
        private static readonly HashSet<ViewModelDataType> s_trackable = new HashSet<ViewModelDataType>
        {
            ViewModelDataType.Number,
            ViewModelDataType.String,
            ViewModelDataType.Boolean,
            ViewModelDataType.Color,
            ViewModelDataType.Enum,
            ViewModelDataType.Trigger,
        };

        private ObjectField m_fileField;
        private VisualElement m_viewModels;
        private VisualElement m_tracked;
        private Label m_trackedCount;
        private ToolbarSearchField m_filter;
        private Label m_writesCount;
        private Label m_emptyWrites;
        private ListView m_log;
        private readonly List<ValueTracker.Entry> m_shown = new List<ValueTracker.Entry>();
        private readonly Dictionary<string, Toggle> m_toggles = new Dictionary<string, Toggle>();
        private readonly HashSet<string> m_collapsed = new HashSet<string>();
        private int m_seenVersion = -1;
        private string m_seenRules;

        [MenuItem("Window/Rive/Value Tracker", priority = 1010)]
        internal static void Open()
        {
            GetWindow<RiveValueTrackerWindow>();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Rive Value Tracker");
            minSize = new Vector2(460, 420);
            BuildLayout();
            EditorApplication.update += EditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= EditorUpdate;
        }

        private void BuildLayout()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 10;

            var docs = new Label("View Data Binding Docs");
            RiveEditorStyles.StyleLinkLabel(docs, InspectorDocLinks.UnityDataBinding);
            root.Add(RiveEditorStyles.CreateSubtitleRow(
                "See which of your scripts set view model properties, and the line that set them in Play Mode" +
                ", in any scene. Changes Rive makes itself, like bindings and listeners, show as 'Set by Rive'.", docs));

            var split = new TwoPaneSplitView(0, 300, TwoPaneSplitViewOrientation.Vertical);
            split.style.flexGrow = 1;
            root.Add(split);

            // What's tracked first, since a file's property list can be long.
            var top = new ScrollView { style = { flexGrow = 1 } };
            top.Add(BuildTrackedCard());
            top.Add(BuildPropertiesCard());
            split.Add(top);
            split.Add(BuildWritesCard());

            string path = AssetDatabase.GUIDToAssetPath(ValueTrackerSession.FileGuid);
            var saved = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Asset>(path);
            m_fileField.SetValueWithoutNotify(saved);
            BuildViewModels(saved);
            RefreshTracked();
            RefreshLog(force: true);
        }

        private VisualElement BuildPropertiesCard()
        {
            var card = new VisualElement();
            RiveEditorStyles.StyleCard(card);
            card.Add(CardHeader("Choose Properties", null));
            card.Add(SecondaryLabel("Pick a Rive file, then select the properties to track. They're matched by view model and property name, in any file that uses them."));

            m_fileField = new ObjectField("File") { objectType = typeof(Asset), allowSceneObjects = false };
            m_fileField.style.marginTop = 6;
            m_fileField.RegisterValueChangedCallback(evt =>
            {
                var asset = evt.newValue as Asset;
                ValueTrackerSession.FileGuid = asset != null
                    ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset))
                    : string.Empty;
                BuildViewModels(asset);
            });
            card.Add(m_fileField);

            m_viewModels = new VisualElement();
            card.Add(m_viewModels);
            return card;
        }

        private VisualElement BuildTrackedCard()
        {
            var card = new VisualElement();
            RiveEditorStyles.StyleCard(card);
            var header = CardHeader("Tracked", null);
            m_trackedCount = RiveEditorStyles.CreatePill("0", RiveEditorStyles.PillBackground);
            header.Insert(1, m_trackedCount);
            var clear = new Label("Clear all");
            RiveEditorStyles.StyleLinkLabel(clear, () =>
            {
                ValueTrackerSession.ClearRules();
                SyncRules();
            });
            header.Add(clear);
            card.Add(header);
            m_tracked = new VisualElement { style = { marginTop = 4 } };
            card.Add(m_tracked);
            return card;
        }

        private VisualElement BuildWritesCard()
        {
            var card = new VisualElement();
            RiveEditorStyles.StyleCard(card);
            card.style.flexGrow = 1;
            card.style.marginBottom = 0;

            var header = CardHeader("Values Set", null);
            m_writesCount = RiveEditorStyles.CreatePill("0", RiveEditorStyles.PillBackground);
            header.Insert(1, m_writesCount);

            m_filter = new ToolbarSearchField { tooltip = "Show values whose path, value or script contains this." };
            m_filter.style.width = 180;
            m_filter.style.marginLeft = 8;
            m_filter.RegisterValueChangedCallback(_ => RefreshLog(force: true));
            header.Add(m_filter);

            var console = new Toggle("Console") { value = ValueTrackerSession.LogToConsole, tooltip = "Also print each value set to the Console." };
            console.style.marginLeft = 8;
            console.labelElement.style.minWidth = 0;
            console.labelElement.style.marginRight = 4;
            console.RegisterValueChangedCallback(evt => ValueTrackerSession.LogToConsole = evt.newValue);
            header.Add(console);

            header.Add(new Button(() =>
            {
                ValueTracker.Clear();
                RefreshLog(force: true);
            })
            { text = "Clear", style = { marginLeft = 6 } });
            card.Add(header);

            m_emptyWrites = SecondaryLabel("Values your scripts set on tracked properties show here in Play Mode. Double-click a row, or the script link, to open the line that set it.");
            m_emptyWrites.style.marginTop = 6;
            card.Add(m_emptyWrites);

            m_log = new ListView
            {
                itemsSource = m_shown,
                fixedItemHeight = 22,
                selectionType = SelectionType.Single,
                makeItem = MakeWriteRow,
                bindItem = BindWriteRow,
            };
            m_log.style.flexGrow = 1;
            m_log.style.marginTop = 6;
#if UNITY_2022_2_OR_NEWER
            m_log.itemsChosen += chosen => OpenCaller(chosen.FirstOrDefault() as ValueTracker.Entry);
#else
            m_log.onItemsChosen += chosen => OpenCaller(chosen.FirstOrDefault() as ValueTracker.Entry);
#endif
            card.Add(m_log);
            return card;
        }

        private static VisualElement CardHeader(string title, string pill)
        {
            var header = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            header.Add(new Label(title) { style = { unityFontStyleAndWeight = FontStyle.Bold } });
            if (pill != null)
            {
                header.Add(RiveEditorStyles.CreatePill(pill, RiveEditorStyles.PillBackground));
            }
            header.Add(new VisualElement { style = { flexGrow = 1 } });
            return header;
        }

        private static Label SecondaryLabel(string text)
        {
            return new Label(text)
            {
                style =
                {
                    fontSize = 11,
                    color = RiveEditorStyles.SecondaryText,
                    marginTop = 2,
                    whiteSpace = WhiteSpace.Normal,
                },
            };
        }

        private void BuildViewModels(Asset asset)
        {
            m_viewModels.Clear();
            m_toggles.Clear();
            var metadata = asset != null ? asset.EditorOnlyMetadata : null;
            if (metadata == null || metadata.ViewModels == null || metadata.ViewModels.Count == 0)
            {
                var empty = SecondaryLabel(asset == null ? "No file picked." : "This file has no view models.");
                empty.style.marginTop = 6;
                m_viewModels.Add(empty);
                return;
            }
            foreach (var viewModel in metadata.ViewModels)
            {
                var trackable = viewModel.Properties.Where(p => s_trackable.Contains(p.Type)).ToList();
                if (trackable.Count > 0)
                {
                    m_viewModels.Add(BuildViewModelSection(viewModel.Name, trackable));
                }
            }
        }

        private VisualElement BuildViewModelSection(string viewModel, List<FileMetadata.ViewModelPropertyMetadata> properties)
        {
            var section = new VisualElement { style = { marginTop = 6 } };
            bool expanded = !m_collapsed.Contains(viewModel);

            var header = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            var expander = new Button { text = expanded ? "▾" : "▸", tooltip = "Expand / collapse view model" };
            expander.style.width = 20;
            expander.style.height = 20;
            expander.style.paddingLeft = 0;
            expander.style.paddingRight = 0;
            expander.style.marginRight = 3;
            expander.style.fontSize = 30;
            RiveEditorStyles.StyleIconButton(expander);
            header.Add(expander);
            header.Add(new Label(viewModel) { style = { unityFontStyleAndWeight = FontStyle.Bold, flexGrow = 1 } });
            header.Add(RiveEditorStyles.CreatePill("View Model", RiveEditorStyles.ViewModelPillBackground));
            section.Add(header);

            var rows = new VisualElement { style = { marginLeft = 23, display = expanded ? DisplayStyle.Flex : DisplayStyle.None } };
            foreach (var property in properties)
            {
                string propertyName = property.Name;
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 3 } };
                row.Add(new Label(propertyName));
                var typePill = RiveEditorStyles.CreatePill(property.Type.ToString(), RiveEditorStyles.PillBackground);
                typePill.style.marginLeft = 6;
                row.Add(typePill);
                row.Add(new VisualElement { style = { flexGrow = 1 } });
                var toggle = new Toggle { value = ValueTrackerSession.IsTracked(viewModel, propertyName), tooltip = "Track values set on this property" };
                toggle.style.marginLeft = 8;
                toggle.RegisterValueChangedCallback(evt =>
                {
                    ValueTrackerSession.SetTracked(viewModel, propertyName, evt.newValue);
                    SyncRules();
                });
                m_toggles[viewModel + "\t" + propertyName] = toggle;
                row.Add(toggle);
                rows.Add(row);
            }
            section.Add(rows);

            expander.clicked += () =>
            {
                bool open = rows.style.display == DisplayStyle.None;
                rows.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
                expander.text = open ? "▾" : "▸";
                if (open)
                {
                    m_collapsed.Remove(viewModel);
                }
                else
                {
                    m_collapsed.Add(viewModel);
                }
            };
            return section;
        }

        private void RefreshTracked()
        {
            m_seenRules = ValueTrackerSession.StoredRules;
            var rules = ValueTrackerSession.Rules;
            m_trackedCount.text = rules.Count.ToString();
            m_tracked.Clear();
            if (rules.Count == 0)
            {
                m_tracked.Add(SecondaryLabel("Nothing tracked yet. Select properties below, or use Track This Value in the Data Binding Playground's ⋮ menu."));
            }
            foreach (var rule in rules)
            {
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 3 } };
                var viewModelPill = RiveEditorStyles.CreatePill(rule.ViewModel, RiveEditorStyles.ViewModelPillBackground);
                viewModelPill.style.marginLeft = 0;
                row.Add(viewModelPill);
                row.Add(new Label(rule.Property) { style = { unityFontStyleAndWeight = FontStyle.Bold, marginLeft = 6, flexGrow = 1 } });
                string viewModel = rule.ViewModel;
                string property = rule.Property;
                var remove = new Button(() =>
                {
                    ValueTrackerSession.SetTracked(viewModel, property, false);
                    SyncRules();
                })
                { text = "✕", tooltip = "Stop tracking" };
                remove.style.width = 22;
                remove.style.height = 20;
                RiveEditorStyles.StyleIconButton(remove);
                row.Add(remove);
                m_tracked.Add(row);
            }
            foreach (var pair in m_toggles)
            {
                int tab = pair.Key.IndexOf('\t');
                pair.Value.SetValueWithoutNotify(ValueTrackerSession.IsTracked(pair.Key.Substring(0, tab), pair.Key.Substring(tab + 1)));
            }
        }

        // After this window changes the rules.
        private void SyncRules()
        {
            RefreshTracked();
        }

        private void EditorUpdate()
        {
            // The playground's menu can change what's tracked too.
            if (ValueTrackerSession.StoredRules != m_seenRules)
            {
                RefreshTracked();
            }
            RefreshLog(force: false);
        }

        private void RefreshLog(bool force)
        {
            if (m_log == null || (!force && ValueTracker.Version == m_seenVersion))
            {
                return;
            }
            m_seenVersion = ValueTracker.Version;
            string filter = m_filter?.value;
            m_shown.Clear();
            foreach (var entry in ValueTracker.Snapshot())
            {
                if (string.IsNullOrEmpty(filter) || Describe(entry).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    m_shown.Add(entry);
                }
            }
            m_writesCount.text = m_shown.Count.ToString();
            m_emptyWrites.style.display = m_shown.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            m_log.style.display = m_shown.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            m_log.RefreshItems();
            if (m_shown.Count > 0)
            {
                m_log.ScrollToItem(m_shown.Count - 1);
            }
        }

        private VisualElement MakeWriteRow()
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            row.Add(new Label { name = "frame", style = { width = 72, fontSize = 11, color = RiveEditorStyles.SecondaryText } });
            row.Add(new Label { name = "path", style = { unityFontStyleAndWeight = FontStyle.Bold } });
            row.Add(new Label { name = "value", style = { marginLeft = 6, flexShrink = 1, overflow = Overflow.Hidden } });
            row.Add(new VisualElement { style = { flexGrow = 1 } });
            var caller = new Label { name = "caller" };
            RiveEditorStyles.StyleLinkLabel(caller, () => OpenCaller(caller.userData as ValueTracker.Entry));
            row.Add(caller);
            var byRive = RiveEditorStyles.CreatePill("Set by Rive", RiveEditorStyles.ViewModelPillBackground);
            byRive.name = "by-rive";
            byRive.tooltip = "Rive changed this, not a script. For example a binding, a listener or a state machine.";
            row.Add(byRive);
            return row;
        }

        private void BindWriteRow(VisualElement row, int index)
        {
            ValueTracker.Entry entry = m_shown[index];
            row.Q<Label>("frame").text = $"frame {entry.Frame}";
            row.Q<Label>("path").text = entry.Path;
            row.Q<Label>("value").text = $"= {entry.Value}";
            var caller = row.Q<Label>("caller");
            caller.style.display = entry.ByRive ? DisplayStyle.None : DisplayStyle.Flex;
            row.Q("by-rive").style.display = entry.ByRive ? DisplayStyle.Flex : DisplayStyle.None;
            caller.userData = entry;
            caller.text = entry.HasCaller
                ? $"{entry.Caller.TypeName}.{entry.Caller.MethodName} · {System.IO.Path.GetFileName(entry.Caller.File)}:{entry.Caller.Line}"
                : "caller unknown";
            caller.tooltip = entry.HasCaller ? $"{entry.Caller.File}:{entry.Caller.Line}" : null;
        }

        internal static string Describe(ValueTracker.Entry entry)
        {
            string from = entry.ByRive
                ? "  ←  Set by Rive"
                : entry.HasCaller
                ? $"  ←  {entry.Caller.TypeName}.{entry.Caller.MethodName} ({System.IO.Path.GetFileName(entry.Caller.File)}:{entry.Caller.Line})"
                : string.Empty;
            return $"frame {entry.Frame}  {entry.Path} = {entry.Value}{from}";
        }

        private static void OpenCaller(ValueTracker.Entry entry)
        {
            if (entry == null || !entry.HasCaller)
            {
                return;
            }
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(entry.Caller.File);
            if (script != null)
            {
                AssetDatabase.OpenAsset(script, entry.Caller.Line);
            }
            else
            {
                UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(
                    System.IO.Path.GetFullPath(entry.Caller.File), entry.Caller.Line);
            }
        }
    }
}
