namespace Rive.Components
{
    /// <summary>
    /// Defines how the artboard should be scaled within the frame when using the Layout fit mode.
    /// </summary>
    public enum LayoutScalingMode
    {
        /// <summary>
        /// This mode will keep the artboard at a constant pixel size, regardless of screen resolution. This means that the artboard may appear larger or smaller depending on the screen resolution.
        /// </summary>
        ConstantPixelSize = 0,

        /// <summary>
        /// This mode will scale the artboard to maintain the same relative size as the original artboard dimensions across different resolutions. This means that the artboard will always appear the same size relative to the screen.
        /// </summary>
        ReferenceArtboardSize = 1,

        /// <summary>
        /// Maintains consistent physical size (in inches) across different devices by accounting for screen DPI. On higher DPI displays, content will appear larger to maintain consistent physical dimensions.
        /// </summary>
        ConstantPhysicalSize = 2,
    }
}
