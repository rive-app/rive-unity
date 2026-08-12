using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Rive.Tests.Utils;
using Rive.Utils;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rive.Tests
{
    /// <summary>
    /// Covers global view models: discovering them on the <see cref="File"/>, binding them through
    /// <see cref="StateMachine.BindViewModelInstance(ViewModelInstance, IReadOnlyDictionary{string, ViewModelInstance})"/>,
    /// and the error paths around bad names and disposed objects.
    /// </summary>
    /// <remarks>
    /// The backing asset (global_variables_test.riv) has three global view models: Sizes, Colors, and
    /// Labels, plus three non-global ones (Main, Child, IntermediateList) that shouldn't show up as
    /// globals.
    /// </remarks>
    public class GlobalViewModelTests
    {
        private const string SizesViewModel = "Sizes";
        private const string ColorsViewModel = "Colors";
        private const string LabelsViewModel = "Labels";

        private const string MainArtboard = "Main";

        // The names, in the order the file lists them.
        private static readonly string[] ExpectedGlobalNames =
        {
            SizesViewModel,
            ColorsViewModel,
            LabelsViewModel
        };

        private TestAssetLoadingManager m_testAssetLoadingManager;
        private MockLogger m_mockLogger;
        private List<File> m_loadedFiles;
        private List<StateMachine> m_stateMachines;

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            m_testAssetLoadingManager = new TestAssetLoadingManager();
        }

        [SetUp]
        public void Setup()
        {
            m_mockLogger = new MockLogger();
            m_loadedFiles = new List<File>();
            m_stateMachines = new List<StateMachine>();
            DebugLogger.Instance = m_mockLogger;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var stateMachine in m_stateMachines)
            {
                if (stateMachine != null && !stateMachine.IsDisposed)
                {
                    stateMachine.Dispose();
                }
            }
            m_stateMachines.Clear();

            foreach (var file in m_loadedFiles)
            {
                if (file != null && !file.IsDisposed)
                {
                    file.Dispose();
                }
            }
            m_loadedFiles.Clear();

            m_testAssetLoadingManager.UnloadAllAssets();
        }

        private IEnumerator LoadTestFile(Action<File> onLoaded)
        {
            Asset riveAsset = null;
            yield return m_testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_global_variables_test,
                (asset) => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {TestAssetReferences.riv_global_variables_test}")
            );

            File file = File.Load(riveAsset);
            Assert.IsNotNull(file, "Failed to load the Rive file.");
            m_loadedFiles.Add(file);

            onLoaded(file);
        }

        /// <summary>
        /// Loads the file and hands the test a freshly instanced state machine off the Main artboard.
        /// Nothing is bound yet, so globals start out empty.
        /// </summary>
        private IEnumerator LoadStateMachine(Action<File, Artboard, StateMachine> onLoaded)
        {
            yield return LoadTestFile((file) =>
            {
                Artboard artboard = file.Artboard(MainArtboard);
                Assert.IsNotNull(artboard, $"Expected an artboard named '{MainArtboard}'.");

                StateMachine stateMachine = artboard.StateMachine();
                Assert.IsNotNull(stateMachine, "Expected a default state machine on the Main artboard.");
                m_stateMachines.Add(stateMachine);

                onLoaded(file, artboard, stateMachine);
            });
        }

        private ViewModelInstance CreateMainInstance(Artboard artboard)
        {
            ViewModel viewModel = artboard.DefaultViewModel;
            Assert.IsNotNull(viewModel, $"Expected artboard '{artboard.Name}' to have a default view model.");

            ViewModelInstance instance = viewModel.CreateDefaultInstance();
            Assert.IsNotNull(instance, "Failed to create the main view model instance.");
            return instance;
        }

        private ViewModelInstance CreateGlobalInstance(File file, string viewModelName, string instanceName = null)
        {
            ViewModel viewModel = file.GetViewModelByName(viewModelName);
            Assert.IsNotNull(viewModel, $"Expected a view model named '{viewModelName}'.");

            ViewModelInstance instance = instanceName == null
                ? viewModel.CreateDefaultInstance()
                : viewModel.CreateInstanceByName(instanceName);
            Assert.IsNotNull(instance, $"Failed to create instance '{instanceName ?? "(default)"}' of '{viewModelName}'.");
            return instance;
        }

        private static void AssertColorEquals(Color32 expected, Color32 actual, string message)
        {
            Assert.AreEqual(
                $"({expected.r}, {expected.g}, {expected.b}, {expected.a})",
                $"({actual.r}, {actual.g}, {actual.b}, {actual.a})",
                message);
        }

        [UnityTest]
        public IEnumerator GlobalViewModelNames_ReturnsGlobalsInFileOrder()
        {
            yield return LoadTestFile((file) =>
            {
                CollectionAssert.AreEqual(ExpectedGlobalNames, file.GlobalViewModelNames,
                    "Global view model names should match the file's globals, in file order.");
            });
        }

        [UnityTest]
        public IEnumerator GlobalViewModelNames_ExcludeNonGlobalViewModels()
        {
            yield return LoadTestFile((file) =>
            {
                foreach (string nonGlobal in new[] { "Main", "Child", "IntermediateList" })
                {
                    Assert.IsNotNull(file.GetViewModelByName(nonGlobal),
                        $"Expected the file to contain a view model named '{nonGlobal}'.");
                    CollectionAssert.DoesNotContain(file.GlobalViewModelNames, nonGlobal,
                        $"'{nonGlobal}' is not a global view model and should not be listed as one.");
                }
            });
        }

        [UnityTest]
        public IEnumerator GetGlobalViewModelInstance_BeforeBind_ReturnsNull()
        {
            yield return LoadStateMachine((file, artboard, stateMachine) =>
            {
                foreach (string name in ExpectedGlobalNames)
                {
                    Assert.IsNull(stateMachine.GetGlobalViewModelInstance(name),
                        $"Global '{name}' should have no instance until something binds.");
                }
            });
        }

        [UnityTest]
        public IEnumerator GetGlobalViewModelInstance_UnknownOrEmptyName_ReturnsNull()
        {
            yield return LoadStateMachine((file, artboard, stateMachine) =>
            {
                stateMachine.BindViewModelInstance(CreateMainInstance(artboard));

                Assert.IsNull(stateMachine.GetGlobalViewModelInstance("NotAGlobal"));
                Assert.IsNull(stateMachine.GetGlobalViewModelInstance(string.Empty));
                Assert.IsNull(stateMachine.GetGlobalViewModelInstance(null));
            });
        }

        [UnityTest]
        public IEnumerator BindViewModelInstance_WithGlobals_BindsEveryNamedGlobal()
        {
            yield return LoadStateMachine((file, artboard, stateMachine) =>
            {
                var globals = new Dictionary<string, ViewModelInstance>();
                foreach (string name in ExpectedGlobalNames)
                {
                    globals[name] = CreateGlobalInstance(file, name);
                }

                ViewModelInstance main = CreateMainInstance(artboard);
                m_mockLogger.Clear();

                bool bound = stateMachine.BindViewModelInstance(main, globals);

                Assert.IsTrue(bound, "Binding every global by its file name should succeed.");
                CollectionAssert.IsEmpty(m_mockLogger.LoggedErrors);

                foreach (string name in ExpectedGlobalNames)
                {
                    Assert.AreSame(globals[name], stateMachine.GetGlobalViewModelInstance(name),
                        $"Global '{name}' should resolve to the instance that was bound.");
                }
            });
        }

        [UnityTest]
        public IEnumerator BindViewModelInstance_WithGlobals_BindsMainInstance()
        {
            yield return LoadStateMachine((file, artboard, stateMachine) =>
            {
                ViewModelInstance main = CreateMainInstance(artboard);

                Assert.IsTrue(stateMachine.BindViewModelInstance(
                    main,
                    new Dictionary<string, ViewModelInstance>
                    {
                        { LabelsViewModel, CreateGlobalInstance(file, LabelsViewModel) }
                    }));

                Assert.AreSame(main, stateMachine.ViewModelInstance,
                    "Binding with globals should still bind the main instance.");
            });
        }

        [UnityTest]
        public IEnumerator BindViewModelInstance_OmittedGlobal_GetsDefaultInstance()
        {
            yield return LoadStateMachine((file, artboard, stateMachine) =>
            {
                // Only Labels is supplied. Sizes and Colors are filled in by the runtime with their
                // defaults rather than being left empty.
                Assert.IsTrue(stateMachine.BindViewModelInstance(
                    CreateMainInstance(artboard),
                    new Dictionary<string, ViewModelInstance>
                    {
                        { LabelsViewModel, CreateGlobalInstance(file, LabelsViewModel, "UK") }
                    }));

                ViewModelInstance sizes = stateMachine.GetGlobalViewModelInstance(SizesViewModel);
                Assert.IsNotNull(sizes, "An omitted global should still be bound to a default instance.");
                Assert.AreEqual(16f, sizes.GetProperty<ViewModelInstanceNumberProperty>("gaps").Value,
                    "The omitted Sizes global should hold its default 'gaps' value.");

                ViewModelInstance colors = stateMachine.GetGlobalViewModelInstance(ColorsViewModel);
                Assert.IsNotNull(colors, "An omitted global should still be bound to a default instance.");
                AssertColorEquals(
                    new Color32(223, 223, 223, 255),
                    colors.GetProperty<ViewModelInstanceColorProperty>("backgroundColor").Value32,
                    "The omitted Colors global should hold its default 'backgroundColor' value.");
            });
        }

        [UnityTest]
        public IEnumerator BindViewModelInstance_MainOnlyOverload_StillCreatesGlobals()
        {
            yield return LoadStateMachine((file, artboard, stateMachine) =>
            {
                stateMachine.BindViewModelInstance(CreateMainInstance(artboard));

                foreach (string name in ExpectedGlobalNames)
                {
                    Assert.IsNotNull(stateMachine.GetGlobalViewModelInstance(name),
                        $"Binding only a main instance should still give '{name}' a default instance.");
                }
            });
        }

        [UnityTest]
        public IEnumerator GetGlobalViewModelInstance_ReturnsSameWrapperAcrossCalls()
        {
            yield return LoadStateMachine((file, artboard, stateMachine) =>
            {
                // No instance was provided for this global, so the wrapper uses whatever the native code returns.
                // Each call to native code returns a new runtime handle, but multiple reads at the C# level should still return the same wrapper for the same underlying core instance.

                stateMachine.BindViewModelInstance(CreateMainInstance(artboard));

                ViewModelInstance first = stateMachine.GetGlobalViewModelInstance(ColorsViewModel);
                ViewModelInstance second = stateMachine.GetGlobalViewModelInstance(ColorsViewModel);

                Assert.IsNotNull(first);
                Assert.AreSame(first, second);
            });
        }

        [UnityTest]
        public IEnumerator BindViewModelInstance_Rebind_ReplacesGlobalInstance()
        {
            yield return LoadStateMachine((file, artboard, stateMachine) =>
            {
                ViewModelInstance main = CreateMainInstance(artboard);
                ViewModelInstance uk = CreateGlobalInstance(file, LabelsViewModel, "UK");
                ViewModelInstance us = CreateGlobalInstance(file, LabelsViewModel, "US");

                Assert.IsTrue(stateMachine.BindViewModelInstance(
                    main,
                    new Dictionary<string, ViewModelInstance> { { LabelsViewModel, uk } }));
                Assert.AreSame(uk, stateMachine.GetGlobalViewModelInstance(LabelsViewModel));

                Assert.IsTrue(stateMachine.BindViewModelInstance(
                    main,
                    new Dictionary<string, ViewModelInstance> { { LabelsViewModel, us } }));
                Assert.AreSame(us, stateMachine.GetGlobalViewModelInstance(LabelsViewModel),
                    "Rebinding a global should replace the previously bound instance.");
            });
        }

        [UnityTest]
        public IEnumerator BindViewModelInstance_NamedInstance_CarriesItsOwnValues()
        {
            yield return LoadStateMachine((file, artboard, stateMachine) =>
            {
                Assert.IsTrue(stateMachine.BindViewModelInstance(
                    CreateMainInstance(artboard),
                    new Dictionary<string, ViewModelInstance>
                    {
                        { LabelsViewModel, CreateGlobalInstance(file, LabelsViewModel, "US") }
                    }));

                ViewModelInstance labels = stateMachine.GetGlobalViewModelInstance(LabelsViewModel);
                Assert.AreEqual("$", labels.GetProperty<ViewModelInstanceStringProperty>("currency").Value,
                    "The bound global should carry the named instance's value, not the default one.");
            });
        }

        [UnityTest]
        public IEnumerator BindViewModelInstance_SameGlobalOnTwoStateMachines_IsShared()
        {
            yield return LoadTestFile((file) =>
            {
                // A global bound into two artboards' state machines is one instance, so an edit made
                // through one is visible through the other.
                ViewModelInstance shared = CreateGlobalInstance(file, LabelsViewModel);
                var stateMachines = new List<StateMachine>();

                foreach (string artboardName in new[] { "Main", "Child" })
                {
                    Artboard artboard = file.Artboard(artboardName);
                    Assert.IsNotNull(artboard, $"Expected an artboard named '{artboardName}'.");

                    StateMachine stateMachine = artboard.StateMachine();
                    Assert.IsNotNull(stateMachine);
                    m_stateMachines.Add(stateMachine);
                    stateMachines.Add(stateMachine);

                    Assert.IsTrue(stateMachine.BindViewModelInstance(
                        artboard.DefaultViewModel.CreateDefaultInstance(),
                        new Dictionary<string, ViewModelInstance> { { LabelsViewModel, shared } }));

                    Assert.AreSame(shared, stateMachine.GetGlobalViewModelInstance(LabelsViewModel),
                        $"'{artboardName}' should be bound to the shared Labels instance.");
                }

                stateMachines[0].GetGlobalViewModelInstance(LabelsViewModel)
                    .GetProperty<ViewModelInstanceStringProperty>("currency").Value = "USD";

                Assert.AreEqual("USD",
                    stateMachines[1].GetGlobalViewModelInstance(LabelsViewModel)
                        .GetProperty<ViewModelInstanceStringProperty>("currency").Value,
                    "Editing a shared global through one state machine should be visible through the other.");
            });
        }

        /// <summary>
        /// This test checks that "global" variables are local to each state machine/artboard by default.
        /// Just because two state machines/artboards use a global with the same name, does not mean they both
        /// reference the same instance. Unless you explicitly bind the same instance, each state machine/artboard
        /// creates its own independent global, so changes are not shared.
        /// </summary>
        [UnityTest]
        public IEnumerator BindViewModelInstance_DefaultGlobals_AreNotSharedAcrossStateMachines()
        {
            yield return LoadTestFile((file) =>
            {
                // File.Artboard instances a new artboard on every call, so this is two separate
                // artboards of the same name, similar to two widgets showing the same thing.
                // We do not explicitly bind a global by name, so each state machine creates its own default global instance.
                var stateMachines = new List<StateMachine>();

                for (int i = 0; i < 2; i++)
                {
                    Artboard artboard = file.Artboard(MainArtboard);
                    Assert.IsNotNull(artboard, $"Expected an artboard named '{MainArtboard}'.");

                    StateMachine stateMachine = artboard.StateMachine();
                    Assert.IsNotNull(stateMachine);
                    m_stateMachines.Add(stateMachine);
                    stateMachines.Add(stateMachine);

                    stateMachine.BindViewModelInstance(CreateMainInstance(artboard));
                }

                ViewModelInstance first = stateMachines[0].GetGlobalViewModelInstance(LabelsViewModel);
                ViewModelInstance second = stateMachines[1].GetGlobalViewModelInstance(LabelsViewModel);

                Assert.IsNotNull(first);
                Assert.IsNotNull(second);
                Assert.AreNotSame(first, second,
                    "Each state machine should create its own default global instance instead of sharing one.");

                var firstCurrency = first.GetProperty<ViewModelInstanceStringProperty>("currency");
                var secondCurrency = second.GetProperty<ViewModelInstanceStringProperty>("currency");

                // Both defaults should have identical values to begin with.
                Assert.AreEqual(secondCurrency.Value, firstCurrency.Value,
                    "Two default global instances of the same type should initially have the same value.");

                string secondCurrencyValueSnapshot = secondCurrency.Value;
                firstCurrency.Value = "USD";

                // Changing the value on one state machine should not affect the other.
                Assert.AreEqual(secondCurrencyValueSnapshot, secondCurrency.Value,
                    "Changing a default global in one state machine should not impact the other's default.");

                Assert.AreNotEqual(
                    firstCurrency.Value, secondCurrency.Value,
                    "Changing a default global in one state machine should not affect the other's.");
            });
        }

        [UnityTest]
        public IEnumerator BindViewModelInstance_NullMain_BindsGlobalsAndBackfillsMain()
        {
            yield return LoadStateMachine((file, artboard, stateMachine) =>
            {
                // This artboard has a default view model, so bind() fabricates a main when none is
                // staged. Globals from the map are still applied.
                ViewModelInstance labels = CreateGlobalInstance(file, LabelsViewModel);
                m_mockLogger.Clear();

                bool bound = stateMachine.BindViewModelInstance(
                    null,
                    new Dictionary<string, ViewModelInstance> { { LabelsViewModel, labels } });

                Assert.IsTrue(bound);
                CollectionAssert.IsEmpty(m_mockLogger.LoggedErrors);
                Assert.IsNotNull(stateMachine.ViewModelInstance,
                    "Bind should back-fill a default main when the artboard has a default view model.");
                Assert.AreSame(labels, stateMachine.GetGlobalViewModelInstance(LabelsViewModel));
            });
        }

        [UnityTest]
        public IEnumerator BindViewModelInstance_NullMainAfterBound_KeepsExistingMainAndUpdatesGlobals()
        {
            yield return LoadStateMachine((file, artboard, stateMachine) =>
            {

                ViewModelInstance main = CreateMainInstance(artboard);
                ViewModelInstance uk = CreateGlobalInstance(file, LabelsViewModel, "UK");
                ViewModelInstance us = CreateGlobalInstance(file, LabelsViewModel, "US");

                Assert.IsTrue(stateMachine.BindViewModelInstance(
                    main,
                    new Dictionary<string, ViewModelInstance> { { LabelsViewModel, uk } }));
                Assert.AreSame(main, stateMachine.ViewModelInstance);
                Assert.AreSame(uk, stateMachine.GetGlobalViewModelInstance(LabelsViewModel));

                Assert.IsTrue(stateMachine.BindViewModelInstance(
                    null,
                    new Dictionary<string, ViewModelInstance> { { LabelsViewModel, us } }));

                Assert.AreSame(main, stateMachine.ViewModelInstance,
                    "A null main on rebind must not replace or re-create the already-bound main.");
                Assert.AreSame(us, stateMachine.GetGlobalViewModelInstance(LabelsViewModel),
                    "Updated globals from the map should still apply.");
            });
        }

        [UnityTest]
        public IEnumerator BindViewModelInstance_NullGlobalsMap_TreatedAsEmpty()
        {
            yield return LoadStateMachine((file, artboard, stateMachine) =>
            {
                ViewModelInstance main = CreateMainInstance(artboard);
                m_mockLogger.Clear();

                bool bound = stateMachine.BindViewModelInstance(main, null);

                Assert.IsTrue(bound);
                CollectionAssert.IsEmpty(m_mockLogger.LoggedErrors);
                Assert.AreSame(main, stateMachine.ViewModelInstance);
                Assert.IsNotNull(stateMachine.GetGlobalViewModelInstance(LabelsViewModel),
                    "A null globals map means no overrides; omitted globals still get defaults.");
            });
        }

        [UnityTest]
        public IEnumerator BindViewModelInstance_DisposedMain_IsRejectedBeforeBinding()
        {
            yield return LoadStateMachine((file, artboard, stateMachine) =>
            {
                ViewModelInstance main = CreateMainInstance(artboard);
                main.Dispose();

                bool wasBound = stateMachine.BindViewModelInstance(
                    main,
                    new Dictionary<string, ViewModelInstance>
                    {
                        { LabelsViewModel, CreateGlobalInstance(file, LabelsViewModel) }
                    });

                Assert.IsFalse(wasBound);
                Assert.IsTrue(m_mockLogger.LoggedErrorsContains("ViewModelInstance has been disposed"));
                Assert.IsNull(stateMachine.ViewModelInstance);
                Assert.IsNull(stateMachine.GetGlobalViewModelInstance(LabelsViewModel),
                    "A rejected bind should not have touched the state machine.");
            });
        }

        [UnityTest]
        public IEnumerator BindViewModelInstance_DisposedMain_MainOnlyOverload_IsRejected()
        {
            yield return LoadStateMachine((file, artboard, stateMachine) =>
            {
                ViewModelInstance main = CreateMainInstance(artboard);
                main.Dispose();
                m_mockLogger.Clear();

                stateMachine.BindViewModelInstance(main);

                Assert.IsTrue(m_mockLogger.LoggedErrorsContains("ViewModelInstance has been disposed"));
                Assert.IsNull(stateMachine.ViewModelInstance);
                Assert.IsNull(stateMachine.GetGlobalViewModelInstance(LabelsViewModel),
                    "A rejected bind should not have created the globals that a bind would.");
            });
        }

        /// <summary>
        /// If a global name is misspelled in the input dictionary, nothing should be changed or bound.
        /// This prevents accidentally creating default globals in the correct slots, which could result in unexpected state.
        /// </summary>
        [UnityTest]
        public IEnumerator BindViewModelInstance_UnknownGlobalName_LeavesStateMachineUnchanged()
        {
            yield return LoadStateMachine((file, artboard, stateMachine) =>
            {

                ViewModelInstance main = CreateMainInstance(artboard);
                ViewModelInstance labels = CreateGlobalInstance(file, LabelsViewModel);

                bool bound = stateMachine.BindViewModelInstance(
                    main,
                    new Dictionary<string, ViewModelInstance>
                    {
                        { LabelsViewModel, labels },
                        { "Colours", CreateGlobalInstance(file, ColorsViewModel) }
                    });

                Assert.IsFalse(bound, "An unknown global name should fail the whole bind.");
                Assert.IsTrue(m_mockLogger.LoggedErrorsContains("No global view model named 'Colours'"));
                Assert.IsNull(stateMachine.ViewModelInstance,
                    "A failed dictionary bind must not stage the main instance.");
                Assert.IsNull(stateMachine.GetGlobalViewModelInstance(LabelsViewModel),
                    "A failed dictionary bind must not apply the valid global entries either.");
                Assert.IsNull(stateMachine.GetGlobalViewModelInstance(ColorsViewModel),
                    "A failed dictionary bind must not back-fill defaults for omitted/typo'd slots.");
            });
        }

        [UnityTest]
        public IEnumerator BindViewModelInstance_UnknownGlobalName_AfterPriorBind_DoesNotMutate()
        {
            yield return LoadStateMachine((file, artboard, stateMachine) =>
            {
                ViewModelInstance main = CreateMainInstance(artboard);
                ViewModelInstance uk = CreateGlobalInstance(file, LabelsViewModel, "UK");

                Assert.IsTrue(stateMachine.BindViewModelInstance(
                    main,
                    new Dictionary<string, ViewModelInstance> { { LabelsViewModel, uk } }));

                ViewModelInstance us = CreateGlobalInstance(file, LabelsViewModel, "US");
                m_mockLogger.Clear();

                bool bound = stateMachine.BindViewModelInstance(
                    main,
                    new Dictionary<string, ViewModelInstance>
                    {
                        { LabelsViewModel, us },
                        { "NotAGlobal", CreateGlobalInstance(file, SizesViewModel) }
                    });

                Assert.IsFalse(bound);
                Assert.AreSame(main, stateMachine.ViewModelInstance);
                Assert.AreSame(uk, stateMachine.GetGlobalViewModelInstance(LabelsViewModel),
                    "A failed rebind must leave previously bound globals untouched.");
            });
        }

        /// <summary>
        /// If any entry in the dictionary is null, the whole binding should fail and nothing should change.
        /// Even if some other entries are valid, they should not be used if any entry is null.
        /// </summary>
        [UnityTest]
        public IEnumerator BindViewModelInstance_NullGlobalInstance_LeavesStateMachineUnchanged()
        {
            yield return LoadStateMachine((file, artboard, stateMachine) =>
            {
                ViewModelInstance main = CreateMainInstance(artboard);
                ViewModelInstance colors = CreateGlobalInstance(file, ColorsViewModel);

                bool bound = stateMachine.BindViewModelInstance(
                    main,
                    new Dictionary<string, ViewModelInstance>
                    {
                        { LabelsViewModel, null },
                        { ColorsViewModel, colors }
                    });

                Assert.IsFalse(bound);
                Assert.IsTrue(m_mockLogger.LoggedErrorsContains(
                    $"Global view model '{LabelsViewModel}' was given a null instance"));
                Assert.IsNull(stateMachine.ViewModelInstance,
                    "A null entry must fail the whole bind before anything is staged.");
                Assert.IsNull(stateMachine.GetGlobalViewModelInstance(ColorsViewModel),
                    "Valid sibling entries must not apply when another entry is null.");
            });
        }

        [UnityTest]
        public IEnumerator BindViewModelInstance_DisposedGlobal_LeavesStateMachineUnchanged()
        {
            yield return LoadStateMachine((file, artboard, stateMachine) =>
            {
                ViewModelInstance main = CreateMainInstance(artboard);
                ViewModelInstance labels = CreateGlobalInstance(file, LabelsViewModel);
                ViewModelInstance colors = CreateGlobalInstance(file, ColorsViewModel);
                labels.Dispose();

                bool bound = stateMachine.BindViewModelInstance(
                    main,
                    new Dictionary<string, ViewModelInstance>
                    {
                        { LabelsViewModel, labels },
                        { ColorsViewModel, colors }
                    });

                Assert.IsFalse(bound);
                Assert.IsTrue(m_mockLogger.LoggedErrorsContains(
                    $"Global view model '{LabelsViewModel}' was given a disposed instance"));
                Assert.IsNull(stateMachine.ViewModelInstance,
                    "A disposed global entry must fail the whole bind before anything is staged.");
                Assert.IsNull(stateMachine.GetGlobalViewModelInstance(ColorsViewModel),
                    "Valid sibling entries must not apply when another entry is disposed.");
            });
        }

        [UnityTest]
        public IEnumerator BindViewModelInstance_RebindOmittingGlobal_KeepsPreviousUserInstance()
        {
            yield return LoadStateMachine((file, artboard, stateMachine) =>
            {
                // When you rebind and leave out a key, the existing global instance stays the same—it isn't removed or swapped for something else.

                ViewModelInstance main = CreateMainInstance(artboard);
                ViewModelInstance uk = CreateGlobalInstance(file, LabelsViewModel, "UK");
                ViewModelInstance sizes = CreateGlobalInstance(file, SizesViewModel);

                Assert.IsTrue(stateMachine.BindViewModelInstance(
                    main,
                    new Dictionary<string, ViewModelInstance>
                    {
                        { LabelsViewModel, uk },
                        { SizesViewModel, sizes }
                    }));

                Assert.IsTrue(stateMachine.BindViewModelInstance(
                    main,
                    new Dictionary<string, ViewModelInstance>
                    {
                        { SizesViewModel, sizes }
                    }));

                Assert.AreSame(uk, stateMachine.GetGlobalViewModelInstance(LabelsViewModel),
                    "Omitting a previously user-set global from a later rebind must keep that instance.");
                Assert.AreSame(sizes, stateMachine.GetGlobalViewModelInstance(SizesViewModel));
            });
        }

        [UnityTest]
        public IEnumerator BindViewModelInstance_RebindOmittingGlobal_KeepsMutatedAutoCreatedDefault()
        {
            yield return LoadStateMachine((file, artboard, stateMachine) =>
            {
                // bind() will initially fill in defaults for any missing globals.
                // If you rebind later but leave out a key, the existing instance (with any changes made to it) should be kept.
                // A new default shouldn't be created in that case.

                ViewModelInstance main = CreateMainInstance(artboard);

                Assert.IsTrue(stateMachine.BindViewModelInstance(
                    main,
                    new Dictionary<string, ViewModelInstance>()));

                ViewModelInstance colors = stateMachine.GetGlobalViewModelInstance(ColorsViewModel);
                Assert.IsNotNull(colors);
                var background = colors.GetProperty<ViewModelInstanceColorProperty>("backgroundColor");
                background.Value32 = new Color32(10, 20, 30, 255);

                ViewModelInstance uk = CreateGlobalInstance(file, LabelsViewModel, "UK");
                Assert.IsTrue(stateMachine.BindViewModelInstance(
                    main,
                    new Dictionary<string, ViewModelInstance> { { LabelsViewModel, uk } }));

                ViewModelInstance colorsAfter = stateMachine.GetGlobalViewModelInstance(ColorsViewModel);
                Assert.AreSame(colors, colorsAfter,
                    "An auto-created default omitted from a later rebind must keep the same instance.");
                AssertColorEquals(
                    new Color32(10, 20, 30, 255),
                    colorsAfter.GetProperty<ViewModelInstanceColorProperty>("backgroundColor").Value32,
                    "Mutations on an auto-created default must survive a rebind that omits that key.");
                Assert.AreSame(uk, stateMachine.GetGlobalViewModelInstance(LabelsViewModel));
            });
        }

        [UnityTest]
        public IEnumerator BindViewModelInstance_CrossViewModelOverride_IsAllowed()
        {
            yield return LoadStateMachine((file, artboard, stateMachine) =>
            {
                // Core allows placing an instance of one view model into another global's slot.
                ViewModelInstance sizesInstance = CreateGlobalInstance(file, SizesViewModel);

                bool bound = stateMachine.BindViewModelInstance(
                    CreateMainInstance(artboard),
                    new Dictionary<string, ViewModelInstance>
                    {
                        { ColorsViewModel, sizesInstance }
                    });

                Assert.IsTrue(bound, "Cross-view-model placement into a global slot is allowed in core runtime.");
                Assert.AreSame(sizesInstance, stateMachine.GetGlobalViewModelInstance(ColorsViewModel));
            });
        }

        [UnityTest]
        public IEnumerator GetGlobalViewModelInstance_AfterBoundInstanceIsDisposed_ReturnsLiveWrapper()
        {
            yield return LoadStateMachine((file, artboard, stateMachine) =>
            {
                // The state machine caches the instances it hands out. Disposing one behind its back
                // has to invalidate that entry, or later reads return a wrapper with a closed handle.
                ViewModelInstance usInstance = CreateGlobalInstance(file, LabelsViewModel, "US");

                Assert.IsTrue(stateMachine.BindViewModelInstance(
                    CreateMainInstance(artboard),
                    new Dictionary<string, ViewModelInstance> { { LabelsViewModel, usInstance } }));
                Assert.AreSame(usInstance, stateMachine.GetGlobalViewModelInstance(LabelsViewModel));

                usInstance.Dispose();

                ViewModelInstance refreshed = stateMachine.GetGlobalViewModelInstance(LabelsViewModel);

                Assert.IsNotNull(refreshed, "The global is still bound natively, so it should resolve.");
                Assert.AreNotSame(usInstance, refreshed, "A disposed wrapper should not be handed out again.");
                Assert.IsFalse(refreshed.IsDisposed);
                Assert.AreEqual("$", refreshed.GetProperty<ViewModelInstanceStringProperty>("currency").Value,
                    "The refreshed wrapper should read the same instance that was bound.");
            });
        }

        [UnityTest]
        public IEnumerator BindViewModelInstance_RepeatedBadName_IsReportedEachTime()
        {
            yield return LoadStateMachine((file, artboard, stateMachine) =>
            {
                ViewModelInstance main = CreateMainInstance(artboard);
                var globals = new Dictionary<string, ViewModelInstance>
                {
                    { "NotAGlobal", CreateGlobalInstance(file, SizesViewModel) }
                };

                m_mockLogger.Clear();

                stateMachine.BindViewModelInstance(main, globals);
                stateMachine.BindViewModelInstance(main, globals);
                stateMachine.BindViewModelInstance(main, globals);

                Assert.AreEqual(3, m_mockLogger.LoggedErrors.Count,
                    "Every bind that names a global the file doesn't have should say so.");
            });
        }

        [UnityTest]
        public IEnumerator GlobalBindingApis_OnDisposedStateMachine_FailSoftly()
        {
            yield return LoadStateMachine((file, artboard, stateMachine) =>
            {
                ViewModelInstance main = CreateMainInstance(artboard);
                var globals = new Dictionary<string, ViewModelInstance>
                {
                    { LabelsViewModel, CreateGlobalInstance(file, LabelsViewModel) }
                };

                stateMachine.Dispose();
                m_mockLogger.Clear();

                Assert.IsFalse(stateMachine.BindViewModelInstance(main, globals));
                Assert.IsNull(stateMachine.GetGlobalViewModelInstance(LabelsViewModel));
                Assert.IsTrue(m_mockLogger.LoggedErrorsContains("disposed StateMachine"));
            });
        }

