namespace Rive
{
    /// <summary>
    /// A 2D Matrix
    /// </summary>
    public struct Mat2D
    {
        public float xx;
        public float xy;
        public float yx;
        public float yy;
        public float tx;
        public float ty;

        public static Mat2D identity
        {
            get { return new Mat2D(1, 0, 0, 1, 0, 0); }
        }

        public Mat2D(float xx, float xy, float yx, float yy, float tx, float ty)
        {
            this.xx = xx;
            this.xy = xy;
            this.yx = yx;
            this.yy = yy;
            this.tx = tx;
            this.ty = ty;
        }
    }

}