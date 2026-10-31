using System;
using System.Collections.Generic;
using Rive.Utils;
using UnityEngine;

namespace Rive.Components
{
    /// <summary>
    /// Directions shared by focus providers.
    /// </summary>
    internal enum FocusNavigationDirection : byte
    {
        Next = 0,
        Previous = 1,
        Left = 2,
        Right = 3,
        Up = 4,
        Down = 5
    }

    /// <summary>
    /// Why Unity selected this panel's focus provider.
    /// </summary>
    internal enum FocusEntryReason : byte
    {
        Navigation = 0,
        Pointer = 1,
        Programmatic = 2
    }

    /// <summary>
    /// Where the panel is in a focus session.
    /// </summary>
    internal enum PanelFocusSessionState : byte
    {
        /// <summary>
        /// Unity does not have this panel selected.
        /// </summary>
        Unfocused = 0,

        /// <summary>
        /// Unity has the panel selected, but nothing in Rive is focused yet.
        /// </summary>
        EntryPending = 1,

        /// <summary>
        /// A widget currently has Rive focus.
        /// </summary>
        RiveFocused = 2,

        /// <summary>
        /// Rive blurred itself but Unity still has this surface selected. The next
        /// arrow should leave instead of jumping back in.
        /// </summary>
        Released = 3
    }

    /// <summary>
    /// Handles communication between Unity's focus system and the panel. There is one of these for each panel.
    /// </summary>
    internal interface IPanelFocusProvider
    {
        /// <summary>
        /// Returns true if Unity is currently focused on this provider.
        /// </summary>
        bool HasHostFocus { get; }

        /// <summary>
        /// Tells the provider whether any content in the panel can be focused, so it knows when to add or remove itself from Unity's focus system.
        /// </summary>
        /// <param name="isFocusable">True if the panel currently has focusable elements; false otherwise.</param>
        void SetFocusable(bool isFocusable);

        /// <summary>
        /// Requests Unity to give focus to this provider. Only does anything if AllowFocusInterrupt is enabled.
        /// </summary>
        void RequestNativeFocus();

        /// <summary>
        /// A move that Unity was told Rive would take ended up leaving Rive, so Unity navigates from here instead.
        /// Only happens in panels that don't wait for Rive.
        /// </summary>
        void ContinueNavigation(FocusNavigationDirection direction);

        /// <summary>
        /// This object is no longer the Unity focus provider for its panel.
        /// This can happen if another component like the TextureFocusInputProvider takes over, or if the panel removes this provider.
        /// Only update this object's local state here,do not change the controller's FocusProvider.
        /// Otherwise, you could accidentally clear or overwrite the intended replacement.
        /// </summary>
        void NotifyDetached();
    }

    /// <summary>
    /// What a key does with the focused selection, for the clipboard.
    /// </summary>
    internal enum KeySelection : byte
    {
        None = 0,
        // Reads the selection before the key.
        Copy = 1,
        // Reads it, then deletes it when the focused node takes text.
        Cut = 2,
    }

    /// <summary>
    /// A widget that can take Rive focus.
    /// </summary>
    internal interface IRiveFocusWidget
    {
        /// <summary>
        /// True when this widget is enabled, loaded, and has focusable Rive content.
        /// </summary>
        bool IsEligibleForFocus { get; }

        /// <summary>
        /// The focus as of the widget's last advance or focus move.
        /// </summary>
        FocusState FocusState { get; }

        /// <summary>
        /// Moves focus inside the widget. done runs on the main thread with the result,
        /// before this returns when the panel waits for Rive.
        /// </summary>
        void MoveFocus(FocusNavigationDirection direction, Action<StateMachineNative.FocusResult> done);

        /// <summary>
        /// Give an empty widget a starting focus. Next/Right/Down use FocusNext;
        /// Previous/Left/Up use FocusPrevious. Directional APIs can't seed an empty tree.
        /// </summary>
        void SeedFocus(FocusNavigationDirection direction, Action<StateMachineNative.FocusResult> done);

