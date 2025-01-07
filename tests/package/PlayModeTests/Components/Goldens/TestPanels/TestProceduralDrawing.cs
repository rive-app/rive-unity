using Rive.Components;
using UnityEngine;
namespace Rive.Tests
{
    /// <summary>
    /// A procedural drawing that draws a blue rectangle with a red triangle centered in it.
    /// </summary>
    public class TestProceduralDrawing : ProceduralDrawing
    {
        private Path m_rectanglePath;
        private Path m_trianglePath;
        private Paint m_rectanglePaint;
        private Paint m_trianglePaint;
        private Path m_clipPath;

        // The triangle will take up 60% of the available vertical space
        // So, if we have 100 pixels of height, the triangle will be 60 pixels tall
        private const float TRIANGLE_SIZE_PERCENT = 0.6f;  // 60%

        // For an equilateral triangle (all sides equal):
        // - If height = 1, then width ≈ 0.866
        // - This comes from the mathematical ratio width = height * (√3/2)
        private const float EQUAL_SIDED_TRIANGLE_RATIO = 0.866f;

        // How much space to leave at the top and bottom
        private const float TOP_AND_BOTTOM_PADDING_COUNT = 3f;

        public override void Draw(IRenderer renderer, AABB frame, RenderContext renderContext)
        {
            float width = frame.maxX - frame.minX;
            float height = frame.maxY - frame.minY;
            float padding = Mathf.Min(width, height) * 0.1f; // 10% padding so we don't draw on the edges

            if (renderContext.ClippingMode == RenderContext.ClippingModeSetting.CheckClipping)
            {
                SetupClipping(renderer, frame);
            }

            if (m_rectanglePath == null) InitializePaths(frame.minX, frame.minY, width, height, padding);

            renderer.Draw(m_rectanglePath, m_rectanglePaint);
            renderer.Draw(m_trianglePath, m_trianglePaint);
        }

        private void InitializePaths(float minX, float minY, float width, float height, float padding)
        {
            m_rectanglePath = new Path();
            m_rectanglePaint = new Paint
            {
                Color = new Rive.Color(0xFF0000FF), // Blue
                Style = PaintingStyle.Fill,
                Join = StrokeJoin.Round,
                Thickness = 2.0f
            };

            m_rectanglePath.MoveTo(minX + padding, minY + padding);
            m_rectanglePath.LineTo(minX + width - padding, minY + padding);
            m_rectanglePath.LineTo(minX + width - padding, minY + height - padding);
            m_rectanglePath.LineTo(minX + padding, minY + height - padding);
            m_rectanglePath.Close();

            m_trianglePath = new Path();
            m_trianglePaint = new Paint
            {
                Color = new Rive.Color(0xFFFF0000), // Red
                Style = PaintingStyle.Fill,
                Join = StrokeJoin.Round,
                Thickness = 2.0f
            };


            // We find the center of our drawing area , so we can center the triangle
            float centerX = minX + width / 2;
            float centerY = minY + height / 2;

            float heightAfterPadding = height - (padding * TOP_AND_BOTTOM_PADDING_COUNT);

            // Make our triangle a percentage of that available height so it's not too big
            float triangleHeight = heightAfterPadding * TRIANGLE_SIZE_PERCENT;

            // Calculate the width needed to make an equal-sided triangle
            float triangleWidth = triangleHeight * EQUAL_SIDED_TRIANGLE_RATIO;


            // Draw triangle centered in the middle of the drawing area
            m_trianglePath.MoveTo(centerX, centerY - triangleHeight / 2);
            m_trianglePath.LineTo(centerX + triangleWidth / 2, centerY + triangleHeight / 2);
            m_trianglePath.LineTo(centerX - triangleWidth / 2, centerY + triangleHeight / 2);
            m_trianglePath.Close();
        }

        private void SetupClipping(IRenderer renderer, AABB frame)
        {
            if (m_clipPath == null)
            {
                m_clipPath = new Path();
            }
            else
            {
                m_clipPath.Reset();
            }

            m_clipPath.MoveTo(frame.minX, frame.minY);
            m_clipPath.LineTo(frame.maxX, frame.minY);
            m_clipPath.LineTo(frame.maxX, frame.maxY);
            m_clipPath.LineTo(frame.minX, frame.maxY);
            m_clipPath.Close();
            renderer.Clip(m_clipPath);
        }

        // This is a static drawing, so we return false, which means we don't need to redraw
        // return true if something has changed and you want to redraw
        public override bool Advance(float deltaTime)
        {
            return false;
        }
    }
}