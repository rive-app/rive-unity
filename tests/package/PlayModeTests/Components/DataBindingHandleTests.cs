using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Rive.Components;
using Rive.Tests.Utils;
using Rive.Utils;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rive.Tests
{
    /// <summary>
    /// Data binding through handles in a BackgroundThread panel, mirroring <see cref="DataBindingTests"/>
    /// for the behaviour both families share. The plain-only parts (property object caches, the
    /// callbacks hub internals, HandleCallbacks) have no handle equivalent.
    /// </summary>
    public class DataBindingHandleTests
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

        private IEnumerator LoadAsset(string path, Action<Asset> onLoaded)
        {
            return m_assetLoader.LoadAssetCoroutine<Asset>(path, onLoaded, () => Assert.Fail($"Failed to load {path}"));
        }

        /// A BackgroundThread panel with one widget, loaded and bound as configured.
        private IEnumerator LoadWidget(string path, Action<RiveWidget> onLoaded, Action<RivePanel, RiveWidget> configure = null)
        {
            Asset asset = null;
            yield return LoadAsset(path, a => asset = a);
            RivePanel panel = RivePanelTestUtils.CreatePanel();
            m_objects.Add(panel.gameObject);
            panel.ThreadingMode = ThreadingMode.BackgroundThread;
            RiveWidget widget = RivePanelTestUtils.CreateWidget<RiveWidget>();
            widget.transform.SetParent(panel.WidgetContainer, false);
            RivePanelTestUtils.MakeWidgetFillPanel(widget);
            configure?.Invoke(panel, widget);
            widget.Load(asset);
            yield return WaitUntil(() => widget.Status == WidgetStatus.Loaded || widget.Status == WidgetStatus.Error, "The load");
            onLoaded(widget);
        }

        private IEnumerator LoadPerson(Action<RiveWidget> onLoaded) =>
            LoadWidget(TestAssetReferences.riv_asset_databinding_test, onLoaded);

        private IEnumerator LoadList(Action<RiveWidget> onLoaded) =>
            LoadWidget(TestAssetReferences.riv_db_list_test, onLoaded);

        // The widget's bound instance, looked up after what's queued so far.
        private ViewModelInstanceHandle Bound(RiveWidget widget) => Track(widget.StateMachineHandle.GetViewModelInstance());

        // Resolves the handle and checks it failed with the code, and stays invalid.
        private static IEnumerator ExpectInvalid(ViewModelInstanceHandle handle, RiveErrorCode code)
        {
            Future<ViewModelInstanceHandle> resolve = handle.ResolveAsync();
            yield return WaitUntil(() => resolve.IsDone, "The lookup");
            Assert.AreEqual(FutureStatus.Failed, resolve.Status);
            Assert.AreEqual(HandleStatus.Invalid, handle.Status);
            Assert.AreEqual(code, handle.Error.Code, handle.Error.Message);
            Assert.AreSame(handle.Error, resolve.Exception);
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

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        private static IEnumerator Await<T>(Future<T> read)
        {
            yield return WaitUntil(() => read.IsDone, "The read");
            Assert.AreEqual(FutureStatus.Succeeded, read.Status, read.Exception?.Message);
        }

        // ---- Binding modes ----

        [UnityTest]
        public IEnumerator AutoBindDefault_BindsDefaultInstance()
        {
            RiveWidget widget = null;
            yield return LoadPerson(w => widget = w);

            ViewModelInstanceHandle bound = Bound(widget);
            Assert.AreEqual(HandleStatus.Pending, bound.Status, "Nothing is confirmed until Rive has looked.");
            Future<string> viewModel = bound.GetViewModelNameAsync();
            yield return Await(viewModel);

            Assert.AreEqual("PersonViewModel", viewModel.Result);
            Assert.AreEqual(HandleStatus.Valid, bound.Status);
        }
        [UnityTest]
        public IEnumerator Manual_DoesNotAutoBind()
        {
            RiveWidget widget = null;
            yield return LoadWidget(TestAssetReferences.riv_asset_databinding_test, w => widget = w,
                (p, w) => w.BindingMode = RiveWidget.DataBindingMode.Manual);

            Assert.AreEqual(WidgetStatus.Loaded, widget.Status);
            yield return ExpectInvalid(Bound(widget), RiveErrorCode.ViewModelInstanceNotFound);
        }
        [UnityTest]
        public IEnumerator AutoBindSelected_BindsNamedInstance()
        {
            Asset asset = null;
            yield return LoadAsset(TestAssetReferences.riv_asset_databinding_test, a => asset = a);
            string expected;
            using (File plain = File.Load(asset))
            using (ViewModelInstance jane = plain.GetViewModelByName("PersonViewModel").CreateInstanceByName("Jane"))
            {
                expected = jane.GetStringProperty("name").Value;
            }

            RiveWidget widget = null;
            yield return LoadWidget(TestAssetReferences.riv_asset_databinding_test, w => widget = w, (p, w) =>
            {
                w.BindingMode = RiveWidget.DataBindingMode.AutoBindSelected;
                w.ViewModelInstanceName = "Jane";
            });
            Future<string> name = Bound(widget).GetStringProperty("name").GetValueAsync();
            yield return Await(name);

            Assert.AreEqual(expected, name.Result);
        }

        [UnityTest]
        public IEnumerator AutoBindSelected_WithInvalidName_LogsError()
        {
            RiveWidget widget = null;
            yield return LoadWidget(TestAssetReferences.riv_asset_databinding_test, w => widget = w, (p, w) =>
            {
                w.BindingMode = RiveWidget.DataBindingMode.AutoBindSelected;
                w.ViewModelInstanceName = "NonExistentInstance";
            });

            Assert.Greater(m_mockLogger.LoggedErrors.Count, 0);
            yield return ExpectInvalid(Bound(widget), RiveErrorCode.ViewModelInstanceNotFound);
        }
        // ---- Values ----

        [UnityTest]
        public IEnumerator PropertyValues_CanBeSetAndRead()
        {
            RiveWidget widget = null;
            yield return LoadPerson(w => widget = w);
            ViewModelInstanceHandle person = Bound(widget);
            var red = new UnityEngine.Color(1f, 0f, 0f, 1f);

            person.GetStringProperty("name").SetValue("Test User");
            person.GetNumberProperty("age").SetValue(42f);
            person.GetBooleanProperty("agreedToTerms").SetValue(true);
            person.GetColorProperty("favColor").SetValue(red);
            person.GetEnumProperty("country").SetValue("japan");
            person.GetEnumProperty("favDrink/type").SetValue("Coffee");
            Future<string> name = person.GetStringProperty("name").GetValueAsync();
            Future<float> age = person.GetNumberProperty("age").GetValueAsync();
            Future<bool> agreed = person.GetBooleanProperty("agreedToTerms").GetValueAsync();
            Future<UnityEngine.Color> color = person.GetColorProperty("favColor").GetValueAsync();
            Future<string> country = person.GetEnumProperty("country").GetValueAsync();
            Future<string> drink = person.GetEnumProperty("favDrink/type").GetValueAsync();
            yield return Await(drink);

            Assert.AreEqual("Test User", name.Result);
            Assert.AreEqual(42f, age.Result);
            Assert.IsTrue(agreed.Result);
            Assert.AreEqual(red, color.Result);
            Assert.AreEqual("japan", country.Result);
            Assert.AreEqual("Coffee", drink.Result);
        }

        // Reads are pooled down to their future state, so reading a value every
        // frame and waiting on it costs nothing once warmed up.
        [UnityPlatform(RuntimePlatform.OSXEditor, RuntimePlatform.WindowsEditor, RuntimePlatform.LinuxEditor)]
        [UnityTest]
        public IEnumerator ValueRead_WaitedOn_AllocatesNothing()
        {
            RiveWidget widget = null;
            yield return LoadPerson(w => widget = w);
            NumberPropertyHandle age = Bound(widget).GetNumberProperty("age");
            yield return Await(age.ResolveAsync());
            TestDelegate read = () => age.GetValueAsync().WaitForCompletion();
            for (int i = 0; i < 8; i++)
            {
                read();
            }

            Assert.That(read, UnityEngine.TestTools.Constraints.ConstraintExtensions.AllocatingGCMemory(
                UnityEngine.TestTools.Constraints.Is.Not));
        }

        [UnityTest]
        public IEnumerator ValueRead_UsedAgainAfterItsOneUse_Throws()
        {
            RiveWidget widget = null;
            yield return LoadPerson(w => widget = w);
            NumberPropertyHandle age = Bound(widget).GetNumberProperty("age");
            age.SetValue(30f);
            Future<float> read = age.GetValueAsync();

            Assert.AreEqual(30f, read.WaitForCompletion());
            Assert.Throws<InvalidOperationException>(() => { bool _ = read.IsDone; },
                "Its state goes back to the pool after the one use.");
        }

        // Reads are delivered by the frame loop on the main thread. We shouldn't
        // deliver them from a wait on another thread.
        [NeedsManagedThreads]
        [UnityTest]
        public IEnumerator ValueRead_WaitedOnFromAnotherThread_FinishesOnTheMainThread()
        {
            RiveWidget widget = null;
            yield return LoadPerson(w => widget = w);
            NumberPropertyHandle age = Bound(widget).GetNumberProperty("age");
            age.SetValue(30f);
            Future<float> read = age.GetValueAsync();
            int mainThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
            int finishedOn = -1;
            read.Completed += _ => finishedOn = System.Threading.Thread.CurrentThread.ManagedThreadId;

            float value = 0f;
            var waiting = System.Threading.Tasks.Task.Run(() => value = read.WaitForCompletion());
            for (int i = 0; i < 300 && !waiting.IsCompleted; i++)
            {
                yield return null;
            }

            Assert.IsTrue(waiting.IsCompleted, "The wait should end once the main thread delivers the read.");
            Assert.IsNull(waiting.Exception, "The wait shouldn't throw.");
            Assert.AreEqual(30f, value);
            Assert.AreEqual(mainThread, finishedOn, "The read should finish on the main thread.");
        }

        // The frame loop may not run again after a shutdown, as on quit or a
        // domain reload, so the shutdown itself has to end the wait.
        [NeedsRiveThread]
        [NeedsManagedThreads]
        [UnityTest]
        public IEnumerator ValueRead_WaitedOnFromAnotherThread_EndsWhenTheHostStops()
        {
            RiveWidget widget = null;
            yield return LoadPerson(w => widget = w);
            NumberPropertyHandle age = Bound(widget).GetNumberProperty("age");
            yield return Await(age.ResolveAsync());

            Future<float> read = age.GetValueAsync();
            var started = new System.Threading.ManualResetEventSlim(false);
            var waiting = System.Threading.Tasks.Task.Run(() =>
            {
                started.Set();
                try
                {
                    read.WaitForCompletion();
                }
                catch (OperationCanceledException)
                {
                    // Canceled by the shutdown, unless it landed first.
                }
            });
            Assert.IsTrue(started.Wait(2000), "The wait should have started.");

            // No frame passes between here and the check, so only the
            // shutdown can end the wait.
            try
            {
                Rive.Host.CommandTransport.Shutdown();
                Assert.IsTrue(waiting.Wait(2000), "The wait should end when the host stops.");
            }
            finally
            {
                Rive.Host.CommandTransport.EnsureStarted();
            }
        }

        [UnityTest]
        public IEnumerator BooleanFalse_ReadsBackAsFalse()
        {
            RiveWidget widget = null;
            yield return LoadPerson(w => widget = w);
            BooleanPropertyHandle agreed = Bound(widget).GetBooleanProperty("agreedToTerms");

            agreed.SetValue(true);
            agreed.SetValue(false);
            Future<bool> read = agreed.GetValueAsync();
            yield return Await(read);

            Assert.IsFalse(read.Result);
        }

        [UnityTest]
        public IEnumerator StringSetToNull_ReadsBackEmpty()
        {
            RiveWidget widget = null;
            yield return LoadPerson(w => widget = w);
            StringPropertyHandle name = Bound(widget).GetStringProperty("name");

            name.SetValue(null);
            Future<string> read = name.GetValueAsync();
            yield return Await(read);

            Assert.AreEqual(string.Empty, read.Result);
        }

        [UnityTest]
        public IEnumerator Enum_AcceptsEachValue()
        {
            RiveWidget widget = null;
            yield return LoadPerson(w => widget = w);
            EnumPropertyHandle country = Bound(widget).GetEnumProperty("country");

            foreach (string value in new[] { "japan", "usa", "canada" })
            {
                country.SetValue(value);
                Future<string> read = country.GetValueAsync();
                yield return Await(read);
                Assert.AreEqual(value, read.Result);
            }
        }

        [UnityTest]
        public IEnumerator BlankInstance_HasZeroDefaults()
        {
            RiveWidget widget = null;
            yield return LoadPerson(w => widget = w);
            ViewModelInstanceHandle blank = Track(widget.FileHandle.GetViewModel("PersonViewModel").InstantiateBlank());

            Future<string> name = blank.GetStringProperty("name").GetValueAsync();
            Future<float> age = blank.GetNumberProperty("age").GetValueAsync();
            Future<bool> agreed = blank.GetBooleanProperty("agreedToTerms").GetValueAsync();
            yield return Await(agreed);

            Assert.AreEqual(string.Empty, name.Result);
            Assert.AreEqual(0f, age.Result);
            Assert.IsFalse(agreed.Result);
        }

        [UnityTest]
        public IEnumerator ViewModel_NameAndInstanceNames()
        {
            RiveWidget widget = null;
            yield return LoadPerson(w => widget = w);
            ViewModelHandle person = widget.FileHandle.GetViewModel("PersonViewModel");

            Assert.AreEqual("PersonViewModel", person.GetNameAsync().Result);
            CollectionAssert.AreEqual(new[] { "Steve", "Jane" }, person.GetInstanceNamesAsync().Result);
            Assert.AreEqual("PersonViewModel", widget.StateMachineHandle.Artboard.GetDefaultViewModel().GetNameAsync().Result);
        }

        [UnityTest]
        public IEnumerator NestedPath_AndNestedInstance_ReachTheSameProperty()
        {
            RiveWidget widget = null;
            yield return LoadPerson(w => widget = w);
            ViewModelInstanceHandle drink = Track(Bound(widget).GetViewModelInstanceProperty("favDrink"));

            drink.GetStringProperty("name").SetValue("Matcha");
            Future<string> viaPath = Bound(widget).GetStringProperty("favDrink/name").GetValueAsync();
            yield return Await(viaPath);

            Assert.AreEqual("Matcha", viaPath.Result);
        }

        // ---- Metadata and names ----

        [UnityTest]
        public IEnumerator ViewModelProperties_AndEnums_MatchThePlainFile()
        {
            Asset asset = null;
            yield return LoadAsset(TestAssetReferences.riv_asset_databinding_test, a => asset = a);
            RiveWidget widget = null;
            yield return LoadPerson(w => widget = w);
            FileHandle file = widget.FileHandle;

            using (File plain = File.Load(asset))
            {
                foreach (ViewModel viewModel in plain.ViewModels)
                {
                    IReadOnlyList<ViewModelPropertyData> properties = file.GetViewModel(viewModel.Name).GetPropertiesAsync().Result;
                    Assert.AreEqual(viewModel.Properties.Count, properties.Count, viewModel.Name);
                    for (int i = 0; i < viewModel.Properties.Count; i++)
                    {
                        Assert.AreEqual(viewModel.Properties[i].Name, properties[i].Name);
                        Assert.AreEqual(viewModel.Properties[i].Type, properties[i].Type, viewModel.Properties[i].Name);
                    }
                }
                IReadOnlyList<ViewModelEnumData> enums = file.GetViewModelEnumsAsync().Result;
                Assert.AreEqual(plain.ViewModelEnums.Count, enums.Count);
                for (int i = 0; i < plain.ViewModelEnums.Count; i++)
                {
                    Assert.AreEqual(plain.ViewModelEnums[i].Name, enums[i].Name);
                    CollectionAssert.AreEqual(plain.ViewModelEnums[i].Values, enums[i].Values);
                }
            }
        }

        [UnityTest]
        public IEnumerator EnumValues_AreKnownUpFront_IncludingNested()
        {
            RiveWidget widget = null;
            yield return LoadPerson(w => widget = w);

            // An instance made from a view model has a known view model, so the file has these.
            ViewModelInstanceHandle person = Track(widget.FileHandle.GetViewModel("PersonViewModel").Instantiate());
            CollectionAssert.AreEqual(new[] { "japan", "usa", "canada" }, person.GetEnumProperty("country").GetEnumValuesAsync().Result);
            CollectionAssert.AreEqual(new[] { "Tea", "Coffee" }, person.GetEnumProperty("favDrink/type").GetEnumValuesAsync().Result);
            ViewModelInstanceHandle drink = Track(person.GetViewModelInstanceProperty("favDrink"));
            CollectionAssert.AreEqual(new[] { "Tea", "Coffee" }, drink.GetEnumProperty("type").GetEnumValuesAsync().Result);

            // What's bound could be any view model's instance, so Rive reads them.
            Future<IReadOnlyList<string>> viaBound = Bound(widget).GetEnumProperty("country").GetEnumValuesAsync();
            yield return WaitUntil(() => viaBound.IsDone, "The read");
            CollectionAssert.AreEqual(new[] { "japan", "usa", "canada" }, viaBound.Result);

            Future<IReadOnlyList<string>> missing = person.GetEnumProperty("missing").GetEnumValuesAsync();
            yield return WaitUntil(() => missing.IsDone, "The failed read");
            Assert.AreEqual(RiveErrorCode.PropertyNotFound, ((RiveException)missing.Exception).Code);
        }
        [UnityTest]
        public IEnumerator SystemEnums_HaveValues_AndAcceptEachOne()
        {
            Asset asset = null;
            yield return LoadAsset(TestAssetReferences.riv_system_enum_test, a => asset = a);
            Future<FileHandle> load = FileHandle.LoadAsync(asset);
            yield return WaitUntil(() => load.IsDone, "The load");
            FileHandle file = Track(load.Result);

            int enumProperties = 0;
            foreach (string viewModelName in file.GetViewModelNamesAsync().Result)
            {
                ViewModelHandle viewModel = file.GetViewModel(viewModelName);
                ViewModelInstanceHandle instance = Track(viewModel.Instantiate());
                foreach (ViewModelPropertyData property in viewModel.GetPropertiesAsync().Result)
                {
                    if (property.Type != ViewModelDataType.Enum)
                    {
                        continue;
                    }
                    enumProperties++;
                    EnumPropertyHandle enumProperty = instance.GetEnumProperty(property.Name);
                    Future<IReadOnlyList<string>> values = enumProperty.GetEnumValuesAsync();
                    yield return Await(values);
                    Assert.Greater(values.Result.Count, 0, $"'{viewModelName}.{property.Name}' should have values.");
                    foreach (string value in values.Result)
                    {
                        enumProperty.SetValue(value);
                        Future<string> read = enumProperty.GetValueAsync();
                        yield return Await(read);
                        Assert.AreEqual(value, read.Result, $"'{viewModelName}.{property.Name}'");
                    }
                }
            }
            Assert.Greater(enumProperties, 0, "The file should have enum properties.");
        }

        [UnityTest]
        public IEnumerator InstanceNames_AreAsync()
        {
            Asset asset = null;
            yield return LoadAsset(TestAssetReferences.riv_asset_databinding_test, a => asset = a);
            string defaultName;
            using (File plain = File.Load(asset))
            using (ViewModelInstance defaults = plain.GetViewModelByName("PersonViewModel").CreateDefaultInstance())
            {
                defaultName = defaults.Name;
            }
            RiveWidget widget = null;
            yield return LoadPerson(w => widget = w);
            ViewModelHandle person = widget.FileHandle.GetViewModel("PersonViewModel");

            ViewModelInstanceHandle jane = Track(person.Instantiate("Jane"));
            Assert.AreEqual("Jane", jane.GetNameAsync().Result, "Made by name, so it's already there.");
            ViewModelInstanceHandle defaults2 = Track(person.Instantiate());
            Future<string> name = defaults2.GetNameAsync();
            yield return Await(name);
            Assert.AreEqual(defaultName, name.Result);

            ViewModelInstanceHandle drink = Track(jane.GetViewModelInstanceProperty("favDrink"));
            Assert.AreEqual("DrinkViewModel", drink.GetViewModelNameAsync().Result, "A nested instance's view model is known from the file.");
        }

        [UnityTest]
        public IEnumerator ListItem_ViewModelName_IsAsync()
        {
            RiveWidget widget = null;
            yield return LoadList(w => widget = w);
            ListPropertyHandle items = Bound(widget).GetListProperty("items");
            items.Add(Todo(widget, "one"));
            ViewModelInstanceHandle item = Track(items.GetInstanceAt(0));

            // A list can hold more than one view model, so this one comes from Rive's thread.
            Future<string> viewModelName = item.GetViewModelNameAsync();
            yield return Await(viewModelName);
            Assert.AreEqual("TodoItem", viewModelName.Result);
        }

        // ---- Replacing nested instances ----

        [UnityTest]
        public IEnumerator NestedInstance_CanBeReplaced_AndPathsFollowIt()
        {
            RiveWidget widget = null;
            yield return LoadPerson(w => widget = w);
            ViewModelInstanceHandle person = Bound(widget);
            var seen = new List<string>();
            person.GetStringProperty("favDrink/name").Subscribe(seen.Add);

            ViewModelInstanceHandle replacement = Track(widget.FileHandle.GetViewModel("DrinkViewModel").InstantiateBlank());
            replacement.GetStringProperty("name").SetValue("Replacement");
            person.SetViewModelInstanceProperty("favDrink", replacement);
            Future<string> viaPath = person.GetStringProperty("favDrink/name").GetValueAsync();
            yield return Await(viaPath);
            Assert.AreEqual("Replacement", viaPath.Result);

            replacement.GetStringProperty("name").SetValue("Changed");
            yield return WaitUntil(() => seen.Contains("Changed"), "The path's callback following the new instance");
        }

        [UnityTest]
        public IEnumerator NestedInstance_CanBeSharedAcrossWidgets()
        {
            RiveWidget first = null;
            RiveWidget second = null;
            yield return LoadPerson(w => first = w);
            yield return LoadPerson(w => second = w);
            ViewModelInstanceHandle shared = Track(first.FileHandle.GetViewModel("DrinkViewModel").InstantiateBlank());

            Bound(first).SetViewModelInstanceProperty("favDrink", shared);
            Bound(second).SetViewModelInstanceProperty("favDrink", shared);
            shared.GetStringProperty("name").SetValue("Shared");
            Future<string> viaFirst = Bound(first).GetStringProperty("favDrink/name").GetValueAsync();
            Future<string> viaSecond = Bound(second).GetStringProperty("favDrink/name").GetValueAsync();
            yield return Await(viaSecond);

            Assert.AreEqual("Shared", viaFirst.Result);
            Assert.AreEqual("Shared", viaSecond.Result);
        }

        [UnityTest]
        public IEnumerator ReplacingABadPath_IsReported()
        {
            var errors = new List<RiveException>();
            RiveWidget widget = null;
            yield return LoadWidget(TestAssetReferences.riv_asset_databinding_test, w => widget = w,
                (p, w) => w.OnError += errors.Add);
            ViewModelInstanceHandle replacement = Track(widget.FileHandle.GetViewModel("DrinkViewModel").InstantiateBlank());

            Bound(widget).SetViewModelInstanceProperty("name", replacement);
            yield return WaitUntil(() => errors.Count > 0, "The report");

            Assert.AreEqual(RiveErrorCode.PropertyNotFound, errors[0].Code);
            StringAssert.Contains("SetViewModelInstanceProperty", errors[0].Message);
        }

        [UnityTest]
        public IEnumerator ReplacingANestedNonViewModelProperty_IsReported()
        {
            var errors = new List<RiveException>();
            RiveWidget widget = null;
            yield return LoadWidget(TestAssetReferences.riv_asset_databinding_test,
                w => widget = w, (p, w) => w.OnError += errors.Add);
            ViewModelInstanceHandle replacement = Track(widget.FileHandle.GetViewModel("DrinkViewModel").InstantiateBlank());

            Bound(widget).SetViewModelInstanceProperty("favDrink/name", replacement);
            yield return WaitUntil(() => errors.Count > 0, "The report");

            Assert.AreEqual(RiveErrorCode.PropertyNotFound, errors[0].Code);
            StringAssert.Contains("SetViewModelInstanceProperty", errors[0].Message);
        }

        // ---- Change callbacks ----

        [UnityTest]
        public IEnumerator PropertyValueChanges_TriggerCallbacks()
        {
            RiveWidget widget = null;
            yield return LoadPerson(w => widget = w);
            ViewModelInstanceHandle person = Bound(widget);
            // Each new value differs from the current one, so each is a change.
            Future<float> currentAge = person.GetNumberProperty("age").GetValueAsync();
            Future<bool> currentAgreed = person.GetBooleanProperty("agreedToTerms").GetValueAsync();
            Future<UnityEngine.Color> currentColor = person.GetColorProperty("favColor").GetValueAsync();
            Future<string> currentCountry = person.GetEnumProperty("country").GetValueAsync();
            yield return Await(currentCountry);
            float newAge = currentAge.Result + 1f;
            bool newAgreed = !currentAgreed.Result;
            var newColor = new UnityEngine.Color(1f - currentColor.Result.r, 1f - currentColor.Result.g, 1f - currentColor.Result.b, 1f);
            string newCountry = currentCountry.Result == "usa" ? "canada" : "usa";

            string name = null;
            float? age = null;
            bool? agreed = null;
            UnityEngine.Color? color = null;
            string country = null;
            person.GetStringProperty("name").Subscribe(v => name = v);
            person.GetNumberProperty("age").Subscribe(v => age = v);
            person.GetBooleanProperty("agreedToTerms").Subscribe(v => agreed = v);
            person.GetColorProperty("favColor").Subscribe(v => color = v);
            person.GetEnumProperty("country").Subscribe(v => country = v);

            person.GetStringProperty("name").SetValue("Callback");
            person.GetNumberProperty("age").SetValue(newAge);
            person.GetBooleanProperty("agreedToTerms").SetValue(newAgreed);
            person.GetColorProperty("favColor").SetValue(newColor);
            person.GetEnumProperty("country").SetValue(newCountry);
            yield return WaitUntil(() => name != null && age.HasValue && agreed.HasValue && color.HasValue && country != null,
                "Every callback");

            Assert.AreEqual("Callback", name);
            Assert.AreEqual(newAge, age.Value);
            Assert.AreEqual(newAgreed, agreed.Value);
            Assert.AreEqual((Color32)newColor, (Color32)color.Value);
            Assert.AreEqual(newCountry, country);
        }

        [UnityTest]
        public IEnumerator Number_FiresOncePerSet()
        {
            RiveWidget widget = null;
            yield return LoadPerson(w => widget = w);
            NumberPropertyHandle age = Bound(widget).GetNumberProperty("age");
            int calls = 0;
            age.Subscribe(_ => calls++);

            age.SetValue(10f);
            yield return WaitUntil(() => calls > 0, "The first callback");
            yield return Frames(5);
            Assert.AreEqual(1, calls, "One set, one callback.");

            age.SetValue(11f);
            yield return WaitUntil(() => calls > 1, "The second callback");
            yield return Frames(5);
            Assert.AreEqual(2, calls);
        }

        [UnityTest]
        public IEnumerator Boolean_FiresOnEverySubsequentChange()
        {
            RiveWidget widget = null;
            yield return LoadPerson(w => widget = w);
            BooleanPropertyHandle agreed = Bound(widget).GetBooleanProperty("agreedToTerms");
            agreed.SetValue(false);
            yield return Frames(3);
            int calls = 0;
            agreed.Subscribe(_ => calls++);

            foreach (bool value in new[] { true, false, true })
            {
                int before = calls;
                agreed.SetValue(value);
                yield return WaitUntil(() => calls > before, $"The callback for {value}");
            }
            Assert.AreEqual(3, calls);
        }

        [UnityTest]
        public IEnumerator Trigger_FiresOncePerFire()
        {
            RiveWidget widget = null;
            yield return LoadPerson(w => widget = w);
            TriggerPropertyHandle submit = Bound(widget).GetTriggerProperty("onFormSubmit");
            int calls = 0;
            submit.Subscribe(() => calls++);

            submit.Fire();
            yield return WaitUntil(() => calls > 0, "The trigger callback");
            yield return Frames(5);
            Assert.AreEqual(1, calls);
        }

        [UnityTest]
        public IEnumerator Trigger_ManualPanel_FiresAfterTheTick()
        {
            RivePanel panel = null;
            RiveWidget widget = null;
            yield return LoadWidget(TestAssetReferences.riv_asset_databinding_test, w => widget = w,
                (p, w) => { panel = p; p.UpdateMode = RivePanel.PanelUpdateMode.Manual; });
            TriggerPropertyHandle submit = Bound(widget).GetTriggerProperty("onFormSubmit");
            int calls = 0;
            submit.Subscribe(() => calls++);

            submit.Fire();
            yield return Frames(5);
            Assert.AreEqual(0, calls, "Nothing advances a Manual panel until it ticks.");

            panel.Tick(0.016f);
            yield return WaitUntil(() => calls > 0, "The callback after the tick");
            Assert.AreEqual(1, calls);
        }

        [UnityTest]
        public IEnumerator MultipleCallbacks_AreAllTriggered()
        {
            RiveWidget widget = null;
            yield return LoadPerson(w => widget = w);
            NumberPropertyHandle age = Bound(widget).GetNumberProperty("age");
            int first = 0, second = 0;
            age.Subscribe(_ => first++);
            age.Subscribe(_ => second++);

            age.SetValue(20f);
            yield return WaitUntil(() => first > 0 && second > 0, "Both callbacks");
        }

        [UnityTest]
        public IEnumerator ExceptionInOneCallback_DoesNotStopTheOthers()
        {
            RiveWidget widget = null;
            yield return LoadPerson(w => widget = w);
            ViewModelInstanceHandle person = Bound(widget);
            bool nameSeen = false;
            person.GetNumberProperty("age").Subscribe(_ => throw new InvalidOperationException("from a test callback"));
            person.GetStringProperty("name").Subscribe(_ => nameSeen = true);

            person.GetNumberProperty("age").SetValue(30f);
            person.GetStringProperty("name").SetValue("Still delivered");
            yield return WaitUntil(() => nameSeen, "The other callback");

            Assert.IsTrue(m_mockLogger.LoggedExceptionsContains("from a test callback"));
        }

        [UnityTest]
        public IEnumerator UnboundInstance_CallbacksStillFire()
        {
            RiveWidget widget = null;
            yield return LoadPerson(w => widget = w);
            // Not bound to anything; the panel's capture still covers it.
            ViewModelInstanceHandle loose = Track(widget.FileHandle.GetViewModel("PersonViewModel").Instantiate());
            string seen = null;
            loose.GetStringProperty("name").Subscribe(v => seen = v);

            loose.GetStringProperty("name").SetValue("Loose");
            yield return WaitUntil(() => seen != null, "The callback");

            Assert.AreEqual("Loose", seen);
        }

        [UnityTest]
        public IEnumerator Unsubscribed_DoesNotInvokeCallback()
        {
            RiveWidget widget = null;
            yield return LoadPerson(w => widget = w);
            NumberPropertyHandle age = Bound(widget).GetNumberProperty("age");
            int calls = 0;
            PropertySubscription subscription = age.Subscribe(_ => calls++);
            Assert.IsTrue(subscription.IsActive);
            subscription.Dispose();
            Assert.IsFalse(subscription.IsActive);
            Assert.DoesNotThrow(subscription.Dispose, "Disposing twice is fine.");

            age.SetValue(50f);
            Future<float> read = age.GetValueAsync();
            yield return Await(read);
            yield return Frames(5);

            Assert.AreEqual(0, calls);
        }

        [UnityTest]
        public IEnumerator DisposingOneSubscription_KeepsTheOther()
        {
            RiveWidget widget = null;
            yield return LoadPerson(w => widget = w);
            NumberPropertyHandle age = Bound(widget).GetNumberProperty("age");
            int calls = 0;
            Action<float> onAge = _ => calls++;
            PropertySubscription first = age.Subscribe(onAge);
            PropertySubscription second = age.Subscribe(onAge);
            first.Dispose();

            age.SetValue(51f);
            yield return WaitUntil(() => calls > 0, "The remaining callback");
            yield return Frames(5);

            Assert.AreEqual(1, calls);
            Assert.IsTrue(second.IsActive);
        }

        [UnityTest]
        public IEnumerator DisposingTheInstance_EndsItsSubscriptions()
        {
            RiveWidget widget = null;
            yield return LoadPerson(w => widget = w);
            ViewModelInstanceHandle loose = widget.FileHandle.GetViewModel("PersonViewModel").Instantiate();
            PropertySubscription subscription = loose.GetStringProperty("name").Subscribe(_ => { });
            Assert.IsTrue(subscription.IsActive);

            loose.Dispose();

            Assert.IsFalse(subscription.IsActive);
            Assert.DoesNotThrow(subscription.Dispose);
        }

        // ---- Lists ----

        [UnityTest]
        public IEnumerator List_CountMatchesThePlainList()
        {
            Asset asset = null;
            yield return LoadAsset(TestAssetReferences.riv_db_list_test, a => asset = a);
            int expected;
            using (File plain = File.Load(asset))
            using (Artboard artboard = plain.Artboard(0))
            using (ViewModelInstance instance = artboard.DefaultViewModel.CreateDefaultInstance())
            {
                expected = instance.GetListProperty("items").Count;
            }

            RiveWidget widget = null;
            yield return LoadList(w => widget = w);
            Future<int> count = Bound(widget).GetListProperty("items").GetCountAsync();
            yield return Await(count);

            Assert.AreEqual(expected, count.Result);
        }

        private ViewModelInstanceHandle Todo(RiveWidget widget, string text)
        {
            ViewModelInstanceHandle item = Track(widget.FileHandle.GetViewModel("TodoItem").InstantiateBlank());
            item.GetStringProperty("text").SetValue(text);
            return item;
        }

        private static IEnumerator Texts(ListPropertyHandle list, List<string> into)
        {
            Future<int> count = list.GetCountAsync();
            yield return Await(count);
            var reads = new List<Future<string>>();
            for (int i = 0; i < count.Result; i++)
            {
                reads.Add(list.GetInstanceAt(i).GetStringProperty("text").GetValueAsync());
            }
            for (int i = 0; i < reads.Count; i++)
            {
                yield return Await(reads[i]);
                into.Add(reads[i].Result);
            }
        }

        [UnityTest]
        public IEnumerator List_AddInsertRemoveAtSwap_InOrder()
        {
            RiveWidget widget = null;
            yield return LoadList(w => widget = w);
            ListPropertyHandle items = Bound(widget).GetListProperty("items");
            items.Clear();

            items.Add(Todo(widget, "B"));
            items.Add(Todo(widget, "D"));
            items.Insert(0, Todo(widget, "A"));
            items.Insert(2, Todo(widget, "C"));
            var afterInsert = new List<string>();
            yield return Texts(items, afterInsert);
            CollectionAssert.AreEqual(new[] { "A", "B", "C", "D" }, afterInsert);

            items.RemoveAt(1);
            items.Swap(0, 2);
            var afterSwap = new List<string>();
            yield return Texts(items, afterSwap);
            CollectionAssert.AreEqual(new[] { "D", "C", "A" }, afterSwap);
        }

        [UnityTest]
        public IEnumerator List_Remove_RemovesEveryOccurrence()
        {
            RiveWidget widget = null;
            yield return LoadList(w => widget = w);
            ListPropertyHandle items = Bound(widget).GetListProperty("items");
            items.Clear();
            ViewModelInstanceHandle twice = Todo(widget, "twice");

            items.Add(twice);
            items.Add(Todo(widget, "other"));
            items.Add(twice);
            items.Remove(twice);
            var texts = new List<string>();
            yield return Texts(items, texts);

            CollectionAssert.AreEqual(new[] { "other" }, texts);
        }

        [UnityTest]
        public IEnumerator List_Clear_RemovesAll_AndItemsStayReadable()
        {
            RiveWidget widget = null;
            yield return LoadList(w => widget = w);
            ListPropertyHandle items = Bound(widget).GetListProperty("items");
            ViewModelInstanceHandle kept = Todo(widget, "kept");
            items.Add(kept);

            items.Clear();
            items.Clear();
            Future<int> count = items.GetCountAsync();
            Future<string> text = kept.GetStringProperty("text").GetValueAsync();
            yield return Await(text);

            Assert.AreEqual(0, count.Result, "Clearing twice is fine.");
            Assert.AreEqual("kept", text.Result, "A cleared item is still usable.");
        }

        [UnityTest]
        public IEnumerator List_Subscribe_FiresForEachChange()
        {
            RiveWidget widget = null;
            yield return LoadList(w => widget = w);
            ListPropertyHandle items = Bound(widget).GetListProperty("items");
            items.Clear();
            items.Add(Todo(widget, "one"));
            items.Add(Todo(widget, "two"));
            yield return Frames(3);
            int calls = 0;
            items.Subscribe(() => calls++);

            var changes = new Action[]
            {
                () => items.Add(Todo(widget, "three")),
                () => items.Swap(0, 1),
                () => items.RemoveAt(0),
                () => items.Clear(),
            };
            foreach (Action change in changes)
            {
                int before = calls;
                change();
                yield return WaitUntil(() => calls > before, "The list callback");
            }
            Assert.AreEqual(changes.Length, calls);
        }

        [UnityTest]
        public IEnumerator List_ItemCallbacks_FireForAddedAndFetchedItems()
        {
            RiveWidget widget = null;
            yield return LoadList(w => widget = w);
            ListPropertyHandle items = Bound(widget).GetListProperty("items");
            items.Clear();
            ViewModelInstanceHandle added = Todo(widget, "added");
            items.Add(added);
            string viaAdded = null;
            string viaFetched = null;
            added.GetStringProperty("text").Subscribe(v => viaAdded = v);
            ViewModelInstanceHandle fetched = Track(items.GetInstanceAt(0));
            fetched.GetStringProperty("text").Subscribe(v => viaFetched = v);

            fetched.GetStringProperty("text").SetValue("renamed");
            yield return WaitUntil(() => viaAdded != null && viaFetched != null, "Both item callbacks");

            Assert.AreEqual("renamed", viaAdded);
            Assert.AreEqual("renamed", viaFetched);
        }

        // ---- Artboards ----

        [UnityTest]
        public IEnumerator ArtboardProperty_CanUseAnotherFilesArtboard()
        {
            Asset external = null;
            yield return LoadAsset(TestAssetReferences.riv_ratingAnimationWithEvents, a => external = a);
            Future<FileHandle> loading = FileHandle.LoadAsync(external);
            yield return WaitUntil(() => loading.IsDone, "The external load");
            FileHandle externalFile = Track(loading.Result);

            RiveWidget widget = null;
            yield return LoadWidget(TestAssetReferences.riv_artboard_db_test, w => widget = w);
            int calls = 0;
            ArtboardPropertyHandle property = Bound(widget).GetArtboardProperty("artboard_1");
            property.Subscribe(() => calls++);
            property.SetValue(Track(externalFile.GetBindableArtboard(externalFile.GetArtboardNamesAsync().Result[0])));
            yield return WaitUntil(() => calls > 0, "The artboard callback");

            Assert.AreEqual(0, m_mockLogger.LoggedErrors.Count);
        }

        [UnityTest]
        public IEnumerator BindableArtboard_SameName_GivesSeparateHandles()
        {
            RiveWidget widget = null;
            yield return LoadWidget(TestAssetReferences.riv_artboard_db_test, w => widget = w);

            BindableArtboardHandle first = Track(widget.FileHandle.GetBindableArtboard("ArtboardBlue"));
            BindableArtboardHandle second = Track(widget.FileHandle.GetBindableArtboard("ArtboardBlue"));

            Assert.AreNotSame(first, second);
            first.Dispose();
            Assert.IsTrue(first.IsDisposed);
            Assert.IsFalse(second.IsDisposed, "Each handle holds its own reference.");
        }

        // ---- Disposal ----

        [UnityTest]
        public IEnumerator DisposedInstance_WritesLogAndReadsFail()
        {
            RiveWidget widget = null;
            yield return LoadPerson(w => widget = w);
            ViewModelInstanceHandle instance = widget.FileHandle.GetViewModel("PersonViewModel").Instantiate();
            NumberPropertyHandle age = instance.GetNumberProperty("age");
            instance.Dispose();

            age.SetValue(1f);
            Assert.IsTrue(m_mockLogger.LoggedErrorsContains("has been disposed"));
            Future<float> read = age.GetValueAsync();
            Assert.AreEqual(FutureStatus.Failed, read.Status);
            Assert.AreEqual(RiveErrorCode.ResourceDisposed, ((RiveException)read.Exception).Code);

            PropertySubscription subscription = age.Subscribe(_ => { });
            Assert.IsFalse(subscription.IsActive);
            Assert.DoesNotThrow(subscription.Dispose);
            Assert.IsNull(instance.GetViewModelInstanceProperty("favDrink"));
        }

        // ---- Globals through a widget ----

        [UnityTest]
        public IEnumerator Global_FiresCallbacks_IncludingForAReboundInstance()
        {
            RiveWidget widget = null;
            yield return LoadWidget(TestAssetReferences.riv_global_variables_test, w => widget = w);
            StateMachineHandle stateMachine = widget.StateMachineHandle;
            ViewModelInstanceHandle labels = Track(stateMachine.GetGlobalViewModelInstance("Labels"));
            var seen = new List<string>();
            labels.GetStringProperty("currency").Subscribe(seen.Add);

            labels.GetStringProperty("currency").SetValue("first");
            yield return WaitUntil(() => seen.Count > 0, "The global callback");
            labels.GetStringProperty("currency").SetValue("second");
            yield return WaitUntil(() => seen.Count > 1, "The second callback");
            CollectionAssert.AreEqual(new[] { "first", "second" }, seen);

            ViewModelInstanceHandle rebound = Track(widget.FileHandle.GetViewModel("Labels").Instantiate("US"));
            Future bind = stateMachine.BindViewModelInstanceAsync(null,
                new Dictionary<string, ViewModelInstanceHandle> { { "Labels", rebound } });
            yield return WaitUntil(() => bind.IsDone, "The bind");
            Assert.AreEqual(FutureStatus.Succeeded, bind.Status);
            string reboundSeen = null;
            rebound.GetStringProperty("currency").Subscribe(v => reboundSeen = v);
            rebound.GetStringProperty("currency").SetValue("rebound");
            yield return WaitUntil(() => reboundSeen != null, "The rebound instance's callback");
        }

        [UnityTest]
        public IEnumerator FailedGlobalsBind_ReachesOnError_WithoutAwaiting()
        {
            RiveWidget widget = null;
            yield return LoadWidget(TestAssetReferences.riv_global_variables_test, w => widget = w);
            var errors = new List<RiveException>();
            widget.OnError += errors.Add;
            StateMachineHandle stateMachine = widget.StateMachineHandle;

            Future atTheCall = stateMachine.BindViewModelInstanceAsync(null,
                new Dictionary<string, ViewModelInstanceHandle> { { "Colours", Track(widget.FileHandle.GetViewModel("Colors").Instantiate()) } });
            Assert.AreEqual(1, errors.Count, "A bad name reaches OnError straight away.");
            yield return WaitUntil(() => atTheCall.IsDone, "The bind");
            Assert.AreSame(atTheCall.Exception, errors[0]);

            ViewModelInstanceHandle released = Track(widget.FileHandle.GetViewModel("Labels").Instantiate());
            ViewModelInstanceNative.ReleaseLater(released.Native);
            Future onRivesThread = stateMachine.BindViewModelInstanceAsync(null,
                new Dictionary<string, ViewModelInstanceHandle> { { "Labels", released } });
            yield return WaitUntil(() => onRivesThread.IsDone, "The bind");

            Assert.AreEqual(2, errors.Count, "A bind that fails on Rive's thread reaches OnError too.");
            Assert.AreEqual(RiveErrorCode.ViewModelInstanceNotFound, errors[1].Code);
            Assert.AreSame(onRivesThread.Exception, errors[1]);
        }

        [UnityTest]
        public IEnumerator WidgetDestroyed_ReleasesItsReferencesToABoundGlobal()
        {
            RiveWidget widget = null;
            yield return LoadWidget(TestAssetReferences.riv_global_variables_test, w => widget = w);
            ViewModelInstanceHandle labels = Track(widget.FileHandle.GetViewModel("Labels").Instantiate());
            int before = ViewModelInstanceNative.RefCountForTests(labels.Native);
            StateMachineHandle stateMachine = widget.StateMachineHandle;
            Future bind = stateMachine.BindViewModelInstanceAsync(null,
                new Dictionary<string, ViewModelInstanceHandle> { { "Labels", labels } });
            yield return WaitUntil(() => bind.IsDone, "The bind");
            Assert.AreEqual(FutureStatus.Succeeded, bind.Status);
            Assert.Greater(ViewModelInstanceNative.RefCountForTests(labels.Native), before);

            UnityEngine.Object.Destroy(widget.gameObject);
            yield return Frames(3);

            yield return WaitUntil(() => ViewModelInstanceNative.RefCountForTests(labels.Native) == before,
                "The widget's state machine letting go");
        }
    }
}
