using System;
using System.Collections.Generic;
using Rive.Host;
using Rive.Producer;
using UnityEngine;

namespace Rive.Components
{
    /// <summary>
    /// One widget's part of an advance. The main thread fills the inputs
    /// before it goes out, the reply fills the outputs, and the main thread
    /// reads them once it lands. Nothing here is live state.
    /// </summary>
    internal sealed class WidgetAdvance
    {
        internal WidgetBehaviour Widget;
        internal float Delta;

        // Inputs, taken on the main thread.
        internal bool Active;
        internal WidgetCore Core;
        internal float Speed;
        internal bool CollectEvents;
        internal bool LayoutFix;
        internal int SizeVersion;
        // Whether it added a tick entry.
        internal bool HasEntry;

        // Outputs, read from the reply.
        internal bool HasSize;
        internal Vector2 ArtboardSize;

        /// For a widget no panel advances. Sends its entry on its own and
        /// waits for it.
        internal void TickAndWait(StateMachineNative.TickSender sender)
        {
            sender.Entries.Clear();
            HasEntry = Widget.WriteAdvance(this, sender.Entries);
            if (!HasEntry)
            {
                return;
            }
            m_onTickReply ??= (batch, message) =>
            {
                var reader = new PayloadReader(batch, message);
                ReadTick(ref reader);
            };
            RequestTicket ticket = sender.Send(m_onTickReply, keep: true);
            CommandTransport.Join(ref ticket);
        }

        private Action<HostMessageBatch, HostMessage> m_onTickReply;

        /// Reads this widget's entry of the tick reply.
        internal void ReadTick(ref PayloadReader reader)
        {
            StateMachineNative.TickResult result = Core.ReadTick(ref reader);
            HasSize = result.Found;
            ArtboardSize = new Vector2(result.ArtboardSize.Width, result.ArtboardSize.Height);
        }

        internal void Clear()
        {
            Widget = null;
            Delta = 0f;
            Active = false;
            Core = null;
            Speed = 0f;
            CollectEvents = false;
            LayoutFix = false;
            SizeVersion = 0;
            HasEntry = false;
            HasSize = false;
            ArtboardSize = default;
        }
    }

    /// <summary>
    /// A panel's advances. One is out at a time, and time that arrives while
    /// it's out goes into the next one. Each lands on the main thread, where
    /// widgets pick up what it found and TickAsync callers are finished.
    /// </summary>
    internal sealed class PanelAdvance
    {
        internal sealed class Request
        {
            internal readonly List<WidgetAdvance> Widgets = new List<WidgetAdvance>();
            internal readonly List<FutureState<bool>> Waiters =
                new List<FutureState<bool>>();
            internal readonly PayloadWriter Entries = new PayloadWriter();
            internal int Count;
            internal PanelAdvance Owner;
            // When it went out, only while the pacing overlay is on.
            internal double SentAt;
        }

        /// <summary>
        /// What the pacing overlay shows for a panel. The overlay reads and
        /// clears it a couple of times a second.
        /// </summary>
        internal sealed class PacingStats
        {
            internal int Sent;
            internal int Landed;
            // Frames that had time to send but an advance was still out.
            internal int Skipped;
            internal double RoundTripSum;
            internal double RoundTripMax;

            internal void Clear()
            {
                Sent = 0;
                Landed = 0;
                Skipped = 0;
                RoundTripSum = 0;
                RoundTripMax = 0;
            }
        }

        /// Null unless the pacing overlay is showing this panel.
        internal PacingStats Pacing;

        internal sealed class Landed
        {
        }

        private readonly ProducerChannel<Request, Landed> m_channel;
        private Request m_building;

        internal PanelAdvance()
        {
            m_channel = new ProducerChannel<Request, Landed>(
                ChannelMode.Fifo,
                Tick,
                ReadTick,
                OnLanded,
                Reset);
        }

        internal bool InFlight => m_channel.HasInFlight;

        /// Starts the next advance. Add widgets and waiters, then Send.
        internal void Begin()
        {
            if (m_building != null)
            {
                m_channel.Discard(m_building);
            }
            m_building = m_channel.Begin();
            m_building.Owner = this;
        }

