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
            m_x = x; m_y = y;
        }

        private float m_x;
        private float m_y;

        public float x
        {
            get
            {
                return m_x;
            }
        }

        public float y
        {
            get
            {
                return m_y;
            }
        }

        public static readonly Alignment topLeft = new Alignment(-1.0f, -1.0f);
        public static readonly Alignment topCenter = new Alignment(0.0f, -1.0f);
        public static readonly Alignment topRight = new Alignment(1.0f, -1.0f);
        public static readonly Alignment centerLeft = new Alignment(-1.0f, 0.0f);
        public static readonly Alignment center = new Alignment(0.0f, 0.0f);
        public static readonly Alignment centerRight = new Alignment(1.0f, 0.0f);
        public static readonly Alignment bottomLeft = new Alignment(-1.0f, 1.0f);
        public static readonly Alignment bottomCenter = new Alignment(0.0f, 1.0f);
        public static readonly Alignment bottomRight = new Alignment(1.0f, 1.0f);
    }
}