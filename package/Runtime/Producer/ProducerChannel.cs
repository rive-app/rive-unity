using System;
using System.Collections.Generic;
using Rive.Host;
using Rive.Utils;

namespace Rive.Producer
{
    internal enum ChannelMode
    {
        /// Every request runs and lands, in send order.
        Fifo = 0,

        /// One request per key goes out at a time. The next waits unsent, and
        /// Begin with that key hands it back so it goes out once with the
        /// newest contents.
        LatestValue = 1,
    }

    /// <summary>
    /// Sends requests to the command server and hands the results back on the
    /// main thread, in send order, without waiting. JoinAll is the only call
    /// that waits.
    ///
    /// send queues the request's routine with the request id it's given, and
    /// parse fills the result from its reply. A request belongs to the channel
    /// from Send until afterLanded returns, so don't touch it in between.
    /// Requests, results and entries are pooled. Main thread only.
    ///
    /// onLanded is for our own bookkeeping. User code goes in afterLanded,
    /// which runs outside the delivery window before the next entry lands.
    /// While it runs, a nested Poll or Join on this channel does nothing, so
    /// the outer loop keeps delivering in order.
    /// </summary>
    internal sealed class ProducerChannel<TRequest, TResult>
        where TRequest : class, new()
        where TResult : class, new()
    {
        private enum EntryState
        {
            // Waiting for the key's earlier request to land.
            Held = 0,
            Sent = 1,
            Landed = 2,
        }

        private sealed class Entry
        {
            internal TRequest Request;
            internal TResult Result;
            internal object Key;
            internal EntryState State;
            internal RequestTicket Ticket;
            // The request id it last went out with. Cleared on reuse, so a late
            // reply can't land on the next request.
            internal ulong SentId;
            // Made once per entry and reused, so sending allocates nothing.
            internal Action<ulong> SendNow;
            internal Action<HostMessageBatch, HostMessage> OnReply;
            internal bool Failed;
            internal bool Dropped;
            // Begin handed back an entry that's already held.
            internal bool Reclaimed;
        }

        private readonly ChannelMode m_mode;
        private readonly Action<TRequest, ulong> m_send;
        private readonly Action<TRequest, TResult, HostMessageBatch, HostMessage> m_parse;
        private readonly Action<TRequest, TResult> m_onLanded;
        private readonly Action<TRequest, TResult> m_afterLanded;
        private readonly Action<TRequest, TResult> m_reset;
        private readonly Func<ulong, bool> m_cancelSent;

        // Sent or held, waiting to land, in send order.
        private readonly List<Entry> m_inFlight = new List<Entry>();
        // Handed out by Begin and not sent yet.
        private readonly List<Entry> m_begun = new List<Entry>();

        private readonly Stack<Entry> m_entryPool = new Stack<Entry>();
        private readonly Stack<TRequest> m_requestPool = new Stack<TRequest>();
        private readonly Stack<TResult> m_resultPool = new Stack<TResult>();

        private bool m_delivering;
        private bool m_inAfterLanded;

        /// onLanded, then afterLanded, run on the main thread in send order.
        /// reset clears a pair before it's reused, delivered or not.
        /// cancelSent takes a sent request's id and says whether it stopped
        /// it before it ran. Without it only held requests cancel.
        internal ProducerChannel(
            ChannelMode mode,
            Action<TRequest, ulong> send,
            Action<TRequest, TResult, HostMessageBatch, HostMessage> parse,
            Action<TRequest, TResult> onLanded,
            Action<TRequest, TResult> reset,
            Action<TRequest, TResult> afterLanded = null,
            Func<ulong, bool> cancelSent = null)
        {
            m_mode = mode;
            m_send = send ?? throw new ArgumentNullException(nameof(send));
            m_parse = parse;
            m_onLanded = onLanded;
            m_afterLanded = afterLanded;
            m_reset = reset;
            m_cancelSent = cancelSent;
        }

        /// True while an entry's afterLanded runs.
        internal bool InAfterLanded => m_inAfterLanded;

        internal int InFlightCount => m_inFlight.Count;

        internal bool HasInFlight => m_inFlight.Count > 0;

        /// A request to fill in. With LatestValue and a key, the request still
        /// held for that key comes back instead, holding what was sent, so the
        /// caller can merge into it.
        internal TRequest Begin(object key = null)
        {
            if (m_mode == ChannelMode.LatestValue && key != null)
            {
                Entry held = FindHeld(key);
                if (held != null)
                {
                    held.Reclaimed = true;
                    m_begun.Add(held);
                    return held.Request;
                }
            }

            Entry entry = m_entryPool.Count > 0 ? m_entryPool.Pop() : new Entry();
            entry.Request = m_requestPool.Count > 0 ? m_requestPool.Pop() : new TRequest();
            entry.Result = m_resultPool.Count > 0 ? m_resultPool.Pop() : new TResult();
            entry.Key = key;
            entry.State = EntryState.Held;
            entry.Ticket = default;
            entry.Failed = false;
            entry.Dropped = false;
            entry.Reclaimed = false;
            m_begun.Add(entry);
            return entry.Request;
        }

        /// Hands the request over. Never waits.
        internal void Send(TRequest request)
        {
            Entry entry = TakeBegun(request);
            if (entry == null)
            {
                throw new InvalidOperationException(
                    "Send takes a request from this channel's Begin, once.");
            }

            if (entry.Reclaimed)
            {
                // Already held, and goes out when its turn comes.
                entry.Reclaimed = false;
                return;
            }

            m_inFlight.Add(entry);
            if (!KeyBusy(entry))
            {
                SendEntry(entry);
            }
        }

        // An earlier request with the same key is still out.
        private bool KeyBusy(Entry entry)
        {
            if (m_mode != ChannelMode.LatestValue || entry.Key == null)
            {
                return false;
            }
            for (int i = 0; i < m_inFlight.Count; i++)
            {
                Entry other = m_inFlight[i];
                if (other != entry && other.State == EntryState.Sent &&
                    Equals(other.Key, entry.Key))
                {
                    return true;
                }
            }
            return false;
        }

        private Entry FindHeld(object key)
        {
            for (int i = m_inFlight.Count - 1; i >= 0; i--)
            {
                Entry entry = m_inFlight[i];
                if (entry.State == EntryState.Held && Equals(entry.Key, key))
                {
                    return entry;
                }
            }
            return null;
        }

        private void SendEntry(Entry entry)
        {
            entry.State = EntryState.Sent;
            if (entry.SendNow == null)
            {
                MakeCallbacks(entry);
            }
            try
            {
                entry.Ticket = CommandTransport.Send(entry.SendNow, entry.OnReply, keep: false);
            }
            catch (Exception e)
            {
                // Nothing went out, so nothing will land.
                entry.Failed = true;
                entry.State = EntryState.Landed;
                DebugLogger.Instance.LogException(e);
            }
        }

        // Its own method, so the closure over entry is only made here and not on
        // every send.
        private void MakeCallbacks(Entry entry)
        {
            entry.SendNow = id =>
            {
                entry.SentId = id;
                m_send(entry.Request, id);
            };
            entry.OnReply = (batch, message) => Land(entry, batch, message);
        }

        private void Land(Entry entry, HostMessageBatch batch, HostMessage message)
        {
            // A reply to an earlier use of this entry.
            if (message.RequestId != entry.SentId)
            {
                return;
            }
            entry.State = EntryState.Landed;
            if (m_parse == null)
            {
                return;
            }
            try
            {
                m_parse(entry.Request, entry.Result, batch, message);
            }
            catch (Exception e)
            {
                entry.Failed = true;
                DebugLogger.Instance.LogException(e);
            }
        }

        // Sends the next held request for each key that has come free.
        private void SendFreed()
        {
            for (int i = 0; i < m_inFlight.Count; i++)
            {
                Entry entry = m_inFlight[i];
                if (entry.State == EntryState.Held && !KeyBusy(entry))
                {
                    SendEntry(entry);
                }
            }
        }

        /// Lands whatever has finished, in order, and stops at the first that
        /// hasn't. Never waits.
        internal void Poll()
        {
            while (PollOne())
            {
            }
        }

        /// Lands the oldest entry if it has finished. Never waits. For callers
        /// that do something between results.
        internal bool PollOne()
        {
            if (m_delivering || m_inAfterLanded || m_inFlight.Count == 0)
            {
                return false;
            }
            Entry entry = m_inFlight[0];
            if (!IsFinished(entry))
            {
                return false;
            }
            m_inFlight.RemoveAt(0);
            Land(entry);
            SendFreed();
            return true;
        }

        /// Waits for everything sent and lands it. Not from inside onLanded.
        internal void JoinAll()
        {
            while (JoinNext())
            {
            }
        }

        /// Waits for the oldest entry and lands it. False when there's nothing
        /// left, or the host stopped before it landed.
        internal bool JoinNext()
        {
            if (m_delivering)
            {
                throw new InvalidOperationException(
                    "JoinAll can't be called from onLanded. It would wait on "
                        + "results queued behind the one being delivered.");
            }
            if (m_inFlight.Count == 0 || m_inAfterLanded)
            {
                // Inside afterLanded the outer loop carries on in order.
                return false;
            }

            Entry entry = m_inFlight[0];
            if (entry.State == EntryState.Held)
            {
                SendEntry(entry);
            }
            CommandTransport.Join(ref entry.Ticket);
            return IsFinished(entry) && PollOne();
        }

        /// Recycles matching requests instead of delivering them.
        internal void Drop(Predicate<TRequest> match)
        {
            for (int i = m_inFlight.Count - 1; i >= 0; i--)
            {
                Entry entry = m_inFlight[i];
                if (!match(entry.Request))
                {
                    continue;
                }
                m_inFlight.RemoveAt(i);
                Retire(entry);
            }
            SendFreed();
        }

        /// Hands back a request from Begin that won't be sent.
        internal void Discard(TRequest request)
        {
            Entry entry = TakeBegun(request);
            if (entry == null)
            {
                return;
            }
            if (entry.Reclaimed)
            {
                // Still held with what it had before, so it goes out as is.
                entry.Reclaimed = false;
                return;
            }
            Recycle(entry);
        }

        /// True while the request is held and hasn't gone out.
        internal bool IsUnstarted(TRequest request)
        {
            Entry entry = FindInFlight(request);
            return entry != null && entry.State == EntryState.Held;
        }

        /// Recycles the request if it hasn't run. False once it has, and it
        /// lands as normal.
        internal bool TryCancel(TRequest request)
        {
            Entry entry = FindInFlight(request);
            if (entry == null)
            {
                return false;
            }
            // A sent one still replies, and the request id check drops it.
            bool stopped = entry.State == EntryState.Held ||
                           (entry.State == EntryState.Sent && m_cancelSent != null &&
                            m_cancelSent(entry.Ticket.Id));
            if (!stopped)
            {
                return false;
            }
            m_inFlight.Remove(entry);
            Recycle(entry);
            return true;
        }

        /// Main thread. Sees what's in flight, in send order.
        internal void VisitInFlight(Action<TRequest, TResult> visit)
        {
            for (int i = 0; i < m_inFlight.Count; i++)
            {
                visit(m_inFlight[i].Request, m_inFlight[i].Result);
            }
        }

        private Entry FindInFlight(TRequest request)
        {
            for (int i = m_inFlight.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(m_inFlight[i].Request, request))
                {
                    return m_inFlight[i];
                }
            }
            return null;
        }

