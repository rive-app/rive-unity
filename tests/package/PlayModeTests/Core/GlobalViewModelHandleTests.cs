using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using Rive.Components;
using Rive.Producer;
using Rive.Tests.Utils;
using Rive.Utils;
using UnityEngine;
using UnityEngine.TestTools;
using Rive.Host;

namespace Rive.Tests
{
    /// <summary>
    /// Global view models through handles, mirroring <see cref="GlobalViewModelTests"/>. A global
    /// handle is a new handle to whatever instance is bound, so identity is checked through values:
    /// change it through one handle, read it through the other.
    /// </summary>
    public class GlobalViewModelHandleTests
    {
        private const string SizesViewModel = "Sizes";
        private const string ColorsViewModel = "Colors";
        private const string LabelsViewModel = "Labels";

        private static readonly string[] ExpectedGlobalNames = { SizesViewModel, ColorsViewModel, LabelsViewModel };

        private TestAssetLoadingManager m_assetLoader;
        private MockLogger m_mockLogger;
        private readonly List<IDisposable> m_toDispose = new List<IDisposable>();
        private readonly List<GameObject> m_objects = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            m_mockLogger = new MockLogger();
            DebugLogger.Instance = m_mockLogger;
            m_assetLoader = new TestAssetLoadingManager();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < m_objects.Count; i++)
            {
                if (m_objects[i] != null)
                {
                    UnityEngine.Object.Destroy(m_objects[i]);
                }
            }
            m_objects.Clear();
            for (int i = m_toDispose.Count - 1; i >= 0; i--)
            {
                m_toDispose[i]?.Dispose();
            }
            m_toDispose.Clear();
            m_assetLoader.UnloadAllAssets();
        }

        private T Track<T>(T disposable) where T : IDisposable
        {
            m_toDispose.Add(disposable);
            return disposable;
        }

        private IEnumerator LoadFile(Action<FileHandle> onLoaded)
        {
            Asset asset = null;
            yield return m_assetLoader.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_global_variables_test, a => asset = a, () => Assert.Fail("Failed to load the globals asset"));
            Future<FileHandle> load = FileHandle.LoadAsync(asset);
            yield return WaitUntil(() => load.IsDone, "The file load");
            Assert.AreEqual(FutureStatus.Succeeded, load.Status);
            onLoaded(Track(load.Result));
        }

        // A state machine off the named artboard with nothing bound yet.
        private IEnumerator LoadStateMachine(Action<FileHandle, ArtboardHandle, StateMachineHandle> onLoaded, string artboardName = "Main")
        {
            FileHandle file = null;
            yield return LoadFile(f => file = f);
            ArtboardHandle artboard = Track(file.InstantiateArtboard(artboardName));
            onLoaded(file, artboard, Track(artboard.InstantiateStateMachine()));
        }

        private ViewModelInstanceHandle Main(ArtboardHandle artboard) => Track(artboard.GetDefaultViewModel().Instantiate());

        private ViewModelInstanceHandle Global(FileHandle file, string viewModel, string instance = null) =>
            Track(instance == null ? file.GetViewModel(viewModel).Instantiate() : file.GetViewModel(viewModel).Instantiate(instance));

        private ViewModelInstanceHandle BoundGlobal(StateMachineHandle stateMachine, string name) =>
            Track(stateMachine.GetGlobalViewModelInstance(name));

        private static IEnumerator WaitUntil(Func<bool> condition, string what)
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!condition())
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, $"{what} didn't happen within 10 seconds.");
                yield return null;
            }
        }

        private static IEnumerator Read<T>(Future<T> read, Action<Future<T>> onDone)
        {
            yield return WaitUntil(() => read.IsDone, "The read");
            onDone(read);
        }

        // Checks core's main instance through a fresh lookup. Handles to one
        // instance share a native handle, so that's what's compared.
        private IEnumerator AssertMain(StateMachineHandle stateMachine, ViewModelInstanceHandle expected, string message = null)
        {
            ViewModelInstanceHandle bound = Track(stateMachine.GetViewModelInstance());
            Future<ViewModelInstanceHandle> resolve = bound.ResolveAsync();
            yield return WaitUntil(() => resolve.IsDone, "The main instance lookup");
            Assert.AreEqual(FutureStatus.Succeeded, resolve.Status, resolve.Exception?.Message);
            Assert.AreEqual(expected.Native.Value, bound.Native.Value, message ?? "The state machine should have this main instance bound.");
        }

        // Waits for a bind and checks it went through.
        private static IEnumerator ExpectBound(Future bind)
        {
            yield return WaitUntil(() => bind.IsDone, "The bind");
            Assert.AreEqual(FutureStatus.Succeeded, bind.Status, bind.Exception?.Message);
        }

        // A bind rejected at the call is already done.
        private static void AssertRejected(Future bind, RiveErrorCode code)
        {
            Assert.IsTrue(bind.IsDone, "A bind rejected at the call should be done straight away.");
            AssertFailed(bind, code);
        }

        private static void AssertFailed(Future bind, RiveErrorCode code)
        {
            Assert.AreEqual(FutureStatus.Failed, bind.Status);
            Assert.IsInstanceOf<RiveException>(bind.Exception);
            Assert.AreEqual(code, ((RiveException)bind.Exception).Code, bind.Exception.Message);
        }

        private static Dictionary<string, ViewModelInstanceHandle> Globals(params (string, ViewModelInstanceHandle)[] entries)
        {
            var globals = new Dictionary<string, ViewModelInstanceHandle>();
            foreach (var (name, instance) in entries)
            {
                globals[name] = instance;
            }
            return globals;
        }

        [UnityTest]
        public IEnumerator GlobalViewModelNames_ReturnsGlobalsInFileOrder()
        {
            FileHandle file = null;
            yield return LoadFile(f => file = f);
            CollectionAssert.AreEqual(ExpectedGlobalNames, file.GetGlobalViewModelNamesAsync().Result);
        }

        [UnityTest]
        public IEnumerator GlobalViewModelNames_ExcludeNonGlobalViewModels()
        {
            FileHandle file = null;
            yield return LoadFile(f => file = f);
            foreach (string nonGlobal in new[] { "Main", "Child", "IntermediateList" })
            {
                Assert.IsNotNull(file.GetViewModel(nonGlobal), $"'{nonGlobal}' is a view model in the file.");
                CollectionAssert.DoesNotContain(file.GetGlobalViewModelNamesAsync().Result, nonGlobal);
            }
        }

        [UnityTest]
        public IEnumerator GetGlobal_BeforeBind_RefersToNothing()
        {
            StateMachineHandle stateMachine = null;
            yield return LoadStateMachine((f, a, s) => stateMachine = s);

            Future<string> read = BoundGlobal(stateMachine, LabelsViewModel).GetStringProperty("currency").GetValueAsync();
            yield return WaitUntil(() => read.IsDone, "The read");

            Assert.AreEqual(FutureStatus.Failed, read.Status);
            Assert.AreEqual(RiveErrorCode.ViewModelInstanceNotFound, ((RiveException)read.Exception).Code);
        }

        [UnityTest]
        public IEnumerator GetGlobal_UnknownOrEmptyName_ReturnsNull()
        {
            StateMachineHandle stateMachine = null;
            yield return LoadStateMachine((f, a, s) => stateMachine = s);

            Assert.IsNull(stateMachine.GetGlobalViewModelInstance("NotAGlobal"));
            Assert.IsNull(stateMachine.GetGlobalViewModelInstance(string.Empty));
            Assert.IsNull(stateMachine.GetGlobalViewModelInstance(null));
            Assert.IsTrue(m_mockLogger.LoggedWarningsContains("no global view model named 'NotAGlobal'"));
        }

        [UnityTest]
        public IEnumerator BindWithGlobals_BindsEveryNamedGlobal()
        {
            FileHandle file = null;
            ArtboardHandle artboard = null;
            StateMachineHandle stateMachine = null;
            yield return LoadStateMachine((f, a, s) => { file = f; artboard = a; stateMachine = s; });

            ViewModelInstanceHandle sizes = Global(file, SizesViewModel);
            sizes.GetNumberProperty("gaps").SetValue(77f);
            ViewModelInstanceHandle colors = Global(file, ColorsViewModel);
            colors.GetColorProperty("backgroundColor").SetValue(new UnityEngine.Color(1f, 0f, 0f, 1f));
            ViewModelInstanceHandle labels = Global(file, LabelsViewModel);
            labels.GetStringProperty("currency").SetValue("bound");

            yield return ExpectBound(stateMachine.BindViewModelInstanceAsync(Main(artboard),
                Globals((SizesViewModel, sizes), (ColorsViewModel, colors), (LabelsViewModel, labels))));

            Future<float> gaps = BoundGlobal(stateMachine, SizesViewModel).GetNumberProperty("gaps").GetValueAsync();
            Future<UnityEngine.Color> background = BoundGlobal(stateMachine, ColorsViewModel).GetColorProperty("backgroundColor").GetValueAsync();
            Future<string> currency = BoundGlobal(stateMachine, LabelsViewModel).GetStringProperty("currency").GetValueAsync();
            yield return WaitUntil(() => currency.IsDone, "The reads");

            Assert.AreEqual(77f, gaps.Result);
            Assert.AreEqual(new UnityEngine.Color(1f, 0f, 0f, 1f), background.Result);
            Assert.AreEqual("bound", currency.Result);
        }

        [UnityTest]
        public IEnumerator BindWithGlobals_BindsMainInstance()
        {
            FileHandle file = null;
            ArtboardHandle artboard = null;
            StateMachineHandle stateMachine = null;
            yield return LoadStateMachine((f, a, s) => { file = f; artboard = a; stateMachine = s; });
            ViewModelInstanceHandle main = Main(artboard);

            yield return ExpectBound(stateMachine.BindViewModelInstanceAsync(main, Globals((LabelsViewModel, Global(file, LabelsViewModel, "UK")))));

            yield return AssertMain(stateMachine, main);
        }

        [UnityTest]
        public IEnumerator OmittedGlobal_GetsDefaultInstance()
        {
            FileHandle file = null;
            ArtboardHandle artboard = null;
            StateMachineHandle stateMachine = null;
            yield return LoadStateMachine((f, a, s) => { file = f; artboard = a; stateMachine = s; });

            yield return ExpectBound(stateMachine.BindViewModelInstanceAsync(Main(artboard),
                Globals((LabelsViewModel, Global(file, LabelsViewModel, "UK")))));
            Future<float> gaps = BoundGlobal(stateMachine, SizesViewModel).GetNumberProperty("gaps").GetValueAsync();
            Future<UnityEngine.Color> background = BoundGlobal(stateMachine, ColorsViewModel).GetColorProperty("backgroundColor").GetValueAsync();
            yield return WaitUntil(() => background.IsDone, "The reads");

            Assert.AreEqual(16f, gaps.Result, "The omitted Sizes global should hold its default 'gaps'.");
            Assert.AreEqual((Color32)new UnityEngine.Color(223f / 255f, 223f / 255f, 223f / 255f, 1f), (Color32)background.Result,
                "The omitted Colors global should hold its default 'backgroundColor'.");
        }

        [UnityTest]
        public IEnumerator MainOnlyBind_StillCreatesGlobals()
        {
            ArtboardHandle artboard = null;
            StateMachineHandle stateMachine = null;
            yield return LoadStateMachine((f, a, s) => { artboard = a; stateMachine = s; });

            stateMachine.BindViewModelInstanceAsync(Main(artboard));
            Future<float> gaps = BoundGlobal(stateMachine, SizesViewModel).GetNumberProperty("gaps").GetValueAsync();
            Future<string> currency = BoundGlobal(stateMachine, LabelsViewModel).GetStringProperty("currency").GetValueAsync();
            Future<UnityEngine.Color> background = BoundGlobal(stateMachine, ColorsViewModel).GetColorProperty("backgroundColor").GetValueAsync();
            yield return WaitUntil(() => background.IsDone, "The reads");

            Assert.AreEqual(FutureStatus.Succeeded, gaps.Status);
            Assert.AreEqual(FutureStatus.Succeeded, currency.Status);
            Assert.AreEqual(FutureStatus.Succeeded, background.Status);
        }

        [UnityTest]
        public IEnumerator Rebind_ReplacesGlobalInstance()
        {
            FileHandle file = null;
            ArtboardHandle artboard = null;
            StateMachineHandle stateMachine = null;
            yield return LoadStateMachine((f, a, s) => { file = f; artboard = a; stateMachine = s; });
            ViewModelInstanceHandle main = Main(artboard);

            yield return ExpectBound(stateMachine.BindViewModelInstanceAsync(main, Globals((LabelsViewModel, Global(file, LabelsViewModel, "UK")))));
            yield return ExpectBound(stateMachine.BindViewModelInstanceAsync(main, Globals((LabelsViewModel, Global(file, LabelsViewModel, "US")))));
            Future<string> currency = BoundGlobal(stateMachine, LabelsViewModel).GetStringProperty("currency").GetValueAsync();
            yield return WaitUntil(() => currency.IsDone, "The read");

            Assert.AreEqual("$", currency.Result, "The rebind should replace UK with US.");
        }

        [UnityTest]
        public IEnumerator NamedInstance_CarriesItsOwnValues()
        {
            FileHandle file = null;
            ArtboardHandle artboard = null;
            StateMachineHandle stateMachine = null;
            yield return LoadStateMachine((f, a, s) => { file = f; artboard = a; stateMachine = s; });

            yield return ExpectBound(stateMachine.BindViewModelInstanceAsync(Main(artboard), Globals((LabelsViewModel, Global(file, LabelsViewModel, "US")))));
            Future<string> currency = BoundGlobal(stateMachine, LabelsViewModel).GetStringProperty("currency").GetValueAsync();
            yield return WaitUntil(() => currency.IsDone, "The read");

            Assert.AreEqual("$", currency.Result);
        }

        [UnityTest]
        public IEnumerator SharedGlobal_ChangesShowOnBothStateMachines()
        {
            FileHandle file = null;
            yield return LoadFile(f => file = f);
            ViewModelInstanceHandle shared = Global(file, LabelsViewModel);
            var stateMachines = new List<StateMachineHandle>();
            foreach (string artboardName in new[] { "Main", "Child" })
            {
                ArtboardHandle artboard = Track(file.InstantiateArtboard(artboardName));
                StateMachineHandle stateMachine = Track(artboard.InstantiateStateMachine());
                yield return ExpectBound(stateMachine.BindViewModelInstanceAsync(Main(artboard), Globals((LabelsViewModel, shared))));
                stateMachines.Add(stateMachine);
            }

            var seen = new List<string>();
            StringPropertyHandle viaSecond = BoundGlobal(stateMachines[1], LabelsViewModel).GetStringProperty("currency");
            viaSecond.Subscribe(seen.Add);
            BoundGlobal(stateMachines[0], LabelsViewModel).GetStringProperty("currency").SetValue("USD");
            stateMachines[1].Advance(1f / 60f);
            yield return WaitUntil(() => seen.Count > 0, "The change through the other state machine");

            Assert.AreEqual("USD", seen[seen.Count - 1]);
        }

        [UnityTest]
        public IEnumerator DefaultGlobals_AreNotSharedAcrossStateMachines()
        {
            FileHandle file = null;
            yield return LoadFile(f => file = f);
            var labels = new List<ViewModelInstanceHandle>();
            for (int i = 0; i < 2; i++)
            {
                ArtboardHandle artboard = Track(file.InstantiateArtboard("Main"));
                StateMachineHandle stateMachine = Track(artboard.InstantiateStateMachine());
                stateMachine.BindViewModelInstanceAsync(Main(artboard));
                labels.Add(BoundGlobal(stateMachine, LabelsViewModel));
            }
            Future<string> original = labels[1].GetStringProperty("currency").GetValueAsync();
            yield return WaitUntil(() => original.IsDone, "The read");

            labels[0].GetStringProperty("currency").SetValue("USD");
            Future<string> first = labels[0].GetStringProperty("currency").GetValueAsync();
            Future<string> second = labels[1].GetStringProperty("currency").GetValueAsync();
            yield return WaitUntil(() => second.IsDone, "The reads");

            Assert.AreEqual("USD", first.Result);
            Assert.AreEqual(original.Result, second.Result, "Each state machine makes its own default global.");
        }

        [UnityTest]
        public IEnumerator NullMain_BindsGlobals()
        {
            FileHandle file = null;
            StateMachineHandle stateMachine = null;
            yield return LoadStateMachine((f, a, s) => { file = f; stateMachine = s; });

            yield return ExpectBound(stateMachine.BindViewModelInstanceAsync(null, Globals((LabelsViewModel, Global(file, LabelsViewModel, "US")))));
            Future<string> currency = BoundGlobal(stateMachine, LabelsViewModel).GetStringProperty("currency").GetValueAsync();
            yield return WaitUntil(() => currency.IsDone, "The read");

            Assert.AreEqual("$", currency.Result);
        }

        [UnityTest]
        public IEnumerator NullMain_FillsInAMainInstance_TheStateMachineReports()
        {
            FileHandle file = null;
            StateMachineHandle stateMachine = null;
            yield return LoadStateMachine((f, a, s) => { file = f; stateMachine = s; });

            yield return ExpectBound(stateMachine.BindViewModelInstanceAsync(null, Globals((LabelsViewModel, Global(file, LabelsViewModel, "US")))));

            ViewModelInstanceHandle filled = Track(stateMachine.GetViewModelInstance());
            Future<string> viewModelName = filled.GetViewModelNameAsync();
            Future<string> name = filled.GetNameAsync();
            yield return WaitUntil(() => name.IsDone && viewModelName.IsDone, "The reads");
            Assert.AreEqual("Main", viewModelName.Result);
            Assert.AreEqual(FutureStatus.Succeeded, name.Status, "It refers to a real instance.");
        }

        [UnityTest]
        public IEnumerator BindNull_FillsInDefaults()
        {
            StateMachineHandle stateMachine = null;
            yield return LoadStateMachine((f, a, s) => stateMachine = s);

            Future bind = stateMachine.BindViewModelInstanceAsync(null);
            ViewModelInstanceHandle filled = Track(stateMachine.GetViewModelInstance());
            Future<float> gaps = BoundGlobal(stateMachine, SizesViewModel).GetNumberProperty("gaps").GetValueAsync();
            Future<string> name = filled.GetNameAsync();
            yield return ExpectBound(bind);
            yield return WaitUntil(() => gaps.IsDone && name.IsDone, "The reads");

            Assert.AreEqual(16f, gaps.Result);
            Assert.AreEqual(FutureStatus.Succeeded, name.Status);
        }

        [UnityTest]
        public IEnumerator NullMainAfterBound_KeepsExistingMainAndUpdatesGlobals()
        {
            FileHandle file = null;
            ArtboardHandle artboard = null;
            StateMachineHandle stateMachine = null;
            yield return LoadStateMachine((f, a, s) => { file = f; artboard = a; stateMachine = s; });
            ViewModelInstanceHandle main = Main(artboard);

            yield return ExpectBound(stateMachine.BindViewModelInstanceAsync(main, Globals((LabelsViewModel, Global(file, LabelsViewModel, "UK")))));
            yield return ExpectBound(stateMachine.BindViewModelInstanceAsync(null, Globals((LabelsViewModel, Global(file, LabelsViewModel, "US")))));
            Future<string> currency = BoundGlobal(stateMachine, LabelsViewModel).GetStringProperty("currency").GetValueAsync();
            yield return WaitUntil(() => currency.IsDone, "The read");

            Assert.AreEqual("$", currency.Result);
            yield return AssertMain(stateMachine, main, "A null main keeps the bound one.");
        }

        [UnityTest]
        public IEnumerator NullGlobalsMap_TreatedAsEmpty()
        {
            ArtboardHandle artboard = null;
            StateMachineHandle stateMachine = null;
            yield return LoadStateMachine((f, a, s) => { artboard = a; stateMachine = s; });

            yield return ExpectBound(stateMachine.BindViewModelInstanceAsync(Main(artboard), null));
            Future<float> gaps = BoundGlobal(stateMachine, SizesViewModel).GetNumberProperty("gaps").GetValueAsync();
            yield return WaitUntil(() => gaps.IsDone, "The read");

            Assert.AreEqual(16f, gaps.Result, "Globals are still filled in.");
        }

        [UnityTest]
        public IEnumerator DisposedMain_IsRejected()
        {
            FileHandle file = null;
            ArtboardHandle artboard = null;
            StateMachineHandle stateMachine = null;
            yield return LoadStateMachine((f, a, s) => { file = f; artboard = a; stateMachine = s; });
            ViewModelInstanceHandle main = artboard.GetDefaultViewModel().Instantiate();
            main.Dispose();

            // The globals still apply; only the main instance is skipped.
            Future withGlobals = stateMachine.BindViewModelInstanceAsync(main, Globals((LabelsViewModel, Global(file, LabelsViewModel))));
            Assert.IsTrue(m_mockLogger.LoggedErrorsContains("the main instance is disposed"), "Reported at the call.");
            yield return WaitUntil(() => withGlobals.IsDone, "The bind");
            AssertFailed(withGlobals, RiveErrorCode.ResourceDisposed);
            AssertRejected(stateMachine.BindViewModelInstanceAsync(main), RiveErrorCode.ResourceDisposed);
            Assert.IsTrue(m_mockLogger.LoggedErrorsContains("the instance is disposed"), "The main-only overload rejects it too.");
        }

        [UnityTest]
        public IEnumerator BadEntries_AreSkipped_AndTheRestApply()
        {
            FileHandle file = null;
            ArtboardHandle artboard = null;
            StateMachineHandle stateMachine = null;
            yield return LoadStateMachine((f, a, s) => { file = f; artboard = a; stateMachine = s; });
            ViewModelInstanceHandle main = Main(artboard);
            ViewModelInstanceHandle uk = Global(file, LabelsViewModel, "UK");
            uk.GetStringProperty("currency").SetValue("first");
            yield return ExpectBound(stateMachine.BindViewModelInstanceAsync(main, Globals((LabelsViewModel, uk))));

            ViewModelInstanceHandle disposed = file.GetViewModel(LabelsViewModel).Instantiate();
            disposed.Dispose();
            ViewModelInstanceHandle other = Track(Track(file.InstantiateArtboard("Child")).GetDefaultViewModel().Instantiate());
            var bad = new[]
            {
                Globals((LabelsViewModel, Global(file, LabelsViewModel, "US")), ("Colours", Global(file, ColorsViewModel))),
                Globals((LabelsViewModel, null)),
                Globals((LabelsViewModel, disposed)),
            };
            var codes = new[] { RiveErrorCode.ViewModelNotFound, RiveErrorCode.ViewModelInstanceNotFound, RiveErrorCode.ResourceDisposed };
            for (int i = 0; i < bad.Length; i++)
            {
                Future bind = stateMachine.BindViewModelInstanceAsync(other, bad[i]);
                yield return WaitUntil(() => bind.IsDone, "The bind");
                AssertFailed(bind, codes[i]);
            }
            Future<string> currency = BoundGlobal(stateMachine, LabelsViewModel).GetStringProperty("currency").GetValueAsync();
            yield return WaitUntil(() => currency.IsDone, "The read");

            Assert.IsTrue(m_mockLogger.LoggedErrorsContains("'Colours' isn't a global view model"));
            Assert.IsTrue(m_mockLogger.LoggedErrorsContains("is null"));
            Assert.IsTrue(m_mockLogger.LoggedErrorsContains("is disposed"));
            Assert.AreEqual("$", currency.Result, "The good global in the first bind still applied.");
            yield return AssertMain(stateMachine, other, "The main instance still applied.");
        }
        [UnityTest]
        public IEnumerator RepeatedBadName_IsReportedEachTime()
        {
            FileHandle file = null;
            ArtboardHandle artboard = null;
            StateMachineHandle stateMachine = null;
            yield return LoadStateMachine((f, a, s) => { file = f; artboard = a; stateMachine = s; });
            ViewModelInstanceHandle main = Main(artboard);

            for (int i = 0; i < 2; i++)
            {
                stateMachine.BindViewModelInstanceAsync(main, Globals(("Colours", Global(file, ColorsViewModel))));
            }
            Assert.AreEqual(2, m_mockLogger.LoggedErrors.FindAll(e => e.Contains("'Colours' isn't a global view model")).Count);
        }

        [UnityTest]
        public IEnumerator RebindOmittingGlobal_KeepsPreviousUserInstance()
        {
            FileHandle file = null;
            ArtboardHandle artboard = null;
            StateMachineHandle stateMachine = null;
            yield return LoadStateMachine((f, a, s) => { file = f; artboard = a; stateMachine = s; });
            ViewModelInstanceHandle main = Main(artboard);
            ViewModelInstanceHandle labels = Global(file, LabelsViewModel, "UK");
            labels.GetStringProperty("currency").SetValue("kept");
            ViewModelInstanceHandle sizes = Global(file, SizesViewModel);

            yield return ExpectBound(stateMachine.BindViewModelInstanceAsync(main, Globals((LabelsViewModel, labels), (SizesViewModel, sizes))));
            yield return ExpectBound(stateMachine.BindViewModelInstanceAsync(main, Globals((SizesViewModel, sizes))));
            Future<string> currency = BoundGlobal(stateMachine, LabelsViewModel).GetStringProperty("currency").GetValueAsync();
            yield return WaitUntil(() => currency.IsDone, "The read");

            Assert.AreEqual("kept", currency.Result);
        }

        [UnityTest]
        public IEnumerator RebindOmittingGlobal_KeepsMutatedDefault()
        {
            FileHandle file = null;
            ArtboardHandle artboard = null;
            StateMachineHandle stateMachine = null;
            yield return LoadStateMachine((f, a, s) => { file = f; artboard = a; stateMachine = s; });
            ViewModelInstanceHandle main = Main(artboard);
            var mutated = new UnityEngine.Color(10f / 255f, 20f / 255f, 30f / 255f, 1f);

            yield return ExpectBound(stateMachine.BindViewModelInstanceAsync(main, Globals()));
            BoundGlobal(stateMachine, ColorsViewModel).GetColorProperty("backgroundColor").SetValue(mutated);
            yield return ExpectBound(stateMachine.BindViewModelInstanceAsync(main, Globals((LabelsViewModel, Global(file, LabelsViewModel, "UK")))));
            Future<UnityEngine.Color> background = BoundGlobal(stateMachine, ColorsViewModel).GetColorProperty("backgroundColor").GetValueAsync();
            yield return WaitUntil(() => background.IsDone, "The read");

            Assert.AreEqual((Color32)mutated, (Color32)background.Result);
        }

        [UnityTest]
        public IEnumerator CrossViewModelOverride_IsAllowed()
        {
            FileHandle file = null;
            ArtboardHandle artboard = null;
            StateMachineHandle stateMachine = null;
            yield return LoadStateMachine((f, a, s) => { file = f; artboard = a; stateMachine = s; });
            ViewModelInstanceHandle sizes = Global(file, SizesViewModel);
            sizes.GetNumberProperty("gaps").SetValue(5f);

            // Core allows an instance of one view model in another global's slot.
            yield return ExpectBound(stateMachine.BindViewModelInstanceAsync(Main(artboard), Globals((ColorsViewModel, sizes))));
            Future<float> gaps = BoundGlobal(stateMachine, ColorsViewModel).GetNumberProperty("gaps").GetValueAsync();
            yield return WaitUntil(() => gaps.IsDone, "The read");

            Assert.AreEqual(5f, gaps.Result, "The Colors slot holds the Sizes instance.");
        }

        [UnityTest]
        public IEnumerator DisposedStateMachine_FailsSoftly()
        {
            FileHandle file = null;
            ArtboardHandle artboard = null;
            StateMachineHandle stateMachine = null;
            yield return LoadStateMachine((f, a, s) => { file = f; artboard = a; stateMachine = s; });
            stateMachine.Dispose();

            AssertRejected(stateMachine.BindViewModelInstanceAsync(Main(artboard), Globals((LabelsViewModel, Global(file, LabelsViewModel)))),
                RiveErrorCode.ResourceDisposed);
            Assert.IsNull(stateMachine.GetGlobalViewModelInstance(LabelsViewModel));
            Assert.IsNull(stateMachine.GetViewModelInstance());
            Assert.IsTrue(m_mockLogger.LoggedErrorsContains("has been disposed"));
        }

        [NeedsRiveThread]
        [UnityTest]
        public IEnumerator BindAsync_NeverWaits_AndFinishesOnceRiveHasBound()
        {
            FileHandle file = null;
            ArtboardHandle artboard = null;
            StateMachineHandle stateMachine = null;
            yield return LoadStateMachine((f, a, s) => { file = f; artboard = a; stateMachine = s; });
            ViewModelInstanceHandle main = Main(artboard);
            ViewModelInstanceHandle us = Global(file, LabelsViewModel, "US");

            var waits = new List<string>();
            Future bind;
            Future<string> currency;
            ViewModelInstanceHandle bound;
            ManualResetEventSlim gate = HoldProducer();
            CommandTransport.MainThreadWaitsForTests = waits;
            try
            {
                bind = stateMachine.BindViewModelInstanceAsync(main, Globals((LabelsViewModel, us)));
                currency = BoundGlobal(stateMachine, LabelsViewModel).GetStringProperty("currency").GetValueAsync();
                bound = Track(stateMachine.GetViewModelInstance());
                Assert.IsFalse(bind.IsDone, "It can't finish before Rive has run it.");
                Assert.AreEqual(HandleStatus.Pending, bound.Status, "Nothing is confirmed before Rive has looked.");
            }
            finally
            {
                CommandTransport.MainThreadWaitsForTests = null;
                gate.Set();
            }
            CollectionAssert.IsEmpty(waits);

            yield return ExpectBound(bind);
            yield return WaitUntil(() => currency.IsDone, "The read");
            Assert.AreEqual("$", currency.Result, "A read queued after the bind sees it.");
            Future<ViewModelInstanceHandle> resolve = bound.ResolveAsync();
            yield return WaitUntil(() => resolve.IsDone, "The lookup");
            Assert.AreEqual(main.Native.Value, bound.Native.Value, "A lookup queued after the bind gets what it bound.");
        }
        [UnityTest]
        public IEnumerator PartialGlobalBind_KeepsWhatApplied_AndReadsBackTheMain()
        {
            FileHandle file = null;
            ArtboardHandle artboard = null;
            StateMachineHandle stateMachine = null;
            yield return LoadStateMachine((f, a, s) => { file = f; artboard = a; stateMachine = s; });
            ViewModelInstanceHandle main = Main(artboard);
            ViewModelInstanceHandle uk = Global(file, LabelsViewModel, "UK");
            uk.GetStringProperty("currency").SetValue("first");
            yield return ExpectBound(stateMachine.BindViewModelInstanceAsync(main, Globals((LabelsViewModel, uk))));

            // Nothing is bound on a new state machine, so its global handle refers to nothing.
            StateMachineHandle unbound = Track(Track(file.InstantiateArtboard("Main")).InstantiateStateMachine());
            ViewModelInstanceHandle provisional = BoundGlobal(unbound, LabelsViewModel);
            ViewModelInstanceHandle sizes = Global(file, SizesViewModel);
            sizes.GetNumberProperty("gaps").SetValue(99f);
            ViewModelInstanceHandle newMain = Main(artboard);
            Future bind = stateMachine.BindViewModelInstanceAsync(newMain,
                Globals((SizesViewModel, sizes), (LabelsViewModel, provisional)));
            yield return WaitUntil(() => bind.IsDone, "The bind");

            AssertFailed(bind, RiveErrorCode.ViewModelInstanceNotFound);
            Assert.AreSame(provisional.Error, bind.Exception, "It fails with the global's own lookup error.");
            Assert.AreEqual(1, m_mockLogger.LoggedWarnings.FindAll(w => w.Contains("No instance is bound for global 'Labels'")).Count,
                "The missing global is reported once, by its lookup.");
            yield return AssertGlobals(stateMachine, 99f, "first");
            yield return AssertMain(stateMachine, newMain, "The main instance applied, so that's what's read back.");
        }
        [UnityTest]
        public IEnumerator ReleasedGlobal_IsSkipped_AndTheOthersStay()
        {
            FileHandle file = null;
            ArtboardHandle artboard = null;
            StateMachineHandle stateMachine = null;
            yield return LoadStateMachine((f, a, s) => { file = f; artboard = a; stateMachine = s; });
            ViewModelInstanceHandle main = Main(artboard);
            ViewModelInstanceHandle sizes = Global(file, SizesViewModel);
            sizes.GetNumberProperty("gaps").SetValue(5f);
            ViewModelInstanceHandle uk = Global(file, LabelsViewModel, "UK");
            uk.GetStringProperty("currency").SetValue("first");
            yield return ExpectBound(stateMachine.BindViewModelInstanceAsync(main, Globals((SizesViewModel, sizes), (LabelsViewModel, uk))));

            // Released behind the handle's back, so Rive only finds out when it
            // tries to set it, after Sizes is set.
            ViewModelInstanceHandle released = Global(file, LabelsViewModel, "US");
            ViewModelInstanceNative.ReleaseLater(released.Native);
            ViewModelInstanceHandle otherSizes = Global(file, SizesViewModel);
            otherSizes.GetNumberProperty("gaps").SetValue(99f);
            ViewModelInstanceHandle newMain = Main(artboard);
            Future bind = stateMachine.BindViewModelInstanceAsync(newMain,
                Globals((SizesViewModel, otherSizes), (LabelsViewModel, released)));
            yield return WaitUntil(() => bind.IsDone, "The bind");

            AssertFailed(bind, RiveErrorCode.ViewModelInstanceNotFound);
            StringAssert.Contains("couldn't set global 'Labels'", bind.Exception.Message);
            yield return AssertGlobals(stateMachine, 99f, "first");
            yield return AssertMain(stateMachine, newMain);
        }
        [UnityTest]
        public IEnumerator ReleasedMain_IsSkipped_AndTheGlobalsStay()
        {
            FileHandle file = null;
            ArtboardHandle artboard = null;
            StateMachineHandle stateMachine = null;
            yield return LoadStateMachine((f, a, s) => { file = f; artboard = a; stateMachine = s; });
            ViewModelInstanceHandle main = Main(artboard);
            ViewModelInstanceHandle uk = Global(file, LabelsViewModel, "UK");
            uk.GetStringProperty("currency").SetValue("first");
            yield return ExpectBound(stateMachine.BindViewModelInstanceAsync(main, Globals((LabelsViewModel, uk))));

            ViewModelInstanceHandle releasedMain = Main(artboard);
            ViewModelInstanceNative.ReleaseLater(releasedMain.Native);
            Future bind = stateMachine.BindViewModelInstanceAsync(releasedMain, Globals((LabelsViewModel, Global(file, LabelsViewModel, "US"))));
            yield return WaitUntil(() => bind.IsDone, "The bind");

            AssertFailed(bind, RiveErrorCode.ViewModelInstanceNotFound);
            StringAssert.Contains("couldn't set the main instance", bind.Exception.Message);
            yield return AssertGlobals(stateMachine, 16f, "$");
            yield return AssertMain(stateMachine, main, "The main instance wasn't replaced, and the read-back says so.");
        }
        [UnityTest]
        public IEnumerator InvalidName_IsReportedAtTheCall_AndTheRestApply()
        {
            FileHandle file = null;
            ArtboardHandle artboard = null;
            StateMachineHandle stateMachine = null;
            yield return LoadStateMachine((f, a, s) => { file = f; artboard = a; stateMachine = s; });
            ViewModelInstanceHandle main = Main(artboard);
            ViewModelInstanceHandle uk = Global(file, LabelsViewModel, "UK");
            uk.GetStringProperty("currency").SetValue("first");
            yield return ExpectBound(stateMachine.BindViewModelInstanceAsync(main, Globals((LabelsViewModel, uk))));

            ViewModelInstanceHandle other = Main(artboard);
            Future bind = stateMachine.BindViewModelInstanceAsync(other,
                Globals((LabelsViewModel, Global(file, LabelsViewModel, "US")), ("Colours", Global(file, ColorsViewModel))));
            Assert.IsTrue(m_mockLogger.LoggedErrorsContains("'Colours' isn't a global view model"), "Reported at the call.");
            yield return WaitUntil(() => bind.IsDone, "The bind");

            AssertFailed(bind, RiveErrorCode.ViewModelNotFound);
            yield return AssertGlobals(stateMachine, 16f, "$");
            yield return AssertMain(stateMachine, other);
        }
        private IEnumerator AssertGlobals(StateMachineHandle stateMachine, float gaps, string currency)
        {
            Future<float> readGaps = BoundGlobal(stateMachine, SizesViewModel).GetNumberProperty("gaps").GetValueAsync();
            Future<string> readCurrency = BoundGlobal(stateMachine, LabelsViewModel).GetStringProperty("currency").GetValueAsync();
            yield return WaitUntil(() => readGaps.IsDone && readCurrency.IsDone, "The reads");
            Assert.AreEqual(gaps, readGaps.Result, "Sizes' gaps");
            Assert.AreEqual(currency, readCurrency.Result, "Labels' currency");
        }

        // Holds the producer until the returned gate is set.
        private static ManualResetEventSlim HoldProducer()
        {
            var started = new ManualResetEventSlim(false);
            var gate = new ManualResetEventSlim(false);
            ServerGate.Hold(started, gate);
            Assert.IsTrue(started.Wait(2000), "The producer should have picked the job up.");
            return gate;
        }

        [UnityTest]
        public IEnumerator RepeatedBindsAndReads_DoNotLeakNativeReferences()
        {
            FileHandle file = null;
            ArtboardHandle artboard = null;
            StateMachineHandle stateMachine = null;
            yield return LoadStateMachine((f, a, s) => { file = f; artboard = a; stateMachine = s; });
            ViewModelInstanceHandle main = Main(artboard);
            ViewModelInstanceHandle labels = Global(file, LabelsViewModel);

            stateMachine.BindViewModelInstanceAsync(main, Globals((LabelsViewModel, labels)));
            int mainRefs = ViewModelInstanceNative.RefCountForTests(main.Native);
            int labelRefs = ViewModelInstanceNative.RefCountForTests(labels.Native);
            for (int i = 0; i < 5; i++)
            {
                stateMachine.BindViewModelInstanceAsync(main, Globals((LabelsViewModel, labels)));
                stateMachine.GetGlobalViewModelInstance(LabelsViewModel).Dispose();
            }

            Assert.AreEqual(mainRefs, ViewModelInstanceNative.RefCountForTests(main.Native), "Rebinding shouldn't leak the main instance.");
            Assert.AreEqual(labelRefs, ViewModelInstanceNative.RefCountForTests(labels.Native), "Rebinding and reading shouldn't leak the global.");
        }

        [UnityTest]
        public IEnumerator DisposingTheStateMachine_ReleasesItsReferences()
        {
            FileHandle file = null;
            ArtboardHandle artboard = null;
            StateMachineHandle stateMachine = null;
            yield return LoadStateMachine((f, a, s) => { file = f; artboard = a; stateMachine = s; });
            ViewModelInstanceHandle main = Main(artboard);
            ViewModelInstanceHandle labels = Global(file, LabelsViewModel);
            int mainBefore = ViewModelInstanceNative.RefCountForTests(main.Native);
            int labelsBefore = ViewModelInstanceNative.RefCountForTests(labels.Native);

            stateMachine.BindViewModelInstanceAsync(main, Globals((LabelsViewModel, labels)));
            Assert.Greater(ViewModelInstanceNative.RefCountForTests(main.Native), mainBefore, "The state machine holds the main instance.");
            stateMachine.Dispose();
            artboard.Dispose();

            Assert.AreEqual(mainBefore, ViewModelInstanceNative.RefCountForTests(main.Native));
            Assert.AreEqual(labelsBefore, ViewModelInstanceNative.RefCountForTests(labels.Native));
        }

        [UnityTest]
        public IEnumerator BackgroundWidget_GivesItsGlobals()
        {
            Asset asset = null;
            yield return m_assetLoader.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_global_variables_test, a => asset = a, () => Assert.Fail("Failed to load the test asset"));
            RivePanel panel = RivePanelTestUtils.CreatePanel();
            m_objects.Add(panel.gameObject);
            panel.ThreadingMode = ThreadingMode.BackgroundThread;
            RiveWidget widget = RivePanelTestUtils.CreateWidget<RiveWidget>();
            widget.transform.SetParent(panel.WidgetContainer, false);
            RivePanelTestUtils.MakeWidgetFillPanel(widget);

            widget.Load(asset, artboardName: "Main", stateMachineName: null);
            yield return WaitUntil(() => widget.Status == WidgetStatus.Loaded, "The load");
            Future<float> gaps = BoundGlobal(widget.StateMachineHandle, SizesViewModel).GetNumberProperty("gaps").GetValueAsync();
            yield return WaitUntil(() => gaps.IsDone, "The read");

            Assert.AreEqual(16f, gaps.Result, "The load fills in the globals.");
        }
    }
}
