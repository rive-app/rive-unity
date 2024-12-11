using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Rive.Components;
using Rive.Tests.Utils;
using Rive.Utils;
using UnityEngine.EventSystems;
using System;

namespace Rive.Tests
{
    public class RiveTextureRendererTests
    {
        private GameObject m_rendererObject;
        private GameObject m_panelObject;
        private RiveTextureRenderer m_panelRenderer;
        private MockLogger m_mockLogger;
        private MockRivePanelComponent m_mockPanel;

        private RivePanel m_realPanel;

        private MeshRenderer m_meshRenderer;
        private GameObject m_eventSystemObject;
        private PhysicsRaycaster m_raycaster;
        private TestInputModule m_inputModule;

        private void Setup(bool spawnEventSystem = true, Type customcolliderType = null)
        {
            m_mockLogger = new MockLogger();
            DebugLogger.Instance = m_mockLogger;

            // Create EventSystem if needed
            if (spawnEventSystem)
            {
                m_eventSystemObject = new GameObject("EventSystem");
                m_eventSystemObject.AddComponent<EventSystem>();
            }

            // Create panel GameObject
            m_panelObject = new GameObject("TestPanel");
            m_mockPanel = m_panelObject.AddComponent<MockRivePanelComponent>();

            // Create renderer GameObject with required components
            m_rendererObject = GameObject.CreatePrimitive(PrimitiveType.Cube);

            if (customcolliderType != null)
            {
                // Destroy default collider if custom collider is specified
                DestroyObj(m_rendererObject.GetComponent<Collider>());

                m_rendererObject.AddComponent(customcolliderType);
            }
            m_meshRenderer = m_rendererObject.GetComponent<MeshRenderer>();
            m_panelRenderer = m_rendererObject.AddComponent<RiveTextureRenderer>();



            m_panelRenderer.SetPanel(m_mockPanel);
        }

        private void SetupWithRealPanel(bool spawnEventSystem = true, Type customcolliderType = null)
        {
            m_mockLogger = new MockLogger();
            DebugLogger.Instance = m_mockLogger;

            // Create EventSystem if needed
            if (spawnEventSystem)
            {
                m_eventSystemObject = new GameObject("EventSystem");
                m_eventSystemObject.AddComponent<EventSystem>();
                m_inputModule = m_eventSystemObject.AddComponent<TestInputModule>();

            }

            m_realPanel = RivePanelTestUtils.CreatePanel();
            m_panelObject = m_realPanel.gameObject;

            m_rendererObject = GameObject.CreatePrimitive(PrimitiveType.Cube);

            if (customcolliderType != null)
            {
                // Destroy default collider if custom collider is specified
                DestroyObj(m_rendererObject.GetComponent<Collider>());

                m_rendererObject.AddComponent(customcolliderType);
            }
            m_meshRenderer = m_rendererObject.GetComponent<MeshRenderer>();
            m_panelRenderer = m_rendererObject.AddComponent<RiveTextureRenderer>();



            m_panelRenderer.SetPanel(m_realPanel);

        }

        [TearDown]
        public void TearDown()
        {
            if (m_eventSystemObject != null)
            {
                DestroyObj(m_eventSystemObject);
            }
            DestroyObj(m_panelObject);
            DestroyObj(m_rendererObject);

            if (m_realPanel != null)
            {
                DestroyObj(m_realPanel.gameObject);
            }
        }


        private void DestroyObj(UnityEngine.Object obj)
        {
            if (obj != null)
            {
                UnityEngine.Object.Destroy(obj);

            }
        }
        [UnityTest]
        public IEnumerator OnEnable_InitializesComponents()
        {
            Setup();
            yield return null;

            Assert.IsNotNull(m_panelRenderer.Renderer, "Renderer should be initialized");
            Assert.IsNotNull(m_rendererObject.GetComponent<TexturePanelInputProvider>(),
                "TexturePanelInputProvider should be added");
            Assert.IsNotNull(m_rendererObject.GetComponent<PanelVisibilityOptimizer>(),
                "PanelVisibilityOptimizer should be added");
        }

