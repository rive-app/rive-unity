using System;
using System.Collections;
using System.Collections.Generic;
using Rive.EditorTools;
using Rive.Utils;
using UnityEngine;
using UnityEngine.Pool;

namespace Rive.Components
{

    /// <summary>
    /// RivePanels are responsible for displaying and updating a collection of RiveWidgets. The panel represents a viewport within which the widgets are displayed and rendered to a single Render Target.
    /// </summary>
    [AddComponentMenu("Rive/Rive Panel")]
    [DisallowMultipleComponent]
    [InspectorSection(RivePanelInspectorSections.Advanced, "Advanced", order: 1, style: SectionStyle.Foldout)]
    [InspectorSection(RivePanelInspectorSections.Events, "Events", order: 2, style: SectionStyle.Foldout)]

#if UNITY_EDITOR
    // Since it is a required component, we only want to hide the PanelContextPreviewManager component. We still want it to be serialized so that it isn't constantly recreated when entering and exiting play mode 
    [HideComponents(hideFlags: HideFlags.HideInInspector, typeof(PanelContextPreviewManager))]
    [RequireComponent(typeof(PanelContextPreviewManager))]
#endif
    [RequireComponent(typeof(RectTransform))]
    public sealed class RivePanel : MonoBehaviour, IRivePanel
    {
        /// <summary>
        /// The mode for updating the panel.
        /// </summary>
        public enum PanelUpdateMode
        {
            /// <summary>
            /// The panel and its widgets will be tick automatically.
            /// </summary>
            Auto = 0,

            /// <summary>
            /// The panel and its widgets will tick only when explicitly called.
            /// </summary>
            Manual = 1
        }

        private static class RivePanelInspectorSections
        {

            public const string Events = "events";
            public const string Advanced = "advanced";
        }

        private class WidgetMetadata
        {
            public Action SiblingIndexChangedAction;
            public Action ParentChangedAction;

            public void Reset()
            {
                SiblingIndexChangedAction = null;
                ParentChangedAction = null;

            }
        }

        Comparer<IRiveWidget> m_widgetComparer;

        private IRenderTargetStrategy m_renderTargetStrategy;

        [InspectorField(RivePanelInspectorSections.Advanced, "Custom Render Target Strategy")]
        [Tooltip("The RenderTargetStrategy to use for rendering the panel. By default, each panel renders to a single RenderTexture that matches the panel's RectTransform dimensions. However, you can pass in a different strategy to render to a pool of RenderTextures or to a single RenderTexture that is shared between multiple panels.")]
        [SerializeField] private RenderTargetStrategy m_customRenderTargetStrategy;

        [Tooltip("Determines how the panel will update its widgets. In Auto mode, the panel will tick its widgets every frame. In Manual mode, the panel will only update and advance its widgets when you explictly call the panel's Tick() method. This is useful if you want to control how often the panel updates its widgets.")]
        [SerializeField] private PanelUpdateMode m_updateMode = PanelUpdateMode.Auto;

        [InspectorField(RivePanelInspectorSections.Advanced)]
        [Tooltip("Determines whether the panel will be rendered in the Edit mode.")]
        [SerializeField] private bool m_disableEditorPreview = false;


        private List<IRiveWidget> m_sortedWidgets = new List<IRiveWidget>();
        private Dictionary<WidgetBehaviour, WidgetMetadata> widgetMetadata = new Dictionary<WidgetBehaviour, WidgetMetadata>();


        private List<IPanelInputProvider> m_panelInputProviders = new List<IPanelInputProvider>();


        private List<IRiveWidget> m_raycastResults = new List<IRiveWidget>();
        private Coroutine m_sortWidgetsCoroutine;

        private readonly Action<IRiveWidget, Vector2> m_pointerDownHandler = (widget, localPoint) => widget.OnPointerDown(localPoint);
        private readonly Action<IRiveWidget, Vector2> m_pointerUpHandler = (widget, localPoint) => widget.OnPointerUp(localPoint);
        private readonly Action<IRiveWidget, Vector2> m_pointerMoveHandler = (widget, localPoint) => widget.OnPointerMove(localPoint);

        private readonly Action<IRiveWidget, Vector2> m_pointerExitHandler = (widget, localPoint) => widget.OnPointerExit(localPoint);

        private readonly Action<IRiveWidget, Vector2> m_pointerEnterHandler = (widget, localPoint) => widget.OnPointerEnter(localPoint);


