using System;

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



            if (RenderTargetStrategy.ProceduralDrawingRequiresRotationCorrection())
            {
                renderer.Save();
                // When the drawing is rotated by 90 degrees, it appears offset by the width of the frame, so we translate it back by that amount
                // The order of operations is important here, so we first translate by the width of the frame, then rotate by 90 degrees, otherwise the translation will be incorrect
                renderer.Transform(System.Numerics.Matrix3x2.CreateTranslation(frame.maxX, 0));

                //Rotate the drawing by 90 degrees
                renderer.Transform(System.Numerics.Matrix3x2.CreateRotation((float)Math.PI / 2));

                Drawing.Draw(renderer, frame, renderContext);
                renderer.Restore();
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