#if UNITY_EDITOR
        // Leak tests: Every time we bind or read a global, a new
        // ViewModelInstanceRuntime wrapper is created through P/Invoke. Each wrapper adds a native reference
        // to the underlying core object. If we don't properly clean up (dispose) these wrappers,
        // we'll slowly leak native references each time.
        //
        // Note: What matters is the change in reference count between two checks, not the absolute value,
        // since other parts of the code might also be holding references. Also, we can only read
        // the ref count while our wrapper is still alive, so each check below uses a still-referenced instance.

        private const int LeakProbeIterations = 25;

        private const float WaitForCollectionTimeoutSeconds = 5f;

        [UnityTest]
        public IEnumerator BindViewModelInstance_RepeatedBinds_DoNotLeakNativeReferences()
        {
            yield return LoadStateMachine((file, artboard, stateMachine) =>
            {
                ViewModelInstance main = CreateMainInstance(artboard);
                var globals = new Dictionary<string, ViewModelInstance>();
                foreach (string name in ExpectedGlobalNames)
                {
                    globals[name] = CreateGlobalInstance(file, name);
                }

                // Snapshot after the first bind so one-time setup isn't counted as a leak.
                Assert.IsTrue(stateMachine.BindViewModelInstance(main, globals));

                int mainRefCount = main.DebugNativeRefCount;
                var globalRefCounts = new Dictionary<string, int>();
                foreach (string name in ExpectedGlobalNames)
                {
                    globalRefCounts[name] = globals[name].DebugNativeRefCount;
                }

                for (int i = 0; i < LeakProbeIterations; i++)
                {
                    Assert.IsTrue(stateMachine.BindViewModelInstance(main, globals));
                }

                Assert.AreEqual(mainRefCount, main.DebugNativeRefCount,
                    $"Rebinding the same main instance {LeakProbeIterations} times should not change its native reference count.");

                foreach (string name in ExpectedGlobalNames)
                {
                    Assert.AreEqual(globalRefCounts[name], globals[name].DebugNativeRefCount,
                        $"Rebinding should not change the native reference count of global '{name}'.");
                }
            });
        }

        [UnityTest]
        public IEnumerator GetGlobalViewModelInstance_RepeatedNativeReads_DoNotLeakNativeReferences()
        {
            yield return LoadStateMachine((file, artboard, stateMachine) =>
            {
                // Only bind the main instance here so we don't keep a reference to the Labels global ourselves.
                // This allows us to create and dispose of probe wrappers in the loop below to test for leaks.
                stateMachine.BindViewModelInstance(CreateMainInstance(artboard));

                ViewModelInstance probe = stateMachine.GetGlobalViewModelInstance(LabelsViewModel);
                Assert.IsNotNull(probe);
                int before = probe.DebugNativeRefCount;

                // Disposing the probe wrapper removes it from the state machine's cache,
                // so the next call will fetch a fresh wrapper from native (not just reuse the cache).
                // If we didn't do this, we'd only test the cache and wouldn't catch leaks from repeated native access.
                probe.Dispose();

                for (int i = 0; i < LeakProbeIterations; i++)
                {
                    ViewModelInstance read = stateMachine.GetGlobalViewModelInstance(LabelsViewModel);
                    Assert.IsNotNull(read, "The global stays bound, so every read should resolve.");
                    read.Dispose();
                }

                // Taken with exactly one live wrapper, same as the reading before the loop.
                ViewModelInstance after = stateMachine.GetGlobalViewModelInstance(LabelsViewModel);
                Assert.IsNotNull(after);
                Assert.AreEqual(before, after.DebugNativeRefCount,
                    $"Reading a global {LeakProbeIterations} times should not accumulate native references.");
            });
        }

        [UnityTest]
        public IEnumerator Dispose_ReleasesEveryNativeReferenceTakenByBinding()
        {
            yield return LoadStateMachine((file, artboard, stateMachine) =>
            {
                // This test's wrapper is the only reference keeping the core instance alive, so we can still check the count after everything else in the runtime lets go.
                ViewModelInstance labels = CreateGlobalInstance(file, LabelsViewModel);

                // When we first create an instance, only the C# object holds it. So the reference count starts at 1 ("we are the only owner"). Later, we'll check that after cleanup we return to 1.
                Assert.AreEqual(1, labels.DebugNativeRefCount,
                    "A newly created instance should be held only by the wrapper that created it.");

                Assert.IsTrue(stateMachine.BindViewModelInstance(
                    CreateMainInstance(artboard),
                    new Dictionary<string, ViewModelInstance> { { LabelsViewModel, labels } }));

                // Binding should increase the reference count, since the runtime takes its own reference to the global.
                Assert.Greater(labels.DebugNativeRefCount, 1,
                    "Binding a global should take at least one native reference on it.");

                // Disposing both the state machine and the artboard releases all references taken by the binding. We have to dispose both before the count drops.
                stateMachine.Dispose();
                artboard.Dispose();

                // After cleanup, our test wrapper is the only thing left holding a reference, so the count is 1 again. Releasing our handle will free it.
                Assert.AreEqual(1, labels.DebugNativeRefCount,
                    "Disposing should leave the test's wrapper as the only holder, so the instance is freed when it goes.");

                // Release our last reference and ensure the instance reports as disposed.
                labels.Dispose();
                Assert.IsTrue(labels.IsDisposed);
            });
        }

        [UnityTest]
        public IEnumerator Dispose_WithBoundGlobals_StillReleasesTheNativeFile()
        {
            // Bypass FileLoader's asset cache. File.Load(asset) shares one native file across every
            // load of the same asset, so a Dispose only unrefs when the cache refcount hits zero.
            // Selecting the .riv in the Project window (AssetEditor) or opening the data-binding
            // playground bumps that count and makes isRiveFileValid stay true after our Dispose.
            // A custom loader always allocates a fresh instance this test alone owns.
            Asset riveAsset = null;
            yield return m_testAssetLoadingManager.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_global_variables_test,
                (asset) => riveAsset = asset,
                () => Assert.Fail($"Failed to load asset at {TestAssetReferences.riv_global_variables_test}")
            );

            File file = File.Load(riveAsset, _ => false);
            Assert.IsNotNull(file, "Failed to load the Rive file.");
            m_loadedFiles.Add(file);

            Artboard artboard = file.Artboard(MainArtboard);
            Assert.IsNotNull(artboard, $"Expected an artboard named '{MainArtboard}'.");

            StateMachine stateMachine = artboard.StateMachine();
            Assert.IsNotNull(stateMachine, "Expected a default state machine on the Main artboard.");
            m_stateMachines.Add(stateMachine);

            // Globals are accessed by the artboard referring back to its file. This test checks that releasing everything stops the file from being kept alive through those connections.
            IntPtr nativeFile = file.NativeFile;

            ViewModelInstance labels = CreateGlobalInstance(file, LabelsViewModel);
            ViewModelInstance main = CreateMainInstance(artboard);
            Assert.IsTrue(stateMachine.BindViewModelInstance(
                main,
                new Dictionary<string, ViewModelInstance> { { LabelsViewModel, labels } }));

            Assert.IsTrue(NativeFileInterface.isRiveFileValid(nativeFile),
                "The file should be alive while its artboard and state machine are.");

            // Every instance goes too: they outlive the file otherwise, pointing at view models the
            // file owns.
            main.Dispose();
            labels.Dispose();
            stateMachine.Dispose();
            artboard.Dispose();
            file.Dispose();
