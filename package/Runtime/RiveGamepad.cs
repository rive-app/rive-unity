using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Rive
{
    /// <summary>
    /// A gamepad button, in the W3C standard gamepad layout. The names match the ones Rive scripts use.
    /// </summary>
    public enum RiveGamepadButton : byte
    {
        /// <summary>
        /// The bottom face button: A on Xbox, Cross on PlayStation.
        /// </summary>
        South = 0,
        /// <summary>
        /// The right face button: B on Xbox, Circle on PlayStation.
        /// </summary>
        East = 1,
        /// <summary>
        /// The left face button: X on Xbox, Square on PlayStation.
        /// </summary>
        West = 2,
        /// <summary>
        /// The top face button: Y on Xbox, Triangle on PlayStation.
        /// </summary>
        North = 3,
        LeftShoulder = 4,
        RightShoulder = 5,
        /// <summary>
        /// How far the left trigger is pulled, from 0 to 1.
        /// </summary>
        LeftTrigger = 6,
        /// <summary>
        /// How far the right trigger is pulled, from 0 to 1.
        /// </summary>
        RightTrigger = 7,
        /// <summary>
        /// The left center button: View on Xbox, Share or Create on PlayStation. Unity's select button.
        /// </summary>
        Back = 8,
        /// <summary>
        /// The right center button: Menu on Xbox, Options on PlayStation. Unity's start button.
        /// </summary>
        Forward = 9,
        LeftStick = 10,
        RightStick = 11,
        DpadUp = 12,
        DpadDown = 13,
        DpadLeft = 14,
        DpadRight = 15,
        /// <summary>
        /// The home or guide button. Unity's Gamepad doesn't read it.
        /// </summary>
        Start = 16,
    }

    /// <summary>
    /// A gamepad axis, in the W3C standard gamepad layout.
    /// </summary>
    public enum RiveGamepadAxis : byte
    {
        /// <summary>
        /// From -1 at the left to 1 at the right.
        /// </summary>
        LeftX = 0,
        /// <summary>
        /// From -1 at the top to 1 at the bottom, the opposite of Unity's sticks.
        /// </summary>
        LeftY = 1,
        RightX = 2,
        /// <summary>
        /// From -1 at the top to 1 at the bottom, the opposite of Unity's sticks.
        /// </summary>
        RightY = 3,
        /// <summary>
        /// From 0 to 1, the same as <see cref="RiveGamepadButton.LeftTrigger"/>.
        /// </summary>
        LeftTrigger = 4,
        /// <summary>
        /// From 0 to 1, the same as <see cref="RiveGamepadButton.RightTrigger"/>.
        /// </summary>
        RightTrigger = 5,
    }

    /// <summary>
    /// The gamepads one state machine has heard about, and their values as
    /// last sent. Builds batches in core's gamepad_batch.hpp wire format with
    /// only what changed, announcing a pad the first time it shows up.
    /// </summary>
    internal sealed class GamepadTracker
    {
        internal const int ButtonCount = 17;
        internal const int AxisCount = 6;

        private const uint WireVersion = 2;
        private const byte Connected = 0;
        private const byte Update = 1;
        private const byte Disconnected = 2;
        private const byte ButtonChange = 0;
        private const byte AxisChange = 1;

        private sealed class Pad
        {
            internal readonly float[] Buttons = new float[ButtonCount];
            internal readonly float[] Axes = new float[AxisCount];
            // Last set by a panel from Unity's gamepads. A script setting a value takes it over.
            internal bool FromPanel;
        }

        private readonly Dictionary<int, Pad> m_pads = new Dictionary<int, Pad>();
        private readonly List<byte> m_batch = new List<byte>();
        private readonly List<int> m_scratchIds = new List<int>();

        /// Every value of one of a panel's pads. Null when nothing changed.
        internal byte[] SetFromPanel(int deviceId, float[] buttons, float[] axes)
        {
            Begin();
            Pad pad = Announce(deviceId);
            pad.FromPanel = true;
            int countAt = BeginUpdate(deviceId);
            int changes = 0;
            for (int i = 0; i < ButtonCount; i++)
            {
                changes += Change(pad.Buttons, ButtonChange, i, buttons[i]);
            }
            for (int i = 0; i < AxisCount; i++)
            {
                changes += Change(pad.Axes, AxisChange, i, axes[i]);
            }
            EndUpdate(countAt, changes);
            return Finish();
        }

        /// One button. Null when it already had that value.
        internal byte[] SetButton(int deviceId, RiveGamepadButton button, float value)
        {
            return SetOne(deviceId, ButtonChange, (int)button, ButtonCount, value);
        }

        /// One axis. Null when it already had that value.
        internal byte[] SetAxis(int deviceId, RiveGamepadAxis axis, float value)
        {
            return SetOne(deviceId, AxisChange, (int)axis, AxisCount, value);
        }

        /// Forgets one pad. Null when it wasn't known.
        internal byte[] Disconnect(int deviceId)
        {
            if (!m_pads.Remove(deviceId))
            {
                return null;
            }
            Begin();
            WriteDisconnected(deviceId);
            return Finish();
        }

        /// Forgets a panel's pads that keep doesn't name. Null when there were none.
        internal byte[] DisconnectFromPanelBut(ICollection<int> keep)
        {
            m_scratchIds.Clear();
            foreach (KeyValuePair<int, Pad> entry in m_pads)
            {
                if (entry.Value.FromPanel && !keep.Contains(entry.Key))
                {
                    m_scratchIds.Add(entry.Key);
                }
            }
            if (m_scratchIds.Count == 0)
            {
                return null;
            }
            Begin();
            foreach (int deviceId in m_scratchIds)
            {
                m_pads.Remove(deviceId);
                WriteDisconnected(deviceId);
            }
            return Finish();
        }

        /// Puts a panel's pads at rest, so nothing stays held. Null when nothing was.
        internal byte[] ReleaseFromPanel()
        {
            Begin();
            foreach (KeyValuePair<int, Pad> entry in m_pads)
            {
                if (!entry.Value.FromPanel)
                {
                    continue;
                }
                int countAt = BeginUpdate(entry.Key);
                int changes = 0;
                for (int i = 0; i < ButtonCount; i++)
                {
                    changes += Change(entry.Value.Buttons, ButtonChange, i, 0f);
                }
                for (int i = 0; i < AxisCount; i++)
                {
                    changes += Change(entry.Value.Axes, AxisChange, i, 0f);
                }
                EndUpdate(countAt, changes);
            }
            return Finish();
        }

        private byte[] SetOne(int deviceId, byte kind, int index, int count, float value)
        {
            if (index < 0 || index >= count)
            {
                return null;
            }
            Begin();
            Pad pad = Announce(deviceId);
            pad.FromPanel = false;
            int countAt = BeginUpdate(deviceId);
            int changes = Change(kind == ButtonChange ? pad.Buttons : pad.Axes, kind, index, value);
            EndUpdate(countAt, changes);
            return Finish();
        }

        private void Begin()
        {
            m_batch.Clear();
            U32(WireVersion);
        }

        // Core drops updates from a pad it hasn't been told about, so the
        // first sight of one announces it at rest.
        private Pad Announce(int deviceId)
        {
            if (m_pads.TryGetValue(deviceId, out Pad pad))
            {
                return pad;
            }
            pad = new Pad();
            m_pads.Add(deviceId, pad);
            m_batch.Add(Connected);
            U32((uint)deviceId);
            m_batch.Add(0); // standard mapping
            m_batch.Add(ButtonCount);
            m_batch.Add(AxisCount);
            m_batch.Add(0); // padding
            for (int i = 0; i < ButtonCount + AxisCount; i++)
            {
                F32(0f);
            }
            return pad;
        }

        // Returns where the change count goes, filled in by EndUpdate.
        private int BeginUpdate(int deviceId)
        {
            m_batch.Add(Update);
            U32((uint)deviceId);
            m_batch.Add(0);
            return m_batch.Count - 1;
        }

        private void EndUpdate(int countAt, int changes)
        {
            if (changes == 0)
            {
                // Drops the record header again: type, device id and count.
                m_batch.RemoveRange(countAt - 5, 6);
                return;
            }
            m_batch[countAt] = (byte)changes;
        }

        private int Change(float[] values, byte kind, int index, float value)
        {
            if (values[index] == value)
            {
                return 0;
            }
            values[index] = value;
            m_batch.Add(kind);
            m_batch.Add((byte)index);
            F32(value);
            return 1;
        }

        private void WriteDisconnected(int deviceId)
        {
            m_batch.Add(Disconnected);
            U32((uint)deviceId);
        }

        // Only the version means nothing to send.
        private byte[] Finish()
        {
            return m_batch.Count > sizeof(uint) ? m_batch.ToArray() : null;
        }

        private void U32(uint value)
        {
            m_batch.Add((byte)value);
            m_batch.Add((byte)(value >> 8));
            m_batch.Add((byte)(value >> 16));
            m_batch.Add((byte)(value >> 24));
        }

        private void F32(float value)
        {
            U32(new FloatBits { Float = value }.Bits);
        }

        // BitConverter.SingleToInt32Bits isn't in every API level Unity offers.
        [StructLayout(LayoutKind.Explicit)]
        private struct FloatBits
        {
            [FieldOffset(0)] public float Float;
            [FieldOffset(0)] public uint Bits;
        }
    }
}
