using System.Collections.Generic;
using UnityEngine;

namespace Rive.Components
{
    /// <summary>
    /// Picks the next widget in a direction using panel-space rects.
    /// A mix of the Rive core focus manager scoring logic with CSS-style score (primary gap, orthogonal gap, overlap). A candidate counts if
    /// its center is past the origin's leading edge, so stacked widgets still work
    /// when their rects overlap a bit. Unity Y is up. The CSS logic is specifically used to account for slight overlaps the way Unity's Rect class handles them.
    /// </summary>
    internal static class FocusSpatialNavigator
    {
        private const float HorizontalWeight = 30.0f;
        private const float VerticalWeight = 2.0f;

        public static bool IsSequential(FocusNavigationDirection direction)
        {
            return direction == FocusNavigationDirection.Next ||
                   direction == FocusNavigationDirection.Previous;
        }

        public static bool IsForwardSeed(FocusNavigationDirection direction)
        {
            return direction == FocusNavigationDirection.Next ||
                   direction == FocusNavigationDirection.Right ||
                   direction == FocusNavigationDirection.Down;
        }

        public static Rect GetRectInPanelSpace(RectTransform widget, RectTransform panel)
        {
            if (widget == null || panel == null)
            {
                return Rect.zero;
            }

            Vector3[] corners = new Vector3[4];
            widget.GetWorldCorners(corners);

            Vector2 min = panel.InverseTransformPoint(corners[0]);
            Vector2 max = min;
            for (int i = 1; i < 4; i++)
            {
                Vector2 point = panel.InverseTransformPoint(corners[i]);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        public static Rect CreateEntryOrigin(Rect panelRect, FocusNavigationDirection direction)
        {
            // Keep the origin outside the panel so a widget on the edge still counts.
            float xOutset = Mathf.Max(1f, panelRect.width);
            float yOutset = Mathf.Max(1f, panelRect.height);

            switch (direction)
            {
                case FocusNavigationDirection.Right:
                    return new Rect(panelRect.xMin - xOutset, panelRect.yMin, 0f, panelRect.height);
                case FocusNavigationDirection.Left:
                    return new Rect(panelRect.xMax + xOutset, panelRect.yMin, 0f, panelRect.height);
                case FocusNavigationDirection.Down:
                    return new Rect(panelRect.xMin, panelRect.yMax + yOutset, panelRect.width, 0f);
                case FocusNavigationDirection.Up:
                    return new Rect(panelRect.xMin, panelRect.yMin - yOutset, panelRect.width, 0f);
                default:
                    return panelRect;
            }
        }

        /// <summary>
        /// Best candidate index, or -1. Ties keep the earlier index (sorted-widget order).
        /// </summary>
        public static int FindBestIndex(
            Rect origin,
            IReadOnlyList<Rect> candidates,
            FocusNavigationDirection direction)
        {
            int bestIndex = -1;
            float bestScore = float.MaxValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                float score = Score(origin, candidates[i], direction);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        public static float Score(Rect current, Rect candidate, FocusNavigationDirection direction)
        {
            if (!IsCenterPastLeadingEdge(current, candidate, direction))
            {
                return float.MaxValue;
            }

            float displacement;
            float orthogonalDistance;
            float overlap;
            float orthogonalWeight;

            switch (direction)
            {
                case FocusNavigationDirection.Left:
                    displacement = Mathf.Max(0f, current.xMin - candidate.xMax);
                    orthogonalDistance = OrthogonalGap(current.yMin, current.yMax, candidate.yMin, candidate.yMax);
                    overlap = Overlap(current.yMin, current.yMax, candidate.yMin, candidate.yMax);
                    orthogonalWeight = HorizontalWeight;
                    break;
                case FocusNavigationDirection.Right:
                    displacement = Mathf.Max(0f, candidate.xMin - current.xMax);
                    orthogonalDistance = OrthogonalGap(current.yMin, current.yMax, candidate.yMin, candidate.yMax);
                    overlap = Overlap(current.yMin, current.yMax, candidate.yMin, candidate.yMax);
                    orthogonalWeight = HorizontalWeight;
                    break;
                case FocusNavigationDirection.Up:
                    displacement = Mathf.Max(0f, candidate.yMin - current.yMax);
                    orthogonalDistance = OrthogonalGap(current.xMin, current.xMax, candidate.xMin, candidate.xMax);
                    overlap = Overlap(current.xMin, current.xMax, candidate.xMin, candidate.xMax);
                    orthogonalWeight = VerticalWeight;
                    break;
                case FocusNavigationDirection.Down:
                    displacement = Mathf.Max(0f, current.yMin - candidate.yMax);
                    orthogonalDistance = OrthogonalGap(current.xMin, current.xMax, candidate.xMin, candidate.xMax);
                    overlap = Overlap(current.xMin, current.xMax, candidate.xMin, candidate.xMax);
                    orthogonalWeight = VerticalWeight;
                    break;
                default:
                    return float.MaxValue;
            }

            return displacement + orthogonalWeight * orthogonalDistance - Mathf.Sqrt(overlap);
        }

        private static bool IsCenterPastLeadingEdge(
            Rect current,
            Rect candidate,
            FocusNavigationDirection direction)
        {
            Vector2 center = candidate.center;
            switch (direction)
            {
                case FocusNavigationDirection.Left:
                    return center.x < current.xMin;
                case FocusNavigationDirection.Right:
                    return center.x > current.xMax;
                case FocusNavigationDirection.Up:
                    return center.y > current.yMax;
                case FocusNavigationDirection.Down:
                    return center.y < current.yMin;
                default:
                    return false;
            }
        }

        private static float Overlap(float aMin, float aMax, float bMin, float bMax)
        {
            float overlapMin = Mathf.Max(aMin, bMin);
            float overlapMax = Mathf.Min(aMax, bMax);
            return Mathf.Max(0f, overlapMax - overlapMin);
        }

        private static float OrthogonalGap(float aMin, float aMax, float bMin, float bMax)
        {
            return Mathf.Max(0f, Mathf.Max(bMin - aMax, aMin - bMax));
        }
    }
}
