using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Rive.Components;
using Rive.Tests.Utils;
using Rive.Utils;
using System.Collections.Generic;
using UnityEngine.TestTools;
using System.Collections;
using UnityEngine.EventSystems;
using System.Linq;

namespace Rive.Tests
{
    public class RiveCanvasRendererTests
    {
        private GameObject m_gameObject;
        private RiveCanvasRenderer m_renderer;
        private MockLogger m_mockLogger;
        private Canvas m_canvas;
        private MockRivePanelComponent m_mockPanel;

        private RivePanel m_realPanel;
        private GameObject m_eventSystemObject;
        private TestInputModule m_inputModule;


        private void Setup(bool spawnEventSystem = true, bool spawnGraphicsRaycaster = true)
        {
            m_mockLogger = new MockLogger();
            DebugLogger.Instance = m_mockLogger;

            // Create canvas for UI components
            var canvasObj = new GameObject("Canvas");
            m_canvas = canvasObj.AddComponent<Canvas>();
            if (spawnGraphicsRaycaster)
            {
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // Create EventSystem if needed
            if (spawnEventSystem)
            {
                m_eventSystemObject = new GameObject("EventSystem");
                m_eventSystemObject.AddComponent<EventSystem>();
            }

            // Create renderer GameObject with required components
            m_gameObject = new GameObject("TestRenderer");
            m_gameObject.transform.SetParent(m_canvas.transform);

            // Add mock panel component first
            m_mockPanel = m_gameObject.AddComponent<MockRivePanelComponent>();
            m_renderer = m_gameObject.AddComponent<RiveCanvasRenderer>();


        }

        private void SetupWithRealPanel(bool spawnEventSystem = true, bool spawnGraphicsRaycaster = true)
        {
            m_mockLogger = new MockLogger();
            DebugLogger.Instance = m_mockLogger;

            // Create canvas for UI components
            var canvasObj = new GameObject("Canvas");
            m_canvas = canvasObj.AddComponent<Canvas>();
            if (spawnGraphicsRaycaster)
            {
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // Create EventSystem if needed
            if (spawnEventSystem)
            {
                m_eventSystemObject = new GameObject("EventSystem");
                m_eventSystemObject.AddComponent<EventSystem>();

                // Destroy any existing input module
                var existingInputModule = m_eventSystemObject.GetComponent<TestInputModule>();

                if (existingInputModule != null)
                {
                    DestroyObj(existingInputModule);
                }

                m_inputModule = m_eventSystemObject.AddComponent<TestInputModule>();
            }


            // Add mock panel component first
            m_realPanel = RivePanelTestUtils.CreatePanel("RivePanel");
            m_realPanel.transform.SetParent(m_canvas.transform);

            m_renderer = m_realPanel.gameObject.AddComponent<RiveCanvasRenderer>();


        }

        [TearDown]
        public void TearDown()
        {
            if (m_eventSystemObject != null)
            {
                DestroyObj(m_eventSystemObject);
            }

            if (m_canvas != null)
            {
                DestroyObj(m_canvas.gameObject);
            }
            DestroyObj(m_gameObject);

            if (m_realPanel != null)
            {
                DestroyObj(m_realPanel.gameObject);
            }

            if (m_inputModule != null)
            {
                DestroyObj(m_inputModule.gameObject);
            }
        }

        private void DestroyObj(UnityEngine.Object obj)
        {
            if (obj != null)
            {
                UnityEngine.Object.Destroy(obj);

            }
        }

        [Test]
        public void OnEnable_InitializesComponents()
        {
            Setup();

            m_renderer.gameObject.SetActive(false);
            m_renderer.gameObject.SetActive(true);



            Assert.IsNotNull(m_renderer.DisplayImage, "DisplayImage should be initialized");

            // If EnablePointerInput is set, then raycastTarget should be true
            if (m_renderer.PointerInputMode == PointerInputMode.EnablePointerInput)
            {
                Assert.IsTrue(m_renderer.DisplayImage.raycastTarget, "RawImage should be raycast target by default");
            }
            else
            {
                Assert.IsFalse(m_renderer.DisplayImage.raycastTarget, "RawImage should not be raycast target by default");
            }
        }

        [UnityTest]
        public IEnumerator UpdateVisualTarget_UpdatesRawImageProperties()
        {
            Setup();

            // Setup mock render texture
            var renderTexture = new RenderTexture(100, 100, 0);
            m_mockPanel.SetRenderTexture(renderTexture);

            // Set mock panel properties
            m_mockPanel.SetOffsetInRenderTexture(new Vector2(0.1f, 0.2f));
            m_mockPanel.SetScaleInRenderTexture(new Vector2(0.8f, 0.9f));

            m_mockPanel.TriggerRenderTargetUpdated();

            yield return null;

            // Verify RawImage properties
            Assert.IsTrue(ReferenceEquals(renderTexture, m_renderer.DisplayImage.texture), "Texture should be updated");
            Assert.AreEqual(
                new Rect(m_mockPanel.OffsetInRenderTexture, m_mockPanel.ScaleInRenderTexture),
                m_renderer.DisplayImage.uvRect,
                "UV rect should match panel offset and scale"
            );

            renderTexture.Release();
            DestroyObj(renderTexture);
        }

        [Test]
        public void PointerInputMode_DisableInput_UpdatesRaycastTarget()
        {
            Setup();

            Assert.IsTrue(m_renderer.DisplayImage.raycastTarget, "RaycastTarget should be enabled by default");

            m_renderer.PointerInputMode = PointerInputMode.DisablePointerInput;
            Assert.IsFalse(m_renderer.DisplayImage.raycastTarget, "RaycastTarget should be disabled");

            m_renderer.PointerInputMode = PointerInputMode.EnablePointerInput;
            Assert.IsTrue(m_renderer.DisplayImage.raycastTarget, "RaycastTarget should be re-enabled");
        }

        [UnityTest]
        public IEnumerator Start_WithoutEventSystem_LogsWarning()
        {
            Setup(spawnEventSystem: false);

            yield return null;
            // Find an event system in the scene 
            var eventSystem = UnityEngine.Object.FindObjectOfType<EventSystem>();

            Assert.IsNull(eventSystem, "EventSystem should not be present in the scene");
            Assert.IsNull(EventSystem.current, "EventSystem.current should be null");

            m_renderer.PointerInputMode = PointerInputMode.EnablePointerInput;
            Assert.IsTrue(m_mockLogger.LoggedWarnings.Count > 0);

            Assert.IsTrue(m_mockLogger.LoggedWarnings[0].Contains(nameof(EventSystem)), "Warning should mention EventSystem");
        }

        [UnityTest]
        public IEnumerator Start_WithoutGraphicRaycaster_LogsWarning()
        {

            Setup(spawnGraphicsRaycaster: false);


            m_renderer.PointerInputMode = PointerInputMode.EnablePointerInput;
            yield return null;

            Assert.IsTrue(m_mockLogger.LoggedWarnings.Count > 0);
            Assert.IsTrue(m_mockLogger.LoggedWarnings[0].Contains(nameof(GraphicRaycaster)), "Warning should mention GraphicRaycaster");
        }

        [UnityTest]
        public IEnumerator Start_WithEventSystemAndGraphicsRaycaster_NoWarnings()
        {
            Setup(spawnEventSystem: true, spawnGraphicsRaycaster: true);
            m_renderer.PointerInputMode = PointerInputMode.EnablePointerInput;
            yield return null;

            Assert.AreEqual(0, m_mockLogger.LoggedWarnings.Count,
                "No warnings should be logged when all required components are present");
        }

        [UnityTest]
        public IEnumerator Start_WithoutEventSystemAndGraphicsRaycaster_LogsNoWarnings_IfPointerInputDisabled()
        {
            Setup(spawnEventSystem: false, spawnGraphicsRaycaster: false);
            m_renderer.PointerInputMode = PointerInputMode.DisablePointerInput;
            yield return null;

            Assert.AreEqual(0, m_mockLogger.LoggedWarnings.Count,
                "No warnings should be logged when pointer input is disabled");
        }

        [UnityTest]
        public IEnumerator OnEnable_InitializesInputProvider()
        {
            Setup();
            yield return null;

            var inputProvider = m_renderer.GetComponent<IPanelInputProvider>();
            Assert.IsNotNull(m_renderer, "Input provider should be initialized");
            Assert.IsTrue(m_mockPanel.InputProviders.Contains(inputProvider), "Panel should use the initialized input provider");
        }

        [UnityTest]
        public IEnumerator Destroying_CanvasRenderer_UnregistersInputProvider()
        {
            Setup();
            yield return null;

            var inputProvider = m_renderer.GetComponent<IPanelInputProvider>();
            Assert.IsTrue(m_mockPanel.InputProviders.Contains(inputProvider), "Panel should use the initialized input provider");

            DestroyObj(m_renderer);
            yield return null;

            Assert.IsFalse(m_mockPanel.InputProviders.Contains(inputProvider), "Panel should not use the destroyed input provider");
            Assert.AreEqual(0, m_mockPanel.InputProviders.Count, "Panel should not have any input providers");
        }


        [UnityTest]
        public IEnumerator EventSystem_PointerEvents_PropagateToPanel()
        {
            SetupWithRealPanel();
            m_realPanel.SetDimensions(new Vector2(1920, 1080));

            var mockWidget = RivePanelTestUtils.CreateWidget<MockRiveWidget>();
            m_realPanel.AddToHierarchy(mockWidget);

            RivePanelTestUtils.MakeWidgetFillPanel(mockWidget);

            m_renderer.PointerInputMode = PointerInputMode.EnablePointerInput;



            yield return null;


            Vector2 pointerPosition = m_renderer.RectTransform.position + new Vector3(50, 50, 0); // Center of the panel

            // Simulate pointer down
            m_inputModule.PointerDownAt(pointerPosition);
            yield return null;
            Assert.AreEqual(1, mockWidget.PointerDownCalledCount, "PointerDown should be called once");

            // Simulate pointer move
            pointerPosition += new Vector2(10, 10);
            m_inputModule.PointerMoveAt(pointerPosition);

            yield return null;
            Assert.Greater(mockWidget.PointerMoveCalledCount, 0, "PointerMove should be called at least once");

            // Simulate pointer up
            m_inputModule.PointerUpAt(pointerPosition);
            yield return null;
            Assert.AreEqual(1, mockWidget.PointerUpCalledCount, "PointerUp should be called once");
        }

        [UnityTest]
        public IEnumerator EventSystem_PointerEvents_DisabledInput_NoEvents()
        {
            SetupWithRealPanel();
            m_realPanel.SetDimensions(new Vector2(1920, 1080));

            var mockWidget = RivePanelTestUtils.CreateWidget<MockRiveWidget>();
            m_realPanel.AddToHierarchy(mockWidget);
            RivePanelTestUtils.MakeWidgetFillPanel(mockWidget);

            // Disable pointer input
            m_renderer.PointerInputMode = PointerInputMode.DisablePointerInput;

            yield return null;


            Vector2 pointerPosition = m_renderer.RectTransform.position + new Vector3(50, 50, 0); // Center of the panel

            m_inputModule.PointerDownAt(pointerPosition);
            m_inputModule.PointerMoveAt(pointerPosition);
            m_inputModule.PointerUpAt(pointerPosition);

            yield return null;

            Assert.AreEqual(0, mockWidget.PointerDownCalledCount, "PointerDown should not be called when input is disabled");
            Assert.AreEqual(0, mockWidget.PointerMoveCalledCount, "PointerMove should not be called when input is disabled");
            Assert.AreEqual(0, mockWidget.PointerUpCalledCount, "PointerUp should not be called when input is disabled");
        }

        [UnityTest]
        public IEnumerator EventSystem_PointerEvents_OutsideBounds_NoEvents()
        {
            SetupWithRealPanel();

            var mockWidget = RivePanelTestUtils.CreateWidget<MockRiveWidget>();
            m_realPanel.AddToHierarchy(mockWidget);
            RivePanelTestUtils.MakeWidgetFillPanel(mockWidget);

            m_realPanel.SetDimensions(new Vector2(100, 100));


            yield return null;

            Assert.AreEqual(0, mockWidget.PointerDownCalledCount, "PointerDown count should be 0 initially");
            Assert.AreEqual(0, mockWidget.PointerMoveCalledCount, "PointerMove count should be 0 initially");
            Assert.AreEqual(0, mockWidget.PointerUpCalledCount, "PointerUp count should be 0 initially");

            Vector2 pointerPosition = m_renderer.RectTransform.position + new Vector3(1200, 1200, 0); // Well outside bounds

            m_inputModule.PointerDownAt(pointerPosition);
            m_inputModule.PointerUpAt(pointerPosition);

            yield return null;

            Assert.AreEqual(0, mockWidget.PointerDownCalledCount, "PointerDown should not be called for events outside bounds");
            Assert.AreEqual(0, mockWidget.PointerUpCalledCount, "PointerUp should not be called for events outside bounds");

        }

        [Test]
        public void CustomMaterial_SetAndGet_UpdatesDisplayImage()
        {
            Setup();

            var customMaterial = new Material(Shader.Find("UI/Default"));
            m_renderer.CustomMaterial = customMaterial;

            Assert.AreEqual(customMaterial, m_renderer.CustomMaterial, "CustomMaterial getter should return set material");
            Assert.AreEqual(customMaterial, m_renderer.DisplayImage.material, "DisplayImage material should be updated");

            DestroyObj(customMaterial);
        }

    }

    public class MockRivePanelComponent : MonoBehaviour, IRivePanel
    {
        private bool m_isRendering;
        private bool m_enabled = true;
        private readonly List<IRiveWidget> m_widgets = new List<IRiveWidget>();
        private IPanelInputProvider m_inputProvider;
        private RenderTexture m_renderTexture;
        private Vector2 m_scaleInRenderTexture = Vector2.one;
        private Vector2 m_offsetInRenderTexture = Vector2.zero;
        private RectTransform m_rectTransform;

        private List<IPanelInputProvider> m_inputProviders = new List<IPanelInputProvider>();


        public IReadOnlyList<IPanelInputProvider> InputProviders => m_inputProviders;

        public event Action<IRiveWidget> OnWidgetAdded;
        public event Action<IRiveWidget> OnWidgetRemoved;
        public event Action OnRenderingStateChanged;
        public event Action OnRenderTargetStrategyChanged;
        public event Action OnRenderTargetUpdated;

        private void Awake()
        {
            m_rectTransform = GetComponent<RectTransform>();
            if (m_rectTransform == null)
            {
                m_rectTransform = gameObject.AddComponent<RectTransform>();
            }
        }

        public RectTransform WidgetContainer => m_rectTransform;
        public IReadOnlyList<IRiveWidget> Widgets => m_widgets;
        public bool IsRendering => m_isRendering;
        public bool Enabled => m_enabled && (this != null && this.gameObject.activeInHierarchy);

        public RenderTexture RenderTexture => m_renderTexture;
        public Vector2 ScaleInRenderTexture => m_scaleInRenderTexture;
        public Vector2 OffsetInRenderTexture => m_offsetInRenderTexture;

        public DrawOptimizationOptions DrawOptimization => DrawOptimizationOptions.AlwaysDraw;

        public void SetRenderTexture(RenderTexture texture) => m_renderTexture = texture;
        public void SetScaleInRenderTexture(Vector2 scale) => m_scaleInRenderTexture = scale;
        public void SetOffsetInRenderTexture(Vector2 offset) => m_offsetInRenderTexture = offset;

        public bool StartRendering()
        {
            if (m_isRendering) return false;
            m_isRendering = true;
            OnRenderingStateChanged?.Invoke();
            return true;
        }

        public bool StopRendering()
        {
            if (!m_isRendering) return false;
            m_isRendering = false;
            OnRenderingStateChanged?.Invoke();
            return true;
        }

        public void TriggerRenderingStateChanged() => OnRenderingStateChanged?.Invoke();
        public void TriggerRenderTargetUpdated() => OnRenderTargetUpdated?.Invoke();
        public void TriggerRenderTargetStrategyChanged() => OnRenderTargetStrategyChanged?.Invoke();

        public void AddWidget(IRiveWidget widget)
        {
            m_widgets.Add(widget);
            OnWidgetAdded?.Invoke(widget);
        }

        public void RemoveWidget(IRiveWidget widget)
        {
            if (m_widgets.Remove(widget))
            {
                OnWidgetRemoved?.Invoke(widget);
            }
        }

        public void RegisterInputProvider(IPanelInputProvider inputProvider)
        {
            m_inputProviders.Add(inputProvider);
        }

        public void UnregisterInputProvider(IPanelInputProvider inputProvider)
        {
            m_inputProviders.Remove(inputProvider);
        }
    }
}