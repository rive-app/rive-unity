using UnityEngine;
using UnityEngine.EventSystems;

namespace Rive.Components
{
    /// <summary>
    /// EventSystem focus provider for world-space / texture renderers.
    /// Maps UI directions to Rive as-is and does not hunt for neighboring 3D objects.
    /// </summary>
    internal class TextureFocusInputProvider : MonoBehaviour, IPanelFocusProvider, ISelectHandler, IDeselectHandler, IMoveHandler, IPointerDownHandler,
        IUpdateSelectedHandler, ISubmitHandler, ICancelHandler
    {
        [HideInInspector]
        [SerializeField] private RiveTextureRenderer m_riveTextureRenderer;

        private bool m_isRegistered;
        private bool m_hasHostFocus;
        private bool m_isFocusable = true;

        private RivePanel Panel => m_riveTextureRenderer != null ? m_riveTextureRenderer.RivePanel as RivePanel : null;

        private PanelFocusController Controller => Panel != null ? Panel.FocusController : null;

        public bool HasHostFocus => m_hasHostFocus;

        private void OnEnable()
        {
            FetchRendererIfNeeded();
            TryRegister();
        }

        private void OnDisable()
        {
            Unregister();
            m_hasHostFocus = false;
        }

        private void FetchRendererIfNeeded()
        {
            if (m_riveTextureRenderer == null)
            {
                m_riveTextureRenderer = GetComponent<RiveTextureRenderer>();
            }
        }

        internal void BindRenderer(RiveTextureRenderer renderer)
        {
            m_riveTextureRenderer = renderer;
        }

        internal void TryRegister()
        {
            RivePanel panel = Panel;
            if (panel == null)
            {
                return;
            }

            if (m_riveTextureRenderer != null && m_riveTextureRenderer.FocusBehavior == FocusBehavior.DisableFocus)
            {
                Unregister();
                m_isFocusable = false;
                return;
            }

            if (m_isRegistered)
            {
                return;
            }

            panel.FocusController.FocusProvider = this;
            m_isRegistered = true;
        }

        internal void Unregister()
        {
            if (!m_isRegistered)
            {
                return;
            }

            PanelFocusController controller = Controller;
            if (controller != null && ReferenceEquals(controller.FocusProvider, this))
            {
                controller.FocusProvider = null;
            }

            m_isRegistered = false;
        }

        public void NotifyDetached()
        {
            m_isRegistered = false;
            m_hasHostFocus = false;
            m_isFocusable = false;
        }

        public void SetFocusable(bool isFocusable)
        {
            bool enableFocus = m_riveTextureRenderer == null ||
                               m_riveTextureRenderer.FocusBehavior == FocusBehavior.EnableFocus;
            m_isFocusable = enableFocus && isFocusable;
        }

        public void RequestNativeFocus()
        {
            if (EventSystem.current == null)
            {
                return;
            }

            EventSystem.current.SetSelectedGameObject(gameObject);
        }

        // Nothing to move on to, since it doesn't look for neighboring 3D objects.
        public void ContinueNavigation(FocusNavigationDirection direction)
        {
        }

        public void OnSelect(BaseEventData eventData)
        {
            if (!m_isFocusable)
            {
                return;
            }

            PanelFocusController controller = Controller;
            if (controller == null || !ReferenceEquals(controller.FocusProvider, this))
            {
                return;
            }

            m_hasHostFocus = true;

            FocusEntryReason reason = FocusEntryReason.Programmatic;
            FocusNavigationDirection? direction = null;

            if (eventData is PointerEventData)
            {
                reason = FocusEntryReason.Pointer;
            }
            else if (eventData is AxisEventData axisEventData &&
                     CanvasFocusInputProvider.TryGetDirection(axisEventData.moveDir, out FocusNavigationDirection navDirection))
            {
                reason = FocusEntryReason.Navigation;
                direction = navDirection;
            }

            controller.NotifyHostFocusGained(reason, direction);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            m_hasHostFocus = false;
            PanelFocusController controller = Controller;
            if (controller != null && ReferenceEquals(controller.FocusProvider, this))
            {
                controller.NotifyHostFocusLost();
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!m_isFocusable)
            {
                return;
            }

            PanelFocusController controller = Controller;
            if (controller != null && ReferenceEquals(controller.FocusProvider, this))
            {
                controller.NotifyHostPointerActivated();
            }
        }

        public void OnUpdateSelected(BaseEventData eventData)
        {
            PanelFocusController controller = OwnController;
            controller?.Keyboard.OnUpdateSelected(eventData);
        }

        public void OnSubmit(BaseEventData eventData)
        {
            OwnController?.Keyboard.OnSubmit();
        }

        public void OnCancel(BaseEventData eventData)
        {
            OwnController?.Keyboard.OnCancel();
        }

        // The controller, while this is its provider and focusable.
        private PanelFocusController OwnController
        {
            get
            {
                PanelFocusController controller = Controller;
                return m_isFocusable && controller != null && ReferenceEquals(controller.FocusProvider, this)
                    ? controller
                    : null;
            }
        }

        public void OnMove(AxisEventData eventData)
        {
            if (!m_isFocusable)
            {
                return;
            }

            PanelFocusController controller = Controller;
            // A held arrow key already moves focus through the keyboard.
            if (OwnController != null && controller.Keyboard.OwnsMove(eventData.moveDir))
            {
                eventData.Use();
                return;
            }

            if (controller != null &&
                ReferenceEquals(controller.FocusProvider, this) &&
                CanvasFocusInputProvider.TryGetDirection(eventData.moveDir, out FocusNavigationDirection direction) &&
                controller.TryMoveFocus(direction))
            {
                eventData.Use();
            }
        }

        private void OnValidate()
        {
            FetchRendererIfNeeded();
        }
    }
}
