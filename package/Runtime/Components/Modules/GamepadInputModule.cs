using System.Collections.Generic;
#if RIVE_USING_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
using UnityEngine;
using UnityEngine.InputSystem;
#endif

namespace Rive.Components
{
    /// <summary>
    /// The connected gamepads' values for one frame, in the W3C standard
    /// layout. Pads and their arrays are reused from frame to frame.
    /// </summary>
    internal sealed class GamepadFrame
    {
        internal sealed class Pad
        {
            internal int DeviceId;
            internal readonly float[] Buttons = new float[GamepadTracker.ButtonCount];
            internal readonly float[] Axes = new float[GamepadTracker.AxisCount];
        }

        private readonly List<Pad> m_pads = new List<Pad>();
        private int m_count;

        internal readonly HashSet<int> DeviceIds = new HashSet<int>();

        internal int Count => m_count;

        internal Pad this[int index] => m_pads[index];

        internal void Clear()
        {
            m_count = 0;
            DeviceIds.Clear();
        }

        /// A pad to fill in, its values left from whichever pad used it last.
        internal Pad Add(int deviceId)
        {
            if (m_count == m_pads.Count)
            {
                m_pads.Add(new Pad());
            }
            Pad pad = m_pads[m_count++];
            pad.DeviceId = deviceId;
            DeviceIds.Add(deviceId);
            return pad;
        }
    }

    /// <summary>
    /// Reads the Input System's gamepads once a frame, before panels tick,
    /// and has each panel send its widgets what changed. Without the Input
    /// System there are never any pads.
    /// </summary>
    internal sealed class GamepadInputModule : FrameModule
    {
        private readonly ICollection<RivePanel> m_panels;
        private readonly GamepadFrame m_frame = new GamepadFrame();

        internal GamepadInputModule(ICollection<RivePanel> panels)
        {
            m_panels = panels;
        }

        /// Tests only. Fills the frame instead of the Input System.
        internal static System.Action<GamepadFrame> ReadForTests;

        internal override void BeforeTick()
        {
            if (ReadForTests != null)
            {
                m_frame.Clear();
                ReadForTests(m_frame);
            }
            else
            {
                Read(m_frame);
            }
            foreach (RivePanel panel in m_panels)
            {
                if (panel != null && panel.isActiveAndEnabled)
                {
                    panel.SyncGamepads(m_frame);
                }
            }
        }

        internal static void Read(GamepadFrame frame)
        {
            frame.Clear();
#if RIVE_USING_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            foreach (Gamepad gamepad in Gamepad.all)
            {
                if (gamepad.added)
                {
                    Fill(frame.Add(gamepad.deviceId), gamepad);
                }
            }
#endif
        }

#if RIVE_USING_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
        private static void Fill(GamepadFrame.Pad pad, Gamepad gamepad)
        {
            float[] buttons = pad.Buttons;
            buttons[(int)RiveGamepadButton.South] = gamepad.buttonSouth.ReadValue();
            buttons[(int)RiveGamepadButton.East] = gamepad.buttonEast.ReadValue();
            buttons[(int)RiveGamepadButton.West] = gamepad.buttonWest.ReadValue();
            buttons[(int)RiveGamepadButton.North] = gamepad.buttonNorth.ReadValue();
            buttons[(int)RiveGamepadButton.LeftShoulder] = gamepad.leftShoulder.ReadValue();
            buttons[(int)RiveGamepadButton.RightShoulder] = gamepad.rightShoulder.ReadValue();
            buttons[(int)RiveGamepadButton.LeftTrigger] = gamepad.leftTrigger.ReadValue();
            buttons[(int)RiveGamepadButton.RightTrigger] = gamepad.rightTrigger.ReadValue();
            buttons[(int)RiveGamepadButton.Back] = gamepad.selectButton.ReadValue();
            buttons[(int)RiveGamepadButton.Forward] = gamepad.startButton.ReadValue();
            buttons[(int)RiveGamepadButton.LeftStick] = gamepad.leftStickButton.ReadValue();
            buttons[(int)RiveGamepadButton.RightStick] = gamepad.rightStickButton.ReadValue();
            buttons[(int)RiveGamepadButton.DpadUp] = gamepad.dpad.up.ReadValue();
            buttons[(int)RiveGamepadButton.DpadDown] = gamepad.dpad.down.ReadValue();
            buttons[(int)RiveGamepadButton.DpadLeft] = gamepad.dpad.left.ReadValue();
            buttons[(int)RiveGamepadButton.DpadRight] = gamepad.dpad.right.ReadValue();
            buttons[(int)RiveGamepadButton.Start] = 0f;

            // W3C sticks read positive down, Unity's positive up.
            float[] axes = pad.Axes;
            Vector2 left = gamepad.leftStick.ReadValue();
            Vector2 right = gamepad.rightStick.ReadValue();
            axes[(int)RiveGamepadAxis.LeftX] = left.x;
            axes[(int)RiveGamepadAxis.LeftY] = -left.y;
            axes[(int)RiveGamepadAxis.RightX] = right.x;
            axes[(int)RiveGamepadAxis.RightY] = -right.y;
            axes[(int)RiveGamepadAxis.LeftTrigger] = buttons[(int)RiveGamepadButton.LeftTrigger];
            axes[(int)RiveGamepadAxis.RightTrigger] = buttons[(int)RiveGamepadButton.RightTrigger];
        }
#endif
    }
}
