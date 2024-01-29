namespace Rive
{
    public static class Extensions
    {
        public static System.Numerics.Vector2 ToVector2(this UnityEngine.Vector2 vec2)
        {
            return new System.Numerics.Vector2(vec2.x, vec2.y);
        }

        public static UnityEngine.Vector2 ToVector2(this System.Numerics.Vector2 vec2)
        {
            return new UnityEngine.Vector2(vec2.X, vec2.Y);
        }
    }
}
