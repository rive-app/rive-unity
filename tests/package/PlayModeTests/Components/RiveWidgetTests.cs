using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using Rive.Components;
using System.Linq;
using Rive.Tests.Utils;
using Rive.Utils;
using System;
using NUnit.Framework;
using System.Collections.Generic;

namespace Rive.Tests
{
    public class RiveWidgetTests
    {
        private GameObject m_widgetObject;
        private RiveWidget m_widget;
        private MockLogger mockLogger;
        private TestAssetLoadingManager testAssetLoadingManager;

        private RivePanel m_panel;

        private List<File> m_loadedFiles;

        public class ExpectedEventData
        {
            public string EventName { get; set; }
            public Action<RiveWidget> TriggerAction { get; set; }
        }


        public class HitTestScenario
        {
            public enum PointerEventType
            {
                PointerDown = 0,
                PointerUp = 1,
                PointerMove = 2,
                HitTest = 3
            }

            /// <summary>
            /// The size of the panel containing the widget. The widget will fill the panel.
            /// </summary>
            public Vector2Int PanelSize { get; set; }

            /// <summary>
            /// The point to hit test against the widget
            /// </summary>
            public Vector2 HitTestPoint { get; set; }

            /// <summary>
            /// The expected result of the hit test. True if the point is expected to hit the widget, false otherwise.
            /// </summary>
            public bool ExpectedResult { get; set; }

            /// <summary>
            /// The fit mode to use for the widget
            /// </summary>
            public Fit FitMode { get; set; } = Fit.Contain;

            /// <summary>
            /// The alignment of the widget within the panel
            /// </summary>
            public Alignment Alignment { get; set; } = Alignment.Center;

            /// <summary>
            /// The type of pointer event to simulate
            /// </summary>
            public PointerEventType PointerEvent { get; set; } = PointerEventType.PointerDown;


            public float LayoutScaleFactor { get; set; } = 1.0f;

            public LayoutScalingMode LayoutScalingMode { get; set; } = LayoutScalingMode.ReferenceArtboardSize;

            public HitTestScenario(Vector2Int panelSize, Vector2 hitTestPoint, bool expectedResult, Fit fitMode, Alignment alignment, PointerEventType pointerEvent, float layoutScaleFactor = 1.0f, LayoutScalingMode layoutScalingMode = LayoutScalingMode.ReferenceArtboardSize)
            {
                PanelSize = panelSize;
                HitTestPoint = hitTestPoint;
                ExpectedResult = expectedResult;
                FitMode = fitMode;
                Alignment = alignment;
                PointerEvent = pointerEvent;
                LayoutScaleFactor = layoutScaleFactor;
                LayoutScalingMode = layoutScalingMode;
            }
        }

        public class TestAssetData
        {
            public string addressableAssetPath;
            public string defaultArtboardName;
            public string defaultStateMachineName;


            public HitTestScenario[] hitTestScenarios;

            // Each event data contains the expected event name and how to trigger it
            public ExpectedEventData[] expectedEvents;
        }

