using System;
using System.Collections.Generic;
using Rive.Producer;
using Rive.Utils;
using UnityEngine;
using Rive.Host;

namespace Rive.Components
{
    /// <summary>
    /// Pointer events for asynchronous panels. Each one runs as a routine
    /// and lands on the main thread in the order it was queued, without
    /// holding up the frame.
    /// </summary>
    internal sealed class PointerInputModule : FrameModule
    {
        internal sealed class Request
        {
            internal RivePanel Panel;
            internal RiveWidget Widget;
            internal RiveWidget.PointerEventKind Kind;
            internal RiveWidget.PointerEventWork Work;
            internal int PointerId;
            // The values channel capture sent straight after this job.
            internal object Capture;
            // Main thread only. The panel went away, so the widget isn't told.
            internal bool Cancelled;
        }

        internal sealed class Result
        {
            internal bool Hit;
            internal bool Ran;
        }

        private readonly ProducerChannel<Request, Result> m_channel;
        private readonly Action<Request, Result> m_markCancelled;
        private readonly List<Request> m_movesToCancel = new List<Request>();
        private RivePanel m_unregistering;
        private Request m_lastMove;
        private RiveWidget m_landedWidget;
        private bool m_disposed;

        internal PointerInputModule()
        {
            m_channel = new ProducerChannel<Request, Result>(
                ChannelMode.Fifo,
                (request, requestId) => request.Work.Core.SendPointer(requestId, request.Work),
                ReadReply,
                OnLanded,
                Reset,
                cancelSent: StateMachineNative.CancelPointer);
            m_markCancelled = MarkCancelled;
        }

        internal void Queue(
            RivePanel panel,
            RiveWidget widget,
            Vector2 point,
            int pointerId,
            RiveWidget.PointerEventKind kind)
        {
            if (panel == null || !panel.isActiveAndEnabled ||
                widget == null || widget.Status != WidgetStatus.Loaded)
            {
                return;
            }

            if (!widget.TryPreparePointerEvent(point, pointerId, kind, out var work))
            {
                return;
            }

            // Only the newest move matters, as long as nothing came between.
            if (kind == RiveWidget.PointerEventKind.Move && m_lastMove != null &&
                ReferenceEquals(m_lastMove.Panel, panel) &&
                ReferenceEquals(m_lastMove.Widget, widget) &&
                m_lastMove.PointerId == pointerId)
            {
                // Its capture goes too, the one after this move covers it.
                object capture = m_lastMove.Capture;
                if (m_channel.TryCancel(m_lastMove))
                {
                    PropertyCallbacksHub.Instance.CancelPointerCapture(capture);
                }
            }

            Request request = m_channel.Begin();
            request.Panel = panel;
            request.Widget = widget;
            request.Kind = kind;
            request.PointerId = pointerId;
            request.Work = work;
            m_channel.Send(request);
            // Keeps the value this event set, even when the next one resets it.
            request.Capture = PropertyCallbacksHub.Instance.SubmitPointerCapture();
            m_lastMove = kind == RiveWidget.PointerEventKind.Move ? request : null;
        }

        internal override void Poll()
        {
            Process();
        }

        // Pointer results never gate a synchronous tick, they just land.
        internal override void JoinForSync()
        {
            Process();
        }

        internal override void OnPanelRemoved(RivePanel panel)
        {
            UnregisterPanel(panel);
        }

        /// <summary>
        /// Lands finished pointer events without holding up panel ticks.
        /// </summary>
        internal void Process()
        {
            using var noWait = CommandTransport.NoWait("pointer process");
            while (m_channel.PollOne())
            {
                DispatchLandedWidget();
            }
        }

        /// <summary>
        /// Waits for every queued event and lands it.
        /// </summary>
        internal void Join()
        {
            while (m_channel.JoinNext())
            {
                DispatchLandedWidget();
            }
        }

        /// <summary>
        /// The panel won't be told about its events any more. Queued moves are
        /// dropped, but Down, Up and Exit still reach the state machine, or a
        /// re-enabled panel could keep a pressed or hovered pointer.
        /// </summary>
        internal void UnregisterPanel(RivePanel panel)
        {
            m_unregistering = panel;
            m_channel.VisitInFlight(m_markCancelled);
            m_unregistering = null;

            for (int i = 0; i < m_movesToCancel.Count; i++)
            {
                m_channel.TryCancel(m_movesToCancel[i]);
            }
            m_movesToCancel.Clear();
        }

        internal override void Dispose()
        {
            // Let queued events reach native, but tell nobody.
            m_disposed = true;
            m_channel.JoinAll();
            m_lastMove = null;
        }

        /// Tests only. Kinds still in flight, and whether each has run.
        internal void InFlightForTests(List<RiveWidget.PointerEventKind> kinds, List<bool> ran)
        {
            m_channel.VisitInFlight((request, result) =>
            {
                kinds.Add(request.Kind);
                ran?.Add(result.Ran);
            });
        }

        private void MarkCancelled(Request request, Result result)
        {
            if (!ReferenceEquals(request.Panel, m_unregistering))
            {
                return;
            }
            request.Cancelled = true;
            if (request.Kind == RiveWidget.PointerEventKind.Move)
            {
                m_movesToCancel.Add(request);
            }
        }

        private static void ReadReply(Request request, Result result, HostMessageBatch batch, HostMessage message)
        {
            var reader = new PayloadReader(batch, message);
            result.Hit = request.Work.Core.ReadPointer(ref reader);
            result.Ran = true;
        }

        private void OnLanded(Request request, Result result)
        {
            if (m_disposed)
            {
                return;
            }

            if (!request.Cancelled && request.Panel != null &&
                request.Panel.isActiveAndEnabled && request.Widget != null &&
                request.Widget.Status == WidgetStatus.Loaded &&
                ReferenceEquals(request.Widget.Core, request.Work.Core))
            {
                request.Widget.CompletePointerEvent(request.Kind, result.Hit);
                m_landedWidget = request.Widget;
            }
        }

        // Outside onLanded, so a callback here may switch execution mode.
        private void DispatchLandedWidget()
        {
            RiveWidget widget = m_landedWidget;
            m_landedWidget = null;
            if (widget == null || widget.Status != WidgetStatus.Loaded)
            {
                return;
            }
            try
            {
                widget.DispatchAdvanceCallbacks();
            }
            catch (Exception e)
            {
                DebugLogger.Instance.LogException(e);
            }
        }

        private void Reset(Request request, Result result)
        {
            if (ReferenceEquals(m_lastMove, request))
            {
                m_lastMove = null;
            }
            request.Panel = null;
            request.Widget = null;
            request.Work = default;
            request.PointerId = 0;
            request.Capture = null;
            request.Cancelled = false;
            result.Hit = false;
            result.Ran = false;
        }
    }
}
