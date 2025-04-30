using UnityEngine;

namespace Rive.Components
{
    /// <summary>
    /// Interface for a render pipeline handler. The render pipeline handler is responsible for registering and unregistering renderers with the render pipeline.
    /// </summary>
    public interface IRenderPipelineHandler
    {
        /// <summary>
        /// Registers a renderer with the handler. This tells the render pipeline to execute the renderer's render commands.
        /// </summary>
        /// <param name="renderer"> The Rive renderer to register</param>
        void Register(IRenderer renderer);

        /// <summary>
        /// Unregisters a renderer with the handler. This tells the render pipeline to stop executing the renderer's render commands.
        /// </summary>
        /// <param name="renderer"> The Rive renderer to unregister</param>
        void Unregister(IRenderer renderer);

        /// <summary>
        /// Returns true if the renderer is registered with the handler.
        /// </summary>
        /// <param name="renderer"> The Rive renderer to check</param>
        /// <returns> True if the renderer is registered with the handler</returns>
        bool IsRendererRegistered(IRenderer renderer);

        /// <summary>
        /// Allocates a render texture with the specified width and height.
        /// </summary>
        /// <param name="width"> The width of the render texture</param>
        /// <param name="height"> The height of the render texture</param>
        /// <returns> The allocated render texture</returns>
        RenderTexture AllocateRenderTexture(int width, int height);


        /// <summary>
        /// Releases a render texture.
        /// </summary>
        /// <param name="renderTexture"> The render texture to release</param>
        void ReleaseRenderTexture(RenderTexture renderTexture);

        /// <summary>
        /// Resize a render texture to the specified width and height.
        /// </summary>
        /// <param name="renderTexture"> The render texture to resize</param>
        /// <param name="width"> The new width of the render texture</param>
        /// <param name="height"> The new height of the render texture</param>
        /// <returns></returns>
        RenderTexture ResizeRenderTexture(RenderTexture renderTexture, int width, int height);

        /// <summary>
        /// Sets the render texture for the specified renderer. This tells the render pipeline handler to use the specified render texture for rendering.
        /// </summary>
        /// <param name="renderer"> The Rive renderer to set the render texture for</param>
        /// <param name="renderTexture"> The render texture to set</param>
        void SetRendererTexture(IRenderer renderer, RenderTexture renderTexture);

    }
}