        private static int DefaultEventHandlersPoolCapacity => 1;


        private static ObjectPool<WidgetMetadata> s_handlersPool;
        private static ObjectPool<WidgetMetadata> EventHandlerPool
        {
            get
            {
                if (s_handlersPool == null)
                {
                    s_handlersPool = new ObjectPool<WidgetMetadata>(
                        createFunc: () => new WidgetMetadata(),
                        actionOnRelease: (handlers) => handlers.Reset(),
                        collectionCheck: true,
                        defaultCapacity: DefaultEventHandlersPoolCapacity
                    );
                }
                return s_handlersPool;
            }
        }



        /// <summary>
        /// Used to track if the panel is dirty outside of widget updates.
        /// </summary>
        private bool m_isDirty = false;

        /// <summary>
        /// Sets the panel dirty. This will cause the panel to be redrawn on the next frame.
        /// </summary>
        internal void SetDirty()
        {
            if (WidgetContainer == null)
            {
                return;
            }
            m_isDirty = true;
        }

        /// <summary>
        /// The RectTransform that holds the panel's widgets.
        /// </summary>
        public RectTransform WidgetContainer => this.transform as RectTransform;

        public IReadOnlyList<IRiveWidget> Widgets
        {
            get
            {
                return m_sortedWidgets;
            }
        }

        public IRenderTargetStrategy RenderTargetStrategy
        {
            get
            {
                return m_renderTargetStrategy;
            }
            set
            {
                if (ReferenceEquals(m_renderTargetStrategy, value))
                {
                    return;
                }

                bool wasRegisteredInOldStrategy = m_renderTargetStrategy != null && m_renderTargetStrategy.IsPanelRegistered(this);



                UnsubscribeFromRenderTargetStrategyEvents();
                if (m_renderTargetStrategy != null && wasRegisteredInOldStrategy)
                {
                    m_renderTargetStrategy.UnregisterPanel(this);
                }

                m_renderTargetStrategy = value;

                if (m_renderTargetStrategy != null && wasRegisteredInOldStrategy)
                {
                    m_renderTargetStrategy.RegisterPanel(this);
                    // The new strategy will have a different RenderTexture, so we need to let consumers know that the RenderTexture has changed
                    HandleRenderTargetUpdated(this);
                }

                SubscribeToRenderTargetStrategyEvents();


                OnRenderTargetStrategyChanged?.Invoke();
            }
        }


        public PanelUpdateMode UpdateMode
        {
            get
            {
                return m_updateMode;
            }
            set
            {
                m_updateMode = value;
            }
        }


        public bool DisableEditorPreview
        {
            get
            {
                return m_disableEditorPreview;
            }
            set
            {
                m_disableEditorPreview = value;
            }
        }

        public RenderTexture RenderTexture
        {
            get
            {
                if (RenderTargetStrategy == null)
                {
                    return null;
                }
                return RenderTargetStrategy.GetRenderTexture(this);
            }
        }

        public Vector2 ScaleInRenderTexture
        {
            get
            {
                if (RenderTargetStrategy == null)
                {
                    return Vector2.one;
                }
                return RenderTargetStrategy.GetPanelScale(this);
            }
        }

        public Vector2 OffsetInRenderTexture
        {
            get
            {
                if (RenderTargetStrategy == null)
                {
                    return Vector2.zero;
                }
                return RenderTargetStrategy.GetPanelOffset(this);
            }
        }

        public event Action<IRiveWidget> OnWidgetAdded;

        public event Action<IRiveWidget> OnWidgetRemoved;

        public event Action OnRenderingStateChanged;

        public event Action OnRenderTargetStrategyChanged;

        public event Action OnRenderTargetUpdated;




        public bool IsRendering => RenderTargetStrategy == null ? false : RenderTargetStrategy.IsPanelRegistered(this);

        public bool Enabled => this != null && this.enabled && this.gameObject != null && this.gameObject.activeInHierarchy;

        private void InitializeDefaultRenderTargetStrategyIfNeeded()
        {
            if (m_renderTargetStrategy != null)
            {
                return;
            }

            if (m_customRenderTargetStrategy != null)
            {
                m_renderTargetStrategy = m_customRenderTargetStrategy;
                return;
            }



            if (!gameObject.TryGetComponent<SimpleRenderTargetStrategy>(out var strategy))
            {
                strategy = gameObject.AddComponent<SimpleRenderTargetStrategy>();
            }
            m_renderTargetStrategy = strategy;
        }

