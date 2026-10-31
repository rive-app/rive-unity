using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Rive.Components.Utilities;
using Rive.Producer;
using Rive.Utils;
using UnityEngine;
using Rive.Host;

namespace Rive.Components
{
    /// <summary>
    /// A loaded widget's state machine. Advances it, sends pointer input and
    /// collects reported events. Its advances go out as tick entries, and
    /// the events they report queue here until the main thread takes them.
    ///
    /// A load makes a new one, so work still out for the old state machine
    /// only ever touches the old core.
    /// </summary>
    internal sealed class WidgetCore
    {
        private readonly Artboard m_artboard;
        private readonly StateMachine m_stateMachine;

        // Filled in the order Rive ran things, emptied on the main thread.
        // Advances and pointer events land through different channels, so
        // this is what keeps their events in order.
        private readonly ConcurrentQueue<ReportedEventData> m_events =
            new ConcurrentQueue<ReportedEventData>();
        private readonly List<ReportedEventData> m_collectScratch = new List<ReportedEventData>();

        // Main thread only. Counts focus commands sent, so a tick that went
        // out before one doesn't overwrite what it found.
        private FocusState m_focus;
        private int m_focusVersion;
        // Tick replies can be read on another thread, so focus has its own.
        private readonly List<ReportedEventData> m_focusEventScratch = new List<ReportedEventData>();

        internal WidgetCore(Artboard artboard, StateMachine stateMachine)
        {
            m_artboard = artboard;
            m_stateMachine = stateMachine;
            m_stateMachine.FocusChanged += OnStateMachineFocusChanged;
        }

        internal Artboard Artboard => m_artboard;

        /// Tests only. Runs as each advance is sent.
        internal static System.Action<WidgetCore> AdvancingForTests;

        /// Adds this widget's advance to a tick. The layout fix advances by 0
        /// first.
        internal void WriteTick(PayloadWriter entries, float deltaTime, float speed, bool collectEvents, bool layoutFix)
        {
            AdvancingForTests?.Invoke(this);
            uint flags = (collectEvents ? StateMachineNative.TickCollectEvents : 0)
                         | (layoutFix ? StateMachineNative.TickLayoutFix : 0);
            StateMachineNative.WriteTickEntry(
                entries, m_stateMachine.NativeStateMachine, m_artboard.NativeArtboard, deltaTime * speed, flags);
        }

        /// Reads this widget's entry of a tick reply and queues its events.
        internal StateMachineNative.TickResult ReadTick(ref PayloadReader reader)
        {
            m_collectScratch.Clear();
            StateMachineNative.TickResult result = StateMachineNative.ReadTickEntry(ref reader, m_collectScratch);
            for (int i = 0; i < m_collectScratch.Count; i++)
            {
                m_events.Enqueue(m_collectScratch[i]);
            }
            m_collectScratch.Clear();
            return result;
        }

        /// Main thread, for the synchronous paths. Waits for the advance.
        internal void AdvanceAndWait(float deltaTime, float speed, bool collectEvents)
        {
            ThreadCheck.Main();
            RequestTicket ticket = SendAdvance(deltaTime, speed, collectEvents, keep: true);
            CommandTransport.Join(ref ticket);
        }

        /// Doesn't wait. Runs after anything already sent, so writes made
        /// before it apply first.
        internal void AdvanceLater(float deltaTime, float speed, bool collectEvents)
        {
            SendAdvance(deltaTime, speed, collectEvents, keep: false);
        }

        // Pointer events on synchronous panels advance straight after, so the
        // payload and reply are made once per widget rather than per advance.
        private StateMachineNative.TickSender m_advanceSender;
        private Action<HostMessageBatch, HostMessage> m_onAdvanceReply;
        private int m_advanceFocusVersion;

        private RequestTicket SendAdvance(float deltaTime, float speed, bool collectEvents, bool keep)
        {
            m_advanceSender ??= new StateMachineNative.TickSender();
            m_onAdvanceReply ??= (batch, message) =>
            {
                var reader = new PayloadReader(batch, message);
                StateMachineNative.TickResult result = ReadTick(ref reader);
                // Sent from the main thread, so it lands there.
                ApplyFocus(result, m_advanceFocusVersion);
            };
            m_advanceFocusVersion = m_focusVersion;
            m_advanceSender.Entries.Clear();
            WriteTick(m_advanceSender.Entries, deltaTime, speed, collectEvents, layoutFix: false);
            return m_advanceSender.Send(m_onAdvanceReply, keep);
        }