        /// <summary>
        /// Return test asset data for widget testing
        /// </summary>
        protected virtual TestAssetData[] GetTestAssetInfo()
        {

            return new TestAssetData[]
            {
                new TestAssetData
                {
                    addressableAssetPath = TestAssetReferences.riv_sophiaHud,
                    defaultArtboardName = "SOPHIA III HUD",
                    defaultStateMachineName = "SOPHIA_III_DASHBOARD_SEQ",
                    hitTestScenarios = new HitTestScenario[]
                    {
                        // EXPECTED VALID POINTS
                        // This riv file's entire artboard is a hitbox
                        new HitTestScenario(panelSize: new Vector2Int(1920, 1080), hitTestPoint: new Vector2(0.50f, 0.45f), expectedResult: true, fitMode: Fit.Cover, alignment: Alignment.Center, pointerEvent: HitTestScenario.PointerEventType.PointerDown),
                        new HitTestScenario(panelSize: new Vector2Int(1920, 1080), hitTestPoint: new Vector2(0.48f, 0.35f), expectedResult: true, fitMode: Fit.Cover, alignment: Alignment.Center, pointerEvent: HitTestScenario.PointerEventType.PointerDown),

                    },

                },
                new TestAssetData
                {
                    addressableAssetPath = TestAssetReferences.riv_cleanTheCar,
                    defaultArtboardName = "Car",
                    defaultStateMachineName = "Motion",
                    hitTestScenarios = new HitTestScenario[]
                    {
                         // EXPECTED VALID POINTS
                        new HitTestScenario(panelSize: new Vector2Int(1920, 1080), hitTestPoint: new Vector2(0.47f, 0.42f), expectedResult: true, fitMode: Fit.Contain, alignment: Alignment.Center, pointerEvent: HitTestScenario.PointerEventType.PointerMove),
                        new HitTestScenario(panelSize: new Vector2Int(1920, 1080), hitTestPoint: new Vector2(0.44f, 0.56f), expectedResult: true, fitMode: Fit.Contain, alignment: Alignment.Center, pointerEvent: HitTestScenario.PointerEventType.PointerUp),
                        new HitTestScenario(panelSize: new Vector2Int(1920, 1080), hitTestPoint: new Vector2(0.44f, 0.56f), expectedResult: true, fitMode: Fit.Contain, alignment: Alignment.Center, pointerEvent: HitTestScenario.PointerEventType.PointerDown),

                        new HitTestScenario(panelSize: new Vector2Int(1920, 1080), hitTestPoint: new Vector2(0.47f, 0.42f), expectedResult: true, fitMode: Fit.Contain, alignment: Alignment.Center, pointerEvent: HitTestScenario.PointerEventType.HitTest),
                        new HitTestScenario(panelSize: new Vector2Int(1920, 1080), hitTestPoint: new Vector2(0.44f, 0.56f), expectedResult: true, fitMode: Fit.Contain, alignment: Alignment.Center, pointerEvent: HitTestScenario.PointerEventType.HitTest),

                        // EXPECTED INVALID POINTS
                        new HitTestScenario(panelSize: new Vector2Int(1920, 1080), hitTestPoint: new Vector2(0.15f, 0.20f), expectedResult: false, fitMode: Fit.Contain, alignment: Alignment.Center, pointerEvent: HitTestScenario.PointerEventType.PointerUp),
                        new HitTestScenario(panelSize: new Vector2Int(1920, 1080), hitTestPoint: new Vector2(0.48f, 0.01f), expectedResult: false, fitMode: Fit.Contain, alignment: Alignment.Center, pointerEvent: HitTestScenario.PointerEventType.PointerMove),
                        new HitTestScenario(panelSize: new Vector2Int(1920, 1080), hitTestPoint: new Vector2(0.40f, 0.54f), expectedResult: false, fitMode: Fit.Contain, alignment: Alignment.Center, pointerEvent: HitTestScenario.PointerEventType.PointerDown),

                        new HitTestScenario(panelSize: new Vector2Int(1920, 1080), hitTestPoint: new Vector2(0.40f, 0.54f), expectedResult: false, fitMode: Fit.Contain, alignment: Alignment.Center, pointerEvent: HitTestScenario.PointerEventType.HitTest),
                        new HitTestScenario(panelSize: new Vector2Int(1920, 1080), hitTestPoint: new Vector2(0.48f, 0.01f), expectedResult: false, fitMode: Fit.Contain, alignment: Alignment.Center, pointerEvent: HitTestScenario.PointerEventType.HitTest)


                    },

                },
                new TestAssetData
                {
                    addressableAssetPath = TestAssetReferences.riv_ratingAnimationWithEvents,
                    defaultArtboardName = "New Artboard",
                    defaultStateMachineName = "State Machine 1",
                    hitTestScenarios = new HitTestScenario[]
                    {
                         // EXPECTED VALID POINTS

                        new HitTestScenario(panelSize: new Vector2Int(1920, 1080), hitTestPoint: new Vector2(0.34f, 0.50f), expectedResult: true, fitMode: Fit.Contain, alignment: Alignment.Center, pointerEvent: HitTestScenario.PointerEventType.PointerDown), // First star
                        new HitTestScenario(panelSize: new Vector2Int(1920, 1080), hitTestPoint: new Vector2(0.43f, 0.50f), expectedResult: true, fitMode: Fit.Contain, alignment: Alignment.Center, pointerEvent: HitTestScenario.PointerEventType.PointerDown), // Second star
                        new HitTestScenario(panelSize: new Vector2Int(1920, 1080), hitTestPoint: new Vector2(0.51f, 0.50f), expectedResult: true, fitMode: Fit.Contain, alignment: Alignment.Center, pointerEvent: HitTestScenario.PointerEventType.PointerDown), // Third star
                        new HitTestScenario(panelSize: new Vector2Int(1920, 1080), hitTestPoint: new Vector2(0.59f, 0.50f), expectedResult: true, fitMode: Fit.Contain, alignment: Alignment.Center, pointerEvent: HitTestScenario.PointerEventType.PointerDown), // Fourth star
                        new HitTestScenario(panelSize: new Vector2Int(1920, 1080), hitTestPoint: new Vector2(0.66f, 0.50f), expectedResult: true, fitMode: Fit.Contain, alignment: Alignment.Center, pointerEvent: HitTestScenario.PointerEventType.PointerDown), // Fifth star

                        new HitTestScenario(panelSize: new Vector2Int(1920, 1080), hitTestPoint: new Vector2(0.34f, 0.50f), expectedResult: true, fitMode: Fit.Contain, alignment: Alignment.Center, pointerEvent: HitTestScenario.PointerEventType.HitTest), // First star HitTest should be true
                        new HitTestScenario(panelSize: new Vector2Int(1920, 1080), hitTestPoint: new Vector2(0.66f, 0.50f), expectedResult: true, fitMode: Fit.Contain, alignment: Alignment.Center, pointerEvent: HitTestScenario.PointerEventType.HitTest), // Fifth star HitTest should be true

                        // EXPECTED INVALID POINTS
                        new HitTestScenario(panelSize: new Vector2Int(1920, 1080), hitTestPoint: new Vector2(0.46f, 0.76f), expectedResult: false, fitMode: Fit.Contain, alignment: Alignment.Center, pointerEvent: HitTestScenario.PointerEventType.PointerUp),
                        new HitTestScenario(panelSize: new Vector2Int(1920, 1080), hitTestPoint: new Vector2(0.65f, 0.06f), expectedResult: false, fitMode: Fit.Contain, alignment: Alignment.Center, pointerEvent: HitTestScenario.PointerEventType.PointerMove),
                        new HitTestScenario(panelSize: new Vector2Int(1920, 1080), hitTestPoint: new Vector2(0.24f, 0.06f), expectedResult: false, fitMode: Fit.Contain, alignment: Alignment.Center, pointerEvent: HitTestScenario.PointerEventType.PointerDown)

                    },
                    expectedEvents = new ExpectedEventData[]
                    {
                        new ExpectedEventData
                        {
                            // Triggered when star 1 is clicked
                            EventName = "rating1",
                            TriggerAction = (widget) => {

                                widget.StateMachine.GetNumber("rating").Value = 1;

                            }
                        },
                        new ExpectedEventData
                        {
                            // Triggered when star 2 is clicked
                            EventName = "rating2",
                            TriggerAction = (widget) => {
                                widget.StateMachine.GetNumber("rating").Value = 2;

                                }
                        },
                        new ExpectedEventData
                        {
                            // Triggered when star 3 is clicked
                            EventName = "rating3",
                            TriggerAction = (widget) =>{
                                                                    widget.StateMachine.GetNumber("rating").Value = 3;

                            }
                        },
                        new ExpectedEventData
                        {
                            // Triggered when star 4 is clicked
                            EventName = "rating4",
                            TriggerAction = (widget) => {
                                widget.StateMachine.GetNumber("rating").Value = 4;
                            }
                        },
                        new ExpectedEventData
                        {
                            // Triggered when star 5 is clicked
                            EventName = "gotorive",
                            TriggerAction = (widget) => {
                                widget.StateMachine.GetNumber("rating").Value = 5;

                            }
                        }
                    }
                },
                 new TestAssetData
                {
                    addressableAssetPath = TestAssetReferences.riv_layoutTest,
                    defaultArtboardName = "Artboard",
                    defaultStateMachineName = "State Machine 1",
                    hitTestScenarios = new HitTestScenario[]
                    {
                         // EXPECTED VALID POINTS
                       
                       // ScalingMode: ReferenceArtboardSize: Landscape

                        new HitTestScenario(panelSize: new Vector2Int(1920, 1080), hitTestPoint: new Vector2(0.13f, 0.94f), expectedResult: true, fitMode: Fit.Layout, alignment: Alignment.Center, pointerEvent: HitTestScenario.PointerEventType.PointerDown, layoutScalingMode: LayoutScalingMode.ReferenceArtboardSize), // Button 1
                        new HitTestScenario(panelSize: new Vector2Int(1920, 1080), hitTestPoint: new Vector2(0.26f, 0.94f), expectedResult: true, fitMode: Fit.Layout, alignment: Alignment.Center, pointerEvent: HitTestScenario.PointerEventType.PointerDown, layoutScalingMode: LayoutScalingMode.ReferenceArtboardSize), // Button 2
                        new HitTestScenario(panelSize: new Vector2Int(1920, 1080), hitTestPoint: new Vector2(0.49f, 0.92f), expectedResult: true, fitMode: Fit.Layout, alignment: Alignment.Center, pointerEvent: HitTestScenario.PointerEventType.PointerDown, layoutScalingMode: LayoutScalingMode.ReferenceArtboardSize), // Button 3
                        new HitTestScenario(panelSize: new Vector2Int(1920, 1080), hitTestPoint: new Vector2(0.70f, 0.92f), expectedResult: true, fitMode: Fit.Layout, alignment: Alignment.Center, pointerEvent: HitTestScenario.PointerEventType.PointerDown, layoutScalingMode: LayoutScalingMode.ReferenceArtboardSize), // Button 4
                         new HitTestScenario(panelSize: new Vector2Int(1920, 1080), hitTestPoint: new Vector2(0.89f, 0.94f), expectedResult: true, fitMode: Fit.Layout, alignment: Alignment.Center, pointerEvent: HitTestScenario.PointerEventType.PointerDown, layoutScalingMode: LayoutScalingMode.ReferenceArtboardSize), // Button 5
                       
                       // Test different scale factor values
                        new HitTestScenario(panelSize: new Vector2Int(1080, 1920), hitTestPoint: new Vector2(0.49f, 0.86f), expectedResult: true, fitMode: Fit.Layout, alignment: Alignment.Center, pointerEvent: HitTestScenario.PointerEventType.PointerDown, layoutScalingMode: LayoutScalingMode.ReferenceArtboardSize, layoutScaleFactor: 2f), // Button 1
                        new HitTestScenario(panelSize: new Vector2Int(1080, 1920), hitTestPoint: new Vector2(0.14f, 0.96f), expectedResult: true, fitMode: Fit.Layout, alignment: Alignment.Center, pointerEvent: HitTestScenario.PointerEventType.PointerDown, layoutScalingMode: LayoutScalingMode.ReferenceArtboardSize, layoutScaleFactor: 0.5f), // Button 1


                       // ScalingMode: ReferenceArtboardSize: Portrait

                        new HitTestScenario(panelSize: new Vector2Int(1080, 1920), hitTestPoint: new Vector2(0.50f, 0.94f), expectedResult: true, fitMode: Fit.Layout, alignment: Alignment.Center, pointerEvent: HitTestScenario.PointerEventType.PointerDown, layoutScalingMode: LayoutScalingMode.ReferenceArtboardSize), // Button 1
                        new HitTestScenario(panelSize: new Vector2Int(1080, 1920), hitTestPoint: new Vector2(0.50f, 0.81f), expectedResult: true, fitMode: Fit.Layout, alignment: Alignment.Center, pointerEvent: HitTestScenario.PointerEventType.PointerDown, layoutScalingMode: LayoutScalingMode.ReferenceArtboardSize), // Button 2
                        new HitTestScenario(panelSize: new Vector2Int(1080, 1920), hitTestPoint: new Vector2(0.50f, 0.70f), expectedResult: true, fitMode: Fit.Layout, alignment: Alignment.Center, pointerEvent: HitTestScenario.PointerEventType.PointerDown, layoutScalingMode: LayoutScalingMode.ReferenceArtboardSize), // Button 3
                        new HitTestScenario(panelSize: new Vector2Int(1080, 1920), hitTestPoint: new Vector2(0.50f, 0.59f), expectedResult: true, fitMode: Fit.Layout, alignment: Alignment.Center, pointerEvent: HitTestScenario.PointerEventType.PointerDown, layoutScalingMode: LayoutScalingMode.ReferenceArtboardSize), // Button 4
                        new HitTestScenario(panelSize: new Vector2Int(1080, 1920), hitTestPoint: new Vector2(0.50f, 0.48f), expectedResult: true, fitMode: Fit.Layout, alignment: Alignment.Center, pointerEvent: HitTestScenario.PointerEventType.PointerDown, layoutScalingMode: LayoutScalingMode.ReferenceArtboardSize), // Button 5


                        // ScalingMode: ConstantPixelSize 
                        new HitTestScenario(panelSize: new Vector2Int(1920, 1080), hitTestPoint: new Vector2(0.12f, 0.97f), expectedResult: true, fitMode: Fit.Layout, alignment: Alignment.Center, pointerEvent: HitTestScenario.PointerEventType.PointerDown, layoutScalingMode: LayoutScalingMode.ConstantPixelSize), // Button 1
                        new HitTestScenario(panelSize: new Vector2Int(1920, 1080), hitTestPoint: new Vector2(0.90f, 0.97f), expectedResult: true, fitMode: Fit.Layout, alignment: Alignment.Center, pointerEvent: HitTestScenario.PointerEventType.PointerDown, layoutScalingMode: LayoutScalingMode.ConstantPixelSize), // Button 5

                        new HitTestScenario(panelSize: new Vector2Int(2560, 1440), hitTestPoint: new Vector2(0.12f, 0.97f), expectedResult: true, fitMode: Fit.Layout, alignment: Alignment.Center, pointerEvent: HitTestScenario.PointerEventType.PointerDown, layoutScalingMode: LayoutScalingMode.ConstantPixelSize), // Button 1
                        new HitTestScenario(panelSize: new Vector2Int(2560, 1440), hitTestPoint: new Vector2(0.90f, 0.97f), expectedResult: true, fitMode: Fit.Layout, alignment: Alignment.Center, pointerEvent: HitTestScenario.PointerEventType.PointerDown, layoutScalingMode: LayoutScalingMode.ConstantPixelSize), // Button 5

                        new HitTestScenario(panelSize: new Vector2Int(960, 600), hitTestPoint: new Vector2(0.12f, 0.97f), expectedResult: true, fitMode: Fit.Layout, alignment: Alignment.Center, pointerEvent: HitTestScenario.PointerEventType.PointerDown, layoutScalingMode: LayoutScalingMode.ConstantPixelSize), // Button 1
                        new HitTestScenario(panelSize: new Vector2Int(960, 600), hitTestPoint: new Vector2(0.90f, 0.97f), expectedResult: true, fitMode: Fit.Layout, alignment: Alignment.Center, pointerEvent: HitTestScenario.PointerEventType.PointerDown, layoutScalingMode: LayoutScalingMode.ConstantPixelSize), // Button 5


                         // ScalingMode: ConstantPhysicalSize 
                        new HitTestScenario(panelSize: new Vector2Int(1920, 1080), hitTestPoint: new Vector2(0.12f, 0.94f), expectedResult: true, fitMode: Fit.Layout, alignment: Alignment.Center, pointerEvent: HitTestScenario.PointerEventType.PointerDown, layoutScalingMode: LayoutScalingMode.ConstantPhysicalSize), // Button 1
                        new HitTestScenario(panelSize: new Vector2Int(1920, 1080), hitTestPoint: new Vector2(0.90f, 0.97f), expectedResult: true, fitMode: Fit.Layout, alignment: Alignment.Center, pointerEvent: HitTestScenario.PointerEventType.PointerDown, layoutScalingMode: LayoutScalingMode.ConstantPhysicalSize), // Button 5

                        new HitTestScenario(panelSize: new Vector2Int(2560, 1440), hitTestPoint: new Vector2(0.12f, 0.97f), expectedResult: true, fitMode: Fit.Layout, alignment: Alignment.Center, pointerEvent: HitTestScenario.PointerEventType.PointerDown, layoutScalingMode: LayoutScalingMode.ConstantPhysicalSize), // Button 1
                        new HitTestScenario(panelSize: new Vector2Int(2560, 1440), hitTestPoint: new Vector2(0.90f, 0.97f), expectedResult: true, fitMode: Fit.Layout, alignment: Alignment.Center, pointerEvent: HitTestScenario.PointerEventType.PointerDown, layoutScalingMode: LayoutScalingMode.ConstantPhysicalSize), // Button 5

                        new HitTestScenario(panelSize: new Vector2Int(960, 600), hitTestPoint: new Vector2(0.11f, 0.88f), expectedResult: true, fitMode: Fit.Layout, alignment: Alignment.Center, pointerEvent: HitTestScenario.PointerEventType.PointerDown, layoutScalingMode: LayoutScalingMode.ConstantPhysicalSize), // Button 1
                        new HitTestScenario(panelSize: new Vector2Int(960, 600), hitTestPoint: new Vector2(0.89f, 0.89f), expectedResult: true, fitMode: Fit.Layout, alignment: Alignment.Center, pointerEvent: HitTestScenario.PointerEventType.PointerDown, layoutScalingMode: LayoutScalingMode.ConstantPhysicalSize), // Button 5

                        // EXPECTED INVALID POINTS
                        new HitTestScenario(panelSize: new Vector2Int(1920, 1080), hitTestPoint: new Vector2(0.58f, 0.43f), expectedResult: false, fitMode: Fit.Layout, alignment: Alignment.Center, pointerEvent: HitTestScenario.PointerEventType.PointerDown),
                        new HitTestScenario(panelSize: new Vector2Int(1920, 1080), hitTestPoint: new Vector2(0.99f, 0.25f), expectedResult: false, fitMode: Fit.Contain, alignment: Alignment.Center, pointerEvent: HitTestScenario.PointerEventType.PointerMove),
                        new HitTestScenario(panelSize: new Vector2Int(1920, 1080), hitTestPoint: new Vector2(0.31f, 0.19f), expectedResult: false, fitMode: Fit.Contain, alignment: Alignment.Center, pointerEvent: HitTestScenario.PointerEventType.PointerUp)

                    },

                },

            };
        }

