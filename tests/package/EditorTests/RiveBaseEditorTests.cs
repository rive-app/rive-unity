using UnityEngine;
using UnityEditor;
using NUnit.Framework;
using UnityEngine.UIElements;
using Rive.EditorTools;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using System;


namespace Rive.Tests.Editor
{

    public class RiveBaseEditorTests
    {
        private static class SectionInfo
        {
            public const string Section1 = "section1";
            public const string Section2 = "section2";

            public const string Section1DisplayName = "Section 1";
            public const string Section2DisplayName = "Section 2";
        }

        [InspectorSection(SectionInfo.Section1, SectionInfo.Section1DisplayName, order: 0)]
        [InspectorSection(SectionInfo.Section2, SectionInfo.Section2DisplayName, order: 1)]
        private class TestComponent : MonoBehaviour
        {
            [SerializeField]
            private string testField;

            [SerializeField]
            [InspectorField(SectionInfo.Section1)]
            private int sectionField;

            [SerializeField]
            [InspectorField(SectionInfo.Section2)]
            private int section2Field;

            [SerializeField]
            [InspectorField(displayName: "Hidden Field", sectionId: SectionInfo.Section1)]
            [HideIf(nameof(IsHidden))]
            private float hiddenFieldWithHideIf;


            [SerializeField]
            [InspectorField(displayName: "Hidden Field", sectionId: SectionInfo.Section1)]
            [ShowIf(nameof(ShouldShow))]
            private float hiddenFieldWithShowIf;


            private int m_callbackCount = 0;
            private bool m_initializedCallback = false;

            [SerializeField]
            [OnValueChanged(nameof(OnValueChanged))]
            private int fieldWithCallback;

            [SerializeField]
            [OnValueChanged(nameof(OnInitCallback), invokeOnInitialization: true)]
            private int fieldWithInitCallback;

            private void OnValueChanged()
            {
                m_callbackCount++;
            }

            private void OnInitCallback()
            {
                m_initializedCallback = true;
                m_callbackCount++;
            }

            // We're testing if the field is hidden when a property returns false
            public bool IsHidden => true;

            // We're testing if the field is hidden when a method returns false
            public bool ShouldShow() => false;


            public string BindingPath_HiddenFieldWithShowIf => nameof(hiddenFieldWithShowIf);

            public string BindingPath_HiddenFieldWithHideIf => nameof(hiddenFieldWithHideIf);

            public string BindingPath_SectionField => nameof(sectionField);

            public string BindingPath_Section2Field => nameof(section2Field);

            public string BindingPath_TestFieldWithoutInspectorFieldAttribute => nameof(testField);


            public int CallbackCount => m_callbackCount;
            public bool InitializedCallback => m_initializedCallback;
            public string BindingPath_FieldWithCallback => nameof(fieldWithCallback);
            public string BindingPath_FieldWithInitCallback => nameof(fieldWithInitCallback);

        }

        [CustomEditor(typeof(TestComponent))]
        private class TestComponentEditor : RiveBaseEditor
        {

        }

        private GameObject m_testObject;
        private TestComponent m_testComponent;
        private RiveBaseEditor m_editor;
        private Scene m_testScene;

        private bool WaitForCondition(Func<bool> condition, int timeoutMs = 1000)
        {
            var startTime = EditorApplication.timeSinceStartup;
            while (EditorApplication.timeSinceStartup - startTime < timeoutMs / 1000f)
            {
                if (condition())
                {
                    return true;
                }
                // Process events to allow the UI to update
                EditorApplication.QueuePlayerLoopUpdate();
            }
            return false;
        }



        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            // Restore the original scene
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        }

        [SetUp]
        public void SetUp()
        {
            // Create and switch to a temporary scene
            m_testScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            Assert.IsTrue(m_testScene.IsValid());
            SceneManager.SetActiveScene(m_testScene);
            Assert.IsTrue(SceneManager.GetActiveScene() == m_testScene);
            Assert.IsTrue(m_testScene.isLoaded);
            Assert.AreEqual(SceneManager.loadedSceneCount, 1);

            m_testObject = new GameObject("Test Object");
            SceneManager.MoveGameObjectToScene(m_testObject, m_testScene);

            m_testComponent = m_testObject.AddComponent<TestComponent>();
            m_editor = UnityEditor.Editor.CreateEditor(m_testComponent) as RiveBaseEditor;


            Assert.IsNotNull(m_testObject, "Failed to create TestObject");
            Assert.IsNotNull(m_testComponent, "Failed to create TestComponent");
            Assert.IsNotNull(m_editor, "Failed to create editor for TestComponent");
        }

