using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
#if RIVE_USING_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Rive.Components
{
    /// <summary>
    /// Keyboard input for a panel while Unity has its focus provider selected. Keys go to the
    /// focused widget first, like a DOM keydown on the web runtime; arrows and Tab that Rive
    /// doesn't claim then move focus.
    /// </summary>
    /// <remarks>
    /// Reads IMGUI key events in <see cref="IUpdateSelectedHandler.OnUpdateSelected"/>, the way
    /// Unity's InputField does, so both input backends work the same. The UI input modules
    /// call it before navigation, so using the event there keeps that frame's move and submit
    /// for Rive.
    /// </remarks>
    internal sealed class PanelKeyboardInput
    {
        private readonly PanelFocusController m_controller;
        private readonly Event m_event = new Event();
        private readonly HashSet<KeyCode> m_held = new HashSet<KeyCode>();
        private readonly StringBuilder m_text = new StringBuilder();
        private readonly List<Event> m_frameEvents = new List<Event>();
        // The frame keyboard events last arrived, so submit and cancel from the same key are skipped.
        private int m_keyboardFrame = -1;
        private bool m_imeOn;
        private bool m_expectsText;

        internal PanelKeyboardInput(PanelFocusController controller)
        {
            m_controller = controller;
            SoftKeyboard = new PanelSoftKeyboard(controller);
        }

        internal PanelSoftKeyboard SoftKeyboard { get; }

        /// <summary>
        /// The system clipboard. Tests swap in their own.
        /// </summary>
        internal Func<string> ReadClipboard = () => GUIUtility.systemCopyBuffer;
        internal Action<string> WriteClipboard = text => GUIUtility.systemCopyBuffer = text;

        /// <summary>
        /// Drains the frame's key events. Uses eventData when Rive owns this frame's arrows or Tab.
        /// </summary>
        internal void OnUpdateSelected(BaseEventData eventData)
        {
            SoftKeyboard.ReadTyped();
            m_frameEvents.Clear();
            while (Event.PopEvent(m_event))
            {
                if (m_event.type == EventType.KeyDown || m_event.type == EventType.KeyUp)
                {
                    m_frameEvents.Add(new Event(m_event));
                }
            }
            Process(m_frameEvents, eventData);
            m_frameEvents.Clear();
        }

        /// <summary>
        /// One frame's key events, in order. Tests pass their own.
        /// </summary>
        internal void Process(List<Event> events, BaseEventData eventData)
        {
            bool riveFocused = m_controller.SessionState == PanelFocusSessionState.RiveFocused;
            IRiveFocusWidget widget = m_controller.ActiveWidget;
            bool expectsText = riveFocused && widget != null && widget.FocusState.ExpectsKeyboardInput;
            m_expectsText = expectsText;
            m_text.Clear();

            for (int i = 0; i < events.Count; i++)
            {
                Event e = events[i];
                if (e.type == EventType.KeyDown)
                {
                    m_keyboardFrame = Time.frameCount;
                    if (e.keyCode != KeyCode.None)
                    {
                        // Text typed before this key reaches Rive before it, like backspace after a letter.
                        FlushText();
                        OnKeyDown(e.keyCode, Modifiers(e), eventData);
                    }
                    char character = e.character;
                    // Keys like backspace also send a control character, but the key already did the edit.
                    // Ctrl and Cmd make shortcuts, but AltGr comes as Ctrl+Alt and still types.
                    bool shortcut = (e.control || e.command) && !e.alt;
                    if (expectsText && !char.IsControl(character) && !shortcut)
                    {
                        m_text.Append(character);
                    }
                }
                else if (e.type == EventType.KeyUp)
                {
                    m_keyboardFrame = Time.frameCount;
                    FlushText();
                    OnKeyUp(e.keyCode, Modifiers(e));
                }
            }

            // Typing into a text field shouldn't navigate, and the UI modules move on WASD by default.
            if (expectsText && events.Count > 0)
            {
                eventData.Use();
            }

            FlushText();
        }

        private void FlushText()
        {
            if (m_text.Length == 0)
            {
                return;
            }
            // Read again, a key this frame may have moved focus.
            m_controller.ActiveWidget?.TextInput(m_text.ToString(), null);
            m_text.Clear();
        }

        /// <summary>
        /// True when held keys already drive this move, or are typing, so the UI module's repeat is skipped.
        /// </summary>
        internal bool OwnsMove(MoveDirection direction)
        {
            if (m_controller.SessionState != PanelFocusSessionState.RiveFocused)
            {
                return false;
            }
            IRiveFocusWidget widget = m_controller.ActiveWidget;
            if (m_held.Count > 0 && widget != null && widget.FocusState.ExpectsKeyboardInput)
            {
                return true;
            }
            switch (direction)
            {
                case MoveDirection.Left:
                    return m_held.Contains(KeyCode.LeftArrow);
                case MoveDirection.Right:
                    return m_held.Contains(KeyCode.RightArrow);
                case MoveDirection.Up:
                    return m_held.Contains(KeyCode.UpArrow);
                case MoveDirection.Down:
                    return m_held.Contains(KeyCode.DownArrow);
                default:
                    return false;
            }
        }

        /// <summary>
        /// Submit from a gamepad or other non-keyboard source, as Enter. Keyboard Enter already went through as a key.
        /// </summary>
        internal void OnSubmit()
        {
            SendTap(RiveKey.Enter);
        }

        /// <summary>
        /// Cancel from a gamepad or other non-keyboard source, as Escape.
        /// </summary>
        internal void OnCancel()
        {
            SendTap(RiveKey.Escape);
        }

        /// <summary>
        /// The panel lost Unity's selection.
        /// </summary>
        internal void Reset()
        {
            m_held.Clear();
            m_text.Clear();
            SetIme(false);
            SoftKeyboard.Close();
        }

        /// <summary>
        /// IME composition and the on-screen keyboard stay on while the focused node takes text.
        /// </summary>
        internal void SyncIme(bool hostFocused)
        {
            IRiveFocusWidget widget = m_controller.ActiveWidget;
            SetIme(hostFocused &&
                   m_controller.SessionState == PanelFocusSessionState.RiveFocused &&
                   widget != null &&
                   widget.FocusState.ExpectsKeyboardInput);
            SoftKeyboard.Sync(hostFocused);
        }

        private void SendTap(RiveKey key)
        {
            if (m_keyboardFrame == Time.frameCount ||
                m_controller.SessionState != PanelFocusSessionState.RiveFocused)
            {
                return;
            }
            IRiveFocusWidget widget = m_controller.ActiveWidget;
            widget?.KeyInput(key, RiveKeyModifiers.None, true, false, KeySelection.None, null);
            widget?.KeyInput(key, RiveKeyModifiers.None, false, false, KeySelection.None, null);
        }

        private void OnKeyDown(KeyCode keyCode, RiveKeyModifiers modifiers, BaseEventData eventData)
        {
            bool repeat = !m_held.Add(keyCode);
            if (!TryMap(keyCode, out RiveKey key))
            {
                return;
            }

            FocusNavigationDirection? navigation = Navigation(key, modifiers);
            if (m_controller.SessionState != PanelFocusSessionState.RiveFocused)
            {
                // Tab enters the tree from a panel selected by pointer; arrows enter through the UI module's move.
                if (key == RiveKey.Tab && m_controller.SessionState == PanelFocusSessionState.EntryPending)
                {
                    eventData.Use();
                    m_controller.TryMoveFocus(navigation.Value);
                }
                return;
            }

            IRiveFocusWidget widget = m_controller.ActiveWidget;
            if (widget == null)
            {
                return;
            }

            if (navigation == null)
            {
                SendKeyDown(widget, key, modifiers, repeat);
                return;
            }

            // Rive gets the key first, and only an unclaimed one moves focus, so this frame's
            // move is Rive's either way.
            eventData.Use();
            FocusNavigationDirection direction = navigation.Value;
            widget.KeyInput(key, modifiers, true, repeat, KeySelection.None, result =>
            {
                if (result.Claimed || m_controller.SessionState != PanelFocusSessionState.RiveFocused)
                {
                    return;
                }
                bool moved = m_controller.TryMoveFocus(direction);
                // A panel that waits answers here. One that doesn't hands back from the controller.
                if (!moved && m_controller.WaitsForRive && !FocusSpatialNavigator.IsSequential(direction))
                {
                    m_controller.FocusProvider?.ContinueNavigation(direction);
                }
            });
        }

        private void OnKeyUp(KeyCode keyCode, RiveKeyModifiers modifiers)
        {
            bool wasHeld = m_held.Remove(keyCode);
            if (!wasHeld ||
                !TryMap(keyCode, out RiveKey key) ||
                m_controller.SessionState != PanelFocusSessionState.RiveFocused)
            {
                return;
            }
            m_controller.ActiveWidget?.KeyInput(key, modifiers, false, false, KeySelection.None, null);
        }

        // Core leaves the clipboard to the host. Copy and cut read the selection with the key,
        // so it's the one the key saw, and paste goes in as text.
        private void SendKeyDown(IRiveFocusWidget widget, RiveKey key, RiveKeyModifiers modifiers, bool repeat)
        {
            bool system = modifiers == SystemModifier || modifiers == (SystemModifier | RiveKeyModifiers.Shift);
            KeySelection selection = KeySelection.None;
            if (system && key == RiveKey.C)
            {
                selection = KeySelection.Copy;
            }
            else if (system && key == RiveKey.X)
            {
                selection = m_expectsText ? KeySelection.Cut : KeySelection.Copy;
            }

            if (selection == KeySelection.None)
            {
                widget.KeyInput(key, modifiers, true, repeat, KeySelection.None, null);
            }
            else
            {
                widget.KeyInput(key, modifiers, true, repeat, selection, result =>
                {
                    if (!string.IsNullOrEmpty(result.SelectedText))
                    {
                        WriteClipboard(result.SelectedText);
                    }
                });
            }

            if (system && key == RiveKey.V && m_expectsText)
            {
                string pasted = ReadClipboard();
                if (!string.IsNullOrEmpty(pasted))
                {
                    // Single line fields drop the line breaks themselves.
                    m_text.Append(pasted.Replace("\r\n", "\n").Replace('\r', '\n'));
                }
            }
        }

        // The shortcut modifier core's text fields use: Ctrl on Windows, Cmd everywhere else.
        private static RiveKeyModifiers SystemModifier =>
            SystemInfo.operatingSystemFamily == OperatingSystemFamily.Windows
                ? RiveKeyModifiers.Ctrl
                : RiveKeyModifiers.Meta;

        // Arrows and Tab move focus when Rive leaves them. Alt, Ctrl and Cmd arrows are OS shortcuts.
        private static FocusNavigationDirection? Navigation(RiveKey key, RiveKeyModifiers modifiers)
        {
            if (key == RiveKey.Tab)
            {
                return (modifiers & RiveKeyModifiers.Shift) != 0
                    ? FocusNavigationDirection.Previous
                    : FocusNavigationDirection.Next;
            }
            if ((modifiers & (RiveKeyModifiers.Alt | RiveKeyModifiers.Ctrl | RiveKeyModifiers.Meta)) != 0)
            {
                return null;
            }
            switch (key)
            {
                case RiveKey.Left:
                    return FocusNavigationDirection.Left;
                case RiveKey.Right:
                    return FocusNavigationDirection.Right;
                case RiveKey.Up:
                    return FocusNavigationDirection.Up;
                case RiveKey.Down:
                    return FocusNavigationDirection.Down;
                default:
                    return null;
            }
        }

        private void SetIme(bool on)
        {
            if (m_imeOn == on)
            {
                return;
            }
            m_imeOn = on;
#if ENABLE_LEGACY_INPUT_MANAGER
            Input.imeCompositionMode = on ? IMECompositionMode.On : IMECompositionMode.Auto;
#elif RIVE_USING_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            Keyboard.current?.SetIMEEnabled(on);
#endif
        }

        private static RiveKeyModifiers Modifiers(Event e)
        {
            RiveKeyModifiers modifiers = RiveKeyModifiers.None;
            if (e.shift)
            {
                modifiers |= RiveKeyModifiers.Shift;
            }
            if (e.control)
            {
                modifiers |= RiveKeyModifiers.Ctrl;
            }
            if (e.alt)
            {
                modifiers |= RiveKeyModifiers.Alt;
            }
            if (e.command)
            {
                modifiers |= RiveKeyModifiers.Meta;
            }
            return modifiers;
        }

        internal static bool TryMap(KeyCode keyCode, out RiveKey key)
        {
            if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
            {
                key = (RiveKey)((int)RiveKey.A + (keyCode - KeyCode.A));
                return true;
            }
            if (keyCode >= KeyCode.Alpha0 && keyCode <= KeyCode.Alpha9)
            {
                key = (RiveKey)((int)RiveKey.Key0 + (keyCode - KeyCode.Alpha0));
                return true;
            }
            if (keyCode >= KeyCode.Keypad0 && keyCode <= KeyCode.Keypad9)
            {
                key = (RiveKey)((int)RiveKey.Kp0 + (keyCode - KeyCode.Keypad0));
                return true;
            }
            if (keyCode >= KeyCode.F1 && keyCode <= KeyCode.F15)
            {
                key = (RiveKey)((int)RiveKey.F1 + (keyCode - KeyCode.F1));
                return true;
            }
            return s_keys.TryGetValue(keyCode, out key);
        }

        private static readonly Dictionary<KeyCode, RiveKey> s_keys = new Dictionary<KeyCode, RiveKey>
        {
            { KeyCode.Space, RiveKey.Space },
            { KeyCode.Quote, RiveKey.Apostrophe },
            { KeyCode.Comma, RiveKey.Comma },
            { KeyCode.Minus, RiveKey.Minus },
            { KeyCode.Period, RiveKey.Period },
            { KeyCode.Slash, RiveKey.Slash },
            { KeyCode.Semicolon, RiveKey.Semicolon },
            { KeyCode.Equals, RiveKey.Equal },
            { KeyCode.LeftBracket, RiveKey.LeftBracket },
            { KeyCode.Backslash, RiveKey.Backslash },
            { KeyCode.RightBracket, RiveKey.RightBracket },
            { KeyCode.BackQuote, RiveKey.GraveAccent },
            { KeyCode.Escape, RiveKey.Escape },
            { KeyCode.Return, RiveKey.Enter },
            { KeyCode.Tab, RiveKey.Tab },
            { KeyCode.Backspace, RiveKey.Backspace },
            { KeyCode.Insert, RiveKey.Insert },
            { KeyCode.Delete, RiveKey.Delete },
            { KeyCode.RightArrow, RiveKey.Right },
            { KeyCode.LeftArrow, RiveKey.Left },
            { KeyCode.DownArrow, RiveKey.Down },
            { KeyCode.UpArrow, RiveKey.Up },
            { KeyCode.PageUp, RiveKey.PageUp },
            { KeyCode.PageDown, RiveKey.PageDown },
            { KeyCode.Home, RiveKey.Home },
            { KeyCode.End, RiveKey.End },
            { KeyCode.CapsLock, RiveKey.CapsLock },
            { KeyCode.ScrollLock, RiveKey.ScrollLock },
            { KeyCode.Numlock, RiveKey.NumLock },
            { KeyCode.Print, RiveKey.PrintScreen },
            { KeyCode.Pause, RiveKey.Pause },
            { KeyCode.KeypadPeriod, RiveKey.KpDecimal },
            { KeyCode.KeypadDivide, RiveKey.KpDivide },
            { KeyCode.KeypadMultiply, RiveKey.KpMultiply },
            { KeyCode.KeypadMinus, RiveKey.KpSubtract },
            { KeyCode.KeypadPlus, RiveKey.KpAdd },
            { KeyCode.KeypadEnter, RiveKey.KpEnter },
            { KeyCode.KeypadEquals, RiveKey.KpEqual },
            { KeyCode.LeftShift, RiveKey.LeftShift },
            { KeyCode.LeftControl, RiveKey.LeftControl },
            { KeyCode.LeftAlt, RiveKey.LeftAlt },
            { KeyCode.LeftCommand, RiveKey.LeftSuper },
            { KeyCode.RightShift, RiveKey.RightShift },
            { KeyCode.RightControl, RiveKey.RightControl },
            { KeyCode.RightAlt, RiveKey.RightAlt },
            { KeyCode.RightCommand, RiveKey.RightSuper },
            { KeyCode.Menu, RiveKey.Menu },
        };
    }
}
