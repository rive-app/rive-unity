using System;
using System.Collections.Generic;
using Rive.Utils;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Rive.Components
{
    /// <summary>
    /// Provides input to a RiveCanvasRenderer and the RivePanel it displays.
    /// </summary>
    internal class CanvasPanelInputProvider : MonoBehaviour, IPanelInputProvider, ICanvasRaycastFilter, IPointerDownHandler, IPointerUpHandler, IPointerMoveHandler
    {

        [HideInInspector]
        [SerializeField] private RiveCanvasRenderer m_riveCanvasRenderer;

        private List<IRiveWidget> m_RaycastResults = new List<IRiveWidget>();



        private IRivePanel RivePanel
        {
            get
            {
                if (m_riveCanvasRenderer == null)
                {
                    return null;
                }
                return m_riveCanvasRenderer.RivePanel;
            }
        }

        public event Action<Vector2> PointerPressed;
        public event Action<Vector2> PointerReleased;
        public event Action<Vector2> PointerMoved;

        void OnValidate()
        {
            FetchPanelRendererIfNeeded();

        }


        private void OnEnable()
        {
            FetchPanelRendererIfNeeded();

            if (m_riveCanvasRenderer == null)
            {
                DebugLogger.Instance.LogWarning($"No {nameof(RiveCanvasRenderer)} component found on the GameObject - {gameObject.name}");
            }
        }

        private void FetchPanelRendererIfNeeded()
        {
            if (m_riveCanvasRenderer == null || !ReferenceEquals(m_riveCanvasRenderer.gameObject, this.gameObject))
            {
                m_riveCanvasRenderer = GetComponent<RiveCanvasRenderer>();
            }
        }

        private static bool TryGetScreenPointToNormalizedLocalPointInFrame(RectTransform rectTransform, Vector2 screenPoint, Camera eventCamera, out Vector2 normalizedLocalPointInFrame)
        {
            normalizedLocalPointInFrame = Vector2.zero;

            if (rectTransform == null)
            {
                return false;
            }

            Vector2 localPoint;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out localPoint))
            {
                return false;
            }

            normalizedLocalPointInFrame = Rect.PointToNormalized(rectTransform.rect, localPoint);

            return true;
        }


        // When used in a canvas, we don't want the panel raycast target to block raycasts if there are no widgets to hit so we filter out the raycasts in this case.
        // This allows the raycasts to pass through to regular canvas raycast targets outside of the panel.
        public bool IsRaycastLocationValid(Vector2 screenPosition, Camera eventCamera)
        {
            if (RivePanel == null || m_riveCanvasRenderer.PointerInputMode == PointerInputMode.DisablePointerInput)
            {
                return false;
            }

            m_RaycastResults.Clear();
            Vector2 localNormalizedPointInPanel;
            if (!TryGetScreenPointToNormalizedLocalPointInFrame(m_riveCanvasRenderer.RectTransform, screenPosition, eventCamera, out localNormalizedPointInPanel))
            {
                return false;
            }

            PanelRaycaster.RaycastAll(RivePanel, localNormalizedPointInPanel, m_RaycastResults);

            bool locationIsValid = m_RaycastResults.Count > 0;


            return locationIsValid;
        }


        private void ProcessPointerEvent(PointerEventData eventData, Action<Vector2> pointerHandler)
        {
            if (pointerHandler == null)
            {
                return;
            }

            if (RivePanel == null || m_riveCanvasRenderer.PointerInputMode == PointerInputMode.DisablePointerInput || !RivePanel.Enabled)
            {
                return;
            }

            Vector2 canvasNormalizedPoint;
            if (!TryGetScreenPointToNormalizedLocalPointInFrame(m_riveCanvasRenderer.RectTransform, eventData.position, eventData.enterEventCamera, out canvasNormalizedPoint))
            {
                return;
            }

            pointerHandler(canvasNormalizedPoint);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            ProcessPointerEvent(eventData, PointerPressed);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            ProcessPointerEvent(eventData, PointerReleased);

        }

        public void OnPointerMove(PointerEventData eventData)
        {
            ProcessPointerEvent(eventData, PointerMoved);
        }


    }
}