        private void SubscribeToRenderTargetStrategyEvents()
        {
            if (m_renderTargetStrategy != null)
            {
                m_renderTargetStrategy.OnRenderTargetUpdated += HandleRenderTargetUpdated;
                m_renderTargetStrategy.OnPanelRegistered += HandlePanelRegistrationStateChange;
                m_renderTargetStrategy.OnPanelUnregistered += HandlePanelRegistrationStateChange;
            }
        }

        private void UnsubscribeFromRenderTargetStrategyEvents()
        {
            if (m_renderTargetStrategy != null)
            {
                m_renderTargetStrategy.OnRenderTargetUpdated -= HandleRenderTargetUpdated;
                m_renderTargetStrategy.OnPanelRegistered -= HandlePanelRegistrationStateChange;
                m_renderTargetStrategy.OnPanelUnregistered -= HandlePanelRegistrationStateChange;
            }
        }



        void OnEnable()
        {

            InitializeDefaultRenderTargetStrategyIfNeeded();


            if (!RenderTargetStrategy.IsPanelRegistered(this))
            {
                RegisterPanelAndUpdateTarget();

            }


            if (m_sortedWidgets.Count > 0)
            {
                RedrawIfNeeded();
            }


            SubscribeToRenderTargetStrategyEvents();


        }

        private void HandlePanelRegistrationStateChange(IRivePanel panel)
        {
            if (ReferenceEquals(panel, this))
            {
                OnRenderingStateChanged?.Invoke();
            }
        }

        void Start()
        {
            RedrawIfNeeded();
        }

        void OnDisable()
        {

            UnsubscribeFromRenderTargetStrategyEvents();

            if (RenderTargetStrategy != null && RenderTargetStrategy.IsPanelRegistered(this))
            {
                UnregisterAndTriggerEventIfNeeded();
            }



        }

        public static void ClearWidgetEventPool()
        {
            s_handlersPool?.Clear();
            s_handlersPool = null;
        }

        private bool UnregisterAndTriggerEventIfNeeded()
        {
            if (RenderTargetStrategy.IsPanelRegistered(this))
            {
                bool wasUnregistered = RenderTargetStrategy.UnregisterPanel(this);

                if (wasUnregistered)
                {
                    OnRenderingStateChanged?.Invoke();
                }

                return wasUnregistered;
            }

            return false;
        }


        private void SubscribeToWidgetEvents(WidgetBehaviour widget)
        {
            var metadata = EventHandlerPool.Get();
            metadata.SiblingIndexChangedAction = () => HandleWidgetSiblingIndexChanged(widget);
            metadata.ParentChangedAction = () => HandleWidgetHierarchyChanged(widget);

            widgetMetadata[widget] = metadata;

            widget.OnSiblingIndexChanged += metadata.SiblingIndexChangedAction;
            widget.OnParentChanged += metadata.ParentChangedAction;
        }

        private void UnsubscribeFromWidgetEvents(WidgetBehaviour widget)
        {

            if (widgetMetadata.TryGetValue(widget, out var metadata))
            {
                widget.OnSiblingIndexChanged -= metadata.SiblingIndexChangedAction;
                widget.OnParentChanged -= metadata.ParentChangedAction;
                EventHandlerPool.Release(metadata);
                widgetMetadata.Remove(widget);
            }
        }

        private void HandleWidgetSiblingIndexChanged(WidgetBehaviour widget)
        {
            SortWidgetsIfNeeded();
        }

        private void HandleWidgetHierarchyChanged(WidgetBehaviour widget)
        {

            SortWidgetsIfNeeded();

        }

        /// <summary>
        /// Registers a widget with the panel. The widget will be added to the panel's list of widgets and will be rendered to the panel's Render Target
        /// </summary>
        /// <param name="widget"></param>
        internal void RegisterWidgetForRendering(WidgetBehaviour widget)
        {
            InitializeDefaultRenderTargetStrategyIfNeeded();



            if (widget == null)
            {
                DebugLogger.Instance.LogWarning("Cannot add widget that is not a RiveWidgetBase.");
                return;
            }

            if (!widget.transform.IsChildOf(this.transform))
            {
                DebugLogger.Instance.LogWarning("Cannot add widget that is not a child of this panel.");
                return;
            }


            if (!m_sortedWidgets.Contains(widget))
            {
                InsertSorted(widget);


                RedrawIfNeeded();
                SubscribeToWidgetEvents(widget);

                OnWidgetAdded?.Invoke(widget);
            }
        }



