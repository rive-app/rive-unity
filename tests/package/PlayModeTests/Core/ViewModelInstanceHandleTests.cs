using System;
using System.Collections;
using System.Collections.Generic;
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
    /// ViewModelHandle, ViewModelInstanceHandle and the path-based property
    /// handles: nothing waits, reads and callbacks come back in order, and
    /// missing paths show up as warnings and failed reads.
    /// </summary>
    public class ViewModelInstanceHandleTests
    {
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

        private IEnumerator LoadFile(string path, Action<FileHandle> onLoaded)
        {
            Asset asset = null;
            yield return m_assetLoader.LoadAssetCoroutine<Asset>(
                path, a => asset = a, () => Assert.Fail($"Failed to load {path}"));
            Future<FileHandle> load = FileHandle.LoadAsync(asset);
            yield return WaitFor(load);
            Assert.AreEqual(FutureStatus.Succeeded, load.Status, "The load should succeed.");
            onLoaded(Track(load.Result));
        }

        private static IEnumerator WaitFor<T>(Future<T> operation)
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!operation.IsDone)
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, "It didn't finish within 10 seconds.");
                yield return null;
            }
        }

        private static IEnumerator WaitUntil(Func<bool> condition, string what)
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!condition())
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, $"{what} didn't happen within 10 seconds.");
                yield return null;
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // A state machine only sees the last value written before it advances.
        [NeedsRiveThread]
        [UnityTest]
        public IEnumerator BoolSetAndSetBack_BeforeAnAdvance_WarnsOnce()
        {
            FileHandle file = null;
            yield return LoadFile(TestAssetReferences.riv_asset_databinding_test, f => file = f);
            ViewModelInstanceHandle person = Track(file.GetViewModel("PersonViewModel").Instantiate());
            BooleanPropertyHandle agreed = person.GetBooleanProperty("agreedToTerms");

            agreed.SetValue(true);
            agreed.SetValue(false);
            agreed.SetValue(true);

            Assert.AreEqual(1, m_mockLogger.LoggedWarnings.FindAll(w => w.Contains("agreedToTerms")).Count,
                "Setting a bool back before an advance should warn once.");
        }

        [NeedsRiveThread]
        [UnityTest]
        public IEnumerator BoolSetAcrossAnAdvance_DoesNotWarn()
        {
            FileHandle file = null;
            yield return LoadFile(TestAssetReferences.riv_asset_databinding_test, f => file = f);
            ViewModelInstanceHandle person = Track(file.GetViewModel("PersonViewModel").Instantiate());
            BooleanPropertyHandle agreed = person.GetBooleanProperty("agreedToTerms");

            agreed.SetValue(true);
            // Stands in for an advance going out between the writes.
            UnseenBoolCheck.Advances++;
            agreed.SetValue(false);

            Assert.IsFalse(m_mockLogger.LoggedWarningsContains("agreedToTerms"),
                "A value an advance has seen shouldn't warn when it changes.");
        }
