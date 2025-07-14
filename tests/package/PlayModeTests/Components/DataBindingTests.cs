using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rive.Components;
using Rive.Tests.Utils;
using Rive.Utils;
using UnityEngine;
using UnityEngine.TestTools;
using static Rive.Tests.DataBindingTests.DataBindingTestAsset;
using static Rive.Tests.DataBindingTests.DataBindingTestAsset.ViewModelInfo;
using Object = UnityEngine.Object;

namespace Rive.Tests
{
    public class DataBindingTests
    {
        private GameObject m_widgetObject;
        private RiveWidget m_widget;
        private MockLogger mockLogger;
        private TestAssetLoadingManager testAssetLoadingManager;
        private RivePanel m_panel;
        private List<File> m_loadedFiles;

        public class DataBindingTestAsset
        {


            /// <summary>
            /// Represents an enum type definition in a view model
            /// </summary>
            public class EnumTypeDefinition
            {
                public string TypeId { get; set; }
                public string[] Values { get; set; }
            }

            /// <summary>
            /// Represents an enum property definition in a view model
            /// </summary>
            public class EnumPropertyDefinition
            {
                public string PropertyName { get; set; }
                public string EnumTypeId { get; set; }

                public string ViewModelName { get; set; }
            }

            public class PropertyInfoToSet
            {
                public object ValueToSet { get; set; }

                public Type PropertyType { get; set; }
            }

            public class ViewModelInfo
            {
                public class PropertyInfo
                {
                    public string Name { get; private set; }
                    public ViewModelDataType Type { get; private set; }

                    public PropertyInfo(string name, ViewModelDataType type)
                    {
                        Name = name;
                        Type = type;
                    }

                    public virtual void Validate(ViewModelInstance vmInstance, string addressableAssetPath)
                    {
                        ViewModelInstancePrimitiveProperty prop = null;

                        switch (Type)
                        {
                            case ViewModelDataType.String:
                                prop = vmInstance.GetProperty<ViewModelInstanceStringProperty>(Name);
                                break;
                            case ViewModelDataType.Number:
                                prop = vmInstance.GetProperty<ViewModelInstanceNumberProperty>(Name);
                                break;
                            case ViewModelDataType.Boolean:
                                prop = vmInstance.GetProperty<ViewModelInstanceBooleanProperty>(Name);
                                break;
                            case ViewModelDataType.Color:
                                prop = vmInstance.GetProperty<ViewModelInstanceColorProperty>(Name);
                                break;
                            case ViewModelDataType.Enum:
                                prop = vmInstance.GetProperty<ViewModelInstanceEnumProperty>(Name);
                                break;
                            case ViewModelDataType.Trigger:
                                prop = vmInstance.GetProperty<ViewModelInstanceTriggerProperty>(Name);
                                break;
                            case ViewModelDataType.ViewModel:
                                ViewModelInstance nestedVmInstance = vmInstance.GetProperty<ViewModelInstance>(Name);
                                Assert.IsNotNull(nestedVmInstance, $"Nested view model '{Name}' should exist in {addressableAssetPath}");
                                return;
                            default:

                                break;

                        }

                        Assert.IsNotNull(prop, $"Property '{Name}' should exist in {addressableAssetPath}");
                    }
                }



                public string Name { get; private set; }
                public string[] InstanceNames { get; private set; }

                public List<PropertyInfo> Properties { get; private set; }


                public ViewModelInfo(string name, string[] instanceNames, List<PropertyInfo> properties)
                {
                    Name = name;
                    InstanceNames = instanceNames;
                    Properties = properties;
                }

            }

            public string addressableAssetPath;
            public string defaultArtboardName;
            public string defaultStateMachineName;

            public string defaultArtboardViewModelName;

            // Expected properties in the view model

            public EnumPropertyDefinition[] expectedEnumProperties;


            public ViewModelInfo[] expectedViewModelsInFile;



            public EnumTypeDefinition[] enumTypesInFile;

            // Test values to set and verify
            public Dictionary<string, PropertyInfoToSet> testPropertyValuesToSet;

            public ViewModelInfo GetDefaultArtboardViewModelInfo()
            {
                return expectedViewModelsInFile.FirstOrDefault(vm => vm.Name == defaultArtboardViewModelName);
            }
        }

        protected virtual DataBindingTestAsset[] GetTestAssetInfo()
        {
            return new DataBindingTestAsset[]
            {
                new DataBindingTestAsset
                {
                    addressableAssetPath = TestAssetReferences.riv_asset_databinding_test,
                    defaultArtboardName = "Artboard",
                    defaultStateMachineName = "State Machine 1",
                    defaultArtboardViewModelName = "PersonViewModel",


                    expectedEnumProperties = new[]
                {
                    new DataBindingTestAsset.EnumPropertyDefinition
                    {
                        PropertyName = "country",
                        EnumTypeId = "Country",
                        ViewModelName = "PersonViewModel"
                    },
                    new DataBindingTestAsset.EnumPropertyDefinition
                    {
                        PropertyName = "favDrink/type",
                        EnumTypeId = "DrinkType",
                        ViewModelName = "DrinkViewModel"
                    }
                },
                    enumTypesInFile = new[]
                {
                    new DataBindingTestAsset.EnumTypeDefinition
                    {
                        TypeId = "Country",
                        Values = new[] { "japan", "usa", "canada" }
                    },
                    new DataBindingTestAsset.EnumTypeDefinition
                    {
                        TypeId = "DrinkType",
                        Values = new[] { "Tea", "Coffee" }
                    }
                },

                    testPropertyValuesToSet = new Dictionary<string, DataBindingTestAsset.PropertyInfoToSet>
                    {
                        {
                            "name",
                            new DataBindingTestAsset.PropertyInfoToSet
                            {
                                ValueToSet = "Test User",
                                PropertyType = typeof(ViewModelInstanceStringProperty)
                            }
                        },
                        {
                            "age",
                            new DataBindingTestAsset.PropertyInfoToSet
                            {
                                ValueToSet = 42f,
                                PropertyType = typeof(ViewModelInstanceNumberProperty)
                            }
                        },
                        {
                            "agreedToTerms",
                            new DataBindingTestAsset.PropertyInfoToSet
                            {
                                ValueToSet = true,
                                PropertyType = typeof(ViewModelInstanceBooleanProperty)
                            }
                        },
                        {
                            "favColor",
                            new DataBindingTestAsset.PropertyInfoToSet
                            {
                                ValueToSet = new Color32(255, 0, 128, 255),
                                PropertyType = typeof(ViewModelInstanceColorProperty)
                            }
                        },
                        {
                            "country",
                            new DataBindingTestAsset.PropertyInfoToSet
                            {
                                ValueToSet = "japan",
                                PropertyType = typeof(ViewModelInstanceEnumProperty)
                            }
                        },
                        {
                            "favDrink/type",
                            new DataBindingTestAsset.PropertyInfoToSet
                            {
                                ValueToSet = "Coffee",
                                PropertyType = typeof(ViewModelInstanceEnumProperty)
                            }
                        }
                    },
                    expectedViewModelsInFile = new[]
                    {
                        new DataBindingTestAsset.ViewModelInfo(name: "PersonViewModel", instanceNames: new[] { "Steve", "Jane" },
                            properties: new List<DataBindingTestAsset.ViewModelInfo.PropertyInfo>
                            {
                                new DataBindingTestAsset.ViewModelInfo.PropertyInfo("name", ViewModelDataType.String),
                                new DataBindingTestAsset.ViewModelInfo.PropertyInfo("age", ViewModelDataType.Number),
                                new DataBindingTestAsset.ViewModelInfo.PropertyInfo("agreedToTerms", ViewModelDataType.Boolean),
                                new DataBindingTestAsset.ViewModelInfo.PropertyInfo("favColor", ViewModelDataType.Color),
                                new DataBindingTestAsset.ViewModelInfo.PropertyInfo("country", ViewModelDataType.Enum),
                                new DataBindingTestAsset.ViewModelInfo.PropertyInfo("onFormReset", ViewModelDataType.Trigger),
                                new DataBindingTestAsset.ViewModelInfo.PropertyInfo("onFormSubmit", ViewModelDataType.Trigger),
                                new DataBindingTestAsset.ViewModelInfo.PropertyInfo("favDrink", ViewModelDataType.ViewModel)
                            }),
                        new DataBindingTestAsset.ViewModelInfo(name: "DrinkViewModel", instanceNames: new[] { "London Fog" },
                            properties: new List<DataBindingTestAsset.ViewModelInfo.PropertyInfo>
                            {
                                new DataBindingTestAsset.ViewModelInfo.PropertyInfo("type", ViewModelDataType.Enum),
                                new DataBindingTestAsset.ViewModelInfo.PropertyInfo("name", ViewModelDataType.String)
                            })


                    }
                },
                new DataBindingTestAsset
                {
                    addressableAssetPath = TestAssetReferences.riv_duelist_db,
                    defaultArtboardName = "Main menu",
                    defaultStateMachineName = "State Machine 1",
                    defaultArtboardViewModelName = "Main Menu",


                    expectedEnumProperties = new[]
                {
                    new DataBindingTestAsset.EnumPropertyDefinition
                    {
                        PropertyName = "SectionId",
                        EnumTypeId = "Section",
                        ViewModelName = "MenuButton"
                    },
                    new DataBindingTestAsset.EnumPropertyDefinition
                    {
                        PropertyName = "expandedSection",
                        EnumTypeId = "Section",
                        ViewModelName = "Navigation"
                    },
                    new DataBindingTestAsset.EnumPropertyDefinition
                    {
                        PropertyName = "selectedSubSection",
                        EnumTypeId = "Section",
                        ViewModelName = "Main Menu"
                    },
                    new DataBindingTestAsset.EnumPropertyDefinition
                    {
                        PropertyName = "ActiveItem",
                        EnumTypeId = "SubButtonItem",
                        ViewModelName = "SubButtonC-Item"
                    }
                },
                    enumTypesInFile = new[]
                {
                    new DataBindingTestAsset.EnumTypeDefinition
                    {
                        TypeId = "Section",
                        Values = new[] { "None", "Play", "Options", "Quit" }
                    },
                    new DataBindingTestAsset.EnumTypeDefinition
                    {
                        TypeId = "SubButtonItem",
                        Values = new[] { "Item-1", "Item-2" }
                    }
                },

                    testPropertyValuesToSet = new Dictionary<string, DataBindingTestAsset.PropertyInfoToSet>
                    {

                    },
                    expectedViewModelsInFile = new[]
                    {
                        new DataBindingTestAsset.ViewModelInfo(name: "Button", instanceNames: new[] { "Button-Play", "Button-Quit", "Button-Options" },
                            properties: new List<DataBindingTestAsset.ViewModelInfo.PropertyInfo>
                            {
                                new DataBindingTestAsset.ViewModelInfo.PropertyInfo("Color property", ViewModelDataType.Color),
                                new DataBindingTestAsset.ViewModelInfo.PropertyInfo("Label", ViewModelDataType.String),
                                new DataBindingTestAsset.ViewModelInfo.PropertyInfo("IsHovered", ViewModelDataType.Boolean),
                            }),
                        new DataBindingTestAsset.ViewModelInfo(name: "MenuButton", instanceNames: new[] { "MenuButton-Play", "MenuButton-Options", "MenuButton-Quit" },
                            properties: new List<DataBindingTestAsset.ViewModelInfo.PropertyInfo>
                            {
                                new DataBindingTestAsset.ViewModelInfo.PropertyInfo("SubButton-Options", ViewModelDataType.ViewModel),
                                new DataBindingTestAsset.ViewModelInfo.PropertyInfo("SectionId", ViewModelDataType.Enum),
                                new DataBindingTestAsset.ViewModelInfo.PropertyInfo("SubButton2", ViewModelDataType.ViewModel),
                                new DataBindingTestAsset.ViewModelInfo.PropertyInfo("SubButton1", ViewModelDataType.ViewModel),
                                new DataBindingTestAsset.ViewModelInfo.PropertyInfo("button", ViewModelDataType.ViewModel)

                            }),
                        new DataBindingTestAsset.ViewModelInfo(name: "Navigation", instanceNames: new[] { "Navigation-Instance" },
                            properties: new List<DataBindingTestAsset.ViewModelInfo.PropertyInfo>
                            {
                                new DataBindingTestAsset.ViewModelInfo.PropertyInfo("Options", ViewModelDataType.ViewModel),
                                new DataBindingTestAsset.ViewModelInfo.PropertyInfo("Quit", ViewModelDataType.ViewModel),
                                new DataBindingTestAsset.ViewModelInfo.PropertyInfo("Play", ViewModelDataType.ViewModel),
                                new DataBindingTestAsset.ViewModelInfo.PropertyInfo("expandedSection", ViewModelDataType.Enum),

                            }),
                        new DataBindingTestAsset.ViewModelInfo(name: "Main Menu", instanceNames: new[] { "MainInstance" },
                            properties: new List<DataBindingTestAsset.ViewModelInfo.PropertyInfo>
                            {
                                new DataBindingTestAsset.ViewModelInfo.PropertyInfo("height", ViewModelDataType.Number),
                                new DataBindingTestAsset.ViewModelInfo.PropertyInfo("With", ViewModelDataType.Number),
                                new DataBindingTestAsset.ViewModelInfo.PropertyInfo("Navigation", ViewModelDataType.ViewModel),
                                new DataBindingTestAsset.ViewModelInfo.PropertyInfo("selectedSubSection", ViewModelDataType.Enum),

                            })  ,
                        new DataBindingTestAsset.ViewModelInfo(name: "SubButtonA", instanceNames: new[] { "ContinueGame", "Yes", "No", "NewGame", "Empty" },
                            properties: new List<DataBindingTestAsset.ViewModelInfo.PropertyInfo>
                            {
                                new DataBindingTestAsset.ViewModelInfo.PropertyInfo("Subutton expand", ViewModelDataType.Boolean),
                                new DataBindingTestAsset.ViewModelInfo.PropertyInfo("Label", ViewModelDataType.String)

                            }),
                         new DataBindingTestAsset.ViewModelInfo(name: "SubButtonB", instanceNames: new[] { "Music", "Empty", "Fx" },
                            properties: new List<DataBindingTestAsset.ViewModelInfo.PropertyInfo>
                            {
                                new DataBindingTestAsset.ViewModelInfo.PropertyInfo("Names", ViewModelDataType.String),
                                new DataBindingTestAsset.ViewModelInfo.PropertyInfo("Slider", ViewModelDataType.Number)

                            }),
                          new DataBindingTestAsset.ViewModelInfo(name: "SubButtonC-Item", instanceNames: new[] { "FramePsec", "Languages", "Resolution" },
                            properties: new List<DataBindingTestAsset.ViewModelInfo.PropertyInfo>
                            {
                                new DataBindingTestAsset.ViewModelInfo.PropertyInfo("ActiveItem", ViewModelDataType.Enum),
                                new DataBindingTestAsset.ViewModelInfo.PropertyInfo("Item-2", ViewModelDataType.String),
                                new DataBindingTestAsset.ViewModelInfo.PropertyInfo("Item-1", ViewModelDataType.String)

                            }),
                           new DataBindingTestAsset.ViewModelInfo(name: "SubButtonC", instanceNames: new[] { "Resolution", "Languages", "Empty", "FrameRate" },
                            properties: new List<DataBindingTestAsset.ViewModelInfo.PropertyInfo>
                            {
                                new DataBindingTestAsset.ViewModelInfo.PropertyInfo("Label", ViewModelDataType.String),
                                new DataBindingTestAsset.ViewModelInfo.PropertyInfo("Items", ViewModelDataType.ViewModel),

                            }),
                            new DataBindingTestAsset.ViewModelInfo(name: "SubButtons-Options", instanceNames: new[] { "SubButtons-Opts-Instance", "Empty-SubButtons-Opts" },
                            properties: new List<DataBindingTestAsset.ViewModelInfo.PropertyInfo>
                            {
                                new DataBindingTestAsset.ViewModelInfo.PropertyInfo("Languages", ViewModelDataType.ViewModel),
                                new DataBindingTestAsset.ViewModelInfo.PropertyInfo("FrameRate", ViewModelDataType.ViewModel),
                                new DataBindingTestAsset.ViewModelInfo.PropertyInfo("Resolution", ViewModelDataType.ViewModel),
                                new DataBindingTestAsset.ViewModelInfo.PropertyInfo("Music", ViewModelDataType.ViewModel),
                                new DataBindingTestAsset.ViewModelInfo.PropertyInfo("FX", ViewModelDataType.ViewModel),

                            })
                    }
                }

            };
        }


