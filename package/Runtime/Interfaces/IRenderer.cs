using UnityEngine.Rendering;

namespace Rive
{
    /// <summary>
    /// Interface for rendering Rive content to a render queue
    /// </summary>
    public interface IRenderer
    {

        /// <summary>
        /// Clear the commands in the render queue
        /// </summary>
        void Clear();

        /// <summary>
        /// Draw the given artboard to the render queue
        /// </summary>
        /// <param name="artboard">The artboard to draw</param>
        void Draw(Artboard artboard);

        /// <summary>
        /// Draw the given path and paint to the render queue
        /// </summary>
        /// <param name="path">The path to draw</param>
        /// <param name="paint">The paint to apply to the path</param>
        void Draw(Path path, Paint paint);

        /// <summary>
        /// Clip the render queue to the given path
        /// </summary>
        /// <param name="path">The path to use as a clip mask</param>
        void Clip(Path path);

        /// <summary>
        /// Save the current render queue state
        /// </summary>
        void Save();

        /// <summary>
        /// Restore the last saved render queue state
        /// </summary>
        void Restore();

        /// <summary>
        /// Transform the render queue by the given translation
        /// </summary>
        /// <param name="translation">The translation vector to apply</param>
        void Translate(System.Numerics.Vector2 translation);

        /// <summary>
        /// Transform the render queue by the given translation
        /// </summary>
        /// <param name="x">The x translation</param>
        /// <param name="y">The y translation</param>
        void Translate(float x, float y);

        /// <summary>
        /// Transform the render queue by the given matrix
        /// </summary>
        /// <param name="matrix">The transformation matrix to apply</param>
        void Transform(System.Numerics.Matrix3x2 matrix);

        /// <summary>
        /// Align the artboard to the given fit and alignment
        /// </summary>
        /// <param name="fit">The fit mode to use</param>
        /// <param name="alignment">The alignment to apply</param>
        /// <param name="artboard">The artboard to align</param>
        /// <param name="scaleFactor">Optional scale factor to apply (defaults to 1.0)</param>
        void Align(Fit fit, Alignment alignment, Artboard artboard, float scaleFactor = 1.0f);

        /// <summary>
        /// Align the artboard to the given fit and alignment, with the given frame
        /// </summary>
        /// <param name="fit">The fit mode to use</param>
        /// <param name="alignment">The alignment to apply</param>
        /// <param name="artboard">The artboard to align</param>
        /// <param name="frame">The frame to align within</param>
        /// <param name="scaleFactor">Optional scale factor to apply (defaults to 1.0)</param>
        void Align(Fit fit, Alignment alignment, Artboard artboard, AABB frame, float scaleFactor = 1.0f);


        /// <summary>
        /// Add the render queue commands to an existing command buffer
        /// </summary>
        /// <param name="commandBuffer">The command buffer to add commands to</param>
        /// <param name="release">Whether to release resources after execution (defaults to false)</param>
        void AddToCommandBuffer(UnityEngine.Rendering.CommandBuffer commandBuffer, bool release = false);


    }
}