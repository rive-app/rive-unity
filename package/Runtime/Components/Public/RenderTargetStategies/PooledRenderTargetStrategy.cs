using System.Collections.Generic;
using Rive.Components.Utilities;
using Rive.EditorTools;
using Rive.Utils;
using UnityEngine;
using UnityEngine.Pool;

namespace Rive.Components
{
    /// <summary>
    /// A render target strategy that uses a pool of render textures and renderers to render Rive objects. If you want multiple RiveViews to share the same render texture pool, you can assign the same PoolRenderTargetStrategy reference to them.
    /// </summary>
    [AddComponentMenu("Rive/Render Target Strategies/Pooled Render Target Strategy")]
    public class PooledRenderTargetStrategy : RenderTargetStrategy
    {
        private struct PanelOffsetScale
        {
            public Vector2 Offset;
            public Vector2 Scale;

            public PanelOffsetScale(Vector2 offset, Vector2 scale)
            {
                Offset = offset;
                Scale = scale;
            }
        }

        private class PanelMetadataInfo
        {
            public RenderTexture RenderTexture;
            public Renderer Renderer;
            public Vector2 Scale;
            public Vector2 Offset;

            public void Initialize(RenderTexture renderTexture, Renderer renderer, Vector2 scale, Vector2 offset)
            {
                RenderTexture = renderTexture;
                Renderer = renderer;
                Scale = scale;
                Offset = offset;
            }

            public void Clear()
            {
                RenderTexture = null;
                Renderer = null;
                Scale = Vector2.zero;
                Offset = Vector2.zero;
            }
        }

        /// <summary>
        /// The behavior of the pool when there are no available render textures.
        /// </summary>
        public enum PoolOverflowBehavior
        {
            /// <summary>
            /// Creates new objects when the pool is full and destroys them when they are released
            /// </summary>
            Flexible = 0,

            /// <summary>
            /// If there are no available objects in the pool, prevent new panels from being rendered
            /// </summary>
            Fixed = 1,

        }

        [Tooltip("Controls when rendering occurs. In Batched mode, panels are rendered once per frame regardless of redraw requests. In Immediate mode, panels are rendered instantly when requested.")]
        [SerializeField] private DrawTimingOption m_drawTiming = DrawTimingOption.DrawBatched;

        [WidthHeightDimensions("Texture Size")]
        [Tooltip("The size of the pooled render textures. The rendered panels will be scaled to fit within this size.")]
        [SerializeField] private Vector2Int m_pooledTextureSize = new Vector2Int(1024, 1024);

        [Tooltip("The initial allocated size of the pool.")]
        [SerializeField] private int m_initialPoolSize = 2;

        [Tooltip("The maximum size the pool can grow to.")]
        [SerializeField] private int m_maxPoolSize = 6;

        [Tooltip("The behavior of the pool when there are no available render textures. 'Flexible' creates new objects when the pool is full and destroys them when they are released. 'Fixed' prevents new panels from being rendered if there are no available objects in the pool.")]
        [SerializeField] private PoolOverflowBehavior m_poolOverflowBehavior = PoolOverflowBehavior.Flexible;

        private ObjectPool<RenderTexture> m_renderTexturePool;

        private ObjectPool<PanelMetadataInfo> m_panelMetadataPool;

        private Dictionary<IRivePanel, PanelMetadataInfo> m_rivePanelData = new Dictionary<IRivePanel, PanelMetadataInfo>();

        private Dictionary<RenderTexture, Renderer> m_renderTextureRendererMap = new Dictionary<RenderTexture, Renderer>();

        private List<IRivePanel> m_panelsToRedraw = new List<IRivePanel>();


        private bool IsInitialized => m_renderTexturePool != null;


        private RenderTargetSpaceOccupancy TargetSpaceOccupancy
        {
            get
            {
                return RenderTargetSpaceOccupancy.Exclusive;
            }
        }



        /// <summary>
        /// The behavior of the pool when there are no available render textures.
        /// </summary>
        public PoolOverflowBehavior PoolOverflow { get => m_poolOverflowBehavior; set => m_poolOverflowBehavior = value; }

