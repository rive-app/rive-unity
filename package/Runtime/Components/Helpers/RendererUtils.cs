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
            RendererCleanupHelper cleanupHelper = RendererCleanupHelper.Instance;

            // The cleanup helper doubles as the coroutine host.
            MonoBehaviour coroutineHelper = cleanupHelper != null
                ? cleanupHelper
                : RenderPipelineHelper.CurrentHandler as MonoBehaviour;

            RenderQueue renderQueue = new RenderQueue(renderTexture, true, coroutineHelper);

            Renderer renderer = renderQueue.Renderer();

            // Track the renderer so it can be disposed deterministically on application/editor quit,
            // before Unity tears down the graphics device.
            cleanupHelper?.Register(renderer);

            return renderer;
        }

        /// <summary>
        /// Releases the renderer, its underlying render queue, and its resources.
        /// </summary>
        /// <param name="renderer"> The renderer to release. </param>
        public static void ReleaseRenderer(Renderer renderer)
        {
            if (renderer?.RenderQueue == null) return;

            RendererCleanupHelper cleanupHelper = RendererCleanupHelper.Instance;

            if (Application.isPlaying && cleanupHelper != null)
            {
                // Deferred (end-of-frame) release while playing, or synchronous if already quitting.
                cleanupHelper.ReleaseRenderer(renderer);
            }
            else
            {
                // In edit mode (or when no helper exists) we can't use coroutines, so we release the
                // renderer immediately.
                cleanupHelper?.Unregister(renderer);
                renderer.RenderQueue.Dispose();
            }
        }
    }
}
