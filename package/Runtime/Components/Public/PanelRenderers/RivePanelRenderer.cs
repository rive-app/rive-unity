using Rive.EditorTools;
using Rive.Utils;
using UnityEngine;

namespace Rive.Components
{
    /// <summary>
    /// The PointerInputMode determines whether the panel will receive pointer input events.
    /// </summary>
    public enum PointerInputMode
    {
        /// <summary>
        /// The panel will receive pointer input events.
        /// </summary>
        EnablePointerInput = 0,

        /// <summary>
        /// The panel will not receive pointer input events.
        /// </summary>
        DisablePointerInput = 1
    }

    /// <summary>
    /// Whether Unity's UI navigation can select a panel renderer when the panel has focusable Rive content.
    /// </summary>
    public enum FocusBehavior : byte
    {
        /// <summary>
        /// Unity can select this renderer and send navigation into the panel.
        /// </summary>
        EnableFocus = 0,

        /// <summary>
        /// Unity's navigation skips this renderer. Pointer input is unaffected.
        /// </summary>
        DisableFocus = 1
    }

    /// <summary>
    /// Base class for components that display a <see cref="RivePanel"/> on a Unity target
    /// such as a Canvas or a Mesh.
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class RivePanelRenderer : MonoBehaviour
    {

#if UNITY_EDITOR
        [OnValueChanged(nameof(HandlePointerInputModeChanged))]
#endif
        [Tooltip("Determines whether the panel will receive pointer input events from this renderer.")]
        [SerializeField] private PointerInputMode m_pointerInputMode = PointerInputMode.EnablePointerInput;

#if UNITY_EDITOR
        [OnValueChanged(nameof(HandleFocusBehaviorChanged))]
#endif
        [Tooltip("If enabled, Unity's UI navigation can select this renderer when the panel has focusable Rive content.")]
        [SerializeField] private FocusBehavior m_focusBehavior = FocusBehavior.EnableFocus;



        /// <summary>
        /// The RivePanel that this renderer is associated with.
        /// </summary>
        public abstract IRivePanel RivePanel { get; internal set; }

        /// <summary>
        /// The PointerInputMode determines whether the panel render will pass pointer input events to the RivePanel.
        /// </summary>
        public PointerInputMode PointerInputMode
        {
            get => m_pointerInputMode; set
            {
                if (m_pointerInputMode == value) { return; }
                m_pointerInputMode = value; HandlePointerInputModeChanged();
            }
        }


        /// <summary>
        /// Whether Unity's UI navigation can select this renderer when the panel has focusable Rive content.
        /// </summary>
        public FocusBehavior FocusBehavior
        {
            get => m_focusBehavior;
            set
            {
                if (m_focusBehavior == value) { return; }
                m_focusBehavior = value;
                HandleFocusBehaviorChanged();
            }
        }

        protected void SubscribeToPanelEvents()
        {
            if (RivePanel == null)
            {
                DebugLogger.Instance.LogWarning($"No {nameof(RivePanel)} component found for this {nameof(RivePanelRenderer)} - {gameObject.name}");
                return;
            }

            RivePanel.OnRenderingStateChanged += OnRenderingStateChanged;
            RivePanel.OnRenderTargetUpdated += HandleRenderTargetUpdated;
        }

        protected void UnsubscribeFromPanelEvents()
        {
            if (RivePanel == null)
            {
                return;
            }
            RivePanel.OnRenderingStateChanged -= OnRenderingStateChanged;
            RivePanel.OnRenderTargetUpdated -= HandleRenderTargetUpdated;
        }



        protected virtual void OnEnable()
        {

            if (RivePanel == null)
            {
                return;
            }



            if (RivePanel.IsRendering)
            {
                UpdateVisualTarget();
            }
            SubscribeToPanelEvents();
        }



        protected virtual void OnDisable()
        {
            if (RivePanel == null)
            {
                return;
            }
            UnsubscribeFromPanelEvents();
        }

        protected void HandleRenderTargetUpdated()
        {
            UpdateVisualTarget();

        }

        private void OnRenderingStateChanged()
        {
            UpdateVisualTarget();
        }


        /// <summary>
        /// Use this method to reflect the Rive visual on the target where the Rive graphic is being displayed. This is called when the render target is updated or when the panel detects it might need to update the targets using the render texture.
        /// </summary>
        protected abstract void UpdateVisualTarget();


        protected virtual void HandlePointerInputModeChanged()
        {

        }

        /// <summary>
        /// Called when FocusBehavior changes.
        /// </summary>
        protected virtual void HandleFocusBehaviorChanged()
        {
        }



        /// <summary>
        /// This method is called when the script is loaded or a value is changed in the inspector (Called in the editor only).
        /// </summary>
        protected virtual void OnValidate()
        {


        }
    }
}
