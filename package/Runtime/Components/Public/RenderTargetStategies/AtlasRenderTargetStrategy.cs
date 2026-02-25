using System.Collections.Generic;
using Rive.Components.Utilities;
using Rive.EditorTools;
using Rive.Utils;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Rendering;

namespace Rive.Components
{
    /// <summary>
    /// This strategy renders RivePanels to a single atlas texture. It uses the simple Shelf Packing  algorithm to pack the render objects into the atlas. If a render object's dimensions exceeds the maximum resolution, it will be scaled down to fit a specified maximum resolution.
    /// </summary>
#if UNITY_EDITOR
    [InspectorSection(InspectorSections.Advanced, "Advanced", style: SectionStyle.Foldout)]
#endif
    [AddComponentMenu("Rive/Render Target Strategies/Atlas Render Target Strategy")]
    public class AtlasRenderTargetStrategy : RenderTargetStrategy
    {
#if UNITY_EDITOR

        private static class InspectorSections
        {

            public const string Advanced = "advanced";
        }
#endif

        private class RenderObjectData
        {
            public RectInt Position { get; set; }
            public Vector2Int ScaledDimensions { get; set; }
            public Vector2Int LastKnownSize { get; set; }

            public void Reset()
            {
                Position = default;
                ScaledDimensions = default;
                LastKnownSize = default;

            }
        }

        [Tooltip("Controls when rendering occurs. In Batched mode, panels are rendered once per frame regardless of redraw requests. In Immediate mode, panels are rendered instantly when requested.")]
        [SerializeField] private DrawTimingOption m_drawTiming = DrawTimingOption.DrawBatched;


        [WidthHeightDimensions("Starting Size")]
        [Tooltip("The initial dimensions of the atlas texture.")]
        [SerializeField] private Vector2Int m_startingSize = new Vector2Int(1024, 1024);

        [WidthHeightDimensions("Max Atlas Size")]
        [Tooltip("The maximum dimensions the atlas texture can grow to")]
        [SerializeField] private Vector2Int m_maxAtlasSize = new Vector2Int(2048, 2048);

        [Tooltip("The maximum resolution of a single panel within the atlas texture. If a panel's dimensions exceed this resolution, it will be scaled down. This resolution is applied to the larger dimension of the panel's size.")]
        [SerializeField] private int m_maxResolutionPerPanel = 512;

        [Tooltip("The padding between panels in the atlas texture.")]
        [SerializeField] private int m_padding = 0;

#if UNITY_EDITOR
        [InspectorField(InspectorSections.Advanced)]
#endif
        [Tooltip("The custom atlas packing provider to use. If not set, the default Shelf packing provider will be used.")]
        [SerializeField] private RenderTargetAtlasPackingProvider m_customAtlasPackingProvider;

        private RenderTargetAtlasPackingProvider.IPackingStrategy m_packingStrategy;

        private RenderTexture m_atlasTexture;
        private List<IRivePanel> m_renderObjectKeys = new List<IRivePanel>();

        private UnityEngine.Pool.ObjectPool<RenderObjectData> m_dataPool;

        private Path m_clipPath;


        private Renderer m_renderer;

        private bool m_isRendererRegistered = false;


        private bool m_isInitialized = false;


        private Dictionary<IRivePanel, RenderObjectData> m_rivePanelData = new Dictionary<IRivePanel, RenderObjectData>();


        /// <summary>
        /// Batch command options for the atlas render target strategy. Used to determine what action to take on the next frame.
        /// </summary>
        private enum BatchCommandOption
        {
            None = 0,

            Redraw = 1,

            RepackAndRedraw = 2,
        }

        private BatchCommandOption m_batchCommand = BatchCommandOption.None;


        public Vector2Int StartingSize => m_startingSize;

        public Vector2Int MaxAtlasSize => m_maxAtlasSize;

        public int MaxResolutionPerObject => m_maxResolutionPerPanel;

        public int Padding => m_padding;

        public override DrawTimingOption DrawTiming { get => m_drawTiming; set => m_drawTiming = value; }

        /// <summary>
        /// The custom atlas packing provider to use. If not set, the default Shelf packing provider will be used. This can only be set before initialization (before any panels are registered).
        /// </summary>
        public RenderTargetAtlasPackingProvider.IPackingStrategy PackingStrategy
        {
            get => m_packingStrategy;
            set
            {
                if (m_isInitialized)
                {
                    DebugLogger.Instance.LogWarning("Cannot set the packing strategy after initialization.");
                    return;
                }

                m_packingStrategy = value;
            }
        }

