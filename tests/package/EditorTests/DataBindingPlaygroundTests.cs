using System;
using System.Collections;
using NUnit.Framework;
using Rive.Components;
using Rive.EditorTools;
using Rive.Tests.Utils;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Rive.Tests
{
    /// <summary>
    /// The data binding playground window, in Play Mode, against a widget whose file
    /// was loaded with RiveWidget.Load(File) rather than from its assigned asset.
    /// </summary>
    public class DataBindingPlaygroundTests
    {
        private const float TimeoutSeconds = 5f;

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
                yield return LoadWidget(TestAssetReferences.riv_db_list_test, w => widget = w);

                window = EditorWindow.GetWindow<DataBindingPlaygroundWindow>();
                window.SetTargetForTests(widget);
                Assert.IsNull(window.RowControlForTests("age"), "The list file shouldn't have an age row.");

                // The user loads a different file themselves while the window is open.
                var personAsset = AssetDatabase.LoadAssetAtPath<Asset>(TestAssetReferences.riv_asset_databinding_test);
                Assert.IsNotNull(personAsset, $"Couldn't load {TestAssetReferences.riv_asset_databinding_test}.");
                personFile = File.Load(personAsset);
                widget.Load(personFile);
                yield return Until(() => widget.Status == WidgetStatus.Loaded, "The person file didn't load in time.");
                Assert.AreSame(personFile, widget.File);

                ViewModelInstance instance = widget.StateMachine.ViewModelInstance;
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
                Assert.AreEqual(44f, instance.GetNumberProperty("age").Value, "Editing the row should set age.");
                Assert.AreEqual(expectedAgreed, instance.GetBooleanProperty("agreedToTerms").Value,
                    "Toggling the row should set agreedToTerms.");

                // Changes made elsewhere show up in the row.
                instance.GetNumberProperty("age").Value = 33f;
                window.RefreshForTests();
                Assert.AreEqual(33f, age.value, "The age row should show 33.");
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

        private static IEnumerator LoadWidget(string path, Action<RiveWidget> loaded)
        {
            var asset = AssetDatabase.LoadAssetAtPath<Asset>(path);
            Assert.IsNotNull(asset, $"Couldn't load {path}.");
            var panel = new GameObject("PlaygroundPanel").AddComponent<RivePanel>();
            panel.SetDimensions(new Vector2(256, 256));
            var widget = new GameObject("PlaygroundWidget").AddComponent<RiveWidget>();
            widget.transform.SetParent(panel.WidgetContainer, false);
            widget.Load(asset);
            yield return Until(() =>
            {
                Assert.AreNotEqual(WidgetStatus.Error, widget.Status, $"{path} failed to load.");
                return widget.Status == WidgetStatus.Loaded;
            }, $"{path} didn't load in time.");
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