        /// <summary>
        /// The size of the pooled render textures.
        /// </summary>
        public Vector2Int PooledTextureSize { get => m_pooledTextureSize; }

        /// <summary>
        /// The initial allocated size of the pool.
        /// </summary>
        public int InitialPoolSize { get => m_initialPoolSize; }

        /// <summary>
        /// The maximum size the pool can grow to.
        /// </summary>
        public int MaxPoolSize { get => m_maxPoolSize; }
        public override DrawTimingOption DrawTiming { get => m_drawTiming; set => m_drawTiming = value; }

        private void InitializePoolIfNeeded()
        {
            if (IsInitialized)
            {
                return;
            }
            m_renderTexturePool = new ObjectPool<RenderTexture>(
                createFunc: AllocateRenderTexture,
                actionOnDestroy: HandleRenderTextureDestroyAction,
                collectionCheck: true,
                defaultCapacity: m_initialPoolSize,
                maxSize: m_maxPoolSize
            );

            m_panelMetadataPool = new ObjectPool<PanelMetadataInfo>(
                createFunc: () => new PanelMetadataInfo(),
                actionOnRelease: info => info.Clear(),
                actionOnDestroy: null,
                defaultCapacity: m_initialPoolSize,
                maxSize: m_maxPoolSize
            );


        }

        /// <summary>
        /// Configures the pool settings. Only works before the pool is initialized.
        /// </summary>
        /// <param name="textureSize">The size of pooled render textures</param>
        /// <param name="initialPoolSize">Initial pool capacity</param>
        /// <param name="maxPoolSize">Maximum pool size</param>
        /// <param name="overflowBehavior">How to handle pool overflow</param>
        /// <returns>True if settings were applied, false if pool was already initialized</returns>
        public bool Configure(Vector2Int textureSize, int initialPoolSize, int maxPoolSize, PoolOverflowBehavior overflowBehavior)
        {
            if (IsInitialized)
            {
                DebugLogger.Instance.LogWarning("Cannot configure PooledRenderTargetStrategy after initialization");
                return false;
            }

            m_pooledTextureSize = textureSize;
            m_initialPoolSize = initialPoolSize;
            m_maxPoolSize = maxPoolSize;
            m_poolOverflowBehavior = overflowBehavior;
            return true;
        }

        private RenderTexture AllocateRenderTexture()
        {
            return CreateRenderTexture(m_pooledTextureSize.x, m_pooledTextureSize.y);
        }


        private void HandleRenderTextureDestroyAction(RenderTexture rt)
        {
            ReleaseRenderTexture(rt);
            Destroy(rt);
        }

        private PanelOffsetScale CalculatePanelOffsetScale(IRivePanel rivePanel, RenderTexture renderTexture)
        {
            int pooledTextureWidth = m_pooledTextureSize.x;
            int pooledTextureHeight = m_pooledTextureSize.y;
            Vector2Int dimensions = new Vector2Int((int)rivePanel.WidgetContainer.rect.size.x, (int)rivePanel.WidgetContainer.rect.size.y);
            float scaleX = (float)pooledTextureWidth / dimensions.x;
            float scaleY = (float)pooledTextureHeight / dimensions.y;
            float scale = Mathf.Min(scaleX, scaleY);

            Vector2 scaledSize = new Vector2(dimensions.x * scale, dimensions.y * scale);
            // We calculate the offset to center the panel in the render texture
            Vector2 offset = new Vector2(
                (pooledTextureWidth - scaledSize.x) * 0.5f,
                (pooledTextureHeight - scaledSize.y) * 0.5f
            );

            return new PanelOffsetScale(offset, new Vector2(scale, scale));
        }


        private Renderer GetOrCreateRendererForRenderTexture(RenderTexture renderTexture)
        {
            if (m_renderTextureRendererMap.TryGetValue(renderTexture, out var renderer))
            {
                return renderer;
            }

            renderer = RendererUtils.CreateRenderer(renderTexture);
            m_renderTextureRendererMap[renderTexture] = renderer;
            return renderer;
        }