        private Entry TakeBegun(TRequest request)
        {
            for (int i = 0; i < m_begun.Count; i++)
            {
                if (ReferenceEquals(m_begun[i].Request, request))
                {
                    Entry entry = m_begun[i];
                    m_begun.RemoveAt(i);
                    return entry;
                }
            }
            return null;
        }

        // A reply still on its way is ignored once the entry is reused.
        private void Retire(Entry entry)
        {
            entry.Dropped = true;
            Recycle(entry);
        }

        // Landed, or never going to. A stopped host means the reply isn't
        // coming, so it's dropped.
        private bool IsFinished(Entry entry)
        {
            if (entry.State == EntryState.Landed)
            {
                return true;
            }
            if (entry.State == EntryState.Sent && !CommandHost.IsRunning)
            {
                entry.Dropped = true;
                return true;
            }
            return false;
        }

        private void Land(Entry entry)
        {
            bool deliver = !entry.Dropped && !entry.Failed;
            if (deliver && m_onLanded != null)
            {
                m_delivering = true;
                try
                {
                    m_onLanded(entry.Request, entry.Result);
                }
                catch (Exception e)
                {
                    DebugLogger.Instance.LogException(e);
                }
                finally
                {
                    m_delivering = false;
                }
            }
            if (deliver && m_afterLanded != null)
            {
                m_inAfterLanded = true;
                try
                {
                    m_afterLanded(entry.Request, entry.Result);
                }
                catch (Exception e)
                {
                    DebugLogger.Instance.LogException(e);
                }
                finally
                {
                    m_inAfterLanded = false;
                }
            }
            Recycle(entry);
        }

        private void Recycle(Entry entry)
        {
            if (entry.Request != null)
            {
                try
                {
                    m_reset?.Invoke(entry.Request, entry.Result);
                }
                catch (Exception e)
                {
                    DebugLogger.Instance.LogException(e);
                }
                m_requestPool.Push(entry.Request);
                m_resultPool.Push(entry.Result);
            }
            entry.Request = null;
            entry.Result = null;
            entry.Key = null;
            entry.Ticket = default;
            entry.SentId = 0;
            m_entryPool.Push(entry);
        }
    }
}