        /// <summary>
        /// Drops the widget's focus without waiting.
        /// </summary>
        void ClearFocus();

        /// <summary>
        /// Sends a key to the focused node and its ancestors. done gets whether one claimed it, like MoveFocus,
        /// and the selection from before the key when asked for.
        /// </summary>
        void KeyInput(
            RiveKey key, RiveKeyModifiers modifiers, bool pressed, bool repeat, KeySelection selection, Action<StateMachineNative.FocusResult> done);

        /// <summary>
        /// Sends committed text to the focused node and its ancestors. done gets whether one claimed it, like MoveFocus.
        /// </summary>
        void TextInput(string text, Action<StateMachineNative.FocusResult> done);

        /// <summary>
        /// Widget rect in panel space, used to pick a neighboring widget.
        /// </summary>
        Rect GetPanelSpaceRect(RectTransform panelContainer);

        /// <summary>
        /// Rect to leave from. Uses the focused node's bounds when we have them, otherwise the whole widget.
        /// </summary>
        Rect GetOriginRect(RectTransform panelContainer);
    }

    /// <summary>
    /// Moves focus between Rive widgets in a panel.
    /// Unity still handles Selectable-to-Selectable (or object-to-object) navigation;
    /// this only handles the Rive side, including jumping from one widget to another.
    /// </summary>
    /// <remarks>
    /// A move can take several steps, like trying the focused widget and then its neighbors,
    /// and each step continues from Rive's answer. When the panel waits for Rive, that all
    /// happens before the move returns. Otherwise Unity is told up front whether Rive takes the
    /// move, from the last known focus, and moves arriving while one is out queue behind it.
    /// </remarks>
    internal sealed class PanelFocusController
    {
        // Enough for a held arrow key between two answers.
        private const int MaxQueuedMoves = 4;

        private readonly RivePanel m_panel;
        private readonly Dictionary<IRiveFocusWidget, bool> m_previousHasFocus =
            new Dictionary<IRiveFocusWidget, bool>();
        private readonly List<IRiveFocusWidget> m_eligibleScratch = new List<IRiveFocusWidget>();
        private readonly List<IRiveFocusWidget> m_newlyFocusedScratch = new List<IRiveFocusWidget>();
        private readonly List<IRiveFocusWidget> m_currentlyFocusedScratch = new List<IRiveFocusWidget>();
        private readonly List<IRiveFocusWidget> m_staleScratch = new List<IRiveFocusWidget>();
        private readonly Queue<FocusNavigationDirection> m_queuedMoves = new Queue<FocusNavigationDirection>();
        // Focus commands still out per widget, so ending a session can clear what they may take.
        private readonly Dictionary<IRiveFocusWidget, int> m_commandsOut = new Dictionary<IRiveFocusWidget, int>();
        private readonly HashSet<IRiveFocusWidget> m_clearScratch = new HashSet<IRiveFocusWidget>();

        private IPanelFocusProvider m_focusProvider;
        private IRiveFocusWidget m_activeWidget;
        private PanelFocusSessionState m_sessionState = PanelFocusSessionState.Unfocused;
        private bool m_notifiedHasFocusableContent;
        private bool m_hasNotifiedHasFocusableContent;
        private bool m_moveInFlight;
        // Bumped whenever the session is reset, so a move still out finds it's stale.
        private int m_sessionVersion;

        public PanelFocusController(RivePanel panel)
        {
            m_panel = panel;
            Keyboard = new PanelKeyboardInput(this);
        }

        /// <summary>
        /// Keys and text for the focused widget while Unity has the panel selected.
        /// </summary>
        internal PanelKeyboardInput Keyboard { get; }

        public PanelFocusSessionState SessionState => m_sessionState;

        public IRiveFocusWidget ActiveWidget => m_activeWidget;

        /// <summary>
        /// True while a move is waiting on Rive, in a panel that doesn't wait.
        /// </summary>
        internal bool MoveInFlight => m_moveInFlight;

