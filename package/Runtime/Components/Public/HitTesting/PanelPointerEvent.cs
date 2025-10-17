using UnityEngine;

namespace Rive.Components
{
    /// <summary>
    /// Pointer data delivered by input providers and propagated through the panel/widget pipeline.
    /// </summary>
    public readonly struct PanelPointerEvent
    {
        public readonly Vector2 Position;
        public readonly int PointerId;
        /// <summary>
        /// Creates a new pointer event for panel input propagation.
        /// </summary>
        /// <param name="position">
        /// The normalized point in panel space [0,1] where (0,0) is bottom-left and (1,1) is top-right.
        /// </param>
        /// <param name="pointerId">The unique id for the active pointer/touch.</param>
        public PanelPointerEvent(Vector2 position, int pointerId)
        {
            Position = position;
            PointerId = pointerId;
        }
    }
}