        [SetUp]
        public void Setup()
        {
            mockLogger = new MockLogger();
            DebugLogger.Instance = mockLogger;

            testAssetLoadingManager = new TestAssetLoadingManager();

            m_loadedFiles = new List<File>();

            m_widgetObject = new GameObject("RiveWidget");
            m_widget = m_widgetObject.AddComponent<RiveWidget>();

            var panelObject = new GameObject("RivePanel");
            m_panel = panelObject.AddComponent<RivePanel>();
            m_widget.transform.SetParent(m_panel.WidgetContainer, false);

        }

        [TearDown]
        public void TearDown()
        {
            foreach (var file in m_loadedFiles)
            {
                file.Dispose();
            }
            m_loadedFiles.Clear();

            if (m_widgetObject != null)
            {
                Object.Destroy(m_widgetObject);
            }

            if (m_panel != null)
            {
                Object.Destroy(m_panel.gameObject);
            }

            testAssetLoadingManager.UnloadAllAssets();

        }

        private File LoadAndTrackFile(Asset asset)
        {
            var file = File.Load(asset);
            m_loadedFiles.Add(file);
            return file;
        }

        [UnityTest]
        public IEnumerator ViewModelInstance_IsAvailableAfterLoad()
        {
            foreach (var testAsset in GetTestAssetInfo())
            {
                Asset riveAsset = null;

                yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                    testAsset.addressableAssetPath,
                    (asset) => riveAsset = asset,
                    () => Assert.Fail($"Failed to load asset at {testAsset.addressableAssetPath}")
                );

                File riveFile = LoadAndTrackFile(riveAsset);


                m_widget.Load(riveFile, testAsset.defaultArtboardName, testAsset.defaultStateMachineName);

                yield return new WaitUntil(() => m_widget.Status == WidgetStatus.Loaded);

                Assert.IsNotNull(m_widget.StateMachine.ViewModelInstance,
                    $"ViewModelInstance should be available after loading for asset {testAsset.addressableAssetPath}");


            }
        }



