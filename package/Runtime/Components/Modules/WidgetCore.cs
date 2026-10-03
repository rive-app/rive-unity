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
    /// A loaded widget's state machine: advancing it, pointer input and
    /// reported event collection. Its advances go out as tick entries, and
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

        internal WidgetCore(Artboard artboard, StateMachine stateMachine)
        {
            m_artboard = artboard;
            m_stateMachine = stateMachine;
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

        private RequestTicket SendAdvance(float deltaTime, float speed, bool collectEvents, bool keep)
        {
            m_advanceSender ??= new StateMachineNative.TickSender();
            m_onAdvanceReply ??= (batch, message) =>
            {
                var reader = new PayloadReader(batch, message);
                ReadTick(ref reader);
            };
            m_advanceSender.Entries.Clear();
            WriteTick(m_advanceSender.Entries, deltaTime, speed, collectEvents, layoutFix: false);
            return m_advanceSender.Send(m_onAdvanceReply, keep);
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

        /// Sends a pointer event for the frame point in work. A down or up
        /// that hits advances by 0 on the server, so what it set settles.
        internal bool SendPointer(ulong requestId, in RiveWidget.PointerEventWork work)
        {
            uint flags = StateMachineNative.PointerInFrame | StateMachineNative.PointerSettle
                         | (work.CollectEvents ? StateMachineNative.PointerCollectEvents : 0);
            return StateMachineNative.SendPointer(
                requestId, m_stateMachine.NativeStateMachine, m_artboard.NativeArtboard, ToNative(work.Kind),
                work.ScreenPosition, work.PointerId, flags, work.ScreenRect, work.Fit, work.Alignment);
        }

        /// Reads a pointer reply and queues its events. True if it hit.
        internal bool ReadPointer(ref PayloadReader reader)
        {
            m_collectScratch.Clear();
            bool hit = StateMachineNative.ReadPointer(ref reader, m_collectScratch) != 0;
            for (int i = 0; i < m_collectScratch.Count; i++)
            {
                m_events.Enqueue(m_collectScratch[i]);
            }
            m_collectScratch.Clear();
            return hit;
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
            return m_events.TryDequeue(out data);
        }

        internal bool HasEvents => !m_events.IsEmpty;
    }
}
