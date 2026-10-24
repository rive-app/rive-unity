using System;
using System.Collections;
using NUnit.Framework;
using Rive.Components;
using Rive.EditorTools;
using Rive.Host;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Rive.Tests
{
    /// <summary>
    /// The data binding playground window, in Play Mode, against MainThread and
    /// BackgroundThread panels: rows follow changes made elsewhere, refreshes
    /// never read from Rive, edits reach Rive, and closing the window drops its
    /// subscriptions. Also a widget whose file was loaded with Load(File)
    /// rather than from its assigned asset.
    /// </summary>
    public class DataBindingPlaygroundTests
    {
        private const string PersonFile = "Packages/app.rive.rive-unity.tests/Shared/Assets/data_binding_test.riv";
        private const string ListFile = "Packages/app.rive.rive-unity.tests/Shared/Assets/db_list_test.riv";
        private const float TimeoutSeconds = 5f;

        [UnityTest]
        public IEnumerator Playground_FollowsChangesAndEdits_WithoutReadingOnRefresh()
        {
            yield return new EnterPlayMode();
            // In its own coroutine so nothing it makes predates the domain reload.
            yield return FollowsChangesAndEdits();
            yield return new ExitPlayMode();
        }

        private static IEnumerator FollowsChangesAndEdits()
        {
            var camera = new GameObject("PlaygroundTestCamera", typeof(Camera));
            DataBindingPlaygroundWindow window = null;
            try
            {
                foreach (ThreadingMode mode in new[] { ThreadingMode.MainThread, ThreadingMode.BackgroundThread })
                {
                    RiveWidget widget = null;
                    yield return LoadWidget(PersonFile, mode, w => widget = w);
                    ViewModelInstance instance = widget.LoadedStateMachine.ViewModelInstance;
                    Assert.IsNotNull(instance, $"{mode}: the person file should bind its view model.");

                    int subscribedBefore = PropertyCallbacksHub.Instance.SubscribedPropertyCountForTests;
                    window = EditorWindow.GetWindow<DataBindingPlaygroundWindow>();
                    window.SetTargetForTests(widget);
                    var age = window.RowControlForTests("age") as FloatField;
                    var agreed = window.RowControlForTests("agreedToTerms") as Toggle;
                    Assert.IsNotNull(age, $"{mode}: there should be a number row for age.");
                    Assert.IsNotNull(agreed, $"{mode}: there should be a toggle row for agreedToTerms.");
                    Assert.Greater(PropertyCallbacksHub.Instance.SubscribedPropertyCountForTests, subscribedBefore,
                        $"{mode}: the rows should subscribe to their properties.");

                    long requests = CommandTransport.RequestsForTests;
                    for (int i = 0; i < 10; i++)
                    {
                        window.RefreshForTests();
                    }
                    Assert.AreEqual(0, CommandTransport.RequestsForTests - requests,
                        $"{mode}: refreshing shouldn't read anything from Rive.");

                    // A change made elsewhere reaches the row through its subscription.
                    instance.GetNumberProperty("age").Value = 33f;
                    yield return Until(() => Mathf.Approximately(age.value, 33f), $"{mode}: the age row should show 33.");

                    // Edits in the window reach Rive.
                    age.value = 44f;
                    agreed.value = !agreed.value;
                    bool expectedAgreed = agreed.value;
                    yield return null;
                    yield return null;
                    Assert.AreEqual(44f, instance.GetNumberProperty("age").Value, $"{mode}: editing the row should set age.");
                    Assert.AreEqual(expectedAgreed, instance.GetBooleanProperty("agreedToTerms").Value,
                        $"{mode}: toggling the row should set agreedToTerms.");

                    window.Close();
                    window = null;
                    Assert.AreEqual(subscribedBefore, PropertyCallbacksHub.Instance.SubscribedPropertyCountForTests,
                        $"{mode}: closing the window should drop its subscriptions.");
                    UnityEngine.Object.Destroy(widget.GetComponentInParent<RivePanel>().gameObject);
                    yield return null;
                }
            }
            finally
            {
                if (window != null)
                {
                    window.Close();
                }
                UnityEngine.Object.Destroy(camera);
            }
        }

        [UnityTest]
        public IEnumerator Playground_ListRow_RereadsOnlyAfterTheListChanges()
        {
            yield return new EnterPlayMode();
            // In its own coroutine so nothing it makes predates the domain reload.
            yield return ListRowRereadsOnChange();
            yield return new ExitPlayMode();
        }

        private static IEnumerator ListRowRereadsOnChange()
        {
            var camera = new GameObject("PlaygroundTestCamera", typeof(Camera));
            DataBindingPlaygroundWindow window = null;
            try
            {
                RiveWidget widget = null;
                yield return LoadWidget(ListFile, ThreadingMode.BackgroundThread, w => widget = w);
                Assert.IsNotNull(widget.LoadedStateMachine, "The list file should have a state machine.");
                ViewModelInstance instance = widget.LoadedStateMachine.ViewModelInstance;
                Assert.IsNotNull(instance, "The list file should bind its view model.");
                ViewModelInstanceListProperty items = instance.GetListProperty("items");
                Assert.IsNotNull(items, "The list file's view model should have an items list.");
                int startCount = items.Count;

                window = EditorWindow.GetWindow<DataBindingPlaygroundWindow>();
                window.SetTargetForTests(widget);
                Assert.AreEqual(startCount, window.ListItemCountForTests("items"), "The list row should start with the list's items.");

                long requests = CommandTransport.RequestsForTests;
                for (int i = 0; i < 10; i++)
                {
                    window.RefreshForTests();
                }
                Assert.AreEqual(0, CommandTransport.RequestsForTests - requests,
                    "An unchanged list shouldn't be read again on refresh.");

                ViewModel itemType = widget.LoadedFile?.GetViewModelByName("TodoItem");
                Assert.IsNotNull(itemType, "The list file should have a TodoItem view model.");
                items.Add(itemType.CreateInstance());
                yield return Until(() =>
                {
                    window.RefreshForTests();
                    return window.ListItemCountForTests("items") == startCount + 1;
                }, "The list row should pick up the added item.");
            }
            finally
            {
                if (window != null)
                {
                    window.Close();
                }
                UnityEngine.Object.Destroy(camera);
            }
        }

        [UnityTest]
        public IEnumerator Playground_EditsUserLoadedFile_WhenItDiffersFromTheAsset()
        {
            yield return new EnterPlayMode();
            // In its own coroutine so nothing it makes predates the domain reload.
            yield return EditsUserLoadedFile();
            yield return new ExitPlayMode();
        }

        private static IEnumerator EditsUserLoadedFile()
        {
            var camera = new GameObject("PlaygroundTestCamera", typeof(Camera));
            DataBindingPlaygroundWindow window = null;
            RiveWidget widget = null;
            File personFile = null;
            try
            {
                // The widget's asset is the list file, so its metadata has no person rows.
                yield return LoadWidget(ListFile, ThreadingMode.MainThread, w => widget = w);

                window = EditorWindow.GetWindow<DataBindingPlaygroundWindow>();
                window.SetTargetForTests(widget);
                Assert.IsNull(window.RowControlForTests("age"), "The list file shouldn't have an age row.");

                // The user loads a different file themselves while the window is open.
                var personAsset = AssetDatabase.LoadAssetAtPath<Asset>(PersonFile);
                Assert.IsNotNull(personAsset, $"Couldn't load {PersonFile}.");
                personFile = File.Load(personAsset);
                widget.Load(personFile);
                yield return Until(() => widget.Status == WidgetStatus.Loaded, "The person file didn't load in time.");
                Assert.AreSame(personFile, widget.LoadedFile);

                ViewModelInstance instance = widget.LoadedStateMachine.ViewModelInstance;
                Assert.IsNotNull(instance, "The person file should bind its view model.");

                window.RefreshForTests();
                var age = window.RowControlForTests("age") as FloatField;
                var agreed = window.RowControlForTests("agreedToTerms") as Toggle;
                Assert.IsNotNull(age, "The window should rebuild with a number row for age.");
                Assert.IsNotNull(agreed, "The window should rebuild with a toggle row for agreedToTerms.");
                Assert.IsTrue(age.enabledSelf, "The age row should be editable.");

                // Edits in the window reach the user's file.
                age.value = 44f;
                agreed.value = !agreed.value;
                bool expectedAgreed = agreed.value;
                yield return null;
                yield return null;
                Assert.AreEqual(44f, instance.GetNumberProperty("age").Value, "Editing the row should set age.");
                Assert.AreEqual(expectedAgreed, instance.GetBooleanProperty("agreedToTerms").Value,
                    "Toggling the row should set agreedToTerms.");

                // Changes made elsewhere show up in the row.
                instance.GetNumberProperty("age").Value = 33f;
                yield return Until(() => Mathf.Approximately(age.value, 33f), "The age row should show 33.");
            }
            finally
            {
                if (window != null)
                {
                    window.Close();
                }
                if (widget != null)
                {
                    UnityEngine.Object.Destroy(widget.GetComponentInParent<RivePanel>().gameObject);
                }
                UnityEngine.Object.Destroy(camera);
            }

            // The widget doesn't own a file passed to Load(File), so we release it once the widget is gone.
            yield return null;
            personFile?.Dispose();
        }

        private static IEnumerator LoadWidget(string path, ThreadingMode mode, Action<RiveWidget> loaded)
        {
            var asset = AssetDatabase.LoadAssetAtPath<Asset>(path);
            Assert.IsNotNull(asset, $"Couldn't load {path}.");
            var panel = new GameObject($"Playground{mode}Panel").AddComponent<RivePanel>();
            panel.ThreadingMode = mode;
            panel.SetDimensions(new Vector2(256, 256));
            var widget = new GameObject("PlaygroundWidget").AddComponent<RiveWidget>();
            widget.transform.SetParent(panel.WidgetContainer, false);
            widget.Load(asset);
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (widget.Status != WidgetStatus.Loaded)
            {
                Assert.AreNotEqual(WidgetStatus.Error, widget.Status, $"{path} failed to load.");
                Assert.Less(Time.realtimeSinceStartup, deadline, $"{path} didn't load in time.");
                yield return null;
            }
            loaded(widget);
        }

        private static IEnumerator Until(Func<bool> done, string failure)
        {
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (!done())
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, failure);
                yield return null;
            }
        }
    }
}