        public IPanelFocusProvider FocusProvider
        {
            get => m_focusProvider;
            set
            {
                if (ReferenceEquals(m_focusProvider, value))
                {
                    return;
                }

                IPanelFocusProvider previous = m_focusProvider;
                m_focusProvider = value;

                if (previous != null)
                {
                    bool previousHadHostFocus = previous.HasHostFocus;
                    previous.NotifyDetached();
                    if (previousHadHostFocus && m_sessionState != PanelFocusSessionState.Unfocused)
                    {
                        ClearAllRiveFocus();
                        SetSession(PanelFocusSessionState.Unfocused, null);
                    }
                }

                NotifyFocusableContentIfNeeded(force: true);
            }
        }

        public bool HasFocusableContent
        {
            get
            {
                CollectEligible(m_eligibleScratch);
                return m_eligibleScratch.Count > 0;
            }
        }

        public FocusState FocusState => m_activeWidget != null ? m_activeWidget.FocusState : default;

        /// <summary>
        /// Enters the panel's Rive content from Unity.
        /// </summary>
        /// <param name="direction">The way focus is travelling.</param>
        /// <param name="finished">Runs with whether Rive took focus. Before this returns when the panel waits for Rive.</param>
        /// <returns>True if Rive took focus, or, when the panel doesn't wait, the move is out.</returns>
        public bool TryEnterFocus(FocusNavigationDirection direction, Action<bool> finished = null)
        {
            return Run((version, done) => Enter(direction, version, done), finished);
        }

        /// <summary>
        /// Moves focus in response to Unity navigation.
        /// </summary>
        /// <returns>True if Rive takes the move. When the panel doesn't wait for Rive this comes from
        /// the last known focus, and a move that ends up leaving Rive is handed back to the provider.</returns>
        public bool TryMoveFocus(FocusNavigationDirection direction)
        {
            if (m_sessionState == PanelFocusSessionState.Released ||
                m_sessionState == PanelFocusSessionState.Unfocused)
            {
                return false;
            }

            if (m_moveInFlight)
            {
                if (m_queuedMoves.Count >= MaxQueuedMoves)
                {
                    // Dropped rather than handed to Unity, so a held key can't jump focus out early.
                    return true;
                }
                m_queuedMoves.Enqueue(direction);
                return true;
            }

            return Run((version, done) => Move(direction, version, done), moved => OnMoveFinished(direction, moved));
        }

        public void NotifyHostFocusGained(
            FocusEntryReason reason,
            FocusNavigationDirection? direction = null)
        {
            if (AnyEligibleWidgetHasFocus(out IRiveFocusWidget alreadyFocused))
            {
                SetSession(PanelFocusSessionState.RiveFocused, alreadyFocused);
                PollFocus();
                return;
            }

            SetSession(PanelFocusSessionState.EntryPending, null);
            if (reason == FocusEntryReason.Navigation && direction.HasValue)
            {
                // Stays pending if nothing takes it.
                TryEnterFocus(direction.Value);
            }
        }

        public void NotifyHostPointerActivated()
        {
            if (m_sessionState == PanelFocusSessionState.Released ||
                m_sessionState == PanelFocusSessionState.Unfocused)
            {
                SetSession(PanelFocusSessionState.EntryPending, m_activeWidget);
            }
        }

        public void NotifyHostFocusLost()
        {
            Keyboard.Reset();
            ClearAllRiveFocus();
            SetSession(PanelFocusSessionState.Unfocused, null);
        }

        public void ClearFocus()
        {
            ClearAllRiveFocus();
            SetSession(HostFocused ? PanelFocusSessionState.Released : PanelFocusSessionState.Unfocused, null);
        }

        public void HandleWidgetRemoved(IRiveWidget widget)
        {
            if (widget is IRiveFocusWidget focusWidget)
            {
                HandleIneligibleWidget(focusWidget);
            }

            PollFocus();
        }

        public void HandleWidgetsChanged()
        {
            PollFocus();
        }