        [UnityTest]
        public IEnumerator UpdateVisualTarget_UpdatesMaterialProperties_MainTexture()
        {
            Setup();
            // Change the object's material to the Sprites/Default shader since it's included in the build
            Shader shader = Shader.Find("Sprites/Default");
            Assert.IsNotNull(shader, "Sprites/Default default shader should be found");
            m_meshRenderer.material = new Material(shader);

            m_panelRenderer.RefreshMaterials();
            yield return null;

            // Setup mock render texture
            var renderTexture = new RenderTexture(TextureHelper.Descriptor(100, 100));
            m_mockPanel.SetRenderTexture(renderTexture);
            m_mockPanel.SetOffsetInRenderTexture(new Vector2(0.1f, 0.2f));
            m_mockPanel.SetScaleInRenderTexture(new Vector2(0.8f, 0.9f));

            // Set to MainTexture mode
            m_panelRenderer.MaterialTextureAssignmentMode = RiveTextureRenderer.TextureAssignmentMode.MainTexture;
            m_mockPanel.TriggerRenderTargetUpdated();

            yield return null;

            // Verify material properties
            Material material = m_meshRenderer.material;

            yield return null;
            Assert.IsNotNull(material.mainTexture, "Main texture should be set");
            Assert.IsTrue(ReferenceEquals(renderTexture, material.mainTexture),
                "Main texture should be updated");
            Assert.AreEqual(m_mockPanel.OffsetInRenderTexture, material.mainTextureOffset,
                "Texture offset should match panel offset");
            Assert.AreEqual(m_mockPanel.ScaleInRenderTexture, material.mainTextureScale,
                "Texture scale should match panel scale");

            // Set render texture to null and verify material properties are cleared
            m_mockPanel.SetRenderTexture(null);
            m_mockPanel.TriggerRenderTargetUpdated();

            yield return null;

            Assert.IsNull(material.mainTexture, "Main texture should be cleared when set to null");


            renderTexture.Release();
            DestroyObj(renderTexture);
        }

        [UnityTest]
        public IEnumerator UpdateVisualTarget_UpdatesMaterialProperties_TextureProperties()
        {
            Setup();
            // Change the object's material to the Sprites/Default shader since it's included in the build
            Shader shader = Shader.Find("Sprites/Default");
            Assert.IsNotNull(shader, "Sprites/Default default shader should be found");
            m_meshRenderer.material = new Material(shader);

            m_panelRenderer.RefreshMaterials();
            yield return null;

            // Setup mock render texture
            var renderTexture = new RenderTexture(100, 100, 0);
            m_mockPanel.SetRenderTexture(renderTexture);
            m_mockPanel.SetOffsetInRenderTexture(new Vector2(0.1f, 0.2f));
            m_mockPanel.SetScaleInRenderTexture(new Vector2(0.8f, 0.9f));

            // Set to TextureProperties mode with custom property
            m_panelRenderer.MaterialTextureAssignmentMode = RiveTextureRenderer.TextureAssignmentMode.TextureProperties;
            m_panelRenderer.SetMaterialPropertyNames(0, new[] { "_MainTex" });

            m_mockPanel.TriggerRenderTargetUpdated();

            yield return null;

            // Verify material properties
            Material material = m_meshRenderer.material;
            Texture textureInMaterial = material.GetTexture("_MainTex");

            Assert.IsTrue(ReferenceEquals(renderTexture, textureInMaterial),
                "Custom texture property should be updated");

            // Set render texture to null and verify material properties are cleared
            m_mockPanel.SetRenderTexture(null);
            m_mockPanel.TriggerRenderTargetUpdated();

            yield return null;

            Assert.IsNull(material.GetTexture("_MainTex"), "Custom texture property should be cleared when set to null");

            renderTexture.Release();
            DestroyObj(renderTexture);
        }

