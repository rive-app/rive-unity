using UnityEngine;

namespace Rive.Components
{
    /// <summary>
    /// Represents a procedural drawing that can be rendered within a ProceduralRiveWidget. Implement this class to create custom procedural graphics.
    /// </summary>
    public abstract class ProceduralDrawing : MonoBehaviour, IProceduralDrawing
    {

        public abstract void Draw(IRenderer renderer, AABB frame, RenderContext renderContext);

        public virtual bool Advance(float deltaTime)
        {
            return false;
        }


        public virtual bool HitTest(Vector2 point, Rect rect)
        {
            return false;
        }

        public virtual bool HandlePointerDown(Vector2 point, Rect rect)
        {
            return false;
        }

        public virtual bool HandlePointerUp(Vector2 point, Rect rect)
        {
            return false;
        }

        public virtual bool HandlePointerMove(Vector2 point, Rect rect)
        {
            return false;
        }
    }
}