        /// <summary>
        /// Picks up focus Rive changed on its own, like an authored focus action or a press
        /// outside a focused field. Runs after advances and pointer input.
        /// </summary>
        public void PollFocus()
        {
            CollectEligible(m_eligibleScratch);
            NotifyFocusableContentIfNeeded(force: false);

            if (m_activeWidget != null && !m_activeWidget.IsEligibleForFocus)
            {
                HandleIneligibleWidget(m_activeWidget);
            }

            m_newlyFocusedScratch.Clear();
            m_currentlyFocusedScratch.Clear();

            for (int i = 0; i < m_eligibleScratch.Count; i++)
            {
                IRiveFocusWidget widget = m_eligibleScratch[i];
                bool hasFocus = widget.FocusState.HasFocus;
                m_previousHasFocus.TryGetValue(widget, out bool hadFocus);

                if (hasFocus)
                {
                    m_currentlyFocusedScratch.Add(widget);
                    if (!hadFocus)
                    {
                        m_newlyFocusedScratch.Add(widget);
                    }
                }
                else if (hadFocus && ReferenceEquals(widget, m_activeWidget) && !m_moveInFlight)
                {
                    SetSession(HostFocused ? PanelFocusSessionState.Released : PanelFocusSessionState.Unfocused, null);
                }

                m_previousHasFocus[widget] = hasFocus;
            }

            PrunePreviousFocusSnapshot();

            if (m_currentlyFocusedScratch.Count > 1)
            {
                IRiveFocusWidget keep = SelectTopmost(m_newlyFocusedScratch.Count > 0 ? m_newlyFocusedScratch : m_currentlyFocusedScratch);
                for (int i = 0; i < m_currentlyFocusedScratch.Count; i++)
                {
                    if (!ReferenceEquals(m_currentlyFocusedScratch[i], keep))
                    {
                        m_currentlyFocusedScratch[i].ClearFocus();
                        m_previousHasFocus[m_currentlyFocusedScratch[i]] = false;
                    }
                }

                // Clicking from one focused widget into another is expected.
                // Warn only when the user has focus enabled at the Unity level and two widgets newly focused together.
                if (m_focusProvider != null && m_newlyFocusedScratch.Count > 1)
                {
                    DebugLogger.Instance.LogWarning(
                        $"Multiple Rive widgets in {m_panel.name} reported focus in one pass. " +
                        "Keeping the topmost widget and clearing the others.");
                }

                SetSession(PanelFocusSessionState.RiveFocused, keep);
            }
            else if (m_newlyFocusedScratch.Count == 1)
            {
                IRiveFocusWidget newlyFocused = m_newlyFocusedScratch[0];
                SetSession(PanelFocusSessionState.RiveFocused, newlyFocused);
                MaybeRequestNativeFocusInterrupt();
            }
            else if (m_currentlyFocusedScratch.Count == 1)
            {
                SetSession(PanelFocusSessionState.RiveFocused, m_currentlyFocusedScratch[0]);
            }

            Keyboard.SyncIme(HostFocused);
        }

        internal bool HostFocused => m_focusProvider != null && m_focusProvider.HasHostFocus;

        internal bool WaitsForRive => m_panel.ThreadingMode == ThreadingMode.MainThread;

        // Runs a move. When the panel waits, finished runs inside and the real answer comes back.
        // Otherwise the move is out, so Unity is told Rive takes it.
        private bool Run(Action<int, Action<bool>> start, Action<bool> finished)
        {
            bool returned = false;
            bool finishedInline = false;
            bool result = false;
            int version = m_sessionVersion;
            if (!WaitsForRive)
            {
                m_moveInFlight = true;
            }

            start(version, moved =>
            {
                if (!WaitsForRive)
                {
                    m_moveInFlight = false;
                }
                if (version != m_sessionVersion)
                {
                    // The session ended while it was out, so nothing follows on from it.
                    finishedInline = !returned;
                    result = false;
                    return;
                }
                PollFocus();
                if (!returned)
                {
                    finishedInline = true;
                    result = moved;
                }
                finished?.Invoke(moved);
            });

            returned = true;
            return finishedInline ? result : true;
        }

