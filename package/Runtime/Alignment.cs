using System;

namespace Rive
{
    /// <summary>
    /// Alignment is used to align the content of a Rive visual within the available bounds.
    /// </summary>
    [System.Serializable]
    public struct Alignment : IEquatable<Alignment>
    {
        public Alignment(float x, float y)
        {
            m_x = x;
            m_y = y;
        }

        [UnityEngine.SerializeField]
        private float m_x;

        [UnityEngine.SerializeField]
        private float m_y;

        public float X
        {
            get { return m_x; }
        }

        public float Y
        {
            get { return m_y; }
        }

        public override string ToString()
        {
            return $"X: {m_x}, Y: {m_y}";
        }

        public static readonly Alignment TopLeft = new(-1.0f, -1.0f);
        public static readonly Alignment TopCenter = new(0.0f, -1.0f);
        public static readonly Alignment TopRight = new(1.0f, -1.0f);
        public static readonly Alignment CenterLeft = new(-1.0f, 0.0f);
        public static readonly Alignment Center = new(0.0f, 0.0f);
        public static readonly Alignment CenterRight = new(1.0f, 0.0f);
        public static readonly Alignment BottomLeft = new(-1.0f, 1.0f);
        public static readonly Alignment BottomCenter = new(0.0f, 1.0f);
        public static readonly Alignment BottomRight = new(1.0f, 1.0f);


        public bool Equals(Alignment other)
        {
            return m_x.Equals(other.m_x) && m_y.Equals(other.m_y);
        }

        public override bool Equals(object obj)
        {
            return obj is Alignment other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(m_x, m_y);
        }

        public static bool operator ==(Alignment left, Alignment right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Alignment left, Alignment right)
        {
            return !(left == right);
        }


#if UNITY_EDITOR
        internal static string BindingPath_Xfield => nameof(m_x);
        internal static string BindingPath_Yfield => nameof(m_y);
#endif
    }


}