        public override RenderTexture GetRenderTexture(IRivePanel rivePanel)
        {
            if (m_rivePanelData.TryGetValue(rivePanel, out var info))
            {
                return info.RenderTexture;
            }
            return null;
        }

        public override Vector2 GetPanelOffset(IRivePanel rivePanel)
        {
            int pooledTextureWidth = m_pooledTextureSize.x;
            int pooledTextureHeight = m_pooledTextureSize.y;
            if (m_rivePanelData.TryGetValue(rivePanel, out var info))
            {
                // Calculate and return the normalized offset
                // This ensures that the offset is correctly scaled to the texture size,
                // allowing for proper positioning of the panel within the larger texture
                // If we don't do this, the panel will be rendered at the wrong size within the material/raw image
                var xOffset = info.Offset.x / pooledTextureWidth;
                var yOffset = info.Offset.y / pooledTextureHeight;


                return new Vector2(
                    xOffset,
                    yOffset
                );
            }
            return Vector2.zero;
        }

        public override Vector2 GetPanelScale(IRivePanel rivePanel)
        {
            int pooledTextureWidth = m_pooledTextureSize.x;
            int pooledTextureHeight = m_pooledTextureSize.y;
            if (m_rivePanelData.TryGetValue(rivePanel, out var info))
            {
                // Calculate and return the normalized scale
                // This accounts for both the scaling applied to fit the render object
                // into the pooled texture and the relative size of the render object
                // compared to the full texture size
                // If we don't do this, the render object will be rendered at the wrong size within the material/raw image
                Vector2Int dimensions = new Vector2Int((int)rivePanel.WidgetContainer.rect.size.x, (int)rivePanel.WidgetContainer.rect.size.y);
                return new Vector2(
                    (dimensions.x * info.Scale.x) / pooledTextureWidth,
                    (dimensions.y * info.Scale.y) / pooledTextureHeight
                );
            }
            return Vector2.one;
        }


