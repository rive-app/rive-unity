using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using Rive;
using Rive.Components;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rive.EditorTools
{
    internal class DataBindingPlaygroundWindow : EditorWindow
    {
        private RiveWidget m_widget;
        private ObjectField m_widgetField;
        private HelpBox m_playModeHelpBox;
        private VisualElement m_interactiveContainer;
        private ScrollView m_propertiesScroll;

        private readonly List<PropertyBinding> m_propertyBindings = new List<PropertyBinding>();
        private readonly List<ListPropertyBinding> m_listBindings = new List<ListPropertyBinding>();
        private bool m_isRefreshing;
        private enum PlaygroundState
        {
            NotPlaying,
            NoWidget,
            NoFileMetadata,
            NoViewModels,
            NoArtboardMetadata,
            NoDefaultViewModel,
            WidgetNotLoaded,
            NoViewModelInstance,
            Ready
        }

        private FileMetadata m_fileMetadata;
        private FileMetadata.ArtboardMetadata m_artboardMetadata;
        private double m_nextRefreshTime;
        private readonly Dictionary<string, ImageOutOfBandAsset> m_imageSelectionCache = new Dictionary<string, ImageOutOfBandAsset>();
        private readonly Dictionary<string, ArtboardSelection> m_artboardSelectionCache = new Dictionary<string, ArtboardSelection>();
        private readonly Dictionary<string, bool> m_viewModelExpansion = new Dictionary<string, bool>();

        // Used for the artboard databinding dropdowns
        private class ArtboardSelection
        {
            public Asset Asset;
            public File File;
            public string ArtboardName;
        }

        private const string DocsBaseUrl = InspectorDocLinks.UnityDataBinding;

        private class PropertyBinding
        {
            public string Path;
            public VisualElement Control;
            public Action<ViewModelInstance> Sync;
        }

        private class ListPropertyBinding
        {
            public string Path;
            public VisualElement Control;
            public Action<ViewModelInstance> Sync;
            public List<ViewModelInstance> LastItems = new List<ViewModelInstance>();
            public bool InitializedTypeSelection;
        }

        // Adapter that proxies a list property directly into a ListView without keeping a stale mirror.
        private class ListPropertyAdapter : IList<ViewModelInstance>, IList
        {
            private readonly Func<ViewModelInstanceListProperty> m_propertyGetter;
            private readonly Func<ViewModelInstance> m_factory;

            public ListPropertyAdapter(Func<ViewModelInstanceListProperty> propertyGetter, Func<ViewModelInstance> factory)
            {
                m_propertyGetter = propertyGetter;
                m_factory = factory;
            }

            private ViewModelInstanceListProperty Prop => m_propertyGetter?.Invoke();

            public int Count => Prop?.Count ?? 0;

            public bool IsReadOnly => false;
            bool IList.IsFixedSize => false;

            public ViewModelInstance this[int index]
            {
                get => Prop?.GetInstanceAt(index);
                set => MoveOrReplace(index, value);
            }

            object IList.this[int index]
            {
                get => this[index];
                set
                {
                    MoveOrReplace(index, value as ViewModelInstance);
                }
            }

            private void MoveOrReplace(int index, ViewModelInstance value)
            {
                var prop = Prop;
                if (prop == null || value == null)
                {
                    return;
                }

                // If the instance already exists in the list, move it.
                int existingIndex = -1;
                for (int i = 0; i < prop.Count; i++)
                {
                    if (ReferenceEquals(prop.GetInstanceAt(i), value))
                    {
                        existingIndex = i;
                        break;
                    }
                }

                if (existingIndex == index)
                {
                    return;
                }

                if (existingIndex >= 0)
                {
                    prop.RemoveAt(existingIndex);
                    if (existingIndex < index)
                    {
                        index -= 1;
                    }
                }
                else if (index < prop.Count)
                {
                    // Replace the current item at index if value not found elsewhere
                    prop.RemoveAt(index);
                }

                index = Mathf.Clamp(index, 0, prop.Count);
                prop.Insert(value, index);
            }

            public void Add(ViewModelInstance item)
            {
                var prop = Prop;
                if (prop == null)
                {
                    return;
                }

                var toAdd = item ?? m_factory?.Invoke();
                if (toAdd != null)
                {
                    prop.Add(toAdd);
                }
            }

            int IList.Add(object value)
            {
                var prop = Prop;
                if (prop == null)
                {
                    return -1;
                }

                Add(value as ViewModelInstance);
                return prop.Count - 1;
            }

            public void Clear()
            {
                var prop = Prop;
                if (prop == null)
                {
                    return;
                }

                for (int i = prop.Count - 1; i >= 0; i--)
                {
                    prop.RemoveAt(i);
                }
            }

            public bool Contains(ViewModelInstance item)
            {
                var prop = Prop;
                if (prop == null || item == null)
                {
                    return false;
                }

                for (int i = 0; i < prop.Count; i++)
                {
                    if (ReferenceEquals(prop.GetInstanceAt(i), item))
                    {
                        return true;
                    }
                }

                return false;
            }

            public void CopyTo(ViewModelInstance[] array, int arrayIndex)
            {
                var prop = Prop;
                if (prop == null || array == null)
                {
                    return;
                }

                int count = prop.Count;
                for (int i = 0; i < count && arrayIndex + i < array.Length; i++)
                {
                    array[arrayIndex + i] = prop.GetInstanceAt(i);
                }
            }

            public IEnumerator<ViewModelInstance> GetEnumerator()
            {
                var prop = Prop;
                if (prop == null)
                {
                    yield break;
                }

                int count = prop.Count;
                for (int i = 0; i < count; i++)
                {
                    yield return prop.GetInstanceAt(i);
                }
            }

            public int IndexOf(ViewModelInstance item)
            {
                var prop = Prop;
                if (prop == null || item == null)
                {
                    return -1;
                }

                for (int i = 0; i < prop.Count; i++)
                {
                    if (ReferenceEquals(prop.GetInstanceAt(i), item))
                    {
                        return i;
                    }
                }

                return -1;
            }

            public void Insert(int index, ViewModelInstance item)
            {
                var prop = Prop;
                if (prop == null)
                {
                    return;
                }

                var toInsert = item ?? m_factory?.Invoke();
                if (toInsert == null)
                {
                    return;
                }

                index = Mathf.Clamp(index, 0, prop.Count);
                prop.Insert(toInsert, index);
            }

            void IList.Insert(int index, object value) => Insert(index, value as ViewModelInstance);

            public bool Remove(ViewModelInstance item)
            {
                var prop = Prop;
                if (prop == null || item == null)
                {
                    return false;
                }

                prop.Remove(item);
                return true;
            }

            void IList.Remove(object value) => Remove(value as ViewModelInstance);

            public void RemoveAt(int index)
            {
                var prop = Prop;
                if (prop == null || index < 0 || index >= prop.Count)
                {
                    return;
                }

                prop.RemoveAt(index);
            }

            bool IList.Contains(object value) => Contains(value as ViewModelInstance);

            void ICollection.CopyTo(Array array, int index)
            {
                if (array == null)
                {
                    return;
                }

                var prop = Prop;
                if (prop == null)
                {
                    return;
                }

                for (int i = 0; i < prop.Count && index + i < array.Length; i++)
                {
                    array.SetValue(prop.GetInstanceAt(i), index + i);
                }
            }

            bool ICollection.IsSynchronized => false;

            private readonly object m_syncRoot = new object();
            object ICollection.SyncRoot => m_syncRoot;

            int IList.IndexOf(object value) => IndexOf(value as ViewModelInstance);

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }

        public static void Open(RiveWidget widget)
        {
            var window = GetWindow<DataBindingPlaygroundWindow>();
            window.titleContent = new GUIContent("Rive Data Binding Playground");
            window.minSize = new Vector2(420, 360);
            window.SetTarget(widget);
            window.Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Rive Data Binding Playground");
            minSize = new Vector2(420, 360);
            BuildLayout();
            EditorApplication.update += EditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= EditorUpdate;
            m_propertyBindings.Clear();
            m_listBindings.Clear();
            m_widget = null;
            m_artboardSelectionCache.Clear();
            m_viewModelExpansion.Clear();
        }

        private void EditorUpdate()
        {
            // throttle refreshes to avoid unnecessary work
            if (EditorApplication.timeSinceStartup < m_nextRefreshTime)
            {
                return;
            }

            m_nextRefreshTime = EditorApplication.timeSinceStartup + 0.25f;
            UpdateVisibility();
            RefreshValues();
        }

        private void BuildLayout()
        {
            var root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 10;

            var subtitleRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 14
                }
            };
            var subtitle = new Label("Inspect and tweak ViewModel instance properties for the selected RiveWidget.");
            subtitle.style.flexGrow = 1;
            subtitle.style.fontSize = 12;
            subtitle.style.color = new UnityEngine.Color(0.8f, 0.8f, 0.8f, 0.95f);

            var docsLink = new Label("View Data Binding Docs");
            StyleLinkLabel(docsLink, InspectorDocLinks.UnityDataBinding);

            subtitleRow.Add(subtitle);
            subtitleRow.Add(docsLink);
            root.Add(subtitleRow);

            m_playModeHelpBox = new HelpBox(
                "Play with data binding values for the selected RiveWidget while in Play Mode. " +
                "Changes are applied to the widget's current ViewModel instance.",
                HelpBoxMessageType.Info);

            m_playModeHelpBox.style.marginBottom = 10;
            root.Add(m_playModeHelpBox);

            m_widgetField = new ObjectField("Widget")
            {
                objectType = typeof(RiveWidget),
                allowSceneObjects = true
            };
            m_widgetField.RegisterValueChangedCallback(evt =>
            {
                SetTarget(evt.newValue as RiveWidget);
            });
            root.Add(m_widgetField);

            m_interactiveContainer = new VisualElement();
            m_interactiveContainer.style.flexDirection = FlexDirection.Column;
            m_interactiveContainer.style.flexGrow = 1;
            root.Add(m_interactiveContainer);

            m_propertiesScroll = new ScrollView
            {
                style =
                {
                    flexGrow = 1
                }
            };
            m_interactiveContainer.Add(m_propertiesScroll);

            UpdateVisibility();
        }

        private void SetTarget(RiveWidget widget)
        {
            m_widget = widget;
            m_widgetField?.SetValueWithoutNotify(widget);
            RefreshMetadata();
            RebuildProperties();
            UpdateVisibility();
            RefreshValues();
        }

        private void RefreshMetadata()
        {
            m_fileMetadata = m_widget?.Asset?.EditorOnlyMetadata;
            m_artboardMetadata = null;

            if (m_fileMetadata == null)
            {
                return;
            }

            var artboardName = !string.IsNullOrEmpty(m_widget?.ArtboardName)
                ? m_widget.ArtboardName
                : m_fileMetadata.GetArtboardNames().FirstOrDefault();

            if (!string.IsNullOrEmpty(artboardName))
            {
                m_artboardMetadata = m_fileMetadata.GetArtboard(artboardName);
            }
        }

        private void RebuildProperties()
        {
            m_propertyBindings.Clear();
            m_listBindings.Clear();
            m_viewModelExpansion.Clear();
            m_propertiesScroll.Clear();

            if (m_widget == null)
            {
                m_propertiesScroll.Add(new Label("Select a RiveWidget to get started."));
                return;
            }

            if (m_artboardMetadata == null)
            {
                m_propertiesScroll.Add(new HelpBox("No artboard metadata found for the selected widget.", HelpBoxMessageType.Warning));
                return;
            }

            if (m_artboardMetadata.DefaultViewModel == null)
            {
                m_propertiesScroll.Add(new HelpBox("The current artboard does not have a default ViewModel.", HelpBoxMessageType.Warning));
                return;
            }

            BuildViewModelSection(
                m_artboardMetadata.DefaultViewModel,
                string.Empty,
                m_propertiesScroll,
                0);
        }

        private FileMetadata.ViewModelMetadata FindViewModelMetadata(string viewModelName)
        {
            if (m_fileMetadata == null || string.IsNullOrEmpty(viewModelName))
            {
                return null;
            }

            return m_fileMetadata.ViewModels.FirstOrDefault(vm => vm.Name == viewModelName);
        }

        private List<string> GetEnumOptions(FileMetadata.ViewModelPropertyMetadata property)
        {
            if (m_fileMetadata == null || m_fileMetadata.Enums == null || string.IsNullOrEmpty(property.EnumTypeName))
            {
                return null;
            }

            var enumMeta = m_fileMetadata.Enums.FirstOrDefault(e => e.Name == property.EnumTypeName);
            return enumMeta?.Values?.ToList();
        }

        private void AddStringField(VisualElement parent, string path, string displayName, Func<ViewModelInstance> instanceProvider = null, string cacheKey = null, string pathLabelOverride = null, List<PropertyBinding> bindingList = null)
        {
            instanceProvider ??= GetCurrentInstance;
            cacheKey ??= path;
            bindingList ??= m_propertyBindings;

            var field = new TextField();
            field.RegisterValueChangedCallback(evt =>
            {
                var instance = instanceProvider();
                var prop = instance?.GetStringProperty(path);
                if (prop != null)
                {
                    prop.Value = evt.newValue;
                }
            });

            bindingList.Add(new PropertyBinding
            {
                Path = cacheKey,
                Control = field,
                Sync = instance =>
                {
                    var current = instanceProvider();
                    var prop = current?.GetStringProperty(path);
                    field.SetEnabled(prop != null);
                    if (prop != null)
                    {
                        field.SetValueWithoutNotify(prop.Value ?? string.Empty);
                    }
                }
            });

            parent.Add(CreatePropertyCard(displayName, pathLabelOverride ?? path, "String", GetDocUrl(ViewModelDataType.String),
                null, field));
        }

        private void AddNumberField(VisualElement parent, string path, string displayName, Func<ViewModelInstance> instanceProvider = null, string cacheKey = null, string pathLabelOverride = null, List<PropertyBinding> bindingList = null)
        {
            instanceProvider ??= GetCurrentInstance;
            cacheKey ??= path;
            bindingList ??= m_propertyBindings;

            var field = new FloatField();
            field.RegisterValueChangedCallback(evt =>
            {
                var instance = instanceProvider();
                var prop = instance?.GetNumberProperty(path);
                if (prop != null)
                {
                    prop.Value = evt.newValue;
                }
            });

            bindingList.Add(new PropertyBinding
            {
                Path = cacheKey,
                Control = field,
                Sync = instance =>
                {
                    var current = instanceProvider();
                    var prop = current?.GetNumberProperty(path);
                    field.SetEnabled(prop != null);
                    if (prop != null)
                    {
                        field.SetValueWithoutNotify(prop.Value);
                    }
                }
            });

            parent.Add(CreatePropertyCard(displayName, pathLabelOverride ?? path, "Number", GetDocUrl(ViewModelDataType.Number),
                null, field));
        }

        private void AddBooleanField(VisualElement parent, string path, string displayName, Func<ViewModelInstance> instanceProvider = null, string cacheKey = null, string pathLabelOverride = null, List<PropertyBinding> bindingList = null)
        {
            instanceProvider ??= GetCurrentInstance;
            cacheKey ??= path;
            bindingList ??= m_propertyBindings;

            var toggle = new Toggle();
            toggle.RegisterValueChangedCallback(evt =>
            {
                var instance = instanceProvider();
                var prop = instance?.GetBooleanProperty(path);
                if (prop != null)
                {
                    prop.Value = evt.newValue;
                }
            });

            bindingList.Add(new PropertyBinding
            {
                Path = cacheKey,
                Control = toggle,
                Sync = instance =>
                {
                    var current = instanceProvider();
                    var prop = current?.GetBooleanProperty(path);
                    toggle.SetEnabled(prop != null);
                    if (prop != null)
                    {
                        toggle.SetValueWithoutNotify(prop.Value);
                    }
                }
            });

            parent.Add(CreatePropertyCard(displayName, pathLabelOverride ?? path, "Boolean", GetDocUrl(ViewModelDataType.Boolean),
                null, toggle));
        }

        private void AddColorField(VisualElement parent, string path, string displayName, Func<ViewModelInstance> instanceProvider = null, string cacheKey = null, string pathLabelOverride = null, List<PropertyBinding> bindingList = null)
        {
            instanceProvider ??= GetCurrentInstance;
            cacheKey ??= path;
            bindingList ??= m_propertyBindings;

            var field = new ColorField();
            field.RegisterValueChangedCallback(evt =>
            {
                var instance = instanceProvider();
                var prop = instance?.GetColorProperty(path);
                if (prop != null)
                {
                    prop.Value = evt.newValue;
                }
            });

            bindingList.Add(new PropertyBinding
            {
                Path = cacheKey,
                Control = field,
                Sync = instance =>
                {
                    var current = instanceProvider();
                    var prop = current?.GetColorProperty(path);
                    field.SetEnabled(prop != null);
                    if (prop != null)
                    {
                        field.SetValueWithoutNotify(prop.Value);
                    }
                }
            });

            parent.Add(CreatePropertyCard(displayName, pathLabelOverride ?? path, "Color", GetDocUrl(ViewModelDataType.Color),
                null, field));
        }

        private void AddImageField(VisualElement parent, string path, string displayName, Func<ViewModelInstance> instanceProvider = null, string cacheKey = null, string pathLabelOverride = null, List<PropertyBinding> bindingList = null)
        {
            instanceProvider ??= GetCurrentInstance;
            cacheKey ??= path;
            bindingList ??= m_propertyBindings;

            var field = new ObjectField
            {
                objectType = typeof(ImageOutOfBandAsset),
                allowSceneObjects = false
            };

            field.RegisterValueChangedCallback(evt =>
            {
                var instance = instanceProvider();
                var prop = instance?.GetImageProperty(path);
                if (prop == null)
                {
                    return;
                }

                var asset = evt.newValue as ImageOutOfBandAsset;
                if (asset == null)
                {
                    m_imageSelectionCache[cacheKey] = null;
                    prop.Value = null;
                    return;
                }

                m_imageSelectionCache[cacheKey] = asset;
                asset.Load();
                prop.Value = asset;
                asset.Unload();
            });

            bindingList.Add(new PropertyBinding
            {
                Path = cacheKey,
                Control = field,
                Sync = instance =>
                {
                    var current = instanceProvider();
                    var prop = current?.GetImageProperty(path);
                    field.SetEnabled(prop != null);
                    // Reflect cached selection for UX (no getter available from runtime)
                    if (m_imageSelectionCache.TryGetValue(cacheKey, out var cached))
                    {
                        field.SetValueWithoutNotify(cached);
                    }
                    else
                    {
                        field.SetValueWithoutNotify(null);
                    }
                }
            });

            parent.Add(CreatePropertyCard(displayName, pathLabelOverride ?? path, "Image", GetDocUrl(ViewModelDataType.AssetImage),
                null, field));
        }

        private void AddEnumField(VisualElement parent, string path, FileMetadata.ViewModelPropertyMetadata property, Func<ViewModelInstance> instanceProvider = null, string cacheKey = null, string pathLabelOverride = null, List<PropertyBinding> bindingList = null)
        {
            instanceProvider ??= GetCurrentInstance;
            cacheKey ??= path;
            bindingList ??= m_propertyBindings;

            var options = GetEnumOptions(property) ?? new List<string>();
            var popup = new PopupField<string>(null, options, options.FirstOrDefault());

            popup.RegisterValueChangedCallback(evt =>
            {
                var instance = instanceProvider();
                var prop = instance?.GetEnumProperty(path);
                if (prop != null)
                {
                    prop.Value = evt.newValue;
                }
            });

            bindingList.Add(new PropertyBinding
            {
                Path = cacheKey,
                Control = popup,
                Sync = instance =>
                {
                    var currentInstance = instanceProvider();
                    var prop = currentInstance?.GetEnumProperty(path);
                    bool hasProp = prop != null;
                    popup.SetEnabled(hasProp);

                    if (!hasProp)
                    {
                        return;
                    }

                    var currentValue = prop.Value;
                    if (!options.Contains(currentValue))
                    {
                        options = prop.EnumValues?.ToList() ?? options;
                        popup.choices = options;
                    }

                    if (currentValue != null)
                    {
                        popup.SetValueWithoutNotify(currentValue);
                    }
                }
            });

            parent.Add(CreatePropertyCard(property.Name, pathLabelOverride ?? path, $"Enum ({property.EnumTypeName})", GetDocUrl(ViewModelDataType.Enum),
                null, popup));
        }

        private ArtboardSelection GetOrCreateArtboardSelection(string path)
        {
            if (!m_artboardSelectionCache.TryGetValue(path, out var selection))
            {
                selection = new ArtboardSelection();
                m_artboardSelectionCache[path] = selection;
            }
            return selection;
        }

        private void DisposeArtboardSelection(string path)
        {
            if (m_artboardSelectionCache.TryGetValue(path, out var selection))
            {
                selection.File = null;
            }
        }

        private void AddArtboardField(VisualElement parent, string path, string displayName, Func<ViewModelInstance> instanceProvider = null, string cacheKey = null, string pathLabelOverride = null, List<PropertyBinding> bindingList = null)
        {
            instanceProvider ??= GetCurrentInstance;
            cacheKey ??= path;
            bindingList ??= m_propertyBindings;

            var selection = GetOrCreateArtboardSelection(cacheKey);

            var assetField = new ObjectField
            {
                objectType = typeof(Asset),
                allowSceneObjects = false,
                label = "Rive File",
                tooltip = "Select the Rive file containing the desired artboard to bind"
            };

            assetField.style.marginLeft = 0;


            var artboardDropdown = new DropdownField
            {
                choices = new List<string>(),
                label = "Artboard",
                tooltip = "Select the artboard to bind from the selected Rive file",
                value = null
            };

            artboardDropdown.style.marginLeft = 0;

            void ApplySelectionToProperty()
            {
                var instance = instanceProvider();
                var prop = instance?.GetArtboardProperty(path);
                if (prop == null)
                {
                    return;
                }

                if (selection.File == null || string.IsNullOrEmpty(selection.ArtboardName))
                {
                    prop.Value = null;
                    return;
                }

                var bindable = selection.File.BindableArtboard(selection.ArtboardName);
                if (bindable != null)
                {
                    prop.Value = bindable;
                }
            }

            void PopulateArtboards(Asset asset)
            {
                artboardDropdown.choices = new List<string>();
                artboardDropdown.value = null;
                selection.ArtboardName = null;
                artboardDropdown.style.display = asset == null ? DisplayStyle.None : DisplayStyle.Flex;

                if (asset == null)
                {
                    return;
                }

                var names = asset.EditorOnlyMetadata?.GetArtboardNames() ?? Array.Empty<string>();
                artboardDropdown.choices = names.ToList();

                if (names.Length > 0)
                {
                    var chosenArtboard = selection.ArtboardName;
                    if (string.IsNullOrEmpty(chosenArtboard) || !names.Contains(chosenArtboard))
                    {
                        chosenArtboard = names[0];
                    }
                    selection.ArtboardName = chosenArtboard;
                    artboardDropdown.value = chosenArtboard;
                }
            }

            assetField.RegisterValueChangedCallback(evt =>
            {
                var newAsset = evt.newValue as Asset;
                if (!ReferenceEquals(selection.Asset, newAsset))
                {
                    DisposeArtboardSelection(cacheKey);
                    selection.Asset = newAsset;
                    selection.File = null;
                    selection.ArtboardName = null;
                }

                PopulateArtboards(newAsset);

                if (newAsset != null)
                {
                    selection.File = File.Load(newAsset);
                }

                ApplySelectionToProperty();
            });

            artboardDropdown.RegisterValueChangedCallback(evt =>
            {
                selection.ArtboardName = evt.newValue;
                ApplySelectionToProperty();
            });

            bindingList.Add(new PropertyBinding
            {
                Path = cacheKey,
                Control = artboardDropdown,
                Sync = instance =>
                {
                    var current = instanceProvider();
                    var prop = current?.GetArtboardProperty(path);
                    bool hasProp = prop != null;
                    assetField.SetEnabled(hasProp);
                    artboardDropdown.SetEnabled(hasProp && selection.Asset != null);
                    artboardDropdown.style.display = selection.Asset == null ? DisplayStyle.None : DisplayStyle.Flex;

                    if (assetField.value != selection.Asset)
                    {
                        assetField.SetValueWithoutNotify(selection.Asset);
                    }

                    // Ensure file is loaded if we have an asset but no file yet
                    if (hasProp && selection.Asset != null && selection.File == null)
                    {
                        selection.File = File.Load(selection.Asset);
                    }

                    // Populate dropdown if empty but we have asset metadata
                    if (artboardDropdown.choices.Count == 0 && selection.Asset != null)
                    {
                        PopulateArtboards(selection.Asset);
                    }

                    // Keep dropdown selection in sync with cached choice
                    if (selection.ArtboardName != null && artboardDropdown.value != selection.ArtboardName)
                    {
                        artboardDropdown.SetValueWithoutNotify(selection.ArtboardName);
                    }
                }
            });

            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Column;
            container.Add(assetField);
            container.Add(artboardDropdown);

            parent.Add(CreatePropertyCard(displayName, pathLabelOverride ?? path, "Artboard", GetDocUrl(ViewModelDataType.Artboard),
                null, container));
        }

        private void AddTriggerField(VisualElement parent, string path, string displayName, Func<ViewModelInstance> instanceProvider = null, string cacheKey = null, string pathLabelOverride = null, List<PropertyBinding> bindingList = null)
        {
            instanceProvider ??= GetCurrentInstance;
            cacheKey ??= path;
            bindingList ??= m_propertyBindings;

            var button = new Button(() =>
            {
                var instance = instanceProvider();
                var prop = instance?.GetTriggerProperty(path);
                prop?.Trigger();
            })
            {
                text = $"Fire Trigger"
            };

            bindingList.Add(new PropertyBinding
            {
                Path = cacheKey,
                Control = button,
                Sync = instance =>
                {
                    var current = instanceProvider();
                    var prop = current?.GetTriggerProperty(path);
                    button.SetEnabled(prop != null);
                }
            });

            parent.Add(CreatePropertyCard(displayName, pathLabelOverride ?? path, "Trigger", GetDocUrl(ViewModelDataType.Trigger),
                null, button));
        }

        private void AddListField(
            VisualElement parent,
            string path,
            string displayName,
            Func<ViewModelInstance> instanceProvider = null,
            string cacheKey = null,
            string pathLabelOverride = null)
        {
            instanceProvider ??= GetCurrentInstance;
            cacheKey ??= path;

            var listContainer = new VisualElement();
            listContainer.style.flexDirection = FlexDirection.Column;

            var vmNames = m_fileMetadata?.ViewModels?
                .Select(vm => vm.Name)
                .Where(n => !string.IsNullOrEmpty(n))
                .Distinct()
                .ToList() ?? new List<string>();

            var typeRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            var initialType = vmNames.FirstOrDefault();
            var typeDropdown = new DropdownField("New Item Type", vmNames, initialType)
            {
                tooltip = "Select the ViewModel type for newly added list items"
            };

            typeDropdown.style.flexGrow = 1;
            typeDropdown.style.flexShrink = 1;
            typeDropdown.style.marginRight = 6;
            typeDropdown.style.marginBottom = 6;


            typeRow.Add(typeDropdown);
            typeRow.style.marginBottom = 4;
            listContainer.Add(typeRow);

            var itemBindings = new Dictionary<int, List<PropertyBinding>>();

            ViewModelInstance CreateInstanceForList()
            {
                string targetType = typeDropdown.value;
                var listProp = instanceProvider()?.GetListProperty(path);
                if (string.IsNullOrEmpty(targetType))
                {
                    targetType = listProp?.Count > 0 ? listProp.GetInstanceAt(0)?.ViewModelName : null;
                }

                var vm = !string.IsNullOrEmpty(targetType) ? m_widget?.File?.GetViewModelByName(targetType) : null;
                return vm?.CreateInstance();
            }

            var listView = new ListView
            {
                reorderable = true,
                showAddRemoveFooter = true,
                virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
                selectionType = SelectionType.Single
            };
            listView.style.flexGrow = 1;
            listView.style.minHeight = 80;

            // Adapter that proxies directly to the live list property to avoid stale counts
            var listAdapter = new ListPropertyAdapter(
                () => instanceProvider()?.GetListProperty(path),
                CreateInstanceForList);
            listView.itemsSource = listAdapter;

            listView.makeItem = () =>
            {
                var root = new VisualElement();
                root.style.flexDirection = FlexDirection.Column;
                root.style.paddingTop = 6;
                root.style.paddingBottom = 6;
                root.style.paddingLeft = 4;
                root.style.paddingRight = 4;
                root.style.borderBottomWidth = 1;
                root.style.borderBottomColor = new UnityEngine.Color(0.25f, 0.25f, 0.25f, 0.25f);
                return root;
            };

            listView.bindItem = (element, index) =>
            {
                element.Clear();
                var bindings = new List<PropertyBinding>();
                itemBindings[index] = bindings;

                var listProp = instanceProvider()?.GetListProperty(path);
                var instance = (listProp != null && index >= 0 && index < listProp.Count) ? listProp.GetInstanceAt(index) : null;

                var header = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
                var title = new Label(instance?.ViewModelName ?? "Item");
                title.style.unityFontStyleAndWeight = FontStyle.Bold;
                title.style.flexGrow = 1;
                //header.Add(title);
                element.Add(header);

                if (instance == null)
                {
                    element.Add(new HelpBox("List item is null.", HelpBoxMessageType.Warning));
                    return;
                }

                var meta = FindViewModelMetadata(instance.ViewModelName);
                if (meta == null)
                {
                    element.Add(new HelpBox($"No metadata found for '{instance.ViewModelName}'.", HelpBoxMessageType.Warning));
                    return;
                }

                BuildViewModelSection(meta, string.Empty, element, 1, () => instance, $"{path}[{index}]", $"{path}[{index}]", bindings);
            };

            listContainer.Add(listView);

            var listBinding = new ListPropertyBinding
            {
                Path = cacheKey,
                Control = listContainer,
            };

            listBinding.Sync = _ =>
            {
                var listProp = instanceProvider()?.GetListProperty(path);
                bool hasProp = listProp != null;
                listView.SetEnabled(hasProp);
                typeDropdown.SetEnabled(hasProp);

                if (!hasProp)
                {
                    itemBindings.Clear();
                    listView.itemsSource = listAdapter;
                    listView.Rebuild();
                    listView.ClearSelection();
                    listBinding.LastItems.Clear();
                    listBinding.InitializedTypeSelection = false;
                    return;
                }

                // Detect structural changes to avoid unnecessary rebuilds that steal focus
                var currentItems = new List<ViewModelInstance>(listProp.Count);
                for (int i = 0; i < listProp.Count; i++)
                {
                    currentItems.Add(listProp.GetInstanceAt(i));
                }

                bool structureChanged = currentItems.Count != listBinding.LastItems.Count;
                if (!structureChanged)
                {
                    for (int i = 0; i < currentItems.Count; i++)
                    {
                        if (!ReferenceEquals(currentItems[i], listBinding.LastItems[i]))
                        {
                            structureChanged = true;
                            break;
                        }
                    }
                }

                if (structureChanged)
                {
                    itemBindings.Clear();
                    listView.itemsSource = listAdapter;
                    listView.Rebuild();
                    listView.ClearSelection();
                    listBinding.LastItems = currentItems;
                }
                else
                {
                    listBinding.LastItems = currentItems;
                    if (listView.selectedIndex >= listProp.Count)
                    {
                        listView.ClearSelection();
                    }
                }


                string inferred = listProp.Count > 0 ? listProp.GetInstanceAt(0)?.ViewModelName : null;

                if (!listBinding.InitializedTypeSelection)
                {
                    if (!string.IsNullOrEmpty(inferred) && typeDropdown.choices.Contains(inferred))
                    {
                        typeDropdown.SetValueWithoutNotify(inferred);
                    }
                    else if (string.IsNullOrEmpty(typeDropdown.value) && typeDropdown.choices.Count > 0)
                    {
                        typeDropdown.SetValueWithoutNotify(typeDropdown.choices[0]);
                    }

                    listBinding.InitializedTypeSelection = true;
                }
                else
                {
                    bool selectionMissing = string.IsNullOrEmpty(typeDropdown.value) || !typeDropdown.choices.Contains(typeDropdown.value);
                    if (selectionMissing && !string.IsNullOrEmpty(inferred) && typeDropdown.choices.Contains(inferred))
                    {
                        typeDropdown.SetValueWithoutNotify(inferred);
                    }
                }

                foreach (var kvp in itemBindings)
                {
                    var idx = kvp.Key;
                    if (idx < 0 || idx >= listProp.Count)
                    {
                        continue;
                    }

                    var instance = listProp.GetInstanceAt(idx);
                    foreach (var binding in kvp.Value)
                    {
                        binding.Sync?.Invoke(instance);
                    }
                }
            };

            m_listBindings.Add(listBinding);

            parent.Add(CreatePropertyCard(displayName, pathLabelOverride ?? path, "List", GetDocUrl(ViewModelDataType.List),
                null, listContainer, false));
        }

        private void AddUnsupportedLabel(VisualElement parent, string name, ViewModelDataType type)
        {
            var label = new Label($"This property type is not configurable in the playground.");
            label.style.color = new UnityEngine.Color(0.7f, 0.7f, 0.7f);
            parent.Add(CreatePropertyCard(name, name, type.ToString(), GetDocUrl(type),
                null, label));
        }

        private void RefreshValues()
        {
            if (m_isRefreshing)
            {
                return;
            }

            m_isRefreshing = true;
            var instance = GetCurrentInstance();
            try
            {
                foreach (var binding in m_propertyBindings.ToList())
                {
                    binding.Sync?.Invoke(instance);
                }

                foreach (var listBinding in m_listBindings.ToList())
                {
                    listBinding.Sync?.Invoke(instance);
                }
            }
            finally
            {
                m_isRefreshing = false;
            }
        }

        private void UpdateVisibility()
        {
            string message;
            var state = GetPlaygroundState(out message);
            bool ready = state == PlaygroundState.Ready;

            if (m_playModeHelpBox != null)
            {
                m_playModeHelpBox.text = message;
                m_playModeHelpBox.style.display = ready ? DisplayStyle.None : DisplayStyle.Flex;
            }

            if (m_widgetField != null)
            {
                // Only show the widget selector while in Play Mode
                m_widgetField.style.display = EditorApplication.isPlaying ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (m_interactiveContainer != null)
            {
                m_interactiveContainer.style.display = ready ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private PlaygroundState GetPlaygroundState(out string message)
        {
            if (!EditorApplication.isPlaying)
            {
                message = "Enter Play Mode to update values in the widget.";
                return PlaygroundState.NotPlaying;
            }

            if (m_widget == null)
            {
                message = "Select a RiveWidget to get started.";
                return PlaygroundState.NoWidget;
            }

            if (m_fileMetadata == null)
            {
                message = "Selected Rive file has no metadata yet. Reimport or select a Rive asset.";
                return PlaygroundState.NoFileMetadata;
            }

            if (m_fileMetadata.ViewModels == null || m_fileMetadata.ViewModels.Count == 0)
            {
                message = "This Rive file has no ViewModels. Add data binding in the Rive Editor to use the playground.";
                return PlaygroundState.NoViewModels;
            }

            if (m_artboardMetadata == null)
            {
                message = "No artboard metadata found for the selected widget.";
                return PlaygroundState.NoArtboardMetadata;
            }

            if (m_artboardMetadata.DefaultViewModel == null)
            {
                message = "The current artboard has no default ViewModel.";
                return PlaygroundState.NoDefaultViewModel;
            }

            if (m_widget.Status != WidgetStatus.Loaded || m_widget.StateMachine == null)
            {
                message = "Widget is not loaded yet.";
                return PlaygroundState.WidgetNotLoaded;
            }

            if (m_widget.StateMachine.ViewModelInstance == null)
            {
                message = "No ViewModel instance bound to the state machine.";
                return PlaygroundState.NoViewModelInstance;
            }

            message = string.Empty;
            return PlaygroundState.Ready;
        }

        private Button CreateMoreActionsButton(string path, string displayName)
        {
            var button = new Button(() =>
            {
                var menu = new GenericMenu();
                bool hasIndexPath = !string.IsNullOrEmpty(path) && path.Contains("[");
                if (!string.IsNullOrEmpty(path) && !hasIndexPath)
                {
                    menu.AddItem(new GUIContent("Copy Path"), false, () =>
                    {
                        EditorGUIUtility.systemCopyBuffer = path;
                    });
                }
                else
                {
                    string label = hasIndexPath ? "Copy Path (not supported for list items)" : "Copy Path";
                    menu.AddDisabledItem(new GUIContent(label));
                }

                string nameOnly = !string.IsNullOrEmpty(path) ? path.Split('/').Last() : displayName;
                menu.AddItem(new GUIContent("Copy Name"), false, () =>
                {
                    EditorGUIUtility.systemCopyBuffer = nameOnly;
                });

                menu.ShowAsContext();
            })
            {
                text = "⋮",
                tooltip = "More actions",
            };
            button.style.width = 26;
            button.style.marginLeft = 4;
            button.style.height = 20;
            button.style.backgroundColor = new UnityEngine.Color(0, 0, 0, 0);
            button.style.borderLeftWidth = 0;
            button.style.borderRightWidth = 0;
            button.style.borderTopWidth = 0;
            button.style.borderBottomWidth = 0;
            button.style.fontSize = 14;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;

            button.RegisterCallback<MouseEnterEvent>(_ =>
            {
                button.style.backgroundColor = new UnityEngine.Color(0.345f, 0.345f, 0.345f, 0.6f);
            });

            button.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                button.style.backgroundColor = new UnityEngine.Color(0, 0, 0, 0);
            });

            return button;
        }

        private void StyleLinkLabel(Label label, string url)
        {
            label.style.fontSize = 11;
            var linkColor = new UnityEngine.Color(0.55f, 0.78f, 1f, 1f);
            label.style.color = linkColor;
            label.style.marginLeft = 6;
            // We keep the border width constant to avoid layout shift; toggle only the color.
            label.style.borderBottomWidth = 1;
            label.style.borderBottomColor = new StyleColor(new UnityEngine.Color(0, 0, 0, 0));

            label.RegisterCallback<MouseEnterEvent>(_ =>
            {
                label.style.borderBottomColor = new StyleColor(linkColor);
            });

            label.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                label.style.borderBottomColor = new StyleColor(new UnityEngine.Color(0, 0, 0, 0));
            });

            label.RegisterCallback<MouseUpEvent>(_ => Application.OpenURL(url));
        }

        private ViewModelInstance GetCurrentInstance()
        {
            return m_widget?.StateMachine?.ViewModelInstance;
        }

        private void BuildViewModelSection(
            FileMetadata.ViewModelMetadata viewModel,
            string accessPathPrefix,
            VisualElement parent,
            int depth,
            Func<ViewModelInstance> instanceProvider = null,
            string displayPathPrefix = null,
            string cachePathPrefix = null,
            List<PropertyBinding> bindingList = null)
        {
            instanceProvider ??= GetCurrentInstance;
            bindingList ??= m_propertyBindings;

            string resolvedDisplayPrefix = displayPathPrefix ?? accessPathPrefix;
            string resolvedCachePrefix = cachePathPrefix ?? accessPathPrefix;
            string expansionKey = string.IsNullOrEmpty(resolvedCachePrefix) ? "(root)" : resolvedCachePrefix;

            string vmLabel = string.IsNullOrEmpty(accessPathPrefix)
                ? (string.IsNullOrEmpty(viewModel.Name) ? "Default ViewModel" : viewModel.Name)
                : accessPathPrefix.Split('/').Last();

            string viewModelNameLabel = string.IsNullOrEmpty(viewModel.Name) ? null : viewModel.Name;
            string pathLabel = string.IsNullOrEmpty(resolvedDisplayPrefix) ? "(root)" : resolvedDisplayPrefix;
            int childCount = viewModel.Properties?.Count ?? 0;

            bool expanded = m_viewModelExpansion.TryGetValue(expansionKey, out var savedExpanded)
                ? savedExpanded
                : depth == 0;

            var header = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };

            var expander = new Button();
            expander.style.width = 20;
            expander.style.height = 20;
            expander.style.paddingLeft = 0;
            expander.style.paddingRight = 0;
            expander.style.marginRight = 3;
            expander.text = expanded ? "▾" : "▸";
            expander.tooltip = "Expand / collapse view model";
            expander.style.fontSize = 30;
            expander.style.backgroundColor = new UnityEngine.Color(0, 0, 0, 0);
            expander.style.borderLeftWidth = 0;
            expander.style.borderRightWidth = 0;
            expander.style.borderTopWidth = 0;
            expander.style.borderBottomWidth = 0;

            var vmName = new Label(vmLabel) { style = { unityFontStyleAndWeight = FontStyle.Bold, flexGrow = 1 } };

            var vmPill = new Label(string.IsNullOrEmpty(viewModelNameLabel)
                ? "View Model"
                : $"View Model ({viewModelNameLabel})");
            vmPill.style.unityTextAlign = TextAnchor.MiddleCenter;
            vmPill.style.paddingLeft = 6;
            vmPill.style.paddingRight = 6;
            vmPill.style.paddingTop = 2;
            vmPill.style.paddingBottom = 2;
            vmPill.style.marginLeft = 4;
            vmPill.style.borderTopLeftRadius = 4;
            vmPill.style.borderTopRightRadius = 4;
            vmPill.style.borderBottomLeftRadius = 4;
            vmPill.style.borderBottomRightRadius = 4;
            vmPill.style.backgroundColor = new UnityEngine.Color(0.25f, 0.25f, 0.35f, 0.9f);
            vmPill.style.color = new UnityEngine.Color(0.9f, 0.9f, 1f, 1f);

            var countLabel = new Label($"{childCount} properties");
            countLabel.style.marginLeft = 6;
            countLabel.style.fontSize = 11;
            countLabel.style.color = new UnityEngine.Color(0.8f, 0.8f, 0.85f, 0.9f);

            header.Add(expander);
            header.Add(vmName);
            header.Add(vmPill);
            header.Add(countLabel);
            header.Add(CreateMoreActionsButton(resolvedDisplayPrefix, vmLabel));


            var container = new VisualElement();
            container.style.marginLeft = 12;
            container.style.marginTop = 8;
            container.style.marginBottom = 8;
            container.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
            container.style.flexDirection = FlexDirection.Column;

            foreach (var property in viewModel.Properties)
            {
                string propertyAccessPath = string.IsNullOrEmpty(accessPathPrefix)
                    ? property.Name
                    : $"{accessPathPrefix}/{property.Name}";
                string propertyDisplayPath = string.IsNullOrEmpty(resolvedDisplayPrefix)
                    ? property.Name
                    : $"{resolvedDisplayPrefix}/{property.Name}";
                string propertyCachePath = string.IsNullOrEmpty(resolvedCachePrefix)
                    ? propertyAccessPath
                    : $"{resolvedCachePrefix}/{property.Name}";

                switch (property.Type)
                {
                    case ViewModelDataType.String:
                        AddStringField(container, propertyAccessPath, property.Name, instanceProvider, propertyCachePath, propertyDisplayPath, bindingList);
                        break;
                    case ViewModelDataType.Number:
                        AddNumberField(container, propertyAccessPath, property.Name, instanceProvider, propertyCachePath, propertyDisplayPath, bindingList);
                        break;
                    case ViewModelDataType.Boolean:
                        AddBooleanField(container, propertyAccessPath, property.Name, instanceProvider, propertyCachePath, propertyDisplayPath, bindingList);
                        break;
                    case ViewModelDataType.Color:
                        AddColorField(container, propertyAccessPath, property.Name, instanceProvider, propertyCachePath, propertyDisplayPath, bindingList);
                        break;
                    case ViewModelDataType.AssetImage:
                        AddImageField(container, propertyAccessPath, property.Name, instanceProvider, propertyCachePath, propertyDisplayPath, bindingList);
                        break;
                    case ViewModelDataType.Artboard:
                        AddArtboardField(container, propertyAccessPath, property.Name, instanceProvider, propertyCachePath, propertyDisplayPath, bindingList);
                        break;
                    case ViewModelDataType.Enum:
                        AddEnumField(container, propertyAccessPath, property, instanceProvider, propertyCachePath, propertyDisplayPath, bindingList);
                        break;
                    case ViewModelDataType.Trigger:
                        AddTriggerField(container, propertyAccessPath, property.Name, instanceProvider, propertyCachePath, propertyDisplayPath, bindingList);
                        break;
                    case ViewModelDataType.ViewModel:
                        var nestedMeta = FindViewModelMetadata(property.NestedViewModelName);
                        if (nestedMeta != null)
                        {
                            BuildViewModelSection(nestedMeta, propertyAccessPath, container, depth + 1, instanceProvider, propertyDisplayPath, propertyCachePath, bindingList);
                        }
                        else
                        {
                            var missingLabel = new Label($"Nested view model '{property.NestedViewModelName}' not found.");
                            missingLabel.style.color = UnityEngine.Color.yellow;
                            container.Add(missingLabel);
                        }
                        break;
                    case ViewModelDataType.List:
                        AddListField(container, propertyAccessPath, property.Name, instanceProvider, propertyCachePath, propertyDisplayPath);
                        break;
                    case ViewModelDataType.ListIndex:
                        AddUnsupportedLabel(container, property.Name, property.Type);
                        break;
                    default:
                        AddUnsupportedLabel(container, property.Name, property.Type);
                        break;
                }
            }

            void ToggleExpanded()
            {
                expanded = !expanded;
                container.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
                expander.text = expanded ? "▾" : "▸";
                m_viewModelExpansion[expansionKey] = expanded;
            }

            expander.clicked += ToggleExpanded;

            // wrap in a card for clarity
            var vmCard = new VisualElement();
            vmCard.style.marginTop = depth == 0 ? 8 : 4;
            vmCard.style.paddingTop = 8;
            vmCard.style.paddingBottom = 8;
            vmCard.style.paddingLeft = 8;
            vmCard.style.paddingRight = 8;
            vmCard.style.borderTopWidth = 1;
            vmCard.style.borderBottomWidth = 1;
            vmCard.style.borderLeftWidth = 1;
            vmCard.style.borderRightWidth = 1;
            vmCard.style.borderTopColor = new UnityEngine.Color(0.344f, 0.344f, 0.349f, 0.5f);
            vmCard.style.borderBottomColor = new UnityEngine.Color(0.344f, 0.344f, 0.349f, 0.5f);
            vmCard.style.borderLeftColor = new UnityEngine.Color(0.344f, 0.344f, 0.349f, 0.5f);
            vmCard.style.borderRightColor = new UnityEngine.Color(0.344f, 0.344f, 0.349f, 0.5f);
            vmCard.style.backgroundColor = new UnityEngine.Color(0.145f, 0.145f, 0.152f, 0.55f);
            vmCard.style.borderTopLeftRadius = 6;
            vmCard.style.borderTopRightRadius = 6;
            vmCard.style.borderBottomLeftRadius = 6;
            vmCard.style.borderBottomRightRadius = 6;

            vmCard.Add(header);
            vmCard.Add(container);

            parent.Add(vmCard);
        }

        private string GetDocUrl(ViewModelDataType type)
        {
            switch (type)
            {
                case ViewModelDataType.String:
                    return InspectorDocLinks.UnityDataBindingProperties;
                case ViewModelDataType.Number:
                    return InspectorDocLinks.UnityDataBindingProperties;
                case ViewModelDataType.Boolean:
                    return InspectorDocLinks.UnityDataBindingProperties;
                case ViewModelDataType.Color:
                    return InspectorDocLinks.UnityDataBindingProperties;
                case ViewModelDataType.Trigger:
                    return InspectorDocLinks.UnityDataBindingProperties;
                case ViewModelDataType.Enum:
                    return InspectorDocLinks.UnityDataBindingEnums;
                case ViewModelDataType.ViewModel:
                    return InspectorDocLinks.UnityDataBindingViewModel;
                case ViewModelDataType.AssetImage:
                    return InspectorDocLinks.UnityDataBindingImages;
                case ViewModelDataType.List:
                    return InspectorDocLinks.UnityDataBindingLists;
                case ViewModelDataType.ListIndex:
                    return InspectorDocLinks.UnityDataBindingListViewModelIndex;
                case ViewModelDataType.Artboard:
                    return InspectorDocLinks.UnityDataBindingArtboards;
                default:
                    return InspectorDocLinks.UnityDataBinding;
            }
        }

        private VisualElement CreatePropertyCard(string displayName, string path, string typeLabel, string docUrl, string description, VisualElement control, bool showTypePill = false)
        {
            var card = new VisualElement();
            card.style.marginTop = 6;
            card.style.paddingTop = 8;
            card.style.paddingBottom = 8;
            card.style.paddingLeft = 10;
            card.style.paddingRight = 10;
            card.style.borderTopLeftRadius = 6;
            card.style.borderTopRightRadius = 6;
            card.style.borderBottomLeftRadius = 6;
            card.style.borderBottomRightRadius = 6;
            card.style.borderBottomWidth = 1;
            card.style.borderTopWidth = 1;
            card.style.borderLeftWidth = 1;
            card.style.borderRightWidth = 1;
            card.style.borderBottomColor = new UnityEngine.Color(0.25f, 0.25f, 0.25f, 0.4f);
            card.style.borderTopColor = new UnityEngine.Color(0.25f, 0.25f, 0.25f, 0.4f);
            card.style.borderLeftColor = new UnityEngine.Color(0.25f, 0.25f, 0.25f, 0.4f);
            card.style.borderRightColor = new UnityEngine.Color(0.25f, 0.25f, 0.25f, 0.4f);
            card.style.backgroundColor = new UnityEngine.Color(0.12f, 0.12f, 0.12f, 0.45f);

            var header = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            var nameLabel = new Label(displayName);
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.flexGrow = 1;

            if (showTypePill && !string.IsNullOrEmpty(typeLabel))
            {
                var typePill = new Label(typeLabel);
                typePill.style.unityTextAlign = TextAnchor.MiddleCenter;
                typePill.style.paddingLeft = 6;
                typePill.style.paddingRight = 6;
                typePill.style.paddingTop = 2;
                typePill.style.paddingBottom = 2;
                typePill.style.marginLeft = 4;
                typePill.style.borderTopLeftRadius = 4;
                typePill.style.borderTopRightRadius = 4;
                typePill.style.borderBottomLeftRadius = 4;
                typePill.style.borderBottomRightRadius = 4;
                typePill.style.backgroundColor = new UnityEngine.Color(0.25f, 0.25f, 0.25f, 0.8f);
                typePill.style.color = new UnityEngine.Color(0.85f, 0.85f, 0.85f, 1f);
                header.Add(typePill);
            }

            var menuButton = CreateMoreActionsButton(path, displayName);

            // Remove potential parenthesis text to account for enum values. E.g. "Enum (MyEnum)" should be "Enum"
            string strippedTypeLabel = typeLabel;
            if (!string.IsNullOrEmpty(strippedTypeLabel))
            {
                int parenIndex = strippedTypeLabel.IndexOf(" (", StringComparison.Ordinal);
                if (parenIndex > 0)
                {
                    strippedTypeLabel = strippedTypeLabel.Substring(0, parenIndex);
                }
            }

            string docLabel = string.IsNullOrEmpty(typeLabel) ? "Docs" : $"{strippedTypeLabel} Property Documentation";
            var docButton = new Label(docLabel);
            StyleLinkLabel(docButton, docUrl);
            docButton.style.marginLeft = 6;
            docButton.style.alignSelf = Align.Center;
            docButton.style.display = DisplayStyle.None;

            header.Add(nameLabel);
            header.Add(docButton);
            header.Add(menuButton);

            card.Add(header);

            card.RegisterCallback<MouseEnterEvent>(_ =>
            {
                docButton.style.display = DisplayStyle.Flex;
            });

            card.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                docButton.style.display = DisplayStyle.None;
            });

            string pathLineText = null;
            if (!showTypePill)
            {
                pathLineText = $"{typeLabel}";
            }
            else if (!string.IsNullOrEmpty(path))
            {
                pathLineText = $"Path: {path}";
            }

            if (!string.IsNullOrEmpty(pathLineText))
            {
                var pathLabel = new Label(pathLineText);
                pathLabel.style.fontSize = 11;
                pathLabel.style.color = new UnityEngine.Color(0.75f, 0.75f, 0.75f, 0.9f);
                pathLabel.style.marginTop = 2;
                card.Add(pathLabel);
            }

            if (!string.IsNullOrEmpty(description))
            {
                var desc = new Label(description);
                desc.style.fontSize = 11;
                desc.style.color = new UnityEngine.Color(0.8f, 0.8f, 0.8f, 0.95f);
                desc.style.marginTop = 4;
                card.Add(desc);
            }

            control.style.marginTop = 6;
            card.Add(control);

            return card;
        }
    }
}

