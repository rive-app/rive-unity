using System;
using System.Collections.Generic;

namespace Rive.Host
{
    /// <summary>
    /// What a drained message came from. Mirrors HostMessageKind in
    /// command_host.hpp.
    /// </summary>
    internal enum HostMessageKind : uint
    {
        Routine = 1,
    }

    /// <summary>
    /// Tags for the messages Unity routines post. Mirrors RoutineTag in
    /// routine_tags.hpp.
    /// </summary>
    internal enum RoutineTag : uint
    {
        Echo = 1,
        Hold = 2,
        FileLoaded = 3,
        FileAssetChanged = 4,
        AssetDecoded = 5,
        FileAssetList = 6,
        FileAlive = 7,
        ArtboardInstanced = 8,
        StateMachineInstanced = 9,
        ArtboardInfo = 10,
        ArtboardWrite = 11,
        Tick = 12,
        Inputs = 13,
        TextRun = 14,
        ReportedEvents = 15,
        RenderQueue = 16,
        CanvasFlush = 17,
        RenderImages = 18,
        ViewModels = 19,
        ViewModelCapture = 20,
        ViewModelBind = 21,
        Pointer = 22,
    }

    internal struct HostMessage
    {
        public HostMessageKind Kind;
        // The routine tag, or the core message for other kinds.
        public uint Code;
        public ulong Handle;
        public ulong RequestId;
        // Where the payload sits in the batch's bytes.
        public int PayloadOffset;
        public int PayloadSize;
    }

    /// <summary>
    /// One drain's messages, plus the bytes their payloads point into. Reused
    /// from drain to drain.
    /// </summary>
    internal sealed class HostMessageBatch
    {
        // Mirrors HostMessageRecord in command_host.hpp.
        internal const int RecordSize = 32;

        public byte[] Bytes = new byte[256];
        public readonly List<HostMessage> Messages = new List<HostMessage>();

        public byte[] CopyPayload(in HostMessage message)
        {
            var payload = new byte[message.PayloadSize];
            Buffer.BlockCopy(
                Bytes,
                message.PayloadOffset,
                payload,
                0,
                message.PayloadSize
            );
            return payload;
        }

        internal void Parse(int size)
        {
            Messages.Clear();
            int offset = 0;
            while (offset + RecordSize <= size)
            {
                var message = new HostMessage
                {
                    Kind = (HostMessageKind)BitConverter.ToUInt32(Bytes, offset),
                    Code = BitConverter.ToUInt32(Bytes, offset + 4),
                    Handle = BitConverter.ToUInt64(Bytes, offset + 8),
                    RequestId = BitConverter.ToUInt64(Bytes, offset + 16),
                    PayloadSize = (int)BitConverter.ToUInt32(Bytes, offset + 24),
                    PayloadOffset = offset + RecordSize,
                };
                Messages.Add(message);
                offset += RecordSize + ((message.PayloadSize + 7) & ~7);
            }
        }
    }
}
