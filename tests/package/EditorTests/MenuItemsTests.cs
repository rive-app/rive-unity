using UnityEngine;
using UnityEditor;
using NUnit.Framework;
using Rive.Components;
using Rive.EditorTools;
using UnityEngine.EventSystems;
using Rive.Tests.Utils;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

namespace Rive.Tests.EditorTests
{
    public class MenuItemsTests
    {
        private GameObject m_testParent;
        private Asset m_testRiveAsset;
        private Scene m_testScene;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            m_testRiveAsset = AssetDatabase.LoadAssetAtPath<Asset>(TestAssetReferences.riv_cleanTheCar);
            Assert.IsNotNull(m_testRiveAsset, $"Failed to load test asset at path: {TestAssetReferences.riv_sophiaHud}");


        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {

            // Restore the original scene
            // This is required for cleanup or we might get errors after running the tests
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        }


        [SetUp]
        public void SetUp()
        {

            // Create and switch to a temporary scene
            m_testScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            Assert.IsTrue(m_testScene.IsValid());

            SceneManager.SetActiveScene(m_testScene);

            Assert.IsTrue(SceneManager.GetActiveScene() == m_testScene);

            Assert.IsTrue(m_testScene.isLoaded);

            Assert.AreEqual(SceneManager.loadedSceneCount, 1);

            m_testParent = new GameObject("TestParent");
            SceneManager.MoveGameObjectToScene(m_testParent, m_testScene);
        }



        [TearDown]
        public void TearDown()
        {
            if (SceneManager.GetActiveScene() != m_testScene)
            {
                return;
            }

            if (m_testParent != null)
            {
                Object.DestroyImmediate(m_testParent);
            }

            // Clean up any EventSystem that might have been created for input handling
            var eventSystem = Object.FindObjectOfType<EventSystem>();
            if (eventSystem != null)
            {
                Object.DestroyImmediate(eventSystem.gameObject);
            }

            var panels = Object.FindObjectsOfType<RivePanel>();
            foreach (var panel in panels)
            {
                Object.DestroyImmediate(panel.gameObject);
            }




        }

        [Test]
        public void CreateRivePanelInternal_Standalone_CreatesCorrectHierarchy()
        {
            var panel = MenuItems.CreateRivePanelInternal(
                new MenuCommand(m_testParent),
                MenuItems.PanelContext.Standalone
            );

            Assert.IsNotNull(panel);
            Assert.AreEqual("Rive Panel", panel.gameObject.name);
            Assert.IsTrue(panel.GetComponent<RectTransform>() != null);

            // Check that the widget was created
            var widget = panel.GetComponentInChildren<RiveWidget>();
            Assert.IsNotNull(widget);
            Assert.AreEqual("Rive Widget", widget.gameObject.name);
            Assert.IsNull(widget.Asset);

            // Check that the widget fills the parent
            Assert.IsTrue(DoesWidgetFillParent(widget));

            // Check that the panel is the parent of the widget
            Assert.AreEqual(m_testParent.transform, panel.transform.parent);
            Assert.AreEqual(panel.transform, widget.transform.parent);
        }

        private bool DoesWidgetFillParent(RiveWidget widget)
        {
            var widgetRect = widget.GetComponent<RectTransform>();
            var parentRect = widget.transform.parent.GetComponent<RectTransform>();

            return widgetRect.anchorMin == Vector2.zero
                && widgetRect.anchorMax == Vector2.one
                && widgetRect.sizeDelta == Vector2.zero
                && widgetRect.offsetMin == Vector2.zero
                && widgetRect.offsetMax == Vector2.zero
                && widgetRect.anchoredPosition == Vector2.zero
                && widgetRect.anchoredPosition3D == Vector3.zero
                && widgetRect.localPosition == Vector3.zero
                && widgetRect.rect == parentRect.rect;
        }

        [Test]
        public void CreateRivePanelInternal_Canvas_CreatesCorrectHierarchy()
        {
            var panel = MenuItems.CreateRivePanelInternal(
                new MenuCommand(m_testParent),
                MenuItems.PanelContext.Canvas
            );

            Assert.IsNotNull(panel);

            // Check Canvas setup
            var canvas = panel.GetComponentInParent<Canvas>();
            Assert.IsNotNull(canvas);
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);

