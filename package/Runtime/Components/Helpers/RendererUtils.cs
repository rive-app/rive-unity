using UnityEngine;

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
            return CreateRenderer(null);
        }

        /// <summary>
        /// Creates a renderer with the given render texture.
        /// </summary>
        /// <param name="renderTexture"> The render texture to use with the renderer. </param>
        /// <returns> The created renderer. </returns>
        public static Renderer CreateRenderer(RenderTexture renderTexture)
        {
            // The pipeline handler hosts the coroutine for delayed Vulkan textures.
            var coroutineHelper = RenderPipelineHelper.CurrentHandler as MonoBehaviour;
            RenderQueue renderQueue = new RenderQueue(renderTexture, true, coroutineHelper);
            return renderQueue.Renderer();
        }

        /// <summary>
        /// Releases the renderer, its underlying render queue, and its resources.
        /// </summary>
        /// <param name="renderer"> The renderer to release. </param>
        public static void ReleaseRenderer(Renderer renderer)
        {
            if (renderer?.RenderQueue == null) return;
            // Render events already queued keep the native queue alive until
            // they've run, so this doesn't wait for the end of the frame.
            renderer.RenderQueue.Dispose();
        }
    }
}