        /// Main thread. The focus as of the last advance or focus move.
        internal FocusState Focus => m_focus;

        /// Main thread. Taken before a tick goes out, for ApplyFocus.
        internal int FocusVersion => m_focusVersion;

        /// Main thread. Keeps what a tick found unless a focus command went
        /// out after it.
        internal void ApplyFocus(in StateMachineNative.TickResult result, int focusVersion)
        {
            if (result.Found && focusVersion == m_focusVersion)
            {
                m_focus = result.Focus;
            }
        }

        /// Main thread. Moves or clears focus and waits, applying after a
        /// change. Its events queue like an advance's.
        internal StateMachineNative.FocusResult FocusAndWait(StateMachineNative.FocusOp op, bool collectEvents)
        {
            return InputAndWait(
                (id, flags) => StateMachineNative.SendFocus(id, m_stateMachine.NativeStateMachine, op, flags),
                collectEvents);
        }

        /// Main thread. Moves or clears focus without waiting, applying after
        /// a change. landed runs on the main thread with the result.
        internal void FocusLater(StateMachineNative.FocusOp op, bool collectEvents, Action<StateMachineNative.FocusResult> landed)
        {
            InputLater(
                (id, flags) => StateMachineNative.SendFocus(id, m_stateMachine.NativeStateMachine, op, flags),
                collectEvents,
                landed);
        }

        /// Main thread. A key for the focused node, waiting for whether it was claimed.
        /// selection is FocusReadSelection or FocusCutSelection, or 0.
        internal StateMachineNative.FocusResult KeyAndWait(
            RiveKey key, RiveKeyModifiers modifiers, bool pressed, bool repeat, uint selection, bool collectEvents)
        {
            return InputAndWait(
                (id, flags) => StateMachineNative.SendKey(id, m_stateMachine.NativeStateMachine, key, modifiers, pressed, repeat, flags),
                collectEvents,
                selection);
        }

        /// Main thread. A key for the focused node without waiting.
        internal void KeyLater(
            RiveKey key, RiveKeyModifiers modifiers, bool pressed, bool repeat, uint selection, bool collectEvents, Action<StateMachineNative.FocusResult> landed)
        {
            InputLater(
                (id, flags) => StateMachineNative.SendKey(id, m_stateMachine.NativeStateMachine, key, modifiers, pressed, repeat, flags),
                collectEvents,
                landed,
                selection);
        }

        /// Main thread. Committed text for the focused node, waiting for whether it was claimed.
        internal StateMachineNative.FocusResult TextAndWait(string text, bool collectEvents)
        {
            return InputAndWait(
                (id, flags) => StateMachineNative.SendText(id, m_stateMachine.NativeStateMachine, text, flags),
                collectEvents);
        }

        /// Main thread. Committed text for the focused node without waiting.
        internal void TextLater(string text, bool collectEvents, Action<StateMachineNative.FocusResult> landed)
        {
            InputLater(
                (id, flags) => StateMachineNative.SendText(id, m_stateMachine.NativeStateMachine, text, flags),
                collectEvents,
                landed);
        }

        /// Main thread. Sends the panel's pads that changed, or releases them
        /// when it isn't listening, and forgets ones that went away. Doesn't
        /// wait. True if anything went out.
        internal bool SyncGamepads(GamepadFrame frame, bool live)
        {
            GamepadTracker gamepads = m_stateMachine.Gamepads;
            bool sent = Send(gamepads.DisconnectFromPanelBut(frame.DeviceIds));
            if (!live)
            {
                return Send(gamepads.ReleaseFromPanel()) || sent;
            }
            for (int i = 0; i < frame.Count; i++)
            {
                GamepadFrame.Pad pad = frame[i];
                sent = Send(gamepads.SetFromPanel(pad.DeviceId, pad.Buttons, pad.Axes)) || sent;
            }
            return sent;
        }

        private bool Send(byte[] gamepadBatch)
        {
            StateMachineNative.SendGamepad(m_stateMachine.NativeStateMachine, gamepadBatch);
            return gamepadBatch != null;
        }

