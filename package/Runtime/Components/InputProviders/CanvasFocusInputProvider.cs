using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rive.Components
{
    /// <summary>
    /// Hidden Selectable that feeds EventSystem navigation into the panel.
    /// </summary>
    internal class CanvasFocusInputProvider : Selectable, IPanelFocusProvider, IUpdateSelectedHandler, ISubmitHandler, ICancelHandler
    {
        [HideInInspector]
        [SerializeField] private RiveCanvasRenderer m_riveCanvasRenderer;

        private bool m_isRegistered;
        private bool m_hasHostFocus;

        private RivePanel Panel => m_riveCanvasRenderer != null ? m_riveCanvasRenderer.RivePanel as RivePanel : null;

        private PanelFocusController Controller => Panel != null ? Panel.FocusController : null;

        public bool HasHostFocus => m_hasHostFocus;

        protected override void Awake()
        {
            base.Awake();
            transition = Transition.None;
            FetchRendererIfNeeded();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            FetchRendererIfNeeded();
            TryRegister();
        }

        protected override void OnDisable()
        {
            Unregister();
            m_hasHostFocus = false;
            base.OnDisable();
        }

        private void FetchRendererIfNeeded()
        {
            if (m_riveCanvasRenderer == null || !ReferenceEquals(m_riveCanvasRenderer.gameObject, gameObject))
            {
                m_riveCanvasRenderer = GetComponent<RiveCanvasRenderer>();
            }
        }

        internal void TryRegister()
        {
            RivePanel panel = Panel;
            if (panel == null)
            {
                return;
            }

            if (m_riveCanvasRenderer != null && m_riveCanvasRenderer.FocusBehavior == FocusBehavior.DisableFocus)
            {
                Unregister();
                interactable = false;
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
            interactable = false;
        }

        public void SetFocusable(bool isFocusable)
        {
            bool enableFocus = m_riveCanvasRenderer == null ||
                               m_riveCanvasRenderer.FocusBehavior == FocusBehavior.EnableFocus;
            interactable = enableFocus && isFocusable;
        }

        public void RequestNativeFocus()
        {
            if (EventSystem.current == null)
            {
                return;
            }

            EventSystem.current.SetSelectedGameObject(gameObject);
        }

        public void ContinueNavigation(FocusNavigationDirection direction)
        {
            Selectable next = FindNeighbor(direction);
            if (next == null || !next.IsActive() || EventSystem.current == null)
            {
                return;
            }
            // As navigation, so a neighboring Rive panel enters from the right side.
            var eventData = new AxisEventData(EventSystem.current) { moveDir = ToMoveDirection(direction) };
            EventSystem.current.SetSelectedGameObject(next.gameObject, eventData);
        }

        private Selectable FindNeighbor(FocusNavigationDirection direction)
        {
            switch (direction)
            {
                case FocusNavigationDirection.Left:
                    return FindSelectableOnLeft();
                case FocusNavigationDirection.Right:
                    return FindSelectableOnRight();
                case FocusNavigationDirection.Up:
                    return FindSelectableOnUp();
                case FocusNavigationDirection.Down:
                    return FindSelectableOnDown();
                default:
                    return null;
            }
        }

        private static MoveDirection ToMoveDirection(FocusNavigationDirection direction)
        {
            switch (direction)
            {
                case FocusNavigationDirection.Left:
                    return MoveDirection.Left;
                case FocusNavigationDirection.Right:
                    return MoveDirection.Right;
                case FocusNavigationDirection.Up:
                    return MoveDirection.Up;
                case FocusNavigationDirection.Down:
                    return MoveDirection.Down;
                default:
                    return MoveDirection.None;
            }
        }

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
            m_hasHostFocus = true;
            PanelFocusController controller = Controller;
            if (controller == null || !ReferenceEquals(controller.FocusProvider, this))
            {
                return;
            }

            ClassifySelect(eventData, out FocusEntryReason reason, out FocusNavigationDirection? direction);
            controller.NotifyHostFocusGained(reason, direction);
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            m_hasHostFocus = false;
            PanelFocusController controller = Controller;
            if (controller != null && ReferenceEquals(controller.FocusProvider, this))
            {
                controller.NotifyHostFocusLost();
            }
            base.OnDeselect(eventData);
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            PanelFocusController controller = Controller;
            if (controller != null && ReferenceEquals(controller.FocusProvider, this))
            {
                controller.NotifyHostPointerActivated();
            }
            base.OnPointerDown(eventData);
        }

        public void OnUpdateSelected(BaseEventData eventData)
        {
            PanelFocusController controller = Controller;
            if (controller != null && ReferenceEquals(controller.FocusProvider, this))
            {
                controller.Keyboard.OnUpdateSelected(eventData);
            }
        }

        public void OnSubmit(BaseEventData eventData)
        {
            PanelFocusController controller = Controller;
            if (controller != null && ReferenceEquals(controller.FocusProvider, this))
            {
                controller.Keyboard.OnSubmit();
            }
        }

        public void OnCancel(BaseEventData eventData)
        {
            PanelFocusController controller = Controller;
            if (controller != null && ReferenceEquals(controller.FocusProvider, this))
            {
                controller.Keyboard.OnCancel();
            }
        }

        public override void OnMove(AxisEventData eventData)
        {
            PanelFocusController controller = Controller;
            // A held arrow key already moves focus through the keyboard.
            if (controller != null &&
                ReferenceEquals(controller.FocusProvider, this) &&
                controller.Keyboard.OwnsMove(eventData.moveDir))
            {
                eventData.Use();
                return;
            }

            if (controller != null &&
                ReferenceEquals(controller.FocusProvider, this) &&
                TryGetDirection(eventData.moveDir, out FocusNavigationDirection direction))
            {
                if (controller.TryMoveFocus(direction))
                {
                    eventData.Use();
                    return;
                }
            }

            base.OnMove(eventData);
        }

        internal static bool TryGetDirection(MoveDirection moveDir, out FocusNavigationDirection direction)
        {
            switch (moveDir)
            {
                case MoveDirection.Left:
                    direction = FocusNavigationDirection.Left;
                    return true;
                case MoveDirection.Right:
                    direction = FocusNavigationDirection.Right;
                    return true;
                case MoveDirection.Up:
                    direction = FocusNavigationDirection.Up;
                    return true;
                case MoveDirection.Down:
                    direction = FocusNavigationDirection.Down;
                    return true;
                default:
                    direction = FocusNavigationDirection.Next;
                    return false;
            }
        }

        private static void ClassifySelect(
            BaseEventData eventData,
            out FocusEntryReason reason,
            out FocusNavigationDirection? direction)
        {
            direction = null;

            if (eventData is PointerEventData)
            {
                reason = FocusEntryReason.Pointer;
                return;
            }

            if (eventData is AxisEventData axisEventData &&
                TryGetDirection(axisEventData.moveDir, out FocusNavigationDirection navDirection))
            {
                reason = FocusEntryReason.Navigation;
                direction = navDirection;
                return;
            }

            reason = FocusEntryReason.Programmatic;
        }

        private void OnValidate()
        {
            FetchRendererIfNeeded();
        }
    }
}
