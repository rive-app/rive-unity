namespace Rive.Components
{
    /// <summary>
    /// This represents a procedural render object that can be rendered within a RiveWidget.
    /// </summary>
    public class ProceduralRenderObject : RenderObject
    {
        private ProceduralDrawing m_drawing;

        /// <summary>
        /// The procedural drawing to render.
        /// </summary>
        public ProceduralDrawing Drawing { get => m_drawing; }


        public override void DrawContent(IRenderer renderer, AABB frame, RenderContext renderContext)
        {
            if (Drawing == null)
            {
                return;
            }
            Drawing.Draw(renderer, frame, renderContext);
        }

        /// <summary>
        /// Creates a new procedural render object with the given drawing.
        /// </summary>
        /// <param name="drawing"> The procedural drawing to render. </param>
        public ProceduralRenderObject(ProceduralDrawing drawing)
        {
            m_drawing = drawing;
        }


    }
}
