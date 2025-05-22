using UnityEngine;

namespace Rive.Components
{
    /// <summary>
    /// Interface for a procedural drawing that can be used to draw a procedural graphic within a ProceduralRiveWidget.
    /// </summary>
    public interface IProceduralDrawing
    {
        /// <summary>
        /// This should pass the procedural graphic using the given renderer. This might be called multiple times during the lifetime of the object, such as when the render texture is redrawn.
        /// </summary>
        /// <param name="renderer"> The Rive renderer to use to draw the procedural graphic. </param>
        /// <param name="frame"> The frame to draw the procedural visual within. </param>
        /// <param name="renderContext"> Provides additional context to consider when drawing the procedural graphic. </param>
        void Draw(IRenderer renderer, AABB frame, RenderContext renderContext);

        /// <summary>
        /// This should update the procedural graphic. Use this to update the procedural drawing changes over time.
        /// </summary>
        /// <param name="deltaTime"> The time since the last update. </param>
        /// <returns> True if the procedural graphic has changed, false otherwise. </returns>
        bool Advance(float deltaTime);

        /// <summary>
        /// Tests if a given local position within the widget's rectangle hits any interactive elements.
        /// </summary>
        /// <param name="point">
        /// The normalized point of the pointer position in the rectangle. The coordinates are in the range [0,1] where (0,0) is the bottom-left corner and (1,1) is the top-right corner.
        /// </param>
        /// <param name="rect">
        /// The rectangle to test the hit against.
        /// </param>
        /// <returns>
        /// Returns <c>true</c> if the position hits an interactive element; otherwise, <c>false</c>.
        /// </returns>
        bool HitTest(Vector2 point, Rect rect);

        /// <summary>
        /// Responds to a pointer press event within the procedural graphic in the given rect.
        /// </summary>
        /// <param name="point">
        /// The normalized point of the pointer position in the rectangle. The coordinates are in the range [0,1] where (0,0) is the bottom-left corner and (1,1) is the top-right corner.
        /// </param>
        /// <param name="rect">
        /// The rectangle to reference for the pointer down event.
        /// </param>
        /// <returns> Returns <c>true</c> if the pointer down event was handled; otherwise, <c>false</c>. </returns>
        bool HandlePointerDown(Vector2 point, Rect rect);

        /// <summary>
        /// Responds to a pointer up event within the procedural graphic in the given rect.
        /// </summary>
        /// <param name="point">
        /// The normalized point of the pointer position in the rectangle. The coordinates are in the range [0,1] where (0,0) is the bottom-left corner and (1,1) is the top-right corner.
        /// </param>
        /// <param name="rect">
        /// The rectangle to reference for the pointer up event.
        /// </param>
        /// <returns> Returns <c>true</c> if the pointer up event was handled; otherwise, <c>false</c>. </returns>
        bool HandlePointerUp(Vector2 point, Rect rect);

        /// <summary>
        /// Responds to a pointer move event within the procedural graphic in the given rect.
        /// </summary>
        /// <param name="point">
        /// The normalized point of the pointer position in the rectangle. The coordinates are in the range [0,1] where (0,0) is the bottom-left corner and (1,1) is the top-right corner.
        /// </param>
        /// <param name="rect">
        /// The rectangle to reference for the pointer move event.
        /// </param>
        /// <returns> Returns <c>true</c> if the pointer move event was handled; otherwise, <c>false</c>. </returns>
        bool HandlePointerMove(Vector2 point, Rect rect);



        /// <summary>
        /// Responds to a pointer exit event within the procedural graphic in the given rect.
        /// </summary>
        /// <param name="point">
        /// The normalized point of the pointer position in the rectangle. The coordinates are in the range [0,1] where (0,0) is the bottom-left corner and (1,1) is the top-right corner.
        /// </param>
        /// <param name="rect">
        /// The rectangle to reference for the pointer exit event.
        /// </param>
        /// <returns> Returns <c>true</c> if the pointer exit event was handled; otherwise, <c>false</c>. </returns>
        bool HandlePointerExit(Vector2 point, Rect rect);
    }
}