            // Check RiveCanvasRenderer
            var canvasRenderer = panel.GetComponent<RiveCanvasRenderer>();
            Assert.IsNotNull(canvasRenderer);
            Assert.AreEqual(panel, canvasRenderer.RivePanel);

            // Check that the widget was created
            var widget = panel.GetComponentInChildren<RiveWidget>();
            Assert.IsNotNull(widget);
            Assert.IsNull(widget.Asset);
        }

        [Test]
        public void HandleAssetDrop_UnderExistingPanel_CreatesWidget()
        {
            var existingPanel = MenuItems.CreateRivePanelInternal(
                new MenuCommand(m_testParent),
                MenuItems.PanelContext.Standalone
            );

            // Drop new asset under existing panel
            MenuItems.HandleAssetDrop(m_testRiveAsset, existingPanel.transform);

            // Should have created a new widget instead of a new panel
            Assert.AreEqual(1, Object.FindObjectsOfType<RivePanel>().Length);
            Assert.AreEqual(2, existingPanel.GetComponentsInChildren<RiveWidget>().Length);

            // Should have set the asset on the new widget
            var widgets = existingPanel.GetComponentsInChildren<RiveWidget>();
            Assert.IsTrue(ReferenceEquals(widgets[1].Asset, m_testRiveAsset));

            Assert.IsTrue(DoesWidgetFillParent(widgets[1]));
        }

        [Test]
        public void HandleAssetDrop_WithMeshRenderer_CreatesStandaloneSetup()
        {

            m_testParent.AddComponent<MeshRenderer>();

            MenuItems.HandleAssetDrop(m_testRiveAsset, m_testParent.transform);

            // We expect that the gameobject with the Mesh render has a RiveTextureRenderer applied to it
            var textureRenderer = m_testParent.GetComponent<RiveTextureRenderer>();
            Assert.IsNotNull(textureRenderer);
            Assert.IsTrue(ReferenceEquals(textureRenderer.Renderer.gameObject, m_testParent));


            var panel = Object.FindObjectOfType<RivePanel>();
            Assert.IsNotNull(panel);
            Assert.AreEqual(panel, textureRenderer.RivePanel);

            // Check that the panel size matches the artboard size
            // In world space, the panel size should match the artboard size so that the Rive content is rendered at the correct aspect ratio/size
            // In screen space, the panel size should match the screen size

            Assert.Greater(m_testRiveAsset.EditorOnlyMetadata.Artboards.Count, 0);
            var artboard = m_testRiveAsset.EditorOnlyMetadata.Artboards[0];

            var panelRect = panel.WidgetContainer.rect;
            Assert.AreEqual(artboard.Width, panelRect.width);
            Assert.AreEqual(artboard.Height, panelRect.height);

            var widget = panel.GetComponentInChildren<RiveWidget>();
            Assert.IsNotNull(widget);
            Assert.IsTrue(ReferenceEquals(widget.Asset, m_testRiveAsset));
        }


        /// <summary>
        /// Test that dropping an asset with no parent creates a Rive panel that renders to the screen.
        /// </summary>
        [Test]
        public void HandleAssetDrop_NoParent_CreatesCanvasSetup()
        {
            MenuItems.HandleAssetDrop(m_testRiveAsset, null);

            var panel = Object.FindObjectOfType<RivePanel>();
            Assert.IsNotNull(panel);

            var canvas = panel.GetComponentInParent<Canvas>();
            Assert.IsNotNull(canvas);


            // We want input to work immediately, so we create an EventSystem if one doesn't exist
            var eventSystem = Object.FindObjectOfType<EventSystem>();
            Assert.IsNotNull(eventSystem);

            // The widget should have been created with the asset set.
            var widget = panel.GetComponentInChildren<RiveWidget>();
            Assert.IsNotNull(widget);
            Assert.IsTrue(ReferenceEquals(widget.Asset, m_testRiveAsset));
        }

        [Test]
        public void ConfigureRectTransformToFill_SetsCorrectValues()
        {
            var testObj = new GameObject("Test", typeof(RectTransform));
            var rectTransform = testObj.GetComponent<RectTransform>();

            MenuItems.ConfigureRectTransformToFill(rectTransform);

            Assert.AreEqual(Vector2.zero, rectTransform.anchorMin);
            Assert.AreEqual(Vector2.one, rectTransform.anchorMax);
            Assert.AreEqual(Vector2.zero, rectTransform.sizeDelta);

            Object.DestroyImmediate(testObj);
        }
    }
}