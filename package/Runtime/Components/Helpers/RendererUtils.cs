using System.Collections;
using UnityEngine;

namespace Rive.Components.Utilities
{

    internal class RendererUtils
    {
        static WaitForEndOfFrame s_waitForEndOfFrame = new WaitForEndOfFrame();


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

            if (renderer?.RenderQueue == null) return;

            // We need a monobehaviour to start a coroutine , so we use the current handler
            // We could also create a new GameObject to start the coroutine, but for now, we know that there's a handler already for each render pipeline so we use that
            // In the future, we should consider making a dedicated GameObject for this.
            MonoBehaviour coroutineHelper = RenderPipelineHelper.CurrentHandler as MonoBehaviour;

            if (Application.isPlaying && coroutineHelper != null)
            {
                coroutineHelper.StartCoroutine(DeferredRelease(renderer));
            }
            else
            {
                // In edit mode, we can't use coroutines, so we release the renderer immediately.
                renderer.RenderQueue.Dispose();
            }

        }


        private static IEnumerator DeferredRelease(Renderer renderer)
        {
            // Wait for render thread to complete current frame to release the renderer
            // This helps us avoid issues/crashes when disposing the native pointer before the camera is done rendering.

            yield return s_waitForEndOfFrame;

            if (renderer?.RenderQueue != null)
            {
                renderer.RenderQueue.Dispose();
            }
        }
    }
}