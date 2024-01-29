using UnityEngine;

namespace Rive
{
    /// <summary>
    /// A 32 bit color
    /// </summary>
    public struct Color
    {
        public uint value;

        /// <summary>
        /// Creates a new color from the given ARGB values.
        /// </summary>
        static public Color FromARGB(uint a, uint r, uint g, uint b)
        {
            return new Color(
                ((a & 0xff) << 24) | ((r & 0xff) << 16) | ((g & 0xff) << 8) | ((b & 0xff) << 0)
            );
        }

        /// <summary>
        /// Creates a new color from the given RGBO values.
        /// </summary>
        static public Color FromRGBO(uint r, uint g, uint b, float opacity)
        {
            return new Color(
                ((((uint)Mathf.FloorToInt(opacity * 0xff)) & 0xff) << 24)
                    | ((r & 0xff) << 16)
                    | ((g & 0xff) << 8)
                    | ((b & 0xff) << 0)
            );
        }

        /// <summary>
        /// Creates a new color from the given 32 bit value.
        /// </summary>
        public Color(uint value)
        {
            this.value = value & 0xffffffff;
        }

        readonly uint Alpha
        {
            get { return (0xff000000 & value) >> 24; }
        }

        float Opacity
        {
            get { return Alpha / 0xff; }
        }

        readonly uint Red
        {
            get { return (0x00ff0000 & value) >> 16; }
        }

        readonly uint Green
        {
            get { return (0x0000ff00 & value) >> 8; }
        }

        readonly uint Blue
        {
            get { return (0x000000ff & value) >> 0; }
        }

        /// <summary>
        /// Returns a new color from this color with the given alpha value.
        /// </summary>
        public Color WithAlpha(uint a)
        {
            return Color.FromARGB(a, Red, Green, Blue);
        }

        /// <summary>
        /// Returns a new color from this color with the given opacity value.
        /// </summary>
        public Color WithOpacity(float opacity)
        {
            return WithAlpha((uint)Mathf.RoundToInt(255.0f * opacity));
        }

        public override readonly bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            var other = (Color)obj;

            return other.value == value;
        }

        public static bool operator ==(Color c1, Color c2)
        {
            return c1.value == c2.value;
        }

        public static bool operator !=(Color c1, Color c2)
        {
            return c1.value != c2.value;
        }

        public override readonly int GetHashCode()
        {
            return (int)value;
        }
    }
}
