using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rive.Components
{
    /// <summary>
    /// Represents a panel that can be rendered by a Rive Renderer.
    /// </summary>
    public interface IRivePanel
    {
        /// <summary>
        /// The RectTransform of the panel. It holds the widgets that the panel manages.
        /// </summary>
        RectTransform WidgetContainer { get; }

        /// <summary>
        /// A list of widgets that the panel manages. The list is sorted in the order that the widgets should be rendered in. For example, the back-most widget should be at index 0 and the front-most widget should be at the highest index.
        /// </summary>
        IReadOnlyList<IRiveWidget> Widgets { get; }

        /// <summary>
        /// Whether the panel is currently rendering to a render target.
        /// </summary>
        bool IsRendering { get; }

        /// <summary>
        /// Whether the panel is enabled and its GameObject is active in the hierarchy.
        /// </summary>
        bool Enabled { get; }


        /// <summary>
        /// The RenderTexture that this panel renders to.
        /// </summary>
        RenderTexture RenderTexture { get; }

        /// <summary>
        /// The scale of the panel within its RenderTexture.
        /// </summary>
        Vector2 ScaleInRenderTexture { get; }

        /// <summary>
        /// The offset of the panel within its RenderTexture.
        /// </summary>
        Vector2 OffsetInRenderTexture { get; }



        /// <summary>
        /// Starts rendering the panel to its Render Target.
        /// </summary>
        /// <returns> True if the panel was successfully started rendering, false otherwise. </returns>
        bool StartRendering();

        /// <summary>
        /// Stops rendering the panel to its Render Target.
        /// </summary>
        /// <returns> True if the panel was successfully stopped rendering, false otherwise. </returns>
        bool StopRendering();

        /// <summary>
        /// Registers an input provider for this panel.
        /// </summary>
        /// <param name="inputProvider"> The input provider to register. </param>
        public void RegisterInputProvider(IPanelInputProvider inputProvider);

        /// <summary>
        /// Unregisters an input provider from this panel.
        /// </summary>
        /// <param name="inputProvider"> The input provider to unregister. </param>
        public void UnregisterInputProvider(IPanelInputProvider inputProvider);

        /// <summary>
        /// Event that is triggered when a widget is added to the panel.
        /// </summary>
        event Action<IRiveWidget> OnWidgetAdded;

        /// <summary>
        /// Event that is triggered when a widget is removed from the panel.
        /// </summary>
        event Action<IRiveWidget> OnWidgetRemoved;

        /// <summary>
        /// Event that is triggered when the panel's rendering state changes (starts/stops rendering).
        /// </summary>
        event Action OnRenderingStateChanged;

        /// <summary>
        /// Event that is triggered when the panel's render target strategy changes.
        /// </summary>
        event Action OnRenderTargetStrategyChanged;

        /// <summary>
        /// Event that is triggered when the panel's render target is updated.
        /// </summary>
        event Action OnRenderTargetUpdated;

    }
}