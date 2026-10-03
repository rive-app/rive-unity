using System;
using UnityEngine;

using Rive.Host;

namespace Rive
{
    /// <summary>
    /// A view model instance property that holds a color.
    /// </summary>
    public sealed class ViewModelInstanceColorProperty : ViewModelInstancePrimitiveProperty<UnityEngine.Color>
    {
        internal ViewModelInstanceColorProperty(ViewModelInstance rootInstance, string name, int slot) : base(rootInstance, name, slot)
        {
        }

        internal static int ColorToArgb(UnityEngine.Color color)
        {
            return (Mathf.RoundToInt(color.a * 255) << 24) |
                   (Mathf.RoundToInt(color.r * 255) << 16) |
                   (Mathf.RoundToInt(color.g * 255) << 8) |
                   Mathf.RoundToInt(color.b * 255);
        }

        private static int Color32ToArgb(Color32 color)
        {
            return (color.a << 24) |
                   (color.r << 16) |
                   (color.g << 8) |
                   color.b;
        }

        internal static UnityEngine.Color ArgbToColor(int argb)
        {
            return new UnityEngine.Color(
                ((argb >> 16) & 0xFF) / 255f, // R (from ARGB)
                ((argb >> 8) & 0xFF) / 255f,  // G (from ARGB)
                (argb & 0xFF) / 255f,         // B (from ARGB)
                ((argb >> 24) & 0xFF) / 255f  // A (from ARGB)
            );
        }

        private static Color32 ArgbToColor32(int argb)
        {
            return new Color32(
                (byte)((argb >> 16) & 0xFF), // R (from ARGB)
                (byte)((argb >> 8) & 0xFF),  // G (from ARGB)
                (byte)(argb & 0xFF),         // B (from ARGB)
                (byte)((argb >> 24) & 0xFF)  // A (from ARGB)
            );
        }

        /// <summary>
        /// Gets or sets the color value as a Unity Color.
        /// </summary>
        public override UnityEngine.Color Value
        {
            get
            {
                ThrowIfOwnerDisposed();
                return ArgbToColor(ReadArgb());
            }
            set
            {
                ThrowIfOwnerDisposed();
                WriteNative(0f, ColorToArgb(value), null);
            }
        }

        /// <summary>
        /// Gets or sets the color value as a Color32
        /// </summary>
        public Color32 Value32
        {
            get
            {
                ThrowIfOwnerDisposed();
                return ArgbToColor32(ReadArgb());
            }
            set
            {
                ThrowIfOwnerDisposed();
                WriteNative(0f, Color32ToArgb(value), null);
            }
        }

        private int ReadArgb()
        {
            return ReadNative((ref PayloadReader reader) => (int)reader.U32(), 0);
        }

        internal override UnityEngine.Color FromValue(in PropertyValue value) => ArgbToColor((int)value.Bits);
    }
}