        // Focus moves, keys and text all reply with the focus they left, and
        // apply on Rive's thread after a change.
        private StateMachineNative.FocusResult InputAndWait(Func<ulong, uint, bool> send, bool collectEvents, uint extraFlags = 0)
        {
            StateMachineNative.FocusResult result = default;
            int version = ++m_focusVersion;
            uint flags = InputFlags(collectEvents) | extraFlags;
            RequestTicket ticket = CommandTransport.Send(
                id => send(id, flags),
                (batch, message) => result = ReadFocus(batch, message, version, flags));
            CommandTransport.Join(ref ticket);
            return result;
        }

        private void InputLater(
            Func<ulong, uint, bool> send, bool collectEvents, Action<StateMachineNative.FocusResult> landed, uint extraFlags = 0)
        {
            int version = ++m_focusVersion;
            uint flags = InputFlags(collectEvents) | extraFlags;
            CommandTransport.Send(
                id => send(id, flags),
                (batch, message) =>
                {
                    StateMachineNative.FocusResult result = ReadFocus(batch, message, version, flags);
                    landed?.Invoke(result);
                },
                keep: false);
        }

        private static uint InputFlags(bool collectEvents)
        {
            return StateMachineNative.FocusApply
                   | (collectEvents ? StateMachineNative.FocusCollectEvents : 0);
        }

        /// Main thread. Raised when a script moved focus through the state
        /// machine, outside the widget.
        internal event Action FocusChangedOutside;

        private void OnStateMachineFocusChanged(StateMachineNative.FocusResult result)
        {
            // Newer than any tick still out.
            m_focusVersion++;
            m_focus = result.State;
            FocusChangedOutside?.Invoke();
        }

        /// Main thread. Clearing is never waited on, so this shows it
        /// straight away.
        internal void ForgetFocus()
        {
            m_focus = m_focus.WithoutFocus();
        }

        // Keeps the state only if no newer focus command went out since, like ApplyFocus.
        private StateMachineNative.FocusResult ReadFocus(HostMessageBatch batch, HostMessage message, int version, uint flags)
        {
            var reader = new PayloadReader(batch, message);
            StateMachineNative.FocusResult result = StateMachineNative.ReadFocus(ref reader, m_focusEventScratch, flags);
            for (int i = 0; i < m_focusEventScratch.Count; i++)
            {
                m_events.Enqueue(m_focusEventScratch[i]);
            }
            m_focusEventScratch.Clear();
            if (result.Found && version == m_focusVersion)
            {
                m_focus = result.State;
            }
            return result;
        }

        /// Sends the artboard, its state machine and a size check without
        /// waiting. Finishes with them once they land, or with why not.
        internal static Future<ArtboardLoadHelper.Prepared> PrepareLoad(
            File file,
            string artboardName,
            string stateMachineName,
            bool describeFile)
        {
            var prepared = new ArtboardLoadHelper.Prepared();
            FileContents contents = file.Contents;
            int artboardIndex = string.IsNullOrEmpty(artboardName) ? 0 : contents.ArtboardIndex(artboardName);
            if (artboardIndex < 0 || artboardIndex >= contents.Artboards.Length)
            {
                file.ReportMissingArtboard(artboardName);
                prepared.Error = ArtboardLoadHelper.ArtboardNotFound(artboardName);
                return Future<ArtboardLoadHelper.Prepared>.FromResult(prepared);
            }
            FileContents.ArtboardInfo info = contents.Artboards[artboardIndex];
            string machineName = string.IsNullOrEmpty(stateMachineName)
                ? (info.StateMachineNames.Length > 0 ? info.StateMachineNames[0] : null)
                : stateMachineName;

            bool artboardFound = false;
            bool machineFound = false;
            NativeArtboardHandle artboard = ArtboardNative.InstantiateAsync(
                file.NativeFile, info.Name, found => artboardFound = found);
            NativeStateMachineHandle machine = machineName != null
                ? StateMachineNative.InstantiateAsync(artboard, machineName, found => machineFound = found)
                : default;
            return ArtboardNative.GetInfoAsync(artboard, size =>
            {
                // Replies land in order, so both lookups are in by now.
                if (!artboardFound)
                {
                    prepared.Error = ArtboardLoadHelper.ArtboardNotFound(artboardName);
                    return prepared;
                }
                var loaded = new Artboard(artboard, file, info);
                prepared.Artboard = loaded;
                prepared.Width = size.Size.Width;
                prepared.Height = size.Size.Height;
                prepared.HasAudio = size.HasAudio;
                if (!machineFound)
                {
                    Artboard.LogMissingStateMachine(string.IsNullOrEmpty(stateMachineName) ? null : stateMachineName);
                    prepared.Error = ArtboardLoadHelper.StateMachineNotFound(stateMachineName, artboardName);
                    return prepared;
                }
                prepared.StateMachine = new StateMachine(machine, loaded, machineName);
                if (describeFile)
                {
                    prepared.Contents = contents;
                }
                return prepared;
            });
        }