        private void HandlePanelDrawing(IRivePanel panel)
        {
            if (m_rivePanelData.TryGetValue(panel, out var info))
            {
                RefreshRenderTextureDimensions(panel);
                var renderer = info.Renderer;
                var offset = info.Offset;
                var scale = info.Scale;
                renderer.SetArtboardDirtCheckEnabled(panel.DrawOptimization == DrawOptimizationOptions.DrawWhenChanged);
                renderer.Clear();

                // Calculate the panel's allocated space within the render texture
                Vector2Int dimensions = new Vector2Int((int)panel.WidgetContainer.rect.size.x, (int)panel.WidgetContainer.rect.size.y);
                Vector2Int scaledDimensions = new Vector2Int(
                    Mathf.RoundToInt(dimensions.x * scale.x),
                    Mathf.RoundToInt(dimensions.y * scale.y)
                );

                var targetInfo = new RenderTargetInfo(
                    new Vector2Int(m_pooledTextureSize.x, m_pooledTextureSize.y), // Full texture size
                    scaledDimensions // Panel's allocated space after scaling
                );

                // Translate to the correct position. We need to do this because rendering the artboard within a frame that is offset and scaled to fit the render texture
                // This allows us to pool render textures of the same size and reuse them for different render objects with different dimensions
                renderer.Transform(System.Numerics.Matrix3x2.CreateTranslation(offset.x, offset.y));

                // Apply scaling
                renderer.Transform(System.Numerics.Matrix3x2.CreateScale(scale.x, scale.y));



                DrawPanelWithRenderer(renderer, panel, targetInfo, TargetSpaceOccupancy);
                TriggerRenderTargetUpdatedEvent(panel);


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

            if (m_panelsToRedraw.Contains(panel))
            {
                return;
            }

            m_panelsToRedraw.Add(panel);

        }
        private void LateUpdate()
        {

            if (DrawTiming != DrawTimingOption.DrawBatched)
            {
                return;
            }
            // We wait till LateUpdate rather than the end of the current frame (e.g. WaitTillEndOfFrame) because using the latter can cause glitches in the rendering
            // We want to batch the drawing of all panels only once instead of drawing them multiple times in the same frame

            if (m_panelsToRedraw.Count > 0)
            {
                for (int i = 0; i < m_panelsToRedraw.Count; i++)
                {
                    HandlePanelDrawing(m_panelsToRedraw[i]);
                }
                m_panelsToRedraw.Clear();
            }
        }


        public override bool IsPanelRegistered(IRivePanel panel)
        {
            return m_rivePanelData.ContainsKey(panel);

        }

        public override bool RegisterPanel(IRivePanel panel)
        {
            if (panel == null || IsPanelRegistered(panel) || IsDestroyed)
            {
                return false;
            }
            InitializePoolIfNeeded();


            if (m_poolOverflowBehavior == PoolOverflowBehavior.Fixed && m_renderTexturePool.CountActive >= m_maxPoolSize)
            {
                DebugLogger.Instance.LogWarning("The render texture pool is empty. Please change the overflow behavior.");
                return false;
            }

            var renderTexture = m_renderTexturePool.Get();
            if (!renderTexture.IsCreated())
            {
                renderTexture.Create();
            }
            var offsetScale = CalculatePanelOffsetScale(panel, renderTexture);

            var renderer = GetOrCreateRendererForRenderTexture(renderTexture);

            if (!ReferenceEquals(renderer.RenderQueue.Texture, renderTexture))
            {
                RenderPipelineHandler.SetRendererTexture(renderer, renderTexture);

            }



            PanelMetadataInfo meta = m_panelMetadataPool.Get();
            meta.Initialize(renderTexture, renderer, offsetScale.Scale, offsetScale.Offset);

            m_rivePanelData[panel] = meta;

            RegisterRenderer(renderer);

            DrawPanel(panel);

            return true;
        }

        public override bool UnregisterPanel(IRivePanel panel)
        {
            if (m_rivePanelData.TryGetValue(panel, out var info))
            {

                UnregisterRenderer(info.Renderer);
                m_renderTexturePool.Release(info.RenderTexture);
                m_panelMetadataPool.Release(info);
                m_rivePanelData.Remove(panel);
            }

            return true;
        }

        /// <summary>
        /// Refreshes the render texture for the given panel if necessary.
        /// </summary>
        /// <param name="panel"> The panel to refresh the render texture for.</param>
        /// <returns>True if the render texture was refreshed, false otherwise.</returns>
        private bool RefreshRenderTextureDimensions(IRivePanel panel)
        {
            bool neededRefresh = false;

            if (m_rivePanelData.TryGetValue(panel, out var info))
            {
                PanelOffsetScale offsetScale = CalculatePanelOffsetScale(panel, info.RenderTexture);

                neededRefresh = offsetScale.Offset != info.Offset || offsetScale.Scale != info.Scale;

                if (!neededRefresh)
                {
                    return false;
                }
                info.Offset = offsetScale.Offset;
                info.Scale = offsetScale.Scale;


            }


            return neededRefresh;
        }



        protected override IEnumerable<Renderer> GetRenderers()
        {
            foreach (var kvp in m_rivePanelData)
            {
                yield return kvp.Value.Renderer;
            }
        }

        private void Cleanup()
        {
            // Reuse this list for cleanup
            m_panelsToRedraw.Clear();
            m_panelsToRedraw.AddRange(m_rivePanelData.Keys);



            for (int i = m_panelsToRedraw.Count - 1; i >= 0; i--)
            {
                var panel = m_panelsToRedraw[i];
                if (panel != null)
                {
                    UnregisterPanel(panel);
                }
            }

            m_panelsToRedraw?.Clear();
            m_panelMetadataPool?.Clear();
            m_renderTexturePool?.Clear(); // This will destroy all render textures

            // Cleanup renderers
            foreach (var kvp in m_renderTextureRendererMap)
            {
                RendererUtils.ReleaseRenderer(kvp.Value);
            }

            m_renderTextureRendererMap.Clear();
        }

        protected override void OnDestroy()
        {
            Cleanup();
            base.OnDestroy();
        }


    }
}