        [SetUp]
        public void Setup()
        {
            mockLogger = new MockLogger();
            DebugLogger.Instance = mockLogger;

            m_widget = RivePanelTestUtils.CreateWidget<RiveWidget>();
            m_widgetObject = m_widget.gameObject;

            m_panel = RivePanelTestUtils.CreatePanel();

            m_widget.transform.SetParent(m_panel.WidgetContainer, false);
            testAssetLoadingManager = new TestAssetLoadingManager();

            m_loadedFiles = new List<File>();

        }

        [TearDown]
        public void TearDown()
        {
            if (m_widgetObject != null)
            {
                UnityEngine.Object.Destroy(m_widgetObject);
            }

            foreach (var file in m_loadedFiles)
            {
                if (file != null && !file.IsDisposed)
                {
                    file.Dispose();
                }
            }

            m_loadedFiles.Clear();


            testAssetLoadingManager.UnloadAllAssets();
        }


        // HELPERS
        private File LoadAndTrackFile(Asset asset)
        {
            var file = File.Load(asset);
            m_loadedFiles.Add(file);
            return file;
        }


        [UnityTest]
        public IEnumerator Load_ValidAsset_LoadsFileAndArtboard()
        {
            TestAssetData[] testDataArray = GetTestAssetInfo();

            Assert.Greater(testDataArray.Length, 0, "No test data found");

            bool hasCompletedFirstLoad = false;
            foreach (TestAssetData testData in testDataArray)
            {
                Asset riveAsset = null;

                yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                    testData.addressableAssetPath,
                    (asset) => riveAsset = asset,
                    () => Assert.Fail($"Failed to load asset at {testData.addressableAssetPath}")
                );

                if (!hasCompletedFirstLoad)
                {
                    Assert.AreEqual(Components.WidgetStatus.Uninitialized, m_widget.Status);
                    Assert.IsNull(m_widget.File, "File should not be loaded");
                    Assert.IsNull(m_widget.Artboard, "Artboard should not be loaded");
                    Assert.IsNull(m_widget.StateMachine, "StateMachine should not be loaded");
                }
                else
                {
                    Assert.AreEqual(Components.WidgetStatus.Loaded, m_widget.Status);
                }



                m_widget.Load(riveAsset);

                if (!hasCompletedFirstLoad)
                {
                    hasCompletedFirstLoad = true;
                }
                yield return null;

                Assert.IsNotNull(m_widget.File, "File should be loaded");
                Assert.IsNotNull(m_widget.Artboard, "Artboard should be loaded");
                Assert.AreEqual(testData.defaultArtboardName, m_widget.Artboard.Name);
                Assert.AreEqual(testData.defaultStateMachineName, m_widget.StateMachine.Name);
                Assert.AreEqual(Components.WidgetStatus.Loaded, m_widget.Status);
            }
        }