#endif

        [NeedsRiveThread]
        [UnityTest]
        public IEnumerator SetAndGet_RoundTrip_WithoutWaiting()
        {
            FileHandle file = null;
            yield return LoadFile(TestAssetReferences.riv_asset_databinding_test, f => file = f);

            var waits = new List<string>();
            CommandTransport.MainThreadWaitsForTests = waits;
            Future<string> name;
            Future<float> age;
            Future<bool> agreed;
            Future<UnityEngine.Color> color;
            Future<string> country;
            Future<string> drink;
            try
            {
                ViewModelInstanceHandle person = Track(file.GetViewModel("PersonViewModel").Instantiate());
                person.GetStringProperty("name").SetValue("Test User");
                person.GetNumberProperty("age").SetValue(42f);
                person.GetBooleanProperty("agreedToTerms").SetValue(true);
                person.GetColorProperty("favColor").SetValue(new UnityEngine.Color(1f, 0f, 0f, 1f));
                person.GetEnumProperty("country").SetValue("japan");
                person.GetEnumProperty("favDrink/type").SetValue("Coffee");

                name = person.GetStringProperty("name").GetValueAsync();
                age = person.GetNumberProperty("age").GetValueAsync();
                agreed = person.GetBooleanProperty("agreedToTerms").GetValueAsync();
                color = person.GetColorProperty("favColor").GetValueAsync();
                country = person.GetEnumProperty("country").GetValueAsync();
                drink = person.GetEnumProperty("favDrink/type").GetValueAsync();
            }
            finally
            {
                CommandTransport.MainThreadWaitsForTests = null;
            }
            CollectionAssert.IsEmpty(waits, "Nothing on a view model handle should wait.");

            yield return WaitFor(drink);
            Assert.AreEqual("Test User", name.Result);
            Assert.AreEqual(42f, age.Result);
            Assert.IsTrue(agreed.Result);
            Assert.AreEqual(new UnityEngine.Color(1f, 0f, 0f, 1f), color.Result);
            Assert.AreEqual("japan", country.Result);
            Assert.AreEqual("Coffee", drink.Result, "A nested path should reach the nested view model.");
        }

        // Rive's strings are UTF-8. On Windows the default marshalling is the
        // system code page, which is what this catches.
        [UnityTest]
        public IEnumerator NonAsciiStrings_RoundTrip()
        {
            const string text = "Café ✓ 日本語 🎉";
            FileHandle file = null;
            yield return LoadFile(TestAssetReferences.riv_asset_databinding_test, f => file = f);

            ViewModelInstanceHandle handle = Track(file.GetViewModel("PersonViewModel").Instantiate());
            handle.GetStringProperty("name").SetValue(text);
            Future<string> read = handle.GetStringProperty("name").GetValueAsync();
            yield return WaitFor(read);
            Assert.AreEqual(text, read.Result, "Through a handle");

            using (ViewModelInstance plain = file.File.GetViewModelByName("PersonViewModel").CreateInstance())
            {
                ViewModelInstanceStringProperty name = plain.GetStringProperty("name");
                name.Value = text;
                Assert.AreEqual(text, name.Value, "Through a plain property");
            }
        }

        [UnityTest]
        public IEnumerator SamePath_GivesTheSameHandle()
        {
            FileHandle file = null;
            yield return LoadFile(TestAssetReferences.riv_asset_databinding_test, f => file = f);
            ViewModelInstanceHandle person = Track(file.GetViewModel("PersonViewModel").Instantiate());

            Assert.AreSame(person.GetNumberProperty("age"), person.GetNumberProperty("age"));
            Assert.AreEqual("PersonViewModel", person.GetViewModelNameAsync().Result);
        }

        [UnityTest]
        public IEnumerator NamedInstance_HasItsValues()
        {
            FileHandle file = null;
            yield return LoadFile(TestAssetReferences.riv_asset_databinding_test, f => file = f);
            string expected;
            using (ViewModelInstance jane = file.File.GetViewModelByName("PersonViewModel").CreateInstanceByName("Jane"))
            {
                expected = jane.GetStringProperty("name").Value;
            }

            ViewModelInstanceHandle handle = Track(file.GetViewModel("PersonViewModel").Instantiate("Jane"));
            Future<string> name = handle.GetStringProperty("name").GetValueAsync();
            yield return WaitFor(name);

            Assert.AreEqual(expected, name.Result);
        }

        [UnityTest]
        public IEnumerator UnknownNames_ReturnNullWithAWarning()
        {
            FileHandle file = null;
            yield return LoadFile(TestAssetReferences.riv_asset_databinding_test, f => file = f);

            Assert.IsNull(file.GetViewModel("No such view model"));
            Assert.IsTrue(m_mockLogger.LoggedWarningsContains("no view model named 'No such view model'"));

            Assert.IsNull(file.GetViewModel("PersonViewModel").Instantiate("Nobody"));
            Assert.IsTrue(m_mockLogger.LoggedWarningsContains("no instance named 'Nobody'"));
        }

        [UnityTest]
        public IEnumerator MissingPath_WarnsAndFailsReads()
        {
            FileHandle file = null;
            yield return LoadFile(TestAssetReferences.riv_asset_databinding_test, f => file = f);
            ViewModelInstanceHandle person = Track(file.GetViewModel("PersonViewModel").Instantiate());

            person.GetNumberProperty("missing").SetValue(1f);
            Future<float> missing = person.GetNumberProperty("missing").GetValueAsync();
            // A property of another type counts as missing too.
            Future<float> wrongType = person.GetNumberProperty("name").GetValueAsync();
            yield return WaitFor(wrongType);
            yield return null;

            Assert.AreEqual(FutureStatus.Failed, missing.Status);
            Assert.AreEqual(RiveErrorCode.PropertyNotFound, ((RiveException)missing.Exception).Code);
            Assert.AreEqual(FutureStatus.Failed, wrongType.Status);
            Assert.IsTrue(m_mockLogger.LoggedWarningsContains("There's no property at 'missing'"),
                "A missing path should be reported once Rive gets to it.");
        }

        [UnityTest]
        public IEnumerator Subscribe_FiresAfterAHandleAdvance()
        {
            FileHandle file = null;
            yield return LoadFile(TestAssetReferences.riv_asset_databinding_test, f => file = f);
            ArtboardHandle artboard = Track(file.InstantiateArtboard());
            StateMachineHandle stateMachine = Track(artboard.InstantiateStateMachine());
            stateMachine.BindViewModelInstanceAsync(null);
            ViewModelInstanceHandle person = Track(stateMachine.GetViewModelInstance());
            Future<string> viewModel = person.GetViewModelNameAsync();
            yield return WaitUntil(() => viewModel.IsDone, "The read");
            Assert.AreEqual("PersonViewModel", viewModel.Result);

            var ages = new List<float>();
            int submits = 0;
            NumberPropertyHandle age = person.GetNumberProperty("age");
            TriggerPropertyHandle submit = person.GetTriggerProperty("onFormSubmit");
            PropertySubscription onAge = age.Subscribe(ages.Add);
            PropertySubscription onSubmit = submit.Subscribe(() => submits++);

            age.SetValue(33f);
            submit.Fire();
            stateMachine.Advance(1f / 60f);
            yield return WaitUntil(() => ages.Count > 0 && submits > 0, "The callbacks");

            Assert.AreEqual(33f, ages[ages.Count - 1]);
            Assert.AreEqual(1, submits);

            onAge.Dispose();
            onSubmit.Dispose();
            int agesBefore = ages.Count;
            age.SetValue(34f);
            submit.Fire();
            stateMachine.Advance(1f / 60f);
            Future<float> read = age.GetValueAsync();
            yield return WaitFor(read);

            Assert.AreEqual(34f, read.Result);
            Assert.AreEqual(agesBefore, ages.Count, "Unsubscribed callbacks shouldn't fire.");
            Assert.AreEqual(1, submits);
        }

        [UnityTest]
        public IEnumerator GetViewModelInstance_CapturesWhatWasBound_AtItsPlaceInTheQueue()
        {
            FileHandle file = null;
            yield return LoadFile(TestAssetReferences.riv_asset_databinding_test, f => file = f);
            StateMachineHandle stateMachine = Track(Track(file.InstantiateArtboard()).InstantiateStateMachine());
            ViewModelInstanceHandle jane = Track(file.GetViewModel("PersonViewModel").Instantiate("Jane"));

            ViewModelInstanceHandle beforeAnyBind = Track(stateMachine.GetViewModelInstance());
            Future bindDefault = stateMachine.BindViewModelInstanceAsync(null);
            ViewModelInstanceHandle afterDefault = Track(stateMachine.GetViewModelInstance());
            Future bindJane = stateMachine.BindViewModelInstanceAsync(jane);
            ViewModelInstanceHandle afterJane = Track(stateMachine.GetViewModelInstance());
            Assert.AreEqual(HandleStatus.Pending, afterDefault.Status, "Nothing is confirmed before Rive has looked.");

            Future<ViewModelInstanceHandle> first = beforeAnyBind.ResolveAsync();
            Future<ViewModelInstanceHandle> second = afterDefault.ResolveAsync();
            Future<ViewModelInstanceHandle> third = afterJane.ResolveAsync();
            yield return WaitUntil(() => first.IsDone && second.IsDone && third.IsDone && bindDefault.IsDone && bindJane.IsDone, "The lookups");

            Assert.AreEqual(FutureStatus.Succeeded, bindDefault.Status);
            Assert.AreEqual(FutureStatus.Succeeded, bindJane.Status);
            Assert.AreEqual(HandleStatus.Invalid, beforeAnyBind.Status, "Nothing was bound when it ran.");
            Assert.AreEqual(RiveErrorCode.ViewModelInstanceNotFound, beforeAnyBind.Error.Code);
            Assert.AreSame(afterJane, third.Result, "ResolveAsync gives back the same handle.");
            Assert.AreEqual(jane.Native.Value, afterJane.Native.Value);

            // It keeps the instance it found, not whatever is bound now.
            Assert.AreNotEqual(jane.Native.Value, afterDefault.Native.Value);
            Future<string> name = afterDefault.GetNameAsync();
            yield return WaitUntil(() => name.IsDone, "The read");
            Assert.AreNotEqual("Jane", name.Result);
        }
        [UnityTest]
        public IEnumerator Dispose_DropsSubscriptions()
        {
            FileHandle file = null;
            yield return LoadFile(TestAssetReferences.riv_asset_databinding_test, f => file = f);
            ArtboardHandle artboard = Track(file.InstantiateArtboard());
            StateMachineHandle stateMachine = Track(artboard.InstantiateStateMachine());
            stateMachine.BindViewModelInstanceAsync(null);
            ViewModelInstanceHandle person = stateMachine.GetViewModelInstance();

            int calls = 0;
            NumberPropertyHandle age = person.GetNumberProperty("age");
            age.Subscribe(_ => calls++);
            age.SetValue(50f);
            person.Dispose();
            stateMachine.Advance(1f / 60f);
            for (int i = 0; i < 5; i++)
            {
                yield return null;
            }

            Assert.IsTrue(person.IsDisposed);
            Assert.AreEqual(0, calls, "A disposed instance's callbacks shouldn't fire.");
            Future<float> read = age.GetValueAsync();
            Assert.AreEqual(FutureStatus.Failed, read.Status);
            Assert.AreEqual(RiveErrorCode.ResourceDisposed, ((RiveException)read.Exception).Code);
        }

        [NeedsRiveThread]
        [UnityTest]
        public IEnumerator List_ChangesApplyInOrder()
        {
            FileHandle file = null;
            yield return LoadFile(TestAssetReferences.riv_db_list_test, f => file = f);
            ArtboardHandle artboard = Track(file.InstantiateArtboard());
            ViewModelHandle rootViewModel = artboard.GetDefaultViewModel();
            Assert.IsNotNull(rootViewModel, "The list test artboard has a default view model.");
            ViewModelInstanceHandle root = Track(rootViewModel.Instantiate());
            ListPropertyHandle items = root.GetListProperty("items");
            ViewModelHandle todo = file.GetViewModel("TodoItem");

            Future<int> before = items.GetCountAsync();
            yield return WaitFor(before);
            int count = before.Result;

            var waits = new List<string>();
            CommandTransport.MainThreadWaitsForTests = waits;
            Future<int> afterAdd;
            Future<int> afterRemove;
            Future<int> afterClear;
            try
            {
                ViewModelInstanceHandle first = Track(todo.InstantiateBlank());
                ViewModelInstanceHandle second = Track(todo.InstantiateBlank());
                items.Add(first);
                items.Insert(0, second);
                items.Add(first);
                afterAdd = items.GetCountAsync();
                items.Remove(first);
                afterRemove = items.GetCountAsync();
                items.Clear();
                afterClear = items.GetCountAsync();
            }
            finally
            {
                CommandTransport.MainThreadWaitsForTests = null;
            }
            CollectionAssert.IsEmpty(waits, "List changes shouldn't wait.");

            yield return WaitFor(afterClear);
            Assert.AreEqual(count + 3, afterAdd.Result);
            Assert.AreEqual(count + 1, afterRemove.Result, "Remove takes every occurrence.");
            Assert.AreEqual(0, afterClear.Result);
        }

        [UnityTest]
        public IEnumerator List_GetInstanceAt_ReachesTheItem()
        {
            FileHandle file = null;
            yield return LoadFile(TestAssetReferences.riv_db_list_test, f => file = f);
            ArtboardHandle artboard = Track(file.InstantiateArtboard());
            ViewModelInstanceHandle root = Track(artboard.GetDefaultViewModel().Instantiate());
            ListPropertyHandle items = root.GetListProperty("items");
            Future<int> before = items.GetCountAsync();
            yield return WaitFor(before);

            ViewModelInstanceHandle item = Track(file.GetViewModel("TodoItem").InstantiateBlank());
            item.GetStringProperty("text").SetValue("Buy milk");
            items.Add(item);
            ViewModelInstanceHandle atEnd = Track(items.GetInstanceAt(before.Result));
            Future<string> text = atEnd.GetStringProperty("text").GetValueAsync();
            yield return WaitFor(text);

            Assert.AreEqual("Buy milk", text.Result);
        }

        [UnityTest]
        public IEnumerator NestedInstance_AndPath_ReachTheSameInstance()
        {
            FileHandle file = null;
            yield return LoadFile(TestAssetReferences.riv_asset_databinding_test, f => file = f);
            ViewModelInstanceHandle person = Track(file.GetViewModel("PersonViewModel").Instantiate());

            ViewModelInstanceHandle drink = Track(person.GetViewModelInstanceProperty("favDrink"));
            drink.GetEnumProperty("type").SetValue("Tea");
            Future<string> viaPath = person.GetEnumProperty("favDrink/type").GetValueAsync();
            yield return WaitFor(viaPath);

            Assert.AreEqual("Tea", viaPath.Result, "The nested handle and the path should reach the same instance.");
        }

        [UnityTest]
        public IEnumerator TwoHandles_OnOneProperty_BothSeeTheChange()
        {
            FileHandle file = null;
            yield return LoadFile(TestAssetReferences.riv_asset_databinding_test, f => file = f);
            StateMachineHandle stateMachine = Track(Track(file.InstantiateArtboard()).InstantiateStateMachine());
            stateMachine.BindViewModelInstanceAsync(null);
            ViewModelInstanceHandle person = Track(stateMachine.GetViewModelInstance());
            ViewModelInstanceHandle drink = Track(person.GetViewModelInstanceProperty("favDrink"));

            var viaPath = new List<string>();
            var viaNested = new List<string>();
            person.GetEnumProperty("favDrink/type").Subscribe(viaPath.Add);
            drink.GetEnumProperty("type").Subscribe(viaNested.Add);
            drink.GetEnumProperty("type").SetValue("Coffee");
            stateMachine.Advance(1f / 60f);
            yield return WaitUntil(() => viaPath.Count > 0 && viaNested.Count > 0, "Both callbacks");

            Assert.AreEqual("Coffee", viaPath[viaPath.Count - 1]);
            Assert.AreEqual("Coffee", viaNested[viaNested.Count - 1]);
        }

        [UnityTest]
        public IEnumerator PlainPropertyAndHandle_OnOneProperty_BothSeeTheChange()
        {
            Asset asset = null;
            yield return m_assetLoader.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_asset_databinding_test, a => asset = a, () => Assert.Fail("Failed to load the test asset"));
            RivePanel panel = RivePanelTestUtils.CreatePanel();
            m_objects.Add(panel.gameObject);
            panel.ThreadingMode = ThreadingMode.BackgroundThread;
            RiveWidget widget = RivePanelTestUtils.CreateWidget<RiveWidget>();
            widget.transform.SetParent(panel.WidgetContainer, false);
            RivePanelTestUtils.MakeWidgetFillPanel(widget);
            widget.Load(asset);
            yield return WaitUntil(() => widget.Status == WidgetStatus.Loaded, "The load");

            // The widget keeps the plain instance internally, so this is the
            // same property through both APIs.
            var plain = widget.LoadedStateMachine.ViewModelInstance.GetNumberProperty("age");
            NumberPropertyHandle handle = widget.StateMachineHandle.GetViewModelInstance().GetNumberProperty("age");
            var viaPlain = new List<float>();
            var viaHandle = new List<float>();
            Action<float> onPlain = viaPlain.Add;
            plain.OnValueChanged += onPlain;
            PropertySubscription onHandle = handle.Subscribe(viaHandle.Add);

            handle.SetValue(77f);
            yield return WaitUntil(() => viaPlain.Count > 0 && viaHandle.Count > 0, "Both callbacks");
            plain.OnValueChanged -= onPlain;
            onHandle.Dispose();

            Assert.AreEqual(77f, viaPlain[viaPlain.Count - 1]);
            Assert.AreEqual(77f, viaHandle[viaHandle.Count - 1]);
        }

        [UnityTest]
        public IEnumerator PlainSubscribing_DoesNotTakeAChangeFromAHandle()
        {
            Asset asset = null;
            yield return m_assetLoader.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_asset_databinding_test, a => asset = a, () => Assert.Fail("Failed to load the test asset"));
            RivePanel panel = RivePanelTestUtils.CreatePanel();
            m_objects.Add(panel.gameObject);
            panel.ThreadingMode = ThreadingMode.BackgroundThread;
            RiveWidget widget = RivePanelTestUtils.CreateWidget<RiveWidget>();
            widget.transform.SetParent(panel.WidgetContainer, false);
            RivePanelTestUtils.MakeWidgetFillPanel(widget);
            widget.Load(asset);
            yield return WaitUntil(() => widget.Status == WidgetStatus.Loaded, "The load");

            NumberPropertyHandle handle = widget.StateMachineHandle.GetViewModelInstance().GetNumberProperty("age");
            var viaHandle = new List<float>();
            var viaPlain = new List<float>();
            Action<float> onPlain = viaPlain.Add;
            PropertySubscription onHandle = handle.Subscribe(viaHandle.Add);

            handle.SetValue(55f);
            // A plain property's first listener skips earlier changes. That
            // used to clear the shared flag, so the handle lost this one.
            var plain = widget.LoadedStateMachine.ViewModelInstance.GetNumberProperty("age");
            plain.OnValueChanged += onPlain;
            yield return WaitUntil(() => viaHandle.Count > 0, "The handle's callback");
            for (int i = 0; i < 3; i++)
            {
                yield return null;
            }
            onHandle.Dispose();
            plain.OnValueChanged -= onPlain;

            Assert.AreEqual(55f, viaHandle[viaHandle.Count - 1]);
            CollectionAssert.IsEmpty(viaPlain, "The plain listener came after the change, so it shouldn't see it.");
        }

        [UnityTest]
        public IEnumerator BackgroundWidget_GivesItsBoundInstance()
        {
            Asset asset = null;
            yield return m_assetLoader.LoadAssetCoroutine<Asset>(
                TestAssetReferences.riv_asset_databinding_test, a => asset = a, () => Assert.Fail("Failed to load the test asset"));
            RivePanel panel = RivePanelTestUtils.CreatePanel();
            m_objects.Add(panel.gameObject);
            panel.ThreadingMode = ThreadingMode.BackgroundThread;
            RiveWidget widget = RivePanelTestUtils.CreateWidget<RiveWidget>();
            widget.transform.SetParent(panel.WidgetContainer, false);
            RivePanelTestUtils.MakeWidgetFillPanel(widget);

            widget.Load(asset);
            yield return WaitUntil(() => widget.Status == WidgetStatus.Loaded, "The load");

            ViewModelInstanceHandle person = widget.StateMachineHandle.GetViewModelInstance();
            Future<string> viewModel = person.GetViewModelNameAsync();
            yield return WaitUntil(() => viewModel.IsDone, "The read");
            Assert.AreEqual("PersonViewModel", viewModel.Result);

            var names = new List<string>();
            StringPropertyHandle name = person.GetStringProperty("name");
            PropertySubscription onName = name.Subscribe(names.Add);
            name.SetValue("From a handle");
            yield return WaitUntil(() => names.Count > 0, "The panel's capture");
            onName.Dispose();

            Assert.AreEqual("From a handle", names[names.Count - 1]);

            // A lookup's handle is yours: disposing it lets go of your hold, not the binding.
            person.Dispose();
            Assert.AreEqual(HandleStatus.Disposed, person.Status);
            ViewModelInstanceHandle again = widget.StateMachineHandle.GetViewModelInstance();
            Future<ViewModelInstanceHandle> resolve = again.ResolveAsync();
            yield return WaitUntil(() => resolve.IsDone, "The lookup");
            Assert.AreEqual(FutureStatus.Succeeded, resolve.Status, "The widget still has it bound.");
            again.Dispose();
        }
    }
}