        private RenderTargetSpaceOccupancy TargetSpaceOccupancy
        {
            get
            {
                if (m_rivePanelData.Count == 1)
                {
                    return RenderTargetSpaceOccupancy.Exclusive;
                }
                return RenderTargetSpaceOccupancy.Shared;
            }
        }

        private void InitializeIfNeeded()
        {
            if (m_isInitialized)
            {
                return;
            }

            if (m_packingStrategy == null)
            {
                m_packingStrategy = (m_customAtlasPackingProvider == null || m_customAtlasPackingProvider.PackingStrategy == null) ? new ShelfPackingStrategy() : m_customAtlasPackingProvider.PackingStrategy;
            }

            if (m_startingSize.x > m_maxAtlasSize.x || m_startingSize.y > m_maxAtlasSize.y)
            {
                m_maxAtlasSize = m_startingSize;

                DebugLogger.Instance.LogWarning("The starting size of the atlas texture is larger than the maximum atlas size. The atlas texture will use the starting size as the maximum size.");
            }

            m_dataPool = new UnityEngine.Pool.ObjectPool<RenderObjectData>(
               createFunc: () => new RenderObjectData(),
               actionOnGet: (data) => data.Reset(),
               collectionCheck: true
           );
            m_packingStrategy.Initialize(m_maxAtlasSize.x, m_maxAtlasSize.y);
            CreateAtlasTexture(m_startingSize.x, m_startingSize.y);

            m_isInitialized = true;
        }


        private void CreateAtlasTexture(int width, int height)
        {
            if (m_renderer == null)
            {
                m_renderer = RendererUtils.CreateRenderer();
            }

            if (m_atlasTexture != null)
            {
                if (m_atlasTexture.width == width && m_atlasTexture.height == height)
                {
                    return;
                }

                // Resize existing texture
                m_atlasTexture = ResizeRenderTexture(m_atlasTexture, width, height);
                if (!m_atlasTexture.IsCreated())
                {
                    m_atlasTexture.Create();
                }
            }
            else
            {
                // Create new texture if none exists
                m_atlasTexture = CreateRenderTexture(width, height);

                if (!m_atlasTexture.IsCreated())
                {
                    m_atlasTexture.Create();
                }
            }

            RenderPipelineHandler.SetRendererTexture(m_renderer, m_atlasTexture);
        }

        private void RedrawAtlas()
        {
            // Atlas uses a single render queue for multiple panels. If any panel requests AlwaysDraw,
            // we should disable dirt-checking and render every frame.
            bool shouldDisableArtboardDirtCheck = false;
            foreach (var panel in m_rivePanelData.Keys)
            {
                if (panel != null && panel.DrawOptimization == DrawOptimizationOptions.AlwaysDraw)
                {
                    shouldDisableArtboardDirtCheck = true;
                    break;
                }
            }
            m_renderer.SetArtboardDirtCheckEnabled(!shouldDisableArtboardDirtCheck);

            // Clear the render queue to avoid rendering leftover visuals from the previous render
            m_renderer.Clear();

            // Redraw each render object in its new position
            foreach (var panel in m_rivePanelData.Keys)
            {
                HandlePanelRendering(panel);
                TriggerRenderTargetUpdatedEvent(panel);
            }

        }

        /// <summary>
        /// In OpenGL, the texture coordinate origin (0, 0) is at the bottom-left corner.  In Direct3D (used by Windows) and Metal (used by macOS), the origin is typically at the top-left corner.
        /// </summary>
        /// <returns></returns>
        private bool ShouldFlipOffsetY()
        {
            return TextureHelper.IsOpenGLPlatform();
        }

        public override Vector2 GetPanelOffset(IRivePanel rivePanel)
        {
            if (m_atlasTexture == null)
            {
                return Vector2.zero;
            }

            if (m_rivePanelData.TryGetValue(rivePanel, out var data))
            {
                float xOffset = (float)data.Position.x / m_atlasTexture.width;
                float yOffset = (float)data.Position.y / m_atlasTexture.height;

                return new Vector2(xOffset, yOffset);
            }
            return Vector2.zero;
        }

        public override RenderTexture GetRenderTexture(IRivePanel rivePanel)
        {
            if (m_rivePanelData.TryGetValue(rivePanel, out var data))
            {
                return m_atlasTexture;
            }

            return null;
        }

