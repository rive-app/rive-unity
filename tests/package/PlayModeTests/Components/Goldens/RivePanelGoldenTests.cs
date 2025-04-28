#if RIVE_USING_GRAPHICS_TEST_FRAMEWORK

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Rive.Tests.Utils;
using System;
using System.Linq;
using Rive.Components;
using System.Collections.Generic;
using UnityEngine.TestTools.Graphics;
using Rive.Utils;

namespace Rive.Tests
{
    public class TestPrefabReferences
    {

        public const string RivePanelWithSingleWidget = "Packages/app.rive.rive-unity.tests/PlayModeTests/Components/Goldens/TestPanels/RivePanelWithSingleWidget.prefab";
        public const string RivePanelWithMultipleWidgets = "Packages/app.rive.rive-unity.tests/PlayModeTests/Components/Goldens/TestPanels/RivePanelWithMultipleWidgets.prefab";
        public const string RivePanelWithSingleWidgetAndLayout = "Packages/app.rive.rive-unity.tests/PlayModeTests/Components/Goldens/TestPanels/RivePanelWithSingleWidgetAndLayout.prefab";
        public const string RivePanelWithProceduralWidget = "Packages/app.rive.rive-unity.tests/PlayModeTests/Components/Goldens/TestPanels/RivePanelWithProceduralWidget.prefab";

        public const string RivePanelWithEmptySingleWidget = "Packages/app.rive.rive-unity.tests/PlayModeTests/Components/Goldens/TestPanels/RivePanelWithEmptySingleWidget.prefab";

        public const string RivePanelWithInitialFrameInputs = "Packages/app.rive.rive-unity.tests/PlayModeTests/Components/Goldens/TestPanels/RivePanelWithInitialFrameInputs.prefab";
    }

    public class RivePanelGoldenTests
    {
        private class CustomTestCases
        {
            public const string AtlasStrategy_Renders_MultiplePanels = "AtlasStrategy_Renders_MultiplePanels";
        }

        public class PanelScenario
        {
            public string GoldenId { get; }
            public string PanelPrefabPath { get; }
            public Action<RivePanel> ConfigurePanel { get; }
            public IEnumerator WaitCondition { get; }

            public bool IncludeInTransformChangeTests { get; set; } = false;

            public PanelScenario(
                string goldenId,
                string panelPrefabPath,
                Action<RivePanel> configurePanel = null,
                IEnumerator waitCondition = null,
                bool includeInTransformChangeTests = false)
            {
                GoldenId = goldenId;
                PanelPrefabPath = panelPrefabPath;
                ConfigurePanel = configurePanel ?? (_ => { });
                WaitCondition = waitCondition;
                IncludeInTransformChangeTests = includeInTransformChangeTests;
            }

            public static IEnumerator WaitForSeconds(float seconds)
            {
                yield return new UnityEngine.WaitForSeconds(seconds);
            }

            public static IEnumerator WaitAFrame()
            {
                yield return null;
            }
        }

        private GoldenTestHelper m_goldenHelper;
        private TestAssetLoadingManager m_testAssetLoadingManager;

        private Camera m_camera;

