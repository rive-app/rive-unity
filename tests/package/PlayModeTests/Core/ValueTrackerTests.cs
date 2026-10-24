#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rive.Components;
using Rive.Tests.Utils;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rive.Tests
{
    /// <summary>
    /// The editor's value tracker: values C# sets on tracked view model properties are
    /// recorded by view model and property name, through handles and the
    /// older API, with the line that made them.
    /// </summary>
    public class ValueTrackerTests
    {
        private TestAssetLoadingManager m_assets;
        private Asset m_person;
        private readonly List<GameObject> m_objects = new List<GameObject>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            ValueTracker.SetRules(new ValueTracker.Rule[0]);
            ValueTracker.Clear();
            m_assets = new TestAssetLoadingManager();
            yield return m_assets.LoadAssetCoroutine<Asset>(TestAssetReferences.riv_asset_databinding_test,
                a => m_person = a, () => Assert.Fail("Failed to load the data binding asset"));
            m_objects.Add(new GameObject("ValueTrackerCamera", typeof(Camera)));
        }

        [TearDown]
        public void TearDown()
        {
            ValueTracker.SetRules(new ValueTracker.Rule[0]);
            ValueTracker.Clear();
            foreach (GameObject go in m_objects)
            {
                if (go != null)
                {
                    Object.Destroy(go);
                }
            }
            m_objects.Clear();
            m_assets.UnloadAllAssets();
        }

        [UnityTest]
        public IEnumerator HandleWrites_AreRecordedByViewModelAndProperty_WithTheirCaller()
        {
            RiveWidget widget = null;
            yield return LoadPerson(ThreadingMode.BackgroundThread, w => widget = w);
            ValueTracker.SetRules(new[]
            {
                new ValueTracker.Rule("PersonViewModel", "age"),
                new ValueTracker.Rule("DrinkViewModel", "name"),
            });

            ViewModelInstanceHandle person = widget.StateMachineHandle.GetViewModelInstance();
            person.GetNumberProperty("age").SetValue(5f);
            person.GetStringProperty("name").SetValue("Not tracked");
            person.GetStringProperty("favDrink/name").SetValue("Tea");
            person.GetViewModelInstanceProperty("favDrink").GetStringProperty("name").SetValue("Mocha");

            List<ValueTracker.Entry> entries = ValueTracker.Snapshot();
            Assert.AreEqual(new[] { "age = 5", "favDrink/name = \"Tea\"", "favDrink/name = \"Mocha\"" },
                entries.Select(e => $"{e.Path} = {e.Value}").ToArray(),
                "Only the tracked properties should be recorded, nested ones by their own view model.");
            Assert.AreEqual(new[] { "PersonViewModel", "DrinkViewModel", "DrinkViewModel" }, entries.Select(e => e.ViewModel).ToArray());
            foreach (ValueTracker.Entry entry in entries)
            {
                Assert.IsTrue(entry.HasCaller, "Each value set should know the line that set it.");
                StringAssert.EndsWith($"{nameof(ValueTrackerTests)}.cs", entry.Caller.File);
                Assert.Greater(entry.Caller.Line, 0);
            }
        }

        [UnityTest]
        public IEnumerator OlderApiWrites_AreRecordedToo()
        {
            RiveWidget widget = null;
            yield return LoadPerson(ThreadingMode.MainThread, w => widget = w);
            ValueTracker.SetRules(new[]
            {
                new ValueTracker.Rule("PersonViewModel", "age"),
                new ValueTracker.Rule("PersonViewModel", "onFormReset"),
                new ValueTracker.Rule("DrinkViewModel", "name"),
            });

            ViewModelInstance person = widget.LoadedStateMachine.ViewModelInstance;
            person.GetNumberProperty("age").Value = 7f;
            person.GetStringProperty("favDrink/name").Value = "Tea";
            person.GetTriggerProperty("onFormReset").Trigger();

            List<ValueTracker.Entry> entries = ValueTracker.Snapshot();
            Assert.AreEqual(new[] { "PersonViewModel.age = 7", "DrinkViewModel.name = \"Tea\"", "PersonViewModel.onFormReset = fired" },
                entries.Select(e => $"{e.ViewModel}.{e.Property} = {e.Value}").ToArray());
            Assert.IsTrue(entries.All(e => e.HasCaller && e.Caller.File.EndsWith($"{nameof(ValueTrackerTests)}.cs")),
                "Each value set should name this test as its caller.");
        }

        [UnityTest]
        public IEnumerator WithNoRules_NothingIsRecorded()
        {
            RiveWidget widget = null;
            yield return LoadPerson(ThreadingMode.BackgroundThread, w => widget = w);
            Assert.IsFalse(ValueTracker.Active);
            widget.StateMachineHandle.GetViewModelInstance().GetNumberProperty("age").SetValue(5f);
            Assert.IsEmpty(ValueTracker.Snapshot());
        }

        [UnityTest]
        public IEnumerator ChangesNotFromCSharp_AreRecordedAsSetByRive()
        {
            RiveWidget widget = null;
            yield return LoadPerson(ThreadingMode.BackgroundThread, w => widget = w);
            ValueTracker.SetRules(new[] { new ValueTracker.Rule("PersonViewModel", "age") });

            NumberPropertyHandle age = widget.StateMachineHandle.GetViewModelInstance().GetNumberProperty("age");
            age.SetValue(5f);
            yield return WaitForEntries(1);
            // Skips SetValue, so the tracker doesn't see C# set it, like a binding.
            age.WriteLater("test", _ => age.Write(42f));
            yield return WaitForEntries(2);
            // Gives C#'s own set time to come back, which shouldn't be recorded.
            yield return Frames(10);

            Assert.AreEqual(new[] { "age = 5 (C#)", "age = 42 (Rive)" },
                ValueTracker.Snapshot().Select(e => $"{e.Path} = {e.Value} ({(e.ByRive ? "Rive" : "C#")})").ToArray(),
                "A change C# didn't make should be recorded as set by Rive, and C#'s own set only once.");
        }

        [UnityTest]
        public IEnumerator OlderApiSets_AreNotMistakenForRive()
        {
            RiveWidget widget = null;
            yield return LoadPerson(ThreadingMode.BackgroundThread, w => widget = w);
            ValueTracker.SetRules(new[] { new ValueTracker.Rule("PersonViewModel", "age") });

            NumberPropertyHandle age = widget.StateMachineHandle.GetViewModelInstance().GetNumberProperty("age");
            yield return age.ResolveAsync();
            widget.LoadedStateMachine.ViewModelInstance.GetNumberProperty("age").Value = 7f;
            yield return Frames(10);

            Assert.AreEqual(new[] { "age = 7 (C#)" },
                ValueTracker.Snapshot().Select(e => $"{e.Property} = {e.Value} ({(e.ByRive ? "Rive" : "C#")})").ToArray(),
                "A set through the older API is C#'s, even when a handle sees it change.");
        }

        [UnityTest]
        public IEnumerator ClearingRules_StopsWatching_AndKeepsSubscribeCallbacks()
        {
            RiveWidget widget = null;
            yield return LoadPerson(ThreadingMode.BackgroundThread, w => widget = w);
            ValueTracker.SetRules(new[]
            {
                new ValueTracker.Rule("PersonViewModel", "age"),
                new ValueTracker.Rule("PersonViewModel", "name"),
            });

            ViewModelInstanceHandle person = widget.StateMachineHandle.GetViewModelInstance();
            NumberPropertyHandle age = person.GetNumberProperty("age");
            StringPropertyHandle name = person.GetStringProperty("name");
            var seen = new List<string>();
            using PropertySubscription subscription = name.Subscribe(value => seen.Add(value));
            Assert.AreEqual(2, ValueTracker.WatchingCountForTests);
            Assert.IsTrue(PropertyCallbacksHub.Instance.HasHandleForTests(age));

            ValueTracker.SetRules(new ValueTracker.Rule[0]);
            Assert.AreEqual(0, ValueTracker.WatchingCountForTests);
            Assert.IsFalse(PropertyCallbacksHub.Instance.HasHandleForTests(age),
                "A handle only the tracker watched should leave the captures.");
            Assert.IsTrue(PropertyCallbacksHub.Instance.HasHandleForTests(name),
                "A handle with Subscribe callbacks should keep them.");

            name.SetValue("Ada");
            for (int i = 0; i < 60 && seen.Count == 0; i++)
            {
                yield return null;
            }
            Assert.AreEqual(new[] { "Ada" }, seen.ToArray());
        }

        private static IEnumerator WaitForEntries(int count)
        {
            for (int i = 0; i < 60 && ValueTracker.Snapshot().Count < count; i++)
            {
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

        private IEnumerator LoadPerson(ThreadingMode mode, System.Action<RiveWidget> loaded)
        {
            RivePanel panel = RivePanelTestUtils.CreatePanel($"ValueTracker{mode}");
            m_objects.Add(panel.gameObject);
            panel.ThreadingMode = mode;
            panel.SetDimensions(new Vector2(256, 256));
            RiveWidget widget = RivePanelTestUtils.CreateWidget<RiveWidget>();
            widget.transform.SetParent(panel.WidgetContainer, false);
            widget.Load(m_person);
            yield return RivePanelTestUtils.WaitForLoaded(widget);
            loaded(widget);
        }
    }
}
#endif
