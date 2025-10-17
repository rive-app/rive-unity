using System;
using Rive.EditorTools;
using Rive.Utils;
using UnityEngine;
using UnityEngine.Events;


namespace Rive.Components
{
    /// <summary>
    /// Base class for creating custom RiveWidgets for different contexts.
    /// </summary>
    [DisallowMultipleComponent]
    [InspectorSection(InspectorSections.Advanced, "Advanced", order: 1, style: SectionStyle.Foldout)]
    [RequireComponent(typeof(RectTransform))]
    public abstract class WidgetBehaviour : MonoBehaviour, IRiveWidget
    {
        protected static class InspectorSections
        {

            public const string Advanced = "advanced";
        }


        [HideInInspector]
        [SerializeField] private RivePanel m_rivePanel;


        private WidgetStatus m_status = WidgetStatus.Uninitialized;


        private int m_cachedSiblingIndex;

        private bool m_shouldTriggerRedraw = false;



        public abstract IRenderObject RenderObject { get; }


        /// <summary>
        /// The current loading status of the widget.
        /// </summary>
        public WidgetStatus Status
        {
            get => m_status; protected set
            {

                if (m_status == value)
                {
                    return;
                }

                m_status = value;

                OnWidgetStatusChanged?.Invoke();
            }
        }

        public bool Enabled => this != null && this.enabled && this.gameObject != null && this.gameObject.activeInHierarchy;


        // These events are used to notify the RivePanel that the widget's sorting might need to be updated.
        public event Action OnSiblingIndexChanged;

        public event Action OnParentChanged;



        public RivePanel RivePanel
        {
            get => m_rivePanel; private set
            {
                if (ReferenceEquals(m_rivePanel, value))
                {
                    return;
                }

                if (m_rivePanel != null)
                {
                    UnsubscribeFromPanelEvents(m_rivePanel);
                }

                m_rivePanel = value;

                if (m_rivePanel != null)
                {
                    SubscribeToPanelEvents(m_rivePanel);
                }

            }
        }

        public RectTransform RectTransform => this.transform as RectTransform;

        public abstract HitTestBehavior HitTestBehavior { get; set; }




        public event Action OnWidgetStatusChanged;



        protected virtual void OnEnable()
        {
            if (RivePanel == null || !IsDescendantOfPanel(RivePanel))
            {
                RivePanel = GetComponentInParent<RivePanel>();
            }


            m_cachedSiblingIndex = this.transform.GetSiblingIndex();

            UpdateRenderTransform();

            if (RivePanel != null)
            {
                RivePanel.RegisterWidgetForRendering(this);
            }


        }


        protected virtual void OnDisable()
        {
            if (m_rivePanel != null)
            {
                m_rivePanel.UnregisterWidgetFromRendering(this);
            }
        }



        private void SubscribeToPanelEvents(RivePanel panel)
        {
            if (panel == null)
            {
                return;
            }

            panel.OnWidgetRemoved += HandleWidgetRemoved;
        }

        private void UnsubscribeFromPanelEvents(RivePanel panel)
        {
            if (panel == null)
            {
                return;
            }

            panel.OnWidgetRemoved -= HandleWidgetRemoved;
        }


        /// <summary>
        /// This is called after the RiveWidget has finished loading but before the OnLoad event is triggered. Use this to perform any additional setup after the RiveWidget has loaded.
        /// </summary>
        protected virtual void HandleLoadComplete()
        {

            // Do this to account for any changes in the RectTransform that may have occurred before the RiveWidget was loaded.
            UpdateRenderTransform();
            TriggerRedrawNeededEvent();

            Status = WidgetStatus.Loaded;

        }





        /// <summary>
        /// Called when the RectTransform dimensions change. We resize the RenderTexture to match the new dimensions.
        /// </summary>
        protected virtual void OnRectTransformDimensionsChange()
        {
            if (m_rivePanel == null || !this.gameObject.activeInHierarchy)
            {
                return;
            }

            if (RenderObject == null)
            {
                return;
            }

            // Make sure we update the rect in the RenderObject as that is what is used to calculate the dimensions.
            UpdateRenderTransform();

            TriggerRedrawNeededEvent();


        }



        protected void TriggerRedrawNeededEvent()
        {
            m_shouldTriggerRedraw = true;
        }

        private void HandleWidgetRemoved(IRiveWidget widget)
        {
            if (!ReferenceEquals(widget, this))
            {
                return;
            }

            if (RivePanel != null && !RivePanel.ContainsWidget(this))
            {
                RivePanel = null;
            }


        }

        private void UpdateRenderTransform()
        {
            if (RenderObject == null)
            {
                return;
            }

            RectTransform panelRectTransform = RivePanel != null ? RivePanel.WidgetContainer : null;

            if (panelRectTransform == null)
            {
                return;
            }



            RenderObject.RenderTransform = RenderTransform.FromRectTransform(RectTransform, panelRectTransform);
        }



        public virtual bool Tick(float deltaTime)
        {
            bool needsRedraw = false;

            if (Status != WidgetStatus.Loaded)
            {
                return needsRedraw;
            }

            if (RectTransform.hasChanged)
            {
                RectTransform.hasChanged = false;
                UpdateRenderTransform();
                OnRectTransformDimensionsChange();
                needsRedraw = true;
            }

            int currentSiblingIndex = this.transform.GetSiblingIndex();
            if (currentSiblingIndex != m_cachedSiblingIndex)
            {
                m_cachedSiblingIndex = currentSiblingIndex;
                OnSiblingIndexChanged?.Invoke();
                needsRedraw = true;
            }

            if (m_shouldTriggerRedraw)
            {
                m_shouldTriggerRedraw = false;
                needsRedraw = true;
            }

            return needsRedraw;
        }