        public override Vector2 GetPanelScale(IRivePanel rivePanel)
        {
            if (m_atlasTexture == null)
            {
                return Vector2.one;
            }

            if (m_rivePanelData.TryGetValue(rivePanel, out var data))
            {
                return new Vector2((float)data.Position.width / m_atlasTexture.width, (float)data.Position.height / m_atlasTexture.height);
            }
            return Vector2.one;
        }


        private void HandlePanelRendering(IRivePanel panel)
        {
            if (m_rivePanelData.TryGetValue(panel, out var data))
            {
                var rect = data.Position;
                // Save the current render queue state
                m_renderer.Save();



                // Calculate the scale to simulate full render texture size
                Vector2Int fullSize = new Vector2Int((int)panel.WidgetContainer.rect.size.x, (int)panel.WidgetContainer.rect.size.y);
                float scaleX = (float)rect.width / fullSize.x;
                float scaleY = (float)rect.height / fullSize.y;
                float scale = Mathf.Min(scaleX, scaleY);



                // Only apply clipping if needed
                // If there's only one panel in the atlas, we don't need to clip as the visuals overflowing won't affect other panels
                bool needsClipping = m_rivePanelData.Count > 1 && DoesPanelNeedClipping(panel);


                // Translate to the correct starting position to match the reference rect's position in the atlas
                if (TextureHelper.ShouldFlipTexture())
                {
                    var translateMatrix = System.Numerics.Matrix3x2.CreateTranslation(rect.x, rect.y);
                    m_renderer.Transform(translateMatrix);

                }
                else
                {
                    // We need to do this for OpenGL as it uses a different coordinate system, but it isn't needed for Direct3D or Metal platforms
                    // If we don't do this, the graphic shows up at the wrong position on the y-axis
                    m_renderer.Transform(System.Numerics.Matrix3x2.CreateTranslation(rect.x, m_atlasTexture.height - rect.y - rect.height));
                }

                // Do the clipping after the translation so the clipping path is in the correct position
                if (needsClipping)
                {
                    if (m_clipPath == null)
                    {
                        m_clipPath = new Path();
                    }
                    ClippingPathHelper.ConfigureClippingPath(m_clipPath, rect.width, rect.height);
                    m_renderer.Clip(m_clipPath);
                }


                // Apply the scale to simulate the full render texture size
                // This is necessary to ensure the fit & alignment calculations are correct and rendered the way they would be if the artboard were to fill the render texture. Especially important for ScaleDown fit mode.
                m_renderer.Transform(System.Numerics.Matrix3x2.CreateScale(scale));

                // Render the artboard using the full size for alignment calculation
                var targetInfo = new RenderTargetInfo(
                            new Vector2Int(m_atlasTexture.width, m_atlasTexture.height),
                            new Vector2Int(rect.width, rect.height)
                        );



                DrawPanelWithRenderer(m_renderer, panel, targetInfo, TargetSpaceOccupancy);



                // Restore the render queue state
                m_renderer.Restore();
            }
        }


        public override void DrawPanel(IRivePanel panel)
        {
            if (!IsPanelRegistered(panel))
            {
                return;
            }
            // Check if panel size has changed to determine if we need to repack
            Vector2Int currentSize = new Vector2Int((int)panel.WidgetContainer.rect.size.x, (int)panel.WidgetContainer.rect.size.y);
            var data = m_rivePanelData[panel];

            if (data.LastKnownSize != currentSize)
            {
                // Size changed, need to repack
                data.LastKnownSize = currentSize;
                RepackAndRedrawIfNeeded();
            }
            else
            {
                // Size hasn't changed, just redraw the panels
                RedrawIfNeeded();
            }
        }



        public override bool IsPanelRegistered(IRivePanel panel)
        {
            return m_rivePanelData.ContainsKey(panel);
        }

        public override bool RegisterPanel(IRivePanel panel)
        {
            if (IsDestroyed)
            {
                return false;
            }

            InitializeIfNeeded();

            if (!IsPanelRegistered(panel))
            {
                // Check if it can fit
                Vector2Int scaledDimensions;
                if (!CanFitRenderObject(panel, out scaledDimensions))
                {
                    DebugLogger.Instance.LogWarning($"Panel cannot fit in the atlas.");
                    return false;
                }

                var data = m_dataPool.Get();
                data.LastKnownSize = new Vector2Int((int)panel.WidgetContainer.rect.size.x, (int)panel.WidgetContainer.rect.size.y);
                m_rivePanelData[panel] = data;


                if (!m_isRendererRegistered)
                {
                    RegisterRenderer(m_renderer);
                    m_isRendererRegistered = true;
                }
                RepackAndRedrawIfNeeded();



                return true;

            }

            return false;
        }

