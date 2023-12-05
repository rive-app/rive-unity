using UnityEngine;

namespace Rive
{
    /// <summary>
    /// A 32 bit color
    /// </summary>
    public struct Color
    {
        public uint value;

        static public Color fromARGB(uint a, uint r, uint g, uint b)
        {
            return new Color(
                ((a & 0xff) << 24) |
                ((r & 0xff) << 16) |
                ((g & 0xff) << 8) |
                ((b & 0xff) << 0));
        }

        static public Color fromRGBO(uint r, uint g, uint b, float opacity)
        {
            return new Color(
                      ((((uint)Mathf.FloorToInt(opacity * 0xff)) & 0xff) << 24) |
                      ((r & 0xff) << 16) |
                      ((g & 0xff) << 8) |
                      ((b & 0xff) << 0));
        }

        public Color(uint value)
        {
            this.value = value & 0xffffffff;
        }

        uint alpha { get { return (0xff000000 & value) >> 24; } }

        float opacity { get { return alpha / 0xff; } }

        uint red { get { return (0x00ff0000 & value) >> 16; } }

        uint green { get { return (0x0000ff00 & value) >> 8; } }

        uint blue { get { return (0x000000ff & value) >> 0; } }

        Color withAlpha(uint a)
        {
            return Color.fromARGB(a, red, green, blue);
        }

        Color withOpacity(float opacity)
        {
            return withAlpha((uint)Mathf.RoundToInt(255.0f * opacity));
        }


        public override bool Equals(object obj)
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

        public override int GetHashCode()
        {
            return (int)value;
        }
    }

}