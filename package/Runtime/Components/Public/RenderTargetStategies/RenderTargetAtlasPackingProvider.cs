using UnityEngine;

namespace Rive.Components
{
    /// <summary>
    /// Base class for providing atlas packing strategies to the AtlasRenderTargetStrategy.
    /// Allows customization of how render targets are packed into the atlas texture.
    /// </summary>
    public abstract class RenderTargetAtlasPackingProvider : MonoBehaviour
    {
        /// <summary>
        /// Interface defining the required methods for an atlas packing strategy.
        /// Implementations determine how rectangles are arranged within the atlas texture.
        /// </summary>
        public interface IPackingStrategy
        {
            /// <summary>
            /// Initializes the packing strategy with the given dimensions. If already initialized, resets the packing area and clears any existing rectangles.
            /// </summary>
            /// <param name="width">The width of the packing area</param>
            /// <param name="height">The height of the packing area</param>
            void Initialize(int width, int height);

            /// <summary>
            /// Attempts to insert a rectangle of the given dimensions into the packing area.
            /// </summary>
            /// <param name="width">The width of the rectangle to insert</param>
            /// <param name="height">The height of the rectangle to insert</param>
            /// <param name="rect">The resulting position and dimensions if insertion succeeds</param>
            /// <returns>True if insertion succeeded, false if there was no room</returns>
            bool TryInsert(int width, int height, out RectInt rect);


        }
        /// <summary>
        /// The packing strategy to use when packing the render targets into the atlas.
        /// </summary>
        public abstract IPackingStrategy PackingStrategy { get; }
    }
}
