using Rive.Producer;

namespace Rive
{
    /// <summary>
    /// A renderer's asynchronous records. A record that hasn't gone out is
    /// handed back and updated in place, so a busy server has at most one per
    /// renderer and always records the newest draw list.
    /// </summary>
    internal sealed class RecordStaging
    {
        internal sealed class Record
        {
            internal DrawOp[] Ops;
            internal int Count;
            internal NativeRenderQueueHandle Queue;
            internal uint Generation;
            internal bool DirtCheckEnabled;
            internal bool ForceRenderNext;
            internal uint Frame;
        }

        internal sealed class Recorded
        {
        }

        private readonly ProducerChannel<Record, Recorded> m_channel;

        internal RecordStaging()
        {
            m_channel = new ProducerChannel<Record, Recorded>(
                ChannelMode.LatestValue,
                SendRecord,
                null,
                null,
                Reset);
        }

        /// The record to fill. If one is still waiting it comes back holding
        /// what was sent, so a force flag can be merged into it.
        internal Record Begin()
        {
            m_channel.Poll();
            // No key, so a record is never held behind the one before it. Each
            // frame's record goes out when it is made and the server takes them
            // in order; the native queue's accept gate is the backpressure.
            return m_channel.Begin();
        }

        internal void Send(Record record)
        {
            m_channel.Send(record);
        }

        /// Tests. Sends anything held back and waits until the server has
        /// recorded it all, so the next render event replays the newest.
        internal void SettleForTests()
        {
            do
            {
                m_channel.Poll();
                Rive.Host.CommandTransport.Barrier();
                m_channel.Poll();
            }
            while (m_channel.HasInFlight);
        }

        private static void SendRecord(Record record, ulong requestId)
        {
            if (PacingCounters.Enabled)
            {
                PacingCounters.RecordsSent++;
            }
            CanvasNative.RecordDrawList(
                requestId,
                record.Queue,
                record.Ops,
                (uint)record.Count,
                record.Generation,
                record.DirtCheckEnabled,
                record.ForceRenderNext,
                record.Frame);
        }

        // Keeps the ops array so the next record reuses it.
        private static void Reset(Record record, Recorded recorded)
        {
            record.Count = 0;
            record.Queue = default;
            record.Generation = 0;
            record.DirtCheckEnabled = false;
            record.ForceRenderNext = false;
            record.Frame = 0;
        }
    }
}
