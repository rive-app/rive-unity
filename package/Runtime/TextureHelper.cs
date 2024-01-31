using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Rive
{
    public static class TextureHelper
    {
        /// <summary>
        /// Returns a compatible GraphicsFormat for creating RenderTextures that
        /// work with Rive's Renderer.
        /// </summary>
        public static GraphicsFormat Format => GraphicsFormat.R8G8B8A8_UNorm;

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
    }
}
