using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Rive.Host
{
    /// <summary>
    /// Reads a routine's reply. Little endian, strings are a u32 byte length
    /// then UTF-8. Past the end it reads zeros and empty strings.
    /// </summary>
    internal struct PayloadReader
    {
        private readonly byte[] m_bytes;
        private readonly int m_end;
        private int m_offset;

        internal PayloadReader(HostMessageBatch batch, in HostMessage message)
        {
            m_bytes = batch.Bytes;
            m_offset = message.PayloadOffset;
            m_end = message.PayloadOffset + message.PayloadSize;
        }

        /// Most replies lead with a u32 ok.
        internal bool Ok() => U32() != 0;

        internal bool Bool() => U32() != 0;

        internal uint U32()
        {
            if (m_offset + 4 > m_end)
            {
                m_offset = m_end;
                return 0;
            }
            uint value = BitConverter.ToUInt32(m_bytes, m_offset);
            m_offset += 4;
            return value;
        }

        internal ulong U64()
        {
            ulong low = U32();
            ulong high = U32();
            return low | (high << 32);
        }

        internal float F32()
        {
            if (m_offset + 4 > m_end)
            {
                m_offset = m_end;
                return 0f;
            }
            float value = BitConverter.ToSingle(m_bytes, m_offset);
            m_offset += 4;
            return value;
        }

        internal string String()
        {
            int length = (int)U32();
            if (length <= 0 || m_offset + length > m_end)
            {
                m_offset = Math.Min(m_offset + Math.Max(length, 0), m_end);
                return string.Empty;
            }
            string value = Encoding.UTF8.GetString(m_bytes, m_offset, length);
            m_offset += length;
            return value;
        }
    }

    /// <summary>
    /// Packs what a routine takes. Reused, so Clear it before each use.
    /// </summary>
    internal sealed class PayloadWriter
    {
        private byte[] m_bytes = new byte[256];
        private int m_size;

        internal byte[] Bytes => m_bytes;

        internal int Size => m_size;

        internal void Clear()
        {
            m_size = 0;
        }

        internal void U32(uint value)
        {
            Reserve(4);
            m_bytes[m_size] = (byte)value;
            m_bytes[m_size + 1] = (byte)(value >> 8);
            m_bytes[m_size + 2] = (byte)(value >> 16);
            m_bytes[m_size + 3] = (byte)(value >> 24);
            m_size += 4;
        }

        internal void U64(ulong value)
        {
            U32((uint)value);
            U32((uint)(value >> 32));
        }

        /// A u32 byte length, then UTF-8.
        internal void String(string value)
        {
            int length = value == null ? 0 : Encoding.UTF8.GetByteCount(value);
            U32((uint)length);
            Reserve(length);
            if (length > 0)
            {
                Encoding.UTF8.GetBytes(value, 0, value.Length, m_bytes, m_size);
            }
            m_size += length;
        }

        internal void F32(float value)
        {
            U32(new FloatBits { Float = value }.Bits);
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct FloatBits
        {
            [FieldOffset(0)] internal float Float;
            [FieldOffset(0)] internal uint Bits;
        }

        private void Reserve(int count)
        {
            if (m_size + count > m_bytes.Length)
            {
                Array.Resize(ref m_bytes, Math.Max(m_bytes.Length * 2, m_size + count));
            }
        }
    }
}