        private void OnMoveFinished(FocusNavigationDirection direction, bool moved)
        {
            if (WaitsForRive)
            {
                return;
            }

            if (!moved)
            {
                // Unity already counted this move as Rive's, so it navigates on from here.
                // Leaving Unity's selection deselects the panel, which ends the session.
                m_queuedMoves.Clear();
                if (HostFocused && !FocusSpatialNavigator.IsSequential(direction))
                {
                    m_focusProvider.ContinueNavigation(direction);
                }
                return;
            }

            if (m_queuedMoves.Count > 0)
            {
                TryMoveFocus(m_queuedMoves.Dequeue());
            }
        }

        // Every step takes the session version the move started in, and stops once it's stale.
        private void Move(FocusNavigationDirection direction, int version, Action<bool> done)
        {
            if (m_sessionState == PanelFocusSessionState.EntryPending || !IsLive(m_activeWidget))
            {
                Enter(direction, version, done);
                return;
            }

            IRiveFocusWidget active = m_activeWidget;
            bool sequential = FocusSpatialNavigator.IsSequential(direction);
            CommandSent(active);
            active.MoveFocus(direction, result =>
            {
                CommandLanded(active);
                // Ending the session cleared the widget behind this command, so there's nothing to undo.
                if (version != m_sessionVersion)
                {
                    done(false);
                    return;
                }

                // At an edge that stops, a sequential move keeps focus where it is.
                if (result.Moved || (sequential && result.State.HasFocus))
                {
                    done(true);
                    return;
                }

                // Read now rather than up front, so it starts from where focus is after the move.
                List<IRiveFocusWidget> candidates = sequential
                    ? SequentialSiblings(active, direction)
                    : SpatialCandidates(active.GetOriginRect(m_panel.WidgetContainer), direction, active);
                Seed(candidates, 0, direction, version, done);
            });
        }

        private void Enter(FocusNavigationDirection direction, int version, Action<bool> done)
        {
            List<IRiveFocusWidget> candidates;
            if (FocusSpatialNavigator.IsSequential(direction))
            {
                candidates = new List<IRiveFocusWidget>();
                CollectEligible(candidates);
                if (!FocusSpatialNavigator.IsForwardSeed(direction))
                {
                    candidates.Reverse();
                }
            }
            else
            {
                Rect panelRect = m_panel.WidgetContainer != null ? m_panel.WidgetContainer.rect : Rect.zero;
                Rect origin = FocusSpatialNavigator.CreateEntryOrigin(panelRect, direction);
                candidates = SpatialCandidates(origin, direction, exclude: null);
            }

            Seed(candidates, 0, direction, version, done);
        }

        // Tries each candidate in order until one takes focus.
        private void Seed(
            List<IRiveFocusWidget> candidates,
            int index,
            FocusNavigationDirection direction,
            int version,
            Action<bool> done)
        {
            if (version != m_sessionVersion)
            {
                done(false);
                return;
            }

            while (index < candidates.Count && !IsEligible(candidates[index]))
            {
                index++;
            }

            if (index >= candidates.Count)
            {
                done(false);
                return;
            }

            IRiveFocusWidget candidate = candidates[index];
            CommandSent(candidate);
            candidate.SeedFocus(direction, result =>
            {
                CommandLanded(candidate);
                if (version != m_sessionVersion)
                {
                    done(false);
                    return;
                }

                if (result.Moved)
                {
                    TransferOwnership(m_activeWidget, candidate);
                    done(true);
                    return;
                }

                Seed(candidates, index + 1, direction, version, done);
            });
        }

