using UnityEngine;
using UnityEngine.Pool;

namespace Rive.Components.Utilities
{

    internal class RendererUtils
    {
        /// <summary>
        /// Creates a renderer without a backing render texture.
        /// </summary>
        /// <returns> The created renderer. </returns>
        public static Renderer CreateRenderer()
        {
            RenderQueue renderQueue = new RenderQueue(null, true);

            return renderQueue.Renderer();
        }

        /// <summary>
        /// Creates a renderer with the given render texture.
        /// </summary>
        /// <param name="renderTexture"> The render texture to use with the renderer. </param>
        /// <returns> The created renderer. </returns>
        public static Renderer CreateRenderer(RenderTexture renderTexture)
        {
            RenderQueue renderQueue = new RenderQueue(renderTexture, true);

            return renderQueue.Renderer();
        }

        /// <summary>
        /// Releases the renderer, its underlying render queue, and its resources.
        /// </summary>
        /// <param name="renderer"> The renderer to release. </param>
        public static void ReleaseRenderer(Renderer renderer)
        {
            renderer.RenderQueue.Dispose();
        }
    }
}