        public override bool UnregisterPanel(IRivePanel panel)
        {
            if (m_rivePanelData.Remove(panel, out var data))
            {
                m_dataPool.Release(data);
                RepackAndRedrawIfNeeded();
            }

            if (m_rivePanelData.Count == 0)
            {
                UnregisterRenderer(m_renderer);
                m_isRendererRegistered = false;
            }

            return true;
        }

        private Vector2Int GetOrCalculateScaledDimensions(IRivePanel rivePanel, bool forceRecalculate = false)
        {
            RenderObjectData data = null;
            bool hasData = m_rivePanelData.TryGetValue(rivePanel, out data);

            if (!hasData || forceRecalculate || data.ScaledDimensions == default)
            {

                Vector2Int dimensions = new Vector2Int((int)rivePanel.WidgetContainer.rect.size.x, (int)rivePanel.WidgetContainer.rect.size.y);
                int originalWidth = dimensions.x;
                int originalHeight = dimensions.y;

                float scale = 1f;
                if (originalWidth > m_maxResolutionPerPanel || originalHeight > m_maxResolutionPerPanel)
                {
                    scale = (float)m_maxResolutionPerPanel / Mathf.Max(originalWidth, originalHeight);
                }

                Vector2Int scaledDimensions = new Vector2Int(
                    Mathf.RoundToInt(originalWidth * scale),
                    Mathf.RoundToInt(originalHeight * scale)
                );

                if (hasData)
                {
                    data.ScaledDimensions = scaledDimensions;
                }

                return scaledDimensions;
            }

            return data.ScaledDimensions;
        }

        private bool CanFitRenderObject(IRivePanel rivePanel, out Vector2Int scaledDimensions, bool forceRecalculate = false)
        {
            scaledDimensions = GetOrCalculateScaledDimensions(rivePanel);

            int maxAtlasWidth = m_maxAtlasSize.x;
            int maxAtlasHeight = m_maxAtlasSize.y;

            // Check if the object is larger than the maximum allowed dimensions
            if (scaledDimensions.x + m_padding * 2 > maxAtlasWidth || scaledDimensions.y + m_padding * 2 > maxAtlasHeight)
            {
                // Cannot fit into the atlas even at maximum size
                return false;
            }

            // Simulate packing with current placements into an atlas of maximum allowed size
            m_packingStrategy.Initialize(maxAtlasWidth, maxAtlasHeight);

            // Place existing render objects
            foreach (var existingRenderObject in m_rivePanelData.Keys)
            {
                Vector2Int existingScaledDimensions = GetOrCalculateScaledDimensions(existingRenderObject, forceRecalculate);

                int existingRectWidth = existingScaledDimensions.x + m_padding * 2;
                int existingRectHeight = existingScaledDimensions.y + m_padding * 2;

                RectInt existingRect;
                if (!m_packingStrategy.TryInsert(existingRectWidth, existingRectHeight, out existingRect))
                {
                    // Failed to place existing object
                    return false;
                }


            }

            // Try to place the new render object
            int rectWidth = scaledDimensions.x + m_padding * 2;
            int rectHeight = scaledDimensions.y + m_padding * 2;

            RectInt rect;
            if (!m_packingStrategy.TryInsert(rectWidth, rectHeight, out rect))
            {
                // Cannot fit the new render object
                return false;
            }


            return true;
        }

        private void RepackAndRedrawIfNeeded()
        {
            if (DrawTiming == DrawTimingOption.DrawImmediate)
            {
                Repack();
                return;
            }


            m_batchCommand = BatchCommandOption.RepackAndRedraw;

        }


        private void RedrawIfNeeded()
        {
            if (DrawTiming == DrawTimingOption.DrawImmediate)
            {
                RedrawAtlas();
                return;
            }

            // Handle DrawTimingOption.DrawBatched

            // a RepackAndRedraw command has a higher priority than a Redraw command, so we only set the m_batchCommandOption if it's not already set to avoid it getting overwritten
            if (m_batchCommand == BatchCommandOption.None)
            {
                m_batchCommand = BatchCommandOption.Redraw;
            }

        }

