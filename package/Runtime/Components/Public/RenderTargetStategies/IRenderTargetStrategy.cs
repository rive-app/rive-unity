using UnityEngine;

namespace Rive.Components
{
    /// <summary>
    /// Settings for controlling the timing of when render objects are drawn. 
    /// </summary>
    public enum DrawTimingOption
    {

        /// <summary>
        /// Batch the render objects and draw them all at once. If a panel is requested to be drawn multiple times in a frame, the render objects will be drawn only once on the next frame.
        /// </summary>
        DrawBatched = 0,
        /// <summary>
        /// Draw the render objects immediately. If a panel is requested to be drawn multiple times in a frame, the render objects will be drawn multiple times.
        /// </summary>
        DrawImmediate = 1,
    }
    /// <summary>
    /// Interface for classes that provide a strategy for rendering Rive panels to a render target.
    /// </summary>
    public interface IRenderTargetStrategy
    {
        public DrawTimingOption DrawTiming { get; set; }

        /// <summary>
        /// Registers a panel to be rendered by this strategy.
        /// </summary>
        /// <param name="panel"> The panel to register the render object with. </param>
        /// <returns> True if the render object was successfully registered, false otherwise. </returns>
        bool RegisterPanel(IRivePanel panel);

        /// <summary>
        /// Removes a panel from being rendered by this strategy.
        /// </summary>
        /// <param name="panel"> The panel to remove the render object from. </param>
        /// <returns> True if the render object was successfully removed, false otherwise. </returns>
        bool UnregisterPanel(IRivePanel panel);

        /// <summary>
        /// Returns whether the given panel is registered with this strategy.
        /// </summary>
        /// <param name="panel"> The panel to check for the render object. </param>
        /// <returns> True if the render object is registered, false otherwise. </returns>
        bool IsPanelRegistered(IRivePanel panel);

        /// <summary>
        /// Returns the render texture for the given panel.
        /// </summary>
        /// <param name="panel"> The panel to get the render texture for. </param>
        /// <returns> The render texture for the given render object.</returns>
        RenderTexture GetRenderTexture(IRivePanel panel);

        /// <summary>
        /// Returns the offset for the given panel in UV space.
        /// </summary>
        /// <param name="panel"> The panel to get the offset for. </param>
        /// <returns> The offset for the given panel in UV space. </returns>
        Vector2 GetPanelOffset(IRivePanel panel);

        /// <summary>
        /// Returns the scale for the given panel within the render target.
        /// </summary>
        /// <param name="panel"> The panel to get the scale for. </param>
        /// <returns> The scale for the given panel within the render target. </returns>
        Vector2 GetPanelScale(IRivePanel panel);

        /// <summary>
        /// Draws the given panel to the render target. This should handle being called multiple times in a single frame.
        /// </summary>
        /// <param name="panel"></param>
        void DrawPanel(IRivePanel panel);

        /// <summary>
        /// Triggers when the render target is updated.
        /// </summary>
        event System.Action<IRivePanel> OnRenderTargetUpdated;

        /// <summary>
        /// Triggers when a panel is registered.
        /// </summary>
        /// <param name="panel"></param>
        event System.Action<IRivePanel> OnPanelRegistered;

        /// <summary>
        /// Triggers when a panel is unregistered.
        /// </summary>
        /// <param name="panel"></param>
        event System.Action<IRivePanel> OnPanelUnregistered;
    }
}