        private List<IRiveFocusWidget> SequentialSiblings(IRiveFocusWidget from, FocusNavigationDirection direction)
        {
            var siblings = new List<IRiveFocusWidget>();
            int step = FocusSpatialNavigator.IsForwardSeed(direction) ? 1 : -1;
            IReadOnlyList<IRiveWidget> widgets = m_panel.Widgets;
            int panelIndex = IndexOfWidget(widgets, from);
            if (panelIndex < 0)
            {
                return siblings;
            }

            for (int i = panelIndex + step; i >= 0 && i < widgets.Count; i += step)
            {
                if (widgets[i] is IRiveFocusWidget candidate && candidate.IsEligibleForFocus)
                {
                    siblings.Add(candidate);
                }
            }

            return siblings;
        }

        // Eligible widgets, best first, for leaving origin in direction.
        private List<IRiveFocusWidget> SpatialCandidates(
            Rect origin,
            FocusNavigationDirection direction,
            IRiveFocusWidget exclude)
        {
            var widgets = new List<IRiveFocusWidget>();
            var rects = new List<Rect>();
            CollectEligible(m_eligibleScratch);
            for (int i = 0; i < m_eligibleScratch.Count; i++)
            {
                IRiveFocusWidget widget = m_eligibleScratch[i];
                if (ReferenceEquals(widget, exclude))
                {
                    continue;
                }

                widgets.Add(widget);
                rects.Add(widget.GetPanelSpaceRect(m_panel.WidgetContainer));
            }

            var ordered = new List<IRiveFocusWidget>(widgets.Count);
            while (rects.Count > 0)
            {
                int best = FocusSpatialNavigator.FindBestIndex(origin, rects, direction);
                if (best < 0)
                {
                    break;
                }

                ordered.Add(widgets[best]);
                widgets.RemoveAt(best);
                rects.RemoveAt(best);
            }

            return ordered;
        }

        private void CommandSent(IRiveFocusWidget widget)
        {
            m_commandsOut.TryGetValue(widget, out int count);
            m_commandsOut[widget] = count + 1;
        }

        private void CommandLanded(IRiveFocusWidget widget)
        {
            if (m_commandsOut.TryGetValue(widget, out int count))
            {
                if (count <= 1)
                {
                    m_commandsOut.Remove(widget);
                }
                else
                {
                    m_commandsOut[widget] = count - 1;
                }
            }
        }

        private void MaybeRequestNativeFocusInterrupt()
        {
            if (!m_panel.AllowFocusInterrupt || m_focusProvider == null || m_focusProvider.HasHostFocus)
            {
                return;
            }

            m_focusProvider.RequestNativeFocus();
        }

        private void TransferOwnership(IRiveFocusWidget previous, IRiveFocusWidget next)
        {
            if (previous != null && !ReferenceEquals(previous, next) && IsLive(previous))
            {
                previous.ClearFocus();
                m_previousHasFocus[previous] = false;
            }

            SetSession(PanelFocusSessionState.RiveFocused, next);
        }

        private void HandleIneligibleWidget(IRiveFocusWidget widget)
        {
            if (widget == null)
            {
                return;
            }

            if (ReferenceEquals(widget, m_activeWidget))
            {
                if (IsLive(widget))
                {
                    widget.ClearFocus();
                }
                SetSession(HostFocused ? PanelFocusSessionState.Released : PanelFocusSessionState.Unfocused, null);
            }

            m_previousHasFocus.Remove(widget);
        }

        // Also clears widgets with a focus command still out, whatever their cached focus says.
        // Rive runs commands in order, so the clear lands after that command and before any
        // later input or entry, and a late reply never has anything left to undo.
        private void ClearAllRiveFocus()
        {
            m_clearScratch.Clear();
            CollectEligible(m_eligibleScratch);
            for (int i = 0; i < m_eligibleScratch.Count; i++)
            {
                if (m_eligibleScratch[i].FocusState.HasFocus)
                {
                    m_clearScratch.Add(m_eligibleScratch[i]);
                }
            }

            if (IsLive(m_activeWidget) && m_activeWidget.FocusState.HasFocus)
            {
                m_clearScratch.Add(m_activeWidget);
            }

            foreach (IRiveFocusWidget widget in m_commandsOut.Keys)
            {
                if (IsLive(widget))
                {
                    m_clearScratch.Add(widget);
                }
            }

            foreach (IRiveFocusWidget widget in m_clearScratch)
            {
                widget.ClearFocus();
            }
            m_clearScratch.Clear();

            m_previousHasFocus.Clear();
        }

