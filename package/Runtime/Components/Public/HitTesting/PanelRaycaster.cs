using System.Collections.Generic;
using Rive.Utils;
using UnityEngine;

namespace Rive.Components
{
    /// <summary>
    /// Utility class for performing hit testing (raycasting) on RivePanels to detect which widgets are under a given point.
    /// This is used to handle input events and determine which widgets should receive pointer interactions.
    /// </summary>
    public class PanelRaycaster
    {
        /// <summary>
        /// Populates the raycastResults list with the widgets in a RivePanel that are hit by the given normalized local point in the panel.
        /// </summary>
        /// <param name="rivePanel"> The RivePanel to check for hit widgets. </param>
        /// <param name="normalizedPointInPanel"> The normalized local point in the panel to check for hit widgets. The coordinates are in the range [0,1] where (0,0) is the bottom-left corner and (1,1) is the top-right corner. </param>
        /// <param name="eventCamera"> The camera used by the event system or canvas </param>
        /// <param name="raycastResults"> The list to populate with hit widgets. </param>
        public static void RaycastAll(IRivePanel rivePanel, Vector2 normalizedPointInPanel, List<IRiveWidget> raycastResults)
        {
            RectTransform panelRectTransform = rivePanel.WidgetContainer;
            if (panelRectTransform == null)
            {
                DebugLogger.Instance.LogError("Panel RectTransform is null.");
                return;
            }



            for (int i = rivePanel.Widgets.Count - 1; i >= 0; i--)
            {
                var widget = rivePanel.Widgets[i];
                if (widget == null || !widget.Enabled || widget.RenderObject == null || widget.HitTestBehavior == HitTestBehavior.None)
                    continue;

                Vector2 normalizedWidgetPoint;
                bool isWithinWidgetBounds = TryGetNormalizedPointInWidget(rivePanel, normalizedPointInPanel, widget, out normalizedWidgetPoint);

                if (ProcessHitTestBehavior(widget, normalizedWidgetPoint, raycastResults, isWithinWidgetBounds))
                {
                    return;
                }


            }
        }


        /// <summary>
        /// Processes the hit test behavior of a widget and adds it to the raycastResults list if it should be hit.
        /// </summary>
        /// <param name="widget"> The widget to process. </param>
        /// <param name="normalizedPointInWidgetRect"> The normalized point in the widget's rect to check for a hit. The coordinates are in the range [0,1] where (0,0) is the bottom-left corner and (1,1) is the top-right corner. </param>
        /// <param name="raycastResults"> The list to populate with hit widgets. </param>
        /// <returns> True if should should block other widgets from being hit, false otherwise. </returns>
        private static bool ProcessHitTestBehavior(IRiveWidget widget, Vector2 normalizedPointInWidgetRect, List<IRiveWidget> raycastResults, bool isWithinWidgetBounds)
        {
            switch (widget.HitTestBehavior)
            {
                case HitTestBehavior.Opaque:

                    raycastResults.Add(widget);
                    // Block other widgets from being hit if the pointer is within the widget
                    return isWithinWidgetBounds;
                case HitTestBehavior.Translucent:
                    bool foundHit = widget.HitTest(normalizedPointInWidgetRect);

                    if (foundHit)
                    {
                        raycastResults.Add(widget);
                        return isWithinWidgetBounds;
                    }
                    break;
                case HitTestBehavior.Transparent:
                    raycastResults.Add(widget);
                    // Continue checking other widgets
                    return false;
                case HitTestBehavior.None:
                    // Do not add to raycastResults
                    return false;
            }

            return false;
        }

        /// <summary>
        /// Tries to get the normalized local point in the widget from the normalized local point in the panel.
        /// </summary>
        /// <param name="rivePanel"> The RivePanel that contains the widget. </param>
        /// <param name="normalizedPointInPanel"> The normalized local point in the panel.  The coordinates are in the range [0,1] where (0,0) is the bottom-left corner and (1,1) is the top-right corner.</param>
        /// <param name="widget"> The widget to get the normalized local point in. </param>
        /// <param name="normalizedWidgetPoint"> The normalized point in the widget's rect. </param>
        /// <returns> True if the normalized local point is within the widget's bounds, false otherwise. </returns>
        public static bool TryGetNormalizedPointInWidget(IRivePanel rivePanel, Vector2 normalizedPointInPanel, IRiveWidget widget, out Vector2 normalizedWidgetPoint)
        {
            normalizedWidgetPoint = Vector2.zero;

            RectTransform panelRectTransform = rivePanel.WidgetContainer;
            if (panelRectTransform == null)
            {
                DebugLogger.Instance.LogError("Panel RectTransform is null.");
                return false;
            }

            // We convert normalized local point [0,1] to local point [-0.5, 0.5] relative to panel's size
            Vector2 panelLocalPoint = new Vector2(
                (normalizedPointInPanel.x - 0.5f) * panelRectTransform.rect.width,
                (normalizedPointInPanel.y - 0.5f) * panelRectTransform.rect.height
            );

            Vector3 worldPoint = panelRectTransform.TransformPoint(panelLocalPoint);

            Vector3 widgetLocalPoint = widget.RectTransform.InverseTransformPoint(worldPoint);

            normalizedWidgetPoint = new Vector2(
                   (widgetLocalPoint.x - widget.RectTransform.rect.xMin) / widget.RectTransform.rect.width,
                   (widgetLocalPoint.y - widget.RectTransform.rect.yMin) / widget.RectTransform.rect.height
               );

            if (widget.RectTransform.rect.Contains(widgetLocalPoint))
            {
                return true;
            }

            return false;
        }
    }
}