        [Test]
        public void VisibilityOptimization_UpdatesOptimizer()
        {
            Setup();

            var optimizer = m_panelRenderer.GetComponent<PanelVisibilityOptimizer>();
            Assert.IsNotNull(optimizer, "Visibility optimizer should exist");

            m_panelRenderer.VisibilityOptimization = VisibilityOptimizationMode.AlwaysRender;
            Assert.AreEqual(VisibilityOptimizationMode.AlwaysRender, optimizer.VisibilityMode,
                "Optimizer mode should match renderer setting");

            m_panelRenderer.VisibilityOptimization = VisibilityOptimizationMode.RenderWhenVisible;
            Assert.AreEqual(VisibilityOptimizationMode.RenderWhenVisible, optimizer.VisibilityMode,
                "Optimizer mode should update when renderer setting changes");
        }

        [UnityTest]
        public IEnumerator Start_WithoutEventSystem_LogsWarning()
        {
            Setup(spawnEventSystem: false);

            yield return null;

            m_panelRenderer.PointerInputMode = PointerInputMode.EnablePointerInput;
            Assert.IsTrue(m_mockLogger.LoggedWarnings.Count > 0);

            // Check that at least one warning mentions EventSystem
            bool logContainsEventSystem = false;
            foreach (string warning in m_mockLogger.LoggedWarnings)
            {
                if (warning.Contains(nameof(EventSystem)))
                {
                    logContainsEventSystem = true;
                    break;
                }
            }

            Assert.IsTrue(logContainsEventSystem,
                "Warning should mention EventSystem");
        }

        [UnityTest]
        public IEnumerator Start_WithEventSystem_NoWarnings()
        {
            Setup(spawnEventSystem: true);
            m_panelRenderer.PointerInputMode = PointerInputMode.EnablePointerInput;
            yield return null;

            // Check that no warnings related to EventSystem are logged
            bool logContainsEventSystem = false;
            foreach (string warning in m_mockLogger.LoggedWarnings)
            {
                if (warning.Contains(nameof(EventSystem)))
                {
                    logContainsEventSystem = true;
                    break;
                }
            }

            Assert.IsFalse(logContainsEventSystem,
                "No warnings should be logged when EventSystem is present");
        }



        [Test]
        public void MaterialPropertyNames_UpdatesTextureAssignment()
        {
            Setup();

            m_panelRenderer.MaterialTextureAssignmentMode = RiveTextureRenderer.TextureAssignmentMode.TextureProperties;

            var propertyNames = new[] { "_CustomTexture1", "_CustomTexture2" };
            m_panelRenderer.SetMaterialPropertyNames(0, propertyNames);



            CollectionAssert.AreEqual(propertyNames, m_panelRenderer.GetMaterialPropertyNames(0),
                "Material property names should be stored correctly");
        }