        /// <summary>
        /// Removes a widget from the panel. The widget will be removed from the panel's list of widgets and will no longer be rendered to the panel's Render Target.
        /// </summary>
        /// <param name="widget"></param>
        internal void UnregisterWidgetFromRendering(WidgetBehaviour widget)
        {

            if (widget == null)
            {
                DebugLogger.Instance.LogWarning("Cannot remove widget that is not a RiveWidgetBase.");
                return;
            }

            if (m_sortedWidgets.Contains(widget))
            {
                m_sortedWidgets.Remove(widget);

                SortWidgetsIfNeeded();
                RedrawIfNeeded();
                UnsubscribeFromWidgetEvents(widget);

                OnWidgetRemoved?.Invoke(widget);
            }
        }

        /// <summary>
        /// Adds a widget to the panel's hierarchy. The widget will be a child of the panel's RectTransform.
        /// </summary>
        /// <param name="widget"></param>
        public void AddToHierarchy(WidgetBehaviour widget)
        {
            if (widget == null)
            {
                DebugLogger.Instance.LogWarning("Cannot add null widget to hierarchy.");
                return;
            }

            if (widget.transform.IsChildOf(this.transform))
            {
                DebugLogger.Instance.LogWarning("Widget is already a child of this panel.");
                return;
            }

            widget.transform.SetParent(this.transform, false);
        }

        /// <summary>
        /// Removes a widget from the panel's hierarchy. The widget will no longer be a child of the panel's RectTransform.
        /// </summary>
        /// <param name="widget"></param>
        public void RemoveFromHierarchy(WidgetBehaviour widget)
        {
            if (widget == null)
            {
                DebugLogger.Instance.LogWarning("Cannot remove null widget from hierarchy.");
                return;
            }

            if (!widget.transform.IsChildOf(this.transform))
            {
                DebugLogger.Instance.LogWarning("Widget is not a child of this panel.");
                return;
            }

            widget.transform.SetParent(null, false);
        }

        public bool ContainsWidget(WidgetBehaviour widget)
        {
            return m_sortedWidgets.Contains(widget);
        }



        private void InsertSorted(WidgetBehaviour riveWidget)
        {


            if (m_widgetComparer == null)
            {
                m_widgetComparer = Comparer<IRiveWidget>.Create(CompareWidgets);
            }


            int index = m_sortedWidgets.BinarySearch(riveWidget, m_widgetComparer);
            // We find the correct insertion point for the new widget
            // If index >= 0, an exact match was found, so we insert at that index
            // If index < 0, no exact match was found. In this case, the binary search returns the bitwise complement of the index where the element should be inserted.
            // We use -(index + 1) to decode this and get the actual insertion point.
            if (index < 0)
            {
                index = -(index + 1);
            }
            m_sortedWidgets.Insert(index, riveWidget);
        }



        private int CompareWidgets(IRiveWidget a, IRiveWidget b)
        {
            var widgetA = a as WidgetBehaviour;
            var widgetB = b as WidgetBehaviour;

            if (IsDescendantOf(widgetA.transform, widgetB.transform))
            {
                // If A is a descendant of B, A should be rendered after (on top of) B
                return 1;
            }
            if (IsDescendantOf(widgetB.transform, widgetA.transform))
            {
                // If B is a descendant of A, B should be rendered after (on top of) A
                return -1;
            }

            Transform commonAncestor = FindCommonAncestor(widgetA.transform, widgetB.transform);

            // Get the first children of the common ancestor that are ancestors of A and B
            Transform childA = GetFirstChildInPath(commonAncestor, widgetA.transform);
            Transform childB = GetFirstChildInPath(commonAncestor, widgetB.transform);

            // Compare the sibling indices of these children to determine rendering order
            if (childA != null && childB != null)
            {
                return childA.GetSiblingIndex().CompareTo(childB.GetSiblingIndex());
            }

            // We fallback to depth comparison if something went wrong
            return GetDepth(widgetA.transform).CompareTo(GetDepth(widgetB.transform));
        }


