using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rive.EditorTools
{
    /// <summary>
    /// Shows a welcome popup when the Unity Editor opens (once per session) unless the user opts out.
    /// Also available through the Rive menu for later reference.
    /// </summary>
    internal class RiveQuickStartWindow : EditorWindow
    {
        private const string ShowOnStartKey = "Rive.Editor.Welcome.ShowOnStart";
        private const string SessionShownKey = "Rive.Editor.Welcome.ShownThisSession";
        private string _version;

        private static void ShowFromMenu()
        {
            CreateWindow(string.Empty).ShowUtility();
        }

        [MenuItem("Window/Rive/Quick Start", priority = 1000)]
        private static void ShowFromToolsMenu()
        {
            ShowFromMenu();
        }

        [InitializeOnLoadMethod]
        private static void ShowOnEditorOpen()
        {
            if (!IsShowOnStart())
            {
                return;
            }

            if (SessionState.GetBool(SessionShownKey, false))
            {
                return;
            }

            SessionState.SetBool(SessionShownKey, true);
            // Delay to ensure the editor UI is fully initialized before showing.
            EditorApplication.delayCall += () =>
            {
                var window = CreateWindow(string.Empty);
                window.ShowUtility();
                window.Focus();
            };
        }

        internal static bool IsShowOnStart()
        {
            return EditorPrefs.GetBool(ShowOnStartKey, true);
        }

        private static void SetShowOnStart(bool show)
        {
            EditorPrefs.SetBool(ShowOnStartKey, show);
        }

        private static RiveQuickStartWindow CreateWindow(string version)
        {
            var window = CreateInstance<RiveQuickStartWindow>();
            window._version = version;
            window.titleContent = new GUIContent("Rive for Unity");
            window.minSize = new Vector2(420, 520);
            return window;
        }

        public void CreateGUI()
        {
            BuildUI();
        }

        private void BuildUI()
        {
            var root = rootVisualElement;
            root.Clear();
            root.style.flexDirection = FlexDirection.Column;
            root.style.paddingLeft = 16;
            root.style.paddingRight = 16;
            root.style.paddingTop = 12;
            root.style.paddingBottom = 12;

            var scroll = new ScrollView { verticalScrollerVisibility = ScrollerVisibility.Auto };
            scroll.style.flexGrow = 1;
            scroll.style.marginTop = 6;
            root.Add(scroll);

            var logo = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/app.rive.rive-unity/Editor/Images/welcome-banner.jpg");
            if (logo != null)
            {
                var logoContainer = new VisualElement();
                logoContainer.style.paddingTop = 0;
                logoContainer.style.paddingBottom = 0;
                logoContainer.style.paddingLeft = 0;
                logoContainer.style.paddingRight = 0;
                logoContainer.style.alignItems = Align.Center;
                logoContainer.style.marginBottom = 18;
                logoContainer.style.maxHeight = 200;

                var logoImage = new Image
                {
                    image = logo,
                    scaleMode = ScaleMode.ScaleAndCrop
                };

                logoImage.style.alignSelf = Align.Center;

                logoImage.style.borderTopLeftRadius = 7;
                logoImage.style.borderTopRightRadius = 7;
                logoImage.style.borderBottomLeftRadius = 7;
                logoImage.style.borderBottomRightRadius = 7;

                logoImage.style.overflow = Overflow.Hidden;

                logoContainer.Add(logoImage);
                scroll.Add(logoContainer);
            }

            var subtitle = new Label("Rive is a new way to build menus, HUDs, and 2D graphics for games, with rich interactivity and state-driven animation.");
            subtitle.style.whiteSpace = WhiteSpace.Normal;
            subtitle.style.marginBottom = 12;
            scroll.Add(subtitle);



            scroll.Add(CreateSection(
                null,
                ("Getting Started Guide", InspectorDocLinks.UnityGettingStarted, "Open the getting started guide for the Unity runtime"),
                ("Website", InspectorDocLinks.RiveWebsite, "Open the Rive website"),
                ("Support", InspectorDocLinks.RiveUnitySupport, "Open the issue tracker for the Rive Unity runtime")
            ));
            var footer = new VisualElement();
            footer.style.flexShrink = 0;
            footer.style.marginTop = 8;
            footer.style.paddingTop = 8;
            footer.style.borderTopWidth = 1;
            footer.style.borderTopColor = new UnityEngine.Color(0f, 0f, 0f, 0.1f);
            root.Add(footer);

            var autoShowToggle = new Toggle("Show this window at startup")
            {
                value = IsShowOnStart()
            };
            autoShowToggle.RegisterValueChangedCallback(evt =>
            {
                SetShowOnStart(evt.newValue);

            });
            autoShowToggle.style.marginBottom = 8;
            autoShowToggle.tooltip = "If enabled, this quick-start window will open when the editor starts (once per session).";
            footer.Add(autoShowToggle);

            var note = new Label("Reopen this window later via Window > Rive > Quick Start.");
            note.style.color = new UnityEngine.Color(0.45f, 0.45f, 0.45f);
            note.style.marginBottom = 4;
            note.style.marginLeft = 3;
            note.style.alignSelf = Align.FlexStart;
            footer.Add(note);

        }

        private VisualElement CreateSection(string title, params (string label, string url, string tooltip)[] links)
        {
            var container = new VisualElement();
            container.style.marginTop = 8;


            if (!String.IsNullOrEmpty(title))
            {
                var header = new Label(title);
                header.style.unityFontStyleAndWeight = FontStyle.Bold;
                header.style.fontSize = 13;
                header.style.marginBottom = 4;
                container.Add(header);
            }


            foreach (var (label, url, tooltip) in links)
            {
                if (string.IsNullOrEmpty(url))
                {
                    var suggestion = new Label($"• {label}");
                    suggestion.style.color = new UnityEngine.Color(0.55f, 0.55f, 0.55f);
                    suggestion.style.marginBottom = 4;
                    container.Add(suggestion);
                }
                else
                {
                    var btn = CreateLinkButton(label, url, tooltip);
                    btn.style.marginBottom = 4;
                    container.Add(btn);
                }
            }

            return container;
        }

        private Button CreateLinkButton(string text, string url, string tooltip = "")
        {
            var button = new Button();
            button.text = text;
            button.style.height = 28;
            button.style.justifyContent = Justify.FlexStart;
            button.style.paddingLeft = 10;
            if (!string.IsNullOrEmpty(tooltip))
            {
                button.tooltip = tooltip;
            }

            if (string.IsNullOrEmpty(url))
            {
                button.SetEnabled(false);
            }
            else
            {
                button.clicked += () => Application.OpenURL(url);
            }

            return button;
        }
    }
}