        [UnityTest]
        public IEnumerator UpdateVisualTarget_UpdatesAllMaterialProperties_MultiMaterial()
        {
            Setup();

            // Create multiple materials
            Material[] materials = new Material[]
            {
        new Material(Shader.Find("Sprites/Default")),
        new Material(Shader.Find("Sprites/Default")),
        new Material(Shader.Find("Sprites/Default"))
            };
            m_meshRenderer.materials = materials;

            m_panelRenderer.RefreshMaterials();

            // Setup mock render texture
            var renderTexture = new RenderTexture(100, 100, 0);
            m_mockPanel.SetRenderTexture(renderTexture);
            m_mockPanel.SetOffsetInRenderTexture(new Vector2(0.1f, 0.2f));
            m_mockPanel.SetScaleInRenderTexture(new Vector2(0.8f, 0.9f));

            // Test MainTexture mode
            {
                m_panelRenderer.MaterialTextureAssignmentMode = RiveTextureRenderer.TextureAssignmentMode.MainTexture;
                m_mockPanel.TriggerRenderTargetUpdated();

                yield return null;

                // Verify all materials are updated
                foreach (var material in m_meshRenderer.materials)
                {
                    Assert.IsTrue(ReferenceEquals(renderTexture, material.mainTexture),
                        "Main texture should be updated for all materials");
                    Assert.AreEqual(m_mockPanel.OffsetInRenderTexture, material.mainTextureOffset,
                        "Texture offset should match panel offset for all materials");
                    Assert.AreEqual(m_mockPanel.ScaleInRenderTexture, material.mainTextureScale,
                        "Texture scale should match panel scale for all materials");
                }
            }

            string[] material0Properties = new[] { "_MainTex", "_AlphaTex" }; // These are valid texture properties for Sprites/Default
            string[] material1Properties = new[] { "_AlphaTex" };
            string[] material2Properties = new[] { "_AlphaTex", "_MainTex", };

            // Test TextureProperties mode
            {
                m_panelRenderer.MaterialTextureAssignmentMode = RiveTextureRenderer.TextureAssignmentMode.TextureProperties;


                // Set different property names for each material
                m_panelRenderer.ClearMaterialPropertyNames();

                m_panelRenderer.SetMaterialPropertyNames(0, material0Properties);
                m_panelRenderer.SetMaterialPropertyNames(1, material1Properties);
                m_panelRenderer.SetMaterialPropertyNames(2, material2Properties);

                m_mockPanel.TriggerRenderTargetUpdated();

                yield return null;

                // Verify each material's properties
                var material0 = m_meshRenderer.materials[0];
                foreach (string prop in material0Properties)
                {
                    Assert.IsTrue(ReferenceEquals(renderTexture, material0.GetTexture(prop)),
                        $"Material 0: Property {prop} should be updated");
                    Assert.AreEqual(m_mockPanel.OffsetInRenderTexture, material0.GetTextureOffset(prop),
                        $"Material 0: Property {prop} offset should match");
                    Assert.AreEqual(m_mockPanel.ScaleInRenderTexture, material0.GetTextureScale(prop),
                        $"Material 0: Property {prop} scale should match");
                }

                var material1 = m_meshRenderer.materials[1];
                Assert.IsTrue(ReferenceEquals(renderTexture, material1.GetTexture(material1Properties[0])),
                    "Material 1: Property should be updated");

                var material2 = m_meshRenderer.materials[2];
                foreach (string prop in material2Properties)
                {
                    Assert.IsTrue(ReferenceEquals(renderTexture, material2.GetTexture(prop)),
                        $"Material 2: Property {prop} should be updated");
                }
            }

            // Cleanup
            renderTexture.Release();
            DestroyObj(renderTexture);
            foreach (var material in materials)
            {
                DestroyObj(material);
            }
        }

        [Test]
        public void MaterialPropertyNames_EmptyProperties_SkipsMaterial()
        {
            Setup();

            // Create multiple materials
            Material[] materials = new Material[]
            {
        new Material(Shader.Find("Sprites/Default")), // We use Sprites/Default because we can expect it to be included in the build
        new Material(Shader.Find("Sprites/Default"))
            };
            m_meshRenderer.materials = materials;

            m_panelRenderer.MaterialTextureAssignmentMode = RiveTextureRenderer.TextureAssignmentMode.TextureProperties;

            var texturePropName = "_MainTex";
            // Only set properties for first material
            m_panelRenderer.ClearMaterialPropertyNames();
            m_panelRenderer.SetMaterialPropertyNames(0, new string[] { texturePropName });
            // Material index 1 has no properties - should be skipped

            var renderTexture = new RenderTexture(100, 100, 0);
            m_mockPanel.SetRenderTexture(renderTexture);
            m_mockPanel.TriggerRenderTargetUpdated();

            // Verify second material was skipped (maintains original null texture)
            Assert.IsNull(materials[1].GetTexture(texturePropName),
                "Material without specified properties should be skipped");

            // Cleanup
            renderTexture.Release();
            DestroyObj(renderTexture);
            foreach (var material in materials)
            {
                DestroyObj(material);
            }
        }

        private Camera m_camera;