        /// Sends a pointer event for the frame point in work. With settle, a
        /// down or up that hits advances by 0 on the server, so what it set
        /// settles.
        internal bool SendPointer(ulong requestId, in RiveWidget.PointerEventWork work, bool settle = true)
        {
            uint flags = StateMachineNative.PointerInFrame
                         | (settle ? StateMachineNative.PointerSettle : 0)
                         | (work.CollectEvents ? StateMachineNative.PointerCollectEvents : 0);
            return StateMachineNative.SendPointer(
                requestId, m_stateMachine.NativeStateMachine, m_artboard.NativeArtboard, ToNative(work.Kind),
                work.ScreenPosition, work.PointerId, flags, work.ScreenRect, work.Fit, work.Alignment);
        }

        /// Reads a pointer reply and queues its events. True if it hit.
        internal bool ReadPointer(ref PayloadReader reader)
        {
            return ReadPointerResult(ref reader) != 0;
        }

        // The HitResult as a number.
        private uint ReadPointerResult(ref PayloadReader reader)
        {
            m_collectScratch.Clear();
            uint hit = StateMachineNative.ReadPointer(ref reader, m_collectScratch);
            for (int i = 0; i < m_collectScratch.Count; i++)
            {
                m_events.Enqueue(m_collectScratch[i]);
            }
            m_collectScratch.Clear();
            return hit;
        }

        private PointerWait m_pointerWait;

        /// Main thread. Sends a pointer event and waits, and its events queue
        /// like an async one's. No settle, the caller advances after a hit.
        /// Returns the HitResult as a number.
        internal uint PointerAndWait(in RiveWidget.PointerEventWork work)
        {
            ThreadCheck.Main();
            PointerWait wait = m_pointerWait ?? new PointerWait(this);
            // Out while in use, so a call made while this one waits gets its own.
            m_pointerWait = null;
            wait.Work = work;
            wait.Hit = 0;
            RequestTicket ticket = CommandTransport.Send(wait.Send, wait.OnReply);
            CommandTransport.Join(ref ticket);
            uint hit = wait.Hit;
            m_pointerWait = wait;
            return hit;
        }

        // Made once per widget, so a pointer event doesn't allocate.
        private sealed class PointerWait
        {
            internal readonly Action<ulong> Send;
            internal readonly Action<HostMessageBatch, HostMessage> OnReply;
            internal RiveWidget.PointerEventWork Work;
            internal uint Hit;

            internal PointerWait(WidgetCore core)
            {
                Send = id => core.SendPointer(id, Work, settle: false);
                OnReply = (batch, message) =>
                {
                    var reader = new PayloadReader(batch, message);
                    Hit = core.ReadPointerResult(ref reader);
                };
            }
        }

        private static StateMachineNative.PointerKind ToNative(RiveWidget.PointerEventKind kind)
        {
            switch (kind)
            {
                case RiveWidget.PointerEventKind.Down:
                    return StateMachineNative.PointerKind.Down;
                case RiveWidget.PointerEventKind.Up:
                    return StateMachineNative.PointerKind.Up;
                case RiveWidget.PointerEventKind.Exit:
                    return StateMachineNative.PointerKind.Exit;
                default:
                    return StateMachineNative.PointerKind.Move;
            }
        }

        /// Main thread.
        internal bool TryTakeEvent(out ReportedEventData data)
        {
            ThreadCheck.Main();
            return m_events.TryDequeue(out data);
        }

        internal bool HasEvents => !m_events.IsEmpty;
    }
}
