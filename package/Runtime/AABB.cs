using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
public struct AABB
{
    public float minX;
    public float minY;
    public float maxX;
    public float maxY;

    public AABB(float minX, float minY, float maxX, float maxY)
    {
        this.minX = minX;
        this.minY = minY;
        this.maxX = maxX;
        this.maxY = maxY;
    }

    public override string ToString()
    {
        return $"minX: {minX}, minY: {minY}, maxX: {maxX}, maxY: {maxY}";
    }
}
