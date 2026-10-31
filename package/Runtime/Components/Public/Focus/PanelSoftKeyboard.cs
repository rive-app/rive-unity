using UnityEngine;

namespace Rive.Components
{
    /// <summary>
    /// The on-screen keyboard Unity opens.
    /// </summary>
    internal interface ISoftKeyboard
    {
        bool IsSupported { get; }

        void Open(string text);

        void Close();

        /// <summary>
        /// Visible, or why it closed. Null once nothing's open.
        /// </summary>
        TouchScreenKeyboard.Status? Status { get; }

        string Text { get; set; }
    }

    internal sealed class UnitySoftKeyboard : ISoftKeyboard
    {
        private TouchScreenKeyboard m_keyboard;

        public bool IsSupported => TouchScreenKeyboard.isSupported;

        public void Open(string text)
        {
            // Rive draws the field, so Unity's own text box above the keyboard isn't needed.
            TouchScreenKeyboard.hideInput = true;
            m_keyboard = TouchScreenKeyboard.Open(text, TouchScreenKeyboardType.Default, autocorrection: false, multiline: false);
        }

        public void Close()
        {
            if (m_keyboard != null)
            {
                m_keyboard.active = false;
                m_keyboard = null;
            }
        }

        public TouchScreenKeyboard.Status? Status => m_keyboard?.status;

        public string Text
        {
            get => m_keyboard?.text ?? "";
            set
            {
                if (m_keyboard != null)
                {
                    m_keyboard.text = value;
                    m_keyboard.selection = new RangeInt(value.Length, 0);
                }
            }
        }
    }

    /// <summary>
    /// The on-screen keyboard while a Rive text field has focus and Unity has the panel selected.
    /// Opens when a field takes focus or is tapped, and closes when focus leaves it.
    /// </summary>
    /// <remarks>
    /// Rive owns the text and the caret, so the keyboard only holds a placeholder. What the user
    /// adds after it goes to Rive as text, and deleting into it goes as Backspace. The placeholder
    /// is there because the keyboards don't report Backspace in an empty field.
    /// </remarks>
    internal sealed class PanelSoftKeyboard
    {
        internal const string Placeholder = " ";
        // Long enough for a word the keyboard may still replace, short enough to stay cheap.
        private const int MaxHeld = 32;

        private readonly PanelFocusController m_controller;
        private ISoftKeyboard m_keyboard;
        private bool m_open;
        private bool m_hadText;
        private bool m_tapped;
        // What the keyboard last held, to diff against.
        private string m_seen = Placeholder;
        // What it held before a reset it hasn't shown yet, so reading that again is stale.
        private string m_staleAfterReset;
        private int m_resetFrame;
        // Plenty for a keyboard to take a reset. Past this, a match is the user again.
        private const int ResetFrames = 10;

        internal PanelSoftKeyboard(PanelFocusController controller)
        {
            m_controller = controller;
        }

        /// <summary>
        /// Tests swap in their own.
        /// </summary>
        internal ISoftKeyboard Keyboard
        {
            get => m_keyboard ??= new UnitySoftKeyboard();
            set => m_keyboard = value;
        }

        internal bool IsOpen => m_open;

        /// <summary>
        /// A tap reopens a keyboard the user closed while the field kept focus.
        /// </summary>
        internal void NotifyTap()
        {
            m_tapped = true;
        }

        /// <summary>
        /// Opens or closes with the focused field. Runs after advances and pointer input.
        /// </summary>
        internal void Sync(bool hostFocused)
        {
            IRiveFocusWidget widget = m_controller.ActiveWidget;
            bool wantsText = hostFocused &&
                             m_controller.SessionState == PanelFocusSessionState.RiveFocused &&
                             widget != null &&
                             widget.FocusState.ExpectsKeyboardInput;
            bool tapped = m_tapped;
            m_tapped = false;
            bool gained = wantsText && !m_hadText;
            m_hadText = wantsText;

            if (!wantsText)
            {
                Close();
                return;
            }
            if (!Keyboard.IsSupported)
            {
                return;
            }

            if (!m_open && (gained || tapped))
            {
                Keyboard.Open(Placeholder);
                m_open = true;
                m_seen = Placeholder;
                m_staleAfterReset = null;
            }
        }

        internal void Close()
        {
            m_hadText = false;
            m_tapped = false;
            if (!m_open)
            {
                return;
            }
            m_open = false;
            Keyboard.Close();
        }

        /// <summary>
        /// Sends what was typed since last time. Runs with the panel's key input, outside Rive's
        /// update, since a panel that waits for Rive waits here.
        /// </summary>
        internal void ReadTyped()
        {
            IRiveFocusWidget widget = m_controller.ActiveWidget;
            if (!m_open || widget == null)
            {
                return;
            }

            TouchScreenKeyboard.Status? status = Keyboard.Status;
            string text = Keyboard.Text ?? "";
            if (m_staleAfterReset != null)
            {
                if (text == m_staleAfterReset && Time.frameCount - m_resetFrame < ResetFrames)
                {
                    text = m_seen;
                }
                else
                {
                    m_staleAfterReset = null;
                }
            }

            // A cancelled keyboard puts back the text it opened with, which isn't an edit.
            bool cancelled = status == TouchScreenKeyboard.Status.Canceled || status == TouchScreenKeyboard.Status.LostFocus;
            if (text != m_seen && !cancelled)
            {
                // The keyboard may swap a whole word, so delete back to where they agree, then type the rest.
                int common = 0;
                int shortest = Mathf.Min(text.Length, m_seen.Length);
                while (common < shortest && text[common] == m_seen[common])
                {
                    common++;
                }
                for (int i = common; i < m_seen.Length; i++)
                {
                    widget.KeyInput(RiveKey.Backspace, RiveKeyModifiers.None, true, false, KeySelection.None, null);
                    widget.KeyInput(RiveKey.Backspace, RiveKeyModifiers.None, false, false, KeySelection.None, null);
                }
                if (common < text.Length)
                {
                    widget.TextInput(text.Substring(common), null);
                }
                m_seen = text;

                if (!text.StartsWith(Placeholder) || text.Length > MaxHeld)
                {
                    Reset(text);
                }
            }

            if (status == TouchScreenKeyboard.Status.Done)
            {
                // The keyboard's return key.
                widget.KeyInput(RiveKey.Enter, RiveKeyModifiers.None, true, false, KeySelection.None, null);
                widget.KeyInput(RiveKey.Enter, RiveKeyModifiers.None, false, false, KeySelection.None, null);
            }
            if (status != TouchScreenKeyboard.Status.Visible)
            {
                // Closed by the user. The field keeps focus, and a tap opens it again.
                m_open = false;
                Keyboard.Close();
            }
        }

        private void Reset(string current)
        {
            m_staleAfterReset = current;
            m_resetFrame = Time.frameCount;
            Keyboard.Text = Placeholder;
            m_seen = Placeholder;
        }
    }
}