        internal protected override void PrepareBatchedRender()
        {
            if (m_batchCommand == BatchCommandOption.None)
            {
                return;
            }

            // We wait till LateUpdate (via Orchestrator) to redraw to ensure all render objects have been updated.
            // We also do this because the native rive code might not be ready on the initial frame until LateUpdate.

            if (m_batchCommand == BatchCommandOption.RepackAndRedraw)
            {
                Repack();
            }
            else if (m_batchCommand == BatchCommandOption.Redraw)
            {
                RedrawAtlas();

            }

            m_batchCommand = BatchCommandOption.None;
        }



        // We sort by height descending
        System.Comparison<IRivePanel> comparison = (a, b) => b.WidgetContainer.rect.size.y.CompareTo(a.WidgetContainer.rect.size.y);

        private void Repack()
        {
            int currentWidth = m_startingSize.x;
            int currentHeight = m_startingSize.y;

            m_renderObjectKeys.Clear();
            m_renderObjectKeys.AddRange(m_rivePanelData.Keys);
            m_renderObjectKeys.Sort(comparison);

            bool packed = false;

            while (!packed)
            {
                packed = PackRects(currentWidth, currentHeight, m_renderObjectKeys);

                if (packed)
                    break;

                int maxAtlasWidth = m_maxAtlasSize.x;
                int maxAtlasHeight = m_maxAtlasSize.y;

                // Decide how to grow the atlas
                bool canIncreaseWidth = currentWidth < maxAtlasWidth;
                bool canIncreaseHeight = currentHeight < maxAtlasHeight;

                if (!canIncreaseWidth && !canIncreaseHeight)
                {
                    DebugLogger.Instance.LogWarning($"Atlas size cannot be increased further. Maximum dimensions reached: {maxAtlasWidth}x{maxAtlasHeight}.");
                    break; // Cannot grow further
                }

                // Increase the smaller dimension first
                if (canIncreaseWidth && (currentWidth <= currentHeight || !canIncreaseHeight))
                {
                    currentWidth = Mathf.Min(currentWidth * 2, maxAtlasWidth);
                }
                else if (canIncreaseHeight)
                {
                    currentHeight = Mathf.Min(currentHeight * 2, maxAtlasHeight);
                }
            }

            if (!packed)
            {
                DebugLogger.Instance.LogWarning("Failed to pack all render objects into the atlas.");
                return;
            }

            if (m_atlasTexture == null || currentWidth != m_atlasTexture.width || currentHeight != m_atlasTexture.height)
            {
                CreateAtlasTexture(currentWidth, currentHeight);
            }

            RedrawAtlas();

        }


        private bool PackRects(int width, int height, List<IRivePanel> panelsToPack)
        {
            m_packingStrategy.Initialize(width, height);

            foreach (var renderObject in panelsToPack)
            {
                Vector2Int scaledDimensions = GetOrCalculateScaledDimensions(renderObject, true);


                int rectWidth = scaledDimensions.x + m_padding * 2;
                int rectHeight = scaledDimensions.y + m_padding * 2;

                RectInt rect;
                if (!m_packingStrategy.TryInsert(rectWidth, rectHeight, out rect))
                {
                    // Failed to place the rectangle
                    return false;
                }

                // Store the position with padding included, but dimensions without padding
                m_rivePanelData[renderObject].Position = new RectInt(rect.x + m_padding, rect.y + m_padding, scaledDimensions.x, scaledDimensions.y);
            }

            return true;
        }



        private void DestroyAndReleaseTexture()
        {
            if (m_atlasTexture != null)
            {
                ReleaseRenderTexture(m_atlasTexture);
                Destroy(m_atlasTexture);
                m_atlasTexture = null;
            }
        }


        /// <summary>
        /// Configures the atlas settings. Only works before the strategy is initialized e.g. before any panels are registered.
        /// </summary>
        /// <param name="startingSize">The initial dimensions of the atlas texture</param>
        /// <param name="maxAtlasSize">The maximum dimensions the atlas can grow to</param>
        /// <param name="maxResolutionPerPanel">Maximum resolution for a single panel</param>
        /// <param name="padding">Padding between panels</param>
        /// <returns>True if settings were applied, false if strategy was already initialized</returns>
        public bool Configure(Vector2Int startingSize, Vector2Int maxAtlasSize, int maxResolutionPerPanel = 512, int padding = 0)
        {
            if (m_isInitialized)
            {
                DebugLogger.Instance.LogWarning("Cannot configure AtlasRenderTargetStrategy after initialization");
                return false;
            }

            if (startingSize.x <= 0 || startingSize.y <= 0)
            {
                DebugLogger.Instance.LogWarning("Starting size must be greater than 0.");
                return false;
            }

            if (maxAtlasSize.x <= 0 || maxAtlasSize.y <= 0)
            {
                DebugLogger.Instance.LogWarning("Max atlas size must be greater than 0.");
                return false;
            }

            if (maxResolutionPerPanel <= 0)
            {
                DebugLogger.Instance.LogWarning("Max resolution per panel must be greater than 0.");
                return false;
            }

            if (padding < 0)
            {
                DebugLogger.Instance.LogWarning("Padding must be greater than or equal to 0.");
                return false;
            }

            m_startingSize = startingSize;
            m_maxAtlasSize = maxAtlasSize;
            m_maxResolutionPerPanel = maxResolutionPerPanel;
            m_padding = padding;

            return true;
        }

