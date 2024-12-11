namespace Rive.Components
{
    /// <summary>
    /// This represents an object that can be rendered within a RivePanel.
    /// </summary>
    public abstract class RenderObject : IRenderObject
    {
        /// <summary>
        /// The transform data for the render object. Use this to get or update the position, size, rotation, scale, and pivot of the render object.
        /// </summary>
        public RenderTransform RenderTransform { get; set; }

        /// <summary>
        /// This should draw the content of the render object to the given frame using the given renderer.
        /// </summary>
        /// <param name="renderer"> The renderer to use to draw the content. </param>
        /// <param name="frame"> The frame to draw the content within. </param>
        public abstract void DrawContent(IRenderer renderer, AABB frame, RenderContext renderContext);


    }
}