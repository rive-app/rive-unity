using UnityEngine;

namespace Rive.Components
{
    /// <summary>
    /// Maps artboard focus bounds into panel space using the widget's fit/alignment.
    /// </summary>
    internal static class FocusBoundsTransformer
    {
        public static bool TryTransformToPanel(
            AABB artboardBounds,
            Vector2 artboardSize,
            Fit fit,
            Alignment alignment,
            float layoutScaleFactor,
            RectTransform widget,
            RectTransform panel,
            out Rect panelRect)
        {
            panelRect = Rect.zero;
            if (widget == null || panel == null)
            {
                return false;
            }

            Rect widgetRect = widget.rect;
            // The whole widget, as drawing uses. Layout's scale is applied to the artboard, not the frame.
            AABB frame = new AABB(0f, 0f, widgetRect.width, widgetRect.height);

            AABB content = new AABB(0f, 0f, artboardSize.x, artboardSize.y);

            Vector2 min = TransformArtboardPoint(new Vector2(artboardBounds.minX, artboardBounds.minY), content, frame, fit, alignment, layoutScaleFactor);
            Vector2 max = min;
            Expand(ref min, ref max, TransformArtboardPoint(new Vector2(artboardBounds.maxX, artboardBounds.minY), content, frame, fit, alignment, layoutScaleFactor));
            Expand(ref min, ref max, TransformArtboardPoint(new Vector2(artboardBounds.minX, artboardBounds.maxY), content, frame, fit, alignment, layoutScaleFactor));
            Expand(ref min, ref max, TransformArtboardPoint(new Vector2(artboardBounds.maxX, artboardBounds.maxY), content, frame, fit, alignment, layoutScaleFactor));

            if (TextureHelper.ShouldFlipInput())
            {
                float frameHeight = frame.maxY - frame.minY;
                float minY = frameHeight - max.y;
                float maxY = frameHeight - min.y;
                min.y = minY;
                max.y = maxY;
            }

            // Every corner, since a rotated widget's diagonal doesn't bound it.
            Vector2 first = ToPanel(widget, panel, widgetRect, min.x, min.y);
            Vector2 boundsMin = first;
            Vector2 boundsMax = first;
            Expand(ref boundsMin, ref boundsMax, ToPanel(widget, panel, widgetRect, max.x, min.y));
            Expand(ref boundsMin, ref boundsMax, ToPanel(widget, panel, widgetRect, min.x, max.y));
            Expand(ref boundsMin, ref boundsMax, ToPanel(widget, panel, widgetRect, max.x, max.y));
            panelRect = Rect.MinMaxRect(boundsMin.x, boundsMin.y, boundsMax.x, boundsMax.y);
            return panelRect.width >= 0f && panelRect.height >= 0f;
        }

        private static Vector2 ToPanel(RectTransform widget, RectTransform panel, Rect widgetRect, float x, float y)
        {
            Vector3 world = widget.TransformPoint(new Vector3(widgetRect.xMin + x, widgetRect.yMin + y, 0f));
            return panel.InverseTransformPoint(world);
        }

        private static void Expand(ref Vector2 min, ref Vector2 max, Vector2 point)
        {
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }

        private static Vector2 TransformArtboardPoint(
            Vector2 artboardPoint,
            AABB content,
            AABB frame,
            Fit fit,
            Alignment alignment,
            float scaleFactor)
        {
            float contentWidth = content.maxX - content.minX;
            float contentHeight = content.maxY - content.minY;
            if (contentWidth <= 0f || contentHeight <= 0f)
            {
                return Vector2.zero;
            }

            float x = -content.minX - contentWidth * 0.5f - (alignment.X * contentWidth * 0.5f);
            float y = -content.minY - contentHeight * 0.5f - (alignment.Y * contentHeight * 0.5f);

            GetScale(fit, frame, contentWidth, contentHeight, scaleFactor, out float scaleX, out float scaleY);

            float tx = frame.minX + (frame.maxX - frame.minX) * 0.5f + (alignment.X * (frame.maxX - frame.minX) * 0.5f);
            float ty = frame.minY + (frame.maxY - frame.minY) * 0.5f + (alignment.Y * (frame.maxY - frame.minY) * 0.5f);

            float px = (artboardPoint.x + x) * scaleX + tx;
            float py = (artboardPoint.y + y) * scaleY + ty;
            return new Vector2(px, py);
        }

        private static void GetScale(
            Fit fit,
            AABB frame,
            float contentWidth,
            float contentHeight,
            float scaleFactor,
            out float scaleX,
            out float scaleY)
        {
            float frameWidth = frame.maxX - frame.minX;
            float frameHeight = frame.maxY - frame.minY;

            switch (fit)
            {
                case Fit.Fill:
                    scaleX = frameWidth / contentWidth;
                    scaleY = frameHeight / contentHeight;
                    break;
                case Fit.Cover:
                    scaleX = scaleY = Mathf.Max(frameWidth / contentWidth, frameHeight / contentHeight);
                    break;
                case Fit.FitHeight:
                    scaleX = scaleY = frameHeight / contentHeight;
                    break;
                case Fit.FitWidth:
                    scaleX = scaleY = frameWidth / contentWidth;
                    break;
                case Fit.Layout:
                    scaleX = scaleY = scaleFactor;
                    break;
                case Fit.None:
                    scaleX = scaleY = 1f;
                    break;
                case Fit.ScaleDown:
                    {
                        float minScale = Mathf.Min(frameWidth / contentWidth, frameHeight / contentHeight);
                        scaleX = scaleY = minScale < 1f ? minScale : 1f;
                        break;
                    }
                case Fit.Contain:
                default:
                    scaleX = scaleY = Mathf.Min(frameWidth / contentWidth, frameHeight / contentHeight);
                    break;
            }
        }
    }
}