        private PanelScenario[] GetTestCases()
        {
            return new[]
        {
            new PanelScenario(
                goldenId: "RivePanelWithMultipleWidgets",
                panelPrefabPath: TestPrefabReferences.RivePanelWithMultipleWidgets,
                includeInTransformChangeTests: true
            ),
            new PanelScenario(
                goldenId: "RivePanelWithSingleWidget_Artboard_FitMode_Contain",
                panelPrefabPath: TestPrefabReferences.RivePanelWithSingleWidget,
                configurePanel: (panel) => {
                    var widget = panel.GetComponentInChildren<RiveWidget>();
                    widget.Fit = Fit.Contain;
                },
                includeInTransformChangeTests: true

            ),
            new PanelScenario(
                goldenId: "RivePanelWithSingleWidget_Artboard_FitMode_Fill",
                panelPrefabPath: TestPrefabReferences.RivePanelWithSingleWidget,
                configurePanel: (panel) => {
                    var widget = panel.GetComponentInChildren<RiveWidget>();
                    widget.Fit = Fit.Fill;
                }
            ),
            new PanelScenario(
                goldenId: "RivePanelWithSingleWidget_Artboard_FitMode_Cover",
                panelPrefabPath: TestPrefabReferences.RivePanelWithSingleWidget,
                configurePanel: (panel) => {
                    var widget = panel.GetComponentInChildren<RiveWidget>();
                    widget.Fit = Fit.Cover;
                }
            ),
            new PanelScenario(
                goldenId: "RivePanelWithSingleWidget_Artboard_FitMode_FitWidth",
                panelPrefabPath: TestPrefabReferences.RivePanelWithSingleWidget,
                configurePanel: (panel) => {
                    var widget = panel.GetComponentInChildren<RiveWidget>();
                    widget.Fit = Fit.FitWidth;
                }
            ),
            new PanelScenario(
                goldenId: "RivePanelWithSingleWidget_Artboard_FitMode_FitHeight",
                panelPrefabPath: TestPrefabReferences.RivePanelWithSingleWidget,
                configurePanel: (panel) => {
                    var widget = panel.GetComponentInChildren<RiveWidget>();
                    widget.Fit = Fit.FitHeight;
                }
            ),
            new PanelScenario(
                goldenId: "RivePanelWithSingleWidget_Artboard_FitMode_None",
                panelPrefabPath: TestPrefabReferences.RivePanelWithSingleWidget,
                configurePanel: (panel) => {
                    var widget = panel.GetComponentInChildren<RiveWidget>();
                    widget.Fit = Fit.None;
                }
            ),
            new PanelScenario(
                goldenId: "RivePanelWithSingleWidget_Artboard_FitMode_ScaleDown",
                panelPrefabPath: TestPrefabReferences.RivePanelWithSingleWidget,
                configurePanel: (panel) => {
                    var widget = panel.GetComponentInChildren<RiveWidget>();
                    widget.Fit = Fit.ScaleDown;
                }
            ),

            // Alignment
            new PanelScenario(
                goldenId: "RivePanelWithSingleWidget_Artboard_FitMode_Contain_Alignment_TopLeft",
                panelPrefabPath: TestPrefabReferences.RivePanelWithSingleWidget,
                configurePanel: (panel) => {
                    var widget = panel.GetComponentInChildren<RiveWidget>();
                    widget.Fit = Fit.ScaleDown;
                    widget.Alignment = Alignment.TopLeft;
                }
            ),
            new PanelScenario(
                goldenId: "RivePanelWithSingleWidget_Artboard_FitMode_Contain_Alignment_TopCenter",
                panelPrefabPath: TestPrefabReferences.RivePanelWithSingleWidget,
                configurePanel: (panel) => {
                    var widget = panel.GetComponentInChildren<RiveWidget>();
                    widget.Fit = Fit.ScaleDown;
                    widget.Alignment = Alignment.TopCenter;
                }
            ),
            new PanelScenario(
                goldenId: "RivePanelWithSingleWidget_Artboard_FitMode_Contain_Alignment_TopRight",
                panelPrefabPath: TestPrefabReferences.RivePanelWithSingleWidget,
                configurePanel: (panel) => {
                    var widget = panel.GetComponentInChildren<RiveWidget>();
                    widget.Fit = Fit.ScaleDown;
                    widget.Alignment = Alignment.TopRight;
                }
            ),
            new PanelScenario(
                goldenId: "RivePanelWithSingleWidget_Artboard_FitMode_Contain_Alignment_CenterLeft",
                panelPrefabPath: TestPrefabReferences.RivePanelWithSingleWidget,
                configurePanel: (panel) => {
                    var widget = panel.GetComponentInChildren<RiveWidget>();
                    widget.Fit = Fit.ScaleDown;
                    widget.Alignment = Alignment.CenterLeft;
                }
            ),
            new PanelScenario(
                goldenId: "RivePanelWithSingleWidget_Artboard_FitMode_Contain_Alignment_CenterCenter",
                panelPrefabPath: TestPrefabReferences.RivePanelWithSingleWidget,
                configurePanel: (panel) => {
                    var widget = panel.GetComponentInChildren<RiveWidget>();
                    widget.Fit = Fit.ScaleDown;
                    widget.Alignment = Alignment.Center;
                }
            ),
            new PanelScenario(
                goldenId: "RivePanelWithSingleWidget_Artboard_FitMode_Contain_Alignment_CenterRight",
                panelPrefabPath: TestPrefabReferences.RivePanelWithSingleWidget,
                configurePanel: (panel) => {
                    var widget = panel.GetComponentInChildren<RiveWidget>();
                    widget.Fit = Fit.ScaleDown;
                    widget.Alignment = Alignment.CenterRight;
                }
            ),
            new PanelScenario(
                goldenId: "RivePanelWithSingleWidget_Artboard_FitMode_Contain_Alignment_BottomLeft",
                panelPrefabPath: TestPrefabReferences.RivePanelWithSingleWidget,
                configurePanel: (panel) => {
                    var widget = panel.GetComponentInChildren<RiveWidget>();
                    widget.Fit = Fit.ScaleDown;
                    widget.Alignment = Alignment.BottomLeft;
                }
            ),
            new PanelScenario(
                goldenId: "RivePanelWithSingleWidget_Artboard_FitMode_Contain_Alignment_BottomCenter",
                panelPrefabPath: TestPrefabReferences.RivePanelWithSingleWidget,
                configurePanel: (panel) => {
                    var widget = panel.GetComponentInChildren<RiveWidget>();
                    widget.Fit = Fit.ScaleDown;
                    widget.Alignment = Alignment.BottomCenter;
                }
            ),
            new PanelScenario(
                goldenId: "RivePanelWithSingleWidget_Artboard_FitMode_Contain_Alignment_BottomRight",
                panelPrefabPath: TestPrefabReferences.RivePanelWithSingleWidget,
                configurePanel: (panel) => {
                    var widget = panel.GetComponentInChildren<RiveWidget>();
                    widget.Fit = Fit.ScaleDown;
                    widget.Alignment = Alignment.BottomRight;
                }
            ),


            // Procedural widget
            new PanelScenario(
                goldenId: "RivePanelWithProceduralWidget",
                panelPrefabPath: TestPrefabReferences.RivePanelWithProceduralWidget,
                includeInTransformChangeTests: false
            ),

            // Layout scaling modes

            //ReferenceArtboardSize
            // The widget will look the same at different resolutions, as long as the aspect ratio is the same.
            new PanelScenario(
                goldenId: "RivePanelWithSingleWidgetAndLayout_FitMode_Layout_ReferenceArtboardSize_Landscape_1920x1080",
                panelPrefabPath: TestPrefabReferences.RivePanelWithSingleWidgetAndLayout,
                configurePanel: (panel) => {
                    panel.SetDimensions(new Vector2(1920, 1080));
                    var widget = panel.GetComponentInChildren<RiveWidget>();
                    widget.Fit = Fit.Layout;
                    widget.ScalingMode = LayoutScalingMode.ReferenceArtboardSize;
                }
            ),
            new PanelScenario(
                goldenId: "RivePanelWithSingleWidgetAndLayout_FitMode_Layout_ReferenceArtboardSize_Portrait_1080x1920",
                panelPrefabPath: TestPrefabReferences.RivePanelWithSingleWidgetAndLayout,
                configurePanel: (panel) => {
                    panel.SetDimensions(new Vector2(1080, 1920));
                    var widget = panel.GetComponentInChildren<RiveWidget>();
                    widget.Fit = Fit.Layout;
                    widget.ScalingMode = LayoutScalingMode.ReferenceArtboardSize;
                }
            ),
             new PanelScenario(
                goldenId: "RivePanelWithSingleWidgetAndLayout_FitMode_Layout_ReferenceArtboardSize_Landscape_1280x720",
                panelPrefabPath: TestPrefabReferences.RivePanelWithSingleWidgetAndLayout,
                configurePanel: (panel) => {
                    panel.SetDimensions(new Vector2(1280, 720));
                    var widget = panel.GetComponentInChildren<RiveWidget>();
                    widget.Fit = Fit.Layout;
                    widget.ScalingMode = LayoutScalingMode.ReferenceArtboardSize;
                }
            ),

            // ConstantPixelSize 
            // The widget will look smaller on a larger resolution screen, and larger on a smaller resolution screen.
            new PanelScenario(
                goldenId: "RivePanelWithSingleWidgetAndLayout_FitMode_Layout_ReferenceArtboardSize_Landscape_1920x1080_ScaleFactor_2",
                panelPrefabPath: TestPrefabReferences.RivePanelWithSingleWidgetAndLayout,
                configurePanel: (panel) => {
                    panel.SetDimensions(new Vector2(1920, 1080));
                    var widget = panel.GetComponentInChildren<RiveWidget>();
                    widget.Fit = Fit.Layout;
                    widget.ScalingMode = LayoutScalingMode.ReferenceArtboardSize;
                    widget.ScaleFactor = 2f;
                }
            ),
            new PanelScenario(
                goldenId: "RivePanelWithSingleWidgetAndLayout_FitMode_Layout_ConstantPixelSize_Landscape_1920x1080",
                panelPrefabPath: TestPrefabReferences.RivePanelWithSingleWidgetAndLayout,
                configurePanel: (panel) => {
                    panel.SetDimensions(new Vector2(1920, 1080));
                    var widget = panel.GetComponentInChildren<RiveWidget>();
                    widget.Fit = Fit.Layout;
                    widget.ScalingMode = LayoutScalingMode.ConstantPixelSize;
                }
            ),
            new PanelScenario(
                goldenId: "RivePanelWithSingleWidgetAndLayout_FitMode_Layout_ConstantPixelSize_Portrait_1080x1920",
                panelPrefabPath: TestPrefabReferences.RivePanelWithSingleWidgetAndLayout,
                configurePanel: (panel) => {
                    panel.SetDimensions(new Vector2(1080, 1920));
                    var widget = panel.GetComponentInChildren<RiveWidget>();
                    widget.Fit = Fit.Layout;
                    widget.ScalingMode = LayoutScalingMode.ConstantPixelSize;
                }
            ),
            new PanelScenario(
                goldenId: "RivePanelWithSingleWidgetAndLayout_FitMode_Layout_ConstantPixelSize_Landscape_1280x720",
                panelPrefabPath: TestPrefabReferences.RivePanelWithSingleWidgetAndLayout,
                configurePanel: (panel) => {
                    panel.SetDimensions(new Vector2(1280, 720));
                    var widget = panel.GetComponentInChildren<RiveWidget>();
                    widget.Fit = Fit.Layout;
                    widget.ScalingMode = LayoutScalingMode.ConstantPixelSize;
                }
            ),            
            // Test that the layout is affected by dpi:
            // For example, higher DPR device it appear zoomed in, while lower DPR device it appear zoomed out.
             new PanelScenario(
                goldenId: "RivePanelWithSingleWidgetAndLayout_FitMode_Layout_ConstantPhysicalSize_Landscape_1920x1080_HighDPI",
                panelPrefabPath: TestPrefabReferences.RivePanelWithSingleWidgetAndLayout,
                configurePanel: (panel) => {
                    panel.SetDimensions(new Vector2(1920, 1080));
                    var widget = panel.GetComponentInChildren<RiveWidget>();
                    widget.UseFallbackDPI = true;
                    widget.Fit = Fit.Layout;
                    widget.ScalingMode = LayoutScalingMode.ConstantPhysicalSize;
                    widget.FallbackDPI = 225f;
                    widget.ReferenceDPI = 150f;


                }
            ),
            new PanelScenario(
                goldenId: "RivePanelWithSingleWidgetAndLayout_FitMode_Layout_ConstantPhysicalSize_Landscape__1920x1080_LowDPI",
                panelPrefabPath: TestPrefabReferences.RivePanelWithSingleWidgetAndLayout,
                configurePanel: (panel) => {
                    panel.SetDimensions(new Vector2(1920, 1080));
                    var widget = panel.GetComponentInChildren<RiveWidget>();
                    widget.UseFallbackDPI = true;
                    widget.Fit = Fit.Layout;
                    widget.ScalingMode = LayoutScalingMode.ConstantPhysicalSize;
                    widget.FallbackDPI = 96f;
                    widget.ReferenceDPI = 150f;


                }
            ),
        };
        }

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            m_testAssetLoadingManager = new TestAssetLoadingManager();

