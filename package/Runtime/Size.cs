using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Rive
{
    /// <summary>
    /// A width and height.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)] // Sequential floats, so native can return one by value.
    public readonly struct Size : IEquatable<Size>
    {
        private readonly float m_width;
        private readonly float m_height;

        public Size(float width, float height)
        {
            m_width = width;
            m_height = height;
        }

        public float Width => m_width;

        public float Height => m_height;

        public static implicit operator Vector2(Size size)
        {
            return new Vector2(size.Width, size.Height);
        }

        public bool Equals(Size other)
        {
            return Width.Equals(other.Width) && Height.Equals(other.Height);
        }

        public override bool Equals(object obj)
        {
            return obj is Size other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (Width.GetHashCode() * 397) ^ Height.GetHashCode();
        }

        public static bool operator ==(Size a, Size b) => a.Equals(b);

        public static bool operator !=(Size a, Size b) => !a.Equals(b);

        public override string ToString()
        {
            return $"{Width} x {Height}";
        }
    }
}