        internal void Add(WidgetBehaviour widget, float delta)
        {
            Request request = m_building;
            if (request.Count == request.Widgets.Count)
            {
                request.Widgets.Add(new WidgetAdvance());
            }
            WidgetAdvance slot = request.Widgets[request.Count++];
            slot.Widget = widget;
            slot.Delta = delta;
            widget.PrepareAdvance(slot);
        }

        internal void AddWaiters(List<FutureState<bool>> waiters)
        {
            m_building.Waiters.AddRange(waiters);
        }

        internal void Send()
        {
            Request request = m_building;
            m_building = null;
            if (Pacing != null)
            {
                Pacing.Sent++;
                request.SentAt = Time.realtimeSinceStartupAsDouble;
            }
            if (ImagePipelineTrace.ForTests != null)
            {
                for (int i = 0; i < request.Count; i++)
                {
                    ImagePipelineTrace.ForTests(ImagePipelineTrace.Step.AdvanceSent, 0, request.Widgets[i].Widget);
                }
            }
            m_channel.Send(request);
            // No producer means it ran inline and can land now.
            m_channel.Poll();
        }

        /// Lands the advance if it has finished. Never waits.
        internal void Poll()
        {
            m_channel.Poll();
        }

        /// Waits for the advance and lands it.
        internal void Join()
        {
            m_channel.JoinAll();
        }

        /// Lets go of what's out without waiting. It still runs, in order, but
        /// never lands, and its waiters finish with false.
        internal void Drop()
        {
            m_channel.VisitInFlight(s_failWaiters);
            m_channel.Drop(s_dropAll);
        }

        private static readonly System.Action<Request, Landed> s_failWaiters = FailWaiters;
        private static readonly System.Predicate<Request> s_dropAll = _ => true;

        private static void FailWaiters(Request request, Landed landed)
        {
            for (int i = 0; i < request.Waiters.Count; i++)
            {
                request.Waiters[i].Succeed(false);
            }
            request.Waiters.Clear();
        }

        // One tick routine for every widget in the pass.
        private static void Tick(Request request, ulong requestId)
        {
            request.Entries.Clear();
            for (int i = 0; i < request.Count; i++)
            {
                WidgetAdvance slot = request.Widgets[i];
                slot.HasEntry = slot.Widget.WriteAdvance(slot, request.Entries);
            }
            StateMachineNative.Tick(requestId, request.Entries);
        }

        private static void ReadTick(Request request, Landed landed, HostMessageBatch batch, HostMessage message)
        {
            var reader = new PayloadReader(batch, message);
            for (int i = 0; i < request.Count; i++)
            {
                WidgetAdvance slot = request.Widgets[i];
                if (slot.HasEntry)
                {
                    slot.ReadTick(ref reader);
                }
            }
        }

        private static void OnLanded(Request request, Landed landed)
        {
            PacingStats pacing = request.Owner?.Pacing;
            if (pacing != null && request.SentAt > 0)
            {
                double roundTrip = (Time.realtimeSinceStartupAsDouble - request.SentAt) * 1000.0;
                pacing.Landed++;
                pacing.RoundTripSum += roundTrip;
                pacing.RoundTripMax = Math.Max(pacing.RoundTripMax, roundTrip);
            }
            request.SentAt = 0;
            for (int i = 0; i < request.Count; i++)
            {
                WidgetAdvance slot = request.Widgets[i];
                ImagePipelineTrace.ForTests?.Invoke(ImagePipelineTrace.Step.AdvanceLanded, 0, slot.Widget);
                // Destroyed while it was out.
                if (slot.Widget != null)
                {
                    slot.Widget.ApplyAdvance(slot);
                }
            }
            FinishWaiters(request);
        }

        // Also runs for an advance a stopped producer dropped, so nothing
        // awaiting it hangs.
        private static void Reset(Request request, Landed landed)
        {
            FinishWaiters(request);
            for (int i = 0; i < request.Count; i++)
            {
                request.Widgets[i].Clear();
            }
            request.Count = 0;
            request.Entries.Clear();
        }

        private static void FinishWaiters(Request request)
        {
            for (int i = 0; i < request.Waiters.Count; i++)
            {
                request.Waiters[i].Succeed(true);
            }
            request.Waiters.Clear();
        }
    }
}
