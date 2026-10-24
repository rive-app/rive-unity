using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rive.EditorTools
{
    /// <summary>
    /// The look the Rive editor windows share: cards, pills, link labels and icon buttons.
    /// </summary>
    internal static class RiveEditorStyles
    {
        internal static readonly UnityEngine.Color CardBorder = new UnityEngine.Color(0.25f, 0.25f, 0.25f, 0.4f);
        internal static readonly UnityEngine.Color CardBackground = new UnityEngine.Color(0.12f, 0.12f, 0.12f, 0.45f);
        internal static readonly UnityEngine.Color PillBackground = new UnityEngine.Color(0.25f, 0.25f, 0.25f, 0.8f);
        internal static readonly UnityEngine.Color ViewModelPillBackground = new UnityEngine.Color(0.25f, 0.25f, 0.35f, 0.9f);
        internal static readonly UnityEngine.Color PillText = new UnityEngine.Color(0.85f, 0.85f, 0.85f, 1f);
        internal static readonly UnityEngine.Color SecondaryText = new UnityEngine.Color(0.75f, 0.75f, 0.75f, 0.9f);
        internal static readonly UnityEngine.Color SubtitleText = new UnityEngine.Color(0.8f, 0.8f, 0.8f, 0.95f);
        internal static readonly UnityEngine.Color Link = new UnityEngine.Color(0.55f, 0.78f, 1f, 1f);
        internal static readonly UnityEngine.Color IconHover = new UnityEngine.Color(0.345f, 0.345f, 0.345f, 0.6f);

        internal static void StyleCard(VisualElement card)
        {
            card.style.marginTop = 6;
            card.style.paddingTop = 8;
            card.style.paddingBottom = 8;
            card.style.paddingLeft = 10;
            card.style.paddingRight = 10;
            SetRadius(card, 6);
            card.style.borderBottomWidth = 1;
            card.style.borderTopWidth = 1;
            card.style.borderLeftWidth = 1;
            card.style.borderRightWidth = 1;
            card.style.borderBottomColor = CardBorder;
            card.style.borderTopColor = CardBorder;
            card.style.borderLeftColor = CardBorder;
            card.style.borderRightColor = CardBorder;
            card.style.backgroundColor = CardBackground;
        }

        internal static Label CreatePill(string text, UnityEngine.Color background)
        {
            var pill = new Label(text);
            pill.style.unityTextAlign = TextAnchor.MiddleCenter;
            pill.style.paddingLeft = 6;
            pill.style.paddingRight = 6;
            pill.style.paddingTop = 2;
            pill.style.paddingBottom = 2;
            pill.style.marginLeft = 4;
            SetRadius(pill, 4);
            pill.style.backgroundColor = background;
            pill.style.color = PillText;
            return pill;
        }

        internal static void StyleLinkLabel(Label label, Action onClick)
        {
            label.style.fontSize = 11;
            label.style.color = Link;
            label.style.marginLeft = 6;
            // The border width stays constant to avoid layout shift; only its color changes.
            label.style.borderBottomWidth = 1;
            label.style.borderBottomColor = new StyleColor(new UnityEngine.Color(0, 0, 0, 0));
            label.RegisterCallback<MouseEnterEvent>(_ => label.style.borderBottomColor = new StyleColor(Link));
            label.RegisterCallback<MouseLeaveEvent>(_ => label.style.borderBottomColor = new StyleColor(new UnityEngine.Color(0, 0, 0, 0)));
            label.RegisterCallback<MouseUpEvent>(_ => onClick?.Invoke());
        }

        internal static void StyleLinkLabel(Label label, string url)
        {
            StyleLinkLabel(label, () => Application.OpenURL(url));
        }

        /// A borderless button that shows a background on hover, like the "⋮" menu.
        internal static void StyleIconButton(Button button)
        {
            button.style.backgroundColor = new UnityEngine.Color(0, 0, 0, 0);
            button.style.borderLeftWidth = 0;
            button.style.borderRightWidth = 0;
            button.style.borderTopWidth = 0;
            button.style.borderBottomWidth = 0;
            button.RegisterCallback<MouseEnterEvent>(_ => button.style.backgroundColor = IconHover);
            button.RegisterCallback<MouseLeaveEvent>(_ => button.style.backgroundColor = new UnityEngine.Color(0, 0, 0, 0));
        }

        /// The muted line under a window's title, with an optional link on the right.
        internal static VisualElement CreateSubtitleRow(string text, Label link = null)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 14 } };
            var subtitle = new Label(text) { style = { flexGrow = 1, flexShrink = 1, fontSize = 12, color = SubtitleText, whiteSpace = WhiteSpace.Normal } };
            row.Add(subtitle);
            if (link != null)
            {
                row.Add(link);
            }
            return row;
        }

        internal static void SetRadius(VisualElement element, float radius)
        {
            element.style.borderTopLeftRadius = radius;
            element.style.borderTopRightRadius = radius;
            element.style.borderBottomLeftRadius = radius;
            element.style.borderBottomRightRadius = radius;
        }
    }
}
