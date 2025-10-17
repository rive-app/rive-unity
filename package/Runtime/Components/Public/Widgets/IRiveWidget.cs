using UnityEngine;

namespace Rive.Components
{
    /// <summary>
    /// Interface for widgets that can be rendered by a Rive Panel.
    /// </summary>
    public interface IRiveWidget
    {
        /// <summary>
        /// This holds information about the object that will be rendered by the Rive renderer.
        /// </summary>
        IRenderObject RenderObject { get; }

        /// <summary>
        /// The RectTransform of the widget.
        /// </summary>
        public RectTransform RectTransform { get; }

        /// <summary>
        /// Whether the widget is enabled and its GameObject is active in the hierarchy.
        /// </summary>
        bool Enabled { get; }


        /// <summary>
        /// The hit test behavior of the widget.
        /// </summary>
        HitTestBehavior HitTestBehavior { get; set; }


        /// <summary>
        /// This controls the RiveWidget's update loop.
        /// </summary>
        /// <param name="deltaTime"> The time since the last frame.</param>
        /// <returns>Returns <c>true</c> if the widget needs to be redrawn as a result of this tick; otherwise, <c>false</c>.</returns>
        bool Tick(float deltaTime);



        /// <summary>
        /// Tests if a given local position within the widget's rectangle hits any interactive elements.
        /// </summary>
        /// <param name="normalizedPointInRect">
        /// The normalized point of the pointer position in the widget's rectangle. The coordinates are in the range [0,1] where (0,0) is the bottom-left corner and (1,1) is the top-right corner.
        /// </param>
        /// <returns>
        /// Returns <c>true</c> if the position hits an interactive element; otherwise, <c>false</c>.
        /// </returns>
        bool HitTest(Vector2 normalizedPointInRect);

        /// <summary>
        /// Called when a pointer is pressed on the widget.
        /// </summary>
        /// <param name="normalizedPointInRect">The normalized point of the pointer press in the widget's rectangle. The coordinates are in the range [0,1] where (0,0) is the bottom-left corner and (1,1) is the top-right corner.</param>
        /// <param name="pointerId">The unique id for the active pointer/touch.</param>
        /// <returns>Returns <c>true</c> if the pointer press hits an interactive element; otherwise, <c>false</c>.</returns>
        bool OnPointerDown(Vector2 normalizedPointInRect, int pointerId);

        /// <summary>
        /// Called when a pointer is moved on the widget.
        /// </summary>
        /// <param name="normalizedPointInRect">The normalized point of the pointer position in the widget's rectangle. The coordinates are in the range [0,1] where (0,0) is the bottom-left corner and (1,1) is the top-right corner.</param>
        /// <param name="pointerId">The unique id for the active pointer/touch.</param>
        /// <returns>Returns <c>true</c> if the pointer move hits an interactive element; otherwise, <c>false</c>.</returns>
        bool OnPointerMove(Vector2 normalizedPointInRect, int pointerId);

        /// <summary>
        /// Called when a pointer is released on the widget.
        /// </summary>
        /// <param name="normalizedPointInRect">The normalized point of the pointer release in the widget's rectangle. The coordinates are in the range [0,1] where (0,0) is the bottom-left corner and (1,1) is the top-right corner.</param>
        /// <param name="pointerId">The unique id for the active pointer/touch.</param>
        /// <returns>Returns <c>true</c> if the pointer release hits an interactive element; otherwise, <c>false</c>.</returns>
        bool OnPointerUp(Vector2 normalizedPointInRect, int pointerId);

        /// <summary>
        /// Called when a pointer exits the widget.
        /// </summary>
        /// <param name="normalizedPointInRect"> The normalized point of the pointer exit in the widget's rectangle. The coordinates are in the range [0,1] where (0,0) is the bottom-left corner and (1,1) is the top-right corner.</param>
        /// <param name="pointerId">The unique id for the active pointer/touch.</param>
        /// <returns> Returns <c>true</c> if the pointer exit hits an interactive element; otherwise, <c>false</c>.</returns>
        bool OnPointerExit(Vector2 normalizedPointInRect, int pointerId);

        /// <summary>
        /// Called when a pointer enters the widget.
        /// </summary>
        /// <param name="normalizedPointInRect"> The normalized point of the pointer enter in the widget's rectangle. The coordinates are in the range [0,1] where (0,0) is the bottom-left corner and (1,1) is the top-right corner.</param>
        /// <param name="pointerId">The unique id for the active pointer/touch.</param>
        /// <returns> Returns <c>true</c> if the pointer enter hits an interactive element; otherwise, <c>false</c>.</returns>
        bool OnPointerEnter(Vector2 normalizedPointInRect, int pointerId);

    }
}