        private Transform FindCommonAncestor(Transform a, Transform b)
        {
            if (a == b) return a;

            // First, find depth of both transforms
            int depthA = 0, depthB = 0;
            Transform currentA = a, currentB = b;

            while (currentA != null)
            {
                depthA++;
                currentA = currentA.parent;
            }

            while (currentB != null)
            {
                depthB++;
                currentB = currentB.parent;
            }

            // Reset to start
            currentA = a;
            currentB = b;

            // Move deeper transform up until both are at same depth
            while (depthA > depthB)
            {
                currentA = currentA.parent;
                depthA--;
            }

            while (depthB > depthA)
            {
                currentB = currentB.parent;
                depthB--;
            }

            // Move up both transforms until we find common ancestor
            while (currentA != currentB)
            {
                currentA = currentA.parent;
                currentB = currentB.parent;

                // If we reached the root without finding common ancestor
                if (currentA == null || currentB == null)
                    return null;
            }

            return currentA;
        }

        private Transform GetFirstChildInPath(Transform ancestor, Transform descendant)
        {
            if (ancestor == null || descendant == null) return null;

            Transform current = descendant;
            Transform previous = null;

            while (current != ancestor && current != null)
            {
                previous = current;
                current = current.parent;
            }

            return previous;
        }

        private void OnTransformChildrenChanged()
        {
            SortWidgetsIfNeeded();

        }




        private IEnumerator SortAndRedrawWidgets()
        {
            yield return null;
            if (m_sortedWidgets.Count > 1)
            {
                m_sortedWidgets.Sort(m_widgetComparer);
                RedrawIfNeeded();
            }

            m_sortWidgetsCoroutine = null;
        }

        private void SortWidgetsIfNeeded()
        {
            if (m_sortWidgetsCoroutine != null)
            {
                return;
            }
            if (m_sortedWidgets.Count > 1 && this.gameObject.activeInHierarchy)
            {
                m_sortWidgetsCoroutine = StartCoroutine(SortAndRedrawWidgets());
            }
        }


        private bool IsDescendantOf(Transform descendant, Transform ancestor)
        {
            Transform parent = descendant.parent;
            while (parent != null && parent != this.transform)
            {
                if (parent == ancestor)
                {
                    return true;
                }
                parent = parent.parent;
            }
            return false;
        }


        private int GetDepth(Transform t)
        {
            int depth = 0;
            while (t.parent != this.transform && t.parent != null)
            {
                depth++;
                t = t.parent;
            }
            return depth;
        }


        /// <summary>
        /// Called when the RectTransform dimensions change. We resize the RenderTexture to match the new dimensions.
        /// </summary>
        private void OnRectTransformDimensionsChange()
        {
            if (WidgetContainer == null || !WidgetContainer.gameObject.activeInHierarchy)
            {
                return;
            }


            RedrawIfNeeded();

        }



        private void RedrawIfNeeded()
        {

            if (!gameObject.activeInHierarchy || !this.enabled)
            {
                return;
            }

            InitializeDefaultRenderTargetStrategyIfNeeded();

            if (!RenderTargetStrategy.IsPanelRegistered(this))
            {
                return;
            }


            RenderTargetStrategy.DrawPanel(this);



        }

        private bool RegisterPanelAndUpdateTarget()
        {
            if (RenderTargetStrategy == null)
            {
                return false;
            }
            bool wasRegistered = RenderTargetStrategy.RegisterPanel(this);

            if (wasRegistered)
            {
                OnRenderingStateChanged?.Invoke();
            }
            return wasRegistered;
        }



        private void HandleRenderTargetUpdated(IRivePanel panel)
        {
            if (ReferenceEquals(panel, this))
            {
                OnRenderTargetUpdated?.Invoke();
            }
        }






        void Update()
        {
            if (m_updateMode == PanelUpdateMode.Auto)
            {
                Tick(Time.deltaTime);
            }
        }

        /// <summary>
        /// Called every frame to update the widgets. This is where the widgets should update their visuals based on their state.
        /// </summary>
        /// <param name="deltaTime"></param>
        public void Tick(float deltaTime)
        {
            bool panelNeedsRedraw = false;

            // We go through the widgets in reverse order to avoid issues with potentially removing widgets while iterating
            for (int i = m_sortedWidgets.Count - 1; i >= 0; i--)
            {
                var widget = m_sortedWidgets[i];
                if (widget != null)
                {
                    bool currentWidgetNeedsRedraw = widget.Tick(deltaTime);

                    if (!panelNeedsRedraw && currentWidgetNeedsRedraw)
                    {
                        panelNeedsRedraw = true;
                    }

                }
            }

            if (panelNeedsRedraw || m_isDirty)
            {
                m_isDirty = false;
                RedrawIfNeeded();
            }
        }



