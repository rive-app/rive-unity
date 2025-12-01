using UnityEngine;

namespace Rive.Tests.Utils
{
    /// <summary>
    /// Holds a reference to Rive shaders for different render pipelines for use in tests in the Unity Editor and in Builds.
    /// </summary>
    [CreateAssetMenu(fileName = "MaterialRegistry", menuName = "Rive/Tests/Material Registry")]
    public class MaterialRegistry : ScriptableObject
    {
        [Header("Built-in RP")]
        public Shader RiveLitBiRP;
        public Shader RiveUnlitBiRP;

        [Header("URP")]
        public Shader RiveLitURP;
        public Shader RiveUnlitURP;

        [Header("HDRP")]
        public Shader RiveLitHDRP;
        public Shader RiveUnlitHDRP;

        /// <summary>
        /// Gets the appropriate lit shader for the current render pipeline.
        /// </summary>
        public Shader GetLitShaderForCurrentRP()
        {
            var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            if (rp == null) return RiveLitBiRP;
            var type = rp.GetType().FullName ?? "";
            if (type.Contains("UniversalRenderPipelineAsset")) return RiveLitURP;
            if (type.Contains("HDRenderPipelineAsset")) return RiveLitHDRP;
            return RiveLitBiRP;
        }

        /// <summary>
        /// Gets the appropriate unlit shader for the current render pipeline.
        /// </summary>
        public Shader GetUnlitShaderForCurrentRP()
        {
            var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            if (rp == null) return RiveUnlitBiRP;
            var type = rp.GetType().FullName ?? "";
            if (type.Contains("UniversalRenderPipelineAsset")) return RiveUnlitURP;
            if (type.Contains("HDRenderPipelineAsset")) return RiveUnlitHDRP;
            return RiveUnlitBiRP;
        }
    }
}