        private void SetSession(PanelFocusSessionState state, IRiveFocusWidget active)
        {
            if (state == PanelFocusSessionState.Unfocused || state == PanelFocusSessionState.Released)
            {
                // A move still out belongs to the session that just ended.
                m_sessionVersion++;
                m_queuedMoves.Clear();
            }
            m_sessionState = state;
            m_activeWidget = active;
        }

        private void CollectEligible(List<IRiveFocusWidget> dest)
        {
            dest.Clear();
            IReadOnlyList<IRiveWidget> widgets = m_panel.Widgets;
            for (int i = 0; i < widgets.Count; i++)
            {
                if (widgets[i] is IRiveFocusWidget focusWidget && focusWidget.IsEligibleForFocus)
                {
                    dest.Add(focusWidget);
                }
            }
        }

        private bool AnyEligibleWidgetHasFocus(out IRiveFocusWidget focused)
        {
            CollectEligible(m_eligibleScratch);
            IRiveFocusWidget found = null;
            for (int i = 0; i < m_eligibleScratch.Count; i++)
            {
                if (m_eligibleScratch[i].FocusState.HasFocus)
                {
                    found = m_eligibleScratch[i];
                }
            }

            focused = found;
            return found != null;
        }

        private IRiveFocusWidget SelectTopmost(List<IRiveFocusWidget> widgets)
        {
            IReadOnlyList<IRiveWidget> panelWidgets = m_panel.Widgets;
            IRiveFocusWidget topmost = widgets[0];
            int topmostIndex = -1;
            for (int i = 0; i < widgets.Count; i++)
            {
                int index = IndexOfWidget(panelWidgets, widgets[i]);
                if (index >= topmostIndex)
                {
                    topmostIndex = index;
                    topmost = widgets[i];
                }
            }

            return topmost;
        }

        private static int IndexOfWidget(IReadOnlyList<IRiveWidget> widgets, IRiveFocusWidget widget)
        {
            for (int i = 0; i < widgets.Count; i++)
            {
                if (ReferenceEquals(widgets[i], widget))
                {
                    return i;
                }
            }

            return -1;
        }

        // Widgets are components, so one can be destroyed while a move is out.
        private static bool IsLive(IRiveFocusWidget widget)
        {
            return widget != null && !(widget is UnityEngine.Object component && component == null);
        }

        private static bool IsEligible(IRiveFocusWidget widget)
        {
            return IsLive(widget) && widget.IsEligibleForFocus;
        }

        private void PrunePreviousFocusSnapshot()
        {
            if (m_previousHasFocus.Count == m_eligibleScratch.Count)
            {
                return;
            }

            m_staleScratch.Clear();
            foreach (var pair in m_previousHasFocus)
            {
                if (!m_eligibleScratch.Contains(pair.Key))
                {
                    m_staleScratch.Add(pair.Key);
                }
            }

            for (int i = 0; i < m_staleScratch.Count; i++)
            {
                m_previousHasFocus.Remove(m_staleScratch[i]);
            }
            m_staleScratch.Clear();
        }

        private void NotifyFocusableContentIfNeeded(bool force)
        {
            CollectEligible(m_eligibleScratch);
            bool hasContent = m_eligibleScratch.Count > 0;
            if (!force && m_hasNotifiedHasFocusableContent && hasContent == m_notifiedHasFocusableContent)
            {
                return;
            }

            m_notifiedHasFocusableContent = hasContent;
            m_hasNotifiedHasFocusableContent = true;
            m_focusProvider?.SetFocusable(hasContent);
        }
    }
}