        private void Cleanup()
        {
            m_renderObjectKeys.Clear();
            m_renderObjectKeys.AddRange(m_rivePanelData.Keys);

            for (int i = m_rivePanelData.Count - 1; i >= 0; i--)
            {
                var panel = m_renderObjectKeys[i];
                if (panel != null)
                {
                    UnregisterPanel(panel);
                }
            }

            if (m_dataPool != null)
            {
                m_dataPool.Clear();
            }

            DestroyAndReleaseTexture();

            if (m_renderer != null)
            {
                RendererUtils.ReleaseRenderer(m_renderer);
                m_renderer = null;
            }
        }


        protected override void OnDestroy()
        {
            Cleanup();
            base.OnDestroy();

        }

        protected override IEnumerable<Renderer> GetRenderers()
        {
            if (m_renderer != null)
            {
                yield return m_renderer;
            }
        }


    }


    /// <summary>
    // A shelf-based packing strategy that places rectangles side-by-side in horizontal shelves. Each shelf tracks its used width and stacks vertically in the atlas. When a rectangle doesn't fit in existing shelves, a new shelf is created below.
    /// </summary>
    internal class ShelfPackingStrategy : RenderTargetAtlasPackingProvider.IPackingStrategy
    {
        private int m_width;
        private int m_height;
        private List<Shelf> m_shelves;

        private struct Shelf
        {
            public int Y { get; private set; }
            public int Height { get; private set; }
            public int UsedWidth { get; private set; }

            public Shelf(int y, int height)
            {
                Y = y;
                Height = height;
                UsedWidth = 0;
            }

            public bool TryFit(int width, int height, int shelfWidth, out int fitX)
            {
                if (height > Height || UsedWidth + width > shelfWidth)
                {
                    fitX = 0;
                    return false;
                }

                fitX = UsedWidth;
                return true;
            }

            public void AddBlock(int width)
            {
                UsedWidth += width;
            }
        }

        public void Initialize(int width, int height)
        {
            m_width = width;
            m_height = height;

            if (m_shelves == null)
            {
                m_shelves = new List<Shelf>();
            }
            else
            {
                m_shelves.Clear();
            }
        }

        public bool TryInsert(int requestedWidth, int requestedHeight, out RectInt placementRect)
        {
            // First try to place in an existing shelf
            for (int i = 0; i < m_shelves.Count; i++)
            {
                if (m_shelves[i].TryFit(requestedWidth, requestedHeight, m_width, out int fitX))
                {
                    placementRect = new RectInt(fitX, m_shelves[i].Y, requestedWidth, requestedHeight);
                    var updatedShelf = m_shelves[i];
                    updatedShelf.AddBlock(requestedWidth);
                    m_shelves[i] = updatedShelf;
                    return true;
                }
            }

            // If no existing shelf fits, try to create a new shelf
            bool hasExistingShelves = m_shelves.Count > 0;
            int newShelfY;
            if (hasExistingShelves)
            {
                Shelf lastShelf = m_shelves[m_shelves.Count - 1];
                newShelfY = lastShelf.Y + lastShelf.Height;  // Stack new shelf below the last one
            }
            else
            {
                newShelfY = 0;  // No shelves yet, start at the top
            }

            bool hasRoomForNewShelf = newShelfY + requestedHeight <= m_height;
            if (hasRoomForNewShelf)
            {
                var newShelf = new Shelf(newShelfY, requestedHeight);
                newShelf.AddBlock(requestedWidth);
                m_shelves.Add(newShelf);
                placementRect = new RectInt(0, newShelfY, requestedWidth, requestedHeight);
                return true;
            }

            // No room found in existing or new shelves
            placementRect = new RectInt(0, 0, 0, 0);
            return false;
        }
    }


}