        private Camera GetOrCreateCamera()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("TestCamera");
                camera = cameraObject.AddComponent<Camera>();
                m_raycaster = cameraObject.AddComponent<PhysicsRaycaster>();
            }
            return camera;
        }

        [UnityTest]
        public IEnumerator VisibilityOptimization_StopsRenderingWhenNotVisible()
        {
            Setup();
            m_camera = GetOrCreateCamera();

            // Position camera to see renderer
            m_camera.transform.position = new Vector3(0, 0, -10);
            m_camera.transform.LookAt(m_rendererObject.transform);

            m_panelRenderer.VisibilityOptimization = VisibilityOptimizationMode.RenderWhenVisible;

            yield return null;

            // Initially should be rendering since visible
            Assert.IsTrue(m_mockPanel.IsRendering, "Panel should render when visible");

            // Move object behind and above camera
            m_rendererObject.transform.position = new Vector3(0, 442f, 10);

            yield return null;

            Assert.IsFalse(m_mockPanel.IsRendering, "Panel should stop rendering when not visible");

            // Move back to visible position
            m_rendererObject.transform.position = Vector3.zero;

            yield return null;

            Assert.IsTrue(m_mockPanel.IsRendering, "Panel should resume rendering when visible again");

            if (m_camera.gameObject != Camera.main?.gameObject)
            {
                DestroyObj(m_camera.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator VisibilityOptimization_AlwaysRenderMode_IgnoresVisibility()
        {
            Setup();
            m_camera = GetOrCreateCamera();

            // Position camera to see renderer
            m_camera.transform.position = new Vector3(0, 0, -10);
            m_camera.transform.LookAt(m_rendererObject.transform);

            m_panelRenderer.VisibilityOptimization = VisibilityOptimizationMode.AlwaysRender;

            yield return null;

            Assert.IsTrue(m_mockPanel.IsRendering, "Panel should render when visible in AlwaysRender mode");

            // Move object behind and above camera
            m_rendererObject.transform.position = new Vector3(0, 442f, 10);

            yield return null;

            Assert.IsTrue(m_mockPanel.IsRendering, "Panel should continue rendering when not visible in AlwaysRender mode");

            if (m_camera.gameObject != Camera.main?.gameObject)
            {
                DestroyObj(m_camera.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator VisibilityOptimization_DisabledPanel_StaysDisabled()
        {
            Setup();
            m_camera = GetOrCreateCamera();

            m_camera.transform.position = new Vector3(0, 0, -10);
            m_camera.transform.LookAt(m_rendererObject.transform);

            // Disable panel gameobject first
            m_mockPanel.gameObject.SetActive(false);

            yield return null;

            Assert.IsFalse(m_mockPanel.IsRendering, "Disabled panel should not render when visible");

            // Move object behind and above camera
            m_rendererObject.transform.position = new Vector3(0, 442f, 10);

            yield return null;

            Assert.IsFalse(m_mockPanel.IsRendering, "Disabled panel should not render when not visible");

            // Re-enable panel while not visible
            m_mockPanel.gameObject.SetActive(true);


            yield return null;

            Assert.IsFalse(m_mockPanel.IsRendering, "Re-enabled panel should not render when not visible");

            if (m_camera.gameObject != Camera.main?.gameObject)
            {
                DestroyObj(m_camera.gameObject);
            }
        }


        [UnityTest]
        public IEnumerator OnEnable_InitializesInputProvider()
        {
            Setup();
            yield return null;

            Assert.IsTrue(m_mockPanel.InputProviders.Count > 0, "TexturePanelInputProvider should be added");
        }

        [UnityTest]
        public IEnumerator Destroying_TextureRenderer_UnregistersInputProvider()
        {
            Setup();
            yield return null;

            Assert.IsTrue(m_mockPanel.InputProviders.Count > 0, "TexturePanelInputProvider should be added");

            DestroyObj(m_panelRenderer);

            yield return null;

            Assert.IsFalse(m_mockPanel.InputProviders.Count > 0, "TexturePanelInputProvider should be removed");
        }


        [UnityTest]
        public IEnumerator EventSystem_PointerEvents_PropagateToPanel()
        {
            DebugLogger.Instance = null;
            SetupWithRealPanel(customcolliderType: typeof(MeshCollider));

            var mockWidget = RivePanelTestUtils.CreateWidget<MockRiveWidget>();
            m_realPanel.AddToHierarchy(mockWidget);
            RivePanelTestUtils.MakeWidgetFillPanel(mockWidget);

            m_panelRenderer.PointerInputMode = PointerInputMode.EnablePointerInput;

            yield return null;



            // We need to position the camera and object correctly for raycasting
            var camera = GetOrCreateCamera();
            camera.transform.position = new Vector3(0, 0, -10);
            camera.transform.LookAt(m_rendererObject.transform);
            m_rendererObject.transform.position = Vector3.zero;

            Vector2 pointerPosition = new Vector2(Screen.width / 2, Screen.height / 2); // Center of screen

            // Simulate pointer down
            m_inputModule.PointerDownAt(pointerPosition);

            yield return null;
            Assert.AreEqual(1, mockWidget.PointerDownCalledCount, "PointerDown should be called once");

            // Simulate pointer move
            pointerPosition += new Vector2(10, 10);

            m_inputModule.PointerMoveAt(pointerPosition);
            yield return null;
            Assert.AreEqual(1, mockWidget.PointerMoveCalledCount, "PointerMove should be called once");

            // Simulate pointer up

            m_inputModule.PointerUpAt(pointerPosition);
            yield return null;
            Assert.AreEqual(1, mockWidget.PointerUpCalledCount, "PointerUp should be called once");
        }

        [UnityTest]
        public IEnumerator EventSystem_PointerEvents_DisabledInput_NoEvents()
        {
            SetupWithRealPanel(customcolliderType: typeof(MeshCollider));

            var mockWidget = RivePanelTestUtils.CreateWidget<MockRiveWidget>();
            m_realPanel.AddToHierarchy(mockWidget);
            RivePanelTestUtils.MakeWidgetFillPanel(mockWidget);

            // Disable pointer input
            m_panelRenderer.PointerInputMode = PointerInputMode.DisablePointerInput;

            yield return null;

            Vector2 pointerPosition = new Vector2(Screen.width / 2, Screen.height / 2); // Center of screen


            // Position camera and object for raycasting
            var camera = GetOrCreateCamera();
            camera.transform.position = new Vector3(0, 0, -10);
            camera.transform.LookAt(m_rendererObject.transform);
            m_rendererObject.transform.position = Vector3.zero;

            // Simulate all pointer events
            m_inputModule.PointerDownAt(pointerPosition);
            m_inputModule.PointerMoveAt(pointerPosition + new Vector2(10, 10));
            m_inputModule.PointerUpAt(pointerPosition);

            yield return null;

            Assert.AreEqual(0, mockWidget.PointerDownCalledCount, "PointerDown should not be called when input is disabled");
            Assert.AreEqual(0, mockWidget.PointerMoveCalledCount, "PointerMove should not be called when input is disabled");
            Assert.AreEqual(0, mockWidget.PointerUpCalledCount, "PointerUp should not be called when input is disabled");
        }

        [UnityTest]
        public IEnumerator EventSystem_PointerEvents_OutsideCollider_NoEvents()
        {
            SetupWithRealPanel(customcolliderType: typeof(MeshCollider));

            var mockWidget = RivePanelTestUtils.CreateWidget<MockRiveWidget>();
            m_realPanel.AddToHierarchy(mockWidget);
            RivePanelTestUtils.MakeWidgetFillPanel(mockWidget);

            m_panelRenderer.PointerInputMode = PointerInputMode.EnablePointerInput;

            yield return null;

            Vector2 pointerPosition = new Vector2(Screen.width, Screen.height);


            // Position camera and object for raycasting
            var camera = GetOrCreateCamera();
            camera.transform.position = new Vector3(0, 0, -10);
            camera.transform.LookAt(m_rendererObject.transform);
            m_rendererObject.transform.position = Vector3.zero;

            // Simulate all pointer events
            m_inputModule.PointerDownAt(pointerPosition);
            m_inputModule.PointerMoveAt(pointerPosition);
            m_inputModule.PointerUpAt(pointerPosition);

            yield return null;

            Assert.AreEqual(0, mockWidget.PointerDownCalledCount, "PointerDown should not be called for events outside collider");
            Assert.AreEqual(0, mockWidget.PointerMoveCalledCount, "PointerMove should not be called for events outside collider");
            Assert.AreEqual(0, mockWidget.PointerUpCalledCount, "PointerUp should not be called for events outside collider");
        }


    }
}