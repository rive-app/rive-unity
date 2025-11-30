using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Rive.Components;
using UnityEngine.UIElements;

namespace Rive.Tests.EditorTests
{
    public class MaterialConversionTests
    {
        private const string kTempRoot = "Assets/RiveConversionTests";
        private const string kPluginsPath = "Assets/Plugins";
        private const string kRivePath = "Assets/Plugins/Rive";
        private const string kRiveMaterialsPath = "Assets/Plugins/Rive/Materials";

        private bool m_pluginsFolderPreExisted;
        private bool m_riveFolderPreExisted;
        private bool m_riveMaterialsFolderPreExisted;
        private System.Collections.Generic.HashSet<string> m_preExistingPluginMaterialPaths;

        [SetUp]
        public void SetUp()
        {
            EnsureFolder("Assets");
            EnsureFolder(kTempRoot);
            SnapshotPluginsFolderState();
        }

        /// <summary>
        /// Test that we account for the user moving a material outside the Plugins folder, as long as it has the exact expected name.
        /// </summary>
        [Test]
        public void Reuses_ExactName_Outside_Plugins_When_Found()
        {
            var expectedName = MaterialConversionUtility.Constants.ExpectedLitNameForCurrentPipeline;
            Shader pipelineLitShader =
#if RIVE_USING_URP
                Shader.Find(MaterialConversionUtility.Constants.UnityDefaultShaderURPLit) ?? CreateOrFindDummyShader(MaterialConversionUtility.Constants.UnityDefaultShaderURPLit);
#elif RIVE_USING_HDRP
                Shader.Find(MaterialConversionUtility.Constants.UnityDefaultShaderHDRPLit) ?? CreateOrFindDummyShader(MaterialConversionUtility.Constants.UnityDefaultShaderHDRPLit);
#else
                Shader.Find(MaterialConversionUtility.Constants.UnityDefaultShaderBuiltInLit) ?? CreateOrFindDummyShader(MaterialConversionUtility.Constants.UnityDefaultShaderBuiltInLit);
#endif

            // Ensure plugin copy doesn't override the outside-Plugins candidate.
            var pluginPathPre = $"Assets/Plugins/Rive/Materials/{expectedName}.mat";
            var existingPluginPre = AssetDatabase.LoadAssetAtPath<Material>(pluginPathPre);
            if (existingPluginPre != null)
            {
                AssetDatabase.DeleteAsset(pluginPathPre);
                AssetDatabase.Refresh();
            }

            // Create candidate with exact expected name outside Plugins path.
            var candidatePath = $"{kTempRoot}/{expectedName}.mat";
            var candidateMat = new Material(pipelineLitShader);
            AssetDatabase.CreateAsset(candidateMat, candidatePath);
            AssetDatabase.ImportAsset(candidatePath);

            var go = new GameObject("RiveGO_ReusesExactName");
            try
            {
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = new Material(pipelineLitShader);

                var rtr = go.AddComponent<RiveTextureRenderer>();
                InvokeReset(rtr);

                Assert.AreEqual(candidateMat, mr.sharedMaterial, "Expected reuse of existing exact-name material.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }


        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(kTempRoot);
            AssetDatabase.Refresh();
            CleanupPluginArtifacts();
        }

        /// <summary>
        /// Test that we convert the default material to a Rive lit material when the user adds a RiveTextureRenderer component.
        /// </summary>
        [Test]
        public void Converts_Default_To_RiveLit_On_Reset()
        {
            var expectedName = MaterialConversionUtility.Constants.ExpectedLitNameForCurrentPipeline;

            var go = new GameObject("RiveGO_DefaultConvert");
            try
            {
                var mr = go.AddComponent<MeshRenderer>();
                // Always add a mesh filter to avoid pipeline-specific surprises
                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");

                // Pick the pipeline default shader to simulate Unity defaults
                Shader defaultShader =
#if RIVE_USING_URP
                    Shader.Find(MaterialConversionUtility.Constants.UnityDefaultShaderURPLit) ?? CreateOrFindDummyShader(MaterialConversionUtility.Constants.UnityDefaultShaderURPLit);
#elif RIVE_USING_HDRP
                    Shader.Find(MaterialConversionUtility.Constants.UnityDefaultShaderHDRPLit) ?? CreateOrFindDummyShader(MaterialConversionUtility.Constants.UnityDefaultShaderHDRPLit);
#else
                    Shader.Find(MaterialConversionUtility.Constants.UnityDefaultShaderBuiltInLit) ?? CreateOrFindDummyShader(MaterialConversionUtility.Constants.UnityDefaultShaderBuiltInLit);
#endif
                mr.sharedMaterial = new Material(defaultShader);

                // Add RiveTextureRenderer which triggers Reset() in edit mode.
                var rtr = go.AddComponent<RiveTextureRenderer>();
                // Force a Reset invocation explicitly to be safe.
                InvokeReset(rtr);

                AssertProjectMaterialMatchesExpectation(mr.sharedMaterial, expectedName);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// Test that we create a new plugin material when no exact name match is found either in the Plugins folder or in the project.
        /// </summary>
        [Test]
        public void CreatesPluginMaterial_WhenNoExactNameMatch()
        {
            var expectedName = MaterialConversionUtility.Constants.ExpectedLitNameForCurrentPipeline;
            Shader pipelineLitShader =
#if RIVE_USING_URP
                Shader.Find(MaterialConversionUtility.Constants.UnityDefaultShaderURPLit) ?? CreateOrFindDummyShader(MaterialConversionUtility.Constants.UnityDefaultShaderURPLit);
#elif RIVE_USING_HDRP
                Shader.Find(MaterialConversionUtility.Constants.UnityDefaultShaderHDRPLit) ?? CreateOrFindDummyShader(MaterialConversionUtility.Constants.UnityDefaultShaderHDRPLit);
#else
                Shader.Find(MaterialConversionUtility.Constants.UnityDefaultShaderBuiltInLit) ?? CreateOrFindDummyShader(MaterialConversionUtility.Constants.UnityDefaultShaderBuiltInLit);
#endif

            // Create candidate with matching shader but different name, outside Plugins path.
            var candidatePath = $"{kTempRoot}/Custom_{expectedName}.mat";
            var candidateMat = new Material(pipelineLitShader);
            AssetDatabase.CreateAsset(candidateMat, candidatePath);
            AssetDatabase.ImportAsset(candidatePath);

            var go = new GameObject("RiveGO_ReuseShaderMatch");
            try
            {
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = new Material(pipelineLitShader); // default material that triggers conversion

                var rtr = go.AddComponent<RiveTextureRenderer>();
                InvokeReset(rtr);

                // We always create a new plugin material rather than reusing arbitrary candidates.
                AssertProjectMaterialMatchesExpectation(mr.sharedMaterial, expectedName);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// Test that we do not convert a non-default material when the user adds a RiveTextureRenderer component. We do this to avoid converting materials that the user has explicitly set.
        /// </summary>
        [Test]
        public void NonDefaultMaterial_IsNotConverted()
        {

            var go = new GameObject("RiveGO_NoConvert");
            try
            {
                var mr = go.AddComponent<MeshRenderer>();
                // Use a custom shader name that is not a default one (so it does not match converter's rules).
                var customShader = CreateOrFindDummyShader("Hidden/Rive/NotDefault");
                var customMat = new Material(customShader);
                mr.sharedMaterial = customMat;

                var rtr = go.AddComponent<RiveTextureRenderer>();
                InvokeReset(rtr);

                Assert.AreEqual(customMat, mr.sharedMaterial, "Non-default material should not be converted.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // Helpers
        private static void AssertProjectMaterialMatchesExpectation(Material mat, string expectedBaseName)
        {
            Assert.IsNotNull(mat, "Converted material should not be null.");
            var path = AssetDatabase.GetAssetPath(mat);
            Assert.IsTrue(path.StartsWith("Assets/Plugins/Rive/Materials"), $"Converted material should live under 'Assets/Plugins/Rive/Materials', but was '{path}'.");
            // Name should be exact or Unity-appended with a number suffix (e.g., "Name 1").
            Assert.IsTrue(mat.name == expectedBaseName || mat.name.StartsWith(expectedBaseName + " "), $"Converted material name '{mat.name}' should start with '{expectedBaseName}'.");
        }

        private static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        private void SnapshotPluginsFolderState()
        {
            // Track pre-existing plugin folders and assets so we can cleanup safely
            m_pluginsFolderPreExisted = AssetDatabase.IsValidFolder(kPluginsPath);
            m_riveFolderPreExisted = AssetDatabase.IsValidFolder(kRivePath);
            m_riveMaterialsFolderPreExisted = AssetDatabase.IsValidFolder(kRiveMaterialsPath);
            m_preExistingPluginMaterialPaths = new System.Collections.Generic.HashSet<string>();
            if (m_riveMaterialsFolderPreExisted)
            {
                var existingGuids = AssetDatabase.FindAssets("", new[] { kRiveMaterialsPath });
                for (int i = 0; i < existingGuids.Length; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(existingGuids[i]);
                    m_preExistingPluginMaterialPaths.Add(path);
                }
            }
        }

        private void CleanupPluginArtifacts()
        {
            // Remove plugin materials this test run may have created
            if (AssetDatabase.IsValidFolder(kRiveMaterialsPath))
            {
                string litPrefix = MaterialConversionUtility.Constants.ExpectedLitNameForCurrentPipeline;
                string unlitPrefix = MaterialConversionUtility.Constants.ExpectedUnlitNameForCurrentPipeline;
                var guids = AssetDatabase.FindAssets("", new[] { kRiveMaterialsPath });
                for (int i = 0; i < guids.Length; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (m_preExistingPluginMaterialPaths.Contains(path))
                    {
                        continue;
                    }
                    var filename = System.IO.Path.GetFileNameWithoutExtension(path) ?? string.Empty;
                    if (filename.StartsWith(litPrefix) || filename.StartsWith(unlitPrefix))
                    {
                        AssetDatabase.DeleteAsset(path);
                    }
                }
                AssetDatabase.Refresh();
            }
            // Delete folders only if they didn't exist before and are now empty
            RemoveFolderIfCreatedAndEmpty(kRiveMaterialsPath, m_riveMaterialsFolderPreExisted);
            RemoveFolderIfCreatedAndEmpty(kRivePath, m_riveFolderPreExisted);
            RemoveFolderIfCreatedAndEmpty(kPluginsPath, m_pluginsFolderPreExisted);
        }

        private static void RemoveFolderIfCreatedAndEmpty(string folderPath, bool preExisted)
        {
            if (preExisted || !AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }
            var remaining = AssetDatabase.FindAssets("", new[] { folderPath });
            if (remaining == null || remaining.Length == 0)
            {
                AssetDatabase.DeleteAsset(folderPath);
                AssetDatabase.Refresh();
            }
        }

        private static Shader CreateOrFindDummyShader(string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader != null)
            {
                return shader;
            }
            // Create a minimal unlit shader with the provided name.
            var shaderPath = $"{kTempRoot}/{SanitizeFilename(shaderName)}.shader";
            var code = "Shader \"" + shaderName + "\" {\n" +
                       " SubShader { Tags { \"RenderType\"=\"Opaque\" } Pass { } }\n" +
                       "}";
            System.IO.File.WriteAllText(shaderPath, code);
            AssetDatabase.ImportAsset(shaderPath);
            return AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
        }

        private static string SanitizeFilename(string name)
        {
            foreach (var c in System.IO.Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name.Replace('/', '_');
        }

        private static void InvokeReset(RiveTextureRenderer rtr)
        {
            // Unity calls Reset when adding a component in editor; invoke explicitly for tests.
            var method = typeof(RiveTextureRenderer).GetMethod("Reset", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            method?.Invoke(rtr, null);
        }

        private static void RunEditorButtonConversionTest(string pipelineFolder, string[] candidateMaterialNames)
        {
            var expectedName = MaterialConversionUtility.Constants.ExpectedLitNameForCurrentPipeline;

            var go = new GameObject($"RiveGO_Button_{pipelineFolder}");
            try
            {
                var mr = go.AddComponent<MeshRenderer>();
                // Start with a non-default shader so Reset doesn't convert.
                var nonDefault = CreateOrFindDummyShader("Hidden/Rive/NotDefaultForButton");
                mr.sharedMaterial = new Material(nonDefault);

                var rtr = go.AddComponent<RiveTextureRenderer>();

                var editor = Editor.CreateEditor(rtr);
                try
                {
                    var root = (editor as Editor).CreateInspectorGUI();
                    Assert.IsNotNull(root, "Inspector root should not be null.");
                    var button = root.Q<Button>("RiveConvertMaterialsButton");
                    Assert.IsNotNull(button, "Convert button should be present in the inspector.");

                    var action = button.userData as System.Action;
                    Assert.IsNotNull(action, "Convert button should have an assigned action in userData.");
                    action.Invoke();

                    AssertProjectMaterialMatchesExpectation(mr.sharedMaterial, expectedName);
                }
                finally
                {
                    Object.DestroyImmediate(editor);
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void EditorButton_Assigns_Lit_When_RendererHasNoMaterials()
        {
            var expectedName = MaterialConversionUtility.Constants.ExpectedLitNameForCurrentPipeline;
            var go = new GameObject("RiveGO_Button_NoMats");
            try
            {
                var mr = go.AddComponent<MeshRenderer>();
                // Ensure no materials are assigned
                mr.sharedMaterials = new Material[0];

                var rtr = go.AddComponent<RiveTextureRenderer>();
                var editor = Editor.CreateEditor(rtr);
                try
                {
                    var root = (editor as Editor).CreateInspectorGUI();
                    Assert.IsNotNull(root, "Inspector root should not be null.");
                    var button = root.Q<Button>("RiveConvertMaterialsButton");
                    Assert.IsNotNull(button, "Convert button should be present in the inspector.");
                    var action = button.userData as System.Action;
                    Assert.IsNotNull(action, "Convert button should have an assigned action in userData.");

                    action.Invoke();

                    // After invocation, expect a lit project material to be assigned
                    AssertProjectMaterialMatchesExpectation(mr.sharedMaterial, expectedName);
                }
                finally
                {
                    Object.DestroyImmediate(editor);
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void EditorButton_Converts_On_Click()
        {
            string pipelineFolder =
#if RIVE_USING_URP
                MaterialConversionUtility.Constants.URPFolder;
#elif RIVE_USING_HDRP
                MaterialConversionUtility.Constants.HDRPFolder;
#else
                MaterialConversionUtility.Constants.BuiltInFolder;
#endif
            RunEditorButtonConversionTest(
                pipelineFolder: pipelineFolder,
                candidateMaterialNames: new[]
                {
                    MaterialConversionUtility.Constants.ExpectedLitNameForCurrentPipeline
                });
        }
    }
}