        public bool StartRendering()
        {
            if (!this.enabled || !this.gameObject.activeInHierarchy)
            {
                DebugLogger.Instance.LogWarning("Cannot start rendering on a disabled or inactive panel.");
                return false;
            }

            if (RenderTargetStrategy == null)
            {
                DebugLogger.Instance.LogWarning("Cannot start rendering on a panel with no RenderTargetStrategy set.");
                return false;
            }

            if (!IsRendering)
            {
                return RegisterPanelAndUpdateTarget();
            }

            return false;

        }

        public bool StopRendering()
        {
            if (RenderTargetStrategy == null)
            {
                DebugLogger.Instance.LogWarning("Cannot stop rendering on a panel with no RenderTargetStrategy set.");
                return false;
            }

            if (IsRendering)
            {
                return UnregisterAndTriggerEventIfNeeded();
            }

            return false;
        }

        /// <summary>
        /// Sets the width and height of the panel.
        /// </summary>
        /// <param name="dimensions"> The new dimensions of the panel.</param>
        public void SetDimensions(Vector2 dimensions)
        {
            if (WidgetContainer == null)
            {
                return;
            }

            // Make sure the dimensions are valid
            if (dimensions.x <= 0 || dimensions.y <= 0 || float.IsNaN(dimensions.x) || float.IsNaN(dimensions.y) || float.IsInfinity(dimensions.x) || float.IsInfinity(dimensions.y))
            {
                DebugLogger.Instance.LogWarning($"Invalid dimensions for panel. Width: {dimensions.x}, Height: {dimensions.y}");
                return;
            }

            WidgetContainer.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, dimensions.x);
            WidgetContainer.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, dimensions.y);
        }

        // Input


        private void HandlePointerDown(Vector2 normalizedPointInPanel)
        {
            ProcessPointerEvent(normalizedPointInPanel, m_pointerDownHandler);
        }

        private void HandlePointerUp(Vector2 normalizedPointInPanel)
        {
            ProcessPointerEvent(normalizedPointInPanel, m_pointerUpHandler);
        }

        private void HandlePointerMove(Vector2 normalizedPointInPanel)
        {
            ProcessPointerEvent(normalizedPointInPanel, m_pointerMoveHandler);
        }

        private void HandlePointerExit(Vector2 normalizedPointInPanel)
        {
            ProcessPointerEvent(normalizedPointInPanel, m_pointerExitHandler);
        }

        private void HandlePointerEnter(Vector2 normalizedPointInPanel)
        {
            ProcessPointerEvent(normalizedPointInPanel, m_pointerEnterHandler);
        }

        private void ProcessPointerEvent(Vector2 normalizedPointInPanel, Action<IRiveWidget, Vector2> handler)
        {
            m_raycastResults.Clear();
            PanelRaycaster.RaycastAll(this, normalizedPointInPanel, m_raycastResults);

            for (int i = m_raycastResults.Count - 1; i >= 0; i--)
            {
                var widget = m_raycastResults[i];

                if (widget == null)
                {
                    continue;
                }

                Vector2 normalizedWidgetPoint;

                PanelRaycaster.TryGetNormalizedPointInWidget(
                                    this, normalizedPointInPanel, widget, out normalizedWidgetPoint);


                handler(widget, normalizedWidgetPoint);

            }
        }


        public void RegisterInputProvider(IPanelInputProvider inputProvider)
        {
            if (inputProvider == null || m_panelInputProviders.Contains(inputProvider))
            {
                return;
            }

            inputProvider.PointerPressed += HandlePointerDown;
            inputProvider.PointerReleased += HandlePointerUp;
            inputProvider.PointerMoved += HandlePointerMove;
            inputProvider.PointerExited += HandlePointerExit;
            inputProvider.PointerEntered += HandlePointerEnter;

            m_panelInputProviders.Add(inputProvider);
        }

        public void UnregisterInputProvider(IPanelInputProvider inputProvider)
        {
            if (inputProvider == null || !m_panelInputProviders.Contains(inputProvider))
            {
                return;
            }

            inputProvider.PointerPressed -= HandlePointerDown;
            inputProvider.PointerReleased -= HandlePointerUp;
            inputProvider.PointerMoved -= HandlePointerMove;
            inputProvider.PointerExited -= HandlePointerExit;
            inputProvider.PointerEntered -= HandlePointerEnter;

            m_panelInputProviders.Remove(inputProvider);

        }
    }
}
