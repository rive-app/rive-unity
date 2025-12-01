using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Rive.Components
{

    /// <summary>
    /// Helper class for converting default RP materials to Rive variants.
    /// </summary>
    internal static class MaterialConversionUtility
    {
#if UNITY_EDITOR
        // Exposed constants so tests and editor code can reference folder and name conventions from one place.
        internal static class Constants
        {
            public const string PackageName = Rive.EditorTools.PackageInfo.PACKAGE_NAME;
            public const string RPFolderRoot = "RenderPipelines";
            public const string URPFolder = "URP";
            public const string HDRPFolder = "HDRP";
            public const string BuiltInFolder = "BuiltIn";
            public const string MaterialsFolder = "Materials";

            public const string BaseRiveLit = "RiveLit";
            public const string BaseRiveUnlit = "RiveUnlit";

            public const string SuffixURP = "URP";
            public const string SuffixHDRP = "HDRP";
            public const string SuffixBiRP = "BiRP";

            // Unity default shader names for detection (used to identify pipeline defaults)
            public const string UnityDefaultShaderURPLit = "Universal Render Pipeline/Lit";
            public const string UnityDefaultShaderURPUnlit = "Universal Render Pipeline/Unlit";
            public const string UnityDefaultShaderHDRPLit = "HDRP/Lit";
            public const string UnityDefaultShaderHDRPUnlit = "HDRP/Unlit";
            public const string UnityDefaultShaderBuiltInLit = "Standard";
            public const string UnityDefaultShaderBuiltInUnlitPrefix = "Unlit/";

            // Expected material names for the current render pipeline
            internal static string ExpectedLitNameForCurrentPipeline
            {
                get
                {
                    return BaseRiveLit + GetCurrentPipelineSuffix();
                }
            }
            internal static string ExpectedUnlitNameForCurrentPipeline
            {
                get
                {
                    return BaseRiveUnlit + GetCurrentPipelineSuffix();
                }
            }

            private static string GetCurrentPipelineSuffix()
            {
                switch (GetCurrentPipeline())
                {
                    case Pipeline.URP:
                        return SuffixURP;
                    case Pipeline.HDRP:
                        return SuffixHDRP;
                    default:
                        return SuffixBiRP;
                }
            }
        }

        private enum Pipeline
        {
            BuiltIn = 0,
            URP = 1,
            HDRP = 2,
        }

        private static Pipeline GetCurrentPipeline()
        {
            var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            if (rp == null)
            {
                return Pipeline.BuiltIn;
            }
            var typeName = rp.GetType().FullName;
            if (!string.IsNullOrEmpty(typeName))
            {
                if (typeName.Contains("UniversalRenderPipelineAsset"))
                {
                    return Pipeline.URP;
                }
                if (typeName.Contains("HDRenderPipelineAsset"))
                {
                    return Pipeline.HDRP;
                }
            }
            return Pipeline.BuiltIn;
        }

        // ---------------------------
        // Project-material resolution
        // ---------------------------
        private static Material s_cachedLitMaterial;
        private static Material s_cachedUnlitMaterial;

        private static string GetTargetFolderPath()
        {
            return "Assets/Plugins/Rive/Materials";
        }

        private static string GetBaseName(bool lit)
        {
            return lit ? Constants.BaseRiveLit : Constants.BaseRiveUnlit;
        }

        private static string GetRPFolder(out string suffix)
        {
            switch (GetCurrentPipeline())
            {
                case Pipeline.URP:
                    suffix = Constants.SuffixURP;
                    return Constants.URPFolder;
                case Pipeline.HDRP:
                    suffix = Constants.SuffixHDRP;
                    return Constants.HDRPFolder;
                default:
                    suffix = Constants.SuffixBiRP;
                    return Constants.BuiltInFolder;
            }
        }

        private static void EnsureAssetFolder(string path)
        {
            var parts = path.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets") return;
            string current = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        private static Shader LoadShaderFromRenderPipelinesFolder(string rpFolder, bool lit, out string resolvedName)
        {
            // Attempt to load shader directly from RP ShaderGraph asset
            string litOrUnlit = lit ? "Lit" : "Unlit";
            string shaderGraphPath = $"Packages/{Constants.PackageName}/Runtime/Components/Public/RenderPipelines/{rpFolder}/Shaders/{litOrUnlit}.shadergraph";
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderGraphPath);
            if (shader != null)
            {
                resolvedName = shader.name;
                return shader;
            }
            // Fallback search within the RP Shaders folder for any Shader assets and pick by name hint
            string folder = $"Packages/{Constants.PackageName}/Runtime/Components/Public/RenderPipelines/{rpFolder}/Shaders";
            string[] guids = AssetDatabase.FindAssets("t:Shader", new[] { folder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var s = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (s != null && s.name.IndexOf(litOrUnlit, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    resolvedName = s.name;
                    return s;
                }
            }
            resolvedName = $"<missing RP shader {rpFolder}/{litOrUnlit}>";
            return null;
        }

        private static Shader GetPipelineShader(bool lit, out string shaderName)
        {
            switch (GetCurrentPipeline())
            {
                case Pipeline.URP:
                    return LoadShaderFromRenderPipelinesFolder(Constants.URPFolder, lit, out shaderName);
                case Pipeline.HDRP:
                    return LoadShaderFromRenderPipelinesFolder(Constants.HDRPFolder, lit, out shaderName);
                default:
                    return LoadShaderFromRenderPipelinesFolder(Constants.BuiltInFolder, lit, out shaderName);
            }

        }

        private static System.Collections.Generic.List<Material> FindProjectMaterialsUsingShader(Shader shader)
        {
            var results = new System.Collections.Generic.List<Material>();
            if (shader == null)
            {
                return results;
            }
            var guids = AssetDatabase.FindAssets("t:material", new[] { "Assets" });
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat != null && mat.shader == shader)
                {
                    results.Add(mat);
                }
            }
            results.Sort((a, b) =>
            {
                string ta = AssetDatabase.GetAssetPath(a);
                string tb = AssetDatabase.GetAssetPath(b);
                bool aPrefer = ta.StartsWith(GetTargetFolderPath());
                bool bPrefer = tb.StartsWith(GetTargetFolderPath());
                if (aPrefer == bPrefer) return string.Compare(ta, tb, System.StringComparison.OrdinalIgnoreCase);
                return aPrefer ? -1 : 1;
            });
            return results;
        }

        private static bool ShaderMatches(Shader candidate, Shader target, string targetName)
        {
            if (candidate == null)
            {
                return false;
            }
            if (target != null && candidate == target)
            {
                return true;
            }
            if (!string.IsNullOrEmpty(targetName) &&
                string.Equals(candidate.name, targetName, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Resolve or create a project-local Rive material with exact-name-first strategy:
        /// 1) Use Assets/Plugins/Rive/Materials/&lt;Base+Suffix&gt;.mat if present.
        /// 2) Else use any material in Assets with that exact name and matching shader.
        /// 3) Else, if materials exist with the matching shader, optionally prompt to use suggested; otherwise create.
        /// 4) Else create under Assets/Plugins/Rive/Materials with a unique name.
        /// </summary>
        private static Material LoadOrCreateProjectRiveMaterial(bool lit)
        {
            if (lit && s_cachedLitMaterial != null) return s_cachedLitMaterial;
            if (!lit && s_cachedUnlitMaterial != null) return s_cachedUnlitMaterial;

            string suffix;
            string rpFolder = GetRPFolder(out suffix);
            string expectedName = GetBaseName(lit) + suffix;

            // 1) Exact expected path in Assets/Plugins/Rive/Materials
            string targetFolder = GetTargetFolderPath();
            string targetPath = $"{targetFolder}/{expectedName}.mat";
            var atTarget = AssetDatabase.LoadAssetAtPath<Material>(targetPath);
            if (atTarget != null)
            {
                if (lit) s_cachedLitMaterial = atTarget; else s_cachedUnlitMaterial = atTarget;
                return atTarget;
            }

            // Resolve shader
            var shader = GetPipelineShader(lit, out var shaderName);
            if (shader == null)
            {
                Debug.LogWarning($"Rive: Could not resolve shader for current render pipeline (lit={lit}).");
            }

            // 2) Exact-name anywhere in Assets with matching shader
            string[] nameMatches = AssetDatabase.FindAssets($"t:material {expectedName}", new[] { "Assets" });
            for (int i = 0; i < nameMatches.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(nameMatches[i]);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                // If the user has a material with the exact expected name, prefer it regardless of shader;
                // this allows overriding shader source differences (Graph vs HLSL) while honoring the name contract.
                if (mat != null && mat.name == expectedName)
                {
                    if (lit) s_cachedLitMaterial = mat; else s_cachedUnlitMaterial = mat;
                    return mat;
                }
            }

            // 3) If no exact-name match is found, always create a new material in the Plugins folder.

            // 4) Create new in Assets/Plugins/Rive/Materials with unique name if needed
            EnsureAssetFolder(targetFolder);
            string basePath = $"{targetFolder}/{expectedName}.mat";
            string uniquePath = AssetDatabase.GenerateUniqueAssetPath(basePath);

            Material toSave = new Material(shader != null ? shader : Shader.Find("Hidden/InternalErrorShader"));

            AssetDatabase.CreateAsset(toSave, uniquePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(uniquePath);

            if (lit) s_cachedLitMaterial = toSave; else s_cachedUnlitMaterial = toSave;
            return toSave;
        }


        /// <summary>
        /// Ensures that the given renderer has Rive materials assigned for any default materials. Unlike ReplaceMaterialsWithRive, this only replaces materials that appear to be Unity default materials that Unity would assign automatically.
        /// </summary>
        /// <param name="renderer"></param>
        internal static void EnsureRiveMaterialsOnRenderer(UnityEngine.Renderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            var materials = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                var mat = materials[i];
                if (mat == null || mat.shader == null)
                {
                    continue;
                }
                string shaderName = mat.shader.name;
                bool isDefaultLit = false;
                bool isDefaultUnlit = false;


                switch (GetCurrentPipeline())
                {
                    case Pipeline.URP:
                        isDefaultLit = shaderName == Constants.UnityDefaultShaderURPLit ||
                                       shaderName == "Universal Render Pipeline/Simple Lit";
                        isDefaultUnlit = shaderName == Constants.UnityDefaultShaderURPUnlit;
                        break;
                    case Pipeline.HDRP:
                        isDefaultLit = shaderName == Constants.UnityDefaultShaderHDRPLit;
                        isDefaultUnlit = shaderName == Constants.UnityDefaultShaderHDRPUnlit;
                        break;
                    default:
                        isDefaultLit = shaderName == Constants.UnityDefaultShaderBuiltInLit;
                        isDefaultUnlit = shaderName.StartsWith(Constants.UnityDefaultShaderBuiltInUnlitPrefix);
                        break;
                }

                if (isDefaultLit || isDefaultUnlit)
                {
                    var target = LoadOrCreateProjectRiveMaterial(lit: !isDefaultUnlit);
                    if (target != null && target != mat)
                    {
                        materials[i] = target;
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                renderer.sharedMaterials = materials;
                EditorUtility.SetDirty(renderer);
            }
        }
        /// <summary>
        /// Replaces any non-Rive materials on the given renderer with Rive equivalents. Unlike EnsureRiveMaterialsOnRenderer, this will replace any material that is not already a Rive material, even if it's a custom material.
        /// </summary>
        /// <param name="renderer"></param>
        internal static void ReplaceMaterialsWithRive(UnityEngine.Renderer renderer)
        {
            if (renderer == null)
            {
                return;
            }
            var materials = renderer.sharedMaterials;
            // If there are no materials at all, assign a single lit material.
            if (materials == null || materials.Length == 0)
            {
                var targetIfNone = LoadOrCreateProjectRiveMaterial(lit: true);
                if (targetIfNone != null)
                {
                    renderer.sharedMaterials = new[] { targetIfNone };
                    EditorUtility.SetDirty(renderer);
                }
                return;
            }
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                var mat = materials[i];
                if (mat == null || mat.shader == null)
                {
                    // Replace missing/invalid material with a lit material by default.
                    var defaultLit = LoadOrCreateProjectRiveMaterial(lit: true);
                    if (defaultLit != null)
                    {
                        materials[i] = defaultLit;
                        changed = true;
                    }
                    continue;
                }
                string shaderName = mat.shader.name ?? string.Empty;
                bool appearsUnlit =
                    shaderName.IndexOf("unlit", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    shaderName.StartsWith("Unlit/", System.StringComparison.OrdinalIgnoreCase);

                var target = LoadOrCreateProjectRiveMaterial(lit: !appearsUnlit);
                if (target != null && target != mat)
                {
                    materials[i] = target;
                    changed = true;
                }
            }
            if (changed)
            {
                renderer.sharedMaterials = materials;
                EditorUtility.SetDirty(renderer);
            }
        }
#endif
    }
}


