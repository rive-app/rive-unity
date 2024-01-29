namespace Rive
{
    /// <summary>
    /// Alignment is used to align the content of a Rive animation within the
    /// available bounds.
    /// </summary>
    public class Alignment
    {
        public Alignment(float x, float y)
        {
            m_x = x;
            m_y = y;
        }

        private readonly float m_x;
        private readonly float m_y;

        public float X
        {
            get { return m_x; }
        }

        public float Y
        {
            get { return m_y; }
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
    }
}
