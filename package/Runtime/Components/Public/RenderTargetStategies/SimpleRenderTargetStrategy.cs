using System.Collections.Generic;
using Rive.Components.Utilities;
using Rive.Utils;
using UnityEngine;

namespace Rive.Components
{
    /// <summary>
    /// This render target strategy uses a single render texture for each panel. This is the default strategy used unless a custom strategy is specified. This component should be attached to the same GameObject as the RivePanel component it is managing.
    /// </summary>
    [AddComponentMenu("Rive/Render Target Strategies/Simple Render Target Strategy")]
    [DisallowMultipleComponent]
    public class SimpleRenderTargetStrategy : RenderTargetStrategy
    {

        [SerializeField] private RivePanel m_panel;

        [Tooltip("Controls when rendering occurs. In Batched mode, panels are rendered once per frame regardless of redraw requests. In Immediate mode, panels are rendered instantly when requested.")]
        [SerializeField] private DrawTimingOption m_drawTiming = DrawTimingOption.DrawBatched;



        private Renderer m_renderer;
        private RenderTexture m_renderTexture;
        private bool m_redrawRequested = false;



        private void OnEnable()
        {
            if (m_panel == null && !TryGetComponent(out m_panel))
            {
                DebugLogger.Instance.LogError($"No {nameof(RivePanel)} component found on GameObject. {nameof(SimpleRenderTargetStrategy)} requires a {nameof(RivePanel)} component.");
            }

        }

        private RenderTargetSpaceOccupancy TargetSpaceOccupancy
        {
            get
            {
                return RenderTargetSpaceOccupancy.Exclusive;
            }
        }

        public override DrawTimingOption DrawTiming { get => m_drawTiming; set => m_drawTiming = value; }

        public override bool RegisterPanel(IRivePanel panel)
        {
            if (panel == null)
            {
                DebugLogger.Instance.LogWarning("Trying to add a null panel to the strategy.");
                return false;
            }

            if (!ReferenceEquals(panel, m_panel))
            {
                DebugLogger.Instance.LogWarning("Trying to register wrong panel with this strategy.");
                return false;
            }

            if (IsPanelRegistered(panel))
            {
                DebugLogger.Instance.LogWarning("Panel is already registered with this strategy.");
                return false;
            }

            if (IsDestroyed)
            {
                return false;
            }


            CreateAndRegisterRendererForPanel();
            DrawPanel(panel);
            TriggerPanelRegisteredEvent(panel);

            return true;
        }

        public override bool UnregisterPanel(IRivePanel panel)
        {
            if (IsPanelRegistered(panel))
            {
                UnregisterRenderer(m_renderer);

                TriggerPanelUnregisteredEvent(panel);
                return true;
            }

            return false;
        }

        private void CreateAndRegisterRendererForPanel()
        {
            if (m_renderer == null)
            {
                m_renderer = RendererUtils.CreateRenderer();
            }

            RegisterRenderer(m_renderer);
        }

        public override bool IsPanelRegistered(IRivePanel panel)
        {
            if (m_renderer == null || m_panel == null || panel == null)
            {
                return false;
            }
            return ReferenceEquals(panel, m_panel) && RenderPipelineHandler.IsRendererRegistered(m_renderer);
        }

        public override RenderTexture GetRenderTexture(IRivePanel panel)
        {
            if (!IsPanelRegistered(panel))
            {
                return null;
            }

            return ReferenceEquals(panel, m_panel) ? m_renderTexture : null;
        }


        public override Vector2 GetPanelOffset(IRivePanel panel)
        {
            return Vector2.zero;
        }

        public override Vector2 GetPanelScale(IRivePanel panel)
        {
            return Vector2.one;
        }

        private bool RefreshRenderTexture(IRivePanel panel)
        {
            Vector2Int size =
    ExternalPixelSizeProvider != null
        ? ExternalPixelSizeProvider(panel)
        : new Vector2Int(
              Mathf.Max(1, (int)panel.WidgetContainer.rect.width),
              Mathf.Max(1, (int)panel.WidgetContainer.rect.height)
          );
            size.x = Mathf.Max(1, size.x);
            size.y = Mathf.Max(1, size.y);

            // Use the persistent texture if it exists
            if (m_renderTexture == null)
            {
                m_renderTexture = CreateRenderTexture(size.x, size.y);
                if (m_renderTexture == null)
                {
                    return false;
                }
                if (!m_renderTexture.IsCreated())
                {
                    m_renderTexture.Create();
                }
                RenderPipelineHandler.SetRendererTexture(m_renderer, m_renderTexture);
                return true;
            }

            // Resize if needed
            if (m_renderTexture.width != size.x || m_renderTexture.height != size.y)
            {
                m_renderTexture = ResizeRenderTexture(m_renderTexture, size.x, size.y);
                if (!m_renderTexture.IsCreated())
                {
                    m_renderTexture.Create();
                }
                RenderPipelineHandler.SetRendererTexture(m_renderer, m_renderTexture);

                return true;
            }

            // If for some reason the renderer is not using the correct texture, update it
            if (m_renderer != null && !ReferenceEquals(m_renderer.RenderQueue.Texture, m_renderTexture))
            {
                RenderPipelineHandler.SetRendererTexture(m_renderer, m_renderTexture);

            }

            return false;
        }

        protected override IEnumerable<Renderer> GetRenderers()
        {
            if (m_renderer != null)
            {
                yield return m_renderer;
            }
        }

        public override void DrawPanel(IRivePanel panel)
        {
            if (!IsPanelRegistered(panel))
            {
                return;
            }

            if (DrawTiming == DrawTimingOption.DrawImmediate)
            {
                HandlePanelDrawing(panel);
                return;
            }

            // For DrawBatched mode
            m_redrawRequested = true;

        }

        private void HandlePanelDrawing(IRivePanel panel)
        {
            if (!IsPanelRegistered(panel))
            {
                return;
            }

            bool wasRefreshed = RefreshRenderTexture(panel);
            m_renderer.Clear();


            if (ExternalDrawScaleProvider != null)
            {
                var s = ExternalDrawScaleProvider(panel);
                if (Mathf.Abs(s.x - 1f) > 0.001f || Mathf.Abs(s.y - 1f) > 0.001f)
                {
                    m_renderer.Transform(System.Numerics.Matrix3x2.CreateScale(s.x, s.y));
                }
            }


            var targetInfo = new RenderTargetInfo(
                new Vector2Int(m_renderTexture.width, m_renderTexture.height), // Target size
                new Vector2Int(m_renderTexture.width, m_renderTexture.height)  // Panel allocation (same as target since panel uses entire texture)
            );



            DrawPanelWithRenderer(m_renderer, panel, targetInfo, TargetSpaceOccupancy);

            if (wasRefreshed)
            {
                TriggerRenderTargetUpdatedEvent(panel);
            }
        }



        void LateUpdate()
        {
            if (DrawTiming != DrawTimingOption.DrawBatched)
            {
                return;
            }

            if (m_redrawRequested)
            {
                HandlePanelDrawing(m_panel);
                m_redrawRequested = false;
            }
        }

        private void Cleanup()
        {
            if (m_renderer != null)
            {
                UnregisterRenderer(m_renderer);
                RendererUtils.ReleaseRenderer(m_renderer);
                m_renderer = null;
            }

            if (m_renderTexture != null)
            {
                ReleaseRenderTexture(m_renderTexture);
                Destroy(m_renderTexture);
                m_renderTexture = null;
            }
        }

        protected override void OnDestroy()
        {

            Cleanup();
            base.OnDestroy();

        }
    }
}