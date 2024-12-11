using Rive.Utils;
using UnityEngine;

namespace Rive.Components
{
    /// <summary>
    /// Renders a ProceduralDrawing within a RivePanel.
    /// </summary>
    [AddComponentMenu("Rive/Procedural Rive Widget")]
    public sealed class ProceduralRiveWidget : WidgetBehaviour
    {
        [SerializeField] private ProceduralDrawing m_proceduralDrawing;

        [Tooltip("The hit test behavior for the widget.")]
        [SerializeField] private HitTestBehavior m_hitTestBehavior = HitTestBehavior.None;

        private ProceduralRenderObject m_renderObject;


        public override IRenderObject RenderObject => m_renderObject;

        public ProceduralDrawing ProceduralDrawing => m_proceduralDrawing;

        public override HitTestBehavior HitTestBehavior { get => m_hitTestBehavior; set => m_hitTestBehavior = value; }

        void Start()
        {
            if (m_proceduralDrawing == null)
            {
                return;
            }

            Load(m_proceduralDrawing);
        }

        public override bool Tick(float deltaTime)
        {
            bool needsRedraw = base.Tick(deltaTime);
            if (m_proceduralDrawing == null)
            {
                return needsRedraw;
            }
            if (m_proceduralDrawing.Advance(deltaTime))
            {
                needsRedraw = true;
            }

            return needsRedraw;
        }

        public void Load(ProceduralDrawing proceduralDrawing)
        {
            if (proceduralDrawing == null)
            {
                DebugLogger.Instance.LogError("Can't load null procedural drawing.");
                return;
            }

            Status = WidgetStatus.Loading;

            m_proceduralDrawing = proceduralDrawing;


            try
            {
                m_renderObject = new ProceduralRenderObject(m_proceduralDrawing);
            }
            catch (System.Exception e)
            {
                DebugLogger.Instance.LogError($"Error loading procedural drawing: {e.Message}");
                Status = WidgetStatus.Error;
                return;
            }

            HandleLoadComplete();

        }

        public override bool HitTest(Vector2 normalizedPointInRect)
        {
            if (m_proceduralDrawing == null)
            {
                return base.HitTest(normalizedPointInRect);
            }

            return m_proceduralDrawing.HitTest(normalizedPointInRect, RectTransform.rect);
        }

        public override bool OnPointerDown(Vector2 normalizedPointInRect)
        {
            base.OnPointerDown(normalizedPointInRect);

            if (m_proceduralDrawing == null)
            {
                return false;
            }

            return m_proceduralDrawing.HandlePointerDown(normalizedPointInRect, RectTransform.rect);
        }

        public override bool OnPointerMove(Vector2 normalizedPointInRect)
        {
            base.OnPointerMove(normalizedPointInRect);

            if (m_proceduralDrawing == null)
            {
                return false;
            }

            return m_proceduralDrawing.HandlePointerMove(normalizedPointInRect, RectTransform.rect);
        }

        public override bool OnPointerUp(Vector2 normalizedPointInRect)
        {
            base.OnPointerUp(normalizedPointInRect);

            if (m_proceduralDrawing == null)
            {
                return false;
            }

            return m_proceduralDrawing.HandlePointerUp(normalizedPointInRect, RectTransform.rect);
        }

    }
}