        [TearDown]
        public void TearDown()
        {
            if (SceneManager.GetActiveScene() != m_testScene)
            {
                return;
            }

            if (m_editor != null)
            {
                UnityEngine.Object.DestroyImmediate(m_editor);
            }

            if (m_testObject != null)
            {
                UnityEngine.Object.DestroyImmediate(m_testObject);
            }

            // Clean up any other objects that might have been created
            var components = UnityEngine.Object.FindObjectsOfType<TestComponent>();
            foreach (var component in components)
            {
                UnityEngine.Object.DestroyImmediate(component.gameObject);
            }
        }

        private static VisualElement GetFieldElement(VisualElement root, string fieldName, int instanceId)
        {
            var field = root.Q<PropertyField>($"field-{instanceId}-{nameof(TestComponent)}-{fieldName}");
            return field;
        }

        [Test]
        public void CreateInspectorGUI_CreatesExpectedElements()
        {
            var root = m_editor.CreateInspectorGUI();

            Assert.IsNotNull(root);

            // Test that basic field exists using instance ID
            var instanceId = m_testComponent.GetInstanceID();
            var basicField = GetFieldElement(root, m_testComponent.BindingPath_TestFieldWithoutInspectorFieldAttribute, instanceId);

            Assert.IsNotNull(basicField);

            // Test that section exists
            var section = root.Q<VisualElement>(className: StyleHelper.CLASS_SECTION);
            Assert.IsNotNull(section);

            // Test that section field exists
            var sectionField = GetFieldElement(section, m_testComponent.BindingPath_SectionField, instanceId);
            Assert.IsNotNull(sectionField);
        }

        [Test]
        public void ConditionalVisibility_HidesFieldCorrectly()
        {

            var root = m_editor.CreateInspectorGUI();

            var instanceId = m_testComponent.GetInstanceID();
            var hiddenFieldWithHideIf = GetFieldElement(root, m_testComponent.BindingPath_HiddenFieldWithHideIf, instanceId);
            Assert.IsNotNull(hiddenFieldWithHideIf);

            var hiddenFieldWithShowIf = GetFieldElement(root, m_testComponent.BindingPath_HiddenFieldWithShowIf, instanceId);
            Assert.IsNotNull(hiddenFieldWithShowIf);

            Assert.IsTrue(hiddenFieldWithHideIf.resolvedStyle.display == DisplayStyle.None, "Field is not hidden");
            Assert.IsTrue(hiddenFieldWithShowIf.resolvedStyle.display == DisplayStyle.None, "Field is not hidden");
        }



        [Test]
        public void Sections_AreCreatedInCorrectOrder()
        {
            var root = m_editor.CreateInspectorGUI();

            var sections = root.Query<VisualElement>(className: StyleHelper.CLASS_SECTION).ToList();
            Assert.Greater(sections.Count, 0);

            var section1 = sections[0];
            var section2 = sections[1];

            Assert.AreEqual(SectionInfo.Section1DisplayName, section1.Q<Label>().text);
            Assert.AreEqual(SectionInfo.Section2DisplayName, section2.Q<Label>().text);
        }

        [Test]
        public void Fields_HaveCorrectBindings()
        {
            var root = m_editor.CreateInspectorGUI();

            var instanceId = m_testComponent.GetInstanceID();
            var field = GetFieldElement(root, m_testComponent.BindingPath_TestFieldWithoutInspectorFieldAttribute, instanceId);

            IBindable fieldBindable = field as IBindable;

            Assert.IsNotNull(field);
            Assert.IsNotNull(fieldBindable);
            Assert.AreEqual(m_testComponent.BindingPath_TestFieldWithoutInspectorFieldAttribute, fieldBindable.bindingPath);
        }


    }
}