            // Important: Make sure the resolution is set to a number with a power of 2 (e.g. 512, 1024, 2048, etc.) otherwise, unity will resize the saved image
            // - Make sure the images are saved as and set to RGBA32 in the Unity editor
            // - Make sure the images have Read/Write enabled in the Unity editor
            // - Set captureGolden to true to generate golden images.
            m_goldenHelper = new GoldenTestHelper(assetLoadingManager: m_testAssetLoadingManager, referenceImagesPath: "Packages/app.rive.rive-unity.tests/PlayModeTests/Components/Goldens/ReferenceImages", captureGolden: false, maxResolution: 512, savedImageFormat: GoldenTestHelper.SavedImageFormatType.PNG);

        }

        [SetUp]
        public void Setup()
        {
            m_camera = new GameObject("Camera").AddComponent<Camera>();
            m_camera.tag = "MainCamera";

        }

        [TearDown]
        public void TearDown()
        {
            m_goldenHelper.Cleanup();
            m_testAssetLoadingManager.UnloadAllAssets();
            DestroyObj(m_camera.gameObject);

        }


        private IEnumerator SetupTestPanel(PanelScenario testCase)
        {
            RivePanel panel = null;
            yield return m_testAssetLoadingManager.LoadAssetCoroutine<GameObject>(
                testCase.PanelPrefabPath,
                (prefab) =>
                {
                    var panelObj = UnityEngine.Object.Instantiate(prefab);
                    panel = panelObj.GetComponent<RivePanel>();
                },
                () => Assert.Fail($"Failed to load panel prefab at {testCase.PanelPrefabPath}")
            );

            testCase.ConfigurePanel(panel);
            yield return panel;
        }