        /// <summary>
        /// This is called when a direct or indirect parent of the transform of the GameObject has changed.
        /// </summary>
        protected virtual void OnTransformParentChanged()
        {
            var currentParentPanel = GetComponentInParent<RivePanel>();


            // Unregister from the old panel if the widget is not a child of it anymore
            // or if the widget is a child of a different panel now.
            if (RivePanel != null && !ReferenceEquals(currentParentPanel, RivePanel))
            {
                RivePanel.UnregisterWidgetFromRendering(this);
            }

            // Register with the new panel if there is one
            if (currentParentPanel != null && !currentParentPanel.ContainsWidget(this))
            {
                currentParentPanel.RegisterWidgetForRendering(this);

                RivePanel = currentParentPanel;

            }

            OnParentChanged?.Invoke();

        }

        /// <summary>
        /// Tests if a given local position within the widget's rectangle hits any interactive elements.
        /// </summary>
        /// <param name="normalizedPointInRect">
        /// The normalized point of the pointer position in the widget's rectangle. The coordinates are in the range [0,1] where (0,0) is the bottom-left corner and (1,1) is the top-right corner.
        /// </param>
        /// <returns>
        /// Returns <c>true</c> if the position hits an interactive element; otherwise, <c>false</c>.
        /// </returns>
        public virtual bool HitTest(Vector2 normalizedPointInRect)
        {
            return false;

        }


        /// <summary>
        /// Called when a pointer is pressed on the widget.
        /// </summary>
        /// <param name="normalizedPointInRect">The normalized point of the pointer press in the widget's rectangle. The coordinates are in the range [0,1] where (0,0) is the bottom-left corner and (1,1) is the top-right corner.</param>
        /// <param name="pointerId">The unique id for the active pointer/touch.</param>
        /// <returns>Returns <c>true</c> if the pointer press hits an interactive element; otherwise, <c>false</c>.</returns>
        public virtual bool OnPointerDown(Vector2 normalizedPointInRect, int pointerId)
        {
            return false;
        }

        /// <summary>
        /// Called when a pointer is released on the widget.
        /// </summary>
        /// <param name="normalizedPointInRect">The normalized point of the pointer release in the widget's rectangle. The coordinates are in the range [0,1] where (0,0) is the bottom-left corner and (1,1) is the top-right corner.</param>
        /// <param name="pointerId">The unique id for the active pointer/touch.</param>
        /// <returns>Returns <c>true</c> if the pointer release hits an interactive element; otherwise, <c>false</c>.</returns>
        public virtual bool OnPointerUp(Vector2 normalizedPointInRect, int pointerId)
        {
            return false;
        }

        /// <summary>
        /// Called when a pointer is moved on the widget.
        /// </summary>
        /// <param name="normalizedPointInRect">The normalized point of the pointer position in the widget's rectangle. The coordinates are in the range [0,1] where (0,0) is the bottom-left corner and (1,1) is the top-right corner.</param>
        /// <param name="pointerId">The unique id for the active pointer/touch.</param>
        /// <returns>Returns <c>true</c> if the pointer move hits an interactive element; otherwise, <c>false</c>.</returns>
        public virtual bool OnPointerMove(Vector2 normalizedPointInRect, int pointerId)
        {
            return false;
        }

        /// <summary>
        /// Called when a pointer exits the widget.
        /// </summary>
        /// <param name="normalizedPointInRect"> The normalized point of the pointer exit in the widget's rectangle. The coordinates are in the range [0,1] where (0,0) is the bottom-left corner and (1,1) is the top-right corner.</param>
        /// <param name="pointerId">The unique id for the active pointer/touch.</param>
        /// <returns> Returns <c>true</c> if the pointer exit hits an interactive element; otherwise, <c>false</c>.</returns>
        public virtual bool OnPointerExit(Vector2 normalizedPointInRect, int pointerId)
        {
            return false;
        }

        /// <summary>
        /// Called when a pointer enters the widget.
        /// </summary>
        /// <param name="normalizedPointInRect">The normalized point of the pointer enter in the widget's rectangle. The coordinates are in the range [0,1] where (0,0) is the bottom-left corner and (1,1) is the top-right corner.</param>
        /// <param name="pointerId">The unique id for the active pointer/touch.</param>
        /// <returns> Returns <c>true</c> if the pointer enter hits an interactive element; otherwise, <c>false</c>.</returns>
        public virtual bool OnPointerEnter(Vector2 normalizedPointInRect, int pointerId)
        {
            return false;
        }

        protected virtual void OnDestroy()
        {

        }
        private bool IsDescendantOfPanel(RivePanel rivePanel)
        {
            if (rivePanel == null)
            {
                return false;
            }

            return this.transform.IsChildOf(rivePanel.WidgetContainer);
        }

#if UNITY_EDITOR

        protected virtual void OnValidate()
        {


            if (RivePanel == null || !IsDescendantOfPanel(RivePanel))
            {
                RivePanel = GetComponentInParent<RivePanel>();
            }


        }

#endif

    }
}