        [UnityTest]
        public IEnumerator ViewModelInstance_HasExpectedProperties()
        {
            foreach (var testAsset in GetTestAssetInfo())
            {
                Asset riveAsset = null;

                yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                    testAsset.addressableAssetPath,
                    (asset) => riveAsset = asset,
                    () => Assert.Fail($"Failed to load asset at {testAsset.addressableAssetPath}")
                );

                File riveFile = LoadAndTrackFile(riveAsset);


                m_widget.Load(riveFile, testAsset.defaultArtboardName, testAsset.defaultStateMachineName);

                yield return new WaitUntil(() => m_widget.Status == WidgetStatus.Loaded);

                var viewModelInstance = m_widget.StateMachine.ViewModelInstance;
                Assert.IsNotNull(viewModelInstance, $"ViewModelInstance should exist for asset {testAsset.addressableAssetPath}");


                ViewModelInfo viewModelInfo = testAsset.GetDefaultArtboardViewModelInfo();
                foreach (var propInfo in viewModelInfo.Properties)
                {
                    propInfo.Validate(viewModelInstance, testAsset.addressableAssetPath);
                }



            }
        }

        [UnityTest]
        public IEnumerator EnumProperty_HasExpectedValues()
        {
            List<DataBindingTestAsset> testAssetsWithEnums = GetTestAssetInfo().Where(a =>
                           a.addressableAssetPath == TestAssetReferences.riv_asset_databinding_test).ToList();

            Assert.Greater(testAssetsWithEnums.Count, 0,
                "At least one test asset should have enum properties defined");

            foreach (var testAsset in testAssetsWithEnums)
            {
                Asset riveAsset = null;

                yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                    testAsset.addressableAssetPath,
                    (asset) => riveAsset = asset,
                    () => Assert.Fail($"Failed to load asset at {testAsset.addressableAssetPath}")
                );

                File riveFile = LoadAndTrackFile(riveAsset);


                m_widget.Load(riveFile, testAsset.defaultArtboardName, testAsset.defaultStateMachineName);

                yield return new WaitUntil(() => m_widget.Status == WidgetStatus.Loaded);

                var viewModelInstance = m_widget.StateMachine.ViewModelInstance;
                Assert.IsNotNull(viewModelInstance, $"ViewModelInstance should exist for asset {testAsset.addressableAssetPath}");

                foreach (var enumProp in testAsset.expectedEnumProperties)
                {
                    var prop = viewModelInstance.GetProperty<ViewModelInstanceEnumProperty>(enumProp.PropertyName);
                    Assert.IsNotNull(prop, $"Enum property '{enumProp.PropertyName}' should exist in {testAsset.addressableAssetPath}");

                    // Find the corresponding enum type definition
                    var enumType = testAsset.enumTypesInFile.FirstOrDefault(et => et.TypeId == enumProp.EnumTypeId);
                    Assert.IsNotNull(enumType, $"Enum type '{enumProp.EnumTypeId}' should be defined for property '{enumProp.PropertyName}'");

                    // Check that the enum has the expected values
                    Assert.AreEqual(prop.EnumValues.Count, prop.EnumValues.Count,
                        $"Enum '{enumProp.PropertyName}' should have {prop.Value.Length} values in {testAsset.addressableAssetPath}");

                    for (int i = 0; i < prop.EnumValues.Count; i++)
                    {
                        Assert.AreEqual(enumType.Values[i], prop.EnumValues[i],
                            $"Enum '{enumProp.PropertyName}' value at index {i} should be '{prop.Value[i]}' in {testAsset.addressableAssetPath}");
                    }
                }

            }
        }

        [UnityTest]
        public IEnumerator PropertyValues_CanBeSetAndRead()
        {
            foreach (var testAsset in GetTestAssetInfo())
            {
                Asset riveAsset = null;

                yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                    testAsset.addressableAssetPath,
                    (asset) => riveAsset = asset,
                    () => Assert.Fail($"Failed to load asset at {testAsset.addressableAssetPath}")
                );

                File riveFile = LoadAndTrackFile(riveAsset);


                m_widget.Load(riveFile, testAsset.defaultArtboardName, testAsset.defaultStateMachineName);

                yield return new WaitUntil(() => m_widget.Status == WidgetStatus.Loaded);

                var viewModelInstance = m_widget.StateMachine.ViewModelInstance;
                Assert.IsNotNull(viewModelInstance, $"ViewModelInstance should exist for asset {testAsset.addressableAssetPath}");

                // Set and verify each property
                foreach (var kvp in testAsset.testPropertyValuesToSet)
                {

                    if (kvp.Value.PropertyType == typeof(ViewModelInstanceStringProperty))
                    {
                        var prop = viewModelInstance.GetProperty<ViewModelInstanceStringProperty>(kvp.Key);
                        Assert.IsNotNull(prop, $"String property '{kvp.Key}' should exist in {testAsset.addressableAssetPath}");
                        prop.Value = kvp.Value.ValueToSet as string;
                        Assert.AreEqual(kvp.Value.ValueToSet, prop.Value,
                            $"Property '{kvp.Key}' value should be '{kvp.Value.ValueToSet}' in {testAsset.addressableAssetPath}");
                    }
                    else if (kvp.Value.PropertyType == typeof(ViewModelInstanceNumberProperty))
                    {
                        var prop = viewModelInstance.GetProperty<ViewModelInstanceNumberProperty>(kvp.Key);
                        Assert.IsNotNull(prop, $"Number property '{kvp.Key}' should exist in {testAsset.addressableAssetPath}");
                        float floatValue = (float)kvp.Value.ValueToSet;
                        prop.Value = floatValue;
                        Assert.AreEqual(floatValue, prop.Value,
                            $"Property '{kvp.Key}' value should be '{floatValue}' in {testAsset.addressableAssetPath}");
                    }
                    else if (kvp.Value.PropertyType == typeof(ViewModelInstanceBooleanProperty))
                    {
                        var prop = viewModelInstance.GetProperty<ViewModelInstanceBooleanProperty>(kvp.Key);
                        Assert.IsNotNull(prop, $"Boolean property '{kvp.Key}' should exist in {testAsset.addressableAssetPath}");
                        bool boolValue = (bool)kvp.Value.ValueToSet;
                        prop.Value = boolValue;
                        Assert.AreEqual(boolValue, prop.Value,
                            $"Property '{kvp.Key}' value should be '{boolValue}' in {testAsset.addressableAssetPath}");
                    }
                    else if (kvp.Value.PropertyType == typeof(ViewModelInstanceColorProperty))
                    {
                        var prop = viewModelInstance.GetProperty<ViewModelInstanceColorProperty>(kvp.Key);
                        Assert.IsNotNull(prop, $"Color property '{kvp.Key}' should exist in {testAsset.addressableAssetPath}");

                        // Test both Color and Color32 APIs
                        if (kvp.Value.ValueToSet is Color32 color32Value)
                        {
                            prop.Value32 = color32Value;

                            // Compare color components individually since Color32 equality might not work as expected
                            Assert.AreEqual(color32Value.r, prop.Value32.r,
                                $"Property '{kvp.Key}' red component should be '{color32Value.r}' in {testAsset.addressableAssetPath}");
                            Assert.AreEqual(color32Value.g, prop.Value32.g,
                                $"Property '{kvp.Key}' green component should be '{color32Value.g}' in {testAsset.addressableAssetPath}");
                            Assert.AreEqual(color32Value.b, prop.Value32.b,
                                $"Property '{kvp.Key}' blue component should be '{color32Value.b}' in {testAsset.addressableAssetPath}");
                            Assert.AreEqual(color32Value.a, prop.Value32.a,
                                $"Property '{kvp.Key}' alpha component should be '{color32Value.a}' in {testAsset.addressableAssetPath}");

                            // Also test that Color property returns the same value
                            UnityEngine.Color color = prop.Value;
                            Assert.AreEqual(color32Value.r / 255f, color.r, 0.01f,
                                $"Color.r should match Color32.r for property '{kvp.Key}'");
                            Assert.AreEqual(color32Value.g / 255f, color.g, 0.01f,
                                $"Color.g should match Color32.g for property '{kvp.Key}'");
                            Assert.AreEqual(color32Value.b / 255f, color.b, 0.01f,
                                $"Color.b should match Color32.b for property '{kvp.Key}'");
                            Assert.AreEqual(color32Value.a / 255f, color.a, 0.01f,
                                $"Color.a should match Color32.a for property '{kvp.Key}'");
                        }
                        else if (kvp.Value.ValueToSet is UnityEngine.Color colorValue)
                        {
                            prop.Value = colorValue;

                            // Compare color components with small delta for floating point precision
                            Assert.AreEqual(colorValue.r, prop.Value.r, 0.01f,
                                $"Property '{kvp.Key}' red component should be '{colorValue.r}' in {testAsset.addressableAssetPath}");
                            Assert.AreEqual(colorValue.g, prop.Value.g, 0.01f,
                                $"Property '{kvp.Key}' green component should be '{colorValue.g}' in {testAsset.addressableAssetPath}");
                            Assert.AreEqual(colorValue.b, prop.Value.b, 0.01f,
                                $"Property '{kvp.Key}' blue component should be '{colorValue.b}' in {testAsset.addressableAssetPath}");
                            Assert.AreEqual(colorValue.a, prop.Value.a, 0.01f,
                                $"Property '{kvp.Key}' alpha component should be '{colorValue.a}' in {testAsset.addressableAssetPath}");

                            // Also test that Color32 property returns the equivalent value
                            Color32 color32 = prop.Value32;
                            Assert.AreEqual((byte)(colorValue.r * 255), color32.r,
                                $"Color32.r should match Color.r for property '{kvp.Key}'");
                            Assert.AreEqual((byte)(colorValue.g * 255), color32.g,
                                $"Color32.g should match Color.g for property '{kvp.Key}'");
                            Assert.AreEqual((byte)(colorValue.b * 255), color32.b,
                                $"Color32.b should match Color.b for property '{kvp.Key}'");
                            Assert.AreEqual((byte)(colorValue.a * 255), color32.a,
                                $"Color32.a should match Color.a for property '{kvp.Key}'");
                        }
                    }
                    else if (kvp.Value.PropertyType == typeof(ViewModelInstanceEnumProperty))
                    {
                        var prop = viewModelInstance.GetProperty<ViewModelInstanceEnumProperty>(kvp.Key);
                        Assert.IsNotNull(prop, $"Enum property '{kvp.Key}' should exist in {testAsset.addressableAssetPath}");
                        prop.Value = kvp.Value.ValueToSet as string;
                        Assert.AreEqual(kvp.Value.ValueToSet, prop.Value,
                            $"Property '{kvp.Key}' value should be '{kvp.Value.ValueToSet}' in {testAsset.addressableAssetPath}");
                    }

                }

            }
        }

        private List<PropertyInfo> GetPropertyInfoOfType(DataBindingTestAsset testAsset, ViewModelDataType propertyType)
        {
            return testAsset.GetDefaultArtboardViewModelInfo().Properties
                .Where(p => p.Type == propertyType)
                .ToList();
        }

        private List<DataBindingTestAsset> GetTestAssetsWithPropertyType(ViewModelDataType propertyType)
        {
            return GetTestAssetInfo()
                .Where(ta => ta.expectedViewModelsInFile != null && ta.expectedViewModelsInFile.Length > 0 &&
                            ta.expectedViewModelsInFile.Any(vm => vm.Properties.Any(p => p.Type == propertyType) && vm.Name == ta.defaultArtboardViewModelName))
                .ToList();
        }

        [UnityTest]
        public IEnumerator PropertyValueChanges_TriggerCallbacks()
        {
            List<DataBindingTestAsset> testAssetsWithStringProperties = GetTestAssetInfo().Where(a =>
                           a.addressableAssetPath == TestAssetReferences.riv_asset_databinding_test).ToList();

            Assert.Greater(testAssetsWithStringProperties.Count, 0,
                "At least one test asset should have string properties defined");

            foreach (var testAsset in testAssetsWithStringProperties)
            {
                Asset riveAsset = null;

                yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                    testAsset.addressableAssetPath,
                    (asset) => riveAsset = asset,
                    () => Assert.Fail($"Failed to load asset at {testAsset.addressableAssetPath}")
                );

                File riveFile = LoadAndTrackFile(riveAsset);


                m_widget.Load(riveFile, testAsset.defaultArtboardName, testAsset.defaultStateMachineName);

                yield return new WaitUntil(() => m_widget.Status == WidgetStatus.Loaded);

                var viewModelInstance = m_widget.StateMachine.ViewModelInstance;
                Assert.IsNotNull(viewModelInstance, $"ViewModelInstance should exist for asset {testAsset.addressableAssetPath}");

                // Get a string property to test callbacks
                var prop = GetPropertyInfoOfType(testAsset, ViewModelDataType.String).FirstOrDefault();
                Assert.IsNotNull(prop, $"String property should exist in {testAsset.addressableAssetPath}");
                string propName = prop.Name;
                var stringProp = viewModelInstance.GetProperty<ViewModelInstanceStringProperty>(propName);
                Assert.IsNotNull(stringProp, $"String property '{propName}' should exist in {testAsset.addressableAssetPath}");

                bool callbackTriggered = false;
                string expectedValue = "Test Value " + Guid.NewGuid().ToString().Substring(0, 8); // Unique value

                stringProp.OnValueChanged += (newValue) =>
                {
                    callbackTriggered = true;
                    Assert.AreEqual(expectedValue, newValue);
                    Assert.AreEqual(newValue, stringProp.Value,
                        $"Property '{propName}' value should be '{expectedValue}' in {testAsset.addressableAssetPath}");
                };

                stringProp.Value = expectedValue;

                viewModelInstance.HandleCallbacks();

                Assert.IsTrue(callbackTriggered,
                    $"Callback should have been triggered for property '{propName}' in {testAsset.addressableAssetPath}");

            }
        }


        [UnityTest]
        public IEnumerator PropertyAccessWithWrongType_ReturnsNullAndLogsWarning()
        {
            var testAssetInfo = GetTestAssetInfo().Where(a =>
                           a.addressableAssetPath == TestAssetReferences.riv_asset_databinding_test).ToList();

            foreach (var testAsset in testAssetInfo)
            {
                Asset riveAsset = null;
                yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                    testAsset.addressableAssetPath,
                    (asset) => riveAsset = asset,
                    () => Assert.Fail($"Failed to load asset at {testAsset.addressableAssetPath}")
                );

                File riveFile = LoadAndTrackFile(riveAsset);
                m_widget.Load(riveFile, testAsset.defaultArtboardName, testAsset.defaultStateMachineName);
                yield return new WaitUntil(() => m_widget.Status == WidgetStatus.Loaded);

                var viewModelInstance = m_widget.StateMachine.ViewModelInstance;
                Assert.IsNotNull(viewModelInstance, $"ViewModelInstance should exist for asset {testAsset.addressableAssetPath}");

                // Case 1: String property accessed as number property
                string stringPropName = GetPropertyInfoOfType(testAsset, ViewModelDataType.String).FirstOrDefault()?.Name;
                if (stringPropName != null)
                {
                    // First verify it works correctly with the right type
                    var stringProp = viewModelInstance.GetProperty<ViewModelInstanceStringProperty>(stringPropName);
                    Assert.IsNotNull(stringProp, $"String property '{stringPropName}' should exist in {testAsset.addressableAssetPath}");

                    // Clear logs and try with wrong type
                    mockLogger.Clear();
                    var wrongTypeProp = viewModelInstance.GetProperty<ViewModelInstanceNumberProperty>(stringPropName);

                    // Should return null and log a warning
                    Assert.IsNull(wrongTypeProp, $"String property accessed as number should return null in {testAsset.addressableAssetPath}");
                    Assert.IsTrue(mockLogger.LoggedWarnings.Count > 0 || mockLogger.LoggedErrors.Count > 0,
                        $"Warning should be logged when accessing string property as number in {testAsset.addressableAssetPath}");
                }

                // Case 2: Enum property accessed as string property
                string enumPropName = GetPropertyInfoOfType(testAsset, ViewModelDataType.Enum).FirstOrDefault()?.Name;
                if (enumPropName != null)
                {
                    // Verify correct access first
                    var enumProp = viewModelInstance.GetProperty<ViewModelInstanceEnumProperty>(enumPropName);
                    Assert.IsNotNull(enumProp, $"Enum property '{enumPropName}' should exist in {testAsset.addressableAssetPath}");

                    // Clear logs and try with wrong type
                    mockLogger.Clear();
                    var wrongTypeProp = viewModelInstance.GetProperty<ViewModelInstanceStringProperty>(enumPropName);

                    // Should return null and log a warning
                    Assert.IsNull(wrongTypeProp, $"Enum property accessed as string should return null in {testAsset.addressableAssetPath}");
                    Assert.IsTrue(mockLogger.LoggedWarnings.Count > 0 || mockLogger.LoggedErrors.Count > 0,
                        $"Warning should be logged when accessing enum property as string in {testAsset.addressableAssetPath}");
                }

                // Case 3: Number property accessed as boolean property
                string numberPropName = GetPropertyInfoOfType(testAsset, ViewModelDataType.Number).FirstOrDefault()?.Name;
                if (numberPropName != null)
                {
                    // Verify correct access first
                    var numberProp = viewModelInstance.GetProperty<ViewModelInstanceNumberProperty>(numberPropName);
                    Assert.IsNotNull(numberProp, $"Number property '{numberPropName}' should exist in {testAsset.addressableAssetPath}");

                    // Clear logs and try with wrong type
                    mockLogger.Clear();
                    var wrongTypeProp = viewModelInstance.GetProperty<ViewModelInstanceBooleanProperty>(numberPropName);

                    // Should return null and log a warning
                    Assert.IsNull(wrongTypeProp, $"Number property accessed as boolean should return null in {testAsset.addressableAssetPath}");
                    Assert.IsTrue(mockLogger.LoggedWarnings.Count > 0 || mockLogger.LoggedErrors.Count > 0,
                        $"Warning should be logged when accessing number property as boolean in {testAsset.addressableAssetPath}");
                }

                // Case 4: String property accessed as enum property
                // This caused crashes in the past
                string stringPropName2 = GetPropertyInfoOfType(testAsset, ViewModelDataType.String).FirstOrDefault()?.Name;
                if (stringPropName2 != null)
                {
                    // Verify correct access first
                    var stringProp = viewModelInstance.GetProperty<ViewModelInstanceStringProperty>(stringPropName2);
                    Assert.IsNotNull(stringProp, $"String property '{stringPropName2}' should exist in {testAsset.addressableAssetPath}");

                    // Clear logs and try with wrong type
                    mockLogger.Clear();
                    var wrongTypeProp = viewModelInstance.GetProperty<ViewModelInstanceEnumProperty>(stringPropName2);

                    // Should return null and log a warning
                    Assert.IsNull(wrongTypeProp, $"String property accessed as enum should return null in {testAsset.addressableAssetPath}");
                    Assert.IsTrue(mockLogger.LoggedWarnings.Count > 0 || mockLogger.LoggedErrors.Count > 0,
                        $"Warning should be logged when accessing string property as enum in {testAsset.addressableAssetPath}");
                }

            }
        }

        [UnityTest]
        public IEnumerator TriggerProperty_FiresCallback()
        {
            List<DataBindingTestAsset> testAssetsWithTriggers = GetTestAssetInfo().Where(a =>
                           a.addressableAssetPath == TestAssetReferences.riv_asset_databinding_test).ToList();

            foreach (var testAsset in testAssetsWithTriggers)
            {
                Asset riveAsset = null;

                yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                    testAsset.addressableAssetPath,
                    (asset) => riveAsset = asset,
                    () => Assert.Fail($"Failed to load asset at {testAsset.addressableAssetPath}")
                );

                File riveFile = LoadAndTrackFile(riveAsset);


                m_widget.Load(riveFile, testAsset.defaultArtboardName, testAsset.defaultStateMachineName);

                yield return new WaitUntil(() => m_widget.Status == WidgetStatus.Loaded);

                var viewModelInstance = m_widget.StateMachine.ViewModelInstance;
                Assert.IsNotNull(viewModelInstance, $"ViewModelInstance should exist for asset {testAsset.addressableAssetPath}");

                // Get a trigger property to test
                var prop = GetPropertyInfoOfType(testAsset, ViewModelDataType.Trigger).FirstOrDefault();
                Assert.IsNotNull(prop, $"Trigger property should exist in {testAsset.addressableAssetPath}");
                string propName = prop.Name;
                var triggerProp = viewModelInstance.GetProperty<ViewModelInstanceTriggerProperty>(propName);
                Assert.IsNotNull(triggerProp, $"Trigger property '{propName}' should exist in {testAsset.addressableAssetPath}");

                bool callbackTriggered = false;
                triggerProp.OnTriggered += () =>
                {
                    callbackTriggered = true;
                };

                triggerProp.Trigger();

                viewModelInstance.HandleCallbacks();

                Assert.IsTrue(callbackTriggered,
                    $"Trigger callback should have been triggered for property '{propName}' in {testAsset.addressableAssetPath}");


            }
        }

        [UnityTest]
        public IEnumerator EnumProperty_CanBeSetByValueAndIndex()
        {
            // Only test assets with enum properties defined
            List<DataBindingTestAsset> testAssetsWithEnums = GetTestAssetInfo().Where(a =>
                           a.addressableAssetPath == TestAssetReferences.riv_asset_databinding_test).ToList();

            Assert.Greater(testAssetsWithEnums.Count, 0,
                "At least one test asset should have enum properties defined");

            foreach (var testAsset in testAssetsWithEnums)
            {

                Asset riveAsset = null;

                yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                    testAsset.addressableAssetPath,
                    (asset) => riveAsset = asset,
                    () => Assert.Fail($"Failed to load asset at {testAsset.addressableAssetPath}")
                );

                File riveFile = LoadAndTrackFile(riveAsset);


                m_widget.Load(riveFile, testAsset.defaultArtboardName, testAsset.defaultStateMachineName);

                yield return new WaitUntil(() => m_widget.Status == WidgetStatus.Loaded);

                var viewModelInstance = m_widget.StateMachine.ViewModelInstance;
                Assert.IsNotNull(viewModelInstance, $"ViewModelInstance should exist for asset {testAsset.addressableAssetPath}");

                // Get an enum property to test
                string propName = testAsset.expectedEnumProperties[0]?.PropertyName;
                var enumProp = viewModelInstance.GetProperty<ViewModelInstanceEnumProperty>(propName);
                Assert.IsNotNull(enumProp, $"Enum property '{propName}' should exist in {testAsset.addressableAssetPath}");

                // Ensure we have enum values to test with
                Assert.IsTrue(enumProp.EnumValues.Count > 0,
                    $"Enum property '{propName}' should have at least one value in {testAsset.addressableAssetPath}");

                // Test setting by value
                string enumValue = enumProp.EnumValues[0];
                enumProp.Value = enumValue;
                Assert.AreEqual(enumValue, enumProp.Value,
                    $"Enum value should be '{enumValue}' in {testAsset.addressableAssetPath}");

                // Test setting by index (if we have at least 2 values)
                if (enumProp.EnumValues.Count > 1)
                {
                    enumProp.ValueIndex = 1;
                    Assert.AreEqual(1, enumProp.ValueIndex,
                        $"Enum index should be 1 in {testAsset.addressableAssetPath}");
                    Assert.AreEqual(enumProp.EnumValues[1], enumProp.Value,
                        $"Enum value should be '{enumProp.EnumValues[1]}' in {testAsset.addressableAssetPath}");
                }


            }
        }

        [UnityTest]
        public IEnumerator ImageProperty_CanBeSetAndTriggersCallback()
        {

            string testAssetPath = TestAssetReferences.riv_image_db_test;

            Asset riveAsset = null;
            yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                testAssetPath,
                (asset) => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {testAssetPath}")
            );

            File riveFile = LoadAndTrackFile(riveAsset);
            m_widget.Load(riveFile);
            yield return new WaitUntil(() => m_widget.Status == WidgetStatus.Loaded);

            var viewModelInstance = m_widget.StateMachine.ViewModelInstance;
            Assert.IsNotNull(viewModelInstance, "ViewModelInstance should exist");

            var imageProp = viewModelInstance.GetProperty<ViewModelInstanceImageProperty>("image");
            Assert.IsNotNull(imageProp, "Image property should exist");

            // minimal test image bytes (1x1 PNG)
            byte[] testImageBytes1 = new byte[] {
                0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D,
                0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
                0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53, 0xDE, 0x00, 0x00, 0x00,
                0x0C, 0x49, 0x44, 0x41, 0x54, 0x08, 0xD7, 0x63, 0xF8, 0x0F, 0x00, 0x00,
                0x01, 0x00, 0x01, 0x5C, 0xC2, 0x8A, 0x8E, 0x00, 0x00, 0x00, 0x00, 0x49,
                0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
            };

            // different test image bytes (1x1 PNG with different color)
            byte[] testImageBytes2 = new byte[] {
                0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D,
                0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
                0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53, 0xDE, 0x00, 0x00, 0x00,
                0x0C, 0x49, 0x44, 0x41, 0x54, 0x08, 0xD7, 0x63, 0xFC, 0x0F, 0x00, 0x00,
                0x01, 0x00, 0x01, 0x60, 0xC2, 0x8A, 0x8E, 0x00, 0x00, 0x00, 0x00, 0x49,
                0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
            };

            var testImage1 = OutOfBandAsset.Create<ImageOutOfBandAsset>(testImageBytes1);
            var testImage2 = OutOfBandAsset.Create<ImageOutOfBandAsset>(testImageBytes2);

            testImage1.Load();
            testImage2.Load();

            try
            {

                bool callbackTriggered = false;
                ImageOutOfBandAsset receivedImage = null;

                imageProp.OnValueChanged += (image) =>
                {
                    callbackTriggered = true;
                    receivedImage = image;
                };

                // Test setting image
                imageProp.Value = testImage1;
                viewModelInstance.HandleCallbacks();

                Assert.IsTrue(callbackTriggered, "Callback should be triggered when image is set");
                Assert.AreSame(testImage1, receivedImage, "Callback should receive the correct image");

                // Test setting different image
                callbackTriggered = false;
                receivedImage = null;

                imageProp.Value = testImage2;
                viewModelInstance.HandleCallbacks();

                Assert.IsTrue(callbackTriggered, "Callback should be triggered when image is changed");
                Assert.AreSame(testImage2, receivedImage, "Callback should receive the new image");

                // Test setting null to clear image
                callbackTriggered = false;

                imageProp.Value = null;
                viewModelInstance.HandleCallbacks();

                Assert.IsTrue(callbackTriggered, "Callback should still be triggered when image is set to null");

                Assert.IsNull(receivedImage, "Received image should be null when setting image to null");

                Assert.IsFalse(mockLogger.LoggedErrors.Count > 0 || mockLogger.LoggedWarnings.Count > 0,
                    "Should not log error when trying to set image to null");
            }
            finally
            {
                // Clean up
                if (testImage1 != null) testImage1.Unload();
                if (testImage2 != null) testImage2.Unload();
            }
        }

        [UnityTest]
        public IEnumerator ImageProperty_WithUnloadedAsset_LogsWarning()
        {

            string testAssetPath = TestAssetReferences.riv_image_db_test;

            Asset riveAsset = null;
            yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                testAssetPath,
                (asset) => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {testAssetPath}")
            );

            File riveFile = LoadAndTrackFile(riveAsset);
            m_widget.Load(riveFile);
            yield return new WaitUntil(() => m_widget.Status == WidgetStatus.Loaded);

            var viewModelInstance = m_widget.StateMachine.ViewModelInstance;
            var imageProp = viewModelInstance.GetProperty<ViewModelInstanceImageProperty>("image");
            Assert.IsNotNull(imageProp, "Image property should exist");

            var unloadedImage = ScriptableObject.CreateInstance<ImageOutOfBandAsset>();
            // Don't load the image - it should have NativeAsset == IntPtr.Zero

            mockLogger.Clear();
            imageProp.Value = unloadedImage;

            Assert.IsTrue(mockLogger.LoggedWarnings.Any(w => w.Contains("unloaded")),
                "Should log warning when trying to set unloaded image asset");
        }


        [UnityTest]
        public IEnumerator ListProperty_CanBeAccessedAndHasCorrectLength()
        {
            string testAssetPath = TestAssetReferences.riv_db_list_test;

            Asset riveAsset = null;
            yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                testAssetPath,
                (asset) => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {testAssetPath}")
            );

            File riveFile = LoadAndTrackFile(riveAsset);
            m_widget.Load(riveFile);
            yield return new WaitUntil(() => m_widget.Status == WidgetStatus.Loaded);

            var viewModelInstance = m_widget.StateMachine.ViewModelInstance;
            Assert.IsNotNull(viewModelInstance, "ViewModelInstance should exist");

            var listProperty = viewModelInstance.GetListProperty("items");

            if (listProperty != null)
            {
                Assert.IsNotNull(listProperty, "List property should exist");
                Assert.GreaterOrEqual(listProperty.Count, 0, "List length should be non-negative");

                var listPropertyViaGeneric = viewModelInstance.GetProperty<ViewModelInstanceListProperty>("items");
                Assert.AreSame(listProperty, listPropertyViaGeneric, "Both methods should return same instance");
            }
            else
            {
                Assert.Fail("No list property found in test asset");
            }
        }


        [UnityTest]
        public IEnumerator ListProperty_CanAddAndRemoveInstances()
        {
            string testAssetPath = TestAssetReferences.riv_db_list_test;

            Asset riveAsset = null;
            yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                testAssetPath,
                (asset) => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {testAssetPath}")
            );

            File riveFile = LoadAndTrackFile(riveAsset);
            m_widget.Load(riveFile);
            yield return new WaitUntil(() => m_widget.Status == WidgetStatus.Loaded);

            var viewModelInstance = m_widget.StateMachine.ViewModelInstance;
            var listProperty = viewModelInstance.GetListProperty("items");

            if (listProperty == null)
            {
                Assert.Fail("No list property found in test asset");
                yield break;
            }

            // Get the view model type for list items
            var itemViewModel = m_widget.File.GetViewModelByName("TodoItem");
            if (itemViewModel == null)
            {
                Assert.Fail("No item view model found for list testing");
                yield break;
            }

            int initialLength = listProperty.Count;

            // Create and add a new instance
            var newInstance = itemViewModel.CreateInstance();
            Assert.IsNotNull(newInstance, "Should be able to create new instance");

            listProperty.Add(newInstance);
            Assert.AreEqual(initialLength + 1, listProperty.Count, "Length should increase after adding instance");

            // Verify we can get the added instance
            var retrievedInstance = listProperty.GetInstanceAt(listProperty.Count - 1);
            Assert.IsNotNull(retrievedInstance, "Should be able to retrieve added instance");

            // Test that it is the same instance we added
            Assert.AreSame(newInstance, retrievedInstance, "Retrieved instance should be the same as added instance");

            // Remove the instance
            listProperty.Remove(newInstance);
            Assert.AreEqual(initialLength, listProperty.Count, "Length should return to original after removing instance");
        }

        [UnityTest]
        public IEnumerator ListProperty_RemoveMethod_RemovesEveryOccurenceOfInstance()
        {
            string testAssetPath = TestAssetReferences.riv_db_list_test;

            Asset riveAsset = null;
            yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                testAssetPath,
                (asset) => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {testAssetPath}")
            );

            m_widget.BindingMode = RiveWidget.DataBindingMode.Manual;
            File riveFile = LoadAndTrackFile(riveAsset);
            m_widget.Load(riveFile);
            yield return new WaitUntil(() => m_widget.Status == WidgetStatus.Loaded);

            var viewModelInstance = m_widget.Artboard.DefaultViewModel?.CreateInstance();
            if (viewModelInstance == null)
            {
                Assert.Fail("Failed to create view model instance from default artboard");
                yield break;
            }

            m_widget.StateMachine.BindViewModelInstance(viewModelInstance);

            var listProperty = viewModelInstance.GetListProperty("items");

            if (listProperty == null)
            {
                Assert.Fail("No list property found in test asset");
                yield break;
            }

            var itemViewModel = m_widget.File.GetViewModelByName("TodoItem");
            if (itemViewModel == null)
            {
                Assert.Fail("No item view model found for list testing");
                yield break;
            }

            var instance1 = itemViewModel.CreateInstance();

            listProperty.Add(instance1);
            listProperty.Add(instance1);

            Assert.AreEqual(2, listProperty.Count, "Length should be 2 after adding instances");

            listProperty.Remove(instance1);
            Assert.AreEqual(0, listProperty.Count, "Length should be 0 after removing instance");
        }

        [UnityTest]
        public IEnumerator ListProperty_CanInsertInstanceAtIndex()
        {
            string testAssetPath = TestAssetReferences.riv_db_list_test;

            Asset riveAsset = null;
            yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                testAssetPath,
                (asset) => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {testAssetPath}")
            );

            m_widget.BindingMode = RiveWidget.DataBindingMode.Manual;
            File riveFile = LoadAndTrackFile(riveAsset);
            m_widget.Load(riveFile);
            yield return new WaitUntil(() => m_widget.Status == WidgetStatus.Loaded);


            var viewModelInstance = m_widget.Artboard.DefaultViewModel?.CreateInstance();
            if (viewModelInstance == null)
            {
                Assert.Fail("Failed to create view model instance from default artboard");
                yield break;
            }

            m_widget.StateMachine.BindViewModelInstance(viewModelInstance);

            var listProperty = viewModelInstance.GetListProperty("items");

            if (listProperty == null)
            {
                Assert.Fail("No list property found in test asset");
                yield break;
            }

            // Get the view model type for list items
            var itemViewModel = m_widget.File.GetViewModelByName("TodoItem");
            if (itemViewModel == null)
            {
                Assert.Fail("No item view model found for list testing");
                yield break;
            }

            Assert.AreEqual(0, listProperty.Count, "List should start empty");

            var instance1 = itemViewModel.CreateInstance();
            var instance2 = itemViewModel.CreateInstance();
            var instance3 = itemViewModel.CreateInstance();

            Assert.IsNotNull(instance1, "Should be able to create first instance");
            Assert.IsNotNull(instance2, "Should be able to create second instance");
            Assert.IsNotNull(instance3, "Should be able to create third instance");

            // Add first two instances
            listProperty.Add(instance1);
            listProperty.Add(instance2);

            int initialLength = listProperty.Count;
            Assert.AreEqual(2, initialLength, "Should have 2 items after adding");

            // Insert at the beginning
            listProperty.Insert(instance3, 0);
            Assert.AreEqual(initialLength + 1, listProperty.Count, "Length should increase after inserting instance");

            // Verify the inserted instance is at the correct position
            var retrievedInstance = listProperty.GetInstanceAt(0);
            Assert.IsNotNull(retrievedInstance, "Should be able to retrieve inserted instance");
            Assert.AreSame(instance3, retrievedInstance, "Retrieved instance should be the same as inserted instance");

            // Verify the other instances shifted correctly
            Assert.AreSame(instance1, listProperty.GetInstanceAt(1), "First instance should now be at index 1");
            Assert.AreSame(instance2, listProperty.GetInstanceAt(2), "Second instance should now be at index 2");

            // Test inserting at an invalid index
            mockLogger.Clear();
            listProperty.Insert(itemViewModel.CreateInstance(), -1);
            Assert.IsTrue(mockLogger.LoggedWarnings.Count > 0 || mockLogger.LoggedErrors.Count > 0, "Should log warning when inserting at negative index");
            Assert.AreEqual(initialLength + 1, listProperty.Count, "Length should not change after failed insert");

            // Test inserting at the end
            var instance4 = itemViewModel.CreateInstance();
            listProperty.Insert(instance4, listProperty.Count);
            Assert.AreEqual(initialLength + 2, listProperty.Count, "Length should increase after inserting at end");
            Assert.AreSame(instance4, listProperty.GetInstanceAt(listProperty.Count - 1), "Instance should be at the end");
        }

        [UnityTest]
        public IEnumerator ListProperty_CanRemoveInstanceByIndex()
        {
            string testAssetPath = TestAssetReferences.riv_db_list_test;

            Asset riveAsset = null;
            yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                testAssetPath,
                (asset) => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {testAssetPath}")
            );

            File riveFile = LoadAndTrackFile(riveAsset);
            m_widget.Load(riveFile);
            yield return new WaitUntil(() => m_widget.Status == WidgetStatus.Loaded);


            var viewModelInstance = m_widget.StateMachine.ViewModelInstance;
            var listProperty = viewModelInstance.GetListProperty("items");

            if (listProperty == null)
            {
                Assert.Fail("No list property found in test asset");
                yield break;
            }

            var itemViewModel = m_widget.File.GetViewModelByName("TodoItem");
            if (itemViewModel == null)
            {
                Assert.Fail("No item view model found for list testing");
                yield break;
            }

            var instance1 = itemViewModel.CreateInstance();
            var instance2 = itemViewModel.CreateInstance();

            listProperty.Add(instance1);
            listProperty.Add(instance2);

            int lengthAfterAdding = listProperty.Count;
            Assert.GreaterOrEqual(lengthAfterAdding, 2, "Should have at least 2 items after adding");

            // Remove by index
            listProperty.RemoveAt(lengthAfterAdding - 1);
            Assert.AreEqual(lengthAfterAdding - 1, listProperty.Count, "Length should decrease after removing by index");

            // Test bounds checking
            mockLogger.Clear();
            listProperty.RemoveAt(-1);
            Assert.IsTrue(mockLogger.LoggedWarnings.Count > 0, "Should log warning for negative index");

            mockLogger.Clear();
            listProperty.RemoveAt(listProperty.Count);
            Assert.IsTrue(mockLogger.LoggedWarnings.Count > 0, "Should log warning for out of bounds index");
        }

        [UnityTest]
        public IEnumerator ListProperty_CanSwapInstances()
        {
            string testAssetPath = TestAssetReferences.riv_db_list_test;

            Asset riveAsset = null;
            yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                testAssetPath,
                (asset) => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {testAssetPath}")
            );

            File riveFile = LoadAndTrackFile(riveAsset);
            m_widget.Load(riveFile);
            yield return new WaitUntil(() => m_widget.Status == WidgetStatus.Loaded);

            var viewModelInstance = m_widget.StateMachine.ViewModelInstance;
            var listProperty = viewModelInstance.GetListProperty("items");

            if (listProperty == null)
            {
                Assert.Fail("No list property found in test asset");
                yield break;
            }

            var itemViewModel = m_widget.File.GetViewModelByName("TodoItem");
            if (itemViewModel == null)
            {
                Assert.Fail("No item view model found for list testing");
                yield break;
            }

            var instance1 = itemViewModel.CreateInstance();
            var instance2 = itemViewModel.CreateInstance();

            // We're setting different values to distinguish them
            var textProp1 = instance1.GetStringProperty("text");
            var textProp2 = instance2.GetStringProperty("text");

            if (textProp1 != null && textProp2 != null)
            {
                textProp1.Value = "First Item";
                textProp2.Value = "Second Item";
            }

            listProperty.Add(instance1);
            listProperty.Add(instance2);

            if (listProperty.Count >= 2)
            {
                // Get instances before swap
                var beforeSwapFirst = listProperty.GetInstanceAt(0);
                var beforeSwapSecond = listProperty.GetInstanceAt(1);

                // Swap instances
                listProperty.Swap(0, 1);

                // Verify swap occurred
                var afterSwapFirst = listProperty.GetInstanceAt(0);
                var afterSwapSecond = listProperty.GetInstanceAt(1);

                Assert.AreSame(beforeSwapFirst, afterSwapSecond, "First instance should now be at second position");
                Assert.AreSame(beforeSwapSecond, afterSwapFirst, "Second instance should now be at first position");
            }

            mockLogger.Clear();
            listProperty.Swap(0, 0);
            Assert.IsTrue(mockLogger.LoggedWarnings.Count > 0, "Should log warning when swapping same index");

            mockLogger.Clear();
            listProperty.Swap(-1, 0);
            Assert.IsTrue(mockLogger.LoggedWarnings.Count > 0, "Should log warning for negative index");

            mockLogger.Clear();
            listProperty.Swap(0, listProperty.Count);
            Assert.IsTrue(mockLogger.LoggedWarnings.Count > 0, "Should log warning for out of bounds index");
        }

        [UnityTest]
        public IEnumerator ListProperty_GetInstanceAt_HandlesInvalidIndices()
        {
            string testAssetPath = TestAssetReferences.riv_db_list_test;

            Asset riveAsset = null;
            yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                testAssetPath,
                (asset) => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {testAssetPath}")
            );

            File riveFile = LoadAndTrackFile(riveAsset);
            m_widget.Load(riveFile);
            yield return new WaitUntil(() => m_widget.Status == WidgetStatus.Loaded);

            var viewModelInstance = m_widget.StateMachine.ViewModelInstance;
            var listProperty = viewModelInstance.GetListProperty("items");

            if (listProperty == null)
            {
                Assert.Fail("No list property found in test asset");
                yield break;
            }

            // Test negative index
            mockLogger.Clear();
            var negativeResult = listProperty.GetInstanceAt(-1);
            Assert.IsNull(negativeResult, "Should return null for negative index");
            Assert.IsTrue(mockLogger.LoggedWarnings.Count > 0, "Should log warning for negative index");

            // Test out of bounds index
            mockLogger.Clear();
            var outOfBoundsResult = listProperty.GetInstanceAt(listProperty.Count);
            Assert.IsNull(outOfBoundsResult, "Should return null for out of bounds index");
            Assert.IsTrue(mockLogger.LoggedWarnings.Count > 0, "Should log warning for out of bounds index");
        }

        [UnityTest]
        public IEnumerator ListProperty_OnChanged_TriggersWhenListModified()
        {
            string testAssetPath = TestAssetReferences.riv_db_list_test;

            Asset riveAsset = null;
            yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                testAssetPath,
                (asset) => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {testAssetPath}")
            );

            File riveFile = LoadAndTrackFile(riveAsset);
            m_widget.Load(riveFile);
            yield return new WaitUntil(() => m_widget.Status == WidgetStatus.Loaded);

            var boundViewModelInstance = m_widget.StateMachine.ViewModelInstance;
            var listProperty = boundViewModelInstance.GetListProperty("items");

            if (listProperty == null)
            {
                Assert.Fail("No list property found in test asset");
                yield break;
            }

            var itemViewModel = m_widget.File.GetViewModelByName("TodoItem");
            if (itemViewModel == null)
            {
                Assert.Fail("No item view model found for list testing");
                yield break;
            }

            int changeCallbackCount = 0;
            listProperty.OnChanged += () => changeCallbackCount++;

            // Test adding instance triggers callback
            var newInstance = itemViewModel.CreateInstance();
            listProperty.Add(newInstance);
            boundViewModelInstance.HandleCallbacks();

            Assert.AreEqual(1, changeCallbackCount, "OnChanged should be triggered when adding instance");

            // Test removing instance triggers callback
            listProperty.Remove(newInstance);
            boundViewModelInstance.HandleCallbacks();

            Assert.AreEqual(2, changeCallbackCount, "OnChanged should be triggered when removing instance");

            // Test removing by index triggers callback
            if (listProperty.Count > 0)
            {
                listProperty.RemoveAt(0);
                boundViewModelInstance.HandleCallbacks();

                Assert.AreEqual(3, changeCallbackCount, "OnChanged should be triggered when removing by index");
            }

            // Test swapping triggers callback
            if (listProperty.Count >= 2)
            {
                listProperty.Swap(0, 1);
                boundViewModelInstance.HandleCallbacks();

                Assert.AreEqual(4, changeCallbackCount, "OnChanged should be triggered when swapping instances");
            }
        }

        [UnityTest]
        public IEnumerator ListProperty_InstancePropertyCallbacks_TriggeredAfterAddingToList()
        {
            string testAssetPath = TestAssetReferences.riv_db_list_test;

            Asset riveAsset = null;
            yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                testAssetPath,
                (asset) => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {testAssetPath}")
            );

            File riveFile = LoadAndTrackFile(riveAsset);
            m_widget.Load(riveFile);
            yield return new WaitUntil(() => m_widget.Status == WidgetStatus.Loaded);

            var boundViewModelInstance = m_widget.StateMachine.ViewModelInstance;
            var listProperty = boundViewModelInstance.GetListProperty("items");

            if (listProperty == null)
            {
                Assert.Fail("No list property found in test asset");
                yield break;
            }

            var itemViewModel = m_widget.File.GetViewModelByName("TodoItem");
            if (itemViewModel == null)
            {
                Assert.Fail("No item view model found for list testing");
                yield break;
            }

            var newInstance = itemViewModel.CreateInstance();
            Assert.IsNotNull(newInstance, "Should be able to create new instance");

            // Get a property from the instance to test callbacks
            var textProperty = newInstance.GetStringProperty("text");
            if (textProperty == null)
            {
                Assert.Fail("No text property found in item view model for callback testing");
                yield break;
            }

            int callbackCount = 0;
            string lastCallbackValue = null;

            textProperty.OnValueChanged += (value) =>
            {
                callbackCount++;
                lastCallbackValue = value;
            };

            // Set initial value before adding to list
            string initialValue = "Initial Value";
            textProperty.Value = initialValue;

            // Process callbacks for the standalone instance
            newInstance.HandleCallbacks();

            Assert.AreEqual(1, callbackCount, "Callback should be triggered for standalone instance");
            Assert.AreEqual(initialValue, lastCallbackValue, "Callback should receive correct initial value");

            // Add the instance to the list
            listProperty.Add(newInstance);

            Assert.AreEqual(newInstance, listProperty.GetInstanceAt(listProperty.Count - 1),
                "Instance should be retrievable from list");

            // Reset callback tracking
            callbackCount = 0;
            lastCallbackValue = null;

            // Change the property value after adding to list
            string updatedValue = "Updated Value After Adding To List";
            textProperty.Value = updatedValue;

            // Process callbacks - this should work for instances in the list
            boundViewModelInstance.HandleCallbacks();

            Assert.AreEqual(1, callbackCount,
                "Callback should be triggered for instance property after adding to list");
            Assert.AreEqual(updatedValue, lastCallbackValue,
                "Callback should receive correct updated value");

            // Test that we can also access the property through the list and get the same callback behavior
            var retrievedInstance = listProperty.GetInstanceAt(listProperty.Count - 1);
            var retrievedTextProperty = retrievedInstance.GetStringProperty("text");

            Assert.AreSame(textProperty, retrievedTextProperty,
                "Property retrieved from list instance should be the same object");

            // Reset callback tracking again
            callbackCount = 0;
            lastCallbackValue = null;

            // Change value through the retrieved property
            string finalValue = "Final Value Through Retrieved Property";
            retrievedTextProperty.Value = finalValue;

            // Process callbacks through the retrieved instance
            boundViewModelInstance.HandleCallbacks();

            Assert.AreEqual(1, callbackCount,
                "Callback should be triggered when changing property through retrieved instance");
            Assert.AreEqual(finalValue, lastCallbackValue,
                "Callback should receive correct final value");

            // Verify both property references show the same value
            Assert.AreEqual(finalValue, textProperty.Value,
                "Original property reference should show updated value");
            Assert.AreEqual(finalValue, retrievedTextProperty.Value,
                "Retrieved property reference should show updated value");


            // Reset callback tracking again
            callbackCount = 0;
            lastCallbackValue = null;

            // Remove the instance from the list with removeInstance and verify that callbacks are not triggered
            listProperty.Remove(newInstance);

            boundViewModelInstance.HandleCallbacks();
            Assert.AreEqual(0, callbackCount,
                "Callback should not be triggered when removing instance from list");

            // Add the instance back to the list and then remove it by index
            listProperty.Add(newInstance);
            Assert.AreEqual(newInstance, listProperty.GetInstanceAt(listProperty.Count - 1),
                "Instance should be retrievable from list after re-adding");

            // Reset callback tracking again
            callbackCount = 0;
            lastCallbackValue = null;

            // Remove by index
            listProperty.RemoveAt(listProperty.Count - 1);

            boundViewModelInstance.HandleCallbacks();

            Assert.AreEqual(0, callbackCount,
                "Callback should not be triggered when removing instance by index");

        }

        [UnityTest]
        public IEnumerator PropertyCache_ReturnsSameInstance()
        {
            List<DataBindingTestAsset> testAssetsWithStringProperties = GetTestAssetInfo().Where(a =>
                           a.addressableAssetPath == TestAssetReferences.riv_asset_databinding_test).ToList();

            foreach (var testAsset in testAssetsWithStringProperties)
            {
                Asset riveAsset = null;

                yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                    testAsset.addressableAssetPath,
                    (asset) => riveAsset = asset,
                    () => Assert.Fail($"Failed to load asset at {testAsset.addressableAssetPath}")
                );

                File riveFile = LoadAndTrackFile(riveAsset);


                m_widget.Load(riveFile, testAsset.defaultArtboardName, testAsset.defaultStateMachineName);

                yield return new WaitUntil(() => m_widget.Status == WidgetStatus.Loaded);

                var viewModelInstance = m_widget.StateMachine.ViewModelInstance;
                Assert.IsNotNull(viewModelInstance, $"ViewModelInstance should exist for asset {testAsset.addressableAssetPath}");

                // Get a string property to test caching

                string propName = GetPropertyInfoOfType(testAsset, ViewModelDataType.String).FirstOrDefault()?.Name;
                Assert.IsNotNull(propName, $"String property name should not be null in {testAsset.addressableAssetPath}");

                // Get the same property twice
                var prop1 = viewModelInstance.GetProperty<ViewModelInstanceStringProperty>(propName);
                var prop2 = viewModelInstance.GetProperty<ViewModelInstanceStringProperty>(propName);

                Assert.IsNotNull(prop1, $"Property '{propName}' should exist in {testAsset.addressableAssetPath}");
                Assert.IsNotNull(prop2, $"Property '{propName}' should exist on second retrieval in {testAsset.addressableAssetPath}");
                Assert.AreSame(prop1, prop2,
                    $"Both property references should be the same instance in {testAsset.addressableAssetPath}");

            }
        }

        [UnityTest]
        public IEnumerator MultipleCallbacks_AreAllTriggered()
        {
            List<DataBindingTestAsset> testAssetsWithStringProperties = GetTestAssetInfo().Where(a =>
                           a.addressableAssetPath == TestAssetReferences.riv_asset_databinding_test).ToList();

            foreach (var testAsset in testAssetsWithStringProperties)
            {
                Asset riveAsset = null;

                yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                    testAsset.addressableAssetPath,
                    (asset) => riveAsset = asset,
                    () => Assert.Fail($"Failed to load asset at {testAsset.addressableAssetPath}")
                );

                File riveFile = LoadAndTrackFile(riveAsset);


                m_widget.Load(riveFile, testAsset.defaultArtboardName, testAsset.defaultStateMachineName);

                yield return new WaitUntil(() => m_widget.Status == WidgetStatus.Loaded);

                var viewModelInstance = m_widget.StateMachine.ViewModelInstance;
                Assert.IsNotNull(viewModelInstance, $"ViewModelInstance should exist for asset {testAsset.addressableAssetPath}");

                // Get a string property to test multiple callbacks
                string propName = GetPropertyInfoOfType(testAsset, ViewModelDataType.String).FirstOrDefault()?.Name;
                Assert.IsNotNull(propName, $"String property name should not be null in {testAsset.addressableAssetPath}");
                var stringProp = viewModelInstance.GetProperty<ViewModelInstanceStringProperty>(propName);
                Assert.IsNotNull(stringProp, $"String property '{propName}' should exist in {testAsset.addressableAssetPath}");

                int callbackCount1 = 0;
                int callbackCount2 = 0;
                string expectedValue = "Multiple Callbacks Test";

                // Add multiple callbacks
                stringProp.OnValueChanged += (value) =>
                {
                    callbackCount1++;
                    Assert.AreEqual(expectedValue, value);
                    Assert.AreEqual(expectedValue, stringProp.Value);
                };

                stringProp.OnValueChanged += (value) =>
                {
                    callbackCount2++;
                    Assert.AreEqual(expectedValue, value);
                    Assert.AreEqual(expectedValue, stringProp.Value);
                };

                stringProp.Value = expectedValue;

                // Process callbacks
                viewModelInstance.HandleCallbacks();

                Assert.AreEqual(1, callbackCount1,
                    $"First callback should have been triggered once for property '{propName}' in {testAsset.addressableAssetPath}");
                Assert.AreEqual(1, callbackCount2,
                    $"Second callback should have been triggered once for property '{propName}' in {testAsset.addressableAssetPath}");

            }
        }



        [UnityTest]
        public IEnumerator NestedViewModelProperties_CanBeAccessedAndModified()
        {

            List<DataBindingTestAsset> testAssetsWithNestedViewModels = GetTestAssetInfo().Where(a =>
                           a.addressableAssetPath == TestAssetReferences.riv_asset_databinding_test).ToList();

            Assert.Greater(testAssetsWithNestedViewModels.Count, 0,
                "At least one test asset should have nested view models defined");

            foreach (var testAsset in testAssetsWithNestedViewModels)
            {

                Asset riveAsset = null;

                yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                    testAsset.addressableAssetPath,
                    (asset) => riveAsset = asset,
                    () => Assert.Fail($"Failed to load asset at {testAsset.addressableAssetPath}")
                );

                File riveFile = LoadAndTrackFile(riveAsset);


                m_widget.Load(riveFile, testAsset.defaultArtboardName, testAsset.defaultStateMachineName);

                yield return new WaitUntil(() => m_widget.Status == WidgetStatus.Loaded);

                var viewModelInstance = m_widget.StateMachine.ViewModelInstance;
                Assert.IsNotNull(viewModelInstance, $"ViewModelInstance should exist for asset {testAsset.addressableAssetPath}");

                // Get a nested view model to test
                string nestedModelName = testAsset.expectedViewModelsInFile[0].Properties
                    .FirstOrDefault(p => p.Type == ViewModelDataType.ViewModel)?.Name;

                Assert.IsNotNull(nestedModelName, $"Nested view model name should not be null in {testAsset.addressableAssetPath}");
                var nestedViewModel = viewModelInstance.GetProperty<ViewModelInstance>(nestedModelName);
                Assert.IsNotNull(nestedViewModel,
                    $"Nested view model '{nestedModelName}' should exist in {testAsset.addressableAssetPath}");

                // Find a nested property to test
                string nestedPropertyKey = null;
                foreach (var key in testAsset.testPropertyValuesToSet.Keys)
                {
                    if (key.Contains("/"))
                    {
                        nestedPropertyKey = key;
                        break;
                    }
                }

                // Skip if no nested property found to test
                if (nestedPropertyKey == null)
                {
                    Debug.LogWarning($"No test property found for nested view model '{nestedModelName}' in {testAsset.addressableAssetPath}");
                    yield return null;
                    continue;
                }

                // Get the property name
                string propertyName = nestedPropertyKey.Split('/')[1];

                // Test string property in nested view model
                if (testAsset.testPropertyValuesToSet[nestedPropertyKey].PropertyType == typeof(ViewModelInstanceStringProperty))
                {
                    var stringProp = nestedViewModel.GetProperty<ViewModelInstanceStringProperty>(propertyName);
                    Assert.IsNotNull(stringProp,
                        $"String property '{propertyName}' should exist in nested view model '{nestedModelName}' in {testAsset.addressableAssetPath}");
                    string stringValue = testAsset.testPropertyValuesToSet[nestedPropertyKey].ValueToSet as string;
                    // Set and verify value
                    stringProp.Value = stringValue;
                    Assert.AreEqual(stringValue, stringProp.Value,
                        $"Nested property '{nestedPropertyKey}' value should be '{stringValue}' in {testAsset.addressableAssetPath}");

                    // Test callback
                    bool callbackTriggered = false;
                    stringProp.OnValueChanged += (value) =>
                    {
                        callbackTriggered = true;
                        var expectedValue = stringValue + " Updated";
                        Assert.AreEqual(expectedValue, value);
                        Assert.AreEqual(expectedValue, stringProp.Value);
                    };

                    // Update value
                    stringProp.Value = stringValue + " Updated";

                    // Process callbacks
                    nestedViewModel.HandleCallbacks();

                    // Assert callback was triggered
                    Assert.IsTrue(callbackTriggered,
                        $"Callback should have been triggered for nested property '{nestedPropertyKey}' in {testAsset.addressableAssetPath}");
                }

            }
        }

        [UnityTest]
        public IEnumerator UnsubscribingCallbacks_StopsNotifications()
        {

            List<DataBindingTestAsset> testAssetsWithStringProperties = GetTestAssetInfo().Where(a =>
                           a.addressableAssetPath == TestAssetReferences.riv_asset_databinding_test).ToList();
            Assert.Greater(testAssetsWithStringProperties.Count, 0,
                "At least one test asset should have string properties defined");

            foreach (var testAsset in testAssetsWithStringProperties)
            {
                Asset riveAsset = null;

                yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                    testAsset.addressableAssetPath,
                    (asset) => riveAsset = asset,
                    () => Assert.Fail($"Failed to load asset at {testAsset.addressableAssetPath}")
                );

                File riveFile = LoadAndTrackFile(riveAsset);


                m_widget.Load(riveFile, testAsset.defaultArtboardName, testAsset.defaultStateMachineName);

                yield return new WaitUntil(() => m_widget.Status == WidgetStatus.Loaded);

                var viewModelInstance = m_widget.StateMachine.ViewModelInstance;
                Assert.IsNotNull(viewModelInstance, $"ViewModelInstance should exist for asset {testAsset.addressableAssetPath}");

                // Get a string property to test callback unsubscription
                string propName = GetPropertyInfoOfType(testAsset, ViewModelDataType.String).FirstOrDefault()?.Name;
                Assert.IsNotNull(propName, $"String property name should not be null in {testAsset.addressableAssetPath}");

                var stringProp = viewModelInstance.GetProperty<ViewModelInstanceStringProperty>(propName);
                Assert.IsNotNull(stringProp, $"String property '{propName}' should exist in {testAsset.addressableAssetPath}");

                int callbackCount = 0;
                Action<string> callback = (value) =>
                {
                    callbackCount++;
                };

                stringProp.OnValueChanged += callback;

                // Set value and process callbacks
                stringProp.Value = "First Update";
                viewModelInstance.HandleCallbacks();

                Assert.AreEqual(1, callbackCount,
                    $"Callback should have been triggered once for property '{propName}' in {testAsset.addressableAssetPath}");

                // Remove callback
                stringProp.OnValueChanged -= callback;

                // Set value again and process callbacks
                stringProp.Value = "Second Update";
                viewModelInstance.HandleCallbacks();

                // Assert callback was not triggered again
                Assert.AreEqual(1, callbackCount,
                    $"Callback should not have been triggered after unsubscribing for property '{propName}' in {testAsset.addressableAssetPath}");


            }
        }

        [UnityTest]
        public IEnumerator InvalidPropertyAccess_ReturnsNullAndLogsWarning()
        {
            foreach (var testAsset in GetTestAssetInfo())
            {
                Asset riveAsset = null;

                yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                    testAsset.addressableAssetPath,
                    (asset) => riveAsset = asset,
                    () => Assert.Fail($"Failed to load asset at {testAsset.addressableAssetPath}")
                );

                File riveFile = LoadAndTrackFile(riveAsset);


                m_widget.Load(riveFile, testAsset.defaultArtboardName, testAsset.defaultStateMachineName);

                yield return new WaitUntil(() => m_widget.Status == WidgetStatus.Loaded);

                var viewModelInstance = m_widget.StateMachine.ViewModelInstance;
                Assert.IsNotNull(viewModelInstance, $"ViewModelInstance should exist for asset {testAsset.addressableAssetPath}");

                mockLogger.Clear();

                var nonExistentProp = viewModelInstance.GetProperty<ViewModelInstanceStringProperty>("nonExistentProperty");

                Assert.IsNull(nonExistentProp,
                    $"Non-existent property should return null in {testAsset.addressableAssetPath}");
                Assert.IsTrue(mockLogger.AnyLogTypeContains("Property not found"),
                    $"Warning should be logged when accessing non-existent property in {testAsset.addressableAssetPath}");


            }
        }


        [UnityTest]
        public IEnumerator NestedViewModelInstance_CanBeReplaced()
        {
            DataBindingTestAsset testAsset = GetTestAssetInfo().First(a =>
                a.addressableAssetPath == TestAssetReferences.riv_asset_databinding_test);

            Asset riveAsset = null;
            yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                testAsset.addressableAssetPath,
                (asset) => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {testAsset.addressableAssetPath}")
            );

            File riveFile = LoadAndTrackFile(riveAsset);
            m_widget.Load(riveFile, testAsset.defaultArtboardName, testAsset.defaultStateMachineName);
            yield return new WaitUntil(() => m_widget.Status == WidgetStatus.Loaded);

            var viewModelInstance = m_widget.StateMachine.ViewModelInstance;
            Assert.IsNotNull(viewModelInstance, $"ViewModelInstance should exist for asset {testAsset.addressableAssetPath}");

            string nestedModelPropertyName = "favDrink";

            var originalNestedViewModelInstance = viewModelInstance.GetProperty<ViewModelInstance>(nestedModelPropertyName);
            Assert.IsNotNull(originalNestedViewModelInstance,
                $"Nested view model '{nestedModelPropertyName}' should exist in {testAsset.addressableAssetPath}");

            string nestedViewModelTypeName = "DrinkViewModel";

            var viewModelType = m_widget.File.GetViewModelByName(nestedViewModelTypeName);
            Assert.IsNotNull(viewModelType,
                $"View model type '{nestedViewModelTypeName}' should exist in the file");

            var newNestedViewModelInstance = viewModelType.CreateInstance();
            Assert.IsNotNull(newNestedViewModelInstance,
                $"Should be able to create a new instance of '{nestedViewModelTypeName}'");

            // The name property for the drink view model
            string testPropertyName = "name";

            // Set a value on the original instance
            var originalStringProp = originalNestedViewModelInstance.GetProperty<ViewModelInstanceStringProperty>(testPropertyName);
            Assert.IsNotNull(originalStringProp, $"String property '{testPropertyName}' should exist in nested view model");
            string originalValue = "Original Drink";
            originalStringProp.Value = originalValue;
            Assert.AreEqual(originalValue, originalStringProp.Value,
                $"Original property value should be set correctly");

            // Set a different value on the new instance
            var newStringProp = newNestedViewModelInstance.GetProperty<ViewModelInstanceStringProperty>(testPropertyName);
            Assert.IsNotNull(newStringProp, $"String property '{testPropertyName}' should exist in new nested view model");
            string newValue = "New Drink";
            newStringProp.Value = newValue;
            Assert.AreEqual(newValue, newStringProp.Value,
                $"New property value should be set correctly");

            // Replace the nested view model with the new instance
            mockLogger.Clear();

            viewModelInstance.SetViewModelInstance(nestedModelPropertyName, newNestedViewModelInstance);

            var replacedViewModel = viewModelInstance.GetProperty<ViewModelInstance>(nestedModelPropertyName);
            Assert.IsNotNull(replacedViewModel,
                $"Replaced view model should not be null");

            var replacedStringProp = replacedViewModel.GetProperty<ViewModelInstanceStringProperty>(testPropertyName);
            Assert.AreEqual(newValue, replacedStringProp.Value,
                $"After replacement, property value should match new instance value");

            Assert.AreEqual(0, mockLogger.LoggedErrors.Count,
                $"No errors should be logged during successful replacement in {testAsset.addressableAssetPath} . Logged errors: {string.Join(", ", mockLogger.LoggedErrors)}");


            // Let's also try to fetch the property from the top level view model instance just to make sure it works with nested paths
            var topLevelStringProp = viewModelInstance.GetProperty<ViewModelInstanceStringProperty>($"{nestedModelPropertyName}/{testPropertyName}");

            Assert.IsNotNull(topLevelStringProp, $"Top-level property '{nestedModelPropertyName}/{testPropertyName}' should exist in {testAsset.addressableAssetPath}");

            Assert.AreEqual(newValue, topLevelStringProp.Value,
                $"Top-level property value should match new instance value");

            // Test with invalid vm property path, we want to make sure it also doesn't crash the app 
            mockLogger.Clear();
            viewModelInstance.SetViewModelInstance("nonExistentPath", newNestedViewModelInstance);
            Assert.Greater(mockLogger.LoggedErrors.Count, 0,
                $"An error should be logged when replacing with an invalid path");

            // Same thing with passing a null instance
            mockLogger.Clear();
            viewModelInstance.SetViewModelInstance(nestedModelPropertyName, null);
            Assert.Greater(mockLogger.LoggedErrors.Count, 0,
                $"An error should be logged when replacing with a null instance");

            // Try to replace with an instance of a different view model type. It should fail if you try to pass a PersonViewModel instance to a DrinkViewModel property
            var differentViewModelType = m_widget.File.GetViewModelByName("PersonViewModel");
            Assert.IsNotNull(differentViewModelType,
                $"Different view model type should exist in the file");

            var differentNestedViewModelInstance = differentViewModelType.CreateInstance();
            Assert.IsNotNull(differentNestedViewModelInstance,
                $"Should be able to create a new instance of 'PersonViewModel'");

            mockLogger.Clear();
            viewModelInstance.SetViewModelInstance(nestedModelPropertyName, differentNestedViewModelInstance);

            Assert.Greater(mockLogger.LoggedErrors.Count, 0,
                $"An error should be logged when replacing with a different view model type");

        }

        [UnityTest]
        public IEnumerator NestedViewModelInstance_CanBeReusedAcrossWidgets()
        {
            DataBindingTestAsset testAsset = GetTestAssetInfo().First(a =>
                a.addressableAssetPath == TestAssetReferences.riv_asset_databinding_test);

            Asset riveAsset = null;
            yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                testAsset.addressableAssetPath,
                (asset) => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {testAsset.addressableAssetPath}")
            );

            File riveFile = LoadAndTrackFile(riveAsset);

            GameObject secondWidgetObject = new GameObject("SecondRiveWidget");
            RiveWidget secondWidget = secondWidgetObject.AddComponent<RiveWidget>();
            secondWidget.transform.SetParent(m_panel.WidgetContainer, false);

            GameObject thirdWidgetObject = new GameObject("ThirdRiveWidget");
            RiveWidget thirdWidget = thirdWidgetObject.AddComponent<RiveWidget>();
            thirdWidget.transform.SetParent(m_panel.WidgetContainer, false);

            try
            {
                m_widget.Load(riveFile, testAsset.defaultArtboardName, testAsset.defaultStateMachineName);
                yield return new WaitUntil(() => m_widget.Status == WidgetStatus.Loaded);

                secondWidget.Load(riveFile, testAsset.defaultArtboardName, testAsset.defaultStateMachineName);
                yield return new WaitUntil(() => secondWidget.Status == WidgetStatus.Loaded);

                thirdWidget.Load(riveFile, testAsset.defaultArtboardName, testAsset.defaultStateMachineName);
                yield return new WaitUntil(() => thirdWidget.Status == WidgetStatus.Loaded);

                var firstViewModelInstance = m_widget.StateMachine.ViewModelInstance;
                var secondViewModelInstance = secondWidget.StateMachine.ViewModelInstance;
                var thirdViewModelInstance = thirdWidget.StateMachine.ViewModelInstance;

                Assert.IsNotNull(firstViewModelInstance, "First widget should have a view model instance");
                Assert.IsNotNull(secondViewModelInstance, "Second widget should have a view model instance");
                Assert.IsNotNull(thirdViewModelInstance, "Third widget should have a view model instance");

                // Create a shared nested view model instance
                string nestedViewModelTypeName = "DrinkViewModel";
                var viewModelType = m_widget.File.GetViewModelByName(nestedViewModelTypeName);
                Assert.IsNotNull(viewModelType, $"View model type '{nestedViewModelTypeName}' should exist");

                var sharedNestedViewModelInstance = viewModelType.CreateInstance();
                Assert.IsNotNull(sharedNestedViewModelInstance, $"Should be able to create a '{nestedViewModelTypeName}' instance");

                // Set up a test property on the shared instance
                string testPropertyName = "name";
                var sharedStringProp = sharedNestedViewModelInstance.GetProperty<ViewModelInstanceStringProperty>(testPropertyName);
                Assert.IsNotNull(sharedStringProp, $"Property '{testPropertyName}' should exist");

                string initialValue = "Shared Drink";
                sharedStringProp.Value = initialValue;

                int callbackCount = 0;

                // Add callback on the shared instance
                sharedStringProp.OnValueChanged += (value) =>
                {
                    callbackCount++;
                };

                // Replace nested view model in all widgets with the shared instance
                string nestedModelPropertyName = "favDrink";

                var initialFirstNestedViewModelInstance = firstViewModelInstance.GetProperty<ViewModelInstance>(nestedModelPropertyName);
                var initialSecondNestedViewModelInstance = secondViewModelInstance.GetProperty<ViewModelInstance>(nestedModelPropertyName);
                var initialThirdNestedViewModelInstance = thirdViewModelInstance.GetProperty<ViewModelInstance>(nestedModelPropertyName);

                firstViewModelInstance.SetViewModelInstance(nestedModelPropertyName, sharedNestedViewModelInstance);
                secondViewModelInstance.SetViewModelInstance(nestedModelPropertyName, sharedNestedViewModelInstance);
                thirdViewModelInstance.SetViewModelInstance(nestedModelPropertyName, sharedNestedViewModelInstance);

                // Verify replacement worked
                var firstNestedViewModelInstance = firstViewModelInstance.GetProperty<ViewModelInstance>(nestedModelPropertyName);
                var secondNestedViewModelInstance = secondViewModelInstance.GetProperty<ViewModelInstance>(nestedModelPropertyName);
                var thirdNestedViewModelInstance = thirdViewModelInstance.GetProperty<ViewModelInstance>(nestedModelPropertyName);

                Assert.IsNotNull(firstNestedViewModelInstance, "First widget should have the nested view model");
                Assert.IsNotNull(secondNestedViewModelInstance, "Second widget should have the nested view model");
                Assert.IsNotNull(thirdNestedViewModelInstance, "Third widget should have the nested view model");

                Assert.AreNotSame(initialFirstNestedViewModelInstance, firstNestedViewModelInstance,
                    "First widget should have a different nested view model instance after replacement");
                Assert.AreNotSame(initialSecondNestedViewModelInstance, secondNestedViewModelInstance,
                    "Second widget should have a different nested view model instance after replacement");
                Assert.AreNotSame(initialThirdNestedViewModelInstance, thirdNestedViewModelInstance,
                    "Third widget should have a different nested view model instance after replacement");

                // Verify they are all the SAME instance.  We don't want different C# class instances for the same native pointer.
                Assert.AreSame(sharedNestedViewModelInstance, firstNestedViewModelInstance,
                    "First widget should have the same shared instance");
                Assert.AreSame(sharedNestedViewModelInstance, secondNestedViewModelInstance,
                    "Second widget should have the same shared instance");
                Assert.AreSame(sharedNestedViewModelInstance, thirdNestedViewModelInstance,
                    "Third widget should have the same shared instance");

                Assert.AreSame(firstNestedViewModelInstance, secondNestedViewModelInstance,
                    "First and second widgets should have the same instance");
                Assert.AreSame(secondNestedViewModelInstance, thirdNestedViewModelInstance,
                    "Second and third widgets should have the same instance");

                string newValue = "Updated Shared Drink";
                sharedStringProp.Value = newValue;

                // Process callbacks for ALL widgets. This is equivalent to Advance() happening on all widgets.
                // In a real scenario, this would be called in the main loop of the widget.
                firstViewModelInstance.HandleCallbacks();
                secondViewModelInstance.HandleCallbacks();
                thirdViewModelInstance.HandleCallbacks();

                // Verify callback only fired once despite being in three widgets
                Assert.AreEqual(1, callbackCount,
                    "Callback should only be triggered once despite being used in three widgets");

                // Verify property values were updated in all widgets
                var firstProp = firstViewModelInstance.GetProperty<ViewModelInstanceStringProperty>(
                    $"{nestedModelPropertyName}/{testPropertyName}");
                var secondProp = secondViewModelInstance.GetProperty<ViewModelInstanceStringProperty>(
                    $"{nestedModelPropertyName}/{testPropertyName}");
                var thirdProp = thirdViewModelInstance.GetProperty<ViewModelInstanceStringProperty>(
                    $"{nestedModelPropertyName}/{testPropertyName}");

                Assert.AreEqual(newValue, firstProp.Value, "Property value should be updated in first widget");
                Assert.AreEqual(newValue, secondProp.Value, "Property value should be updated in second widget");
                Assert.AreEqual(newValue, thirdProp.Value, "Property value should be updated in third widget");

                // Check that getting the property from different widgets returns the same instance
                Assert.IsTrue(ReferenceEquals(firstProp, secondProp), "Second widget should be the same instance as first widget");
                Assert.IsTrue(ReferenceEquals(secondProp, thirdProp), "Third widget should be the same instance as second widget");


                // Change from third widget and verify
                callbackCount = 0; // Reset counter
                thirdProp.Value = "Changed from third widget";

                thirdViewModelInstance.HandleCallbacks();

                // Verify callback fired exactly once
                Assert.AreEqual(1, callbackCount, "Callback should fire exactly once when changed from third widget");

                // All widgets should have the updated value
                Assert.AreEqual(thirdProp.Value, firstProp.Value, "First widget value should match third widget");
                Assert.AreEqual(thirdProp.Value, secondProp.Value, "Second widget value should match third widget");

                // Change from first widget and verify
                callbackCount = 0; // Reset counter
                firstProp.Value = "Changed from first widget";

                // Only process callbacks on first widget
                firstViewModelInstance.HandleCallbacks();

                // Verify callback fired exactly once
                Assert.AreEqual(1, callbackCount, "Callback should fire exactly once when changed from first widget");

                // All widgets should have the updated value
                Assert.AreEqual(firstProp.Value, secondProp.Value, "Second widget value should match first widget");
                Assert.AreEqual(firstProp.Value, thirdProp.Value, "Third widget value should match first widget");
            }
            finally
            {
                // Clean up
                if (secondWidgetObject != null)
                {
                    Object.Destroy(secondWidgetObject);
                }

                if (thirdWidgetObject != null)
                {
                    Object.Destroy(thirdWidgetObject);
                }
            }
        }

        [UnityTest]
        public IEnumerator MultiplePropertyChanges_AllDetectedInSingleHandleCallbacks()
        {
            // Helper to add a property of specific type to test list. Returns true if successful
            bool CollectPropertyForTesting<T>(
               DataBindingTestAsset testAsset,
               ViewModelInstance viewModelInstance,
               List<(ViewModelInstancePrimitiveProperty, object, int)> testProps,
               ViewModelDataType dataType) where T : ViewModelInstancePrimitiveProperty
            {
                // Find a property of this type
                var propInfo = GetPropertyInfoOfType(testAsset, dataType).FirstOrDefault();
                if (propInfo == null) return false;

                var prop = viewModelInstance.GetProperty<T>(propInfo.Name);
                if (prop == null) return false;

                // Generate an appropriate test value
                object newValue = null;

                if (prop is ViewModelInstanceStringProperty)
                    newValue = $"Test_{Guid.NewGuid().ToString().Substring(0, 8)}";
                else if (prop is ViewModelInstanceNumberProperty)
                    newValue = UnityEngine.Random.Range(1f, 100f);
                else if (prop is ViewModelInstanceBooleanProperty boolProp)
                    newValue = !boolProp.Value;
                else if (prop is ViewModelInstanceColorProperty)
                    newValue = new Color32((byte)UnityEngine.Random.Range(0f, 1f), (byte)UnityEngine.Random.Range(0f, 1f), (byte)UnityEngine.Random.Range(0f, 1f), 1);
                else if (prop is ViewModelInstanceEnumProperty enumProp && enumProp.EnumValues.Count > 0)
                {
                    // Choose a different enum value if possible
                    int nextIndex = (enumProp.ValueIndex + 1) % enumProp.EnumValues.Count;
                    newValue = enumProp.EnumValues[nextIndex];
                }

                if (newValue != null)
                {
                    testProps.Add((prop, newValue, 0));
                    return true;
                }

                return false;
            }

            var testAssets = GetTestAssetInfo().Where(a =>
                           a.addressableAssetPath == TestAssetReferences.riv_asset_databinding_test);
            foreach (var testAsset in testAssets)
            {
                Asset riveAsset = null;
                yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                    testAsset.addressableAssetPath,
                    (asset) => riveAsset = asset,
                    () => Assert.Fail($"Failed to load asset at {testAsset.addressableAssetPath}")
                );

                File riveFile = LoadAndTrackFile(riveAsset);
                m_widget.Load(riveFile, testAsset.defaultArtboardName, testAsset.defaultStateMachineName);
                yield return new WaitUntil(() => m_widget.Status == WidgetStatus.Loaded);

                var viewModelInstance = m_widget.StateMachine.ViewModelInstance;
                Assert.IsNotNull(viewModelInstance, $"ViewModelInstance should exist for asset {testAsset.addressableAssetPath}");

                // Find properties of different types to test with. 
                var testProps = new List<(ViewModelInstancePrimitiveProperty Property, object NewValue, int CallbackCount)>();

                // Try to add one string property and one property of a different type
                CollectPropertyForTesting<ViewModelInstanceStringProperty>(testAsset, viewModelInstance, testProps, ViewModelDataType.String);

                // Try adding a different type of property if available. We want to verify that different types of properties can be changed and trigger callbacks
                if (!CollectPropertyForTesting<ViewModelInstanceNumberProperty>(testAsset, viewModelInstance, testProps, ViewModelDataType.Number))
                    if (!CollectPropertyForTesting<ViewModelInstanceBooleanProperty>(testAsset, viewModelInstance, testProps, ViewModelDataType.Boolean))
                        if (!CollectPropertyForTesting<ViewModelInstanceColorProperty>(testAsset, viewModelInstance, testProps, ViewModelDataType.Color))
                            CollectPropertyForTesting<ViewModelInstanceEnumProperty>(testAsset, viewModelInstance, testProps, ViewModelDataType.Enum);

                // Ensure we have at least two properties to test
                Assert.GreaterOrEqual(testProps.Count, 2,
                    $"Need at least two properties to test multiple callbacks in {testAsset.addressableAssetPath}");

                // Setup callbacks using generic OnValueChanged<T>
                for (int i = 0; i < testProps.Count; i++)
                {
                    int index = i;
                    var prop = testProps[i].Property;

                    if (prop is ViewModelInstanceStringProperty sp)
                        sp.OnValueChanged += (string _) =>
                        {
                            var current = testProps[index];
                            testProps[index] = (current.Property, current.NewValue, current.CallbackCount + 1);
                        };
                    else if (prop is ViewModelInstanceNumberProperty np)
                        np.OnValueChanged += (float _) =>
                        {
                            var current = testProps[index];
                            testProps[index] = (current.Property, current.NewValue, current.CallbackCount + 1);
                        };
                    else if (prop is ViewModelInstanceBooleanProperty bp)
                        bp.OnValueChanged += (bool _) =>
                        {
                            var current = testProps[index];
                            testProps[index] = (current.Property, current.NewValue, current.CallbackCount + 1);
                        };
                    else if (prop is ViewModelInstanceColorProperty cp)
                        cp.OnValueChanged += (UnityEngine.Color _) =>
                        {
                            var current = testProps[index];
                            testProps[index] = (current.Property, current.NewValue, current.CallbackCount + 1);
                        };
                    else if (prop is ViewModelInstanceEnumProperty ep)
                        ep.OnValueChanged += (string _) =>
                        {
                            var current = testProps[index];
                            testProps[index] = (current.Property, current.NewValue, current.CallbackCount + 1);
                        };
                }

                // Change property values
                foreach (var prop in testProps)
                {
                    if (prop.Property is ViewModelInstanceStringProperty stringProp)
                        stringProp.Value = (string)prop.NewValue;
                    else if (prop.Property is ViewModelInstanceNumberProperty numberProp)
                        numberProp.Value = (float)prop.NewValue;
                    else if (prop.Property is ViewModelInstanceBooleanProperty boolProp)
                        boolProp.Value = (bool)prop.NewValue;
                    else if (prop.Property is ViewModelInstanceColorProperty colorProp)
                        colorProp.Value = (Color32)prop.NewValue;
                    else if (prop.Property is ViewModelInstanceEnumProperty enumProp)
                        enumProp.Value = (string)prop.NewValue;
                }

                // Process callbacks once
                viewModelInstance.HandleCallbacks();

                // Verify all callbacks were triggered exactly once
                foreach (var prop in testProps)
                {
                    Assert.AreEqual(1, prop.CallbackCount,
                        $"Property callback should have been triggered once for {prop.Property.GetType().Name} in {testAsset.addressableAssetPath}");
                }
            }
        }



        // VIEW MODEL TESTS

        [UnityTest]
        public IEnumerator ViewModel_HasCorrectName()
        {
            foreach (var testAsset in GetTestAssetInfo())
            {
                Asset riveAsset = null;

                yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                    testAsset.addressableAssetPath,
                    (asset) => riveAsset = asset,
                    () => Assert.Fail($"Failed to load asset at {testAsset.addressableAssetPath}")
                );

                File riveFile = LoadAndTrackFile(riveAsset);
                m_widget.Load(riveFile, testAsset.defaultArtboardName, testAsset.defaultStateMachineName);

                yield return new WaitUntil(() => m_widget.Status == WidgetStatus.Loaded);

                var viewModel = m_widget.Artboard.DefaultViewModel;
                Assert.IsNotNull(viewModel, $"DefaultViewModel should exist for asset {testAsset.addressableAssetPath}");

                // Check that the view model has a name
                Assert.IsFalse(string.IsNullOrEmpty(viewModel.Name),
                    $"ViewModel should have a name in {testAsset.addressableAssetPath}");
            }
        }

        [UnityTest]
        public IEnumerator ViewModel_HasCorrectProperties()
        {
            foreach (var testAsset in GetTestAssetInfo())
            {
                Asset riveAsset = null;

                yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                    testAsset.addressableAssetPath,
                    (asset) => riveAsset = asset,
                    () => Assert.Fail($"Failed to load asset at {testAsset.addressableAssetPath}")
                );

                File riveFile = LoadAndTrackFile(riveAsset);
                m_widget.Load(riveFile, testAsset.defaultArtboardName, testAsset.defaultStateMachineName);

                yield return new WaitUntil(() => m_widget.Status == WidgetStatus.Loaded);

                var viewModel = m_widget.Artboard.DefaultViewModel;
                Assert.IsNotNull(viewModel, $"DefaultViewModel should exist for asset {testAsset.addressableAssetPath}");

                // Check that the view model has properties
                Assert.IsTrue(viewModel.Properties.Count > 0,
                    $"ViewModel should have properties in {testAsset.addressableAssetPath}");

                // Verify expected properties

                foreach (var vmodelinfo in testAsset.expectedViewModelsInFile)
                {
                    ViewModel currentViewModel = m_widget.File.GetViewModelByName(vmodelinfo.Name);
                    Assert.IsNotNull(currentViewModel, $"ViewModel '{vmodelinfo.Name}' should exist in {testAsset.addressableAssetPath}");

                    foreach (var expectedProperty in vmodelinfo.Properties)
                    {
                        var property = currentViewModel.Properties.FirstOrDefault(p => p.Name == expectedProperty.Name);

                        Assert.IsNotNull(property, $"Property '{expectedProperty.Name}' should exist in ViewModel '{viewModel.Name}' in {testAsset.addressableAssetPath}");
                        Assert.AreEqual(expectedProperty.Type, property.Type,
                            $"Property '{expectedProperty.Name}' should be of type '{expectedProperty.Type}' in ViewModel '{viewModel.Name}' in {testAsset.addressableAssetPath}");
                    }

                }

            }
        }

        [UnityTest]
        public IEnumerator ViewModel_CanCreateInstances()
        {
            foreach (var testAsset in GetTestAssetInfo())
            {
                Asset riveAsset = null;

                yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                    testAsset.addressableAssetPath,
                    (asset) => riveAsset = asset,
                    () => Assert.Fail($"Failed to load asset at {testAsset.addressableAssetPath}")
                );

                File riveFile = LoadAndTrackFile(riveAsset);
                m_widget.Load(riveFile, testAsset.defaultArtboardName, testAsset.defaultStateMachineName);

                yield return new WaitUntil(() => m_widget.Status == WidgetStatus.Loaded);

                var viewModel = m_widget.Artboard.DefaultViewModel;
                Assert.IsNotNull(viewModel, $"DefaultViewModel should exist for asset {testAsset.addressableAssetPath}");

                var instance1 = viewModel.CreateInstance();
                Assert.IsNotNull(instance1, $"CreateInstance should return a valid instance in {testAsset.addressableAssetPath}");

                var instance1ForCreateDefaultInstance = viewModel.CreateDefaultInstance();
                Assert.IsNotNull(instance1ForCreateDefaultInstance, $"CreateDefaultInstance should return a valid instance in {testAsset.addressableAssetPath}");

                // Just to be sure, let's create a third instance with CreateInstance() again, it should NOT be the same as the first one
                // This is a bit redundant, but it helps to ensure that the CreateInstance() method is not returning the same instance every time
                var instance3 = viewModel.CreateInstance();

                Assert.IsNotNull(instance3, $"CreateInstance should return a valid instance in {testAsset.addressableAssetPath}");

                // And lets do the same with CreateDefaultInstance...
                var instance2ForCreateDefaultInstance = viewModel.CreateDefaultInstance();

                Assert.IsNotNull(instance2ForCreateDefaultInstance, $"CreateDefaultInstance should return a valid instance in {testAsset.addressableAssetPath}");

                // Verify instances are different objects
                Assert.AreNotSame(instance1, instance1ForCreateDefaultInstance,
                    $"Different instance creation methods should return different objects in {testAsset.addressableAssetPath}");

                Assert.AreNotSame(instance1, instance3,
                    $"Calling CreateInstance() twice should return different objects in {testAsset.addressableAssetPath}");

                Assert.AreNotSame(instance1ForCreateDefaultInstance, instance2ForCreateDefaultInstance,
                    $"Calling CreateDefaultInstance() twice should return different objects in {testAsset.addressableAssetPath}");

            }
        }



        [UnityTest]
        public IEnumerator ViewModel_InstanceNamesAndCount_AreCorrect()
        {
            foreach (var testAsset in GetTestAssetInfo())
            {
                Asset riveAsset = null;

                yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                    testAsset.addressableAssetPath,
                    (asset) => riveAsset = asset,
                    () => Assert.Fail($"Failed to load asset at {testAsset.addressableAssetPath}")
                );

                File riveFile = LoadAndTrackFile(riveAsset);
                m_widget.Load(riveFile, testAsset.defaultArtboardName, testAsset.defaultStateMachineName);

                yield return new WaitUntil(() => m_widget.Status == WidgetStatus.Loaded);

                // Loop through each expected view model
                foreach (var vmodelinfo in testAsset.expectedViewModelsInFile)
                {
                    var viewModel = m_widget.File.GetViewModelByName(vmodelinfo.Name);
                    Assert.IsNotNull(viewModel, $"ViewModel '{vmodelinfo.Name}' should exist in {testAsset.addressableAssetPath}");

                    // Check that the instance count is correct
                    Assert.AreEqual(vmodelinfo.InstanceNames.Length, viewModel.InstanceCount,
                        $"ViewModel '{vmodelinfo.Name}' should have {vmodelinfo.InstanceNames.Length} instances in {testAsset.addressableAssetPath}");

                    // Check that the instance names are correct
                    Assert.AreEqual(vmodelinfo.InstanceNames.Length, viewModel.InstanceNames.Count,
                        $"ViewModel '{vmodelinfo.Name}' should have {vmodelinfo.InstanceNames.Length} instance names in {testAsset.addressableAssetPath}");

                    for (int i = 0; i < vmodelinfo.InstanceNames.Length; i++)
                    {
                        Assert.AreEqual(vmodelinfo.InstanceNames[i], viewModel.InstanceNames[i],
                            $"Instance name at index {i} should be '{vmodelinfo.InstanceNames[i]}' in ViewModel '{vmodelinfo.Name}' in {testAsset.addressableAssetPath}");
                    }
                }



            }
        }

        [UnityTest]
        public IEnumerator ViewModel_NewInstance_HasExpectedDefaultValues()
        {
            foreach (var testAsset in GetTestAssetInfo())
            {
                Asset riveAsset = null;
                yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                    testAsset.addressableAssetPath,
                    (asset) => riveAsset = asset,
                    () => Assert.Fail($"Failed to load asset at {testAsset.addressableAssetPath}")
                );

                File riveFile = LoadAndTrackFile(riveAsset);
                m_widget.Load(riveFile, testAsset.defaultArtboardName, testAsset.defaultStateMachineName);
                yield return new WaitUntil(() => m_widget.Status == WidgetStatus.Loaded);


                IReadOnlyList<ViewModel> viewModels = riveFile.ViewModels;
                Assert.IsTrue(viewModels.Count > 0,
                    $"File should have at least one view model to test default values in {testAsset.addressableAssetPath}");

                foreach (var viewModel in viewModels)
                {
                    // Create a fresh instance that should have default values
                    var viewModelInstance = viewModel.CreateInstance();
                    Assert.IsNotNull(viewModelInstance,
                        $"Should be able to create a new instance of '{viewModel.Name}'");

                    foreach (var property in viewModel.Properties)
                    {
                        switch (property.Type)
                        {
                            case ViewModelDataType.Number:
                                var numberProp = viewModelInstance.GetProperty<ViewModelInstanceNumberProperty>(property.Name);
                                if (numberProp != null)
                                {
                                    Assert.AreEqual(0f, numberProp.Value,
                                        $"Number property '{property.Name}' should default to 0");
                                }
                                break;

                            case ViewModelDataType.String:
                                var stringProp = viewModelInstance.GetProperty<ViewModelInstanceStringProperty>(property.Name);
                                if (stringProp != null)
                                {
                                    Assert.AreEqual(String.Empty, stringProp.Value,
                                        $"String property '{property.Name}' should default to empty string");
                                }
                                break;

                            case ViewModelDataType.Boolean:
                                var boolProp = viewModelInstance.GetProperty<ViewModelInstanceBooleanProperty>(property.Name);
                                if (boolProp != null)
                                {
                                    Assert.IsFalse(boolProp.Value,
                                        $"Boolean property '{property.Name}' should default to false");
                                }
                                break;

                            case ViewModelDataType.Color:
                                var colorProp = viewModelInstance.GetProperty<ViewModelInstanceColorProperty>(property.Name);
                                if (colorProp != null)
                                {
                                    // Default should be #000000FF (black with full alpha)
                                    UnityEngine.Color expectedColor = new UnityEngine.Color(0, 0, 0, 1);
                                    Assert.AreEqual(expectedColor.r, colorProp.Value.r, 0.01f,
                                        $"Color property '{property.Name}' red component should default to 0");
                                    Assert.AreEqual(expectedColor.g, colorProp.Value.g, 0.01f,
                                        $"Color property '{property.Name}' green component should default to 0");
                                    Assert.AreEqual(expectedColor.b, colorProp.Value.b, 0.01f,
                                        $"Color property '{property.Name}' blue component should default to 0");
                                    Assert.AreEqual(expectedColor.a, colorProp.Value.a, 0.01f,
                                        $"Color property '{property.Name}' alpha component should default to 1");

                                    Assert.AreEqual(0, colorProp.Value32.r,
                                        $"Color32 property '{property.Name}' red component should default to 0");
                                    Assert.AreEqual(0, colorProp.Value32.g,
                                        $"Color32 property '{property.Name}' green component should default to 0");
                                    Assert.AreEqual(0, colorProp.Value32.b,
                                        $"Color32 property '{property.Name}' blue component should default to 0");
                                    Assert.AreEqual(255, colorProp.Value32.a,
                                        $"Color32 property '{property.Name}' alpha component should default to 255");
                                }
                                break;

                            case ViewModelDataType.Enum:
                                var enumProp = viewModelInstance.GetProperty<ViewModelInstanceEnumProperty>(property.Name);
                                if (enumProp != null && enumProp.EnumValues.Count > 0)
                                {
                                    // Default should be the first value
                                    string firstEnumValue = enumProp.EnumValues[0];
                                    Assert.AreEqual(firstEnumValue, enumProp.Value,
                                        $"Enum property '{property.Name}' should default to the first value '{firstEnumValue}'");
                                    Assert.AreEqual(0, enumProp.ValueIndex,
                                        $"Enum property '{property.Name}' index should default to 0");
                                }
                                break;

                            case ViewModelDataType.Trigger:
                                var triggerProp = viewModelInstance.GetProperty<ViewModelInstanceTriggerProperty>(property.Name);
                                // We can only verify the trigger property exists, not its state
                                Assert.IsNotNull(triggerProp,
                                    $"Trigger property '{property.Name}' should exist");
                                break;

                            case ViewModelDataType.ViewModel:
                                // For fresh instances, nested view models should not be null
                                var nestedVMProp = viewModelInstance.GetProperty<ViewModelInstance>(property.Name);
                                Assert.IsNotNull(nestedVMProp,
                                    $"Nested view model property '{property.Name}' should not be null");
                                break;
                        }
                    }


                }
            }
        }

        [UnityTest]
        public IEnumerator DataBindingMode_Manual_DoesNotAutoBind()
        {
            foreach (var testAsset in GetTestAssetInfo())
            {
                Asset riveAsset = null;

                yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                    testAsset.addressableAssetPath,
                    (asset) => riveAsset = asset,
                    () => Assert.Fail($"Failed to load asset at {testAsset.addressableAssetPath}")
                );

                File riveFile = LoadAndTrackFile(riveAsset);

                // Set data binding mode to Manual
                m_widget.BindingMode = Components.RiveWidget.DataBindingMode.Manual;

                m_widget.Load(riveFile, testAsset.defaultArtboardName, testAsset.defaultStateMachineName);

                yield return new WaitUntil(() => m_widget.Status == WidgetStatus.Loaded);

                // In Manual mode, the ViewModelInstance should not be automatically bound
                Assert.IsNull(m_widget.StateMachine.ViewModelInstance,
                    $"ViewModelInstance should be null in Manual mode for asset {testAsset.addressableAssetPath}");
            }
        }

        [UnityTest]
        public IEnumerator DataBindingMode_AutoBindDefault_BindsDefaultInstance()
        {
            foreach (var testAsset in GetTestAssetInfo())
            {
                Asset riveAsset = null;

                yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                    testAsset.addressableAssetPath,
                    (asset) => riveAsset = asset,
                    () => Assert.Fail($"Failed to load asset at {testAsset.addressableAssetPath}")
                );

                File riveFile = LoadAndTrackFile(riveAsset);

                // Set data binding mode to AutoBindDefault
                m_widget.BindingMode = Components.RiveWidget.DataBindingMode.AutoBindDefault;

                m_widget.Load(riveFile, testAsset.defaultArtboardName, testAsset.defaultStateMachineName);

                // Wait for loading to complete
                yield return new WaitUntil(() => m_widget.Status == WidgetStatus.Loaded);

                // In AutoBindDefault mode, the ViewModelInstance should be automatically bound
                Assert.IsNotNull(m_widget.StateMachine.ViewModelInstance,
                    $"ViewModelInstance should be automatically bound in AutoBindDefault mode for asset {testAsset.addressableAssetPath}");
            }
        }

        [UnityTest]
        public IEnumerator DataBindingMode_AutoBindSelected_BindsNamedInstance()
        {
            foreach (var testAsset in GetTestAssetInfo())
            {
                Asset riveAsset = null;

                yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                    testAsset.addressableAssetPath,
                    (asset) => riveAsset = asset,
                    () => Assert.Fail($"Failed to load asset at {testAsset.addressableAssetPath}")
                );

                File riveFile = LoadAndTrackFile(riveAsset);

                // Load the file first to get the view model
                m_widget.Load(riveFile, testAsset.defaultArtboardName, testAsset.defaultStateMachineName);
                yield return new WaitUntil(() => m_widget.Status == WidgetStatus.Loaded);

                string instanceName = m_widget.Artboard.DefaultViewModel.InstanceNames[0];


                // Set data binding mode to AutoBindSelected with the instance name
                m_widget.BindingMode = Components.RiveWidget.DataBindingMode.AutoBindSelected;
                m_widget.ViewModelInstanceName = instanceName;

                m_widget.Load(riveFile, testAsset.defaultArtboardName, testAsset.defaultStateMachineName);

                yield return new WaitUntil(() => m_widget.Status == WidgetStatus.Loaded);

                // In AutoBindSelected mode, the ViewModelInstance should be automatically bound
                Assert.IsNotNull(m_widget.StateMachine.ViewModelInstance,
                    $"ViewModelInstance should be automatically bound in AutoBindSelected mode for asset {testAsset.addressableAssetPath}");
            }
        }

        [UnityTest]
        public IEnumerator DataBindingMode_AutoBindSelected_WithInvalidName_LogsError()
        {
            foreach (var testAsset in GetTestAssetInfo())
            {
                Asset riveAsset = null;

                yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                    testAsset.addressableAssetPath,
                    (asset) => riveAsset = asset,
                    () => Assert.Fail($"Failed to load asset at {testAsset.addressableAssetPath}")
                );

                File riveFile = LoadAndTrackFile(riveAsset);

                mockLogger.Clear();

                // Set data binding mode to AutoBindSelected with an invalid name
                m_widget.BindingMode = Components.RiveWidget.DataBindingMode.AutoBindSelected;
                m_widget.ViewModelInstanceName = "NonExistentInstance";

                m_widget.Load(riveFile, testAsset.defaultArtboardName, testAsset.defaultStateMachineName);

                yield return new WaitUntil(() => m_widget.Status == WidgetStatus.Loaded);

                Assert.IsTrue(mockLogger.LoggedErrors.Count > 0,
                    $"Should log an error when using an invalid instance name in AutoBindSelected mode for asset {testAsset.addressableAssetPath}");
            }
        }

        [UnityTest]
        public IEnumerator ConvenienceMethods_ReturnSameResultAsGenericMethods()
        {
            foreach (var testAsset in GetTestAssetInfo())
            {
                Asset riveAsset = null;

                yield return testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                    testAsset.addressableAssetPath,
                    (asset) => riveAsset = asset,
                    () => Assert.Fail($"Failed to load asset at {testAsset.addressableAssetPath}")
                );

                File riveFile = LoadAndTrackFile(riveAsset);
                m_widget.Load(riveFile, testAsset.defaultArtboardName, testAsset.defaultStateMachineName);
                yield return new WaitUntil(() => m_widget.Status == WidgetStatus.Loaded);

                var viewModelInstance = m_widget.StateMachine.ViewModelInstance;
                Assert.IsNotNull(viewModelInstance, $"ViewModelInstance should exist for asset {testAsset.addressableAssetPath}");

                // Test number property convenience method
                var numberPropInfo = GetPropertyInfoOfType(testAsset, ViewModelDataType.Number).FirstOrDefault();
                if (numberPropInfo != null)
                {
                    var genericNumberProp = viewModelInstance.GetProperty<ViewModelInstanceNumberProperty>(numberPropInfo.Name);
                    var convenienceNumberProp = viewModelInstance.GetNumberProperty(numberPropInfo.Name);

                    Assert.IsNotNull(genericNumberProp, $"Number property '{numberPropInfo.Name}' should exist via generic method");
                    Assert.IsNotNull(convenienceNumberProp, $"Number property '{numberPropInfo.Name}' should exist via convenience method");
                    Assert.AreSame(genericNumberProp, convenienceNumberProp, $"Both methods should return the same instance for '{numberPropInfo.Name}'");
                }

                // Test boolean property convenience method
                var boolPropInfo = GetPropertyInfoOfType(testAsset, ViewModelDataType.Boolean).FirstOrDefault();
                if (boolPropInfo != null)
                {
                    var genericBoolProp = viewModelInstance.GetProperty<ViewModelInstanceBooleanProperty>(boolPropInfo.Name);
                    var convenienceBoolProp = viewModelInstance.GetBooleanProperty(boolPropInfo.Name);

                    Assert.IsNotNull(genericBoolProp, $"Boolean property '{boolPropInfo.Name}' should exist via generic method");
                    Assert.IsNotNull(convenienceBoolProp, $"Boolean property '{boolPropInfo.Name}' should exist via convenience method");
                    Assert.AreSame(genericBoolProp, convenienceBoolProp, $"Both methods should return the same instance for '{boolPropInfo.Name}'");
                }

                // Test string property convenience method
                var stringPropInfo = GetPropertyInfoOfType(testAsset, ViewModelDataType.String).FirstOrDefault();
                if (stringPropInfo != null)
                {
                    var genericStringProp = viewModelInstance.GetProperty<ViewModelInstanceStringProperty>(stringPropInfo.Name);
                    var convenienceStringProp = viewModelInstance.GetStringProperty(stringPropInfo.Name);

                    Assert.IsNotNull(genericStringProp, $"String property '{stringPropInfo.Name}' should exist via generic method");
                    Assert.IsNotNull(convenienceStringProp, $"String property '{stringPropInfo.Name}' should exist via convenience method");
                    Assert.AreSame(genericStringProp, convenienceStringProp, $"Both methods should return the same instance for '{stringPropInfo.Name}'");
                }

                // Test color property convenience method
                var colorPropInfo = GetPropertyInfoOfType(testAsset, ViewModelDataType.Color).FirstOrDefault();
                if (colorPropInfo != null)
                {
                    var genericColorProp = viewModelInstance.GetProperty<ViewModelInstanceColorProperty>(colorPropInfo.Name);
                    var convenienceColorProp = viewModelInstance.GetColorProperty(colorPropInfo.Name);

                    Assert.IsNotNull(genericColorProp, $"Color property '{colorPropInfo.Name}' should exist via generic method");
                    Assert.IsNotNull(convenienceColorProp, $"Color property '{colorPropInfo.Name}' should exist via convenience method");
                    Assert.AreSame(genericColorProp, convenienceColorProp, $"Both methods should return the same instance for '{colorPropInfo.Name}'");
                }

                // Test enum property convenience method
                var enumPropInfo = GetPropertyInfoOfType(testAsset, ViewModelDataType.Enum).FirstOrDefault();
                if (enumPropInfo != null)
                {
                    var genericEnumProp = viewModelInstance.GetProperty<ViewModelInstanceEnumProperty>(enumPropInfo.Name);
                    var convenienceEnumProp = viewModelInstance.GetEnumProperty(enumPropInfo.Name);

                    Assert.IsNotNull(genericEnumProp, $"Enum property '{enumPropInfo.Name}' should exist via generic method");
                    Assert.IsNotNull(convenienceEnumProp, $"Enum property '{enumPropInfo.Name}' should exist via convenience method");
                    Assert.AreSame(genericEnumProp, convenienceEnumProp, $"Both methods should return the same instance for '{enumPropInfo.Name}'");
                }

                // Test trigger property convenience method
                var triggerPropInfo = GetPropertyInfoOfType(testAsset, ViewModelDataType.Trigger).FirstOrDefault();
                if (triggerPropInfo != null)
                {
                    var genericTriggerProp = viewModelInstance.GetProperty<ViewModelInstanceTriggerProperty>(triggerPropInfo.Name);
                    var convenienceTriggerProp = viewModelInstance.GetTriggerProperty(triggerPropInfo.Name);

                    Assert.IsNotNull(genericTriggerProp, $"Trigger property '{triggerPropInfo.Name}' should exist via generic method");
                    Assert.IsNotNull(convenienceTriggerProp, $"Trigger property '{triggerPropInfo.Name}' should exist via convenience method");
                    Assert.AreSame(genericTriggerProp, convenienceTriggerProp, $"Both methods should return the same instance for '{triggerPropInfo.Name}'");
                }

                // Test view model property convenience method
                var viewModelPropInfo = GetPropertyInfoOfType(testAsset, ViewModelDataType.ViewModel).FirstOrDefault();
                if (viewModelPropInfo != null)
                {
                    var genericViewModelProp = viewModelInstance.GetProperty<ViewModelInstance>(viewModelPropInfo.Name);
                    var convenienceViewModelProp = viewModelInstance.GetViewModelInstanceProperty(viewModelPropInfo.Name);

                    Assert.IsNotNull(genericViewModelProp, $"ViewModel property '{viewModelPropInfo.Name}' should exist via generic method");
                    Assert.IsNotNull(convenienceViewModelProp, $"ViewModel property '{viewModelPropInfo.Name}' should exist via convenience method");
                    Assert.AreSame(genericViewModelProp, convenienceViewModelProp, $"Both methods should return the same instance for '{viewModelPropInfo.Name}'");
                }
            }
        }
    }
}