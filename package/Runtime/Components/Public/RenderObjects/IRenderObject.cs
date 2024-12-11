namespace Rive.Components
{
    public struct RenderContext
    {
        public enum ClippingModeSetting
        {
            /// <summary>
            /// Check if clipping is needed and apply if necessary.
            /// </summary>
            CheckClipping = 0,

            /// <summary>
            /// Skip clipping checks since render target bounds will handle it.
            /// </summary>
            SkipClipping = 1,
        }



        public ClippingModeSetting ClippingMode { get; private set; }


        public RenderContext(ClippingModeSetting clippingMode)
        {
            ClippingMode = clippingMode;
        }
    }

    /// <summary>
    /// The IRenderObject interface. This interface should be implemented by classes that want to be rendered within a RiveView.
    /// </summary>
    public interface IRenderObject
    {
        /// <summary>
        /// The transform data for the render object. Use this to provide the position, size, rotation, scale, and pivot of the render object.
        /// </summary>
        RenderTransform RenderTransform { get; set; }

        /// <summary>
        /// This renders the content of the render object to the given frame.
        /// </summary>
        /// <param name="renderer"> The Rive renderer to use to draw the content. </param>
        /// <param name="frame"> The frame to draw the content within. </param>
        void DrawContent(IRenderer renderer, AABB frame, RenderContext renderContext);

    }
}