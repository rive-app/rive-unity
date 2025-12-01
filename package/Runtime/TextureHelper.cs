using Rive.Utils;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Rive
{
    public static class TextureHelper
    {
        /// <summary>
        /// Returns a compatible GraphicsFormat for creating RenderTextures that
        /// work with Rive's Renderer.
        /// </summary>
        public static GraphicsFormat Format => GraphicsFormat.R8G8B8A8_UNorm;

        private static Material s_gammaToLinearUIMaterial;

        const string GAMMA_TO_LINEAR_UI_SHADER_NAME = "Rive/UI/Default";

        /// <summary>
        /// Lazily creates and returns a material that decodes gamma content to linear
        /// Used to display Rive's RenderTexture correctly in Linear color space without relying on sRGB RenderTextures (which are unreliable on some backends).
        /// </summary>
        internal static Material GammaToLinearUIMaterial
        {
            get
            {
                if (s_gammaToLinearUIMaterial == null)
                {
                    var shader = Shader.Find(GAMMA_TO_LINEAR_UI_SHADER_NAME);
                    if (shader != null)
                    {
                        s_gammaToLinearUIMaterial = new Material(shader)
                        {
                            name = "Rive_UI_Default",
                            hideFlags = HideFlags.HideAndDontSave
                        };
                    }
                    else
                    {
                        DebugLogger.Instance.LogError($"Shader '{GAMMA_TO_LINEAR_UI_SHADER_NAME}' not found.");
                    }
                }

                return s_gammaToLinearUIMaterial;
            }
        }

        /// <summary>
        /// Whether we should decode Gamma content to linear when displaying Rive RenderTextures.
        /// </summary>
        internal static bool ProjectNeedsColorSpaceFix
        {
            get
            {
                return QualitySettings.activeColorSpace == ColorSpace.Linear;
            }
        }

        /// <summary>
        ///  Returns a RenderTexture descriptor guaranteed to be compatible with
        ///  Rive's Renderer.
        /// </summary>
        /// <param name="width">The width of the texture in pixels.</param>
        /// <param name="height">The height of the texture in pixels.</param>
        /// <returns></returns>
        public static RenderTextureDescriptor Descriptor(int width, int height)
        {
            return new RenderTextureDescriptor(width, height, Format, 0)
            {
                enableRandomWrite =
                    UnityEngine.SystemInfo.graphicsDeviceType
                    == UnityEngine.Rendering.GraphicsDeviceType.Direct3D11
            };
        }

        /// <summary>
        /// Determines if the texture should be flipped based on the graphics API.
        /// </summary>
        /// <returns></returns>
        public static bool ShouldFlipTexture()
        {
            switch (SystemInfo.graphicsDeviceType)
            {
                case GraphicsDeviceType.Metal:
                case GraphicsDeviceType.Vulkan:
                case GraphicsDeviceType.Direct3D11:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsOpenGLPlatform()
        {
            GraphicsDeviceType deviceType = SystemInfo.graphicsDeviceType;
            bool isOpenGL = deviceType == GraphicsDeviceType.OpenGLCore ||
                           deviceType == GraphicsDeviceType.OpenGLES3;

#if !UNITY_2023_1_OR_NEWER
            // OpenGLES2 is not supported in Unity 2023.1 and newer
            isOpenGL = isOpenGL || deviceType == GraphicsDeviceType.OpenGLES2;
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
    isOpenGL = true;
#endif

            return isOpenGL;
        }

        public static bool IsDirect3DPlatform()
        {
            return SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11 || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D12;
        }

        /// <summary>
        /// Determines if the input should be flipped based on the graphics API.
        /// </summary>
        /// <returns> True if the input should be flipped, false otherwise. </returns>
        public static bool ShouldFlipInput()
        {
            // OpenGL platforms require flipping the input, even if the texture doesn't need to be flipped.
            if (IsOpenGLPlatform())
            {
                return true;
            }

            return ShouldFlipTexture();
        }

        internal static bool NeedsRenderTargetSetOnCommandBuffer()
        {
            // Direct3D11 requires that we call SetRenderTarget on the command buffer. Otherwise, nothing will be rendered.
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11)
            {
                return true;
            }

            return false;
        }

    }
}
