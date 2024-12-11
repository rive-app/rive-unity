using System;
using Rive.Components;
using Rive.Utils;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Rive.Components
{
    /// <summary>
    /// Provides input to a RiveTextureRenderer and the RivePanel it displays.
    /// </summary>
    internal class TexturePanelInputProvider : MonoBehaviour, IPanelInputProvider, IPointerDownHandler, IPointerUpHandler, IPointerMoveHandler
    {


        [HideInInspector]
        [SerializeField] private RiveTextureRenderer m_rivePanelTextureRenderer;

        private bool m_hasLoggedWrongColliderTypeError = false;

        public event Action<Vector2> PointerPressed;
        public event Action<Vector2> PointerReleased;
        public event Action<Vector2> PointerMoved;


        private IRivePanel RivePanel
        {
            get
            {
                if (m_rivePanelTextureRenderer == null)
                {
                    return null;
                }
                return m_rivePanelTextureRenderer.RivePanel;
            }
        }



        private bool IsSupportedCollider(Collider collider)
        {
            return collider is MeshCollider;
        }

        void OnEnable()
        {
            if (m_rivePanelTextureRenderer == null)
            {
                m_rivePanelTextureRenderer = GetComponent<RiveTextureRenderer>();

                if (m_rivePanelTextureRenderer == null)
                {
                    DebugLogger.Instance.LogWarning($"No {nameof(RiveTextureRenderer)} component found on the GameObject - {gameObject.name}");
                }
            }
        }



        /// <summary>
        /// Tries to get the normalized local point in the frame from the RaycastResult.
        /// </summary>
        /// <param name="raycastResult"> The RaycastResult to get the normalized local point from. </param>
        /// <param name="normalizedLocalPointInFrame"> The normalized local point in the frame. </param>
        /// <returns> True if the normalized local point was successfully retrieved, false otherwise. </returns>
        private bool TryGetNormalizedLocalPointInPanel(RaycastResult raycastResult, out Vector2 normalizedLocalPointInFrame)
        {
            normalizedLocalPointInFrame = Vector2.zero;


            if (RivePanel == null)
            {
                return false;
            }


            if (raycastResult.gameObject == null)
            {
                return false;
            }

            Camera camera = raycastResult.module != null ? raycastResult.module.eventCamera : null;
            if (camera == null)
            {
                return false;
            }

            Ray ray = camera.ScreenPointToRay(raycastResult.screenPosition);

            if (!Physics.Raycast(ray, out RaycastHit hit))
            {
                return false;
            }


            // Get the collider from the hit object.
            Collider collider = hit.collider;

            if (collider == null)
            {
                DebugLogger.Instance.Log("Collider is null.");
                return false;
            }


            Vector2 pixelUV;

            if (IsSupportedCollider(collider))
            {
                // For mesh colliders, we can use the texture coordinates directly
                pixelUV = hit.textureCoord;

            }
            else
            {
                LogWrongColliderErrorIfNeeded();
                return false;
            }

            normalizedLocalPointInFrame = pixelUV;


            return true;
        }


        private void LogWrongColliderErrorIfNeeded()
        {
            if (!m_hasLoggedWrongColliderTypeError)
            {
                DebugLogger.Instance.LogWarning($"Only MeshColliders are supported for pointer input on Rive Panels. Make sure the collider on the GameObject is a MeshCollider, or set the {nameof(PointerInputMode)} to {nameof(PointerInputMode.DisablePointerInput)} on the {nameof(RiveTextureRenderer)}.");
                m_hasLoggedWrongColliderTypeError = true;
            }
        }


        private void ProcessEvent(PointerEventData eventData, Action<Vector2> pointerHandler)
        {
            if (pointerHandler == null)
            {
                return;
            }

            if (RivePanel == null || m_rivePanelTextureRenderer.PointerInputMode == PointerInputMode.DisablePointerInput || !RivePanel.Enabled)
            {
                return;
            }

            if (TryGetNormalizedLocalPointInPanel(eventData.pointerCurrentRaycast, out Vector2 normalizedLocalPointInPanel))
            {
                pointerHandler?.Invoke(normalizedLocalPointInPanel);
            }
        }


        public void OnPointerMove(PointerEventData eventData)
        {
            ProcessEvent(eventData, PointerMoved);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            ProcessEvent(eventData, PointerReleased);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            ProcessEvent(eventData, PointerPressed);
        }


        void OnValidate()
        {
            if (m_rivePanelTextureRenderer == null)
            {
                m_rivePanelTextureRenderer = GetComponent<RiveTextureRenderer>();
            }
        }

    }
}
