using UnityEngine;

namespace Rive.Components
{

    /// <summary>
    /// Represents a render object that has an artboard.
    /// </summary>
    public class ArtboardRenderObject : RenderObject
    {


        private Artboard m_artboard;

        private Alignment m_alignment;
        private Path m_clipPath;

        /// <summary>
        /// The fit of the artboard.
        /// </summary>
        private Fit m_fit;

        /// <summary>
        /// The scale factor to use when drawing the artboard when using the Layout fit mode.
        /// </summary>
        private float m_scaleFactor = 1.0f;


        /// <summary>
        /// The Artboard to render.
        /// </summary>
        public Artboard Artboard => m_artboard;

        /// <summary>
        /// The alignment of the artboard within the frame.
        /// </summary>
        public Alignment Alignment { get => m_alignment; set => m_alignment = value; }

        /// <summary>
        ///  The fit of the artboard within the frame.
        /// </summary>
        public Fit Fit { get => m_fit; set => m_fit = value; }

        /// <summary>
        /// The actual scale factor to use when drawing the artboard when using the Layout fit mode, after accounting for any additional calculations based on the scale mode.
        /// </summary>
        public float EffectiveLayoutScaleFactor { get => m_scaleFactor; set => m_scaleFactor = value; }



        public override void DrawContent(IRenderer renderer, AABB frame, RenderContext renderContext)
        {
            if (renderContext.ClippingMode == RenderContext.ClippingModeSetting.CheckClipping)
            {
                ClipIfNeeded(renderer, frame);
            }

            float scaleFactor = 1.0f;

            if (Fit == Fit.Layout)
            {
                scaleFactor = EffectiveLayoutScaleFactor;
            }

            renderer.Align(Fit, Alignment, Artboard, frame, scaleFactor);

            renderer.Draw(m_artboard);



        }

        private void ClipIfNeeded(IRenderer renderer, AABB frame)
        {
            Rect rect = new Rect(frame.minY, frame.minY, frame.maxX, frame.maxY);

            // Determine if clipping is necessary
            // We do this for performance (as clipping can be expensive), so we only clip if the render object overflows the frame
            Vector2 artboardSize = new Vector2(m_artboard.Width, m_artboard.Height);
            bool needsClipping = NeedsClipping(this.Fit, artboardSize, new Vector2(rect.width, rect.height));

            if (needsClipping)
            {
                if (m_clipPath == null)
                {
                    m_clipPath = new Path();
                }
                else
                {
                    m_clipPath.Reset();
                }
                m_clipPath.MoveTo(0, 0);
                m_clipPath.LineTo(rect.width, 0);
                m_clipPath.LineTo(rect.width, rect.height);
                m_clipPath.LineTo(0, rect.height);
                m_clipPath.Close();
                renderer.Clip(m_clipPath);
            }
        }

        internal void Init(Artboard artboard, Alignment alignment, Fit fit, float scaleFactor)
        {
            m_artboard = artboard;
            m_alignment = alignment;
            m_fit = fit;
            m_scaleFactor = scaleFactor;
        }
        public ArtboardRenderObject(Artboard artboard, Alignment alignment, Fit fit, float scaleFactor)
        {
            Init(artboard, alignment, fit, scaleFactor);
        }




        internal static bool NeedsClipping(Fit fit, Vector2 artboardSize, Vector2 frameSize)
        {
            float widthRatio = frameSize.x / artboardSize.x;
            float heightRatio = frameSize.y / artboardSize.y;

            switch (fit)
            {
                case Fit.Fill:
                    return false; // Fill always fits exactly, no clipping needed
                case Fit.Contain:
                    return false; // Contain always fits within the frame, no clipping needed
                case Fit.Cover:
                    return widthRatio != heightRatio; // Only clip if aspect ratios don't match
                case Fit.FitHeight:
                    return widthRatio < heightRatio; // Clip if width overflows after fitting height
                case Fit.FitWidth:
                    return heightRatio < widthRatio; // Clip if height overflows after fitting width
                case Fit.None:
                    return artboardSize.x > frameSize.x || artboardSize.y > frameSize.y;
                case Fit.ScaleDown:
                    return artboardSize.x > frameSize.x || artboardSize.y > frameSize.y;
                case Fit.Layout:
                    return false; // Layout resizes the artboard to fit the frame.
                default:
                    return false; // Default to not clipping if we don't know the fit
            }
        }
    }
}