        [UnityTest]
        public IEnumerator Load_ValidRiveFile_LoadsFileAndArtboard()
        {
            TestAssetData[] testDataArray = GetTestAssetInfo();

            Assert.Greater(testDataArray.Length, 0, "No test data found");

            bool hasCompletedFirstLoad = false;
            foreach (TestAssetData testData in testDataArray)
            {
                Asset riveAsset = null;

                yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                    testData.addressableAssetPath,
                    (asset) => riveAsset = asset,
                    () => Assert.Fail($"Failed to load asset at {testData.addressableAssetPath}")
                );


                // Load the file directly instead of through the asset
                File riveFile = LoadAndTrackFile(riveAsset);

                if (!hasCompletedFirstLoad)
                {
                    Assert.AreEqual(Components.WidgetStatus.Uninitialized, m_widget.Status);
                    Assert.IsNull(m_widget.File, "File should not be loaded");
                    Assert.IsNull(m_widget.Artboard, "Artboard should not be loaded");
                    Assert.IsNull(m_widget.StateMachine, "StateMachine should not be loaded");
                }
                else
                {
                    Assert.AreEqual(Components.WidgetStatus.Loaded, m_widget.Status);
                }

                m_widget.Load(riveFile);

                if (!hasCompletedFirstLoad)
                {
                    hasCompletedFirstLoad = true;
                }
                yield return null;

                Assert.IsNotNull(m_widget.File, "File should be loaded");
                Assert.IsNotNull(m_widget.Artboard, "Artboard should be loaded");
                Assert.AreEqual(testData.defaultArtboardName, m_widget.Artboard.Name);
                Assert.AreEqual(testData.defaultStateMachineName, m_widget.StateMachine.Name);
                Assert.AreEqual(Components.WidgetStatus.Loaded, m_widget.Status);
            }
        }

        /// <summary>
        /// Test that loading a valid Rive file without an artboard name specified loads the default artboard and state machine if the widget has been previously loaded with a different artboard name
        /// </summary>
        [UnityTest]
        public IEnumerator Load_ValidRiveFileWithUnspecifiedArtboardName_LoadsNewArtboardAndStateMachine()
        {

            Asset riveAsset = null;
            yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
               TestAssetReferences.riv_sophiaHud,
                   (asset) => riveAsset = asset,
                   () => Assert.Fail($"Failed to load asset at {TestAssetReferences.riv_sophiaHud}")
               );

            File riveFile = LoadAndTrackFile(riveAsset);

            m_widget.Load(riveFile, artboardName: "DASHBOARD", stateMachineName: "State Machine 1");

            yield return null;

            Assert.AreEqual("DASHBOARD", m_widget.Artboard.Name);
            Assert.AreEqual("State Machine 1", m_widget.StateMachine.Name);

            // Load the file without specifying the artboard name
            m_widget.Load(riveFile);

            yield return null;

            Assert.AreEqual("SOPHIA III HUD", m_widget.Artboard.Name);

            Assert.AreEqual("SOPHIA_III_DASHBOARD_SEQ", m_widget.StateMachine.Name);


        }

        [UnityTest]
        public IEnumerator Load_ValidAssetWithUnspecifiedArtboardName_LoadsNewArtboardAndStateMachine()
        {

            Asset riveAsset = null;
            yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
               TestAssetReferences.riv_sophiaHud,
                   (asset) => riveAsset = asset,
                   () => Assert.Fail($"Failed to load asset at {TestAssetReferences.riv_sophiaHud}")
               );

            m_widget.Load(riveAsset, artboardName: "DASHBOARD", stateMachineName: "State Machine 1");

            yield return null;

            Assert.AreEqual("DASHBOARD", m_widget.Artboard.Name);
            Assert.AreEqual("State Machine 1", m_widget.StateMachine.Name);

            // Load the asset without specifying the artboard name
            m_widget.Load(riveAsset);


            yield return null;
            m_loadedFiles.Add(m_widget.File);

            Assert.AreEqual("SOPHIA III HUD", m_widget.Artboard.Name);

            Assert.AreEqual("SOPHIA_III_DASHBOARD_SEQ", m_widget.StateMachine.Name);
        }

        [UnityTest]
        public IEnumerator RenderObjectProperties_UpdateWhenWidgetPropertiesChange()
        {
            TestAssetData[] testDataArray = GetTestAssetInfo();
            Assert.Greater(testDataArray.Length, 0, "No test data found");

            Asset riveAsset = null;
            yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                testDataArray[0].addressableAssetPath,
                (asset) => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {testDataArray[0].addressableAssetPath}")
            );

            m_widget.Load(riveAsset);
            yield return null;

            ArtboardRenderObject renderObject = m_widget.RenderObject as ArtboardRenderObject;

            m_widget.Fit = Fit.Cover;
            Assert.AreEqual(Fit.Cover, renderObject.Fit, "RenderObject Fit should match widget Fit");

            m_widget.Fit = Fit.Contain;
            Assert.AreEqual(Fit.Contain, renderObject.Fit, "RenderObject Fit should update when widget Fit changes");

            m_widget.Alignment = Alignment.TopLeft;
            Assert.AreEqual(Alignment.TopLeft, renderObject.Alignment, "RenderObject Alignment should match widget Alignment");

            m_widget.Alignment = Alignment.Center;
            Assert.AreEqual(Alignment.Center, renderObject.Alignment, "RenderObject Alignment should update when widget Alignment changes");


        }

        [UnityTest]
        public IEnumerator LayoutProperties_InLayoutMode_ConstantPhysicalSize_TriggersRedraw()
        {
            TestAssetData[] testDataArray = GetTestAssetInfo();
            Assert.Greater(testDataArray.Length, 0, "No test data found");

            Asset riveAsset = null;
            yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                testDataArray[0].addressableAssetPath,
                (asset) => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {testDataArray[0].addressableAssetPath}")
            );

            m_widget.Fit = Fit.Layout;
            m_widget.Load(riveAsset);
            yield return null;

            bool needsRedraw = m_widget.Tick(0);
            Assert.IsFalse(needsRedraw, "Widget should not need redraw after loading");

            m_widget.ScalingMode = LayoutScalingMode.ConstantPhysicalSize;
            needsRedraw = m_widget.Tick(0);
            Assert.IsTrue(needsRedraw, "Changing ScalingMode should trigger redraw");
            yield return null;

            m_widget.ScaleFactor = 2.0f;
            needsRedraw = m_widget.Tick(0);
            Assert.IsTrue(needsRedraw, "Changing ScaleFactor should trigger redraw");
            yield return null;

            m_widget.FallbackDPI = 300f;
            needsRedraw = m_widget.Tick(0);
            Assert.IsTrue(needsRedraw, "Changing FallbackDPI should trigger redraw");
            yield return null;

            m_widget.ReferenceDPI = 1000f;
            needsRedraw = m_widget.Tick(0);
            Assert.IsTrue(needsRedraw, "Changing ReferenceDPI should trigger redraw");
            yield return null;
        }

        [UnityTest]
        public IEnumerator UpdatingDPILayoutProperties_InLayoutModes_OutsideConstantPhysicalSize_DoNotTriggerRedraw()
        {
            TestAssetData[] testDataArray = GetTestAssetInfo();
            Assert.Greater(testDataArray.Length, 0, "No test data found");

            Asset riveAsset = null;
            yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                testDataArray[0].addressableAssetPath,
                (asset) => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {testDataArray[0].addressableAssetPath}")
            );

            m_widget.Fit = Fit.Layout;
            m_widget.Load(riveAsset);
            yield return null;

            Assert.AreEqual(LayoutScalingMode.ReferenceArtboardSize, m_widget.ScalingMode, "ScalingMode should be ReferenceArtboardSize by default");

            m_widget.ScalingMode = LayoutScalingMode.ReferenceArtboardSize;
            bool needsRedraw = m_widget.Tick(0);
            Assert.IsFalse(needsRedraw, "Changing ScalingMode should not trigger redraw if value is the same");

            m_widget.FallbackDPI = 3000f;
            needsRedraw = m_widget.Tick(0);
            Assert.IsFalse(needsRedraw, "Changing FallbackDPI should not trigger redraw when ScalingMode is not ConstantPhysicalSize");

            m_widget.ReferenceDPI = 1000f;
            needsRedraw = m_widget.Tick(0);
            Assert.IsFalse(needsRedraw, "Changing ReferenceDPI should not trigger redraw when ScalingMode is not ConstantPhysicalSize");

            // Change to ConstantPixelSize

            m_widget.ScalingMode = LayoutScalingMode.ConstantPixelSize;
            needsRedraw = m_widget.Tick(0);
            Assert.IsTrue(needsRedraw, "Changing ScalingMode should trigger redraw when changing to ConstantPixelSize");

            m_widget.FallbackDPI = 3500f;
            needsRedraw = m_widget.Tick(0);
            Assert.IsFalse(needsRedraw, "Changing FallbackDPI should not trigger redraw when ScalingMode is not ConstantPhysicalSize");

            m_widget.ReferenceDPI = 2000f;
            needsRedraw = m_widget.Tick(0);
            Assert.IsFalse(needsRedraw, "Changing ReferenceDPI should not trigger redraw when ScalingMode is not ConstantPhysicalSize");



        }

        [UnityTest]
        public IEnumerator UpdatingLayoutProperties_NotInLayoutMode_DoNotTriggerRedraw()
        {
            TestAssetData[] testDataArray = GetTestAssetInfo();
            Assert.Greater(testDataArray.Length, 0, "No test data found");

            Asset riveAsset = null;
            yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                testDataArray[0].addressableAssetPath,
                (asset) => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {testDataArray[0].addressableAssetPath}")
            );

            m_widget.Fit = Fit.Layout;
            m_widget.Load(riveAsset);
            yield return null;

            m_widget.Fit = Fit.Contain;
            bool needsRedraw = m_widget.Tick(0);
            Assert.IsTrue(needsRedraw, "Widget should redraw initially after changing layout fit mode");

            m_widget.ScalingMode = LayoutScalingMode.ConstantPhysicalSize;
            needsRedraw = m_widget.Tick(0);
            Assert.IsFalse(needsRedraw, "Changing ScalingMode should not trigger redraw when Fit is not Layout");

            m_widget.ScaleFactor = 2.0f;
            needsRedraw = m_widget.Tick(0);
            Assert.IsFalse(needsRedraw, "Changing ScaleFactor should not trigger redraw when Fit is not Layout");

            m_widget.FallbackDPI = 300f;
            needsRedraw = m_widget.Tick(0);
            Assert.IsFalse(needsRedraw, "Changing FallbackDPI should not trigger redraw when Fit is not Layout");

            m_widget.ReferenceDPI = 1000f;
            needsRedraw = m_widget.Tick(0);
            Assert.IsFalse(needsRedraw, "Changing ReferenceDPI should not trigger redraw when Fit is not Layout");
        }

        [UnityTest]
        public IEnumerator HitTest_Scenarios_ReturnExpectedResults()
        {
            TestAssetData[] testDataArray = GetTestAssetInfo()
                .Where(data => data.hitTestScenarios != null && data.hitTestScenarios.Length > 0)
                .ToArray();

            Assert.Greater(testDataArray.Length, 0, "No hit test scenarios found in test data");
            foreach (TestAssetData testData in testDataArray)
            {
                Asset riveAsset = null;

                yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                    testData.addressableAssetPath,
                    (asset) => riveAsset = asset,
                    () => Assert.Fail($"Failed to load asset at {testData.addressableAssetPath}")
                );
                RivePanelTestUtils.MakeWidgetFillPanel(m_widget);

                m_widget.Load(riveAsset);
                yield return null;

                foreach (var scenario in testData.hitTestScenarios)
                {

                    m_widget.Fit = scenario.FitMode;
                    m_widget.Alignment = scenario.Alignment;
                    m_widget.ScalingMode = scenario.LayoutScalingMode;
                    m_widget.ScaleFactor = scenario.LayoutScaleFactor;


                    // We use the same reference DPI for all tests to ensure consistent results
                    if (scenario.LayoutScalingMode == LayoutScalingMode.ConstantPhysicalSize)
                    {
                        m_widget.ReferenceDPI = 150f;
                        m_widget.FallbackDPI = 255f; // Macbook Pro 14" DPI Retina Display
                        m_widget.UseFallbackDPI = true;
                    }


                    m_panel.SetDimensions(scenario.PanelSize);


                    yield return null;

                    // Test hit result based on event type
                    bool result = false;
                    switch (scenario.PointerEvent)
                    {
                        case HitTestScenario.PointerEventType.PointerDown:
                            result = m_widget.OnPointerDown(scenario.HitTestPoint);
                            break;
                        case HitTestScenario.PointerEventType.PointerUp:
                            result = m_widget.OnPointerUp(scenario.HitTestPoint);
                            break;
                        case HitTestScenario.PointerEventType.PointerMove:
                            result = m_widget.OnPointerMove(scenario.HitTestPoint);
                            break;
                        case HitTestScenario.PointerEventType.HitTest:
                            result = m_widget.HitTest(scenario.HitTestPoint);
                            break;
                    }

                    Assert.AreEqual(scenario.ExpectedResult, result,
                        $"Hit test scenario failed for point {scenario.HitTestPoint} with fit {scenario.FitMode} and alignment {scenario.Alignment}. And assetPath {testData.addressableAssetPath}");

                }
            }
        }

        [UnityTest]
        public IEnumerator Events_WhenTriggered_AreReported()
        {
            TestAssetData[] testDataArray = GetTestAssetInfo()
                .Where(data => data.expectedEvents != null && data.expectedEvents.Length > 0)
                .ToArray();

            Assert.IsTrue(testDataArray.Length > 0, "No expected events found in test data. Make sure to add expected events to test data.");

            foreach (TestAssetData testData in testDataArray)
            {
                Asset riveAsset = null;

                yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                    testData.addressableAssetPath,
                    (asset) => riveAsset = asset,
                    () => Assert.Fail($"Failed to load asset at {testData.addressableAssetPath}")
                );

                Dictionary<string, bool> reportedEvents = new Dictionary<string, bool>();
                foreach (var eventData in testData.expectedEvents)
                {
                    reportedEvents[eventData.EventName] = false;
                }


                m_widget.OnRiveEventReported += ((evt) =>
                {
                    if (reportedEvents.ContainsKey(evt.Name))
                    {
                        reportedEvents[evt.Name] = true;
                    }
                });

                m_widget.Load(riveAsset);
                yield return null;

                // Trigger each event and verify it was reported
                foreach (var eventData in testData.expectedEvents)
                {
                    // Reset the reported state
                    reportedEvents[eventData.EventName] = false;

                    eventData.TriggerAction(m_widget);

                    // Wait two frames for event to be processed in both the widget and the state machine
                    yield return null;
                    yield return null;

                    Assert.IsTrue(reportedEvents[eventData.EventName],
                        $"Expected event {eventData.EventName} was not reported after triggering");
                }
            }
        }

        [UnityTest]
        public IEnumerator Events_WithPoolingEnabled_ReuseInstances()
        {
            string assetPath = TestAssetReferences.riv_events_test;
            Asset riveAsset = null;
            yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                assetPath,
                (asset) => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {assetPath}")
            );

            m_widget.ReportedEventPoolingMode = RiveWidget.EventPoolingMode.Enabled;
            m_widget.Load(riveAsset);
            yield return null;

            ReportedEvent firstEvent = null;
            ReportedEvent secondEvent = null;

            m_widget.OnRiveEventReported += (evt =>
            {
                if (firstEvent == null)
                {
                    firstEvent = evt;
                }
                else if (secondEvent == null)
                {
                    secondEvent = evt;
                }
            });

            // Trigger the same event twice
            var trigger = m_widget.StateMachine.GetTrigger(ReportedEventTests.TRIGGER_SIMPLE);

            trigger.Fire();
            yield return null;
            yield return null;

            trigger.Fire();
            yield return null;
            yield return null;

            Assert.That(firstEvent, Is.Not.Null, "First event should have been received");
            Assert.That(secondEvent, Is.Not.Null, "Second event should have been received");
            Assert.That(firstEvent, Is.SameAs(secondEvent), "Events should be the same instance when pooling is enabled");
        }

        [UnityTest]
        public IEnumerator Events_WithPoolingDisabled_CreateNewInstances()
        {
            string assetPath = TestAssetReferences.riv_events_test;
            Asset riveAsset = null;
            yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                assetPath,
                (asset) => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {assetPath}")
            );

            m_widget.ReportedEventPoolingMode = RiveWidget.EventPoolingMode.Disabled;
            m_widget.Load(riveAsset);
            yield return null;

            ReportedEvent firstEvent = null;
            ReportedEvent secondEvent = null;

            m_widget.OnRiveEventReported += (evt =>
            {
                if (firstEvent == null)
                {
                    firstEvent = evt;
                }
                else if (secondEvent == null)
                {
                    secondEvent = evt;
                }
            });

            // Trigger the same event twice
            var trigger = m_widget.StateMachine.GetTrigger(ReportedEventTests.TRIGGER_SIMPLE);

            trigger.Fire();
            yield return null;
            yield return null;

            trigger.Fire();
            yield return null;
            yield return null;

            Assert.That(firstEvent, Is.Not.Null, "First event should have been received");
            Assert.That(secondEvent, Is.Not.Null, "Second event should have been received");
            Assert.That(firstEvent, Is.Not.SameAs(secondEvent), "Events should be different instances when pooling is disabled");
        }

        [UnityTest]
        public IEnumerator FiringTrigger_OnInitialFrame_Works()
        {
            string assetPath = TestAssetReferences.riv_initial_frame_tests;
            Asset riveAsset = null;
            yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                assetPath,
                (asset) => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {assetPath}")
            );

            bool triggerRegistered = false;

            m_widget.OnRiveEventReported += (evt =>
          {
              if (evt.Name == "ON_READY")
              {
                  triggerRegistered = true;

              }
          });


            m_widget.OnWidgetStatusChanged += () =>
            {
                if (m_widget.Status == Components.WidgetStatus.Loaded)
                {
                    var trigger = m_widget.StateMachine.GetTrigger("Ready");

                    trigger?.Fire();


                }
            };

            m_widget.Load(riveAsset);
            yield return null;


            // Wait for the triggerRegistered to be true up to 0.3 seconds
            float timeElapsed = 0;
            while (!triggerRegistered && timeElapsed < 0.3f)
            {
                yield return null;
                timeElapsed += Time.deltaTime;
            }

            Assert.IsTrue(triggerRegistered, "Trigger was registered on initial frame");

        }

        [Test]
        public void SpeedProperty_GetSet_WorksCorrectly()
        {
            Assert.AreEqual(1.0f, m_widget.Speed, "Default speed should be 1.0");

            m_widget.Speed = 2.0f;
            Assert.AreEqual(2.0f, m_widget.Speed, "Speed should update to 2.0");

            m_widget.Speed = -1.0f;
            Assert.AreEqual(-1.0f, m_widget.Speed, "Speed should update to -1.0");
        }

        [UnityTest]
        public IEnumerator Cleanup_DisposingWidget_ReleasesResources_IfWidgetLoadedFromAsset()
        {
            TestAssetData testData = GetTestAssetInfo()[0];

            Assert.IsNotNull(testData, "Test data not found");

            Asset riveAsset = null;

            yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                testData.addressableAssetPath,
                (asset) => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {testData.addressableAssetPath}")
            );

            m_widget.Load(riveAsset);
            yield return null;

            File originalFile = m_widget.File;

            m_loadedFiles.Add(originalFile);

            Assert.IsNotNull(originalFile);
            Assert.IsTrue(NativeFileInterface.isRiveFileValid(originalFile.NativeFile));

            UnityEngine.Object.Destroy(m_widget);
            yield return null;

            Assert.IsFalse(NativeFileInterface.isRiveFileValid(originalFile.NativeFile));
        }

        /// <summary>
        /// If a file is passed directly to the widget, that means the user was responsible for loading it, so they should be responsible for cleaning it up.
        /// </summary>
        [UnityTest]
        public IEnumerator Cleanup_DisposingWidget_DoesNotReleaseResources_IfWidgetLoadedFromFile()
        {
            TestAssetData testData = GetTestAssetInfo()[0];

            Assert.IsNotNull(testData, "Test data not found");

            Asset riveAsset = null;

            yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                testData.addressableAssetPath,
                (asset) => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {testData.addressableAssetPath}")
            );

            // Load the file directly instead of through the asset
            File riveFile = LoadAndTrackFile(riveAsset);
            Assert.IsNotNull(riveFile);

            m_widget.Load(riveFile);
            yield return null;

            File originalFile = m_widget.File;
            Assert.IsNotNull(originalFile);
            Assert.IsTrue(NativeFileInterface.isRiveFileValid(originalFile.NativeFile));

            UnityEngine.Object.Destroy(m_widget);
            yield return null;

            // File should still be valid since widget didn't load it from an asset
            Assert.IsTrue(NativeFileInterface.isRiveFileValid(originalFile.NativeFile));

            // Clean up the file ourselves since widget didn't
            riveFile.Dispose();
        }
    }
}