Assert.IsFalse(NativeFileInterface.isRiveFileValid(nativeFile),
                "Binding globals should not keep the native file alive after everything using it is disposed.");
        }

        [UnityTest]
        public IEnumerator Dispose_ReleasesManagedWrappersForGlobals()
        {
            Artboard artboard = null;
            StateMachine stateMachine = null;

            // We need artboard and stateMachine across multiple frames, so capture them outside the callback.
            // The file object stays alive because it's tracked by the test fixture.
            yield return LoadStateMachine((loadedFile, loadedArtboard, loadedStateMachine) =>
            {
                artboard = loadedArtboard;
                stateMachine = loadedStateMachine;
            });

            // Only binding the main view model, so all global view models use the default ones created by the runtime.
            // At this point, only the state machine keeps strong references to those global wrappers.
            stateMachine.BindViewModelInstance(CreateMainInstance(artboard));

            WeakReference globalWeakRef;
            {
                ViewModelInstance global = stateMachine.GetGlobalViewModelInstance(LabelsViewModel);
                Assert.IsNotNull(global);
                globalWeakRef = new WeakReference(global);
                global = null;
            }

            Assert.IsTrue(globalWeakRef.IsAlive,
                "The state machine should hold a strong reference to a global it handed out.");

            stateMachine.Dispose();

            float waitStartTime = Time.realtimeSinceStartup;
            while (globalWeakRef.IsAlive &&
                   Time.realtimeSinceStartup - waitStartTime < WaitForCollectionTimeoutSeconds)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                yield return null;
            }

            Assert.IsFalse(globalWeakRef.IsAlive,
                "Disposing the state machine should release its global wrappers so they can be collected.");
        }
#endif
    }
}
