using System;
using System.Collections.Generic;
using Rive.Utils;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rive.Components
{

    internal enum RenderTargetSpaceOccupancy
    {
        /// <summary>
        /// The render target is being used by a single render object.
        /// </summary>
        Exclusive = 0,

        /// <summary>
        /// The render target is occupied by multiple render objects.
        /// </summary>
        Shared = 1,
    }
    /// <summary>
    /// A base for classes that provide a strategy for rendering Rive panels to a render target.
    /// </summary>
    public abstract class RenderTargetStrategy : MonoBehaviour, IRenderTargetStrategy
    {


        internal struct RenderTargetInfo : IEquatable<RenderTargetInfo>
        {
            /// <summary>
            /// The full dimensions of the render target.
            /// </summary>
            public Vector2Int TargetSize { get; private set; }

            /// <summary>
            /// The dimensions allocated to this panel within the target.
            /// </summary>
            public Vector2Int PanelAllocation { get; private set; }

            /// <summary>
            /// Creates a new RenderTargetInfo with the given render target size and panel allocation.
            /// </summary>
            /// <param name="renderTargetSize"> The full dimensions of the render target. </param>
            /// <param name="panelAllocation"> The dimensions allocated to this panel within the target. </param>
            public RenderTargetInfo(Vector2Int renderTargetSize, Vector2Int panelAllocation)
            {
                TargetSize = renderTargetSize;
                PanelAllocation = panelAllocation;
            }

            public bool Equals(RenderTargetInfo other)
            {
                return TargetSize == other.TargetSize &&
                       PanelAllocation == other.PanelAllocation;
            }

            public override bool Equals(object obj)
            {
                return obj is RenderTargetInfo info && Equals(info);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(TargetSize, PanelAllocation);
            }

            public static bool operator ==(RenderTargetInfo left, RenderTargetInfo right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(RenderTargetInfo left, RenderTargetInfo right)
            {
                return !left.Equals(right);
            }
        }

        private bool m_isDestroyed = false;

        private IRenderPipelineHandler m_renderPipelineHandler;

        protected bool IsDestroyed => m_isDestroyed;

        internal Func<IRivePanel, Vector2Int> ExternalPixelSizeProvider { get; set; }
        internal Func<IRivePanel, Vector2> ExternalDrawScaleProvider { get; set; }

        /// <summary>
        /// The render pipeline handler to use for rendering panels.
        /// </summary>
        public IRenderPipelineHandler RenderPipelineHandler
        {
            get
            {
                if (m_renderPipelineHandler == null && !IsDestroyed)
                {
                    m_renderPipelineHandler = RenderPipelineHelper.GetOrCreateHandler();
                }

                return m_renderPipelineHandler;
            }
            set
            {
                if (ReferenceEquals(m_renderPipelineHandler, value))
                {
                    return;
                }

                // Unregister the renderers from the current handler
                if (m_renderPipelineHandler != null)
                {
                    // Unregister all renderers managed by the current handler
                    foreach (Renderer renderer in GetRenderers())
                    {
                        if (m_renderPipelineHandler.IsRendererRegistered(renderer))
                        {
                            m_renderPipelineHandler.Unregister(renderer);
                        }
                    }
                }

                m_renderPipelineHandler = value;

                // Reregister all renderers managed with the new handler
                if (m_renderPipelineHandler != null)
                {
                    foreach (Renderer renderer in GetRenderers())
                    {
                        if (!m_renderPipelineHandler.IsRendererRegistered(renderer))
                        {
                            m_renderPipelineHandler.Register(renderer);
                        }
                    }
                }
            }
        }



        /// <summary>
        /// Controls when rendering occurs. In Batched mode, panels are rendered once per frame regardless of redraw requests. In Immediate mode, panels are rendered instantly when requested.
        /// </summary>
        public abstract DrawTimingOption DrawTiming { get; set; }




        public event Action<IRivePanel> OnRenderTargetUpdated;

        public event Action<IRivePanel> OnPanelRegistered;

        public event Action<IRivePanel> OnPanelUnregistered;

        /// <summary>
        /// Called once per frame (via Orchestrator) after ticking panels.
        /// Override this to handle any batched render requests.
        /// </summary>
        internal protected virtual void PrepareBatchedRender()
        {
        }

        internal void PrepareRenderFromOrchestrator()
        {
            if (m_isDestroyed || !isActiveAndEnabled)
            {
                return;
            }

            PrepareBatchedRender();
        }

        protected virtual void OnEnable()
        {
            Orchestrator.RegisterRenderTargetStrategy(this);
        }

        protected virtual void OnDisable()
        {
            Orchestrator.UnregisterRenderTargetStrategy(this);
        }


        internal static void DrawPanelWithRenderer(Renderer renderer, IRivePanel panel, RenderTargetInfo targetInfo, RenderTargetSpaceOccupancy renderTargetSpaceOccupancy)
        {
            if (renderer == null || panel == null)
            {
                return;
            }

            for (int i = 0; i < panel.Widgets.Count; i++)
            {
                IRiveWidget widget = panel.Widgets[i];
                if (widget == null || widget.RenderObject == null) continue;

                bool isAutoClippedByRenderTarget = false;
                if (widget.RectTransform != null && panel.WidgetContainer != null)
                {
                    isAutoClippedByRenderTarget = ShouldSkipClippingForWidget(
                        widget.RectTransform,
                        panel.WidgetContainer
                    );
                }

                RenderContext context = new RenderContext(
                    isAutoClippedByRenderTarget
                        ? RenderContext.ClippingModeSetting.SkipClipping
                        : RenderContext.ClippingModeSetting.CheckClipping
                );
                DrawRenderObject(renderer, widget.RenderObject, panel, context);
            }
        }

        internal static bool ShouldSkipClippingForWidget(RectTransform widgetTransform, RectTransform containerTransform)
        {

            containerTransform.GetWorldCorners(s_rectTransformCorners);

            // Check if all container corners are inside the widget's local rect
            for (int i = 0; i < 4; i++)
            {
                Vector3 localPoint = widgetTransform.InverseTransformPoint(s_rectTransformCorners[i]);
                if (!RectContainsInclusive(widgetTransform.rect, (Vector2)localPoint))
                {
                    // At least one corner is not covered, so we can't skip clipping
                    return false;
                }
            }

            // All corners are inside the widget, so it fully covers the container. We don't need to clip it because it will be clipped by the render target bounds or by whatever the render target strategy decides to do for panels that share space.
            return true;
        }

        internal static Vector3[] s_rectTransformCorners = new Vector3[4];



        internal static bool DoesPanelNeedClipping(IRivePanel panel)
        {
            if (panel == null || panel.WidgetContainer == null) return false;

            for (int i = 0; i < panel.Widgets.Count; i++)
            {
                IRiveWidget widget = panel.Widgets[i];
                if (widget == null || widget.RectTransform == null) continue;

                if (DoesWidgetHavePointOutsidePanel(panel.WidgetContainer, widget.RectTransform))
                {
                    // Early out if any widget has a point outside the panel
                    return true;
                }

            }
            return false;
        }


        internal static bool DoesWidgetHavePointOutsidePanel(RectTransform containerTransform, RectTransform widgetTransform)
        {
            if (containerTransform == null || widgetTransform == null) return false;

            Vector3[] corners = s_rectTransformCorners;
            Rect containerRect = containerTransform.rect;

            widgetTransform.GetWorldCorners(corners);

            for (int i = 0; i < 4; i++)
            {
                corners[i] = containerTransform.InverseTransformPoint(corners[i]);
                Vector2 localPoint = new Vector2(corners[i].x, corners[i].y);

                // Using Rect.Contains would cause unnecessary clipping in this case because the widget's corners would be on the panel's edges and that method would consider that as outside the panel.
                if (!RectContainsInclusive(containerRect, localPoint))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool RectContainsInclusive(Rect rect, Vector2 point)
        {
            return point.x >= rect.xMin && point.x <= rect.xMax &&
                   point.y >= rect.yMin && point.y <= rect.yMax;
        }



        /// <summary>
        /// Determines if clipping should be skipped based on the widget size, container size, panel allocation and target size.
        /// We skip clipping if the widget fills or exceeds the panel bounds and the panel allocation fills or exceeds the render target bounds because the widget will be clipped by the render target bounds.
        /// </summary>
        /// <param name="widgetSize">The size of the widget in pixels</param>
        /// <param name="containerSize">The size of the widget container in pixels</param>
        /// <param name="panelAllocation">The size allocated to the panel on the render target in pixels</param>
        /// <param name="targetSize">The size of the render target in pixels</param>
        /// <returns> True if clipping should be skipped, false otherwise. </returns>
        internal static bool ShouldSkipClipping(Vector2 widgetSize, Vector2 containerSize, Vector2Int panelAllocation, Vector2Int targetSize)
        {
            // Widget fills or exceeds panel bounds
            bool widgetFillsPanel = widgetSize.x >= containerSize.x &&
                                   widgetSize.y >= containerSize.y;

            // Panel allocation fills or exceeds render target
            bool panelFillsTarget = panelAllocation.x >= targetSize.x &&
                                   panelAllocation.y >= targetSize.y;

            return widgetFillsPanel && panelFillsTarget;
        }



        internal static void DrawRenderObject(Renderer renderer, IRenderObject renderObject, IRivePanel panel, RenderContext renderContext)
        {
            if (renderer == null)
            {
                DebugLogger.Instance.LogError("Render method called with null renderer");
                return;
            }

            if (renderObject == null)
            {
                DebugLogger.Instance.LogWarning("Render method called with null render object");
                return;
            }

            // Save the current state of the renderer so we can restore it after drawing the render object.

            renderer.Save();

            RenderTransform renderTransform = renderObject.RenderTransform;

            System.Numerics.Matrix3x2 localTransform = CalculateRenderObjectTransformMatrix(renderTransform);

            // Apply the final transformation
            renderer.Transform(localTransform);

            // We draw the artboard if this is an ArtboardRenderObject, if this is a procedural drawing, we draw the procedural drawing.
            renderObject.DrawContent(renderer, new AABB(0, 0, renderTransform.Size.x, renderTransform.Size.y), renderContext);

            renderer.Restore();

        }

        /// <summary>
        /// Calculates the transformation matrix for a render object based on its render transform properties.
        /// </summary>
        /// <param name="renderTransform">The render transform containing position, size, rotation, scale and pivot information.</param>
        /// <returns>The calculated transformation matrix.</returns>
        protected static System.Numerics.Matrix3x2 CalculateRenderObjectTransformMatrix(RenderTransform renderTransform)
        {
            Vector2 Size = renderTransform.Size;
            Vector2 Position = renderTransform.Position;
            float Rotation = renderTransform.Rotation;
            Vector2 Scale = renderTransform.Scale;
            Vector2 Pivot = renderTransform.Pivot;

            float pivotOffsetX = Size.x * Pivot.x;
            float pivotOffsetY = Size.y * Pivot.y;

            // Initialize the local transform to identity because we will be applying transformations in order.
            System.Numerics.Matrix3x2 localTransform = System.Numerics.Matrix3x2.Identity;

            // Translate to Origin (Negative Pivot Offset)
            localTransform *= System.Numerics.Matrix3x2.CreateTranslation(-pivotOffsetX, -pivotOffsetY);

            // Apply Scaling
            if (Scale != Vector2.one)
            {
                localTransform *= System.Numerics.Matrix3x2.CreateScale(Scale.x, Scale.y);
            }

            // Apply Rotation (Negated for Rive's coordinate system)
            if (Rotation != 0)
            {
                localTransform *= System.Numerics.Matrix3x2.CreateRotation(Rotation);
            }

            // Translate Back to Pivot Point (Positive Pivot Offset)
            localTransform *= System.Numerics.Matrix3x2.CreateTranslation(pivotOffsetX, pivotOffsetY);
            bool shouldFlipTexture = TextureHelper.ShouldFlipTexture();
            if (shouldFlipTexture)
            {
                // Flip the Y-axis by applying a scale of (1, -1) on the Y axis.
                localTransform *= System.Numerics.Matrix3x2.CreateScale(1, -1);

                // Adjust the pivot offset translation after flipping to keep positioning correct.
                localTransform *= System.Numerics.Matrix3x2.CreateTranslation(0, -Size.y);
            }

            //Translate to Absolute Position
            localTransform *= System.Numerics.Matrix3x2.CreateTranslation(Position.x, Position.y);

            return localTransform;
        }

        /// <summary>
        /// On Windows, OpenGL and D3D seem to have procedural drawings (and paths) rotated by 90 degrees, so we correct for that here
        /// This could be because of something we're doing in RenderTargetStrategy.CalculateRenderObjectTransformMatrix, so this is a workaround for now
        /// This issue doesn't happen in Android and WebGL builds with OpenGL. It also doesn't happen with Artboards, so it looks like it is specific to the procedural API.
        /// </summary>
        /// <returns> True if procedural drawing requires rotation correction, false otherwise. </returns>
        internal static bool ProceduralDrawingRequiresRotationCorrection()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            return TextureHelper.IsOpenGLPlatform() || TextureHelper.IsDirect3DPlatform() || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Vulkan;
#else
            return false;
#endif
        }


        /// <summary>
        /// Creates a new render texture with the given width and height.
        /// </summary>
        /// <param name="width"> The width of the render texture. </param>
        /// <param name="height"> The height of the render texture. </param>
        /// <returns> The new render texture. </returns>
        protected RenderTexture CreateRenderTexture(int width, int height)
        {
            return RenderPipelineHandler.AllocateRenderTexture(width, height);
        }

        protected void ReleaseRenderTexture(RenderTexture renderTexture)
        {
            RenderPipelineHandler.ReleaseRenderTexture(renderTexture);
        }

        protected RenderTexture ResizeRenderTexture(RenderTexture renderTexture, int width, int height)
        {
            return RenderPipelineHandler.ResizeRenderTexture(renderTexture, width, height);
        }

        /// <summary>
        /// Returns the renderers managed by this strategy.
        /// </summary>
        protected abstract IEnumerable<Renderer> GetRenderers();

        /// <summary>
        /// Registers the given renderer with the render pipeline to be rendered.
        /// </summary>
        /// <param name="renderer"></param>
        protected virtual void RegisterRenderer(Renderer renderer)
        {
            RenderPipelineHandler.Register(renderer);
        }

        /// <summary>
        /// Unregisters the given renderer from the render pipeline so it is no longer rendered.
        /// </summary>
        /// <param name="renderer"></param>
        protected virtual void UnregisterRenderer(Renderer renderer)
        {
            RenderPipelineHandler.Unregister(renderer);
        }



        /// <summary>
        /// Triggers the RenderTargetUpdated event for the given panel.
        /// </summary>
        /// <param name="panel"> The panel that was updated. </param>
        protected virtual void TriggerRenderTargetUpdatedEvent(IRivePanel panel)
        {
            OnRenderTargetUpdated?.Invoke(panel);

        }

        /// <summary>
        /// Triggers the PanelRegistered event for the given panel.
        /// </summary>
        /// <param name="panel"> The panel that was registered. </param>
        protected virtual void TriggerPanelRegisteredEvent(IRivePanel panel)
        {
            OnPanelRegistered?.Invoke(panel);
        }

        /// <summary>
        /// Triggers the PanelUnregistered event for the given panel.
        /// </summary>
        /// <param name="panel"> The panel that was unregistered. </param>
        protected virtual void TriggerPanelUnregisteredEvent(IRivePanel panel)
        {
            OnPanelUnregistered?.Invoke(panel);
        }

        protected virtual void OnDestroy()
        {
            m_isDestroyed = true;
            Orchestrator.UnregisterRenderTargetStrategy(this);
        }

        public abstract void DrawPanel(IRivePanel panel);

        public abstract Vector2 GetPanelOffset(IRivePanel panel);

        public abstract Vector2 GetPanelScale(IRivePanel panel);

        public abstract RenderTexture GetRenderTexture(IRivePanel panel);

        public abstract bool IsPanelRegistered(IRivePanel panel);

        public abstract bool RegisterPanel(IRivePanel panel);

        public abstract bool UnregisterPanel(IRivePanel panel);


    }

    internal static class ClippingPathHelper
    {
        /// <summary>
        /// Configures a clipping path based on the platform and given dimensions. This ensures that it looks correct on all platforms.
        /// </summary>
        /// <param name="path">The path to configure</param>
        /// <param name="width">Width of the clipping rectangle</param>
        /// <param name="height">Height of the clipping rectangle</param>
        public static void ConfigureClippingPath(Path path, float width, float height)
        {
            path.Reset();


            bool shouldFlipYClipPath = RenderTargetStrategy.ProceduralDrawingRequiresRotationCorrection();


            if (shouldFlipYClipPath)
            {
                // Swap width and height for OpenGL/D3D11 platforms, otherwise the rect will have the wrong orientation, e.g vertical instead of horizontal
                path.MoveTo(0, 0);
                path.LineTo(0, width);
                path.LineTo(height, width);
                path.LineTo(height, 0);
            }
            else
            {
                // Use normal coordinates for Metal
                path.MoveTo(0, 0);
                path.LineTo(width, 0);
                path.LineTo(width, height);
                path.LineTo(0, height);
            }

            path.Close();
        }
    }
}