        [UnityTest]
        public IEnumerator Panel_RendersOnFirstFrame()
        {
            foreach (var testCase in GetTestCases())
            {
                var setupResult = SetupTestPanel(testCase);
                yield return setupResult;
                var panel = (RivePanel)setupResult.Current;

                // Wait for end of frame to ensure rendering has occurred.
                yield return new WaitForEndOfFrame();

                Assert.IsNotNull(panel.RenderTexture, "RenderTexture should be created on first frame");
                Assert.IsTrue(panel.RenderTexture.width > 0 && panel.RenderTexture.height > 0,
                    "RenderTexture should have valid dimensions");

                yield return m_goldenHelper.AssertWithRenderTexture(
                    testCase.GoldenId,
                    panel.RenderTexture
                );

                DestroyObj(panel.gameObject);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Panel_TransformChanges_MaintainsRenderTexture()
        {
            var transformTests = new (string name, Action<Transform> action)[]
            {
                // We check that the render texture remains the same after each transform change because if the RectTransform to RenderTransform conversion is working correctly, the positions of the RiveWidgets within the panel should not be affected by rotation, scale, or position changes.
                // Depending on how the conversion is done, if handled incorrectly, rotating the panel may cause widget positions to shift.
                (name: "RotateX", action: (Transform t) => t.rotation = Quaternion.Euler(45, 0, 0)),
                (name: "RotateY", action: (Transform t) => t.rotation = Quaternion.Euler(0, 45, 0)),
                (name: "RotateZ", action: (Transform t) => t.rotation = Quaternion.Euler(0, 0, 45)),
                (name: "RotateAll", action: (Transform t) => t.rotation = Quaternion.Euler(30, 45, 60)),
                (name: "Scale", action: (Transform t) => t.localScale = new Vector3(3f, 3f, 3f)),
                (name: "Position", action: (Transform t) => t.position = new Vector3(100, -50, 25))
            };

            // Only test the panel scenarios where IncludeInTransformChangeTests is true
            PanelScenario[] transformTestCases = GetTestCases().Where(testCase => testCase.IncludeInTransformChangeTests).ToArray();

            Assert.Greater(transformTestCases.Length, 0, "No test cases found with IncludeInTransformChangeTests set to true");



            foreach (var testCase in transformTestCases)
            {
                var setupResult = SetupTestPanel(testCase);
                yield return setupResult;
                var panel = (RivePanel)setupResult.Current;

                yield return new WaitForEndOfFrame();
                if (testCase.WaitCondition != null)
                {
                    yield return testCase.WaitCondition;
                }

                // Capture initial render
                yield return m_goldenHelper.AssertWithRenderTexture(
                    testCase.GoldenId,
                    panel.RenderTexture
                );

                // Test each transform change
                foreach (var test in transformTests)
                {
                    test.action(panel.transform);

                    // The render texture should remain unchanged
                    yield return m_goldenHelper.AssertWithRenderTexture(
                        testCase.GoldenId,
                        panel.RenderTexture
                    );
                }

                DestroyObj(panel.gameObject);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator AtlasStrategy_MultiplePanels_ShareSingleRenderTexture()
        {
            // Create and configure the atlas strategy
            var atlasStrategy = new GameObject("AtlasStrategy").AddComponent<AtlasRenderTargetStrategy>();
            atlasStrategy.Configure(
                startingSize: new Vector2Int(1048, 1024),
                maxAtlasSize: new Vector2Int(1048, 1024),
                maxResolutionPerPanel: 512,
                padding: 2
            );

            var panels = new List<RivePanel>();

            var testCases = GetTestCases().Take(3).ToArray();

            // Create multiple panels using our test cases
            foreach (var testCase in testCases)
            {
                var setupResult = SetupTestPanel(testCase);
                yield return setupResult;
                var panel = (RivePanel)setupResult.Current;

                // Assign the atlas strategy to each panel
                panel.RenderTargetStrategy = atlasStrategy;
                panels.Add(panel);

                if (testCase.WaitCondition != null)
                {
                    yield return testCase.WaitCondition;
                }

                panel.StartRendering();

                Assert.IsTrue(panel.IsRendering, "Panel should be rendering");
            }

            yield return null;

            // Wait for all panels to render
            yield return new WaitForEndOfFrame();

            // Verify that all panels are using the same render texture (the atlas)
            var atlasTexture = panels[0].RenderTexture;
            Assert.IsNotNull(atlasTexture, "Atlas texture should be created");

            foreach (var panel in panels)
            {
                Assert.AreEqual(atlasTexture, panel.RenderTexture,
                    "All panels should share the same atlas texture");
            }

            // Verify that the atlas contains all panel renders
            yield return m_goldenHelper.AssertWithRenderTexture(
                CustomTestCases.AtlasStrategy_Renders_MultiplePanels,
                atlasTexture
            );

            // Cleanup
            foreach (var panel in panels)
            {
                DestroyObj(panel.gameObject);
            }
            DestroyObj(atlasStrategy.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PooledStrategy_HandlesMultipleAspectRatios()
        {
            int maxPoolSize = 3;
            var pooledStrategy = new GameObject("PooledStrategy").AddComponent<PooledRenderTargetStrategy>();
            pooledStrategy.Configure(
                textureSize: new Vector2Int(512, 512),
                initialPoolSize: maxPoolSize,
                maxPoolSize: maxPoolSize,
                PooledRenderTargetStrategy.PoolOverflowBehavior.Fixed
            );

            var panels = new List<RivePanel>();

            // Test cases with different aspect ratios
            var aspectRatioTests = new (string prefabPath, Vector2 dimensions, string goldenId)[]
            {
                (TestPrefabReferences.RivePanelWithSingleWidget, new Vector2(1920, 1080), $"PooledStrategy_HandlesMultipleAspectRatios_{nameof(TestPrefabReferences.RivePanelWithSingleWidget)}_1920x1080"),
                (TestPrefabReferences.RivePanelWithSingleWidget, new Vector2(1080, 1920), $"PooledStrategy_HandlesMultipleAspectRatios_{nameof(TestPrefabReferences.RivePanelWithSingleWidget)}_1080x1920"),
                (TestPrefabReferences.RivePanelWithMultipleWidgets, new Vector2(1000, 1000), $"PooledStrategy_HandlesMultipleAspectRatios_{nameof(TestPrefabReferences.RivePanelWithMultipleWidgets)}_1000x1000"),
            };

            // We create panels with different aspect ratios
            foreach (var test in aspectRatioTests)
            {
                var setupResult = SetupTestPanel(new PanelScenario(
                    goldenId: test.goldenId,
                    panelPrefabPath: test.prefabPath,
                    configurePanel: (panel) =>
                    {
                        panel.SetDimensions(test.dimensions);
                        panel.RenderTargetStrategy = pooledStrategy;

                        if (test.prefabPath == TestPrefabReferences.RivePanelWithSingleWidget)
                        {
                            // Get the widget and set the fit mode to fill so we can see the effect of the different aspect ratios
                            var widget = panel.GetComponentInChildren<RiveWidget>();
                            widget.Fit = Fit.Fill;
                        }


                    }
                ));

                yield return setupResult;
                var panel = (RivePanel)setupResult.Current;
                panels.Add(panel);

                panel.StartRendering();
                Assert.IsTrue(panel.IsRendering, "Panel should be rendering");
            }

            yield return new WaitForEndOfFrame();

            // Verify all render textures are 512x512 (maxResolutionPerPanel)
            for (int i = 0; i < panels.Count; i++)
            {
                var panel = panels[i];
                Assert.IsNotNull(panel.RenderTexture, "Panel should have a render texture");
                Assert.AreEqual(512, panel.RenderTexture.width, "Render texture width should be 512");
                Assert.AreEqual(512, panel.RenderTexture.height, "Render texture height should be 512");

                yield return m_goldenHelper.AssertWithRenderTexture(
                    aspectRatioTests[i].goldenId,
                    panel.RenderTexture
                );

            }

            // Verify we're reusing textures from the pool (should have 3 or fewer unique textures)
            var uniqueTextures = panels
                .Select(p => p.RenderTexture)
                .Distinct()
                .Count();
            Assert.LessOrEqual(uniqueTextures, maxPoolSize, "Should not create more textures than pool size");

            // Cleanup
            foreach (var panel in panels)
            {
                DestroyObj(panel.gameObject);
            }
            DestroyObj(pooledStrategy.gameObject);
            yield return null;
        }

        /// <summary>
        /// Tests that setting inputs on the initial frame (in the OnLoad callback) affects the visuals
        /// </summary>
        /// <returns></returns>
        [UnityTest]
        public IEnumerator PanelWithInitialFrameInputs_AffectsVisuals()
        {
            // This example fires the "Ready" trigger, which displays the 'Ready' text on the artboard
            // It should be visible in the golden image

            var setupResult = SetupTestPanel(new PanelScenario(
                goldenId: "RivePanel_WithInitialFrameInputs_FireTrigger_AffectsVisuals",
                panelPrefabPath: TestPrefabReferences.RivePanelWithInitialFrameInputs,
                configurePanel: (panel) =>
                {
                    var widget = panel.GetComponentInChildren<RiveWidget>();
                    widget.OnWidgetStatusChanged += () =>
                    {
                        if (widget.Status == WidgetStatus.Loaded)
                        {
                            widget.StateMachine.GetTrigger("Ready")?.Fire();
                        }
                    };
                }
            ));
            yield return setupResult;
            var panel = (RivePanel)setupResult.Current;

            yield return new WaitForEndOfFrame();

            // Verify that the initial frame inputs have affected the visuals
            yield return m_goldenHelper.AssertWithRenderTexture(
                "RivePanel_WithInitialFrameInputs_FireTrigger_AffectsVisuals",
                panel.RenderTexture
            );

            DestroyObj(panel.gameObject);
            yield return null;
        }


        [UnityTest]
        public IEnumerator RivePanel_Supports_AssetSwapping()
        {
            var panelPrefabPath = TestPrefabReferences.RivePanelWithSingleWidget;

            // Spawn the panel
            RivePanel panel = null;
            yield return m_testAssetLoadingManager.LoadAssetCoroutine<GameObject>(
                panelPrefabPath,
                (prefab) =>
                {
                    var panelObj = UnityEngine.Object.Instantiate(prefab);
                    panel = panelObj.GetComponent<RivePanel>();
                    panel.SetDimensions(new Vector2(1920, 1080));
                },
                () => Assert.Fail($"Failed to load panel prefab at {panelPrefabPath}")
            );


            // Load the test rive file
            Asset riveAsset = null;

            string riveAssetPath = TestAssetReferences.riv_asset_swapping_test;

            yield return m_testAssetLoadingManager.LoadAssetCoroutine<Rive.Asset>(riveAssetPath,
             (asset) => riveAsset = asset,
             () => Assert.Fail($"Failed to load asset at {riveAssetPath}"));




            // Load the dog image asset we'll be swapping in
            string dogImagePath = TestAssetReferences.imageasset_dog1_asset_swapping;
            ImageOutOfBandAsset dogImageAsset = null;

            yield return m_testAssetLoadingManager.LoadAssetCoroutine<ImageOutOfBandAsset>(dogImagePath,
             (asset) => dogImageAsset = asset,
             () => Assert.Fail($"Failed to load asset at {dogImagePath}"));


            // <-- Panel setup -->
            var widget = panel.GetComponentInChildren<RiveWidget>();

            widget.Fit = Fit.Layout;



            // <-- Case 1 -->

            // First test without using the callback
            File file1 = File.Load(riveAsset);

            widget.Load(file1);

            yield return new WaitForEndOfFrame();

            // Test that it shows up on the first frame

            Assert.IsNotNull(panel.RenderTexture, "RenderTexture should be created on first frame with the images");

            string expectedDefaultImageGoldenId = "RivePanel_WithoutCallback_LoadsDefaultEmbeddedAndReferencedAssets";

            yield return m_goldenHelper.AssertWithRenderTexture(
                expectedDefaultImageGoldenId,
                panel.RenderTexture
            );

            dogImageAsset.Load();

            bool HandleReferencedAssetLoading(EmbeddedAssetReference assetReference)
            {
                // Set both images in the Rive file to the dog image
                if (assetReference is ImageEmbeddedAssetReference imageAssetReference)
                {
                    imageAssetReference.SetImage(dogImageAsset);
                    return true;
                }

                return false;
            }


            // <-- Case 2 -->

            File file2 = File.Load(riveAsset, HandleReferencedAssetLoading);
            widget.Load(file2);

            yield return null;

            yield return m_goldenHelper.AssertWithRenderTexture(
                "RivePanel_WithCallback_UpdatesImages_ForEmbeddedAndReferencedAssets",
                panel.RenderTexture
            );



            // <-- Case 3 -->

            // Test that handling the assets in the callback but not setting them works
            // We expect the images to be unset in the visual. We're making sure sure that embedded images are not shown if the user chooses not to set them in the callback.


            bool HandleReferencedAssetLoadingWithoutSetting(EmbeddedAssetReference assetReference)
            {
                return true;
            }

            File file3 = File.Load(riveAsset, HandleReferencedAssetLoadingWithoutSetting);
            widget.Load(file3);

            yield return null;

            yield return m_goldenHelper.AssertWithRenderTexture(
                "RivePanel_WithCallback_UnsetsImages_ForEmbeddedAndReferencedAssets",
                panel.RenderTexture
            );




            // <-- Case 4 -->
            // Finally, test that if the user loads the file with a callback but uses fallback assets, the fallback assets assigned in the unity editor are used if the user doesn't handle the assets in the callback.

            bool HandleReferencedAssetLoadingWithFallback(EmbeddedAssetReference assetReference)
            {
                return false;
            }

            File file4 = File.Load(riveAsset, HandleReferencedAssetLoadingWithFallback, fallbackToAssignedAssets: true);
            widget.Load(file4);

            yield return null;

            // It should return the same as the default image
            yield return m_goldenHelper.AssertWithRenderTexture(
                expectedDefaultImageGoldenId,
                panel.RenderTexture
            );




            // <-- Cleanup -->

            DestroyObj(panel.gameObject);
            file1?.Dispose();
            file2?.Dispose();
            file3?.Dispose();
            file4?.Dispose();

        }

        [UnityTest]
        public IEnumerator RivePanel_RemoveAndAddWidget_MaintainsVisuals()
        {
            var panelPrefabPath = TestPrefabReferences.RivePanelWithSingleWidget;
            RivePanel panel = null;

            yield return m_testAssetLoadingManager.LoadAssetCoroutine<GameObject>(
                panelPrefabPath,
                (prefab) =>
                {
                    var panelObj = UnityEngine.Object.Instantiate(prefab);
                    panel = panelObj.GetComponent<RivePanel>();
                    panel.SetDimensions(new Vector2(800, 600));
                },
                () => Assert.Fail($"Failed to load panel prefab at {panelPrefabPath}")
            );

            var widget = panel.GetComponentInChildren<RiveWidget>();

            Asset riveAsset = null;
            string riveAssetPath = TestAssetReferences.riv_sophiaHud;
            yield return m_testAssetLoadingManager.LoadAssetCoroutine<Rive.Asset>(
                riveAssetPath,
                (asset) => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {riveAssetPath}")
            );


            widget.Load(riveAsset);

            yield return new WaitForEndOfFrame();

            // Check that the texture has non-transparent content. When nothing is loaded in the widget, the texture is transparent.
            bool hasInitialContent = CheckRenderTextureHasContent(panel.RenderTexture);
            Assert.IsTrue(hasInitialContent, "Initial render texture should have visible content");



            // Remove the widget from panel
            widget.transform.SetParent(null);
            yield return null;
            yield return new WaitForEndOfFrame();

            Assert.IsFalse(panel.ContainsWidget(widget), "Widget should be unregistered after removal");

            // Check that the texture is transparent after removing the widget
            bool hasContentAfterRemove = CheckRenderTextureHasContent(panel.RenderTexture);
            Assert.IsFalse(hasContentAfterRemove, "Render texture should be transparent after removing widget");

            // Add the widget back to the panel
            widget.transform.SetParent(panel.transform);
            yield return null;
            yield return new WaitForEndOfFrame();

            Assert.IsTrue(panel.ContainsWidget(widget), "Widget should be automatically re-registered when added back to panel");

            // Check that the texture has non-transparent content after re-adding
            bool hasContentAfterReAdd = CheckRenderTextureHasContent(panel.RenderTexture);
            Assert.IsTrue(hasContentAfterReAdd, "Render texture should have visible content after re-adding widget");


            // Cleanup
            DestroyObj(panel.gameObject);
            yield return null;
        }


        /// <summary>
        /// Checks if the given RenderTexture has any non-transparent content.
        /// </summary>
        private static bool CheckRenderTextureHasContent(RenderTexture rt)
        {
            if (rt == null) return false;

            Texture2D temp = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            RenderTexture prevActive = RenderTexture.active;
            RenderTexture.active = rt;

            // Read the pixels
            temp.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            temp.Apply();
            RenderTexture.active = prevActive;

            // Check if any pixels have non-zero alpha
            UnityEngine.Color[] pixels = temp.GetPixels();
            bool hasContent = false;

            foreach (UnityEngine.Color pixel in pixels)
            {
                if (pixel.a > 0.01f)
                {
                    hasContent = true;
                    break;
                }
            }

            UnityEngine.Object.Destroy(temp);
            return hasContent;
        }

        [UnityTest]
        public IEnumerator DataBinding_InitialFrame_ShowsExpectedValues()
        {
            // Spawn the panel
            var panelPrefabPath = TestPrefabReferences.RivePanelWithSingleWidget;
            RivePanel panel = null;
            yield return m_testAssetLoadingManager.LoadAssetCoroutine<GameObject>(
                panelPrefabPath,
                (prefab) =>
                {
                    var panelObj = UnityEngine.Object.Instantiate(prefab);
                    panel = panelObj.GetComponent<RivePanel>();
                    panel.SetDimensions(new Vector2(800, 600));
                },
                () => Assert.Fail($"Failed to load panel prefab at {panelPrefabPath}")
            );

            // Load the test rive file with data binding
            Asset riveAsset = null;
            string riveAssetPath = TestAssetReferences.riv_asset_databinding_test;
            yield return m_testAssetLoadingManager.LoadAssetCoroutine<Rive.Asset>(
                riveAssetPath,
                (asset) => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {riveAssetPath}")
            );

            // Configure the widget to use data binding
            var widget = panel.GetComponentInChildren<RiveWidget>();
            File riveFile = File.Load(riveAsset);
            widget.Load(riveFile);
            widget.BindingMode = Components.RiveWidget.DataBindingMode.AutoBindDefault;

            // Wait for the widget to load
            yield return new WaitUntil(() => widget.Status == WidgetStatus.Loaded);
            yield return new WaitForEndOfFrame();

            // Verify the initial frame shows the default data binding values
            yield return m_goldenHelper.AssertWithRenderTexture(
                "RivePanel_DataBinding_InitialFrame",
                panel.RenderTexture
            );

            DestroyObj(panel.gameObject);
            riveFile?.Dispose();
            yield return null;
        }

        [UnityTest]
        public IEnumerator DataBinding_ChangingValues_InOnWidgetStatusChanged_AffectsVisuals_OnFirstFrame()
        {
            var panelPrefabPath = TestPrefabReferences.RivePanelWithSingleWidget;
            RivePanel panel = null;
            yield return m_testAssetLoadingManager.LoadAssetCoroutine<GameObject>(
                panelPrefabPath,
                (prefab) =>
                {
                    var panelObj = UnityEngine.Object.Instantiate(prefab);
                    panel = panelObj.GetComponent<RivePanel>();
                    panel.SetDimensions(new Vector2(800, 600));
                },
                () => Assert.Fail($"Failed to load panel prefab at {panelPrefabPath}")
            );

            Asset riveAsset = null;
            string riveAssetPath = TestAssetReferences.riv_asset_databinding_test;
            yield return m_testAssetLoadingManager.LoadAssetCoroutine<Rive.Asset>(
                riveAssetPath,
                (asset) => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {riveAssetPath}")
            );

            var widget = panel.GetComponentInChildren<RiveWidget>();
            File riveFile = File.Load(riveAsset);

            widget.OnWidgetStatusChanged += () =>
            {
                if (widget.Status == WidgetStatus.Loaded)
                {
                    var viewModelInstance = widget.StateMachine.ViewModelInstance;
                    Assert.IsNotNull(viewModelInstance, "Expected viewModelInstance to be set");

                    // Change string property
                    var nameProp = viewModelInstance.GetProperty<ViewModelInstanceStringProperty>("name");
                    if (nameProp != null)
                    {
                        nameProp.Value = "Golden Test User";
                    }

                    // Change number property
                    var ageProp = viewModelInstance.GetProperty<ViewModelInstanceNumberProperty>("age");
                    if (ageProp != null)
                    {
                        ageProp.Value = 99;
                    }

                    // Change boolean property
                    var agreedTermsProp = viewModelInstance.GetProperty<ViewModelInstanceBooleanProperty>("agreedToTerms");
                    if (agreedTermsProp != null)
                    {
                        agreedTermsProp.Value = true;
                    }

                    // Change color property
                    var favColorProp = viewModelInstance.GetProperty<ViewModelInstanceColorProperty>("favColor");
                    if (favColorProp != null)
                    {
                        favColorProp.Value = new UnityEngine.Color(1, 0, 0, 1); // Bright red
                    }

                    // Change enum property
                    var countryProp = viewModelInstance.GetProperty<ViewModelInstanceEnumProperty>("country");

                    Assert.IsNotNull(countryProp, "Expected countryProp to be a ViewModelInstanceEnumProperty");

                    countryProp.Value = "japan";


                    // Handle child view model properties
                    var favDrink = viewModelInstance.GetProperty<ViewModelInstance>("favDrink");
                    Assert.IsNotNull(favDrink, "Expected favDrink to be a ViewModelInstance");

                    var drinkTypeProp = favDrink.GetProperty<ViewModelInstanceStringProperty>("name");

                    Assert.IsNotNull(drinkTypeProp, "Expected drinkTypeProp to be a ViewModelInstanceStringProperty");

                    drinkTypeProp.Value = "Coffee";


                }
            };

            widget.Load(riveFile);
            widget.BindingMode = Components.RiveWidget.DataBindingMode.AutoBindDefault;

            // Wait for the widget to load and data to be set
            yield return new WaitUntil(() => widget.Status == WidgetStatus.Loaded);
            yield return new WaitForEndOfFrame();

            // Verify that changing values on initial frame affected the visuals
            yield return m_goldenHelper.AssertWithRenderTexture(
                "DataBinding_ChangingValues_InOnWidgetStatusChanged_AffectsVisuals_OnFirstFrame",
                panel.RenderTexture
            );

            DestroyObj(panel.gameObject);
            riveFile?.Dispose();
            yield return null;
        }

        [UnityTest]
        public IEnumerator DataBinding_DifferentInstance_ShowsExpectedVisuals()
        {
            var panelPrefabPath = TestPrefabReferences.RivePanelWithSingleWidget;
            RivePanel panel = null;
            yield return m_testAssetLoadingManager.LoadAssetCoroutine<GameObject>(
                panelPrefabPath,
                (prefab) =>
                {
                    var panelObj = UnityEngine.Object.Instantiate(prefab);
                    panel = panelObj.GetComponent<RivePanel>();
                    panel.SetDimensions(new Vector2(800, 600));
                },
                () => Assert.Fail($"Failed to load panel prefab at {panelPrefabPath}")
            );

            // Load the test rive file with data binding
            Asset riveAsset = null;
            string riveAssetPath = TestAssetReferences.riv_asset_databinding_test;
            yield return m_testAssetLoadingManager.LoadAssetCoroutine<Rive.Asset>(
                riveAssetPath,
                (asset) => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {riveAssetPath}")
            );

            // Get multiple widgets from the panel
            var widget = panel.GetComponentInChildren<RiveWidget>();

            // Instead of the default instance (Steve), let's use the "Jane" instance
            File riveFile = File.Load(riveAsset);
            widget.BindingMode = Components.RiveWidget.DataBindingMode.AutoBindSelected;
            widget.ViewModelInstanceName = "Jane";
            widget.Load(riveFile);


            // Wait for the widget to load and data to be set
            yield return new WaitUntil(() => widget.Status == WidgetStatus.Loaded);
            yield return new WaitForEndOfFrame();


            // Verify that the instance shows different initial values from the default
            yield return m_goldenHelper.AssertWithRenderTexture(
                "RivePanel_DataBinding_DifferentInstance_ShowsExpectedVisuals",
                panel.RenderTexture
            );

            DestroyObj(panel.gameObject);
            riveFile?.Dispose();
            yield return null;
        }

        private static RenderTexture CropTopHalf(RenderTexture original)
        {
            int width = original.width;
            int height = original.height / 2;

            // Read the top‑half pixels (Unity's ReadPixels origin is bottom‑left)
            var prevActive = RenderTexture.active;
            RenderTexture.active = original;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, height, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = prevActive;

            // Blit into a new temporary RT and return it
            var tmpRT = RenderTexture.GetTemporary(width, height, 0, original.format);
            Graphics.Blit(tex, tmpRT);
            UnityEngine.Object.Destroy(tex);
            return tmpRT;
        }

        /// <summary>
        /// We test that the initial values set when the widget status is Loaded show up on the initial frame, regardless of whether the values were set in OnWidgetStatusChanged, Start, or Update.
        /// This is important because we want to ensure that the initial values are set correctly as long as the widget is loaded.
        /// </summary>
        [UnityTest]
        public IEnumerator DataBinding_InitialValues_SetWhenWidgetStatusLoaded_AreConsistent()
        {
            // Test setting data binding values in these three different ways
            var setupMethods = new[]
            {
                ("OnWidgetStatusChanged", RewardsDataBindingTester.SetMethod.OnWidgetStatusChanged),
                ("Start", RewardsDataBindingTester.SetMethod.Start),
                ("Update", RewardsDataBindingTester.SetMethod.Update)
            };

            // Test both attaching the data binding tester to the panel and directly to the widget
            // We do this because the order of operations for methods like Update() in the panel and widget may be different, and we want to ensure that the results are consistent regardless of where the data binding tester is attached.
            var attachmentModes = new[]
            {
                RewardsDataBindingTester.AttachmentMode.ToPanel,
                RewardsDataBindingTester.AttachmentMode.ToWidget
            };

            // We're using a single golden ID for all tests since the results should be identical
            string goldenId = "DataBinding_InitialValues_SetMethods_Consistent";

            ImageComparisonSettings imageComparisonSettings = new ImageComparisonSettings
            {
                PerPixelCorrectnessThreshold = 0.1f,
                PerPixelGammaThreshold = 0.1f,
                IncorrectPixelsThreshold = 0.07f,

                ActivePixelTests = ImageComparisonSettings.PixelTests.DeltaE | ImageComparisonSettings.PixelTests.DeltaAlpha | ImageComparisonSettings.PixelTests.DeltaGamma,
                ActiveImageTests = ImageComparisonSettings.ImageTests.IncorrectPixelsCount
            };

            // Test each combination of setup method and attachment mode
            foreach (var attachmentMode in attachmentModes)
            {
                foreach (var (methodName, method) in setupMethods)
                {
                    RivePanel panel = null;
                    RewardsDataBindingTester rewardsDataBindingTester = null;

                    yield return m_testAssetLoadingManager.LoadAssetCoroutine<GameObject>(
                        TestPrefabReferences.RivePanelWithSingleWidgetAndLayout,
                        (prefab) =>
                        {
                            var panelObj = UnityEngine.Object.Instantiate(prefab);
                            panel = panelObj.GetComponent<RivePanel>();
                            panel.SetDimensions(new Vector2(800, 600));

                            var widget = panel.GetComponentInChildren<RiveWidget>();
                            // Destroy the default widget and add a new one
                            DestroyObj(widget);
                            widget = panelObj.AddComponent<RiveWidget>();
                            widget.transform.SetParent(panelObj.transform, false);
                            widget.ScaleFactor = 9.37f; // We just want to focus on the text for this test.
                            widget.Fit = Fit.Layout;


                            if (attachmentMode == RewardsDataBindingTester.AttachmentMode.ToPanel)
                            {
                                rewardsDataBindingTester = panelObj.AddComponent<RewardsDataBindingTester>();
                            }
                            else
                            {
                                rewardsDataBindingTester = widget.gameObject.AddComponent<RewardsDataBindingTester>();
                            }

                            rewardsDataBindingTester.SetMethodToUse(method);
                            rewardsDataBindingTester.RiveWidget = widget;
                        },
                        () => Assert.Fail($"Failed to load panel prefab")
                    );

                    Asset riveAsset = null;
                    yield return m_testAssetLoadingManager.LoadAssetCoroutine<Rive.Asset>(
                        TestAssetReferences.riv_rewards_db,
                        (asset) => riveAsset = asset,
                        () => Assert.Fail($"Failed to load Rewards asset")
                    );

                    var widget = panel.GetComponentInChildren<RiveWidget>();
                    File riveFile = File.Load(riveAsset);
                    widget.Load(riveFile);
                    widget.BindingMode = Components.RiveWidget.DataBindingMode.AutoBindDefault;

                    yield return rewardsDataBindingTester.IsComplete();
                    yield return new WaitForEndOfFrame();

                    // We're cropping to the top half of the image to avoid subtle animation differences in the bottom half of the image that are not relevant to the test.
                    // We want to verify that the number/text values are set correctly, not the animation state of the widget.
                    RenderTexture croppedRT = CropTopHalf(panel.RenderTexture);
                    yield return m_goldenHelper.AssertWithRenderTexture(
                      goldenId,
                      croppedRT,
                      imageComparisonSettings
                    );
                    RenderTexture.ReleaseTemporary(croppedRT);

                    DestroyObj(panel.gameObject);
                    riveFile?.Dispose();
                    yield return null;
                }
            }
        }

        private void DestroyObj(UnityEngine.Object obj)
        {
            if (obj != null)
            {
                UnityEngine.Object.Destroy(obj);
            }
        }
    }

    /// <summary>
    /// Custom component to test setting rewards values in Start, Update, or OnWidgetStatusChanged.
    /// This component is added to the panel prefab and is used to set the rewards values in the Rive widget.
    /// </summary>
    public class RewardsDataBindingTester : MonoBehaviour
    {
        /// <summary>
        /// Enum to determine which method to use for setting the rewards values.
        /// </summary>
        public enum SetMethod
        {
            OnWidgetStatusChanged,
            Start,
            Update
        }

        /// <summary>
        /// Enum to determine where the RewardsDataBindingTester is attached.
        /// </summary>
        public enum AttachmentMode
        {
            ToPanel,
            ToWidget
        }

        [SerializeField] private RiveWidget m_riveWidget;
        private SetMethod m_methodToUse = SetMethod.OnWidgetStatusChanged;

        public RiveWidget RiveWidget
        {
            get => m_riveWidget;
            set
            {
                if (m_riveWidget != null)
                {
                    m_riveWidget.OnWidgetStatusChanged -= HandleWidgetStatusChanged;
                }
                m_riveWidget = value;

                if (m_methodToUse == SetMethod.OnWidgetStatusChanged)
                {
                    m_riveWidget.OnWidgetStatusChanged += HandleWidgetStatusChanged;
                }
            }
        }

        public void SetMethodToUse(SetMethod method)
        {
            m_methodToUse = method;
        }

        private void OnEnable()
        {

            if (m_methodToUse == SetMethod.OnWidgetStatusChanged && m_riveWidget != null)
            {
                m_riveWidget.OnWidgetStatusChanged += HandleWidgetStatusChanged;
            }
        }

        private void OnDisable()
        {
            if (m_methodToUse == SetMethod.OnWidgetStatusChanged)
            {
                m_riveWidget.OnWidgetStatusChanged -= HandleWidgetStatusChanged;
            }
        }

        private void HandleWidgetStatusChanged()
        {
            if (m_riveWidget.Status == WidgetStatus.Loaded && m_methodToUse == SetMethod.OnWidgetStatusChanged)
            {
                SetRewardsValues();
            }
        }

        private IEnumerator Start()
        {
            if (m_methodToUse == SetMethod.Start)
            {
                if (m_riveWidget.Status == WidgetStatus.Loaded)
                {
                    SetRewardsValues();

                }
                else
                {
                    // We need to wait until the widget is loaded
                    yield return new WaitUntil(() => m_riveWidget.Status == WidgetStatus.Loaded);
                    SetRewardsValues();
                }
            }

        }

        private bool m_hasSetValues = false;

        private void Update()
        {
            if (m_methodToUse == SetMethod.Update && !m_hasSetValues)
            {
                if (m_riveWidget.Status == WidgetStatus.Loaded)
                {
                    SetRewardsValues();
                }
            }
        }

        public IEnumerator IsComplete()
        {
            float timeout = 5.0f;
            float startTime = Time.time;

            while (!m_hasSetValues && Time.time - startTime < timeout)
            {
                yield return null;
            }

            if (!m_hasSetValues)
            {
                DebugLogger.Instance.LogWarning($"Timed out waiting for rewards values to be set using method {m_methodToUse}");
            }

            // To account for the fact that this update might be called after the panel has ticked/advanced the widget, so the values wouldn't show up until the next frame.
            if (m_methodToUse == SetMethod.Update)
            {
                yield return null;
            }
        }

        private void SetRewardsValues()
        {

            var viewModelInstance = m_riveWidget.StateMachine.ViewModelInstance;
            if (viewModelInstance == null)
            {
                Debug.LogError("ViewModelInstance is null. Cannot set rewards values.");
                return;
            }

            var coinProp = viewModelInstance.GetNumberProperty("Coin/Item_Value");
            if (coinProp != null) coinProp.Value = 250;

            var gemProp = viewModelInstance.GetNumberProperty("Gem/Item_Value");
            if (gemProp != null) gemProp.Value = 50;

            // We force the icon change so that our image comparison works better
            var itemProp = viewModelInstance.GetEnumProperty("Coin/Property_Of_Item/Item_Selection");
            if (itemProp != null) itemProp.Value = "Gem";

            m_hasSetValues = true;

        }
    }
}


#endif